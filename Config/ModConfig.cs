using Silk;
using Logger = Silk.Logger;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEngine;
using YamlDotNet.Serialization;

namespace SpiderSurge
{
    public static class ModConfig
    {
        private const string ModId = SpiderSurgeMod.ModId;
        private static readonly HashSet<string> _warned = new HashSet<string>();

        private static readonly Dictionary<string, object> Defaults = new Dictionary<string, object>
        {
            { "EnableSurgeMode", true },
            { "indicator.radius", 1.5f },
            { "indicator.offset.x", 3f },
            { "indicator.offset.y", 8f },
            { "indicator.availableColor", "#00FF00" },
            { "indicator.cooldownColor", "#FF0000" },
            { "indicator.activeColor", "#87CEEB" },
            { "indicator.showOnlyWhenReady", false },
            { "UseDpadForUltimate", false },
            { "UnlimitedPerkChoosingTime", true },
            { "EnableStatsLogging", true },
            { "TelemetryEnabled", false },
            { "display.showTutorial", true },
            { "display.showTelemetryConsentPrompt", true },
        };

        // Surge mode settings
        public static bool enableSurgeMode => ReadBool("EnableSurgeMode");

        // Ability indicator configuration
        public static float IndicatorRadius => ValidateIndicatorRadius(ReadFloat("indicator.radius"));
        public static float IndicatorOffsetX => ReadFloat("indicator.offset.x");
        public static float IndicatorOffsetY => ReadFloat("indicator.offset.y");
        public static Color IndicatorAvailableColor => ReadColor("indicator.availableColor");
        public static Color IndicatorCooldownColor => ReadColor("indicator.cooldownColor");
        public static Color IndicatorActiveColor => ReadColor("indicator.activeColor");
        public static bool IndicatorShowOnlyWhenReady => ReadBool("indicator.showOnlyWhenReady");

        // Ultimate activation configuration
        public static bool UltimateUseDpadActivation => ReadBool("UseDpadForUltimate");

        public static bool UnlimitedPerkChoosingTime => ReadBool("UnlimitedPerkChoosingTime");

        public static bool EnableStatsLogging => ReadBool("EnableStatsLogging");
        public static bool TelemetryEnabled => ReadBool("TelemetryEnabled");
        public static bool ShowTutorial => ReadBool("display.showTutorial");
        public static bool ShowTelemetryConsentPrompt => ReadBool("display.showTelemetryConsentPrompt");

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

        // Silk splits keys on dots into sections and keeps the sections it is given, so each load gets fresh ones
        public static Dictionary<string, object> BuildDefaults()
        {
            var root = new Dictionary<string, object>();
            foreach (var pair in Defaults)
            {
                SetPath(root, pair.Key, pair.Value);
            }
            return root;
        }

        // Older versions wrote "a.b" keys flat, where Silk never reads them; this nests them in the file before Silk loads it
        public static void MigrateFlatKeys()
        {
            string path = Config.GetModConfigPath(ModId);
            try
            {
                if (!File.Exists(path)) return;
                if (!(Normalize(new DeserializerBuilder().Build().Deserialize<object>(File.ReadAllText(path))) is Dictionary<string, object> root)) return;

                List<string> flatKeys = root.Keys.Where(key => key.Contains(".")).ToList();
                if (flatKeys.Count == 0) return;

                int moved = 0;
                int kept = 0;
                var dropped = new List<string>();
                foreach (string key in flatKeys)
                {
                    object flatValue = root[key];
                    root.Remove(key);
                    if (!Defaults.ContainsKey(key))
                    {
                        dropped.Add(key);
                    }
                    // Older versions read the section, so a value there is the one in effect and stays
                    else if (flatValue == null || TryGetPath(root, key, out object current) && current != null)
                    {
                        kept++;
                    }
                    else
                    {
                        SetPath(root, key, flatValue);
                        moved++;
                    }
                }

                File.WriteAllText(path, new SerializerBuilder().Build().Serialize(root));
                string droppedText = dropped.Count > 0 ? $", dropped unused {string.Join(", ", dropped)}" : "";
                Logger.LogInfo($"Moved {moved} old config keys into their sections, kept {kept} section values{droppedText}");
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Could not move the old config keys in {path} into their sections: {ex.Message}");
            }
        }

        private static object Normalize(object node)
        {
            if (node is IDictionary<object, object> map)
            {
                return map.ToDictionary(pair => pair.Key.ToString(), pair => Normalize(pair.Value));
            }
            if (node is IList<object> list)
            {
                return list.Select(Normalize).ToList();
            }
            return node;
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

        private static void SetPath(Dictionary<string, object> root, string key, object value)
        {
            string[] parts = key.Split('.');
            Dictionary<string, object> section = root;
            for (int i = 0; i < parts.Length - 1; i++)
            {
                if (!section.TryGetValue(parts[i], out object next) || !(next is Dictionary<string, object>))
                {
                    next = new Dictionary<string, object>();
                    section[parts[i]] = next;
                }
                section = (Dictionary<string, object>)next;
            }
            section[parts[parts.Length - 1]] = value;
        }

        // Values come back from the file as text, and Silk's typed read would parse them in the player's culture
        private static string ReadRaw(string key)
        {
            object raw = Config.GetModConfigValue<object>(ModId, key, null);
            return raw == null ? null : Convert.ToString(raw, CultureInfo.InvariantCulture).Trim();
        }

        private static bool ReadBool(string key)
        {
            bool fallback = (bool)Defaults[key];
            string raw = ReadRaw(key);
            if (raw == null) return fallback;
            if (bool.TryParse(raw, out bool value)) return value;

            Warn(key, raw, fallback);
            return fallback;
        }

        private static float ReadFloat(string key)
        {
            float fallback = (float)Defaults[key];
            string raw = ReadRaw(key);
            if (raw == null) return fallback;
            if (float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out float value)) return value;

            Warn(key, raw, fallback);
            return fallback;
        }

        private static Color ReadColor(string key)
        {
            string fallback = (string)Defaults[key];
            string raw = ReadRaw(key);
            if (!string.IsNullOrEmpty(raw))
            {
                if (TryParseColor(raw, out Color color)) return color;
                Warn(key, raw, fallback);
            }

            TryParseColor(fallback, out Color defaultColor);
            return defaultColor;
        }

        private static void Warn(string key, string raw, object fallback)
        {
            if (_warned.Add(key + "=" + raw))
            {
                Logger.LogWarning($"Config {key} '{raw}' is not valid, using {Convert.ToString(fallback, CultureInfo.InvariantCulture)}");
            }
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

        // HTML colors (#RRGGBB, #RRGGBBAA, names) or "r,g,b" / "r,g,b,a" with channels from 0 to 1
        private static bool TryParseColor(string s, out Color color)
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
