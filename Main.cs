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
    [SilkMod("SpiderSurge Mod", new string[] { "Dylan" }, "0.1", "0.6.1", "SpiderSurge", 1)]
    public class SpiderSurgeMod : SilkMod
    {
        public static SpiderSurgeMod Instance { get; private set; }
        public const string ModId = "SpiderSurge";

        public override void Initialize()
        {
            Instance = this;
            Logger.LogInfo("Initializing SpiderSurge Mod...");

            // Initialize configuration with default values first
            SetupConfiguration();

            // Check for updates asynchronously
            _ = Task.Run(async () =>
            {
                await Task.Delay(15000);
                await ModUpdater.CheckForUpdatesAsync();
            });

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
            Logger.LogInfo("Unloading SpiderSurge Mod...");

            Harmony.UnpatchID("com.SpiderSurge");
            Logger.LogInfo("Harmony patches removed.");

            Instance = null;
        }
    }
}