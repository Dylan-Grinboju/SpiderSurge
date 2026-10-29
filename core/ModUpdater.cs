using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Logger = SpiderSurge.Common.ThreadSafeLog;

namespace SpiderSurge.Common
{
    public static class ModUpdater
    {
        public const string CheckForUpdatesKey = "general.checkForUpdates";
        public const string ReleasesUrl = "https://github.com/Dylan-Grinboju/SpiderSurge/releases";
        private const string LatestReleaseUrl = "https://api.github.com/repos/Dylan-Grinboju/SpiderSurge/releases/latest";
        private static readonly HttpClient Client = new HttpClient { Timeout = TimeSpan.FromMinutes(3) };
        private static CancellationTokenSource checkCancellation, downloadCancellation;
        private static bool watching, checking, downloading, staged;
        private static UpdateRelease release;
        private static string status = "Check for a published SpiderSurge release.";
        private static string previousResult;
        private static string InstalledPath => typeof(SpiderSurgeMod).Assembly.Location;
        private static string UpdateFolder => Path.Combine(Path.GetDirectoryName(InstalledPath), "Disabled", "SpiderSurgeUpdate");
        public static bool CheckForUpdates => Settings.GetBool(CheckForUpdatesKey);
        public static IEnumerable<KeyValuePair<string, object>> Defaults => new[] { new KeyValuePair<string, object>(CheckForUpdatesKey, true) };

        public static void ScheduleCheck()
        {
            if (!watching)
            {
                Settings.Changed += OnSettingsChanged;
                watching = true;
                string result = Path.Combine(UpdateFolder, "result.txt");
                try { if (File.Exists(result)) previousResult = File.ReadAllText(result); }
                catch (IOException error) { Logger.LogWarning("Could not read the previous update result: " + error.Message); }
            }
            if (!CheckForUpdates) { CancelCheck(); return; }
            if (checkCancellation != null) return;
            checkCancellation = new CancellationTokenSource();
            _ = CheckAsync(checkCancellation.Token, true);
        }

        public static void Shutdown()
        {
            if (watching) Settings.Changed -= OnSettingsChanged;
            watching = false;
            CancelCheck();
            downloadCancellation?.Cancel();
        }
        private static void OnSettingsChanged(string key)
        {
            if (Settings.Affects(key, CheckForUpdatesKey)) ScheduleCheck();
        }
        private static void CancelCheck()
        {
            checkCancellation?.Cancel();
            checkCancellation?.Dispose();
            checkCancellation = null;
            checking = false;
        }

        public static void BuildPage(SettingsPage page)
        {
            page.Title("SpiderSurge updates");
            page.Line("Installed: " + SpiderSurgeMod.Version);
            page.Paragraph("<noparse>" + status + "</noparse>");
            if (!string.IsNullOrEmpty(previousResult)) page.Note("<noparse>Previous update: " + previousResult + "</noparse>");
            page.Note("Updates install after you close the game. Launch it again after installation finishes. Settings and progress are kept. Everyone in an online lobby needs the same version.");
            if (!checking && !downloading && !staged)
                page.Button("Check now", () => { CancelCheck(); checkCancellation = new CancellationTokenSource(); _ = CheckAsync(checkCancellation.Token, false); });
            if (release != null)
            {
                page.Button("Release notes", () => OpenUrl(release.PageUrl));
                if (!downloading && !staged && IsNewerVersion(release.Version, SpiderSurgeMod.Version))
                {
                    try
                    {
                        release.InstallableAsset();
                        page.Button("Download and install on exit", () => StartDownload(release));
                    }
                    catch (InvalidDataException error) { page.Note(error.Message); }
                }
            }
            if (downloading) page.Button("Cancel download", () => downloadCancellation?.Cancel());
            page.Button("Open releases page", () => OpenUrl(ReleasesUrl));
        }

        private static async Task CheckAsync(CancellationToken cancellation, bool automatic)
        {
            if (checking) return;
            checking = true;
            if (!automatic) SetStatus("Checking for updates...");
            try
            {
                if (automatic) await Task.Delay(15000, cancellation);
                using (var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation))
                using (var request = new HttpRequestMessage(HttpMethod.Get, LatestReleaseUrl))
                {
                    timeout.CancelAfter(10000);
                    request.Headers.UserAgent.ParseAdd("SpiderSurge");
                    request.Headers.Accept.ParseAdd("application/vnd.github+json");
                    using (var response = await Client.SendAsync(request, timeout.Token))
                    {
                        response.EnsureSuccessStatusCode();
                        using (var stream = await response.Content.ReadAsStreamAsync())
                        {
                            var found = UpdatePackage.Read<UpdateRelease>(stream);
                            if (found.Draft || found.Prerelease || !Version.TryParse(found.Version, out _)) throw new InvalidDataException("No stable release was returned.");
                            cancellation.ThrowIfCancellationRequested();
                            MainThread.Enqueue(() =>
                            {
                                if (cancellation.IsCancellationRequested) return;
                                release = found;
                                bool newer = IsNewerVersion(found.Version, SpiderSurgeMod.Version);
                                status = newer ? "SpiderSurge " + found.Version + " is available." : "You are up to date (or running a newer development build).";
                                SettingsPage.RefreshOpen();
                                if (automatic && newer && LobbyController.inLobby)
                                    Announcer.TwoOptionsPopup("SpiderSurge " + found.Version + " is available. Review the update?", "View update", "Later",
                                        () => SettingsPage.Show(SettingsPage.MainPage, "settings", "advanced", "updates"), () => { }, null);
                            });
                        }
                    }
                }
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
            catch (Exception error) { SetStatus("Could not check for updates: " + error.Message + " You can try again."); }
            finally { if (!cancellation.IsCancellationRequested) checking = false; MainThread.Enqueue(SettingsPage.RefreshOpen); }
        }

        private static void StartDownload(UpdateRelease selected)
        {
            if (downloading || staged) return;
            downloading = true;
            downloadCancellation = new CancellationTokenSource();
            downloadCancellation.CancelAfter(TimeSpan.FromMinutes(3));
            var cancellation = downloadCancellation.Token;
            using var process = Process.GetCurrentProcess();
            var request = new UpdateRequest { Target = InstalledPath, ProcessId = process.Id, ProcessStart = process.StartTime.ToUniversalTime().Ticks, Version = selected.Version };
            _ = DownloadAsync(selected, request, cancellation);
        }

        private static async Task DownloadAsync(UpdateRelease selected, UpdateRequest install, CancellationToken cancellation)
        {
            SetStatus("Downloading SpiderSurge " + selected.Version + "...");
            try
            {
                var asset = selected.InstallableAsset();
                string folder = UpdateFolder;
                Directory.CreateDirectory(folder);
                string pending = Path.Combine(folder, "download.pending");
                using (var lease = new FileStream(Path.Combine(folder, "update.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
                {
                    install.OriginalHash = UpdatePackage.Hash(install.Target);
                    install.Hash = asset.Digest.Substring(7);
                    using (var response = await Client.GetAsync(asset.Url, HttpCompletionOption.ResponseHeadersRead, cancellation))
                    {
                        response.EnsureSuccessStatusCode();
                        using (var source = await response.Content.ReadAsStreamAsync())
                        using (var file = File.Create(pending))
                        {
                            byte[] buffer = new byte[81920];
                            long count = 0;
                            int read;
                            while ((read = await source.ReadAsync(buffer, 0, buffer.Length, cancellation)) > 0)
                            {
                                count += read;
                                if (count > asset.Size || count > UpdatePackage.MaxBytes) throw new InvalidDataException("Download exceeds the expected size.");
                                await file.WriteAsync(buffer, 0, read, cancellation);
                            }
                            if (count != asset.Size) throw new InvalidDataException("Download is incomplete.");
                        }
                    }
                    UpdatePackage.Verify(pending, install.Hash, install.Version);
                    string helper = Path.Combine(folder, "SpiderSurge.UpdateInstaller.exe");
                    using (var resource = typeof(ModUpdater).Assembly.GetManifestResourceStream("SpiderSurge.UpdateInstaller.exe"))
                    using (var output = File.Create(helper)) resource.CopyTo(output);
                    cancellation.ThrowIfCancellationRequested();
                    UpdatePackage.Write(Path.Combine(folder, "request.json"), install);
                    File.Delete(Path.Combine(folder, "result.txt"));
                    using (var worker = Process.Start(new ProcessStartInfo(helper) { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden, WorkingDirectory = folder }))
                        if (worker == null) throw new IOException("The installer could not start.");
                    staged = true;
                }
                SetStatus("Download verified. Close SpiderHeck normally to install " + selected.Version + ", then launch it again. The previous DLL will be kept under Disabled/SpiderSurgeUpdate/previous.dll.");
            }
            catch (OperationCanceledException) { SetStatus("Download cancelled. Your installed mod has not changed."); }
            catch (Exception error) { SetStatus("Update could not be prepared: " + error.Message + " Your installed mod has not changed. Retry or open the release page."); }
            finally { downloading = false; downloadCancellation?.Dispose(); downloadCancellation = null; MainThread.Enqueue(SettingsPage.RefreshOpen); }
        }

        private static void SetStatus(string value)
        {
            Logger.LogInfo("[Updater] " + value);
            MainThread.Enqueue(() => { status = value; SettingsPage.RefreshOpen(); });
        }
        internal static bool IsNewerVersion(string latest, string current)
        {
            if (!Version.TryParse(latest.Split('-', '+')[0], out var next) || !Version.TryParse(current.Split('-', '+')[0], out var installed)) return false;
            return next > installed || next == installed && current.Contains("-") && !latest.Contains("-");
        }
        private static void OpenUrl(string url)
        {
            try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); }
            catch (Exception error) { SetStatus("Could not open the release page: " + error.Message); }
        }
    }
}
