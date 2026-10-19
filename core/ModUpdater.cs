using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Logger = SpiderSurge.Common.ThreadSafeLog;

namespace SpiderSurge.Common
{
    public static class ModUpdater
    {
        public const string CheckForUpdatesKey = "general.checkForUpdates";
        public const string ReleasesUrl = "https://github.com/Dylan-Grinboju/SpiderSurge/releases";

        // Only a published release counts, so merging to main announces nothing; version.txt on main stays for 0.4.0 and older, which read it
        private const string LatestReleaseUrl = "https://api.github.com/repos/Dylan-Grinboju/SpiderSurge/releases/latest";
        private static readonly Regex TagName = new Regex("\"tag_name\"\\s*:\\s*\"v?([^\"]+)\"");
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
            using (var request = new HttpRequestMessage(HttpMethod.Get, LatestReleaseUrl))
            {
                request.Headers.UserAgent.ParseAdd("SpiderSurge");
                request.Headers.Accept.ParseAdd("application/vnd.github+json");
                using (var response = await _httpClient.SendAsync(request, timeout.Token))
                {
                    response.EnsureSuccessStatusCode();
                    var match = TagName.Match(await response.Content.ReadAsStringAsync());
                    if (!match.Success) throw new FormatException("the latest release has no tag name");
                    return match.Groups[1].Value.Trim();
                }
            }
        }

        // A dev build (0.4.1-dev.12) counts as older than the release with the same number
        private static bool IsNewerVersion(string latestVersion, string currentVersion)
        {
            try
            {
                var latest = new Version(ReleaseNumber(latestVersion));
                var current = new Version(ReleaseNumber(currentVersion));
                return latest > current || (latest == current && IsDevBuild(currentVersion) && !IsDevBuild(latestVersion));
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to compare versions: {ex.Message}");
                return false;
            }
        }

        private static string ReleaseNumber(string version) => version.Split('-', '+')[0];

        private static bool IsDevBuild(string version) => version.Contains("-");

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
