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
        try
        {
            UpdateRequest request;
            using (var file = File.OpenRead(Path.Combine(folder, "request.json"))) request = UpdatePackage.Read<UpdateRequest>(file);
            string target = Path.GetFullPath(request.Target);
            string expectedFolder = Path.Combine(Path.GetDirectoryName(target), "Disabled", "SpiderSurgeUpdate");
            if (!string.Equals(folder.TrimEnd(Path.DirectorySeparatorChar), expectedFolder, StringComparison.OrdinalIgnoreCase)
                || Path.GetExtension(target).ToLowerInvariant() != ".dll")
                throw new InvalidDataException("The update folder does not belong to the installed mod.");
            using (var lease = AcquireLease(folder))
            using (var mutex = new Mutex(false, "Local\\SpiderSurgeUpdate-" + UpdatePackage.Hash(Path.Combine(folder, "request.json"))))
            {
                if (!mutex.WaitOne(0)) return 1;
                try
                {
                    WaitForGame(request);
                    Install(folder, target, request);
                    File.WriteAllText(result, "Installed " + request.Version + ". Previous DLL: " + Path.Combine(folder, "previous.dll"));
                }
                finally { mutex.ReleaseMutex(); }
            }
            return 0;
        }
        catch (Exception error)
        {
            File.WriteAllText(result, "Update was not installed: " + error.Message + " Your previous mod is retained. Retry from Updates or use the release page.");
            return 1;
        }
    }

    private static FileStream AcquireLease(string folder)
    {
        for (int attempt = 0; ; attempt++)
        {
            try { return new FileStream(Path.Combine(folder, "update.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None); }
            catch (IOException) when (attempt < 100) { Thread.Sleep(100); }
        }
    }

    private static void WaitForGame(UpdateRequest request)
    {
        try
        {
            using (var game = Process.GetProcessById(request.ProcessId))
                if (game.StartTime.ToUniversalTime().Ticks == request.ProcessStart) game.WaitForExit();
        }
        catch (ArgumentException) { }
    }

    // File.Replace keeps the old DLL as a backup and never leaves a partially copied installation.
    internal static void Install(string folder, string target, UpdateRequest request)
    {
        string staged = Path.Combine(folder, "download.pending");
        UpdatePackage.Verify(staged, request.Hash, request.Version);
        for (int attempt = 0; ; attempt++)
        {
            if (!string.Equals(UpdatePackage.Hash(target), request.OriginalHash, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("The installed mod changed after the download; it was left untouched.");
            try
            {
                File.Replace(staged, target, Path.Combine(folder, "previous.dll"));
                return;
            }
            catch (IOException) when (attempt < 59) { Thread.Sleep(1000); }
        }
    }
}
