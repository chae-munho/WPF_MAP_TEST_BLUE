using Map.Popups;
using Map.Services.Interfaces;
using System.Windows;

namespace Map.Services
{
    public sealed class DangerDialogService : IDangerDialogService
    {
        private readonly Window _owner;

        public DangerDialogService(Window owner)
        {
            _owner = owner;
        }

        public void ShowMessage(string message)
        {
            var popup = new DangerPopup(message)
            {
                Owner = _owner
            };

            popup.Show();
        }
    }
}