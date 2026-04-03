using Map.Services.Interfaces;
using Map.Views.Popups;
using System.Collections.Generic;
using System.Windows;

namespace Map.Services
{
    public sealed class CameraPopupService : ICameraPopupService
    {
        private readonly Window _owner;
        private readonly TrainVideoWebSocketServerService _videoServer;
        private readonly Dictionary<string, CameraPopup> _popups = new();

        public CameraPopupService(Window owner, TrainVideoWebSocketServerService videoServer)
        {
            _owner = owner;
            _videoServer = videoServer;
        }

        public void ShowIntercomPopup(int trainNo, int carNo)
        {
            string key = $"{trainNo}_{carNo}";

            if (_popups.TryGetValue(key, out CameraPopup? existingPopup))
            {
                if (existingPopup.IsVisible)
                {
                    existingPopup.Activate();
                    existingPopup.Topmost = true;
                    existingPopup.Topmost = false;
                    existingPopup.Focus();
                    return;
                }

                _popups.Remove(key);
            }

            var popup = new CameraPopup(_videoServer)
            {
                Owner = _owner
            };

            popup.Closed += (_, _) =>
            {
                if (_popups.ContainsKey(key) && ReferenceEquals(_popups[key], popup))
                    _popups.Remove(key);
            };

            popup.ShowIntercom(trainNo, carNo);
            _popups[key] = popup;
            popup.Show();
            popup.Activate();
        }

        public void CloseAll()
        {
            var popups = new List<CameraPopup>(_popups.Values);
            _popups.Clear();

            foreach (var popup in popups)
            {
                try
                {
                    popup.Close();
                }
                catch
                {
                }
            }
        }
    }
}