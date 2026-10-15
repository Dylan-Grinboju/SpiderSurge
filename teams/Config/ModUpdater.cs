using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Logger = Silk.Logger;

namespace SpiderTeams
{
    public static class ModUpdater
    {
        private static string CurrentVersion => SpiderTeamsMod.Version;
        private static readonly HttpClient _httpClient = new HttpClient();

        public static void ScheduleCheck()
        {
            if (!ModConfig.CheckForUpdates)
            {
                Logger.LogInfo("SpiderTeams update check is turned off in the config");
                return;
            }

            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(Consts.ModUpdater.StartDelayMs);
                    await CheckForUpdatesAsync();
                }
                catch (Exception ex)
                {
                    Logger.LogError($"SpiderTeams update check failed: {ex.Message}");
                }
            });
        }

        public static async Task CheckForUpdatesAsync()
        {
            try
            {
                var latestVersion = await GetLatestVersionAsync();
                Logger.LogInfo($"SpiderTeams latest version: {latestVersion}, current version: {CurrentVersion}");

                if (IsNewerVersion(latestVersion, CurrentVersion))
                {
                    Logger.LogInfo("A new version of SpiderTeams is available!");
                    MainThread.Enqueue(() => ShowUpdatePrompt(latestVersion));
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to check for SpiderTeams updates: {ex.Message}");
            }
        }

        private static async Task<string> GetLatestVersionAsync()
        {
            using (var timeout = new CancellationTokenSource(Consts.ModUpdater.RequestTimeoutMs))
            {
                using (var response = await _httpClient.GetAsync(Consts.ModUpdater.LatestVersionUrl, timeout.Token))
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
                Logger.LogError($"Failed to compare SpiderTeams versions: {ex.Message}");
                return false;
            }
        }

        private static void ShowUpdatePrompt(string latestVersion)
        {
            if (!ModConfig.CheckForUpdates)
            {
                return;
            }

            var downloadUrl = string.Format(Consts.ModUpdater.DownloadUrl, latestVersion);
            Logger.LogInfo($"SpiderTeams download URL: {downloadUrl}");

            Announcer.TwoOptionsPopup(
                $"SpiderTeams v{latestVersion} is available!\nCurrent version: {CurrentVersion}\nWould you like to open the download page?",
                "Yes", "No",
                () =>
                {
                    try
                    {
                        Logger.LogInfo("Opening SpiderTeams download page...");
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = downloadUrl,
                            UseShellExecute = true
                        });

                        Announcer.InformationPopup(
                            "To update:\n" +
                            "1. Download the new SpiderTeams.dll\n" +
                            "2. Replace the old file in your <Game_Path>/Silk/Mods folder\n" +
                            "3. Restart SpiderHeck"
                        );
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError($"Failed to open SpiderTeams download page: {ex.Message}");
                        Announcer.InformationPopup("Could not open the download page automatically. Please visit:\n" + Consts.ModUpdater.ReleasesUrl);
                    }
                },
                () =>
                {
                    Logger.LogInfo("SpiderTeams update declined by user.");
                },
                null
            );
        }
    }
}
