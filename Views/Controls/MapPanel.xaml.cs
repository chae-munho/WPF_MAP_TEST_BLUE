using Map.Services;
using Map.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;


namespace Map.Views.Controls
{
   
    public partial class MapPanel : UserControl, IMovementProvider
    {
        //지도/캔버스 + 경로 + GPS + 이동 + 포인터/이펙트 + 회전/방향/각도 + 상태 복원/재실행 관련 필드 시작


        private ApiClient? _api;
        //포인터 이미지
        private Image? pointerImage;
        //활성화 이미지
        private Image? glowImage;
        //사용자가 찍은 원본 경로 점(pixel 좌표)
        private readonly List<Point> pathPoints = new();
        // 등간격으로 샘플링된 경로 (vertexPath)
        private List<Point> vertexPath = new();
        // 경로 시각화용 Polyline
        private readonly Polyline routeLine = new()
        {
            Stroke = Brushes.Yellow,
            StrokeThickness = 3
        };
        // 파일 경로
        private static readonly string BaseDir = AppDomain.CurrentDomain.BaseDirectory;
        private readonly string savePath = System.IO.Path.Combine(BaseDir, "pos.txt");
        private readonly string vertexSavePath = System.IO.Path.Combine(BaseDir, "vertex.txt");
        //gps timer
        private readonly DispatcherTimer gpsTimer = new();
        //개발/운영 모드 토글 true : 개발용(점 찍기, 점/라인 표시) false : 실서비스 (점 찍기 금지, 점/라인 숨기기)
        private static readonly bool DEBUG_ROUTE_EDITOR = false;
        // 실제 GPS 기준 전체 경로 (위도/경도 목록)
        private readonly List<(double lat, double lng)> gpsRoute = new()
        {
                      // TODO: 여기에 너가 실제 lat,lng 좌표를 채워 넣으면 됨
 (36.64864970499273, 128.06153436130455),
(36.648237715725806, 128.06175795009995),
(36.64783018110709, 128.06198718901183),
(36.64740918120485, 128.0622106493707),
(36.647010555810844, 128.06245118836682),
(36.64659401008651, 128.0626802963225),
(36.6462129543814, 128.06297139237893),
(36.6458807033367, 128.06334702360445),
(36.64559745587986, 128.0637848278595),
(36.645376825936665, 128.06427381039535),
(36.64518307351026, 128.0647799333937),
(36.644989318967966, 128.0652860538145),
(36.64478214765103, 128.065780804699),
(36.644566064602024, 128.06626424798793),
(36.644264937245836, 128.0666850144777),
(36.64398172787257, 128.06711720689387),
(36.64365832359238, 128.06750970457875),
(36.64342421408669, 128.06799288712034),
(36.64319911245403, 128.06847619115825),
(36.64303677644215, 128.0689939053298),
(36.64283849942815, 128.0694999385496),
(36.64264472519546, 128.070006031448),
(36.64245545374235, 128.07051218411203),
(36.642257220647224, 128.07101261933505),
(36.64207690448662, 128.0715244818814),
(36.64188089449957, 128.07202773826714),
(36.64169166492696, 128.07252829068332),
(36.641495550054245, 128.07304272217283),
(36.64131074560535, 128.0735517172014),
(36.641117055244976, 128.07404660943817),
(36.64093222136051, 128.0745583945296),
(36.640693629341825, 128.07503588553536),
(36.640437015891486, 128.075513123046),
(36.64015397714901, 128.0759229012316),
(36.63983499894532, 128.07632099501458),
(36.63952492816834, 128.076730390848),
(36.63926851028807, 128.07718525565508),
(36.638940619338804, 128.077572034317),
(36.638630443203915, 128.07799260018825),
(36.63831155947988, 128.07837949782373),
(36.63798386724769, 128.07874390722796),
(36.63762023612105, 128.07909663123542),
(36.63728358264314, 128.07945531885684),
(36.636964592964055, 128.07985338330352),
(36.63665901526614, 128.0802628126693),
(36.63637130360742, 128.08068925968095),
(36.63611036611213, 128.08114402975707),
(36.63590753362376, 128.08164992269081),
(36.635772018370446, 128.08218470645878),
(36.635699416676516, 128.08273713988297),
(36.635716859227635, 128.08329642332438),
(36.63581108554221, 128.0838344209858),
(36.63601373179758, 128.08434039694345),
(36.63629356962805, 128.08478037264766),
(36.6366052963654, 128.0851816656266),
(36.63699933195998, 128.08544994510046),
(36.637416502609724, 128.0856514658279),
(36.63787088373674, 128.08572492843405),
(36.6383170702203, 128.08570882768245),
(36.638767557753994, 128.08571514962006),
(36.63921835094565, 128.08568793211685),
(36.639669144102754, 128.08566071436317),
(36.64011973343694, 128.08565585634085),
(36.64056546122255, 128.08569006530166),
(36.64101057751745, 128.08579135543746),
(36.641427696144575, 128.085998479082),
(36.64182167826131, 128.08627237013837),
(36.642156178454535, 128.08664606396343),
(36.64247926218286, 128.0870363732564),
(36.64274319435052, 128.08749014962495),
(36.64301626210896, 128.0879300802874),
(36.64330737318897, 128.08836747246943),
(36.643587297075825, 128.08879632404307),
(36.64388523881819, 128.08922543245237),
(36.64420155638003, 128.0896156660247),
(36.64457254483207, 128.08993958087893),
(36.64496196113191, 128.09021903016617),
(36.64534687184127, 128.09049841878718),
(36.645772888711186, 128.09071688766153),
(36.64619480986097, 128.09089057205438),
(36.64663039890649, 128.0910476776763),
(36.64707545834129, 128.09115459785323),
(36.647506951826685, 128.09126691915313),
(36.6479496560782, 128.09138499124376),
(36.648397095557726, 128.09147797070517),
(36.648837239637636, 128.09162955738145),
(36.64926868121866, 128.09174747477613),
(36.64970902984419, 128.09187670210602),
(36.65014968581361, 128.09197238671743),
(36.65060012025498, 128.09198433860203),
(36.65103827021008, 128.09186192130332),
(36.65145481814431, 128.09163855021586),
(36.65185370486061, 128.09137578661006),
(36.65225238614121, 128.09113538377937),
(36.6526557766398, 128.0908726784965),
(36.653072220378114, 128.09066047942656),
(36.65347996115952, 128.0904146053462),
(36.653918314240364, 128.0902698110319),
(36.654364346233464, 128.0902705085914),
(36.65481467832084, 128.09029363394293),
(36.65524591601366, 128.09043391820597),
(36.65564910036358, 128.0906856450122),
(36.65604757439689, 128.09095967546756),
(36.65635016500408, 128.09137215251226),
(36.65662546872097, 128.09181220697616),
(36.65681906807415, 128.09231821327398),
(36.657012665228905, 128.0928242220636),
(36.657161057701956, 128.09334636992267),
(36.65733647694175, 128.09386890216183),
(36.65747129883415, 128.09439645403947),
(36.65764671338262, 128.09491899065847),
(36.65780861105518, 128.0954413381085),
(36.65797055784823, 128.09595809642983),
(36.65820726983435, 128.09642838517675),
(36.65849161548826, 128.09686300443755),
(36.65882152306529, 128.09724235303642),
(36.65916960332374, 128.09760518667113),
(36.659545175375534, 128.09791808508632),
(36.659895712098916, 128.09825859158644),
(36.6602574840859, 128.09860205695492),
(36.660589715572996, 128.09897306654025),
(36.66086944337295, 128.0994188274151),
(36.66110841934468, 128.09988638057996),
(36.661324662589365, 128.10037598198602),
(36.661459403231156, 128.10090916337901),
(36.66153537174816, 128.1014638781752),
(36.661570742420594, 128.1020236075092),
(36.66154759939858, 128.1025769104542),
(36.6614613861672, 128.1031293129817),
(36.661348296862755, 128.10366455381038),
(36.66119020871674, 128.10419355804285),
(36.66106810502211, 128.10472866656428),
(36.66094599894881, 128.10526377333773),
(36.66080141827367, 128.1057929647051),
(36.66065678333434, 128.1063277454801),
(36.66055714232356, 128.1068687606555),
(36.660407997783366, 128.10740347308013),
(36.66026786052387, 128.10793831251524),
(36.66016826431296, 128.10847373131327),
(36.66000114542566, 128.10900258788953),
(36.6598251708514, 128.1095145386895),
(36.65962677422642, 128.11001498096883),
(36.65933005148041, 128.11043012690146),
(36.659046581025805, 128.11087342041293),
(36.65873654959274, 128.11126600047396),
(36.658359727405056, 128.11157373675624),
(36.65799175749057, 128.1118983733571),
(36.657610428903894, 128.11220603883686),
(36.65722050772915, 128.1124688422131),
(36.65683477737962, 128.11276525472638),
(36.65646240401742, 128.1130786322501),
(36.656067819364026, 128.11335813584208),
(36.65573101251413, 128.1137223461989),
(36.65532762594584, 128.1139793499413),
(36.65496415451866, 128.114304027487),
(36.6545783151122, 128.11461160433905),
(36.65420153691508, 128.11491371710605),
(36.65383801067514, 128.11524397653284),
(36.653465735747986, 128.11554614836916),
(36.65309330293904, 128.11586508995123),
(36.65278309840015, 128.11627439251606),
(36.65244623051069, 128.11664416566347),
(36.65222543803587, 128.1171274437496),
(36.652031410228105, 128.1176390636725),
(36.65198567177303, 128.11819196613692),
(36.65196240232221, 128.1187507845416),
(36.65203383643457, 128.1192997903105),
(36.65226351468048, 128.11978958150908),
(36.652506968224245, 128.12025161725052),
(36.65282768559453, 128.12064208480112),
(36.65322147838439, 128.12092737392314),
(36.65363842603798, 128.12114590173067),
(36.654060246506255, 128.1213253607259),
(36.65449107625908, 128.12150495241522),
(36.65490367581587, 128.12170664873568),
(36.655311138132156, 128.12197537419175),
(36.655650184695226, 128.12233257849732),
(36.65596167421806, 128.12274530417042),
(36.656137174422945, 128.12324552723686),
(36.656172443240216, 128.12380522470013),
(36.65621221422111, 128.1243649881171),
(36.65624747775839, 128.12492468649654),
(36.65627823385472, 128.12548431974034),
(36.65631354497627, 128.1260384278807),
(36.65611057510771, 128.12653873778834),
(36.65586737743, 128.12700490769242),
(36.655552683386084, 128.1274085222232),
(36.65517582084757, 128.12771616645327),
(36.654759314529045, 128.12792816966768),
(36.6543384619196, 128.12812333209138),
(36.65389603835667, 128.1282175273698)


        };
        // 거리 기반 이동 상태 (Unity 동일)
        //현재까지 이동한 누적 거리
        private double distanceTravelled = 0.0;
        //이번 스냅에서 도달해야 하는 목표 거리 gps로부터 계산된 현재 위치에 해당하는 경로 거리를 targetDistance로 설정
        private double targetDistance = 0.0;
        // 직전 tick에서의 목표 거리 (이전 targetDistance)
        private double preTargetDistance = 0.0;
        //현재 이동 속도
        private double moveSpeed = 0.0;
        //vertax 전체 길이 누적 거리
        private double totalVertexDistance = 0.0;
        //vertex 각 점까지의 누적 거리 배열
        private List<double> vertexCumDist = new();
        //gps 원본경로 각 점까지의 누적 거리 배열
        private List<double> gpsCumDist = new();
        //gps 원본 경로 전체 길이
        private double totalGpsDistance = 0.0;
        //이동 방향 부호 +1 : 정방향, -1 : 역방향
        private int moveDirectionSign = 1;
        //마지막으로 정상 gps 데이터를 수신/확인한 시각
        private DateTime lastGpsOkTime = DateTime.MinValue;
        // 1초 폴링이면 2~3초 정도가 적당 근데 1.5초가 적당할듯(마지막 수신 이후 경과 시간을 계산해서 1.5초를 넘기면 gps가 끊겼거나 지연(stale)상태라 보고 후 포인터를 정지하는데 필요한 필드)
        private const double GPS_STALE_SECONDS = 1.5;
        //마지막으로 화면 (포인터/경로 등)을 렌더링(갱신)한 시각
        private DateTime lastRenderTime;
        //마지막 gps 인덱스 저장용 필드 앱 실행시 포인터 방향 전진/후진 방향 유지를 위한 필드 및 마지막으로 저장된 GPS index (재실행 대비)
        private readonly string lastGpsIndexPath = System.IO.Path.Combine(BaseDir, "last_gps_index.txt");
        //마지막으로 저장해둔 gps 인덱스
        private int lastGpsIndex = -1;
        // 포인터 이미지의 방향 보정(도 단위)
        // +면 시계방향(대체로 화면상 아래쪽으로), -면 반시계방향
        private const double POINTER_ANGLE_OFFSET_DEG = 8.0;  // 2~10 정도로 튜닝
        //재실행 후 "첫 GPS"에서만 스냅하기 위한 플래그 (프로그램 껐다가 다시 킬때의 포인터 위치 스냅 필드)
        private bool gpsSnapInitialized = false;
        // GPS 끊김(stale) 상태였다가 복구되면, 첫 수신에서 스냅하기 위한 플래그  (GPS 연결 끊겼다가 다시 연결될 경우 스냅 필드)
        private bool gpsWasStale = false;
        // 포인터 회전 Transform 재사용 (매 프레임 new RotateTransform 방지)
        private readonly RotateTransform pointerRotate = new RotateTransform(0);
        // 회전 스무딩 상태
        private double currentPointerAngleDeg = 0.0;
        private bool pointerAngleInitialized = false;
        // 스냅 시 1회 보간 금지 플래그
        private bool suppressRotationSmoothingOnce = false;
        // 회전 스무딩 강도(클수록 빨리 따라감). 8~16 사이 추천
        private const double POINTER_ROTATE_SMOOTHNESS = 5.0;
        //지도/캔버스 + 경로 + GPS + 이동 + 포인터/이펙트 + 회전/방향/각도 + 상태 복원/재실행 관련 필드 끝



        //추가 최적화 코드  
        // 외부(MainWindow)에서 읽어야 하는 값 (대시보드 회전/블랙막 제어용) 
        public double MoveSpeed => moveSpeed;
        public int MoveDirectionSign => moveDirectionSign;


        
        private bool _initialized = false;

        public MapPanel()
        {
            InitializeComponent();
          
            Loaded += MapPanel_Loaded;
          
        }
        private void MapPanel_Loaded(object sender, RoutedEventArgs e)
        {
          
            if (_initialized) return;
            _initialized = true;

            InitializeMapSystem();
        }
        //Unloaded() 함수는 삭제함 만약 Unloaded()있으면 만일 다른 페이지로 이동할 경우 모든 계산이 멈춰버림 하지만 이건 실시간 관제시스템이므로 계산을 멈추면 안됨

        private void InitializeMapSystem()
        {
          
            //지도/캔버스 + 경로 + GPS + 이동 + 포인터/이펙트 + 회전/방향/각도 + 상태 복원/재실행 관련 생성자 초기화 시작
            //불값에 따라 좌표 찍기 기능 활성 또는 비활성(개발/운영)
            if (!DEBUG_ROUTE_EDITOR)
            {
                MapCanvas.MouseDown -= MapCanvas_MouseDown;
            }
            //불값에 따라 경로 숨기기 또는 보이기(개발/운영)
            if (DEBUG_ROUTE_EDITOR)
            {
                // 경로 Polyline 추가
                MapCanvas.Children.Add(routeLine);
            }
            else
            {
                routeLine.Visibility = Visibility.Collapsed; // 혹시 이미 올라와있어도 숨김
            }
            // 포인터 임시 배치 (초기 위치)
            PlacePointer(124, 336);
            // pos.txt 로드 + 점/경로 복원
            LoadPathPoints();
            // vertex.txt 로드 (없으면 샘플링 수행)
            vertexPath = LoadVertexPath();
            if (vertexPath.Count == 0 && pathPoints.Count >= 2)
            {
                vertexPath = SampleVertexPath(pathPoints, 5.0);
                SaveVertexPath(vertexPath);
            }
            // vertexPath가 있다면 포인터를 첫 점으로 옮겨두는 것도 가능
            if (vertexPath.Count > 0 && pointerImage != null)
            {
                var p = vertexPath[0];
                Canvas.SetLeft(pointerImage, p.X - pointerImage.Width / 2);
                Canvas.SetTop(pointerImage, p.Y - pointerImage.Height / 2);
                distanceTravelled = 0.0;
                targetDistance = 0.0;
                preTargetDistance = 0.0;
                moveSpeed = 0.0;
                moveDirectionSign = 1;
                gpsSnapInitialized = false;
            }
            // STEP 3: 렌더링 루프(부드러운 이동 + 회전)
            lastRenderTime = DateTime.Now;
            Debug.WriteLine($"[HOOK] Rendering += (hash={GetHashCode()})");
            CompositionTarget.Rendering += CompositionTarget_Rendering;
            //재실행 대비 lastGpsIndex 로드 (마지막 포인터 방향 전환용)
            LoadLastGpsIndex();
            // vertexPath 로드/생성 이후
            RebuildVertexDistanceTable();
            // gpsRoute는 고정 리스트니까 앱 시작 때 1회 생성
            RebuildGpsDistanceTable();
            //  GPS 폴링 타이머 설정
            gpsTimer.Interval = TimeSpan.FromSeconds(1);  // 1초마다 /api/nextpos 호출
            gpsTimer.Tick += GpsTimer_Tick;
         
            gpsTimer.Start();
        
            //지도/캔버스 + 경로 + GPS + 이동 + 포인터/이펙트 + 회전/방향/각도 + 상태 복원/재실행 관련 생성자 초기화 끝 
        }
        //지도/캔버스 + 경로 + GPS + 이동 + 포인터/이펙트 + 회전/방향/각도 + 상태 복원/재실행 관련 메서드 선언 시작

        //MainWindow.xaml.cs에서 http client 주입받기
        public void SetApi(ApiClient api)
        {
            _api = api;
        }
        // 포인터(기차 아이콘) 배치      
        private void PlacePointer(double x, double y)
        {
            //  Glow 이미지 생성 (펄스용 이미지)
            glowImage = new Image()
            {
                Source = new BitmapImage(new Uri("/images/glow3.png", UriKind.Relative)),
                Width = 80,
                Height = 80,
                RenderTransformOrigin = new Point(0.5, 0.5),
                Opacity = 0.0 // 처음엔 안 보이도록
            };

            // Glow 애니메이션을 위한 ScaleTransform (처음 크기 = 0)
            ScaleTransform scale = new ScaleTransform(0.0, 0.0);
            glowImage.RenderTransform = scale;

            // Glow 애니메이션 시작 (커짐  즉시 리셋  커짐 반복)
            StartGlowAnimation(scale);

            // Glow 위치 배치
            Canvas.SetLeft(glowImage, x - glowImage.Width / 2);
            Canvas.SetTop(glowImage, y - glowImage.Height / 2);
            MapCanvas.Children.Add(glowImage);



            // 2) 포인터 이미지         
            pointerImage = new Image()
            {
                Source = new BitmapImage(new Uri("/images/pointer2.png", UriKind.Relative)),
                Width = 40,
                Height = 40,
                RenderTransformOrigin = new Point(0.5, 0.5),
                RenderTransform = pointerRotate   // 회전 트랜스폼 재사용 회전 부드럽게
            };

            Canvas.SetLeft(pointerImage, x - pointerImage.Width / 2);
            Canvas.SetTop(pointerImage, y - pointerImage.Height / 2);

            MapCanvas.Children.Add(pointerImage);
        }


        // 펄스를 나타내는 애니메이션 추가 (사라짐  커짐  즉시 사라짐 반복)
        private void StartGlowAnimation(ScaleTransform scale)
        {
            Duration duration = TimeSpan.FromSeconds(1.8);


            //  Scale 애니메이션 0 -> 1.4 -> 유지

            DoubleAnimationUsingKeyFrames scaleAnimX = new DoubleAnimationUsingKeyFrames();
            DoubleAnimationUsingKeyFrames scaleAnimY = new DoubleAnimationUsingKeyFrames();

            scaleAnimX.KeyFrames.Add(new DiscreteDoubleKeyFrame(0.0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            scaleAnimY.KeyFrames.Add(new DiscreteDoubleKeyFrame(0.0, KeyTime.FromTimeSpan(TimeSpan.Zero)));

            scaleAnimX.KeyFrames.Add(new EasingDoubleKeyFrame(1.4, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.6))));
            scaleAnimY.KeyFrames.Add(new EasingDoubleKeyFrame(1.4, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.6))));

            scaleAnimX.KeyFrames.Add(new DiscreteDoubleKeyFrame(1.4, KeyTime.FromTimeSpan(duration.TimeSpan)));
            scaleAnimY.KeyFrames.Add(new DiscreteDoubleKeyFrame(1.4, KeyTime.FromTimeSpan(duration.TimeSpan)));

            scaleAnimX.RepeatBehavior = RepeatBehavior.Forever;
            scaleAnimY.RepeatBehavior = RepeatBehavior.Forever;


            // 2) Opacity 애니메이션 (0 -> 1 -> 0)      
            DoubleAnimationUsingKeyFrames opacityAnim = new DoubleAnimationUsingKeyFrames();

            opacityAnim.KeyFrames.Add(new EasingDoubleKeyFrame(0.0, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            opacityAnim.KeyFrames.Add(new EasingDoubleKeyFrame(1.0, KeyTime.FromTimeSpan(TimeSpan.FromSeconds(0.6))));
            opacityAnim.KeyFrames.Add(new EasingDoubleKeyFrame(0.0, KeyTime.FromTimeSpan(duration.TimeSpan)));

            opacityAnim.RepeatBehavior = RepeatBehavior.Forever;


            // 애니메이션 적용         
            scale.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnimX);
            scale.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnimY);
            glowImage?.BeginAnimation(UIElement.OpacityProperty, opacityAnim);   // ? 안붙히면 경고 뜸
        }



        // [누적거리 테이블 생성: vertexPath (픽셀)]
        private void RebuildVertexDistanceTable()
        {
            vertexCumDist = new List<double>(vertexPath.Count);
            if (vertexPath.Count == 0)
            {
                totalVertexDistance = 0;
                return;
            }

            vertexCumDist.Add(0.0);

            for (int i = 1; i < vertexPath.Count; i++)
            {
                var a = vertexPath[i - 1];
                var b = vertexPath[i];

                double dx = b.X - a.X;
                double dy = b.Y - a.Y;
                double seg = Math.Sqrt(dx * dx + dy * dy);

                vertexCumDist.Add(vertexCumDist[i - 1] + seg);
            }

            totalVertexDistance = vertexCumDist[^1];
        }

       
        // 누적거리 테이블 생성: gpsRoute (미터)
        //비율 매핑이 아니라 거리 매핑을 위해 필요
       
        private void RebuildGpsDistanceTable()
        {
            gpsCumDist = new List<double>(gpsRoute.Count);
            if (gpsRoute.Count == 0)
            {
                totalGpsDistance = 0;
                return;
            }

            gpsCumDist.Add(0.0);

            for (int i = 1; i < gpsRoute.Count; i++)
            {
                var a = gpsRoute[i - 1];
                var b = gpsRoute[i];

                double segMeters = HaversineMeters(a.lat, a.lng, b.lat, b.lng);
                gpsCumDist.Add(gpsCumDist[i - 1] + segMeters);
            }

            totalGpsDistance = gpsCumDist[^1];
        }

        // Haversine (meters) : 두 gps 좌표(위도 경도) 사이의 지표면 직선거리를 미터 단위로 계산해 반환하는 함수
        private static double HaversineMeters(double lat1, double lon1, double lat2, double lon2)
        {
            const double R = 6371000.0; // Earth radius (m)
            double toRad = Math.PI / 180.0;

            double dLat = (lat2 - lat1) * toRad;
            double dLon = (lon2 - lon1) * toRad;

            double a =
                Math.Sin(dLat / 2) * Math.Sin(dLat / 2) +
                Math.Cos(lat1 * toRad) * Math.Cos(lat2 * toRad) *
                Math.Sin(dLon / 2) * Math.Sin(dLon / 2);

            double c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
            return R * c;
        }

        // 거리 기반 샘플링 distance(픽셀) -> 위치(Point) + tangent Vector
        //누적거리 dist에 해당하는 vertexPath 상의 위치를 선형보간으로 샘플링하고 그 지점의 진행방향까지 함께 계산해 반환하는 함수

        private Point SamplePointAtDistance(double dist, out (double vx, double vy) tangent)
        {
            tangent = (1, 0);

            if (vertexPath.Count == 0 || vertexCumDist.Count != vertexPath.Count)
                return new Point(0, 0);

            if (dist <= 0)
            {
                tangent = TangentAroundIndex(0);
                return vertexPath[0];
            }

            if (dist >= totalVertexDistance)
            {
                tangent = TangentAroundIndex(vertexPath.Count - 1);
                return vertexPath[^1];
            }

            // lower_bound: vertexCumDist[i] >= dist 인 i 찾기
            int i1 = LowerBound(vertexCumDist, dist);
            int i0 = Math.Max(i1 - 1, 0);

            var p0 = vertexPath[i0];
            var p1 = vertexPath[i1];

            double d0 = vertexCumDist[i0];
            double d1 = vertexCumDist[i1];
            double segLen = Math.Max(1e-9, d1 - d0);

            double t = (dist - d0) / segLen;

            double x = p0.X + (p1.X - p0.X) * t;
            double y = p0.Y + (p1.Y - p0.Y) * t;

            // tangent은 중앙차분 느낌으로 주변 인덱스 기반
            int iCenter = (Math.Abs(t) < 0.5) ? i0 : i1;
            tangent = TangentAroundIndex(iCenter);

            return new Point(x, y);
        }

        //주어진 인덱스 주변의 이전/다음 점을 이용해 경로의 진행 방향 벡터(탄젠트)를 계산해서 반환하는 함수
        private (double vx, double vy) TangentAroundIndex(int idx)
        {
            if (vertexPath.Count < 2) return (1, 0);

            int iPrev = Math.Max(idx - 1, 0);
            int iNext = Math.Min(idx + 1, vertexPath.Count - 1);

            var a = vertexPath[iPrev];
            var b = vertexPath[iNext];

            double vx = b.X - a.X;
            double vy = b.Y - a.Y;

            if (Math.Abs(vx) < 1e-9 && Math.Abs(vy) < 1e-9) return (1, 0);

            // 정규화는 선택(각도만 쓰면 굳이 필요X)
            return (vx, vy);
        }

        //정렬된 arr에서 value 이상이 처음 나타나는 위치를 이진 탐색으로 찾아 반환하는 함수 누적거리 dist가 경로의 어느 구간에 있는지를 빠르게 찾기 위해 사용
        private static int LowerBound(List<double> arr, double value)
        {
            int lo = 0, hi = arr.Count - 1;
            while (lo < hi)
            {
                int mid = (lo + hi) / 2;
                if (arr[mid] < value) lo = mid + 1;
                else hi = mid;
            }
            return lo;
        }
        // 캔버스 클릭 -> 경로 점 추가
        private void MapCanvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            //불값에 따라 점 찍기 활성 또는 비활성 (개발/운영)
            if (!DEBUG_ROUTE_EDITOR) return;


            Point p = e.GetPosition(MapCanvas);

            pathPoints.Add(p);
            DrawPoint(p);
            routeLine.Points.Add(p);

            SavePathPoints();

            // 점이 추가되었으니 샘플링 다시 수행
            vertexPath = SampleVertexPath(pathPoints, 5.0);
            SaveVertexPath(vertexPath);
            RebuildVertexDistanceTable();   // 추가

            distanceTravelled = 0.0;
            targetDistance = 0.0;
            preTargetDistance = 0.0;
            moveSpeed = 0.0;
            moveDirectionSign = 1;
            gpsSnapInitialized = false;

        }
        //DEBUG_POINT_EDITOR가 켜져 있을 때만 지정한 좌표 p에 빨간 점을 그려서 경로/ 클릭 위치를 디버깅용으로 시각화하는 함수
        private void DrawPoint(Point p)
        {
            //불 값에 따라 찍은 점 보이기 또는 안보이기 (개발/운영)
            if (!DEBUG_ROUTE_EDITOR) return;

            Ellipse dot = new Ellipse()
            {
                Width = 6,
                Height = 6,
                Fill = Brushes.Red
            };

            Canvas.SetLeft(dot, p.X - 3);
            Canvas.SetTop(dot, p.Y - 3);

            MapCanvas.Children.Add(dot);
        }
        // pos.txt 저장/로드     
        private void SavePathPoints()
        {
            //불 값에 따라 픽셀좌표 저장 또는 저장안하기
            if (!DEBUG_ROUTE_EDITOR) return;

            using StreamWriter writer = new StreamWriter(savePath);
            foreach (var p in pathPoints)
                writer.WriteLine($"{p.X},{p.Y}");
        }

        //저장파일 savePath에서 좌표들을 읽어 pathPoint에 복원하고 각 점을 캔버스에 표시 DrawPoint 하여 경로 선에도 추가해 시각화 하는 함수
        private void LoadPathPoints()
        {
            if (!File.Exists(savePath))
                return;

            foreach (var line in File.ReadAllLines(savePath))
            {
                var tokens = line.Split(',');
                if (tokens.Length != 2)
                    continue;

                if (double.TryParse(tokens[0], out double x) &&
                    double.TryParse(tokens[1], out double y))
                {
                    Point p = new Point(x, y);
                    pathPoints.Add(p);
                    DrawPoint(p);
                    routeLine.Points.Add(p);
                }
            }
        }
        // vertexPath 샘플링 : 원본 경로 점들 originalPoints을 따라 이동하면서 지정 간격 interval마다 선형보간으로 점을 생성, 등간격의 샘플 경
        private List<Point> SampleVertexPath(List<Point> originalPoints, double interval = 5.0)
        {
            List<Point> sampled = new();

            if (originalPoints.Count < 2)
                return sampled;

            sampled.Add(originalPoints[0]); // 시작점 포함

            for (int i = 1; i < originalPoints.Count; i++)
            {
                Point prev = originalPoints[i - 1];
                Point curr = originalPoints[i];

                double dx = curr.X - prev.X;
                double dy = curr.Y - prev.Y;
                double length = Math.Sqrt(dx * dx + dy * dy);

                double t = 0.0;
                while (t < length)
                {
                    double ratio = t / length;
                    double x = prev.X + dx * ratio;
                    double y = prev.Y + dy * ratio;

                    Point newP = new Point(x, y);
                    Point last = sampled[sampled.Count - 1];

                    double dist = Math.Sqrt(
                        (newP.X - last.X) * (newP.X - last.X) +
                        (newP.Y - last.Y) * (newP.Y - last.Y));

                    if (dist >= interval)
                        sampled.Add(newP);

                    t += interval;
                }
            }

            return sampled;
        }

        //DEBUG_ROUTE_EDITOR가 켜져 있을 때만 생성된 vertexPath 점 목록을 vertexSavePath 파일에 x,y 형식으로 저장하는 함수
        private void SaveVertexPath(List<Point> vList)
        {
            //불 값에 따라 vertaxPath 저장 또는 안하기
            if (!DEBUG_ROUTE_EDITOR) return;


            using StreamWriter w = new StreamWriter(vertexSavePath);
            foreach (var p in vList)
                w.WriteLine($"{p.X},{p.Y}");
        }
        //vertextSavePath 파일에서 x,y 좌표들을 읽어 List<Point>로 복원해 반환하는 함수
        private List<Point> LoadVertexPath()
        {
            List<Point> list = new();

            if (!File.Exists(vertexSavePath))
                return list;

            foreach (var line in File.ReadAllLines(vertexSavePath))
            {
                var parts = line.Split(',');
                if (parts.Length != 2)
                    continue;

                if (double.TryParse(parts[0], out double x) &&
                    double.TryParse(parts[1], out double y))
                {
                    list.Add(new Point(x, y));
                }
            }
            return list;
        }
       
        private void CompositionTarget_Rendering(object? sender, EventArgs e)
        {
            if (pointerImage == null || vertexPath.Count < 2) return;
            if (vertexCumDist.Count != vertexPath.Count || totalVertexDistance <= 0) return;

            // GPS가 일정 시간 이상 안 오면 자동 정지
            if (gpsSnapInitialized)
            {
                double age = (DateTime.Now - lastGpsOkTime).TotalSeconds;
                if (lastGpsOkTime != DateTime.MinValue && age > GPS_STALE_SECONDS)
                {
                    moveSpeed = 0.0;
                    gpsWasStale = true;  //끊겼던 상태임을 표시 -> 다음에 GPS가 다시 들어오면 스냅 복구
                    // targetDistance = distanceTravelled; // (선택) 목표도 현재로 고정
                }
            }

            DateTime now = DateTime.Now;
            double deltaTime = (now - lastRenderTime).TotalSeconds;
            if (deltaTime <= 0) return;
            lastRenderTime = now;

            distanceTravelled = Math.Clamp(distanceTravelled + moveSpeed * deltaTime, 0.0, totalVertexDistance);

            if (Math.Abs(moveSpeed) > 1e-6)
                moveDirectionSign = Math.Sign(moveSpeed);

            var pos = SamplePointAtDistance(distanceTravelled, out var tan);

            Canvas.SetLeft(pointerImage, pos.X - pointerImage.Width / 2);
            Canvas.SetTop(pointerImage, pos.Y - pointerImage.Height / 2);

            if (glowImage != null)
            {
                Canvas.SetLeft(glowImage, pos.X - glowImage.Width / 2);
                Canvas.SetTop(glowImage, pos.Y - glowImage.Height / 2);
            }

            double vx = tan.vx;
            double vy = tan.vy;

            if (Math.Abs(vx) > 1e-6 || Math.Abs(vy) > 1e-6)
            {
                vx *= moveDirectionSign;
                vy *= moveDirectionSign;

                double angleRad = Math.Atan2(vy, vx);
                double desiredAngleDeg = angleRad * 180.0 / Math.PI;
                desiredAngleDeg += POINTER_ANGLE_OFFSET_DEG;

                //  여기부터 스무딩 적용 
                bool isMoving = Math.Abs(moveSpeed) > 1e-6;

                // 스냅(재실행/복구) 직후 1회는 즉시 각도 세팅
                if (!pointerAngleInitialized || suppressRotationSmoothingOnce || !isMoving)
                {
                    currentPointerAngleDeg = desiredAngleDeg;
                    pointerAngleInitialized = true;
                    suppressRotationSmoothingOnce = false;

                    pointerRotate.Angle = currentPointerAngleDeg;
                }
                else
                {
                    // 프레임 시간 기반 지수 보간 (dt가 튀어도 안정적)
                    double t = 1.0 - Math.Exp(-POINTER_ROTATE_SMOOTHNESS * deltaTime);
                    t = Math.Clamp(t, 0.0, 1.0);

                    currentPointerAngleDeg = LerpAngleDeg(currentPointerAngleDeg, desiredAngleDeg, t);
                    pointerRotate.Angle = currentPointerAngleDeg;
                }
            }
        }
        //포인터 회전 스무딩 보간함수 시작
        private static double LerpAngleDeg(double current, double target, double t)
        {
            double delta = DeltaAngleDeg(current, target);
            return current + delta * t;
        }

        // -180~180 범위로 각도 차이를 정규화 (359도->0도 같은 케이스 튐 방지)
        private static double DeltaAngleDeg(double a, double b)
        {
            double d = (b - a) % 360.0;
            if (d > 180.0) d -= 360.0;
            if (d < -180.0) d += 360.0;
            return d;
        }
        //포인터 회전 스무딩 보간함수 끝



        // 주기적으로 gps api를 호출해 좌표를 받아 목표 거리/스냅 상태(UpdateTargetFromGps)를 갱신하고 끊김 복구 여부를 반영하며 실패 시 이동을 정지시키는 타이머 핸들러
        private bool _gpsPolling = false;
        private async void GpsTimer_Tick(object? sender, EventArgs e)
        {
            if (_gpsPolling) return;
            _gpsPolling = true;


            try
            {
                if (_api == null) return; // 아직 주입 안됐으면 무시

                var data = await _api.GetNextPosAsync();
                if (data == null) return;

                bool forceSnap = gpsWasStale;

                lastGpsOkTime = DateTime.Now;
                UpdateTargetFromGps(data.lat, data.lng, forceSnap);

                gpsWasStale = false;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"GPS 요청 실패: {ex.Message}");
                moveSpeed = 0.0;
                gpsWasStale = true;
            }
            finally {
                _gpsPolling = false;
            }
        }

      
        // GPS(lat,lng) → gpsRoute 최근접 index → gps 누적거리 → vertex 누적거리 스케일 → targetDistance
        private void UpdateTargetFromGps(double lat, double lng, bool forceSnap)
        {
            if (gpsRoute.Count == 0 || vertexPath.Count == 0)
                return;

            if (totalVertexDistance <= 0)
                return;

            int gpsIndex = FindNearestGpsIndex(lat, lng);

            // gpsIndex -> gps 누적거리(미터)
            double gpsDistance = (gpsCumDist.Count == gpsRoute.Count) ? gpsCumDist[gpsIndex] : 0.0;

            // gps누적거리(미터) -> vertex누적거리(픽셀)로 스케일링
            // (gpsRoute가 들쭉날쭉해도 "거리 기반"이라 위치 틀어짐 최소)
            if (totalGpsDistance > 1e-6)
                targetDistance = gpsDistance * (totalVertexDistance / totalGpsDistance);
            else
                targetDistance = 0.0;

            // 1) 재실행 첫 GPS: 방향 복원 (lastGpsIndex 기반)
            if (!gpsSnapInitialized)
            {
                if (gpsIndex <= 0) moveDirectionSign = 1;
                else if (lastGpsIndex >= 0 && lastGpsIndex != gpsIndex)
                {
                    int sign = Math.Sign(gpsIndex - lastGpsIndex);
                    if (sign != 0) moveDirectionSign = sign;
                }
                else
                {
                    moveDirectionSign = 1;
                }
            }
            // 속도 = (이번 목표거리 - 이전 목표거리)/interval
            double intervalSec = gpsTimer.Interval.TotalSeconds;
            if (intervalSec <= 1e-6) intervalSec = 1.0;
            // 3) lastGpsIndex 갱신/저장 (방향 복원에도 쓰임)
            if (gpsIndex != lastGpsIndex)
            {
                lastGpsIndex = gpsIndex;
                SaveLastGpsIndex(lastGpsIndex);
            }
            //  앱 첫 수신 OR GPS 끊김 후 복구 수신이면 "즉시 스냅"
            if (!gpsSnapInitialized || forceSnap)
            {
                distanceTravelled = targetDistance;
                preTargetDistance = targetDistance;   // 다음 틱 속도 폭주 방지
                moveSpeed = 0.0;                      // 복구 첫 틱은 이동값 0으로 안정화
                lastRenderTime = DateTime.Now;        // dt 튐 방지

                suppressRotationSmoothingOnce = true; // 스냅이면 회전도 즉시 적용
                pointerAngleInitialized = false;     //스냅 직후 첫 각도는 무조건 즉시 세팅


                gpsSnapInitialized = true;
                return; // 복구 첫 틱은 여기서 종료 (다음 틱부터 정상 속도 계산)
            }

            // 정상 상태면 속도 계산
            moveSpeed = (targetDistance - preTargetDistance) / intervalSec; // 픽셀/초 (음수면 후진)
            preTargetDistance = targetDistance;
        }
        // KDTree 대신 O(N) 최근접 검색 (좌표 수가 많지 않아서 충분히 빠름) gps좌표와 gpsRoute의 모든 점을 비교해 가장 가까운(거리 제곱이 최소) 점의 인덱스를 O(N)으로 찾아 반환하는 함수
        private int FindNearestGpsIndex(double lat, double lng)
        {
            int bestIndex = 0;
            double bestDistSq = double.MaxValue;

            for (int i = 0; i < gpsRoute.Count; i++)
            {
                var p = gpsRoute[i];
                double dLat = p.lat - lat;
                double dLng = p.lng - lng;
                double distSq = dLat * dLat + dLng * dLng;

                if (distSq < bestDistSq)
                {
                    bestDistSq = distSq;
                    bestIndex = i;
                }
            }
            return bestIndex;
        }
        // lastGpsIndex 파일 I/O
        //앱 실행 시점에 last_gps_index.txt를 읽어 마지막 엔덱스를 복원
        private void LoadLastGpsIndex()
        {
            try
            {
                if (!File.Exists(lastGpsIndexPath))
                {
                    lastGpsIndex = -1;
                    return;
                }

                string txt = File.ReadAllText(lastGpsIndexPath).Trim();
                if (int.TryParse(txt, out int idx))
                    lastGpsIndex = idx;
                else
                    lastGpsIndex = -1;
            }
            catch
            {
                // 파일 읽기 실패해도 앱은 계속 돌아가야 하므로 무시
                lastGpsIndex = -1;
            }
        }
        private void SaveLastGpsIndex(int idx)
        {
            try
            {
                File.WriteAllText(lastGpsIndexPath, idx.ToString());
            }
            catch
            {
                // 저장 실패해도 앱은 계속 돌아가야 하므로 무시
            }
        }
        //지도/캔버스 + 경로 + GPS + 이동 + 포인터/이펙트 + 회전/방향/각도 + 상태 복원/재실행 관련 메서드 선언 끝
    }
}
