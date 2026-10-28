using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEngine;

namespace SpiderSurge.Common
{
    internal static class SupportPage
    {
        public static void Build(SettingsPage page)
        {
            page.Title("Support");
            page.Button("Open logs", () => OpenFolder(LogFolder.Root));
            page.Button("Open config folder", () => OpenFolder(Path.GetDirectoryName(Settings.FilePath)));
            page.Button("Open save folder", () => OpenFolder(ModData.Folder));
            page.Button("Copy diagnostic information", () => Run(() =>
            {
                GUIUtility.systemCopyBuffer = $"SpiderSurge: {SpiderSurgeMod.Version}\nGame: {Application.version}\nUnity: {Application.unityVersion}\nSilk: {typeof(Silk.SilkMod).Assembly.GetName().Version}\n"
                    + string.Join("\n", Parts.All.Select(part => $"{part.Title}: enabled={part.Enabled}, running={part.Running}, failed={part.Failed}, blocked={part.BlockedBy ?? "none"}"));
                Announcer.InformationPopup("Diagnostic information copied.");
            }));
            page.Button("Report a bug", () => Run(() => Application.OpenURL("https://github.com/Dylan-Grinboju/SpiderSurge/issues/new")));
            page.Note("Diagnostic information includes versions and mod-part status. Logs and player names are not copied or uploaded.");
        }
        private static void OpenFolder(string path) => Run(() =>
        {
            Directory.CreateDirectory(path);
            Process.Start(new ProcessStartInfo { FileName = Path.GetFullPath(path), UseShellExecute = true });
        });
        private static void Run(Action action)
        {
            try { action(); }
            catch (Exception ex) { Announcer.InformationPopup("Could not complete this action: " + ex.Message); }
        }
    }
}
