using Silk;
using Logger = Silk.Logger;
using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace StatsMod
{
    public static class ModConfig
    {
        private const string ModId = StatsMod.ModId;

        // Display settings
        public static bool ShowStatsWindow => Config.GetModConfigValue<bool>(ModId, "display.showStatsWindow", true);
        public static bool ShowPlayers => Config.GetModConfigValue<bool>(ModId, "display.showPlayers", true);
        public static bool ShowPlayTime => Config.GetModConfigValue<bool>(ModId, "display.showPlayTime", true);
        public static bool ShowEnemyDeaths => Config.GetModConfigValue<bool>(ModId, "display.showEnemyDeaths", true);

        public static int DisplayPositionX => ValidatePositionX(Config.GetModConfigValue<int>(ModId, "display.position.x", 10));
        public static int DisplayPositionY => ValidatePositionY(Config.GetModConfigValue<int>(ModId, "display.position.y", 10));

        // UI Scaling settings
        public static bool AutoScale => Config.GetModConfigValue<bool>(ModId, "display.autoScale", true);
        public static float UIScale => ValidateScale(Config.GetModConfigValue<float>(ModId, "display.uiScale", 1.0f));

        // Tracking settings
        public static bool TrackingEnabled => Config.GetModConfigValue<bool>(ModId, "tracking.enabled", true);
        public static bool SaveStatsToFile => Config.GetModConfigValue<bool>(ModId, "tracking.saveStatsToFile", true);

        // Updater settings
        public static bool CheckForUpdates => Config.GetModConfigValue<bool>(ModId, "updater.checkForUpdates", true);

        public static void SetDisplayPosition(int x, int y)
        {
            // Validate the values before setting them
            x = ValidatePositionX(x);
            y = ValidatePositionY(y);

            Config.SetModConfigValue(ModId, "display.position.x", x);
            Config.SetModConfigValue(ModId, "display.position.y", y);
        }

        public static void SetTrackingEnabled(bool value)
        {
            Config.SetModConfigValue(ModId, "tracking.enabled", value);
        }

        // Validation methods to ensure config values are within acceptable ranges
        private static int ValidatePositionX(int value)
        {
            if (value < 0)
            {
                Logger.LogWarning($"Position value {value} is negative, clamping to 0");
                return 0;
            }
            if (Screen.width <= value)
            {
                int clamped = Math.Max(0, Screen.width - 10);
                Logger.LogWarning($"Position value {value} exceeds screen width {Screen.width}, clamping to {clamped}");
                return clamped;
            }
            return value;
        }

        private static int ValidatePositionY(int value)
        {
            if (value < 0)
            {
                Logger.LogWarning($"Position value {value} is negative, clamping to 0");
                return 0;
            }
            if (Screen.height <= value)
            {
                int clamped = Math.Max(0, Screen.height - 10);
                Logger.LogWarning($"Position value {value} exceeds screen height {Screen.height}, clamping to {clamped}");
                return clamped;
            }
            return value;
        }

        private static float ValidateScale(float value)
        {
            if (value < 0.5f)
            {
                Logger.LogWarning($"UI Scale value {value} is too small, clamping to 0.5");
                return 0.5f;
            }
            if (value > 3.0f)
            {
                Logger.LogWarning($"UI Scale value {value} is too large, clamping to 3.0");
                return 3.0f;
            }
            return value;
        }
    }
}
