using Map.Models;
using Map.Services.Interfaces;
using System;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Map.Services
{
    public class AppSettingsService : IAppSettingsService
    {
        private const string FileName = "appsettings.json";

        public AppSettings Load()
        {
            string filePath = GetSettingsFilePath();

            try
            {
                EnsureDataFolderExists();

                if (!File.Exists(filePath))
                {
                    var defaultSettings = new AppSettings();
                    Save(defaultSettings);
                    return defaultSettings;
                }

                string json = File.ReadAllText(filePath);
                var settings = JsonSerializer.Deserialize<AppSettings>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (settings == null)
                    return new AppSettings();

                settings.ASettings ??= SideAlertSettings.CreateDefaultA();
                settings.BSettings ??= SideAlertSettings.CreateDefaultB();

                if (string.IsNullOrWhiteSpace(settings.ServerBaseUrl))
                    settings.ServerBaseUrl = "http://192.168.0.173:5090";

                return settings;
            }
            catch
            {
                return new AppSettings();
            }
        }

        public void Save(AppSettings settings)
        {
            string filePath = GetSettingsFilePath();
            EnsureDataFolderExists();

            var options = new JsonSerializerOptions
            {
                WriteIndented = true
            };

            string json = JsonSerializer.Serialize(settings, options);
            File.WriteAllText(filePath, json);
        }

        private void EnsureDataFolderExists()
        {
            string dataFolder = GetDataFolderPath();
            if (!Directory.Exists(dataFolder))
                Directory.CreateDirectory(dataFolder);
        }

        private string GetSettingsFilePath()
        {
            return Path.Combine(GetDataFolderPath(), FileName);
        }

        private string GetDataFolderPath()
        {
            var dir = new DirectoryInfo(AppDomain.CurrentDomain.BaseDirectory);

            while (dir != null)
            {
                string candidateDataFolder = Path.Combine(dir.FullName, "data");
                bool hasCsproj = Directory.GetFiles(dir.FullName, "*.csproj").Any();

                if (Directory.Exists(candidateDataFolder) || hasCsproj)
                    return candidateDataFolder;

                dir = dir.Parent;
            }

            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data");
        }
    }
}