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
    public sealed class TrainVideoWebSocketServerService : IDisposable
    {
        private readonly HttpListener _listener = new();
        private readonly CancellationTokenSource _cts = new();
        private readonly Task _serverTask;

        private readonly int _port;
        private readonly string _wsPath;
        private string _baseUrl;

        private readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private readonly ConcurrentDictionary<string, ClientSession> _sessions = new();
        private readonly ConcurrentDictionary<string, LatestVideoFrame> _latestFrames = new();

        public event Action<string>? LogReceived;

        public string BaseUrl => _baseUrl;

        public TrainVideoWebSocketServerService(string baseUrl)
        {
            _baseUrl = NormalizeBaseUrl(baseUrl);
            var baseUri = new Uri(_baseUrl);

            _port = baseUri.Port;
            _wsPath = "/ws/train-video/";

            string prefix = $"http://+:{_port}{_wsPath}";
            _listener.Prefixes.Add(prefix);

            try
            {
                _listener.Start();
                WriteLog($"영상 WebSocket 서버 시작: ws://<관제고정IP>:{_port}{_wsPath.TrimEnd('/')}");
            }
            catch (HttpListenerException ex)
            {
                throw new InvalidOperationException(
                    $"영상 WebSocket 서버 시작 실패: {ex.Message}\n" +
                    $"관리자 권한 또는 URLACL 등록이 필요할 수 있습니다. Prefix=http://+:{_port}{_wsPath}", ex);
            }

            _serverTask = Task.Run(() => AcceptLoopAsync(_cts.Token));
        }

        public bool TryGetLatestFrame(int train, int carNo, out byte[] jpegBytes, out long sequence)
        {
            string key = MakeFrameKey(train, carNo);

            if (_latestFrames.TryGetValue(key, out LatestVideoFrame? frame))
            {
                jpegBytes = frame.JpegBytes;
                sequence = frame.Sequence;
                return true;
            }

            jpegBytes = Array.Empty<byte>();
            sequence = 0;
            return false;
        }

        public void UpdateBaseUrl(string newBaseUrl)
        {
            string normalized = NormalizeBaseUrl(newBaseUrl);
            var uri = new Uri(normalized);

            if (uri.Port != _port)
                throw new InvalidOperationException(
                    $"현재 실행 중인 영상 WebSocket 서버 포트는 {_port} 입니다. " +
                    $"포트 변경({uri.Port})은 런타임 즉시 반영할 수 없습니다. 프로그램 재시작이 필요합니다.");

            _baseUrl = normalized;
            WriteLog($"영상 BaseUrl 변경 적용: {_baseUrl}");
        }

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

                    WriteLog($"영상 송신 기차 접속됨: session={sessionId}, remote={context.Request.RemoteEndPoint}");

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
                    WriteLog($"영상 AcceptLoop 오류: {ex.Message}");
                }
            }
        }

        private async Task ReceiveLoopAsync(ClientSession session, CancellationToken serverToken)
        {
            byte[] buffer = new byte[1024 * 1024];

            try
            {
                while (!serverToken.IsCancellationRequested && session.Socket.State == WebSocketState.Open)
                {
                    string json = await ReceiveTextMessageAsync(session.Socket, buffer, serverToken).ConfigureAwait(false);
                    if (string.IsNullOrWhiteSpace(json))
                        break;

                    ProcessIncomingMessage(session, json);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch (WebSocketException ex)
            {
                WriteLog($"영상 세션 수신 종료: session={session.SessionId}, error={ex.Message}");
            }
            catch (Exception ex)
            {
                WriteLog($"영상 세션 처리 오류: session={session.SessionId}, error={ex.Message}");
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
                WriteLog($"영상 기차 연결 종료: session={session.SessionId}");
            }
        }

        private void ProcessIncomingMessage(ClientSession session, string json)
        {
            try
            {
                using JsonDocument doc = JsonDocument.Parse(json);

                if (!doc.RootElement.TryGetProperty("Type", out JsonElement typeEl) &&
                    !doc.RootElement.TryGetProperty("type", out typeEl))
                {
                    WriteLog("영상 type 없는 메시지 무시");
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
                                session.Train = msg.Train;
                                WriteLog($"영상 hello 수신: train={msg.Train}, session={session.SessionId}, role={msg.Role}, name={msg.ClientName}");
                            }
                            break;
                        }

                    case "video_frame":
                        {
                            WsVideoFrameMessage? msg = JsonSerializer.Deserialize<WsVideoFrameMessage>(json, _jsonOptions);
                            if (msg == null || msg.Train <= 0 || msg.CarNo <= 0 || string.IsNullOrWhiteSpace(msg.ImageBase64))
                                return;
                            WriteLog($"video_frame 수신: train={msg.Train}, car={msg.CarNo}");
                            byte[] jpegBytes;
                            try
                            {
                                jpegBytes = Convert.FromBase64String(msg.ImageBase64);
                            }
                            catch
                            {
                                WriteLog($"video_frame base64 디코딩 실패: train={msg.Train}, car={msg.CarNo}");
                                return;
                            }

                            string key = MakeFrameKey(msg.Train, msg.CarNo);

                            _latestFrames.AddOrUpdate(
                                key,
                                _ => new LatestVideoFrame(jpegBytes, 1, DateTime.UtcNow),
                                (_, old) => new LatestVideoFrame(jpegBytes, old.Sequence + 1, DateTime.UtcNow));

                            break;
                        }

                    case "video_stop":
                        {
                            WsVideoStopMessage? msg = JsonSerializer.Deserialize<WsVideoStopMessage>(json, _jsonOptions);
                            if (msg != null && msg.Train > 0 && msg.CarNo > 0)
                            {
                                string key = MakeFrameKey(msg.Train, msg.CarNo);
                                _latestFrames.TryRemove(key, out _);

                                WriteLog($"video_stop 수신: train={msg.Train}, car={msg.CarNo}");
                            }
                            break;
                        }

                    default:
                        WriteLog($"알 수 없는 영상 메시지 수신: type={type}");
                        break;
                }
            }
            catch (Exception ex)
            {
                WriteLog($"영상 메시지 처리 실패: {ex.Message}");
            }
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

        private static string NormalizeBaseUrl(string baseUrl)
        {
            if (string.IsNullOrWhiteSpace(baseUrl))
                throw new ArgumentException("baseUrl is required.", nameof(baseUrl));

            return baseUrl.Trim().TrimEnd('/');
        }

        private static string MakeFrameKey(int train, int carNo) => $"{train}_{carNo}";

        private void WriteLog(string message)
        {
            LogReceived?.Invoke($"[VIDEO-WS] {message}");
        }

        public void Dispose()
        {
            try { _cts.Cancel(); } catch { }

            foreach (var session in _sessions.Values)
            {
                try { session.Dispose(); } catch { }
            }

            _sessions.Clear();
            _latestFrames.Clear();

            try
            {
                if (_listener.IsListening)
                    _listener.Stop();
            }
            catch { }

            try { _listener.Close(); } catch { }

            try { _serverTask.Wait(1000); } catch { }

            _cts.Dispose();
        }

        private sealed class ClientSession : IDisposable
        {
            public string SessionId { get; }
            public WebSocket Socket { get; }
            public int Train { get; set; }
            public string ClientName { get; set; } = "";
            public string Role { get; set; } = "";

            public ClientSession(string sessionId, WebSocket socket)
            {
                SessionId = sessionId;
                Socket = socket;
            }

            public void Dispose()
            {
                try { Socket.Dispose(); } catch { }
            }
        }

        private sealed class LatestVideoFrame
        {
            public byte[] JpegBytes { get; }
            public long Sequence { get; }
            public DateTime ReceivedAtUtc { get; }

            public LatestVideoFrame(byte[] jpegBytes, long sequence, DateTime receivedAtUtc)
            {
                JpegBytes = jpegBytes;
                Sequence = sequence;
                ReceivedAtUtc = receivedAtUtc;
            }
        }
    }
}