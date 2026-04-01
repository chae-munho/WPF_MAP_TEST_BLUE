using Map.Models;
using System;
using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;

namespace Map.Popups
{
    public partial class CameraPopup : Window
    {
        public int CurrentTrain { get; private set; }
        public int CurrentCarNo { get; private set; }

        public CameraPopup()
        {
            InitializeComponent();
        }

        public void ShowCamera(int trainNo, int carNo)
        {
            CurrentTrain = trainNo;
            CurrentCarNo = carNo;

            txtSelectedTitle.Text = $"열차 {trainNo} - {carNo}번 객차 호출";
            txtSelectedInfo.Text = $"train={trainNo}, car={carNo}";
            txtVideoPlaceholder.Visibility = Visibility.Visible;
        }

        public void UpdateFrame(WsVideoFrameMessage frame)
        {
            if (frame == null || string.IsNullOrWhiteSpace(frame.ImageBase64))
                return;

            CurrentTrain = frame.Train;
            CurrentCarNo = frame.CarNo;

            txtSelectedTitle.Text = $"열차 {frame.Train} - {frame.CarNo}번 객차 호출";
            txtSelectedInfo.Text = $"수신시각(UTC): {frame.Timestamp} / {frame.Width}x{frame.Height} / {frame.Format}";

            byte[] bytes = Convert.FromBase64String(frame.ImageBase64);

            using MemoryStream ms = new(bytes);
            BitmapImage image = new();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = ms;
            image.EndInit();
            image.Freeze();

            imgVideo.Source = image;
            txtVideoPlaceholder.Visibility = Visibility.Collapsed;
        }

        public void ClearFrame()
        {
            imgVideo.Source = null;
            txtVideoPlaceholder.Visibility = Visibility.Visible;
        }
    }
}