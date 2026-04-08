using Map.Services;
using System;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Map.Views.Popups
{
    public partial class CameraPopup : Window
    {
        private readonly TrainVideoWebSocketServerService _videoServer;
        private readonly DispatcherTimer _frameTimer = new();
        private bool _confirmed;
        private bool _confirmAsked;
        private long _lastSequence;
        //취소/x시 stop 요청
        private bool _stopRequested;

        public int CurrentTrainNo { get; private set; }
        public int CurrentCarNo { get; private set; }

        public CameraPopup(TrainVideoWebSocketServerService videoServer)
        {
            InitializeComponent();
            _videoServer = videoServer;

            _frameTimer.Interval = TimeSpan.FromMilliseconds(100);
            _frameTimer.Tick += FrameTimer_Tick;
        }

        public void ShowIntercom(int trainNo, int carNo)
        {
            CurrentTrainNo = trainNo;
            CurrentCarNo = carNo;

            txtSelectedTitle.Text = $"{carNo}번 객차 호출";
            txtSelectedUrl.Text = "실시간 CCTV 영상";
            txtVideoPlaceholder.Text = "영상 대기 중...";
            txtVideoPlaceholder.Visibility = Visibility.Visible;
            VideoImage.Source = null;
        }

        protected override async void OnContentRendered(EventArgs e)
        {
            base.OnContentRendered(e);

            if (_confirmAsked)
                return;

            _confirmAsked = true;

            var result = MessageBox.Show(
                $"{CurrentCarNo}번 객차에서 인터컴 호출이 발생했습니다. CCTV를 확인하시겠습니까?",
                "인터컴 호출",
                MessageBoxButton.OKCancel,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.OK)
            {
                await SendStopRequestAsync();
                Close();
                return;
            }

            _confirmed = true;
            _frameTimer.Start();
        }
        private async Task SendStopRequestAsync()
        {
            if (_stopRequested)
                return;

            _stopRequested = true;

            try
            {
                if (CurrentTrainNo > 0 && CurrentCarNo > 0)
                {
                    await _videoServer.SendVideoControlAsync(CurrentTrainNo, CurrentCarNo, "stop");
                }
            }
            catch
            {
            }
        }

        private void FrameTimer_Tick(object? sender, EventArgs e)
        {
            if (!_confirmed)
                return;

            if (!_videoServer.TryGetLatestFrame(CurrentTrainNo, CurrentCarNo, out byte[] jpegBytes, out long sequence))
                return;

            if (sequence == _lastSequence)
                return;

            _lastSequence = sequence;

            try
            {
                using var ms = new MemoryStream(jpegBytes);

                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.StreamSource = ms;
                bitmap.EndInit();
                bitmap.Freeze();

                VideoImage.Source = bitmap;
                txtVideoPlaceholder.Visibility = Visibility.Collapsed;
            }
            catch
            {
                txtVideoPlaceholder.Text = "영상 디코딩 실패";
                txtVideoPlaceholder.Visibility = Visibility.Visible;
            }
        }

        protected override async void OnClosed(EventArgs e)
        {
            try
            {
                _frameTimer.Stop();
                _frameTimer.Tick -= FrameTimer_Tick;
                await SendStopRequestAsync();
            }
            catch
            {
            }

            base.OnClosed(e);
        }
    }
}