using Silk;
using Logger = Silk.Logger;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace SpiderSurge
{
    public static class ModConfig
    {
        private const string ModId = SpiderSurgeMod.ModId;

        // Surge mode settings
        public static bool enableSurgeMode => Config.GetModConfigValue(ModId, "EnableSurgeMode", true);

        // Ability indicator configuration
        public static float IndicatorRadius => ValidateIndicatorRadius(Config.GetModConfigValue(ModId, "indicator.radius", 1.5f));
        public static float IndicatorOffsetX => Config.GetModConfigValue(ModId, "indicator.offset.x", 3f);
        public static float IndicatorOffsetY => Config.GetModConfigValue(ModId, "indicator.offset.y", 8f);
        public static Color IndicatorAvailableColor => ParseColor(Config.GetModConfigValue(ModId, "indicator.availableColor", "#00FF00"), Color.green);
        public static Color IndicatorCooldownColor => ParseColor(Config.GetModConfigValue(ModId, "indicator.cooldownColor", "#FF0000"), Color.red);
        public static Color IndicatorActiveColor => ParseColor(Config.GetModConfigValue(ModId, "indicator.activeColor", "#87CEEB"), Color.blue);
        public static bool IndicatorShowOnlyWhenReady => Config.GetModConfigValue(ModId, "indicator.showOnlyWhenReady", false);

        // Ultimate activation configuration
        public static bool UltimateUseDpadActivation => Config.GetModConfigValue(ModId, "UseDpadForUltimate", false);

        public static bool UnlimitedPerkChoosingTime => Config.GetModConfigValue(ModId, "UnlimitedPerkChoosingTime", false);

        public static bool EnableStatsLogging => Config.GetModConfigValue(ModId, "EnableStatsLogging", true);
        public static bool TelemetryEnabled => Config.GetModConfigValue(ModId, "TelemetryEnabled", false);
        public static bool ShowTutorial => Config.GetModConfigValue(ModId, "display.showTutorial", true);
        public static bool ShowTelemetryConsentPrompt => Config.GetModConfigValue(ModId, "display.showTelemetryConsentPrompt", true);

        public static void SetEnableSurgeMode(bool value)
        {
            Config.SetModConfigValue(ModId, "EnableSurgeMode", value);
        }

        public static void SetShowTutorial(bool value)
        {
            Config.SetModConfigValue(ModId, "display.showTutorial", value);
        }

        public static void SetTelemetryEnabled(bool value)
        {
            Config.SetModConfigValue(ModId, "TelemetryEnabled", value);
        }

        public static void SetShowTelemetryConsentPrompt(bool value)
        {
            Config.SetModConfigValue(ModId, "display.showTelemetryConsentPrompt", value);
        }

        // Silk reads "a.b" keys as nested sections, so the flat "a.b" entries older versions wrote were never read
        public static void MigrateFlatKeys(Dictionary<string, object> defaults)
        {
            try
            {
                var configs = AccessTools.Field(typeof(Config), "_modConfigs")?.GetValue(null) as Dictionary<string, Dictionary<string, object>>;
                if (configs == null || !configs.TryGetValue(ModId, out var config)) return;

                List<string> flatKeys = config.Keys.Where(key => key.Contains(".")).ToList();
                if (flatKeys.Count == 0) return;

                foreach (string key in flatKeys)
                {
                    object flatValue = config[key];
                    config.Remove(key);
                    if (!TryGetPath(defaults, key, out object defaultValue) || !TryGetPath(config, key, out object current)) continue;

                    // A value the player already set in the section wins over the old flat one
                    bool setInSection = !string.Equals(Convert.ToString(current, CultureInfo.InvariantCulture),
                        Convert.ToString(defaultValue, CultureInfo.InvariantCulture), StringComparison.OrdinalIgnoreCase);
                    Config.SetModConfigValue(ModId, key, setInSection ? current : flatValue);
                }

                AccessTools.Method(typeof(Config), "SaveModConfig")?.Invoke(null, new object[] { ModId });
                Logger.LogInfo($"Moved {flatKeys.Count} old config keys into their sections");
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Could not migrate old config keys: {ex.Message}");
            }
        }

        private static bool TryGetPath(Dictionary<string, object> root, string key, out object value)
        {
            value = root;
            foreach (string part in key.Split('.'))
            {
                if (!(value is Dictionary<string, object> section) || !section.TryGetValue(part, out value))
                {
                    value = null;
                    return false;
                }
            }
            return true;
        }

        private static float ValidateIndicatorRadius(float value)
        {
            if (value < 0.5f)
            {
                Logger.LogWarning($"Indicator radius {value} is too small, clamping to 0.5");
                return 0.5f;
            }
            if (value > 10f)
            {
                Logger.LogWarning($"Indicator radius {value} is too large, clamping to 10");
                return 10f;
            }
            return value;
        }

        private static Color ParseColor(object raw, Color defaultColor)
        {
            if (raw is Color c)
            {
                return c;
            }
            string s = raw as string;
            if (string.IsNullOrEmpty(s)) return defaultColor;

            // Try HTML hex first (#RRGGBB or #RRGGBBAA)
            if (ColorUtility.TryParseHtmlString(s, out c))
            {
                return c;
            }

            // Try comma separated floats "r,g,b" or "r,g,b,a"
            var parts = s.Split(',');
            try
            {
                if (parts.Length >= 3)
                {
                    float r = float.Parse(parts[0].Trim());
                    float g = float.Parse(parts[1].Trim());
                    float b = float.Parse(parts[2].Trim());
                    float a = 1f;
                    if (parts.Length >= 4)
                    {
                        a = float.Parse(parts[3].Trim());
                    }
                    return new Color(r, g, b, a);
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Failed to parse color '{s}': {ex.Message}");
            }

            return defaultColor;
        }

    }
}
