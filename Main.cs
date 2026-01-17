using Silk;
using Logger = Silk.Logger;
using HarmonyLib;
using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Reflection;

namespace SpiderSurge
{
    // SilkMod Attribute with the format: name, authors, mod version, silk version, and identifier
    [SilkMod("SpiderSurge", new string[] { "Dylan" }, "0.1.0", "0.7.0", "SpiderSurge_Mod", 1)]
    public class SpiderSurge : SilkMod
    {
        public static SpiderSurge Instance { get; private set; }
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

            new SurgeModeManager();
            // Create AbilityManager singleton
            new GameObject("AbilityManager").AddComponent<AbilityManager>();
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

            Logger.LogInfo("Applied patches:");
            foreach (var method in harmony.GetPatchedMethods())
            {
                Logger.LogInfo($"Patched: {method.DeclaringType?.Name}.{method.Name}");
            }

            Logger.LogInfo("Harmony patches applied.");
        }

        private void SetupConfiguration()
        {
            // Define default configuration values
            var defaultConfig = new Dictionary<string, object>
            {
                { "EnableSurgeMode", true },
            };

            // Load the configuration (this will create the YAML file if it doesn't exist)
            Config.LoadModConfig(ModId, defaultConfig);
            Logger.LogInfo("Configuration loaded");
        }

        public override void Unload()
        {
            Logger.LogInfo("Unloading SpiderSurge Mod...");

            Instance = null;
        }
    }
}