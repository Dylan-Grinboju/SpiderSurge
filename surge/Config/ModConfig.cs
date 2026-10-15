using System;
using System.Collections.Generic;
using System.Globalization;
using SpiderSurge.Common;
using UnityEngine;
using Logger = Silk.Logger;

namespace SpiderSurge
{
    public static class ModConfig
    {
        public const string SurgeModeKey = "surge.surgeMode";
        public const string DpadUltimateKey = "surge.dpadUltimate";
        public const string UnlimitedPerkTimeKey = "surge.unlimitedPerkTime";
        public const string ShowTutorialKey = "surge.showTutorial";
        public const string ShowTelemetryPromptKey = "surge.showTelemetryPrompt";
        public const string StatsLoggingKey = "surge.statsLogging";
        public const string TelemetryKey = "surge.telemetry";
        public const string IndicatorRadiusKey = "surge.indicator.radius";
        public const string IndicatorOffsetXKey = "surge.indicator.offset.x";
        public const string IndicatorOffsetYKey = "surge.indicator.offset.y";
        public const string IndicatorAvailableColorKey = "surge.indicator.availableColor";
        public const string IndicatorCooldownColorKey = "surge.indicator.cooldownColor";
        public const string IndicatorActiveColorKey = "surge.indicator.activeColor";
        public const string IndicatorShowOnlyWhenReadyKey = "surge.indicator.showOnlyWhenReady";

        public const float MinIndicatorRadius = 0.5f;
        public const float MaxIndicatorRadius = 10f;

        public static readonly KeyValuePair<string, object>[] Defaults =
        {
            new KeyValuePair<string, object>(SurgeModeKey, true),
            new KeyValuePair<string, object>(DpadUltimateKey, false),
            new KeyValuePair<string, object>(UnlimitedPerkTimeKey, true),
            new KeyValuePair<string, object>(ShowTutorialKey, true),
            new KeyValuePair<string, object>(ShowTelemetryPromptKey, true),
            new KeyValuePair<string, object>(StatsLoggingKey, true),
            new KeyValuePair<string, object>(TelemetryKey, false),
            new KeyValuePair<string, object>(IndicatorRadiusKey, 1.5f),
            new KeyValuePair<string, object>(IndicatorOffsetXKey, 3f),
            new KeyValuePair<string, object>(IndicatorOffsetYKey, 8f),
            new KeyValuePair<string, object>(IndicatorAvailableColorKey, "#00FF00"),
            new KeyValuePair<string, object>(IndicatorCooldownColorKey, "#FF0000"),
            new KeyValuePair<string, object>(IndicatorActiveColorKey, "#87CEEB"),
            new KeyValuePair<string, object>(IndicatorShowOnlyWhenReadyKey, false),
        };

        private static readonly Dictionary<string, Color> _colors = new Dictionary<string, Color>();
        private static readonly HashSet<string> _warned = new HashSet<string>();

        // Surge mode settings
        public static bool enableSurgeMode => Settings.GetBool(SurgeModeKey);

        // Ability indicator configuration
        public static float IndicatorRadius => Mathf.Clamp(Settings.GetFloat(IndicatorRadiusKey), MinIndicatorRadius, MaxIndicatorRadius);
        public static float IndicatorOffsetX => Settings.GetFloat(IndicatorOffsetXKey);
        public static float IndicatorOffsetY => Settings.GetFloat(IndicatorOffsetYKey);
        public static Color IndicatorAvailableColor => ReadColor(IndicatorAvailableColorKey);
        public static Color IndicatorCooldownColor => ReadColor(IndicatorCooldownColorKey);
        public static Color IndicatorActiveColor => ReadColor(IndicatorActiveColorKey);
        public static bool IndicatorShowOnlyWhenReady => Settings.GetBool(IndicatorShowOnlyWhenReadyKey);

        // Ultimate activation configuration
        public static bool UltimateUseDpadActivation => Settings.GetBool(DpadUltimateKey);

        public static bool UnlimitedPerkChoosingTime => Settings.GetBool(UnlimitedPerkTimeKey);

        public static bool EnableStatsLogging => Settings.GetBool(StatsLoggingKey);
        public static bool TelemetryEnabled => Settings.GetBool(TelemetryKey);
        public static bool ShowTutorial => Settings.GetBool(ShowTutorialKey);
        public static bool ShowTelemetryConsentPrompt => Settings.GetBool(ShowTelemetryPromptKey);

        public static void SetEnableSurgeMode(bool value) => Settings.Set(SurgeModeKey, value);

        public static void SetShowTutorial(bool value) => Settings.Set(ShowTutorialKey, value);

        public static void SetTelemetryEnabled(bool value) => Settings.Set(TelemetryKey, value);

        public static void SetShowTelemetryConsentPrompt(bool value) => Settings.Set(ShowTelemetryPromptKey, value);

        private static Color ReadColor(string key)
        {
            string raw = Settings.GetString(key);
            if (_colors.TryGetValue(raw, out Color color))
            {
                return color;
            }

            if (!TryParseColor(raw, out color))
            {
                string fallback = (string)Settings.Default(key);
                if (_warned.Add(key + "=" + raw))
                {
                    Logger.LogWarning($"Setting {key} '{raw}' is not a color, using {fallback}");
                }
                TryParseColor(fallback, out color);
            }
            _colors[raw] = color;
            return color;
        }

        // HTML colors (#RRGGBB, #RRGGBBAA, names) or "r,g,b" / "r,g,b,a" with channels from 0 to 1
        public static bool TryParseColor(string s, out Color color)
        {
            if (ColorUtility.TryParseHtmlString(s, out color)) return true;

            string[] parts = s.Split(',');
            if (parts.Length < 3) return false;

            float[] channels = { 0f, 0f, 0f, 1f };
            for (int i = 0; i < Math.Min(parts.Length, 4); i++)
            {
                if (!float.TryParse(parts[i].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out channels[i])) return false;
            }
            color = new Color(channels[0], channels[1], channels[2], channels[3]);
            return true;
        }
    }
}
