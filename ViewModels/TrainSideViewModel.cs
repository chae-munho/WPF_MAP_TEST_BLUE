using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Windows.Media;

namespace Map.ViewModels
{
    public partial class TrainSideViewModel : ObservableObject
    {
     
        // 숫자 데이터
     
        [ObservableProperty] private int voltage;
        [ObservableProperty] private int current;
        [ObservableProperty] private int battery;
        [ObservableProperty] private int batteryTemp;
        [ObservableProperty] private int motorOutput;
        [ObservableProperty] private int motorSpeed;

      
        // 표시용 (속도계)
        [ObservableProperty] private double needleAngle;
        [ObservableProperty] private Geometry? gaugeClip;

     
        // Lock 상태 / UI
        [ObservableProperty] private bool isLocked = true;
        [ObservableProperty] private ImageSource? lockIcon;

        [ObservableProperty] private bool controlsEnabled = false;
        [ObservableProperty] private double controlsOpacity = 0.4;

      
        // 회전(원형 이미지)
        [ObservableProperty] private double batteryRotateAngle;
        [ObservableProperty] private double batteryTempRotateAngle;
        [ObservableProperty] private double motorRotateAngle;

       
        //수평 막대 현재값만 출력 

        public GraphPointViewModel VoltageBar { get; } = new();
        public GraphPointViewModel MotorOutputBar { get; } = new();

        private const double BAR_MAX_WIDTH = 350.0;

        public void SetLockedUI(bool locked, ImageSource lockedIcon, ImageSource unlockedIcon)
        {
            IsLocked = locked;
            LockIcon = locked ? lockedIcon : unlockedIcon;

            ControlsEnabled = !locked;
            ControlsOpacity = locked ? 0.4 : 1.0;
        }

        // 전압(0~350) -> bar width(0~350)
        public void UpdateVoltageBar(int v)
        {
            Voltage = v;

            VoltageBar.ValueText = v.ToString();
            VoltageBar.BarWidth = MapToWidth(v, 350);
        }

        // 모터출력(0~250) -> bar width(0~250)
        public void UpdateMotorOutputBar(int v)
        {
            MotorOutput = v;

            MotorOutputBar.ValueText = v.ToString();
            MotorOutputBar.BarWidth = MapToWidth(v, 250);
        }

        private static double MapToWidth(int value, int max)
        {
            value = Math.Clamp(value, 0, max);
            if (max <= 0) return 0;

            return BAR_MAX_WIDTH * value / max;
        }
    }
}