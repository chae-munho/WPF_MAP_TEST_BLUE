using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Collections.ObjectModel;
using System.Windows.Media;

namespace Map.ViewModels
{
    public partial class TrainSideViewModel : ObservableObject
    {
        //  숫자 데이터 
        [ObservableProperty] private int voltage;
        [ObservableProperty] private int current;
        [ObservableProperty] private int battery;
        [ObservableProperty] private int batteryTemp;
        [ObservableProperty] private int motorOutput;
        [ObservableProperty] private int motorSpeed;

        //  표시용 
        [ObservableProperty] private double needleAngle;
        [ObservableProperty] private Geometry? gaugeClip;

        //  Lock 상태 / UI 
        [ObservableProperty] private bool isLocked = true;
        [ObservableProperty] private ImageSource? lockIcon;

        [ObservableProperty] private bool controlsEnabled = false;
        [ObservableProperty] private double controlsOpacity = 0.4;

        //  회전(원형 이미지) 
        [ObservableProperty] private double batteryRotateAngle;
        [ObservableProperty] private double batteryTempRotateAngle;
        [ObservableProperty] private double motorRotateAngle;

        // 그래프(8칸) 
        public ObservableCollection<GraphPointViewModel> VoltageGraph { get; } = new();
        public ObservableCollection<GraphPointViewModel> MotorOutputGraph { get; } = new();

        public TrainSideViewModel()
        {
            // 8칸 고정 생성
            for (int i = 0; i < 8; i++)
            {
                VoltageGraph.Add(new GraphPointViewModel());
                MotorOutputGraph.Add(new GraphPointViewModel());
            }
        }

        public void SetLockedUI(bool locked, ImageSource lockedIcon, ImageSource unlockedIcon)
        {
            IsLocked = locked;
            LockIcon = locked ? lockedIcon : unlockedIcon;

            ControlsEnabled = !locked;
            ControlsOpacity = locked ? 0.4 : 1.0;
        }

        public void PushVoltage(string newValue)
        {
            PushSlidingGraph(VoltageGraph, newValue);
        }

        public void PushMotorOutput(string newValue)
        {
            PushSlidingGraph(MotorOutputGraph, newValue);
        }

        private static void PushSlidingGraph(ObservableCollection<GraphPointViewModel> points, string newValue)
        {
            // 왼쪽으로 밀기
            for (int i = 0; i < points.Count - 1; i++)
            {
                points[i].ValueText = points[i + 1].ValueText;
                points[i].BarHeight = points[i + 1].BarHeight;
            }

            // 맨 오른쪽 새 값
            var last = points[^1];
            last.ValueText = newValue;

            if (int.TryParse(newValue, out int v))
            {
                last.BarHeight = Math.Clamp(v * 0.3, 10, 97);
            }
        }
    }
}
