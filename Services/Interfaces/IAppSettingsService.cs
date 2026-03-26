using Map.Models;

namespace Map.Services.Interfaces
{
    public interface IAppSettingsService
    {
        AppSettings Load();
        void Save(AppSettings settings);
    }
}