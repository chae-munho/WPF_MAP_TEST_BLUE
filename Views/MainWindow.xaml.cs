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


        public MainWindow()
        {
            InitializeComponent();

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

                    CloseCameraPopup();

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

        //카메라 팝업 관련 기능 시작
        private void OnVideoFrameReceived(WsVideoFrameMessage frame)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    if (frame == null || string.IsNullOrWhiteSpace(frame.ImageBase64))
                        return;

                    bool needNewPopup =
                        _cameraPopup == null ||
                        _currentPopupTrain != frame.Train ||
                        _currentPopupCar != frame.CarNo;

                    if (needNewPopup)
                    {
                        ShowOrSwitchCameraPopup(frame.Train, frame.CarNo);
                    }

                    _cameraPopup?.UpdateFrame(frame);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"관제 영상 처리 실패: {ex.Message}");
                }
            }));
        }
        private void OnVideoStopReceived(WsVideoStopMessage msg)
        {
           
            // stop 메시지가 와도 자동으로 팝업 닫지 않음
            // 다음 새 호출frame이 올 때만 교체
            System.Diagnostics.Debug.WriteLine($"[VIDEO] stop received train={msg.Train}");
        }
        private void ShowOrSwitchCameraPopup(int trainNo, int carNo)
        {
            CloseCameraPopup();

            var popup = new CameraPopup
            {
                Owner = this
            };

            popup.Closed += (_, _) =>
            {
                if (ReferenceEquals(_cameraPopup, popup))
                {
                    _cameraPopup = null;
                    _currentPopupTrain = 0;
                    _currentPopupCar = 0;
                }
            };

            _cameraPopup = popup;
            _currentPopupTrain = trainNo;
            _currentPopupCar = carNo;

            _cameraPopup.ShowCamera(trainNo, carNo);
            _cameraPopup.Show();
            _cameraPopup.Activate();
        }
        private void CloseCameraPopup()
        {
            try
            {
                if (_cameraPopup != null)
                {
                    var popup = _cameraPopup;
                    _cameraPopup = null;
                    _currentPopupTrain = 0;
                    _currentPopupCar = 0;
                    popup.Close();
                }
            }
            catch
            {
            }
        }


    }
}