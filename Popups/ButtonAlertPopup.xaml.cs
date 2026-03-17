using System.Windows;

namespace Map.Popups
{
    public partial class ButtonAlertPopup : Window
    {
        public ButtonAlertPopup(string message)
        {
            InitializeComponent();
            MessageText.Text = message;
        }

        private void Confirm_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}