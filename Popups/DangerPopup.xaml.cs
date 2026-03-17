using System.Windows;

namespace Map.Popups
{
    public partial class DangerPopup : Window
    {
        public DangerPopup(string message)
        {
            InitializeComponent();
            MessageText.Text = message;
        }

        private void Confirm_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }
    }
}