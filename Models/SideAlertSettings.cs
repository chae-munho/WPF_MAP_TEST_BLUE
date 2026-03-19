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
                VoltageMin = -1,
                VoltageMax = 1000,
                CurrentMin = -1,
                CurrentMax = 1000,
                BatteryMin = -1,
                BatteryMax = 100,
                BatteryTempMin = -1,
                BatteryTempMax = 1000
            };
        }

        public static SideAlertSettings CreateDefaultB()
        {
            return new SideAlertSettings
            {
                VoltageMin = 292,
                VoltageMax = 303,
                CurrentMin = 24,
                CurrentMax = 262,
                BatteryMin = -1,
                BatteryMax = 100,
                BatteryTempMin = 25,
                BatteryTempMax = 32
            };
        }
    }
}