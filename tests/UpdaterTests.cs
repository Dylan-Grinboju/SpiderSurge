using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using SpiderSurge.Common;

internal static class UpdaterTests
{
    private static int count;
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        Console.WriteLine("PASS " + message);
        count++;
    }
    private static void Reject(Action action, string message)
    {
        try { action(); }
        catch (InvalidDataException) { Check(true, message); return; }
        throw new Exception("Accepted: " + message);
    }
    private static int Main(string[] args)
    {
        if (args.Length == 1 && args[0] == "--wait")
        {
            // Stands in for the game: reports its creation time the way the mod does, then stays open
            Console.WriteLine(UpdatePackage.CurrentCreationTime());
            Thread.Sleep(3000);
            return 0;
        }
        string dll = Path.GetFullPath(args[0]);
        string helper = Path.GetFullPath(args[1]);
        string version = AssemblyName.GetAssemblyName(dll).Version.ToString(3);
        string hash = UpdatePackage.Hash(dll);
        using (var resource = Assembly.LoadFile(dll).GetManifestResourceStream("SpiderSurge.UpdateInstaller.exe"))
        {
            Check(resource != null, "single mod DLL contains the installer helper");
            using (var sha = System.Security.Cryptography.SHA256.Create())
                Check(BitConverter.ToString(sha.ComputeHash(resource)).Replace("-", "").ToLowerInvariant() == UpdatePackage.Hash(helper), "embedded installer matches the tested executable");
        }
        var asset = new UpdateAsset { Name = "SpiderSurge.dll", Size = new FileInfo(dll).Length, State = "uploaded", Digest = "sha256:" + hash,
            Url = "https://github.com/Dylan-Grinboju/SpiderSurge/releases/download/v" + version + "/SpiderSurge.dll" };
        var release = new UpdateRelease { Tag = "v" + version, Assets = new[] { asset } };
        Check(release.InstallableAsset() == asset, "accept stable release asset");
        release.Prerelease = true; Reject(() => release.InstallableAsset(), "reject prerelease"); release.Prerelease = false;
        string url = asset.Url; asset.Url = "https://example.org/SpiderSurge.dll"; Reject(() => release.InstallableAsset(), "reject unrelated download URL"); asset.Url = url;
        asset.Digest = null; Reject(() => release.InstallableAsset(), "reject missing checksum"); asset.Digest = "sha256:" + hash;
        release.Assets = new[] { asset, asset }; Reject(() => release.InstallableAsset(), "reject duplicate DLL assets"); release.Assets = new[] { asset };
        UpdatePackage.Verify(dll, hash, version); Check(true, "verify actual merged mod assembly");
        Reject(() => UpdatePackage.Verify(dll, new string('0', 64), version), "reject corrupt checksum");
        Reject(() => UpdatePackage.Verify(dll, hash, "99.99.99"), "reject wrong release version");
        Reject(() => UpdatePackage.Verify(helper, UpdatePackage.Hash(helper), version), "reject wrong assembly identity");

        string root = Path.Combine(Path.GetTempPath(), "spidersurge-updater-tests-" + Guid.NewGuid().ToString("N"));
        string folder = Fixture(Path.Combine(root, "locked"), dll, out string target);
        var request = new UpdateRequest { Target = target, OriginalHash = UpdatePackage.Hash(target), Hash = hash, Version = version };
        string original = request.OriginalHash;
        using (var game = new FileStream(target, FileMode.Open, FileAccess.Read, FileShare.Read))
        using (new Timer(_ => game.Dispose(), null, 1500, Timeout.Infinite))
            Program.Install(folder, target, request);
        Check(UpdatePackage.Hash(target) == hash && UpdatePackage.Hash(Path.Combine(folder, "previous.dll")) == original, "install retries while the mod file is in use");

        folder = Fixture(root, dll, out target);
        request = new UpdateRequest { Target = target, OriginalHash = original, Hash = hash, Version = version };
        request.Hash = new string('0', 64);
        Reject(() => Program.Install(folder, target, request), "corrupt staged file never replaces installed mod");
        Check(UpdatePackage.Hash(target) == original, "original retained after failed verification");
        request.Hash = hash;
        request.OriginalHash = new string('0', 64);
        Reject(() => Program.Install(folder, target, request), "manual install change blocks replacement");
        request.OriginalHash = original;
        File.Copy(helper, Path.Combine(folder, "SpiderSurge.UpdateInstaller.exe"));
        using (var parent = Process.Start(new ProcessStartInfo(Assembly.GetExecutingAssembly().Location, "--wait") { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true }))
        {
            request.ProcessId = parent.Id;
            request.ProcessStart = long.Parse(parent.StandardOutput.ReadLine());
            Check(request.ProcessStart == UpdatePackage.CreationTime(parent.Handle), "game and installer read the same creation time");
            UpdatePackage.Write(Path.Combine(folder, "request.json"), request);
            using (var worker = Process.Start(new ProcessStartInfo(Path.Combine(folder, "SpiderSurge.UpdateInstaller.exe")) { UseShellExecute = false, CreateNoWindow = true }))
            {
                Thread.Sleep(500);
                Check(UpdatePackage.Hash(target) == original && !worker.HasExited, "helper waits for the exact parent process");
                parent.WaitForExit();
                Check(worker.WaitForExit(10000) && worker.ExitCode == 0, "helper installs after normal parent exit");
            }
        }
        Check(UpdatePackage.Hash(target) == hash, "installed bytes match verified download");
        Check(UpdatePackage.Hash(Path.Combine(folder, "previous.dll")) == original, "previous mod backed up under Disabled");
        Check(File.ReadAllText(Path.Combine(folder, "result.txt")).StartsWith("Installed "), "installation outcome persisted");
        Console.WriteLine(count + " checks passed. Fixtures: " + root);
        return 0;
    }

    // A game folder with an installed mod and a verified download staged under Disabled
    private static string Fixture(string root, string dll, out string target)
    {
        string folder = Path.Combine(root, "Disabled", "SpiderSurgeUpdate");
        Directory.CreateDirectory(folder);
        target = Path.Combine(root, "SpiderSurge.dll");
        File.WriteAllText(target, "original mod fixture");
        File.Copy(dll, Path.Combine(folder, "download.pending"));
        return folder;
    }
}
