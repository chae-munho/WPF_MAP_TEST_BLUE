using Map.Services;
using Map.ViewModels;
using System;
using System.Windows;

namespace Map.Views
{
    public partial class MainWindow : Window
    {
        private readonly ApiClient _api = new ApiClient("http://192.168.0.173:5090");
        private MainWindowViewModel? _vm;

        public MainWindow()
        {
            InitializeComponent();

            // MapPanel이 GPS를 ApiClient로 쓰도록 주입
            MapPanelControl?.SetApi(_api);

            // Dialog Service 생성(Owner = this)
            var pwdSvc = new PasswordDialogService(this);
            var alertSvc = new ButtonAlertDialogService(this);
            var dangerSvc = new DangerDialogService(this);
            var adminSettingsSvc = new AdminSettingsDialogService(this);

            _vm = new MainWindowViewModel(_api, pwdSvc, alertSvc, dangerSvc, adminSettingsSvc);

            // MapPanel의 MoveSpeed/MoveDirectionSign 제공
            if (MapPanelControl != null)
                _vm.SetMovementProvider(MapPanelControl);

            DataContext = _vm;

            Closed += (_, __) => _vm?.Dispose();
        }
    }
}
