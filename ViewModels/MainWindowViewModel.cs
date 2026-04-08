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
        private readonly TrainWebSocketServerService _wsServer;
        private IMovementProvider? _movement;

        private readonly IPasswordDialogService _passwordDialog;
        private readonly IButtonAlertDialogService _buttonAlertDialog;
        private readonly IDangerDialogService _dangerDialog;
        private readonly IAdminSettingsDialogService _adminSettingsDialog;

        private readonly DispatcherTimer _dataTimer = new();
        private readonly DispatcherTimer _rotateTimer = new();

        private readonly IAppSettingsService _appSettingsService;
        private readonly Action<string, string> _applyServerBaseUrls;

        public string CurrentServerBaseUrl { get; private set; } = "";

        //기존 MainWindow.xaml.cs 
        private const double GAUGE_MIN = 0;
        private const double GAUGE_MAX = 30;   // 25에서 30으로 수정됨
        private const double GAUGE_FULL_ANGLE = 292;   // 300 -> 292로
        private const double GAUGE_START_ANGLE = -237; // -250 -> -237로

        private const double GAUGE_WIDTH = 233;
        private const double GAUGE_HEIGHT = 193;
        private const double GAUGE_CENTER_X = GAUGE_WIDTH / 2.0;
        private const double GAUGE_CENTER_Y = GAUGE_HEIGHT / 2.0;
        private const double GAUGE_RADIUS = 135;

        //비상정지 상태 변화 감지용 즉 0->1에서 변할 때 뜸 계속 1일 때는 뜨지 않게 
        private int _prevEmergencyA = 0;
        private int _prevEmergencyB = 0;    

       

        //화면 바인딩 대상 
        public TrainSideViewModel TrainA { get; } = new();
        public TrainSideViewModel TrainB { get; } = new();

        public ObservableCollection<AlertItemViewModel> Alerts { get; } = new();

        // 블랙막(패널 6개) 
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

        // 관리자 설정값 디폴트 값은 SideAlertSettings.cs에
        public SideAlertSettings BSettings { get; private set; } = SideAlertSettings.CreateDefaultB();
        public SideAlertSettings ASettings { get; private set; } = SideAlertSettings.CreateDefaultA();


        private readonly TrainVideoWebSocketServerService _videoWsServer;
        private readonly ICameraPopupService _cameraPopupService;

        private int _previousActiveIntercomCar = 0;
        private int _previousActiveIntercomTrain = 0;

        public string CurrentVideoServerBaseUrl { get; private set; } = "";

      

        public MainWindowViewModel(
            TrainWebSocketServerService wsServer,
            TrainVideoWebSocketServerService videoWsServer,
            IPasswordDialogService passwordDialog,
            IButtonAlertDialogService buttonAlertDialog,
            IDangerDialogService dangerDialog,
            IAdminSettingsDialogService adminSettingsDialog,
            ICameraPopupService cameraPopupService,
            IAppSettingsService appSettingsService,
            AppSettings initialSettings,
            Action<string, string> applyServerBaseUrls)
        {
            _wsServer = wsServer;
            _videoWsServer = videoWsServer;
            _passwordDialog = passwordDialog;
            _buttonAlertDialog = buttonAlertDialog;
            _dangerDialog = dangerDialog;
            _adminSettingsDialog = adminSettingsDialog;
            _cameraPopupService = cameraPopupService;
            _appSettingsService = appSettingsService;
            _applyServerBaseUrls = applyServerBaseUrls;

            CurrentServerBaseUrl = initialSettings.ServerBaseUrl;
            CurrentVideoServerBaseUrl = initialSettings.VideoServerBaseUrl;
            BSettings = initialSettings.BSettings?.Clone() ?? SideAlertSettings.CreateDefaultB();
            ASettings = initialSettings.ASettings?.Clone() ?? SideAlertSettings.CreateDefaultA();

            _dataTimer.Interval = TimeSpan.FromSeconds(1);
            _dataTimer.Tick += async (_, __) => await DataTimerTickAsync();
            _dataTimer.Start();

            _rotateTimer.Interval = TimeSpan.FromMilliseconds(16);
            _rotateTimer.Tick += (_, __) => RotateTimerTick();
            _rotateTimer.Start();
        }
        private (int trainNo, int carNo) GetActiveIntercomTarget(string[] arr)
        {
            int aIntercomCar = GetSafeInt(arr, 12);
            int aMaster = GetSafeInt(arr, 14);

            int bIntercomCar = GetSafeInt(arr, 59);
            int bMaster = GetSafeInt(arr, 61);

            if (aMaster == 1 && aIntercomCar > 0)
                return (1, aIntercomCar);

            if (aMaster == 0 && bMaster == 1 && bIntercomCar > 0)
                return (2, bIntercomCar);

            return (0, 0);
        }

        private void HandleActiveIntercomTransition(int currentTrainNo, int currentCarNo)
        {
            if (currentTrainNo <= 0 || currentCarNo <= 0)
            {
                _previousActiveIntercomTrain = 0;
                _previousActiveIntercomCar = 0;
                return;
            }

            if (_previousActiveIntercomCar == 0 && _previousActiveIntercomTrain == 0)
            {
                AddAlert($"객차 {currentCarNo}번 인터컴 호출");
                _cameraPopupService.ShowIntercomPopup(currentTrainNo, currentCarNo);

                _previousActiveIntercomTrain = currentTrainNo;
                _previousActiveIntercomCar = currentCarNo;
                return;
            }

            if (currentTrainNo != _previousActiveIntercomTrain ||
                currentCarNo != _previousActiveIntercomCar)
            {
                AddAlert($"객차 {currentCarNo}번 인터컴 호출");
                _cameraPopupService.ShowIntercomPopup(currentTrainNo, currentCarNo);

                _previousActiveIntercomTrain = currentTrainNo;
                _previousActiveIntercomCar = currentCarNo;
            }
        }

        private static int GetSafeInt(string[] arr, int index)
        {
            if (arr == null || index < 0 || index >= arr.Length)
                return 0;

            return int.TryParse(arr[index], out int value) ? value : 0;
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
                var data = await _wsServer.GetDataAsync();
                if (data == null || string.IsNullOrWhiteSpace(data.argument))
                    return;

                if (_getDataLastOk != true)
                {
                    AddAlert("[GET] 수신 성공");
                    _getDataLastOk = true;
                }

                string[] arr = data.argument.Split(',');

                if (arr.Length < 94)
                    return;

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
        //A면 범위 검사
        private void CheckAlertsA(int voltage, int output, int battery, int batteryTemp)
        {
            if (voltage < ASettings.VoltageMin)
                AddAlert($"가은역 방면 기관차 저전압 발생 ({voltage}V)");
            else if (voltage > ASettings.VoltageMax)
                AddAlert($"가은역 방면 기관차 고전압 발생 ({voltage}V)");

            if (output < ASettings.CurrentMin)
                AddAlert($"가은역 방면 기관차 저전류 발생 ({output}A)");
            else if (output > ASettings.CurrentMax)
                AddAlert($"가은역 방면 기관차 고전류 발생 ({output}A)");

            if (battery < ASettings.BatteryMin)
                AddAlert($"가은역 방면 기관차 배터리 용량 부족 ({battery}%)");
            // battery > ASettings.BatteryMax 는 표시 없음

            if (batteryTemp < ASettings.BatteryTempMin)
                AddAlert($"가은역 방면 기관차 배터리 저온 발생 ({batteryTemp}°C)");
            else if (batteryTemp > ASettings.BatteryTempMax)
                AddAlert($"가은역 방면 기관차 배터리 고온 발생 ({batteryTemp}°C)");
        }
        //B면 범위 검사
        private void CheckAlertsB(int voltage, int output, int battery, int batteryTemp)
        {
            if (voltage < BSettings.VoltageMin)
                AddAlert($"구량리역 방면 기관차 저전압 발생 ({voltage}V)");
            else if (voltage > BSettings.VoltageMax)
                AddAlert($"구량리역 방면 기관차 고전압 발생 ({voltage}V)");

            if (output < BSettings.CurrentMin)
                AddAlert($"구량리역 방면 기관차 저전류 발생 ({output}A)");
            else if (output > BSettings.CurrentMax)
                AddAlert($"구량리역 방면 기관차 고전류 발생 ({output}A)");

            if (battery < BSettings.BatteryMin)
                AddAlert($"구량리역 방면 기관차 배터리 용량 부족 ({battery}%)");
            // battery > BSettings.BatteryMax 는 표시 없음

            if (batteryTemp < BSettings.BatteryTempMin)
                AddAlert($"구량리역 방면 기관차 배터리 저온 발생 ({batteryTemp}°C)");
            else if (batteryTemp > BSettings.BatteryTempMax)
                AddAlert($"구량리역 방면 기관차 배터리 고온 발생 ({batteryTemp}°C)");
        }


        // Dashboard 업데이트    
        private void UpdateDashboard(string[] arr)
        {
            //  Train A (arr[1]~arr[6]) 
            int speedA = int.Parse(arr[2]);   //기차1 속도
            int voltageA = int.Parse(arr[4]); //기차1 전압
            int motorOutputA = int.Parse(arr[6]); //기차1 모터전류(출력)
            int batteryA = int.Parse(arr[8]); //기차1 배터리용량
            int batteryTempA = int.Parse(arr[10]);  //기차1 배터리온도
            
            int emergencyA = int.Parse(arr[36]); //기차1 비상정지 상태(M122)


            TrainA.Voltage = voltageA;
            TrainA.Battery = batteryA;
            TrainA.BatteryTemp = batteryTempA;
            TrainA.MotorOutput = motorOutputA;
            TrainA.MotorSpeed = speedA;


            // 바늘
            double angleA = -90 + (speedA * 6.0);    // 기존 7.2에서 6.0으로 수정됨
            if (angleA > 85) angleA = 85;
            if (angleA < -85) angleA = -85;
            TrainA.NeedleAngle = angleA;

            // 게이지 fill clip
            TrainA.GaugeClip = CreateGaugeClip(speedA);

            // 그래프 8칸
            TrainA.UpdateVoltageBar(voltageA);
            TrainA.UpdateMotorOutputBar(motorOutputA);

            CheckAlertsA(voltageA, motorOutputA, batteryA, batteryTempA);
           

            //A면 비상정지 0 에서 1 변화 감지 시 팝업 1회
            if (_prevEmergencyA == 0 && emergencyA == 1) {
                AddAlert("가은역 방면 기관차 비상정지 발생");
                _dangerDialog.ShowMessage("가은역 방면 기관차 비상정지 상태가 발생했습니다.");
            }
            _prevEmergencyA = emergencyA;



            // Train B (arr[31]~arr[36]) 
            int off = 47;

            int speedB = int.Parse(arr[off + 2]);
            int voltageB = int.Parse(arr[off + 4]);
            int motorOutputB = int.Parse(arr[off + 6]);
            int batteryB = int.Parse(arr[off + 8]);
            int batteryTempB = int.Parse(arr[off + 10]);
           
            int emergencyB = int.Parse(arr[off + 36]); // 기차1 비상정지 상태(M122)

            TrainB.Voltage = voltageB;
            TrainB.Battery = batteryB;
            TrainB.BatteryTemp = batteryTempB;
            TrainB.MotorOutput = motorOutputB;
            TrainB.MotorSpeed = speedB;

            double angleB = -90 + (speedB * 6.0);
            if (angleB > 85) angleB = 85;
            if (angleB < -85) angleB = -85;
            TrainB.NeedleAngle = angleB;

            TrainB.GaugeClip = CreateGaugeClip(speedB);

            TrainB.UpdateVoltageBar(voltageB);
            TrainB.UpdateMotorOutputBar(motorOutputB);

            CheckAlertsB(voltageB, motorOutputB, batteryB, batteryTempB);
        

            //B면 비상정지 0에서 1로 바뀌때 주의팝업 띄우기
            if (_prevEmergencyB == 0 && emergencyB == 1)
            {
                AddAlert("구량리역 방면 기관차 비상정지 발생");
                _dangerDialog.ShowMessage("구량리역 방면 기관차 비상정지 상태가 발생했습니다.");
            }
            _prevEmergencyB = emergencyB;
            var activeIntercomTarget = GetActiveIntercomTarget(arr);
            HandleActiveIntercomTransition(activeIntercomTarget.trainNo, activeIntercomTarget.carNo);
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
        private void OpenAdminSettings()
        {
            bool ok = _adminSettingsDialog.ShowDialog(
                CurrentServerBaseUrl,
                CurrentVideoServerBaseUrl,
                BSettings,
                ASettings,
                out string updatedServerBaseUrl,
                out string updatedVideoServerBaseUrl,
                out SideAlertSettings updatedBSettings,
                out SideAlertSettings updatedASettings);

            if (!ok)
                return;

            try
            {
                CurrentServerBaseUrl = updatedServerBaseUrl;
                CurrentVideoServerBaseUrl = updatedVideoServerBaseUrl;
                BSettings = updatedBSettings;
                ASettings = updatedASettings;

                var appSettings = new AppSettings
                {
                    ServerBaseUrl = CurrentServerBaseUrl,
                    VideoServerBaseUrl = CurrentVideoServerBaseUrl,
                    BSettings = BSettings.Clone(),
                    ASettings = ASettings.Clone()
                };

                _appSettingsService.Save(appSettings);

                _applyServerBaseUrls(CurrentServerBaseUrl, CurrentVideoServerBaseUrl);

                AddAlert("[관리자 설정] 일반/영상 서버 주소 및 기준값이 저장되었습니다.");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"설정 저장 중 오류가 발생했습니다.\n{ex.Message}",
                    "저장 오류", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }


        //down 메서드는 필요가 없을 거 같은데 일단 남겨둠
        //A면 가속 감속 정지 이벤트 핸들러 시작

        // 가은역 방면 제어버튼
        [RelayCommand]
        private async Task TrainAForwardDown()
        {   
            await SendSetDataAsync(1, 1, 1);
        }

        [RelayCommand]
        private async Task TrainAForwardUp()
        {
            if (!_passwordDialog.ShowPassword())
                return;

            AddAlert("가은역 방면 기관차 가속 버튼이 눌렸습니다");

            bool ok = _buttonAlertDialog.ShowMessage("가은역 방면 기관차 가속 버튼이 눌렸습니다");
            if (!ok) return;

            await SendSetDataAsync(1, 0, 1);
        }

        [RelayCommand]
        private async Task TrainABackwardDown()
        {
           

            await SendSetDataAsync(2, 1, 1);
        }

        [RelayCommand]
        private async Task TrainABackwardUp()
        {
            if (!_passwordDialog.ShowPassword())
                return;

            AddAlert("가은역 방면 기관차 감속 버튼이 눌렸습니다");

            bool ok = _buttonAlertDialog.ShowMessage("가은역 방면 기관차 감속 버튼이 눌렸습니다");
            if (!ok) return;

            await SendSetDataAsync(2, 0, 1);
        }

        [RelayCommand]
        private async Task TrainABreakDown()
        {
           

            await SendSetDataAsync(3, 1, 1);
        }

        [RelayCommand]
        private async Task TrainABreakUp()
        {
            if (!_passwordDialog.ShowPassword())
                return;

            AddAlert("가은역 방면 기관차 정지 버튼이 눌렸습니다");

            bool ok = _buttonAlertDialog.ShowMessage("가은역 방면 기관차 정지 버튼이 눌렸습니다");
            if (!ok) return;

            await SendSetDataAsync(3, 0, 1);
        }

        // 구량리역 방면 제어 버튼
        [RelayCommand]
        private async Task TrainBForwardDown()
        {
            await SendSetDataAsync(1, 1, 2);
        }

        [RelayCommand]
        private async Task TrainBForwardUp()
        {
            if (!_passwordDialog.ShowPassword())
                return;

            AddAlert("구량리역 방면 기관차 가속 버튼이 눌렸습니다");

            bool ok = _buttonAlertDialog.ShowMessage("구량리역 방면 기관차 가속 버튼이 눌렸습니다");
            if (!ok) return;

            await SendSetDataAsync(1, 0, 2);
        }

        [RelayCommand]
        private async Task TrainBBackwardDown()
        {
            await SendSetDataAsync(2, 1, 2);
        }

        [RelayCommand]
        private async Task TrainBBackwardUp()
        {
            if (!_passwordDialog.ShowPassword())
                return;

            AddAlert("구량리역 방면 기관차 감속 버튼이 눌렸습니다");

            bool ok = _buttonAlertDialog.ShowMessage("구량리역 방면 기관차 감속 버튼이 눌렸습니다");
            if (!ok) return;

            await SendSetDataAsync(2, 0, 2);
        }

        [RelayCommand]
        private async Task TrainBBreakDown()
        {
           

            await SendSetDataAsync(3, 1, 2);
        }

        [RelayCommand]
        private async Task TrainBBreakUp()
        {
            if (!_passwordDialog.ShowPassword())
                return;

            AddAlert("구량리역 방면 기관차 정지버튼이 눌렸습니다");

            bool ok = _buttonAlertDialog.ShowMessage("구량리역 방면 기관차 정지 버튼이 눌렸습니다");
            if (!ok) return;

            await SendSetDataAsync(3, 0, 2);
        }

        //A면 가속 감속 정지 이벤트 핸들러 종료

        //알람패널 다운로드 버튼 메서드
        [RelayCommand]
        private void DownloadAlerts()
        {
            MessageBox.Show("로그 엑셀 다운로드 구현 예정", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // setdata API
        private async Task SendSetDataAsync(int operation, int value, int train)
        {
            try
            {   //train : A면인지 B면인지, op : 전진 후진 정지 긴급(삭제된 기능), value : 누름, 뗌 상태(1, 0)
                await _wsServer.PostSetDataAsync(operation, value, train);
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
            _cameraPopupService.CloseAll();
        }
    }
}