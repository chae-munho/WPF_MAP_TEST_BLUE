namespace Map.Services.Interfaces
{
    public interface ICameraPopupService
    {
        void ShowIntercomPopup(int trainNo, int carNo);
        void CloseAll();
    }
}