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
    // Network, hashing and file work run on worker threads; the fields below are only read and changed on the main thread
    public static class ModUpdater
    {
        public const string CheckForUpdatesKey = "general.checkForUpdates";
        public const string ReleasesUrl = "https://github.com/Dylan-Grinboju/SpiderSurge/releases";
        private const string LatestReleaseUrl = "https://api.github.com/repos/Dylan-Grinboju/SpiderSurge/releases/latest";
        private const string IdleStatus = "Check for a published SpiderSurge release.";
        private const int StartDelayMs = 15000;
        // The release workflow attaches SpiderSurge.dll a few minutes after the release is published
        private const int AssetRetryDelayMs = 5 * 60 * 1000;
        private const int AssetRetries = 3;
        private static readonly TimeSpan CheckTimeout = TimeSpan.FromSeconds(10), DownloadTimeout = TimeSpan.FromMinutes(3);
        private static readonly HttpClient Client = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
        private static CancellationTokenSource checkCancellation, downloadCancellation;
        private static bool watching, checking, downloading, staged, resultPending;
        private static int assetRetries;
        private static UpdateRelease release, notice;
        private static string status = IdleStatus;
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
                ReadPreviousResult();
            }
            if (!CheckForUpdates) { CancelCheck(); notice = null; return; }
            if (checkCancellation == null) CheckLater(StartDelayMs);
        }

        public static void Shutdown()
        {
            if (watching) Settings.Changed -= OnSettingsChanged;
            watching = false;
            CancelCheck();
            notice = null;
            downloadCancellation?.Cancel();
        }

        // The startup notice waits for the lobby so it never interrupts a match
        public static void Tick()
        {
            if (notice == null || !LobbyController.inLobby) return;
            var found = notice;
            notice = null;
            string where = InstallProblem(found) == null ? "Install it from Mods > SpiderSurge > Settings > Advanced > Updates." : "Download it from its release page.";
            Announcer.TwoOptionsPopup("SpiderSurge " + found.Version + " is available.\n" + where, "Release notes", "Later", () => OpenUrl(found.PageUrl), () => { }, null);
        }

        private static void OnSettingsChanged(string key)
        {
            if (Settings.Affects(key, CheckForUpdatesKey)) ScheduleCheck();
        }
        private static void CancelCheck()
        {
            if (checkCancellation == null) return;
            checkCancellation.Cancel();
            checkCancellation.Dispose();
            checkCancellation = null;
            if (checking) { checking = false; SetStatus(IdleStatus); }
        }
        private static CancellationTokenSource NewCheck()
        {
            CancelCheck();
            return checkCancellation = new CancellationTokenSource();
        }

        // The installer leaves its outcome for the next launch, which shows it for that session and removes it
        private static void ReadPreviousResult()
        {
            resultPending = false;
            try
            {
                string folder = UpdateFolder, path = Path.Combine(folder, "result.txt");
                if (!File.Exists(path)) return;
                FileStream lease;
                try { lease = new FileStream(Path.Combine(folder, "update.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
                catch (IOException) { resultPending = true; return; } // The last session's installer is still running
                using (lease)
                {
                    previousResult = File.ReadAllText(path);
                    File.Delete(path);
                }
            }
            catch (Exception error) { Logger.LogWarning("Could not read the previous update result: " + error.Message); }
        }

        public static void BuildPage(SettingsPage page)
        {
            if (resultPending) ReadPreviousResult();
            page.Title("SpiderSurge updates");
            page.Line("Installed: " + SpiderSurgeMod.Version);
            page.Paragraph("<noparse>" + status + "</noparse>");
            if (resultPending) page.Note("The installer from your last session is still running. Open this page again in a minute to see its result.");
            else if (!string.IsNullOrEmpty(previousResult)) page.Note("<noparse>Previous update: " + previousResult + "</noparse>");
            page.Note("Updates install after you close the game. Launch it again after installation finishes. Settings and progress are kept. Everyone in an online lobby needs the same version.");
            if (!checking && !downloading && !staged)
                page.Button("Check now", () => Check(NewCheck(), false));
            if (release != null)
            {
                page.Button("Release notes", () => OpenUrl(release.PageUrl));
                if (!downloading && !staged && IsNewerVersion(release.Version, SpiderSurgeMod.Version))
                {
                    string problem = InstallProblem(release);
                    if (problem == null) page.Button("Download and install on exit", () => StartDownload(release));
                    else page.Note(problem);
                }
            }
            if (downloading) page.Button("Cancel download", () => downloadCancellation?.Cancel());
            page.Button("Open releases page", () => OpenUrl(ReleasesUrl));
        }

        // An automatic check waits so it doesn't compete with the game's startup
        private static void CheckLater(int delayMs)
        {
            var cancel = NewCheck();
            Task.Delay(delayMs, cancel.Token).ContinueWith(_ => MainThread.Enqueue(() => { if (checkCancellation == cancel) Check(cancel, true); }),
                TaskContinuationOptions.OnlyOnRanToCompletion);
        }

        private static void Check(CancellationTokenSource cancel, bool automatic)
        {
            checking = true;
            if (!automatic) SetStatus("Checking for updates...");
            var token = cancel.Token;
            Task.Run(async () =>
            {
                UpdateRelease found = null;
                string problem = null;
                try { found = await FetchLatest(token); }
                catch (OperationCanceledException) when (!token.IsCancellationRequested) { problem = "GitHub did not answer in time."; }
                catch (Exception error) { problem = error.Message; }
                // A cancelled or replaced check has already reset the page
                MainThread.Enqueue(() => { if (checkCancellation == cancel) Checked(found, problem, automatic); });
            });
        }

        private static async Task<UpdateRelease> FetchLatest(CancellationToken cancellation)
        {
            using (var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellation))
            using (var request = new HttpRequestMessage(HttpMethod.Get, LatestReleaseUrl))
            {
                limit.CancelAfter(CheckTimeout);
                request.Headers.UserAgent.ParseAdd("SpiderSurge");
                request.Headers.Accept.ParseAdd("application/vnd.github+json");
                using (var response = await Client.SendAsync(request, limit.Token))
                {
                    response.EnsureSuccessStatusCode();
                    using (var stream = await response.Content.ReadAsStreamAsync())
                    {
                        var found = UpdatePackage.Read<UpdateRelease>(stream);
                        if (found.Draft || found.Prerelease || !Version.TryParse(found.Version, out _)) throw new InvalidDataException("No stable release was returned.");
                        return found;
                    }
                }
            }
        }

        private static void Checked(UpdateRelease found, string problem, bool automatic)
        {
            checking = false;
            if (problem != null) { SetStatus("Could not check for updates: " + problem + " You can try again."); return; }
            release = found;
            bool newer = IsNewerVersion(found.Version, SpiderSurgeMod.Version);
            SetStatus(newer ? "SpiderSurge " + found.Version + " is available." : "You are up to date (or running a newer development build).");
            if (!automatic || !newer) return;
            // Announce the release once its DLL is attached, or anyway once the retries run out
            if (InstallProblem(found) != null && assetRetries++ < AssetRetries) CheckLater(AssetRetryDelayMs);
            else notice = found;
        }

        private static string InstallProblem(UpdateRelease candidate)
        {
            try { candidate.InstallableAsset(); return null; }
            catch (InvalidDataException error) { return error.Message; }
        }

        private static void StartDownload(UpdateRelease selected)
        {
            if (downloading || staged) return;
            downloading = true;
            var cancel = downloadCancellation = new CancellationTokenSource();
            SetStatus("Downloading SpiderSurge " + selected.Version + "...");
            Task.Run(async () =>
            {
                bool done = false;
                string outcome;
                try
                {
                    await Download(selected, cancel.Token);
                    done = true;
                    outcome = "Download verified. Close SpiderHeck normally to install " + selected.Version + ", then launch it again. The previous DLL will be kept under Disabled/SpiderSurgeUpdate/previous.dll.";
                }
                catch (OperationCanceledException) when (cancel.IsCancellationRequested) { outcome = "Download cancelled. Your installed mod has not changed."; }
                catch (OperationCanceledException) { outcome = "The download took longer than " + DownloadTimeout.TotalMinutes + " minutes and was stopped. Your installed mod has not changed. Retry or open the release page."; }
                catch (Exception error) { outcome = "Update could not be prepared: " + error.Message + " Your installed mod has not changed. Retry or open the release page."; }
                MainThread.Enqueue(() =>
                {
                    downloading = false;
                    staged = done;
                    downloadCancellation = null;
                    cancel.Dispose();
                    SetStatus(outcome);
                });
            });
        }

        private static async Task Download(UpdateRelease selected, CancellationToken cancellation)
        {
            var asset = selected.InstallableAsset();
            string folder = UpdateFolder, pending = Path.Combine(folder, "download.pending"), result = Path.Combine(folder, "result.txt");
            var install = new UpdateRequest { Target = InstalledPath, Hash = asset.Digest.Substring(7), Version = selected.Version, ProcessStart = UpdatePackage.CurrentCreationTime() };
            using (var process = Process.GetCurrentProcess()) install.ProcessId = process.Id;
            Directory.CreateDirectory(folder);
            using (var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellation))
            using (var lease = new FileStream(Path.Combine(folder, "update.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None))
            {
                limit.CancelAfter(DownloadTimeout);
                try
                {
                    install.OriginalHash = UpdatePackage.Hash(install.Target);
                    using (var response = await Client.GetAsync(asset.Url, HttpCompletionOption.ResponseHeadersRead, limit.Token))
                    {
                        response.EnsureSuccessStatusCode();
                        using (var source = await response.Content.ReadAsStreamAsync())
                        using (var file = File.Create(pending))
                        {
                            byte[] buffer = new byte[81920];
                            long count = 0;
                            int read;
                            while ((read = await source.ReadAsync(buffer, 0, buffer.Length, limit.Token)) > 0)
                            {
                                count += read;
                                if (count > asset.Size) throw new InvalidDataException("Download exceeds the expected size.");
                                await file.WriteAsync(buffer, 0, read, limit.Token);
                            }
                            if (count != asset.Size) throw new InvalidDataException("Download is incomplete.");
                        }
                    }
                    UpdatePackage.Verify(pending, install.Hash, install.Version);
                    string helper = Path.Combine(folder, "SpiderSurge.UpdateInstaller.exe");
                    using (var resource = typeof(ModUpdater).Assembly.GetManifestResourceStream("SpiderSurge.UpdateInstaller.exe"))
                    using (var output = File.Create(helper)) resource.CopyTo(output);
                    limit.Token.ThrowIfCancellationRequested();
                    UpdatePackage.Write(Path.Combine(folder, "request.json"), install);
                    // The installer replaces this with its outcome; if it is stopped before then, the next launch still says so
                    File.WriteAllText(result, "Downloaded " + install.Version + ", but the installer did not finish. If Installed still shows the old version, download the update again or use the release page.");
                    using (Process.Start(new ProcessStartInfo(helper) { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = folder })) { }
                }
                catch
                {
                    TryDelete(pending);
                    TryDelete(result);
                    throw;
                }
            }
        }

        private static void TryDelete(string path)
        {
            try { File.Delete(path); }
            catch (Exception error) { Logger.LogWarning("Could not delete " + path + ": " + error.Message); }
        }

        private static void SetStatus(string value)
        {
            Logger.LogInfo("[Updater] " + value);
            status = value;
            if (SettingsPage.IsShowing("updates")) SettingsPage.RefreshOpen();
        }
        internal static bool IsNewerVersion(string latest, string current)
        {
            if (!Version.TryParse(latest.Split('-', '+')[0], out var next) || !Version.TryParse(current.Split('-', '+')[0], out var installed)) return false;
            return next > installed || next == installed && current.Contains("-") && !latest.Contains("-");
        }
        private static void OpenUrl(string url)
        {
            try { UnityEngine.Application.OpenURL(url); }
            catch (Exception error) { Announcer.InformationPopup("Could not open " + url + ": " + error.Message); }
        }
    }
}
