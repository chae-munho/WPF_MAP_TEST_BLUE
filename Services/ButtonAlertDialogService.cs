using Map.Popups;
using Map.Services.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace Map.Services
{
    public sealed class ButtonAlertDialogService : IButtonAlertDialogService
    {
        private readonly Window _owner;
        public ButtonAlertDialogService(Window owner) => _owner = owner;

        public void ShowMessage(string message)
        {
            var popup = new ButtonAlertPopup(message) { Owner = _owner };
            popup.ShowDialog();
        }
    }
}
