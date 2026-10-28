using Silk;
using SpiderSurge.Common;
using Logger = Silk.Logger;

namespace SpiderSurge
{
    // SilkMod Attribute with the format: name, authors, mod version, silk version, and identifier
    [SilkMod(ModName, new string[] { "Dylan" }, ModVersion.Value, "0.7.0", "SpiderSurge_Mod", 1)]
    public class SpiderSurgeMod : SilkMod
    {
        public const string ModName = "SpiderSurge";

        public static SpiderSurgeMod Instance { get; private set; }

        // The release number from the build's vX.Y.Z tag; a build between releases carries a dev suffix, e.g. 0.4.1-dev.12, and a build made outside GitHub Actions ends in +local
        public static string Version => ModVersion.Value;

        // Called by Silk when Unity loads this mod
        public override void Initialize()
        {
            MainThread.Claim();
            Logger.LogInfo($"Initializing SpiderSurge {Version}...");
            if (!OldMods.Claim())
            {
                return;
            }
            Instance = this;

            Settings.Define(Parts.Defaults);
            Settings.Define(ModUpdater.Defaults);
            Settings.Define(Parts.SettingDefaults);

            OldMods.Check();
            Settings.Load();
            Net.Initialize();
            Parts.StartAll();
            ModUpdater.ScheduleCheck();
        }

        // Silk adds the mod as a component once every mod is loaded; this instance runs the shared per-frame work
        private void Update()
        {
            if (Instance is null)
            {
                return;
            }
            MainThread.Drain();
            Settings.Tick();
            HistoryStore.Tick();
            CollectionSeen.Tick();
            Net.Tick();
        }

        private void OnApplicationQuit()
        {
            if (Instance is not null)
            {
                Settings.Flush();
                HistoryStore.FinishWrites();
                CollectionSeen.Flush();
            }
        }

        public override void Unload()
        {
            if (Instance is null)
            {
                return;
            }
            Logger.LogInfo("Unloading SpiderSurge...");
            ModUpdater.Shutdown();
            CollectionSeen.Flush();
            Parts.StopAll();
            HistoryStore.FinishWrites();
            Net.Shutdown();
            Settings.Flush();
            OldMods.Release();
            Instance = null;
        }
    }
}
