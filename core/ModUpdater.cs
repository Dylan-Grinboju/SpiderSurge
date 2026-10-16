using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Logger = Silk.Logger;

namespace SpiderSurge.Common
{
    public static class ModUpdater
    {
        public const string CheckForUpdatesKey = "general.checkForUpdates";
        public const string ReleasesUrl = "https://github.com/Dylan-Grinboju/SpiderSurge/releases";

        private const string LatestVersionUrl = "https://raw.githubusercontent.com/Dylan-Grinboju/SpiderSurge/main/version.txt";
        private const string DownloadUrl = "https://github.com/Dylan-Grinboju/SpiderSurge/releases/tag/v{0}";
        private const int StartDelayMs = 15000;
        private const int RequestTimeoutMs = 5000;

        private static readonly HttpClient _httpClient = new HttpClient();

        private static string CurrentVersion => SpiderSurgeMod.Version;

        public static bool CheckForUpdates => Settings.GetBool(CheckForUpdatesKey);

        public static IEnumerable<KeyValuePair<string, object>> Defaults => new[]
        {
            new KeyValuePair<string, object>(CheckForUpdatesKey, true),
        };

        public static void ScheduleCheck()
        {
            if (!CheckForUpdates)
            {
                Logger.LogInfo("Update checking is turned off in the settings");
                return;
            }

            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(StartDelayMs);
                    await CheckForUpdatesAsync();
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Update check failed: {ex.Message}");
                }
            });
        }

        private static async Task CheckForUpdatesAsync()
        {
            try
            {
                var latestVersion = await GetLatestVersionAsync();
                Logger.LogInfo($"Latest version: {latestVersion}, current version: {CurrentVersion}");

                if (IsNewerVersion(latestVersion, CurrentVersion))
                {
                    Logger.LogInfo("A new version of SpiderSurge is available!");
                    MainThread.Enqueue(() => ShowUpdatePrompt(latestVersion));
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to check for SpiderSurge updates: {ex.Message}");
            }
        }

        private static async Task<string> GetLatestVersionAsync()
        {
            using (var timeout = new CancellationTokenSource(RequestTimeoutMs))
            {
                using (var response = await _httpClient.GetAsync(LatestVersionUrl, timeout.Token))
                {
                    response.EnsureSuccessStatusCode();
                    var content = await response.Content.ReadAsStringAsync();
                    return content.Trim();
                }
            }
        }

        private static bool IsNewerVersion(string latestVersion, string currentVersion)
        {
            try
            {
                return new Version(latestVersion) > new Version(currentVersion);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to compare versions: {ex.Message}");
                return false;
            }
        }

        private static void ShowUpdatePrompt(string latestVersion)
        {
            if (!CheckForUpdates)
            {
                return;
            }

            var downloadUrl = string.Format(DownloadUrl, latestVersion);
            Logger.LogInfo($"Download URL: {downloadUrl}");

            Announcer.TwoOptionsPopup(
                $"SpiderSurge v{latestVersion} is available!\nCurrent version: {CurrentVersion}\nWould you like to open the download page?",
                "Yes", "No",
                () =>
                {
                    try
                    {
                        Logger.LogInfo("Opening SpiderSurge download page...");
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = downloadUrl,
                            UseShellExecute = true
                        });

                        Announcer.InformationPopup(
                            "To update:\n" +
                            "1. Download the new SpiderSurge.dll\n" +
                            "2. Replace the old file in your <Game_Path>/Silk/Mods folder\n" +
                            "3. Restart SpiderHeck"
                        );
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError($"Failed to open download page: {ex.Message}");
                        Announcer.InformationPopup("Could not open the download page automatically. Please visit:\n" + ReleasesUrl);
                    }
                },
                () =>
                {
                    Logger.LogInfo("Update declined by user.");
                },
                null
            );
        }
    }
}
