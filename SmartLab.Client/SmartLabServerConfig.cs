using System;
using System.IO;
using System.Text.Json;

namespace SmartLab.Client
{
    public static class SmartLabServerConfig
    {
        public static string BaseUrl { get; }

        static SmartLabServerConfig()
        {
            string serverUrl = "http://localhost:5047/";

            try
            {
                string configPath = Path.Combine(
                    AppContext.BaseDirectory,
                    "appsettings.json"
                );

                if (File.Exists(configPath))
                {
                    string json = File.ReadAllText(configPath);

                    using JsonDocument document =
                        JsonDocument.Parse(json);

                    if (document.RootElement.TryGetProperty(
                        "ServerUrl",
                        out JsonElement serverUrlElement))
                    {
                        string? configuredUrl =
                            serverUrlElement.GetString();

                        if (!string.IsNullOrWhiteSpace(configuredUrl))
                        {
                            serverUrl = configuredUrl;
                        }
                    }
                }
            }
            catch
            {
                // Use default URL if config cannot be loaded.
            }

            BaseUrl = serverUrl.EndsWith("/")
                ? serverUrl
                : serverUrl + "/";
        }
    }
}