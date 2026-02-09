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
using System.Windows.Shapes;

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
            Close();    
        }
    }
}
