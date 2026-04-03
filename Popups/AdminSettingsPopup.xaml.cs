using Map.Models;
using System;
using System.Windows;

namespace Map.Views.Popups
{
    public partial class AdminSettingsPopup : Window
    {
        public string ServerBaseUrl { get; private set; } = "";
        public string VideoServerBaseUrl { get; private set; } = "";
        public SideAlertSettings BSettings { get; private set; }
        public SideAlertSettings ASettings { get; private set; }

        public AdminSettingsPopup(
            string serverBaseUrl,
            string videoServerBaseUrl,
            SideAlertSettings bSettings,
            SideAlertSettings aSettings)
        {
            InitializeComponent();

            ServerBaseUrl = serverBaseUrl;
            VideoServerBaseUrl = videoServerBaseUrl;
            BSettings = bSettings;
            ASettings = aSettings;

            LoadSettingsToUi();
        }

        private void LoadSettingsToUi()
        {
            ServerBaseUrlTextBox.Text = ServerBaseUrl;
            VideoServerBaseUrlTextBox.Text = VideoServerBaseUrl;

            BVoltageMinTextBox.Text = BSettings.VoltageMin.ToString();
            BVoltageMaxTextBox.Text = BSettings.VoltageMax.ToString();
            BCurrentMinTextBox.Text = BSettings.CurrentMin.ToString();
            BCurrentMaxTextBox.Text = BSettings.CurrentMax.ToString();
            BBatteryMinTextBox.Text = BSettings.BatteryMin.ToString();
            BBatteryMaxTextBox.Text = BSettings.BatteryMax.ToString();
            BBatteryTempMinTextBox.Text = BSettings.BatteryTempMin.ToString();
            BBatteryTempMaxTextBox.Text = BSettings.BatteryTempMax.ToString();

            AVoltageMinTextBox.Text = ASettings.VoltageMin.ToString();
            AVoltageMaxTextBox.Text = ASettings.VoltageMax.ToString();
            ACurrentMinTextBox.Text = ASettings.CurrentMin.ToString();
            ACurrentMaxTextBox.Text = ASettings.CurrentMax.ToString();
            ABatteryMinTextBox.Text = ASettings.BatteryMin.ToString();
            ABatteryMaxTextBox.Text = ASettings.BatteryMax.ToString();
            ABatteryTempMinTextBox.Text = ASettings.BatteryTempMin.ToString();
            ABatteryTempMaxTextBox.Text = ASettings.BatteryTempMax.ToString();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (!TryReadSettings())
                return;

            DialogResult = true;
            Close();
        }

        private bool TryReadSettings()
        {
            if (!TryNormalizeBaseUrl(ServerBaseUrlTextBox.Text.Trim(), out string normalizedServerBaseUrl))
            {
                MessageBox.Show("일반 서버 주소 형식이 올바르지 않습니다.\n예: http://192.168.0.173:5090",
                    "입력 오류", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (!TryNormalizeBaseUrl(VideoServerBaseUrlTextBox.Text.Trim(), out string normalizedVideoServerBaseUrl))
            {
                MessageBox.Show("영상 서버 주소 형식이 올바르지 않습니다.\n예: http://192.168.0.173:5090",
                    "입력 오류", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (!TryParseSideSettings(
                    BVoltageMinTextBox.Text, BVoltageMaxTextBox.Text,
                    BCurrentMinTextBox.Text, BCurrentMaxTextBox.Text,
                    BBatteryMinTextBox.Text, BBatteryMaxTextBox.Text,
                    BBatteryTempMinTextBox.Text, BBatteryTempMaxTextBox.Text,
                    out SideAlertSettings bSettings))
            {
                MessageBox.Show("B면 설정값이 올바르지 않습니다.", "입력 오류", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (!TryParseSideSettings(
                    AVoltageMinTextBox.Text, AVoltageMaxTextBox.Text,
                    ACurrentMinTextBox.Text, ACurrentMaxTextBox.Text,
                    ABatteryMinTextBox.Text, ABatteryMaxTextBox.Text,
                    ABatteryTempMinTextBox.Text, ABatteryTempMaxTextBox.Text,
                    out SideAlertSettings aSettings))
            {
                MessageBox.Show("A면 설정값이 올바르지 않습니다.", "입력 오류", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            ServerBaseUrl = normalizedServerBaseUrl;
            VideoServerBaseUrl = normalizedVideoServerBaseUrl;
            BSettings = bSettings;
            ASettings = aSettings;
            return true;
        }

        private static bool TryNormalizeBaseUrl(string inputUrl, out string normalizedBaseUrl)
        {
            normalizedBaseUrl = "";

            if (!Uri.TryCreate(inputUrl, UriKind.Absolute, out Uri? uri) ||
                (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
            {
                return false;
            }

            normalizedBaseUrl = uri.GetLeftPart(UriPartial.Authority);
            return true;
        }

        private bool TryParseSideSettings(
            string voltageMinText, string voltageMaxText,
            string currentMinText, string currentMaxText,
            string batteryMinText, string batteryMaxText,
            string batteryTempMinText, string batteryTempMaxText,
            out SideAlertSettings settings)
        {
            settings = new SideAlertSettings();

            if (!int.TryParse(voltageMinText, out int voltageMin)) return false;
            if (!int.TryParse(voltageMaxText, out int voltageMax)) return false;
            if (!int.TryParse(currentMinText, out int currentMin)) return false;
            if (!int.TryParse(currentMaxText, out int currentMax)) return false;
            if (!int.TryParse(batteryMinText, out int batteryMin)) return false;
            if (!int.TryParse(batteryMaxText, out int batteryMax)) return false;
            if (!int.TryParse(batteryTempMinText, out int batteryTempMin)) return false;
            if (!int.TryParse(batteryTempMaxText, out int batteryTempMax)) return false;

            if (voltageMin > voltageMax) return false;
            if (currentMin > currentMax) return false;
            if (batteryMin > batteryMax) return false;
            if (batteryTempMin > batteryTempMax) return false;

            settings.VoltageMin = voltageMin;
            settings.VoltageMax = voltageMax;
            settings.CurrentMin = currentMin;
            settings.CurrentMax = currentMax;
            settings.BatteryMin = batteryMin;
            settings.BatteryMax = batteryMax;
            settings.BatteryTempMin = batteryTempMin;
            settings.BatteryTempMax = batteryTempMax;

            return true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}