using Map.Popups;
using Map.Services.Interfaces;
using System.Windows;

namespace Map.Services
{
    public sealed class ButtonAlertDialogService : IButtonAlertDialogService
    {
        private readonly Window _owner;

        public ButtonAlertDialogService(Window owner)
        {
            _owner = owner;
        }

        public bool ShowMessage(string message)
        {
            var popup = new ButtonAlertPopup(message)
            {
                Owner = _owner
            };

            bool? result = popup.ShowDialog();
            return result == true;
        }
    }
}