using Silk;
using Logger = Silk.Logger;
using HarmonyLib;
using UnityEngine;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Reflection;

namespace SpiderSurge
{
    // SilkMod Attribute with the format: name, authors, mod version, silk version, and identifier
    [SilkMod("SpiderSurge", new string[] { "Dylan" }, "0.1.0", "0.7.0", "SpiderSurge_Mod", 1)]
    public class SpiderSurgeMod : SilkMod
    {
        public static SpiderSurgeMod Instance { get; private set; }
        public const string ModId = "SpiderSurge";

        // Get version from assembly at runtime
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
            Instance = this;
            Logger.LogInfo("Initializing SpiderSurge Mod...");
            // Initialize configuration with default values first
            SetupConfiguration();

            new GameObject("SurgeGameModeManager").AddComponent<SurgeGameModeManager>();
            // Create PerksManager singleton
            new GameObject("PerksManager").AddComponent<PerksManager>();
            // Create SoundManager singleton
            new GameObject("SoundManager").AddComponent<SoundManager>();
            // Initialize Tutorial UI
            TutorialUI.Initialize();

            // Initialize CheatManager
            CheatManager.Initialize();
            // Initialize SoundTester for testing sounds without playing the game
            // SoundTester.Initialize();
            // Initialize per-player control settings
            PlayerControlSettings.Initialize();
            // Check for updates asynchronously
            try
            {
                _ = Task.Run(async () =>
                {
                    await Task.Delay(15000);
                    await ModUpdater.CheckForUpdatesAsync();
                });
            }
            catch (System.Exception ex)
            {
                Logger.LogError($"Update check failed: {ex.Message}");
            }

            Harmony harmony = new Harmony("com.SpiderSurge.Mod");
            harmony.PatchAll();

            Logger.LogInfo("Harmony patches applied.");
        }

        private void SetupConfiguration()
        {
            // Define default configuration values
            var defaultConfig = new Dictionary<string, object>
            {
                { "EnableSurgeMode", true },
                // Ability indicator defaults
                { "indicator.radius", 1.5f },
                { "indicator.offset.x", 3f },
                { "indicator.offset.y", 8f },
                { "indicator.availableColor", "#00FF00" },
                { "indicator.cooldownColor", "#FF0000" },
                { "indicator.activeColor", "#87CEEB" },
                { "indicator.showOnlyWhenReady", false },
                // Upgrade activation
                { "UseDpadForUltimate", false },
                { "UnlimitedPerkChoosingTime", true },
                { "EnableStatsLogging", true },
                { "display.showTutorial", true },
            };

            // Load the configuration (this will create the YAML file if it doesn't exist)
            Config.LoadModConfig(ModId, defaultConfig);
            Logger.LogInfo("Configuration loaded");
        }

        public override void Unload()
        {
            Logger.LogInfo("Unloading SpiderSurge Mod...");

            var harmony = new Harmony("com.SpiderSurge.Mod");
            harmony.UnpatchSelf();

            var surgeInfo = GameObject.Find("SurgeGameModeManager");
            if (surgeInfo != null)
                GameObject.Destroy(surgeInfo);

            var perksInfo = GameObject.Find("PerksManager");
            if (perksInfo != null)
                GameObject.Destroy(perksInfo);

            var cheatInfo = GameObject.Find("CheatsModCheatManager");
            if (cheatInfo != null)
                GameObject.Destroy(cheatInfo);

            var soundInfo = GameObject.Find("SoundManager");
            if (soundInfo != null)
                GameObject.Destroy(soundInfo);

            var soundTesterInfo = GameObject.Find("SoundTester");
            if (soundTesterInfo != null)
                GameObject.Destroy(soundTesterInfo);

            var playerControlSettings = GameObject.Find("PlayerControlSettings");
            if (playerControlSettings != null)
                GameObject.Destroy(playerControlSettings);

            Instance = null;
        }
    }
}