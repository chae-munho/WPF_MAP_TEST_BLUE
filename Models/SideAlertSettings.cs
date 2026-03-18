namespace Map.Models
{
    public class SideAlertSettings
    {
        public int VoltageMin { get; set; }
        public int VoltageMax { get; set; }

        public int CurrentMin { get; set; }
        public int CurrentMax { get; set; }

        public int BatteryMin { get; set; }
        public int BatteryMax { get; set; }

        public int BatteryTempMin { get; set; }
        public int BatteryTempMax { get; set; }

        public SideAlertSettings Clone()
        {
            return new SideAlertSettings
            {
                VoltageMin = VoltageMin,
                VoltageMax = VoltageMax,
                CurrentMin = CurrentMin,
                CurrentMax = CurrentMax,
                BatteryMin = BatteryMin,
                BatteryMax = BatteryMax,
                BatteryTempMin = BatteryTempMin,
                BatteryTempMax = BatteryTempMax
            };
        }

        public static SideAlertSettings CreateDefaultA()
        {
            return new SideAlertSettings
            {
                VoltageMin = 304,
                VoltageMax = 304,
                CurrentMin = 263,
                CurrentMax = 264,
                BatteryMin = 19,
                BatteryMax = 20,
                BatteryTempMin = 60,
                BatteryTempMax = 62
            };
        }

        public static SideAlertSettings CreateDefaultB()
        {
            return new SideAlertSettings
            {
                VoltageMin = 304,
                VoltageMax = 304,
                CurrentMin = 263,
                CurrentMax = 264,
                BatteryMin = 19,
                BatteryMax = 20,
                BatteryTempMin = 60,
                BatteryTempMax = 62
            };
        }
    }
}