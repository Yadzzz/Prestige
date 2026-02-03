using System;
using System.IO;
using System.Text.Json;

namespace Server.Configuration
{
    public class ConfigService
    {
        private static ConfigService _instance;
        private static readonly object _lock = new object();

        public EnvironmentConfig Current { get; private set; }

        private ConfigService()
        {
            LoadConfiguration();
        }

        public static ConfigService Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        if (_instance == null)
                        {
                            _instance = new ConfigService();
                        }
                    }
                }
                return _instance;
            }
        }

        private void LoadConfiguration()
        {
            try
            {
                string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
                
                // Fallback to project root if not found in bin (useful for debugging)
                if (!File.Exists(configPath))
                {
                    string projectRoot = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\"));
                    configPath = Path.Combine(projectRoot, "appsettings.json");
                }

                if (!File.Exists(configPath))
                {
                    throw new FileNotFoundException($"Configuration file not found at {configPath}");
                }

                string jsonContent = File.ReadAllText(configPath);
                var appConfig = JsonSerializer.Deserialize<AppConfig>(jsonContent);

                if (appConfig == null || string.IsNullOrEmpty(appConfig.ActiveProfile))
                {
                    throw new InvalidOperationException("Invalid configuration: ActiveProfile is missing.");
                }

                if (!appConfig.Environments.ContainsKey(appConfig.ActiveProfile))
                {
                    throw new InvalidOperationException($"Environment '{appConfig.ActiveProfile}' not found in configuration.");
                }

                Current = appConfig.Environments[appConfig.ActiveProfile];
                Console.WriteLine($"[Config] Loaded profile: {appConfig.ActiveProfile} ({Current.ServerName})");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Config] CRITICAL ERROR loading configuration: {ex.Message}");
                throw;
            }
        }
    }
}
