using Map.Models;
using Map.Services;
using Map.Services.Interfaces;
using Map.ViewModels;
using System;
using System.Windows;
using Map.Popups;

namespace Map.Views
{
    public partial class MainWindow : Window
    {
        private readonly TrainWebSocketServerService _wsServer;
        private readonly IAppSettingsService _appSettingsService;
        private readonly AppSettings _initialSettings;
        private MainWindowViewModel? _vm;

        private CameraPopup? _cameraPopup;
        private int _currentPopupTrain = 0;
        private int _currentPopupCar = 0;

        // 사용자가 X로 닫았는지 기억하는 플래그
        private bool _cameraPopupDismissed = false;

        // 영상 지연 방지용 필드
        private WsVideoFrameMessage? _latestVideoFrame;
        private readonly object _videoFrameLock = new();
        private System.Windows.Threading.DispatcherTimer? _videoUiTimer;

        public MainWindow()
        {
            InitializeComponent();

            _videoUiTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(120)
            };
            _videoUiTimer.Tick += VideoUiTimer_Tick;
            _videoUiTimer.Start();

            _appSettingsService = new AppSettingsService();
            _initialSettings = _appSettingsService.Load();

            _wsServer = new TrainWebSocketServerService(_initialSettings.ServerBaseUrl);

            // MapPanel이 최신 GPS 캐시를 읽도록 주입
            MapPanelControl?.SetApi(_wsServer);

            var pwdSvc = new PasswordDialogService(this);
            var alertSvc = new ButtonAlertDialogService(this);
            var dangerSvc = new DangerDialogService(this);
            var adminSettingsSvc = new AdminSettingsDialogService(this);

            _vm = new MainWindowViewModel(
                _wsServer,
                pwdSvc,
                alertSvc,
                dangerSvc,
                adminSettingsSvc,
                _appSettingsService,
                _initialSettings,
                ApplyServerBaseUrl);

            if (MapPanelControl != null)
                _vm.SetMovementProvider(MapPanelControl);

            DataContext = _vm;

            _wsServer.LogReceived += OnApiLogReceived;
            _wsServer.VideoFrameReceived += OnVideoFrameReceived;
            _wsServer.VideoStopReceived += OnVideoStopReceived;

            Closed += (_, __) =>
            {
                try
                {
                    _wsServer.LogReceived -= OnApiLogReceived;
                    _wsServer.VideoFrameReceived -= OnVideoFrameReceived;
                    _wsServer.VideoStopReceived -= OnVideoStopReceived;

                    if (_videoUiTimer != null)
                    {
                        _videoUiTimer.Stop();
                        _videoUiTimer.Tick -= VideoUiTimer_Tick;
                        _videoUiTimer = null;
                    }

                    CloseCameraPopup(forceResetRequest: true);

                    _vm?.Dispose();
                    _wsServer.Dispose();
                }
                catch
                {
                }
            };
        }

        private void ApplyServerBaseUrl(string newBaseUrl)
        {
            _wsServer.UpdateBaseUrl(newBaseUrl);

            // 같은 인스턴스를 쓰고 있으면 사실상 재주입이 꼭 필요하진 않지만,
            // 명시적으로 다시 넣어주면 더 안전함
            MapPanelControl?.SetApi(_wsServer);
        }

        private void OnApiLogReceived(string msg)
        {
            System.Diagnostics.Debug.WriteLine(msg);
        }

        // 카메라 팝업 관련 기능 시작

        // 영상 프레임이 들어오면 바로 UI를 갱신하지 않고
        // 최신 프레임만 저장해 둠
        private void OnVideoFrameReceived(WsVideoFrameMessage frame)
        {
            if (frame == null || string.IsNullOrWhiteSpace(frame.ImageBase64))
                return;

            lock (_videoFrameLock)
            {
                _latestVideoFrame = frame;
            }
        }

        // 일정 주기마다 최신 프레임 1개만 화면에 반영
        private void VideoUiTimer_Tick(object? sender, EventArgs e)
        {
            WsVideoFrameMessage? frame = null;

            lock (_videoFrameLock)
            {
                frame = _latestVideoFrame;
                _latestVideoFrame = null;
            }

            if (frame == null || string.IsNullOrWhiteSpace(frame.ImageBase64))
                return;

            try
            {
                bool isNewRequest =
                    _currentPopupTrain != frame.Train ||
                    _currentPopupCar != frame.CarNo;

                // 새로운 호출이 오면 다시 팝업 허용
                if (isNewRequest)
                {
                    _cameraPopupDismissed = false;
                    ShowOrSwitchCameraPopup(frame.Train, frame.CarNo);
                }
                else
                {
                    // 같은 호출인데 사용자가 X로 닫은 상태면 다시 띄우지 않음
                    if (_cameraPopup == null && _cameraPopupDismissed)
                        return;

                    // 같은 호출인데 팝업이 없고, 수동 닫힘도 아니라면 복구
                    if (_cameraPopup == null && !_cameraPopupDismissed)
                    {
                        ShowOrSwitchCameraPopup(frame.Train, frame.CarNo);
                    }
                }

                _cameraPopup?.UpdateFrame(frame);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"관제 영상 처리 실패: {ex.Message}");
            }
        }

        private void OnVideoStopReceived(WsVideoStopMessage msg)
        {
            // 유지형 정책:
            // stop 메시지가 와도 자동으로 팝업 닫지 않음
            // 다음 새 호출 frame이 올 때만 교체
            System.Diagnostics.Debug.WriteLine($"[VIDEO] stop received train={msg.Train}");
        }

        private void ShowOrSwitchCameraPopup(int trainNo, int carNo)
        {
            CloseCameraPopup(forceResetRequest: false);

            var popup = new CameraPopup
            {
                Owner = this
            };

            popup.Closed += (_, _) =>
            {
                if (ReferenceEquals(_cameraPopup, popup))
                {
                    _cameraPopup = null;
                    _cameraPopupDismissed = true; // 사용자가 X로 닫음
                }
            };

            _cameraPopup = popup;
            _currentPopupTrain = trainNo;
            _currentPopupCar = carNo;
            _cameraPopupDismissed = false;

            _cameraPopup.ShowCamera(trainNo, carNo);
            _cameraPopup.Show();
            _cameraPopup.Activate();
        }

        private void CloseCameraPopup(bool forceResetRequest)
        {
            try
            {
                if (_cameraPopup != null)
                {
                    var popup = _cameraPopup;
                    _cameraPopup = null;
                    popup.Close();
                }

                if (forceResetRequest)
                {
                    _currentPopupTrain = 0;
                    _currentPopupCar = 0;
                    _cameraPopupDismissed = false;
                }
            }
            catch
            {
            }
        }
    }
}