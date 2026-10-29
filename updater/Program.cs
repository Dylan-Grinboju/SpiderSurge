using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using SpiderSurge.Common;

internal static class Program
{
    private static int Main(string[] args)
    {
        string folder = AppDomain.CurrentDomain.BaseDirectory;
        string result = Path.Combine(folder, "result.txt");
        string target = null;
        try
        {
            UpdateRequest request;
            using (var file = File.OpenRead(Path.Combine(folder, "request.json"))) request = UpdatePackage.Read<UpdateRequest>(file);
            string installed = Path.GetFullPath(request.Target);
            string expectedFolder = Path.Combine(Path.GetDirectoryName(installed), "Disabled", "SpiderSurgeUpdate");
            if (!string.Equals(folder.TrimEnd(Path.DirectorySeparatorChar), expectedFolder, StringComparison.OrdinalIgnoreCase)
                || Path.GetExtension(installed).ToLowerInvariant() != ".dll")
                throw new InvalidDataException("The update folder does not belong to the installed mod.");
            target = installed;
            using (var lease = AcquireLease(folder))
            {
                // Another installer holds the lock and reports the outcome itself
                if (lease == null) return 1;
                WaitForGame(request);
                Install(folder, target, request);
                File.WriteAllText(result, "Installed " + request.Version + ". Previous DLL: " + Path.Combine(folder, "previous.dll"));
            }
            return 0;
        }
        catch (Exception error)
        {
            string kept = target == null || File.Exists(target) ? " Your previous mod is retained."
                : " SpiderSurge.dll is missing: close the game and copy " + Path.Combine(folder, "previous.dll") + " to " + target + ".";
            // If even this fails, the game's own note in result.txt still says the installer did not finish
            try { File.WriteAllText(result, "Update was not installed: " + error.Message + kept + " Retry from Updates or use the release page."); }
            catch (Exception) { }
            return 1;
        }
    }

    private static FileStream AcquireLease(string folder)
    {
        for (int attempt = 0; attempt < 100; attempt++)
        {
            try { return new FileStream(Path.Combine(folder, "update.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
            catch (IOException) { Thread.Sleep(100); }
        }
        return null;
    }

    // Once the game closes its id can belong to a new process, so the kernel's creation time must match too
    private static void WaitForGame(UpdateRequest request)
    {
        Process game;
        try { game = Process.GetProcessById(request.ProcessId); }
        catch (ArgumentException) { return; } // The game has already closed
        using (game)
        {
            try
            {
                if (UpdatePackage.CreationTime(game.Handle) == request.ProcessStart) game.WaitForExit();
            }
            catch (InvalidOperationException) { } // It closed while being checked
        }
    }

    // File.Replace keeps the old DLL as previous.dll and never leaves a partially copied file. A replace that fails after
    // moving the old DLL aside is undone before the next attempt, so the mod is never left missing.
    internal static void Install(string folder, string target, UpdateRequest request)
    {
        string staged = Path.Combine(folder, "download.pending");
        string backup = Path.Combine(folder, "previous.dll");
        UpdatePackage.Verify(staged, request.Hash, request.Version);
        for (int attempt = 0; ; attempt++)
        {
            if (!string.Equals(UpdatePackage.Hash(target), request.OriginalHash, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("The installed mod changed after the download; it was left untouched.");
            try
            {
                File.Replace(staged, target, backup);
                return;
            }
            catch (IOException)
            {
                if (!File.Exists(target) && File.Exists(backup)) File.Move(backup, target);
                if (attempt >= 59) throw;
                Thread.Sleep(1000);
            }
        }
    }
}
