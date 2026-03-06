using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Map.Models;
using Map.Services;
using Map.Services.Interfaces;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Map.ViewModels
{
    public partial class MainWindowViewModel : ObservableObject, IDisposable
    {
        private readonly ApiClient _api;
        private IMovementProvider? _movement;

        private readonly IPasswordDialogService _passwordDialog;
        private readonly IButtonAlertDialogService _buttonAlertDialog;

        private readonly DispatcherTimer _dataTimer = new();
        private readonly DispatcherTimer _rotateTimer = new();

        //기존 MainWindow.xaml.cs 상수 1:1 
        private const double GAUGE_MIN = 0;
        private const double GAUGE_MAX = 30;
        private const double GAUGE_FULL_ANGLE = 310;
        private const double GAUGE_START_ANGLE = -240;

        private const double GAUGE_WIDTH = 233;
        private const double GAUGE_HEIGHT = 193;
        private const double GAUGE_CENTER_X = GAUGE_WIDTH / 2.0;
        private const double GAUGE_CENTER_Y = GAUGE_HEIGHT / 2.0;
        private const double GAUGE_RADIUS = 135;

        private readonly ImageSource _lockImg;
        private readonly ImageSource _unlockImg;

        //화면 바인딩 대상 
        public TrainSideViewModel TrainA { get; } = new();
        public TrainSideViewModel TrainB { get; } = new();

        public ObservableCollection<AlertItemViewModel> Alerts { get; } = new();

        // 블랙막(패널 6개) - 기존 로직 유지
        [ObservableProperty] private Visibility a1Blackout = Visibility.Visible;
        [ObservableProperty] private Visibility a2Blackout = Visibility.Visible;
        [ObservableProperty] private Visibility a3Blackout = Visibility.Visible;
        [ObservableProperty] private Visibility b1Blackout = Visibility.Visible;
        [ObservableProperty] private Visibility b2Blackout = Visibility.Visible;
        [ObservableProperty] private Visibility b3Blackout = Visibility.Visible;

        //타이머 중복 방지 필드(중요)
        private bool _dataPolling = false;
        //getdata 마지막 상태 알림용 변수(null:아직 모르는 상태, true=성공, false=실패)
        private bool? _getDataLastOk = null;

        public MainWindowViewModel(
            ApiClient api,
            IPasswordDialogService passwordDialog,
            IButtonAlertDialogService buttonAlertDialog)
        {
            _api = api;
            _passwordDialog = passwordDialog;
            _buttonAlertDialog = buttonAlertDialog;

            _lockImg = new BitmapImage(new Uri("pack://application:,,,/Map;component/images/lock2.png"));
            _unlockImg = new BitmapImage(new Uri("pack://application:,,,/Map;component/images/unlock.png"));


            // 초기 Lock UI
            TrainA.SetLockedUI(true, _lockImg, _unlockImg);
            TrainB.SetLockedUI(true, _lockImg, _unlockImg);

            // dataTimer (getdata)
            _dataTimer.Interval = TimeSpan.FromSeconds(1);
            _dataTimer.Tick += async (_, __) => await DataTimerTickAsync();
            _dataTimer.Start();

            // rotateTimer (원형회전 + 블랙막)
            _rotateTimer.Interval = TimeSpan.FromMilliseconds(16);
            _rotateTimer.Tick += (_, __) => RotateTimerTick();
            _rotateTimer.Start();
        }

        public void SetMovementProvider(IMovementProvider movementProvider)
        {
            _movement = movementProvider;
        }


        // getdata 폴링 (기존 DataTimer_Tick 1:1)
        private async Task DataTimerTickAsync()
        {
            if (_dataPolling) return;
            _dataPolling = true;

            try
            {
                var data = await _api.GetDataAsync();
                if (data == null) return;

                //실패 -> 성공으로 바뀌는 순간에만 알림
                if (_getDataLastOk != true)
                {
                    AddAlert("[GET] 수신 성공");
                    _getDataLastOk = true;
                }

                string[] arr = data.argument.Split(',');
                UpdateDashboard(arr);
            }
            catch (Exception ex)
            {
                if (_getDataLastOk != false)
                {
                    AddAlert("[GET] 수신 실패");
                    _getDataLastOk = false;
                }
                Debug.WriteLine($"getdata 요청 실패: {ex.Message}");
            }
            finally
            {
                _dataPolling = false;
            }
        }


        // 원 이미지 회전 + 블랙막 (기존 RotateTimer_Tick 1:1)      
        private void RotateTimerTick()
        {
            if (_movement == null) return;

            double moveSpeed = _movement.MoveSpeed;
            int moveDirectionSign = _movement.MoveDirectionSign;

            // 정지시 전부 블랙막 + 회전 중지
            if (Math.Abs(moveSpeed) < 1e-6)
            {
                A1Blackout = Visibility.Visible;
                A2Blackout = Visibility.Visible;
                A3Blackout = Visibility.Visible;

                B1Blackout = Visibility.Visible;
                B2Blackout = Visibility.Visible;
                B3Blackout = Visibility.Visible;
                return;
            }

            bool isForward = moveDirectionSign > 0;
            double speed = 1.0;

            if (isForward)
            {
                TrainA.BatteryRotateAngle = NormalizeAngle(TrainA.BatteryRotateAngle + speed);
                TrainA.BatteryTempRotateAngle = NormalizeAngle(TrainA.BatteryTempRotateAngle - speed);
                TrainA.MotorRotateAngle = NormalizeAngle(TrainA.MotorRotateAngle + speed);

                // A 활성 / B 블랙막
                A1Blackout = Visibility.Collapsed;
                A2Blackout = Visibility.Collapsed;
                A3Blackout = Visibility.Collapsed;

                B1Blackout = Visibility.Visible;
                B2Blackout = Visibility.Visible;
                B3Blackout = Visibility.Visible;
            }
            else
            {
                TrainB.BatteryRotateAngle = NormalizeAngle(TrainB.BatteryRotateAngle + speed);
                TrainB.BatteryTempRotateAngle = NormalizeAngle(TrainB.BatteryTempRotateAngle - speed);
                TrainB.MotorRotateAngle = NormalizeAngle(TrainB.MotorRotateAngle + speed);

                // B 활성 / A 블랙막
                A1Blackout = Visibility.Visible;
                A2Blackout = Visibility.Visible;
                A3Blackout = Visibility.Visible;

                B1Blackout = Visibility.Collapsed;
                B2Blackout = Visibility.Collapsed;
                B3Blackout = Visibility.Collapsed;
            }
        }

        private static double NormalizeAngle(double angle)
        {
            angle %= 360.0;
            if (angle < 0) angle += 360.0;
            return angle;
        }


        // Alerts 
        //  항상 4칸 유지, 최신이 위로 오게 유지
        private void AddAlert(string msg)
        {
            string now = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

            // 최신을 위로
            Alerts.Insert(0, new AlertItemViewModel { Msg = msg, Time = now });

            while (Alerts.Count > 4)
                Alerts.RemoveAt(Alerts.Count - 1);

            while (Alerts.Count < 4)
                Alerts.Add(new AlertItemViewModel { Msg = "", Time = "" });
        }

        private void CheckAlerts(string sideLabel, int voltage, int current, int batteryTemp, int motorSpeed)
        {
            string prefix = $"[{sideLabel}]";

            if (voltage < 10)
                AddAlert($"{prefix} 저전압 발생 ({voltage}V)");

            if (current > 290)
                AddAlert($"{prefix} 과전류 발생 ({current}A)");

            if (batteryTemp > 46)
                AddAlert($"{prefix} 배터리 고온 ({batteryTemp}°C)");

            if (motorSpeed > 24)
                AddAlert($"{prefix} 모터 과속 ({motorSpeed}km/h)");
        }


        // Dashboard 업데이트    
        private void UpdateDashboard(string[] arr)
        {
            //  Train A (arr[1]~arr[6]) 
            int voltageA = int.Parse(arr[1]);
            int currentA = int.Parse(arr[2]);
            int batteryA = int.Parse(arr[3]);
            int batteryTempA = int.Parse(arr[4]);
            int motorOutputA = int.Parse(arr[5]);
            int motorSpeedA = int.Parse(arr[6]);

            TrainA.Voltage = voltageA;
            TrainA.Current = currentA;
            TrainA.Battery = batteryA;
            TrainA.BatteryTemp = batteryTempA;
            TrainA.MotorOutput = motorOutputA;
            TrainA.MotorSpeed = motorSpeedA;

            // 바늘
            double angleA = -90 + (motorSpeedA * 6.0);
            if (angleA > 85) angleA = 85;
            if (angleA < -85) angleA = -85;
            TrainA.NeedleAngle = angleA;

            // 게이지 fill clip
            TrainA.GaugeClip = CreateGaugeClip(motorSpeedA);

            // 그래프 8칸
            TrainA.UpdateVoltageBar(voltageA);
            TrainA.UpdateMotorOutputBar(motorOutputA);

            CheckAlerts("A면", voltageA, currentA, batteryTempA, motorSpeedA);

            // Train B (arr[31]~arr[36]) 
            int off = 30;
            int voltageB = int.Parse(arr[1 + off]);
            int currentB = int.Parse(arr[2 + off]);
            int batteryB = int.Parse(arr[3 + off]);
            int batteryTempB = int.Parse(arr[4 + off]);
            int motorOutputB = int.Parse(arr[5 + off]);
            int motorSpeedB = int.Parse(arr[6 + off]);

            TrainB.Voltage = voltageB;
            TrainB.Current = currentB;
            TrainB.Battery = batteryB;
            TrainB.BatteryTemp = batteryTempB;
            TrainB.MotorOutput = motorOutputB;
            TrainB.MotorSpeed = motorSpeedB;

            double angleB = -90 + (motorSpeedB * 6.18);
            if (angleB > 85) angleB = 85;
            if (angleB < -85) angleB = -85;
            TrainB.NeedleAngle = angleB;

            TrainB.GaugeClip = CreateGaugeClip(motorSpeedB);

            TrainB.UpdateVoltageBar(voltageB);
            TrainB.UpdateMotorOutputBar(motorOutputB);

            CheckAlerts("B면", voltageB, currentB, batteryTempB, motorSpeedB);
        }


        // Gauge Clip
        private Geometry CreateGaugeClip(double speed)
        {
            double normalized = (speed - GAUGE_MIN) / (GAUGE_MAX - GAUGE_MIN);
            normalized = Math.Clamp(normalized, 0, 1);

            double fillAngle = normalized * GAUGE_FULL_ANGLE;
            double endAngle = GAUGE_START_ANGLE + fillAngle;

            Point start = PointOnGaugeArc(GAUGE_START_ANGLE);
            Point end = PointOnGaugeArc(endAngle);

            bool isLargeArc = fillAngle > 180;

            PathFigure fig = new()
            {
                StartPoint = new Point(GAUGE_CENTER_X, GAUGE_CENTER_Y),
                IsClosed = true
            };

            fig.Segments.Add(new LineSegment(start, true));
            fig.Segments.Add(new ArcSegment
            {
                Point = end,
                Size = new Size(GAUGE_RADIUS, GAUGE_RADIUS),
                SweepDirection = SweepDirection.Clockwise,
                IsLargeArc = isLargeArc
            });
            fig.Segments.Add(new LineSegment(new Point(GAUGE_CENTER_X, GAUGE_CENTER_Y), true));

            PathGeometry geo = new();
            geo.Figures.Add(fig);
            return geo;
        }

        private Point PointOnGaugeArc(double angleDeg)
        {
            double rad = angleDeg * Math.PI / 180.0;
            return new Point(
                GAUGE_CENTER_X + GAUGE_RADIUS * Math.Cos(rad),
                GAUGE_CENTER_Y + GAUGE_RADIUS * Math.Sin(rad)
            );
        }


        //Commands (RelayCommand) 
        //Lock 클릭: RelayCommand
        //Down/Up: Behaviors로 연결
        [RelayCommand]
        private void ToggleLockA()
        {
            if (TrainA.IsLocked)
            {
                if (!_passwordDialog.ShowPassword())
                    return;

                TrainA.SetLockedUI(false, _lockImg, _unlockImg);
            }
            else
            {
                TrainA.SetLockedUI(true, _lockImg, _unlockImg);
            }
        }

        [RelayCommand]
        private void ToggleLockB()
        {
            if (TrainB.IsLocked)
            {
                if (!_passwordDialog.ShowPassword())
                    return;

                TrainB.SetLockedUI(false, _lockImg, _unlockImg);
            }
            else
            {
                TrainB.SetLockedUI(true, _lockImg, _unlockImg);
            }
        }

        // A면 버튼
        [RelayCommand]
        private async Task TrainAForwardDown()
        {
            if (TrainA.IsLocked) return;
            await SendSetDataAsync(1, 1, 1);
        }

        [RelayCommand]
        private Task TrainAForwardUp()
        {
            if (TrainA.IsLocked) return Task.CompletedTask;
            AddAlert("기차1 A면에서 전진버튼이 눌렸습니다");
            _buttonAlertDialog.ShowMessage("기차1 A면에서 전진버튼이 눌렸습니다");
            return SendSetDataAsync(1, 0, 1);
        }

        [RelayCommand]
        private async Task TrainABackwardDown()
        {
            if (TrainA.IsLocked) return;

            await SendSetDataAsync(2, 1, 1);
        }

        [RelayCommand]
        private Task TrainABackwardUp()
        {
            if (TrainA.IsLocked) return Task.CompletedTask;
            AddAlert("기차1 A면에서 후진버튼이 눌렸습니다");
            _buttonAlertDialog.ShowMessage("기차1 A면에서 후진버튼이 눌렸습니다");
            return SendSetDataAsync(2, 0, 1);
        }

        [RelayCommand]
        private async Task TrainABreakDown()
        {
            if (TrainA.IsLocked) return;

            await SendSetDataAsync(3, 1, 1);
        }

        [RelayCommand]
        private Task TrainABreakUp()
        {
            if (TrainA.IsLocked) return Task.CompletedTask;
            AddAlert("기차1 A면에서 정지버튼이 눌렸습니다");
            _buttonAlertDialog.ShowMessage("기차1 A면에서 정지 버튼이 눌렸습니다");
            return SendSetDataAsync(3, 0, 1);
        }

        //  B면 버튼 
        [RelayCommand]
        private async Task TrainBForwardDown()
        {
            if (TrainB.IsLocked) return;

            await SendSetDataAsync(1, 1, 2);
        }

        [RelayCommand]
        private Task TrainBForwardUp()
        {
            if (TrainB.IsLocked) return Task.CompletedTask;
            AddAlert("기차1 B면에서 전진버튼이 눌렸습니다");
            _buttonAlertDialog.ShowMessage("기차1 B면에서 전진버튼이 눌렸습니다");
            return SendSetDataAsync(1, 0, 2);
        }

        [RelayCommand]
        private async Task TrainBBackwardDown()
        {
            if (TrainB.IsLocked) return;

            await SendSetDataAsync(2, 1, 2);
        }

        [RelayCommand]
        private Task TrainBBackwardUp()
        {
            if (TrainB.IsLocked) return Task.CompletedTask;
            AddAlert("기차1 B면에서 후진버튼이 눌렸습니다");
            _buttonAlertDialog.ShowMessage("기차1 B면에서 후진버튼이 눌렸습니다");
            return SendSetDataAsync(2, 0, 2);
        }

        [RelayCommand]
        private async Task TrainBBreakDown()
        {
            if (TrainB.IsLocked) return;

            await SendSetDataAsync(3, 1, 2);
        }

        [RelayCommand]
        private Task TrainBBreakUp()
        {
            if (TrainB.IsLocked) return Task.CompletedTask;
            AddAlert("기차1 B면에서 정지버튼이 눌렸습니다");
            _buttonAlertDialog.ShowMessage("기차1 B면에서 정지 버튼이 눌렸습니다");
            return SendSetDataAsync(3, 0, 2);
        }

        // setdata API
        private async Task SendSetDataAsync(int operation, int value, int train)
        {
            try
            {   //train : A면인지 B면인지, op : 전진 후진 정지 긴급(삭제된 기능), value : 누름, 뗌 상태(1, 0)
                await _api.PostSetDataAsync(operation, value, train);
                AddAlert($"[SET] 전송 성공 (train={train}, op={operation}, value={value})");


            }
            catch (Exception ex)
            {
                Debug.WriteLine("setdata POST 실패: " + ex.Message);
                AddAlert($"[SET] 전송 실패 (train={train}, op={operation}, value={value})");

            }
        }
        public void Dispose()
        {
            _dataTimer.Stop();
            _rotateTimer.Stop();
        }
    }
}