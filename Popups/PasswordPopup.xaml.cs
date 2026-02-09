using System;
using System.Windows;
using System.Windows.Media;

namespace Map.Popups
{
    public partial class PasswordPopup : Window
    {
        //임시 비밀번호 (나중에 PLC / 서버 / 설정파일로 교체)
        private const string MASTER_PASSWORD = "ean1234@";

        public PasswordPopup()
        {
            InitializeComponent();

            // 버튼 이벤트 연결
            ConfirmButton.Click += ConfirmButton_Click;
            CancelButton.Click += CancelButton_Click;
        }

      
        // 해상도 대응 스케일 처리    
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            if (Owner == null) return;

            //메인 윈도우 기준 디자인 해상도
            double baseWidth = 1750;
            double baseHeight = 1030;

            double scaleX = Owner.ActualWidth / baseWidth;
            double scaleY = Owner.ActualHeight / baseHeight;

            double scale = Math.Min(scaleX, scaleY);

            PopupScale.ScaleX = scale;
            PopupScale.ScaleY = scale;
        }      
        // 확인 버튼
        private void ConfirmButton_Click(object sender, RoutedEventArgs e)
        {
            string input = PasswordInput.Password;

            if (input == MASTER_PASSWORD)
            {
                // 성공
                DialogResult = true;   // MainWindow에서 결과 확인 가능 MainWindow에서 bool값으로 넘겨줌
                Close();
            }
            else
            {
                //  실패
                ErrorText.Visibility = Visibility.Visible;
                PasswordInput.Clear();
                PasswordInput.Focus();
            }
        }     
        //  취소 버튼
        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
