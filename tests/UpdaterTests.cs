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
    private static void Refuse(UpdateRelease release, string message) => Check(!release.TryGetInstallableAsset(out var asset, out string problem) && asset == null && problem != null, message);

    private static int Main(string[] args)
    {
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
        Check(release.TryGetInstallableAsset(out var accepted, out _) && accepted == asset, "accept stable release asset");
        release.Prerelease = true; Refuse(release, "reject prerelease"); release.Prerelease = false;
        string url = asset.Url; asset.Url = "https://example.org/SpiderSurge.dll"; Refuse(release, "reject unrelated download URL"); asset.Url = url;
        asset.Digest = null; Refuse(release, "reject missing checksum"); asset.Digest = "sha256:" + hash;
        release.Assets = new[] { asset, asset }; Refuse(release, "reject duplicate DLL assets"); release.Assets = new[] { asset };
        UpdatePackage.Verify(dll, hash, version); Check(true, "verify actual merged mod assembly");
        Reject(() => UpdatePackage.Verify(dll, new string('0', 64), version), "reject corrupt checksum");
        Reject(() => UpdatePackage.Verify(dll, hash, "99.99.99"), "reject wrong release version");
        Reject(() => UpdatePackage.Verify(helper, UpdatePackage.Hash(helper), version), "reject wrong assembly identity");

        string root = Path.Combine(Path.GetTempPath(), "spidersurge-updater-tests-" + Guid.NewGuid().ToString("N"));
        try
        {
            string folder = Fixture(Path.Combine(root, "in-use"), dll, out string target);
            string original = UpdatePackage.Hash(target);
            using (var game = new FileStream(target, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (new Timer(_ => game.Dispose(), null, 1500, Timeout.Infinite))
                Program.Install(folder, target, Request(target, original, hash, version));
            Check(UpdatePackage.Hash(target) == hash && UpdatePackage.Hash(Path.Combine(folder, "previous.dll")) == original, "install retries while the mod file is in use");

            folder = Fixture(Path.Combine(root, "unreadable"), dll, out target);
            using (var scanner = new FileStream(target, FileMode.Open, FileAccess.Read, FileShare.None))
            using (new Timer(_ => scanner.Dispose(), null, 1500, Timeout.Infinite))
                Program.Install(folder, target, Request(target, original, hash, version));
            Check(UpdatePackage.Hash(target) == hash, "install retries while the mod file cannot be read");

            folder = Fixture(Path.Combine(root, "refused"), dll, out target);
            Reject(() => Program.Install(folder, target, Request(target, original, new string('0', 64), version)), "corrupt staged file never replaces installed mod");
            Check(UpdatePackage.Hash(target) == original, "original retained after failed verification");
            Reject(() => Program.Install(folder, target, Request(target, new string('0', 64), hash, version)), "manual install change blocks replacement");

            folder = Fixture(Path.Combine(root, "exit"), dll, out target);
            using (var game = Stage(folder, helper, Request(target, original, hash, version)))
            using (var worker = StartInstaller(folder))
            {
                Thread.Sleep(1500);
                Check(UpdatePackage.Hash(target) == original && !worker.HasExited, "installer waits while the game holds the update lock");
                game.Dispose();
                Check(worker.WaitForExit(10000) && worker.ExitCode == 0, "installer installs once the game lets go of the lock");
            }
            Check(UpdatePackage.Hash(target) == hash, "installed bytes match verified download");
            Check(UpdatePackage.Hash(Path.Combine(folder, "previous.dll")) == original, "previous mod backed up under Disabled");
            Check(File.ReadAllText(Path.Combine(folder, "result.txt")).StartsWith("Installed "), "installation outcome persisted");

            folder = Fixture(Path.Combine(root, "cancel"), dll, out target);
            using (var game = Stage(folder, helper, Request(target, original, hash, version)))
            using (var worker = StartInstaller(folder))
            {
                Thread.Sleep(500);
                File.Delete(Path.Combine(folder, "download.pending"));
                game.Dispose();
                Check(worker.WaitForExit(10000) && worker.ExitCode == 0, "installer exits after a cancelled install");
            }
            Check(UpdatePackage.Hash(target) == original && !File.Exists(Path.Combine(folder, "result.txt")), "cancelled install keeps the mod and reports nothing");

            folder = Fixture(Path.Combine(root, "foreign"), dll, out target);
            string elsewhere = Path.Combine(root, "SpiderSurge.dll");
            File.WriteAllText(elsewhere, "another install");
            Stage(folder, helper, Request(elsewhere, UpdatePackage.Hash(elsewhere), hash, version)).Dispose();
            using (var worker = StartInstaller(folder))
                Check(worker.WaitForExit(10000) && worker.ExitCode == 1, "installer refuses a mod outside its update folder");
            Check(File.ReadAllText(elsewhere) == "another install" && UpdatePackage.Hash(target) == original
                && File.ReadAllText(Path.Combine(folder, "result.txt")).Contains("does not belong"), "refused request changes nothing and says why");
        }
        finally
        {
            try { Directory.Delete(root, true); }
            catch (Exception error) { Console.WriteLine("Could not remove " + root + ": " + error.Message); }
        }
        Console.WriteLine(count + " checks passed.");
        return 0;
    }

    private static UpdateRequest Request(string target, string originalHash, string hash, string version) =>
        new UpdateRequest { Target = target, OriginalHash = originalHash, Hash = hash, Version = version };

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

    // Leaves the folder as the game does after a download, holding the update lock as the running game would
    private static FileStream Stage(string folder, string helper, UpdateRequest request)
    {
        File.Copy(helper, Path.Combine(folder, "SpiderSurge.UpdateInstaller.exe"));
        UpdatePackage.Write(Path.Combine(folder, "request.json"), request);
        return UpdatePackage.TryLock(folder);
    }

    private static Process StartInstaller(string folder) =>
        Process.Start(new ProcessStartInfo(Path.Combine(folder, "SpiderSurge.UpdateInstaller.exe")) { UseShellExecute = false, CreateNoWindow = true });
}
