using Silk;
using Logger = Silk.Logger;
using HarmonyLib;
using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SpiderSurge
{
    // SilkMod Attribute with with the format: name, authors, mod version, silk version, and identifier
    [SilkMod("SpiderSurge", new string[] { "Dylan" }, "0.1", "0.6.1", "SpiderSurge", 1)]
    public class SpiderSurge : SilkMod
    {
        public static SpiderSurge Instance { get; private set; }
        public const string ModId = "SpiderSurge";

        // Called by Silk when Unity loads this mod
        public override void Initialize()
        {
            Instance = this;
            Logger.LogInfo("Initializing SpiderSurge...");

            // Initialize configuration with default values first
            SetupConfiguration();

            // Check for updates asynchronously
            _ = Task.Run(async () =>
            {
                await Task.Delay(15000);
                await ModUpdater.CheckForUpdatesAsync();
            });

            // Check if tracking is enabled before initializing mod components
            if (!ModConfig.TrackingEnabled)
            {
                Logger.LogInfo("SpiderSurge tracking is disabled in configuration. Mod components will not be initialized.");
                return;
            }


            var tracker = PlayerTracker.Instance;
            Logger.LogInfo("Player tracker initialized");
            UIManager.Initialize();
            Logger.LogInfo("UI Manager initialized");

            // Initialize AbilityManager
            GameObject abilityManagerObject = new GameObject("SpiderSurge_AbilityManager");
            abilityManagerObject.AddComponent<AbilityManager>();
            Logger.LogInfo("Ability Manager initialized");

            // Initialize SurvivalPlatformDuplicator
            SurvivalPlatformDuplicator.Initialize();
            Logger.LogInfo("Survival Platform Duplicator initialized");

            Harmony harmony = new Harmony("com.SpiderSurge");
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
                { "display", new Dictionary<string, object>
                    {
                        { "showStatsWindow", true },
                        { "showPlayers", true },
                        { "showPlayTime", true },
                        { "showEnemyDeaths", true },
                        { "autoScale", true },
                        { "uiScale", 1.0f },
                        { "position", new Dictionary<string, object>
                            {
                                { "x", 10 },
                                { "y", 10 }
                            }
                        }
                    }
                },
                { "tracking", new Dictionary<string, object>
                    {
                        { "enabled", true },
                        { "saveStatsToFile", true },
                    }
                },
                { "updater", new Dictionary<string, object>
                    {
                        { "checkForUpdates", true }
                    }
                },
            };

            // Load the configuration (this will create the YAML file if it doesn't exist)
            Config.LoadModConfig(ModId, defaultConfig);
            Logger.LogInfo("Configuration loaded");
        }

        public override void Unload()
        {
            Logger.LogInfo("Unloading SpiderSurge...");

            // Only unpatch if tracking was enabled and patches were applied
            if (ModConfig.TrackingEnabled)
            {
                Harmony.UnpatchID("com.SpiderSurge");
                Logger.LogInfo("Harmony patches removed.");
            }
            else
            {
                Logger.LogInfo("No patches to remove - tracking was disabled.");
            }

            Instance = null;
        }
    }
}