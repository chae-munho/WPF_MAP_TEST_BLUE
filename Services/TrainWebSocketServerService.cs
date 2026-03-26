using Map.Models;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Map.Services
{
    /// <summary>
    /// 관제 프로그램 내부에서 WebSocket 서버를 열고,
    /// 기차가 보내는 telemetry / position / heartbeat / control_ack 를 수신한다.
    ///
    /// 기존 UI 코드와의 호환을 위해
    /// GetDataAsync / GetNextPosAsync / PostSetDataAsync 형태는 그대로 유지한다.
    /// </summary>
    public sealed class TrainWebSocketServerService : IDisposable
    {
        private readonly HttpListener _listener = new();
        private readonly CancellationTokenSource _cts = new();
        private readonly Task _serverTask;

        private readonly int _port;
        private readonly string _wsPath;

        private readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        private readonly ConcurrentDictionary<string, ClientSession> _sessions = new();

        private readonly object _telemetryLock = new();
        private readonly object _positionLock = new();

        private int[]? _latestTelemetry;
        private DateTime _latestTelemetryAt = DateTime.MinValue;

        private readonly ConcurrentDictionary<int, GpsResponse> _latestPositionsByTrain = new();
        private GpsResponse? _latestPosition;
        private DateTime _latestPositionAt = DateTime.MinValue;

        public event Action<string>? LogReceived;
        private string _baseUrl;
        public string BaseUrl => _baseUrl;

        public TrainWebSocketServerService(string baseUrl)
        {
            _baseUrl = NormalizeBaseUrl(baseUrl);
            var baseUri = new Uri(_baseUrl);

            _port = baseUri.Port;
            _wsPath = "/ws/train/";

            // 외부 기차 프로그램이 접속해야 하므로 + 바인딩 사용
            // 예: http://+:5090/ws/train/
            string prefix = $"http://+:{_port}{_wsPath}";
            _listener.Prefixes.Add(prefix);

            try
            {
                _listener.Start();
                WriteLog($"WebSocket 서버 시작: ws://<관제고정IP>:{_port}{_wsPath.TrimEnd('/')}");
            }
            catch (HttpListenerException ex)
            {
                throw new InvalidOperationException(
                    $"WebSocket 서버 시작 실패: {ex.Message}\n" +
                    $"관리자 권한 또는 URLACL 등록이 필요할 수 있습니다. Prefix=http://+:{_port}{_wsPath}", ex);
            }

            _serverTask = Task.Run(() => AcceptLoopAsync(_cts.Token));
        }

     
        // 기존 UI 호환용 API
      

        /// <summary>
        /// 최신 telemetry 캐시를 기존 DataResponse 형태로 제공
        /// </summary>
        public Task<DataResponse?> GetDataAsync(CancellationToken ct = default)
        {
            lock (_telemetryLock)
            {
                if (_latestTelemetry == null || _latestTelemetry.Length == 0)
                    return Task.FromResult<DataResponse?>(null);

                return Task.FromResult<DataResponse?>(new DataResponse
                {
                    status = "success",
                    argument = string.Join(",", _latestTelemetry)
                });
            }
        }

        /// <summary>
        /// 최신 GPS 캐시를 기존 GpsResponse 형태로 제공
        /// </summary>
        public Task<GpsResponse?> GetNextPosAsync(CancellationToken ct = default)
        {
            lock (_positionLock)
            {
                if (_latestPosition == null)
                    return Task.FromResult<GpsResponse?>(null);

                return Task.FromResult<GpsResponse?>(new GpsResponse
                {
                    lat = _latestPosition.lat,
                    lng = _latestPosition.lng,
                    source = _latestPosition.source
                });
            }
        }

        /// <summary>
        /// 기존 setdata POST 대신 control 메시지를 WebSocket으로 전송
        /// </summary>
        public async Task PostSetDataAsync(int operation, int value, int train, CancellationToken ct = default)
        {
            string commandId = Guid.NewGuid().ToString("N");

            var msg = new WsControlMessage
            {
                Type = "control",
                Train = train,
                Operation = operation,
                Value = value,
                CommandId = commandId,
                Timestamp = DateTime.UtcNow.ToString("O")
            };

            int sentCount = await SendControlToTargetSessionsAsync(msg, train, ct).ConfigureAwait(false);

            if (sentCount <= 0)
                throw new InvalidOperationException($"제어 명령을 전송할 연결된 기차가 없습니다. train={train}");

            WriteLog($"control 전송 완료: train={train}, op={operation}, value={value}, targets={sentCount}");
        }

        // =========================
        // WebSocket 서버 루프
        // =========================

        private async Task AcceptLoopAsync(CancellationToken token)
        {
            while (!token.IsCancellationRequested)
            {
                HttpListenerContext? context = null;

                try
                {
                    context = await _listener.GetContextAsync().ConfigureAwait(false);

                    if (!context.Request.IsWebSocketRequest)
                    {
                        context.Response.StatusCode = 400;
                        context.Response.Close();
                        continue;
                    }

                    if (!string.Equals(context.Request.Url?.AbsolutePath, _wsPath.TrimEnd('/'), StringComparison.OrdinalIgnoreCase) &&
                        !string.Equals(context.Request.Url?.AbsolutePath, _wsPath, StringComparison.OrdinalIgnoreCase))
                    {
                        context.Response.StatusCode = 404;
                        context.Response.Close();
                        continue;
                    }

                    HttpListenerWebSocketContext wsContext = await context.AcceptWebSocketAsync(null).ConfigureAwait(false);
                    WebSocket socket = wsContext.WebSocket;

                    string sessionId = Guid.NewGuid().ToString("N");
                    var session = new ClientSession(sessionId, socket);

                    _sessions[sessionId] = session;

                    WriteLog($"기차 접속됨: session={sessionId}, remote={context.Request.RemoteEndPoint}");

                    _ = Task.Run(() => ReceiveLoopAsync(session, token), token);
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                catch (HttpListenerException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    WriteLog($"AcceptLoop 오류: {ex.Message}");
                }
            }
        }

        private async Task ReceiveLoopAsync(ClientSession session, CancellationToken serverToken)
        {
            byte[] buffer = new byte[8192];

            try
            {
                while (!serverToken.IsCancellationRequested && session.Socket.State == WebSocketState.Open)
                {
                    string json = await ReceiveTextMessageAsync(session.Socket, buffer, serverToken).ConfigureAwait(false);
                    if (string.IsNullOrWhiteSpace(json))
                        break;

                    await ProcessIncomingMessageAsync(session, json, serverToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (WebSocketException ex)
            {
                WriteLog($"세션 수신 종료: session={session.SessionId}, error={ex.Message}");
            }
            catch (Exception ex)
            {
                WriteLog($"세션 처리 오류: session={session.SessionId}, error={ex.Message}");
            }
            finally
            {
                _sessions.TryRemove(session.SessionId, out _);

                try
                {
                    if (session.Socket.State == WebSocketState.Open || session.Socket.State == WebSocketState.CloseReceived)
                    {
                        await session.Socket.CloseAsync(
                            WebSocketCloseStatus.NormalClosure,
                            "server closing",
                            CancellationToken.None).ConfigureAwait(false);
                    }
                }
                catch
                {
                }

                session.Dispose();
                WriteLog($"기차 연결 종료: session={session.SessionId}");
            }
        }

        private async Task ProcessIncomingMessageAsync(ClientSession session, string json, CancellationToken token)
        {
            try
            {
                using JsonDocument doc = JsonDocument.Parse(json);

                if (!doc.RootElement.TryGetProperty("Type", out JsonElement typeEl) &&
                    !doc.RootElement.TryGetProperty("type", out typeEl))
                {
                    WriteLog($"type 없는 메시지 무시: {json}");
                    return;
                }

                string type = typeEl.GetString() ?? "";

                switch (type)
                {
                    case "hello":
                        {
                            WsHelloMessage? msg = JsonSerializer.Deserialize<WsHelloMessage>(json, _jsonOptions);
                            if (msg != null)
                            {
                                session.ClientName = msg.ClientName;
                                session.Role = msg.Role;
                                WriteLog($"hello 수신: session={session.SessionId}, role={msg.Role}, name={msg.ClientName}");
                            }
                            break;
                        }

                    case "telemetry":
                        {
                            WsTelemetryMessage? msg = JsonSerializer.Deserialize<WsTelemetryMessage>(json, _jsonOptions);
                            if (msg != null && msg.Data != null && msg.Data.Length > 0)
                            {
                                lock (_telemetryLock)
                                {
                                    _latestTelemetry = (int[])msg.Data.Clone();
                                    _latestTelemetryAt = DateTime.Now;
                                }

                                // 현재 패킷 구조상 0번, 47번 인덱스는 각 면/열차 ID
                                if (msg.Data.Length >= 48)
                                {
                                    int trainA = msg.Data[0];
                                    int trainB = msg.Data[47];

                                    if (trainA > 0) session.ObservedTrainIds.TryAdd(trainA, 0);
                                    if (trainB > 0) session.ObservedTrainIds.TryAdd(trainB, 0);
                                }

                                WriteLog($"telemetry 수신: session={session.SessionId}, len={msg.Data.Length}");
                            }
                            break;
                        }

                    case "position":
                        {
                            WsPositionMessage? msg = JsonSerializer.Deserialize<WsPositionMessage>(json, _jsonOptions);
                            if (msg != null)
                            {
                                session.ObservedTrainIds.TryAdd(msg.Train, 0);

                                var gps = new GpsResponse
                                {
                                    lat = msg.Lat,
                                    lng = msg.Lng,
                                    source = string.IsNullOrWhiteSpace(msg.Source) ? "ws" : msg.Source
                                };

                                _latestPositionsByTrain[msg.Train] = gps;

                                lock (_positionLock)
                                {
                                    _latestPosition = gps;
                                    _latestPositionAt = DateTime.Now;
                                }

                                WriteLog($"position 수신: train={msg.Train}, lat={msg.Lat}, lng={msg.Lng}, source={gps.source}");
                            }
                            break;
                        }

                    case "heartbeat":
                        {
                            WsHeartbeatMessage? msg = JsonSerializer.Deserialize<WsHeartbeatMessage>(json, _jsonOptions);
                            session.LastHeartbeatAt = DateTime.Now;
                            WriteLog($"heartbeat 수신: session={session.SessionId}");
                            break;
                        }

                    case "control_ack":
                        {
                            WsControlAckMessage? msg = JsonSerializer.Deserialize<WsControlAckMessage>(json, _jsonOptions);
                            if (msg != null)
                            {
                                WriteLog($"control_ack 수신: train={msg.Train}, op={msg.Operation}, value={msg.Value}, result={msg.Result}, cmdId={msg.CommandId}");
                            }
                            break;
                        }

                    case "pong":
                        {
                            WriteLog($"pong 수신: session={session.SessionId}");
                            break;
                        }

                    default:
                        WriteLog($"알 수 없는 메시지 수신: type={type}");
                        break;
                }
            }
            catch (Exception ex)
            {
                WriteLog($"메시지 처리 실패: {ex.Message}, json={json}");
            }
        }

       
        // control 전송
     

        private async Task<int> SendControlToTargetSessionsAsync(WsControlMessage msg, int train, CancellationToken ct)
        {
            var targets = GetTargetSessions(train);
            int successCount = 0;

            foreach (var session in targets)
            {
                bool ok = await SendToSessionAsync(session, msg, ct).ConfigureAwait(false);
                if (ok) successCount++;
            }

            return successCount;
        }

        /// <summary>
        /// train ID를 이미 관찰한 세션이 있으면 그 세션으로만 보내고,
        /// 없으면 연결된 모든 세션으로 브로드캐스트한다.
        /// </summary>
        private ClientSession[] GetTargetSessions(int train)
        {
            var all = _sessions.Values.ToArray();
            if (all.Length == 0)
                return Array.Empty<ClientSession>();

            var matched = all
                .Where(s => s.Socket.State == WebSocketState.Open && s.ObservedTrainIds.ContainsKey(train))
                .ToArray();

            if (matched.Length > 0)
                return matched;

            return all
                .Where(s => s.Socket.State == WebSocketState.Open)
                .ToArray();
        }

        private async Task<bool> SendToSessionAsync<T>(ClientSession session, T payload, CancellationToken ct)
        {
            if (session.Socket.State != WebSocketState.Open)
                return false;

            string json = JsonSerializer.Serialize(payload, _jsonOptions);
            byte[] bytes = Encoding.UTF8.GetBytes(json);

            await session.SendLock.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                if (session.Socket.State != WebSocketState.Open)
                    return false;

                await session.Socket.SendAsync(
                    new ArraySegment<byte>(bytes),
                    WebSocketMessageType.Text,
                    true,
                    ct).ConfigureAwait(false);

                return true;
            }
            catch (Exception ex)
            {
                WriteLog($"전송 실패: session={session.SessionId}, error={ex.Message}");
                return false;
            }
            finally
            {
                session.SendLock.Release();
            }
        }

        // =========================
        // 유틸
        // =========================
        private static string NormalizeBaseUrl(string baseUrl)
        {
            if (string.IsNullOrWhiteSpace(baseUrl))
                throw new ArgumentException("baseUrl is required.", nameof(baseUrl));

            return baseUrl.Trim().TrimEnd('/');
        }
        public void UpdateBaseUrl(string newBaseUrl)
        {
            string normalized = NormalizeBaseUrl(newBaseUrl);
            var uri = new Uri(normalized);

            if (uri.Port != _port)
                throw new InvalidOperationException(
                    $"현재 실행 중인 WebSocket 서버 포트는 {_port} 입니다. " +
                    $"포트 변경({uri.Port})은 런타임 즉시 반영할 수 없습니다. 프로그램 재시작이 필요합니다.");

            _baseUrl = normalized;
            WriteLog($"BaseUrl 변경 적용: {_baseUrl}");
        }

        private static async Task<string> ReceiveTextMessageAsync(WebSocket socket, byte[] buffer, CancellationToken token)
        {
            using MemoryStream ms = new();

            while (true)
            {
                WebSocketReceiveResult result = await socket.ReceiveAsync(
                    new ArraySegment<byte>(buffer),
                    token).ConfigureAwait(false);

                if (result.MessageType == WebSocketMessageType.Close)
                    return string.Empty;

                ms.Write(buffer, 0, result.Count);

                if (result.EndOfMessage)
                    break;
            }

            return Encoding.UTF8.GetString(ms.ToArray());
        }

        private void WriteLog(string message)
        {
            LogReceived?.Invoke($"[WS] {message}");
        }

        public void Dispose()
        {
            try
            {
                _cts.Cancel();
            }
            catch
            {
            }

            foreach (var session in _sessions.Values)
            {
                try
                {
                    session.Dispose();
                }
                catch
                {
                }
            }

            _sessions.Clear();

            try
            {
                if (_listener.IsListening)
                    _listener.Stop();
            }
            catch
            {
            }

            try
            {
                _listener.Close();
            }
            catch
            {
            }

            try
            {
                _serverTask.Wait(1000);
            }
            catch
            {
            }

            _cts.Dispose();
        }

       
        // 내부 세션 클래스
      

        private sealed class ClientSession : IDisposable
        {
            public string SessionId { get; }
            public WebSocket Socket { get; }
            public string ClientName { get; set; } = "";
            public string Role { get; set; } = "";
            public DateTime LastHeartbeatAt { get; set; } = DateTime.MinValue;

            public ConcurrentDictionary<int, byte> ObservedTrainIds { get; } = new();
            public SemaphoreSlim SendLock { get; } = new(1, 1);

            public ClientSession(string sessionId, WebSocket socket)
            {
                SessionId = sessionId;
                Socket = socket;
            }

            public void Dispose()
            {
                try { SendLock.Dispose(); } catch { }

                try
                {
                    Socket.Dispose();
                }
                catch
                {
                }
            }
        }
    }
}