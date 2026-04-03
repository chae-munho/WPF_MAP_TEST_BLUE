using Map.Models;
using Map.Services;
using Map.Services.Interfaces;
using Map.ViewModels;
using System;
using System.Windows;

namespace Map.Views
{
    public partial class MainWindow : Window
    {
        private readonly TrainWebSocketServerService _wsServer;
        private readonly TrainVideoWebSocketServerService _videoWsServer;
        private readonly IAppSettingsService _appSettingsService;
        private readonly AppSettings _initialSettings;
        private MainWindowViewModel? _vm;
        private CameraPopupService? _cameraPopupService;

        public MainWindow()
        {
            InitializeComponent();

            _appSettingsService = new AppSettingsService();
            _initialSettings = _appSettingsService.Load();

            _wsServer = new TrainWebSocketServerService(_initialSettings.ServerBaseUrl);
            _videoWsServer = new TrainVideoWebSocketServerService(_initialSettings.VideoServerBaseUrl);

            // MapPanel이 최신 GPS 캐시를 읽도록 주입
            MapPanelControl?.SetApi(_wsServer);

            var pwdSvc = new PasswordDialogService(this);
            var alertSvc = new ButtonAlertDialogService(this);
            var dangerSvc = new DangerDialogService(this);
            var adminSettingsSvc = new AdminSettingsDialogService(this);
            _cameraPopupService = new CameraPopupService(this, _videoWsServer);

            _vm = new MainWindowViewModel(
                _wsServer,
                _videoWsServer,
                pwdSvc,
                alertSvc,
                dangerSvc,
                adminSettingsSvc,
                _cameraPopupService,
                _appSettingsService,
                _initialSettings,
                ApplyServerBaseUrls);

            if (MapPanelControl != null)
                _vm.SetMovementProvider(MapPanelControl);

            DataContext = _vm;

            _wsServer.LogReceived += OnApiLogReceived;
            _videoWsServer.LogReceived += OnApiLogReceived;

            Closed += (_, __) =>
            {
                try
                {
                    _wsServer.LogReceived -= OnApiLogReceived;
                    _videoWsServer.LogReceived -= OnApiLogReceived;

                    _cameraPopupService?.CloseAll();
                    _vm?.Dispose();
                    _videoWsServer.Dispose();
                    _wsServer.Dispose();
                }
                catch
                {
                }
            };
        }

        private void ApplyServerBaseUrls(string newBaseUrl, string newVideoBaseUrl)
        {
            _wsServer.UpdateBaseUrl(newBaseUrl);
            _videoWsServer.UpdateBaseUrl(newVideoBaseUrl);

            // 같은 인스턴스를 쓰고 있으면 사실상 재주입이 꼭 필요하진 않지만,
            // 명시적으로 다시 넣어주면 더 안전함
            MapPanelControl?.SetApi(_wsServer);
        }

        private void OnApiLogReceived(string msg)
        {
            System.Diagnostics.Debug.WriteLine(msg);
        }
    }
}