using Map.Models;

namespace Map.Services.Interfaces
{
    public interface IAdminSettingsDialogService
    {
        bool ShowDialog(
            string currentServerBaseUrl,
            //영상 주소
            string currentVideoServerBaseUrl,
            SideAlertSettings currentBSettings,
            SideAlertSettings currentASettings,
            out string updatedServerBaseUrl,
            //영상 주소
            out string updatedVideoServerBaseUrl,
            out SideAlertSettings updatedBSettings,
            out SideAlertSettings updatedASettings);
    }
}