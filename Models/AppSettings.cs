namespace Map.Models
{
    public class AppSettings
    {
        public string ServerBaseUrl { get; set; } = "http://192.168.0.173:5090";

        public SideAlertSettings ASettings { get; set; } = SideAlertSettings.CreateDefaultA();
        public SideAlertSettings BSettings { get; set; } = SideAlertSettings.CreateDefaultB();
    }
}