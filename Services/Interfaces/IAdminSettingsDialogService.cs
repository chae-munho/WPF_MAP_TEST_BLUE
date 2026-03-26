using Map.Models;

namespace Map.Services.Interfaces
{
    public interface IAdminSettingsDialogService
    {
        bool ShowDialog(
            string currentServerBaseUrl,
            SideAlertSettings currentBSettings,
            SideAlertSettings currentASettings,
            out string updatedServerBaseUrl,
            out SideAlertSettings updatedBSettings,
            out SideAlertSettings updatedASettings);
    }
}