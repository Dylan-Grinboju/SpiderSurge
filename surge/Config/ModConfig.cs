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
        public const string UnlimitedPerkTimeKey = "surge.unlimitedPerkTime";
        public const string NoAbilitiesKey = "surge.noAbilities";
        public const string AbilityVolumeKey = "surge.abilityVolume";
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
        public const int OriginalAbilityVolume = 80;
        public const int DefaultAbilityVolume = 50;
        public const int MaxAbilityVolume = 100;

        public static readonly KeyValuePair<string, object>[] Defaults =
        {
            new KeyValuePair<string, object>(UnlimitedPerkTimeKey, true),
            new KeyValuePair<string, object>(NoAbilitiesKey, false),
            new KeyValuePair<string, object>(AbilityVolumeKey, DefaultAbilityVolume),
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

        private static readonly Dictionary<string, (string raw, Color color)> _colors = new Dictionary<string, (string raw, Color color)>();
        private static readonly HashSet<string> _badColors = new HashSet<string>();

        // Ability indicator configuration
        public static float IndicatorRadius => Mathf.Clamp(Settings.GetFloat(IndicatorRadiusKey), MinIndicatorRadius, MaxIndicatorRadius);
        public static float IndicatorOffsetX => Settings.GetFloat(IndicatorOffsetXKey);
        public static float IndicatorOffsetY => Settings.GetFloat(IndicatorOffsetYKey);
        public static Color IndicatorAvailableColor => ReadColor(IndicatorAvailableColorKey);
        public static Color IndicatorCooldownColor => ReadColor(IndicatorCooldownColorKey);
        public static Color IndicatorActiveColor => ReadColor(IndicatorActiveColorKey);
        public static bool IndicatorShowOnlyWhenReady => Settings.GetBool(IndicatorShowOnlyWhenReadyKey);

        public static bool UnlimitedPerkChoosingTime => Settings.GetBool(UnlimitedPerkTimeKey);

        public static bool NoAbilities => Settings.GetBool(NoAbilitiesKey);

        // The slider's 80 plays every Surge sound at its original volume
        public static float AbilityVolume => Mathf.Clamp(Settings.GetInt(AbilityVolumeKey), 0, MaxAbilityVolume) / (float)OriginalAbilityVolume;

        public static bool EnableStatsLogging => Settings.GetBool(StatsLoggingKey);
        public static bool TelemetryEnabled => Settings.GetBool(TelemetryKey);
        public static bool ShowTutorial => Settings.GetBool(ShowTutorialKey);
        public static bool ShowTelemetryConsentPrompt => Settings.GetBool(ShowTelemetryPromptKey);

        public static void SetShowTutorial(bool value) => Settings.Set(ShowTutorialKey, value);

        public static void SetTelemetryEnabled(bool value) => Settings.Set(TelemetryKey, value);

        public static void SetShowTelemetryConsentPrompt(bool value) => Settings.Set(ShowTelemetryPromptKey, value);

        public static void ForgetColors() => _colors.Clear();

        // Cached per key with the text it was read from; a bad value falls back to the default and warns once
        public static Color ReadColor(string key)
        {
            string raw = Settings.GetString(key);
            if (_colors.TryGetValue(key, out var cached) && cached.raw == raw)
            {
                return cached.color;
            }

            if (!TryParseColor(raw, out Color color))
            {
                string fallback = (string)Settings.Default(key);
                if (_badColors.Add(key + "=" + raw))
                {
                    Logger.LogWarning($"Setting {key} '{raw}' is not a color, using {fallback}");
                }
                TryParseColor(fallback, out color);
            }
            _colors[key] = (raw, color);
            return color;
        }

        // HTML colors (#RRGGBB, #RRGGBBAA, names) or "r,g,b" / "r,g,b,a" with channels from 0 to 1
        public static bool TryParseColor(string s, out Color color)
        {
            color = default;
            if (string.IsNullOrEmpty(s)) return false;
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
