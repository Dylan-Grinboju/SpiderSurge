using System;
using System.Net;
using System.Threading.Tasks;
using Silk;
using Logger = Silk.Logger;

namespace SpiderSurge
{
    public static class ModUpdater
    {
        private const string ModId = SpiderSurge.ModId;
        private static string CurrentVersion => SpiderSurge.Version;

        private const string LatestVersionUrl = "https://raw.githubusercontent.com/Dylan-Grinboju/SpiderSurge/main/version.txt";
        private const string DownloadUrl = "https://github.com/Dylan-Grinboju/SpiderSurge/releases/tag/v{0}";

        public static async Task CheckForUpdatesAsync()
        {
            try
            {
                var latestVersion = await GetLatestVersionAsync();
                Logger.LogInfo($"Latest version: {latestVersion}, Current version: {CurrentVersion}");

                if (IsNewerVersion(latestVersion, CurrentVersion))
                {
                    Logger.LogInfo("A new version of SpiderSurge is available!");
                    ShowUpdatePrompt(latestVersion);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to check for SpiderSurge updates: {ex.Message}");
            }
        }

        private static async Task<string> GetLatestVersionAsync()
        {
            using (var client = new TimeoutWebClient(5000)) // 5 second timeout limit
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
                $"SpiderSurge v{latestVersion} is available!\nCurrent version: {CurrentVersion}\nWould you like to open the download page?",
                "Yes", "No",
                () =>
                {
                    try
                    {
                        Logger.LogInfo("Opening SpiderSurge download page...");
                        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                        {
                            FileName = $"https://github.com/Dylan-Grinboju/SpiderSurge/releases/tag/v{latestVersion}",
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
                        Announcer.InformationPopup("Could not open download page automatically. Please visit:\nhttps://github.com/Dylan-Grinboju/SpiderSurge/releases");
                    }
                },
                () =>
                {
                    Logger.LogInfo("Update declined by user.");
                },
                null
            );
        }
        private class TimeoutWebClient : WebClient
        {
            private readonly int _timeout;
            public TimeoutWebClient(int timeout) => _timeout = timeout;
            protected override WebRequest GetWebRequest(Uri uri)
            {
                var request = base.GetWebRequest(uri);
                request.Timeout = _timeout;
                return request;
            }
        }
    }
}
