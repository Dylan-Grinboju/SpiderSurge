using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Logger = SpiderSurge.Common.ThreadSafeLog;

namespace SpiderSurge.Common
{
    // Network, hashing and file work run on worker threads; the fields below are only read and changed on the main thread
    public static partial class ModUpdater
    {
        public const string CheckForUpdatesKey = "general.checkForUpdates";
        public const string ReleasesUrl = "https://github.com/Dylan-Grinboju/SpiderSurge/releases";
        private const string LatestReleaseUrl = "https://api.github.com/repos/Dylan-Grinboju/SpiderSurge/releases/latest";
        private const string IdleStatus = "Check for a published SpiderSurge release.";
        private const int StartDelayMs = 15000;
        private static readonly TimeSpan CheckTimeout = TimeSpan.FromSeconds(10), DownloadTimeout = TimeSpan.FromMinutes(3);
        private static readonly HttpClient Client = new HttpClient { Timeout = Timeout.InfiniteTimeSpan };
        private static CancellationTokenSource checkCancellation, downloadCancellation;
        private static bool watching, checking, automaticCheck, downloading, staged, resultPending;
        private static FileStream installLease;
        private static UpdateRelease release, notice;
        private static string status = IdleStatus;
        private static string previousResult;
        private static string InstalledPath => typeof(SpiderSurgeMod).Assembly.Location;
        private static string UpdateFolder => Path.Combine(Path.GetDirectoryName(InstalledPath), "Disabled", "SpiderSurgeUpdate");
        public static bool CheckForUpdates => Settings.GetBool(CheckForUpdatesKey);
        public static IEnumerable<KeyValuePair<string, object>> Defaults => new[] { new KeyValuePair<string, object>(CheckForUpdatesKey, true) };

        // Turning the startup check off only stops that check, never one the player started
        public static void ScheduleCheck()
        {
            if (!watching)
            {
                Settings.Changed += OnSettingsChanged;
                watching = true;
                ReadPreviousResult();
            }
            if (!CheckForUpdates) { if (automaticCheck) CancelCheck(); notice = null; return; }
            if (checkCancellation == null && !downloading && !staged) CheckLater(StartDelayMs);
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
            if (!downloading && !staged) ShowOffer(found);
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
            checking = false;
        }
        private static CancellationTokenSource NewCheck(bool automatic)
        {
            CancelCheck();
            automaticCheck = automatic;
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
                var lease = UpdatePackage.TryLock(folder);
                if (lease == null) { resultPending = true; return; }
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
            // Download, Cancel download and Cancel install share one row, so a controller stays on it when the button changes
            if (downloading) page.Button("Cancel download", () => downloadCancellation?.Cancel());
            else if (staged) page.Button("Cancel install", CancelInstall);
            else if (release != null && !resultPending && IsNewerVersion(release.Version, SpiderSurgeMod.Version))
            {
                if (release.TryGetInstallableAsset(out _, out string problem)) page.Button("Download and install on exit", () => StartDownload(release));
                else page.Note(problem);
            }
            if (!checking && !downloading && !staged)
                page.Button("Check now", () => Check(NewCheck(false), false));
            if (release != null) page.Button("Release notes", () => OpenUrl(release.PageUrl));
            page.Button("Open releases page", () => OpenUrl(ReleasesUrl));
        }

        // An automatic check waits so it doesn't compete with the game's startup
        private static void CheckLater(int delayMs)
        {
            var cancel = NewCheck(true);
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
                try
                {
                    using (var response = await Cancellable(Client.SendAsync(request, limit.Token), limit.Token))
                    {
                        response.EnsureSuccessStatusCode();
                        using (var stream = await response.Content.ReadAsStreamAsync())
                        {
                            var found = UpdatePackage.Read<UpdateRelease>(stream);
                            if (!found.IsStable) throw new InvalidDataException("No stable release was returned.");
                            return found;
                        }
                    }
                }
                catch (Exception error) when (limit.IsCancellationRequested && !(error is OperationCanceledException))
                {
                    throw new OperationCanceledException(error.Message, error, limit.Token);
                }
            }
        }

        // A download the player already started keeps its status, and the lobby notice is only for a release they haven't acted on
        private static void Checked(UpdateRelease found, string problem, bool automatic)
        {
            checking = false;
            if (downloading || staged) return;
            if (problem != null) { SetStatus("Could not check for updates: " + problem + " You can try again."); return; }
            release = found;
            bool newer = IsNewerVersion(found.Version, SpiderSurgeMod.Version);
            SetStatus(newer ? "SpiderSurge " + found.Version + " is available." : "You are up to date (or running a newer development build).");
            if (automatic && newer) notice = found;
        }

        private static void StartDownload(UpdateRelease selected)
        {
            if (downloading || staged || resultPending) return;
            downloading = true;
            downloadProblem = null;
            var cancel = downloadCancellation = new CancellationTokenSource();
            SetStatus("Downloading SpiderSurge " + selected.Version + "...");
            Task.Run(async () =>
            {
                FileStream lease = null;
                string outcome, problem = null;
                try
                {
                    lease = await Download(selected, cancel.Token);
                    outcome = "Download verified. Close SpiderHeck to install " + selected.Version + ", then launch it again. The previous DLL will be kept under Disabled/SpiderSurgeUpdate/previous.dll.";
                }
                catch (OperationCanceledException) when (cancel.IsCancellationRequested) { outcome = "Download cancelled. Your installed mod has not changed."; }
                catch (OperationCanceledException)
                {
                    problem = "The download took longer than " + DownloadTimeout.TotalMinutes + " minutes and was stopped.";
                    outcome = problem + " Your installed mod has not changed. Retry or open the release page.";
                }
                catch (Exception error)
                {
                    problem = error.Message;
                    outcome = "Update could not be prepared: " + error.Message + " Your installed mod has not changed. Retry or open the release page.";
                }
                MainThread.Enqueue(() =>
                {
                    downloading = false;
                    staged = lease != null;
                    installLease = lease;
                    downloadCancellation = null;
                    downloadProblem = problem;
                    cancel.Dispose();
                    SetStatus(outcome);
                });
            });
        }

        // Returns the update lock still held: the installer waits for it, so it installs once the game exits, even after a crash
        private static async Task<FileStream> Download(UpdateRelease selected, CancellationToken cancellation)
        {
            if (!selected.TryGetInstallableAsset(out var asset, out string problem)) throw new InvalidDataException(problem);
            string folder = UpdateFolder, pending = Path.Combine(folder, "download.pending"), result = Path.Combine(folder, "result.txt");
            var install = new UpdateRequest { Target = InstalledPath, Hash = asset.Digest.Substring(7), Version = selected.Version };
            Directory.CreateDirectory(folder);
            var lease = UpdatePackage.TryLock(folder) ?? throw new IOException("Another SpiderSurge installer is still running. Try again in a minute.");
            using (var limit = CancellationTokenSource.CreateLinkedTokenSource(cancellation))
            {
                limit.CancelAfter(DownloadTimeout);
                try
                {
                    install.OriginalHash = UpdatePackage.Hash(install.Target);
                    using (var response = await Cancellable(Client.GetAsync(asset.Url, HttpCompletionOption.ResponseHeadersRead, limit.Token), limit.Token))
                    {
                        response.EnsureSuccessStatusCode();
                        using (var source = await Cancellable(response.Content.ReadAsStreamAsync(), limit.Token))
                        using (var file = File.Create(pending))
                        {
                            byte[] buffer = new byte[81920];
                            long count = 0;
                            int read;
                            while ((read = await Cancellable(source.ReadAsync(buffer, 0, buffer.Length, limit.Token), limit.Token)) > 0)
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
                    WriteInstaller(helper);
                    limit.Token.ThrowIfCancellationRequested();
                    UpdatePackage.Write(Path.Combine(folder, "request.json"), install);
                    // The installer replaces this with its outcome; if it is stopped before then, the next launch still says so
                    File.WriteAllText(result, "Downloaded " + install.Version + ", but the installer did not finish. If Installed still shows the old version, download the update again or use the release page.");
                    using (Process.Start(new ProcessStartInfo(helper) { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = folder })) { }
                    return lease;
                }
                catch (Exception error)
                {
                    TryDelete(pending);
                    TryDelete(result);
                    lease.Dispose();
                    // A request aborted by the cancellation fails with a network error instead
                    if (limit.IsCancellationRequested && !(error is OperationCanceledException)) throw new OperationCanceledException(error.Message, error, limit.Token);
                    throw;
                }
            }
        }

        // An installer left waiting by a cancelled install may still be running from this file, and it is the same program
        private static void WriteInstaller(string path)
        {
            byte[] bytes;
            using (var resource = typeof(ModUpdater).Assembly.GetManifestResourceStream("SpiderSurge.UpdateInstaller.exe"))
            using (var copy = new MemoryStream())
            {
                resource.CopyTo(copy);
                bytes = copy.ToArray();
            }
            if (!File.Exists(path) || !File.ReadAllBytes(path).SequenceEqual(bytes)) File.WriteAllBytes(path, bytes);
        }

        // With nothing staged, the installer exits without installing once it gets the lock
        private static void CancelInstall()
        {
            if (!staged) return;
            string folder = UpdateFolder;
            try { File.Delete(Path.Combine(folder, "download.pending")); }
            catch (Exception error) { SetStatus("The install could not be cancelled: " + error.Message); return; }
            TryDelete(Path.Combine(folder, "request.json"));
            TryDelete(Path.Combine(folder, "result.txt"));
            installLease.Dispose();
            installLease = null;
            staged = false;
            SetStatus("Install cancelled. Your installed mod has not changed.");
        }

        // Mono's network reads don't stop for a cancellation token, so a stalled one is abandoned and cleaned up whenever it ends
        private static async Task<T> Cancellable<T>(Task<T> work, CancellationToken token)
        {
            var cancelled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            using (token.Register(() => cancelled.TrySetResult(true)))
                if (await Task.WhenAny(work, cancelled.Task) == work) return await work;
            _ = work.ContinueWith(late =>
            {
                if (late.IsFaulted) _ = late.Exception;
                else if (!late.IsCanceled) (late.Result as IDisposable)?.Dispose();
            });
            throw new OperationCanceledException(token);
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
            RefreshPopup();
        }
        private static bool IsNewerVersion(string latest, string current)
        {
            if (!Version.TryParse(latest.Split('-', '+')[0], out var next) || !Version.TryParse(current.Split('-', '+')[0], out var installed)) return false;
            return next > installed || next == installed && current.Contains("-") && !latest.Contains("-");
        }
        // Application.OpenURL fails silently, so the address is shown when the browser can't be opened
        private static void OpenUrl(string url)
        {
            try { using (Process.Start(new ProcessStartInfo(url) { UseShellExecute = true })) { } }
            catch (Exception error) { Announcer.InformationPopup("Could not open your browser (" + error.Message + "). Visit:\n" + url); }
        }
    }
}
