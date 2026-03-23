using Map.Services;
using Map.ViewModels;
using System;
using System.Windows;

namespace Map.Views
{
    public partial class MainWindow : Window
    {
        // 이 주소는 이제 "내가 열 WS 서버 포트" 기준이다.
        // 기차 코드의 ws://관제고정IP:5090/ws/train 과 포트가 같아야 한다.
        private readonly TrainWebSocketServerService _wsServer = new TrainWebSocketServerService("http://192.168.0.173:5090");
        private MainWindowViewModel? _vm;

        public MainWindow()
        {
            InitializeComponent();

            // MapPanel이 최신 GPS 캐시를 읽도록 주입
            MapPanelControl?.SetApi(_wsServer);

            // Dialog Service 생성
            var pwdSvc = new PasswordDialogService(this);
            var alertSvc = new ButtonAlertDialogService(this);
            var dangerSvc = new DangerDialogService(this);
            var adminSettingsSvc = new AdminSettingsDialogService(this);

            _vm = new MainWindowViewModel(_wsServer, pwdSvc, alertSvc, dangerSvc, adminSettingsSvc);

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

        private void OnApiLogReceived(string msg)
        {
            System.Diagnostics.Debug.WriteLine(msg);
        }
    }
}