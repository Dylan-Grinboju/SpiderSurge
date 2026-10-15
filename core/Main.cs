using System.Reflection;
using Silk;
using SpiderSurge.Common;
using Logger = Silk.Logger;

namespace SpiderSurge
{
    // SilkMod Attribute with the format: name, authors, mod version, silk version, and identifier
    [SilkMod(ModName, new string[] { "Dylan" }, "1.0.0", "0.7.0", "SpiderSurge_Mod", 1)]
    public class SpiderSurgeMod : SilkMod
    {
        public const string ModName = "SpiderSurge";

        public static SpiderSurgeMod Instance { get; private set; }

        private static string _version;
        public static string Version
        {
            get
            {
                if (_version == null)
                {
                    var version = Assembly.GetExecutingAssembly().GetName().Version;
                    _version = $"{version.Major}.{version.Minor}.{version.Build}";
                }
                return _version;
            }
        }

        // Called by Silk when Unity loads this mod
        public override void Initialize()
        {
            Logger.LogInfo($"Initializing SpiderSurge {Version}...");
            if (!OldMods.Claim())
            {
                return;
            }
            Instance = this;

            Settings.Define(Parts.Defaults);
            Settings.Define(ModUpdater.Defaults);
            Settings.Define(ModConfig.Defaults);
            Settings.Define(StatsMod.ModConfig.Defaults);
            Settings.Define(SpiderTeams.ModConfig.Defaults);

            OldMods.Check();
            Settings.Load();
            Parts.StartAll();
            ModUpdater.ScheduleCheck();
        }

        // Silk adds the mod as a component once every mod is loaded; this instance runs the shared per-frame work
        private void Update()
        {
            if (Instance == null)
            {
                return;
            }
            MainThread.Drain();
            Settings.Tick();
        }

        private void OnApplicationQuit()
        {
            if (Instance != null)
            {
                Settings.Flush();
            }
        }

        public override void Unload()
        {
            if (Instance == null)
            {
                return;
            }
            Logger.LogInfo("Unloading SpiderSurge...");
            Parts.StopAll();
            Settings.Flush();
            OldMods.Release();
            Instance = null;
        }
    }
}
