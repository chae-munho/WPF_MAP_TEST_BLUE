using Map.Models;
using Map.Services.Interfaces;
using Map.Views.Popups;
using System.Windows;

namespace Map.Services
{
    public class AdminSettingsDialogService : IAdminSettingsDialogService
    {
        private readonly Window _owner;

        public AdminSettingsDialogService(Window owner)
        {
            _owner = owner;
        }

        public bool ShowDialog(
            string currentServerBaseUrl,
            string currentVideoServerBaseUrl,
            SideAlertSettings currentBSettings,
            SideAlertSettings currentASettings,
            out string updatedServerBaseUrl,
            out string updatedVideoServerBaseUrl,
            out SideAlertSettings updatedBSettings,
            out SideAlertSettings updatedASettings)
        {
            var popup = new AdminSettingsPopup(
                currentServerBaseUrl,
                currentVideoServerBaseUrl,
                currentBSettings.Clone(),
                currentASettings.Clone())
            {
                Owner = _owner
            };

            bool? result = popup.ShowDialog();

            if (result == true)
            {
                updatedServerBaseUrl = popup.ServerBaseUrl;
                updatedVideoServerBaseUrl = popup.VideoServerBaseUrl;
                updatedBSettings = popup.BSettings;
                updatedASettings = popup.ASettings;
                return true;
            }

            updatedServerBaseUrl = currentServerBaseUrl;
            updatedVideoServerBaseUrl = currentVideoServerBaseUrl;
            updatedBSettings = currentBSettings;
            updatedASettings = currentASettings;
            return false;
        }
    }
}