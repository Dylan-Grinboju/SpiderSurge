using Silk;
using Logger = Silk.Logger;
using System;
using UnityEngine;

namespace SpiderSurge
{
    public static class ModConfig
    {
        private const string ModId = SpiderSurge.ModId;

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
