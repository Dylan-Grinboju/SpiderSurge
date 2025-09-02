using System;
using System.Net;
using System.Threading.Tasks;
using Silk;
using Logger = Silk.Logger;

namespace StatsMod
{
    public static class ModUpdater
    {
        private const string ModId = StatsMod.ModId;
        private const string CurrentVersion = "1.2";

        private static string LatestVersionUrl = "https://raw.githubusercontent.com/Dylan-Grinboju/spiderheck_stats_mod/main/version.txt";
        private static string DownloadUrl = "https://github.com/Dylan-Grinboju/spiderheck_stats_mod/releases/tag/v{0}";
        private static bool CheckForUpdates => Config.GetModConfigValue<bool>(ModId, "updater.checkForUpdates", true);

        public static async Task CheckForUpdatesAsync()
        {
            if (!CheckForUpdates)
            {
                Logger.LogInfo("Update checking is disabled for Stats Mod");
                return;
            }

            try
            {
                Logger.LogInfo("Checking for Stats Mod updates...");
                var latestVersion = await GetLatestVersionAsync();
                Logger.LogInfo($"Latest version: {latestVersion}, Current version: {CurrentVersion}");

                if (IsNewerVersion(latestVersion, CurrentVersion))
                {
                    Logger.LogInfo("A new version of Stats Mod is available!");
                    ShowUpdatePrompt(latestVersion);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to check for Stats Mod updates: {ex.Message}");
            }
        }

        private static async Task<string> GetLatestVersionAsync()
        {
            using (var client = new WebClient())
            {
                var response = await Task.Run(() => client.DownloadString(LatestVersionUrl));
                return response.Trim();
            }
        }

        private static bool IsNewerVersion(string latestVersion, string currentVersion)
        {
            try
            {
                var latest = new Version(latestVersion);
                var current = new Version(currentVersion);
                return latest > current;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to compare versions: {ex.Message}");
                return false;
            }
        }

        private static void ShowUpdatePrompt(string latestVersion)
        {
            var downloadUrl = string.Format(DownloadUrl, latestVersion);
            Logger.LogInfo($"Download URL: {downloadUrl}");

            Announcer.TwoOptionsPopup(
                $"Stats Mod v{latestVersion} is available!\nCurrent version: {CurrentVersion}\nWould you like to open the download page?",
                "Yes", "No",
                () =>
                {
                    try
                    {
                        Logger.LogInfo("Opening Stats Mod download page...");
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = $"https://github.com/Dylan-Grinboju/spiderheck_stats_mod/releases/tag/v{latestVersion}",
                            UseShellExecute = true
                        });

                        Announcer.InformationPopup(
                            "To update:\n" +
                            "1. Download the new StatsMod.dll\n" +
                            "2. Replace the old file in your <Game_Path>/Silk/Mods folder\n" +
                            "3. Restart SpiderHeck"
                        );
                    }
                    catch (Exception ex)
                    {
                        Logger.LogError($"Failed to open download page: {ex.Message}");
                        Announcer.InformationPopup("Could not open download page automatically. Please visit:\nhttps://github.com/Dylan-Grinboju/spiderheck_stats_mod/releases");
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
