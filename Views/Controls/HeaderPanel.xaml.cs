using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace Map.Views.Controls
{
  
    public partial class HeaderPanel : UserControl
    {
        private readonly DispatcherTimer _timer = new DispatcherTimer();
        public HeaderPanel()
        {
            InitializeComponent();

            // 최초 표시
            TimeText = DateTime.Now.ToString("yyyy년 MM월 dd일 - HH:mm:ss");

            _timer.Interval = TimeSpan.FromSeconds(1);
            _timer.Tick += (_, __) =>
            {
                TimeText = DateTime.Now.ToString("yyyy년 MM월 dd일 - HH:mm:ss");
            };
            _timer.Start();

            //언로드되면 타이머 정지: 메모리 이벤트 누수 방지
            Unloaded += (_, __) => _timer.Stop();
        }
        public string TimeText
        {
            get => (string)GetValue(TimeTextProperty);
            set => SetValue(TimeTextProperty, value);
        }
        public static readonly DependencyProperty TimeTextProperty =
            DependencyProperty.Register(
                nameof(TimeText),
                typeof(string),
                typeof(HeaderPanel),
                new PropertyMetadata("0000년 00월 00일 - 00:00:00")
            );
    }
}
