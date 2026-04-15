using Map.Services;
using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace Map.Views.Popups
{
    public partial class CameraPopup : Window
    {
        private readonly TrainVideoWebSocketServerService _videoServer;

        private CancellationTokenSource? _frameLoopCts;

        private bool _confirmed;
        private bool _confirmAsked;
        private bool _stopRequested;

        private long _lastSequence;

        // 중복 디코딩 방지용
        private int _isRendering;

        public int CurrentTrainNo { get; private set; }
        public int CurrentCarNo { get; private set; }

        public CameraPopup(TrainVideoWebSocketServerService videoServer)
        {
            InitializeComponent();
            _videoServer = videoServer;
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

            _lastSequence = 0;
            _confirmed = false;
            _confirmAsked = false;
            _stopRequested = false;
            Interlocked.Exchange(ref _isRendering, 0);
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
            StartFrameLoop();
        }

        private void StartFrameLoop()
        {
            StopFrameLoop();

            _frameLoopCts = new CancellationTokenSource();
            _ = RunFrameLoopAsync(_frameLoopCts.Token);
        }

        private void StopFrameLoop()
        {
            try
            {
                _frameLoopCts?.Cancel();
            }
            catch
            {
            }
            finally
            {
                _frameLoopCts?.Dispose();
                _frameLoopCts = null;
            }
        }

        private async Task RunFrameLoopAsync(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    if (!_confirmed || CurrentTrainNo <= 0 || CurrentCarNo <= 0)
                    {
                        await Task.Delay(200, token);
                        continue;
                    }

                    if (!_videoServer.TryGetLatestFrame(CurrentTrainNo, CurrentCarNo, out byte[] jpegBytes, out long sequence))
                    {
                        await Task.Delay(200, token);
                        continue;
                    }

                    if (sequence == _lastSequence)
                    {
                        await Task.Delay(200, token);
                        continue;
                    }

                    // 이미 이전 프레임 디코딩 중이면 이번 프레임은 버림
                    if (Interlocked.Exchange(ref _isRendering, 1) == 1)
                    {
                        await Task.Delay(50, token);
                        continue;
                    }

                    try
                    {
                        _lastSequence = sequence;

                        BitmapImage bitmap = await DecodeBitmapAsync(jpegBytes, token);

                        await Dispatcher.InvokeAsync(() =>
                        {
                            if (!_confirmed || token.IsCancellationRequested)
                                return;

                            VideoImage.Source = bitmap;
                            txtVideoPlaceholder.Visibility = Visibility.Collapsed;
                        }, DispatcherPriority.Render, token);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch
                    {
                        try
                        {
                            await Dispatcher.InvokeAsync(() =>
                            {
                                txtVideoPlaceholder.Text = "영상 디코딩 실패";
                                txtVideoPlaceholder.Visibility = Visibility.Visible;
                            }, DispatcherPriority.Render, token);
                        }
                        catch
                        {
                        }
                    }
                    finally
                    {
                        Interlocked.Exchange(ref _isRendering, 0);
                    }

                    await Task.Delay(200, token);
                }
            }
            catch (OperationCanceledException)
            {
            }
            catch
            {
            }
        }

        private static Task<BitmapImage> DecodeBitmapAsync(byte[] jpegBytes, CancellationToken token)
        {
            return Task.Run(() =>
            {
                token.ThrowIfCancellationRequested();

                using var ms = new MemoryStream(jpegBytes);

                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.CreateOptions = BitmapCreateOptions.PreservePixelFormat;
                bitmap.StreamSource = ms;
                bitmap.EndInit();
                bitmap.Freeze();

                token.ThrowIfCancellationRequested();

                return bitmap;
            }, token);
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

        protected override async void OnClosed(EventArgs e)
        {
            try
            {
                _confirmed = false;
                StopFrameLoop();
                await SendStopRequestAsync();
            }
            catch
            {
            }

            base.OnClosed(e);
        }
    }
}