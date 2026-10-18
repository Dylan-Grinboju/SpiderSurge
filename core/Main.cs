using System.Reflection;
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
            MainThread.Claim();
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
            Net.Tick();
        }

        private void OnApplicationQuit()
        {
            if (Instance is not null)
            {
                Settings.Flush();
            }
        }

        public override void Unload()
        {
            if (Instance is null)
            {
                return;
            }
            Logger.LogInfo("Unloading SpiderSurge...");
            Parts.StopAll();
            Net.Shutdown();
            Settings.Flush();
            OldMods.Release();
            Instance = null;
        }
    }
}
