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
        private readonly IAppSettingsService _appSettingsService;
        private readonly AppSettings _initialSettings;
        private MainWindowViewModel? _vm;

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

            Closed += (_, __) =>
            {
                try
                {
                    _wsServer.LogReceived -= OnApiLogReceived;
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
    }
}