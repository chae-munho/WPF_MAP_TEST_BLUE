using Map.Models;

namespace Map.Services.Interfaces
{
    public interface IAdminSettingsDialogService
    {
        bool ShowDialog(
            SideAlertSettings currentBSettings,
            SideAlertSettings currentASettings,
            out SideAlertSettings updatedBSettings,
            out SideAlertSettings updatedASettings);
    }
}