using System;
using System.IO;
using System.Threading;
using SpiderSurge.Common;

internal static class Program
{
    // The game keeps the update lock until it exits, or releases it early with nothing staged when the player cancels the install
    private static int Main(string[] args)
    {
        string folder = AppDomain.CurrentDomain.BaseDirectory;
        string staged = Path.Combine(folder, "download.pending");
        string result = Path.Combine(folder, "result.txt");
        using (WaitForLock(folder))
        {
            if (!File.Exists(staged)) return 0;
            UpdateRequest request;
            string target = null;
            try
            {
                using (var file = File.OpenRead(Path.Combine(folder, "request.json"))) request = UpdatePackage.Read<UpdateRequest>(file);
                string installed = Path.GetFullPath(request.Target);
                string expectedFolder = Path.Combine(Path.GetDirectoryName(installed), "Disabled", "SpiderSurgeUpdate");
                if (!string.Equals(folder.TrimEnd(Path.DirectorySeparatorChar), expectedFolder, StringComparison.OrdinalIgnoreCase)
                    || Path.GetExtension(installed).ToLowerInvariant() != ".dll")
                    throw new InvalidDataException("The update folder does not belong to the installed mod.");
                target = installed;
                Install(folder, target, request);
            }
            catch (Exception error)
            {
                string kept = target == null || File.Exists(target) ? " Your previous mod is retained."
                    : " SpiderSurge.dll is missing: close the game and copy " + Path.Combine(folder, "previous.dll") + " to " + target + ".";
                Report(result, "Update was not installed: " + error.Message + kept + " Retry from Updates or use the release page.");
                return 1;
            }
            Report(result, "Installed " + request.Version + ". Previous DLL: " + Path.Combine(folder, "previous.dll"));
            return 0;
        }
    }

    private static FileStream WaitForLock(string folder)
    {
        while (true)
        {
            var lease = UpdatePackage.TryLock(folder);
            if (lease != null) return lease;
            Thread.Sleep(500);
        }
    }

    // If even this fails, the game's own note in result.txt still says the installer did not finish
    private static void Report(string path, string text)
    {
        try { File.WriteAllText(path, text); }
        catch (Exception) { }
    }

    // File.Replace keeps the old DLL as previous.dll and never leaves a partial file; a replace that fails midway is undone before retrying
    internal static void Install(string folder, string target, UpdateRequest request)
    {
        string staged = Path.Combine(folder, "download.pending");
        string backup = Path.Combine(folder, "previous.dll");
        for (int attempt = 0; ; attempt++)
        {
            try
            {
                UpdatePackage.Verify(staged, request.Hash, request.Version);
                if (!string.Equals(UpdatePackage.Hash(target), request.OriginalHash, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("The installed mod changed after the download; it was left untouched.");
                File.Replace(staged, target, backup);
                return;
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException)
            {
                if (!File.Exists(target) && File.Exists(backup)) File.Move(backup, target);
                if (attempt >= 59) throw;
                Thread.Sleep(1000);
            }
        }
    }
}
