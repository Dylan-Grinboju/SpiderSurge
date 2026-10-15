using System;
using System.Collections.Generic;
using System.Globalization;
using Silk;
using UnityEngine;
using Logger = Silk.Logger;

namespace SpiderSurge.Common
{
    // Every part's settings live in one Silk config file; values are parsed in the invariant culture and cached until changed
    public static class Settings
    {
        public const string FileId = "SpiderSurge";
        private const float SaveDelay = 0.75f;

        private static readonly Dictionary<string, object> _defaults = new Dictionary<string, object>();
        private static readonly List<string> _keys = new List<string>();
        private static readonly Dictionary<string, object> _cache = new Dictionary<string, object>();
        private static readonly Dictionary<string, object> _unsaved = new Dictionary<string, object>();
        private static readonly HashSet<string> _warned = new HashSet<string>();
        private static float _saveAt = -1f;

        private delegate bool Parser<T>(string raw, out T value);

        public static event Action<string> Changed;

        public static string FilePath => Config.GetModConfigPath(FileId);

        public static IReadOnlyList<string> Keys => _keys;

        public static void Define(IEnumerable<KeyValuePair<string, object>> defaults)
        {
            foreach (var pair in defaults)
            {
                if (!_defaults.ContainsKey(pair.Key))
                {
                    _keys.Add(pair.Key);
                }
                _defaults[pair.Key] = pair.Value;
            }
        }

        public static bool IsDefined(string key) => _defaults.ContainsKey(key);

        public static object Default(string key) => _defaults.TryGetValue(key, out object value) ? value : null;

        public static void Load()
        {
            SettingsMigration.Run();
            Config.LoadModConfig(FileId, BuildTree(_defaults));
            _cache.Clear();
            Logger.LogInfo($"SpiderSurge settings loaded from {FilePath}");
        }

        public static void Reload()
        {
            _unsaved.Clear();
            _saveAt = -1f;
            _warned.Clear();
            Config.LoadModConfig(FileId, BuildTree(_defaults));
            _cache.Clear();
            Raise(null);
        }

        public static bool GetBool(string key) => Get<bool>(key, TryParseBool);

        public static int GetInt(string key) => Get<int>(key, TryParseInt);

        public static float GetFloat(string key) => Get<float>(key, TryParseFloat);

        public static string GetString(string key) => Get<string>(key, TryParseString);

        public static void Set(string key, object value)
        {
            _unsaved.Remove(key);
            Config.SetModConfigValue(FileId, key, value);
            _cache.Remove(key);
            Raise(key);
        }

        // For values that change many times a second (sliders, dragged windows): applied now, written once they settle
        public static void SetLater(string key, object value)
        {
            _unsaved[key] = value;
            _cache.Remove(key);
            _saveAt = Time.unscaledTime + SaveDelay;
            Raise(key);
        }

        public static void Reset(string prefix, params string[] keep)
        {
            foreach (string key in _keys)
            {
                if ((key == prefix || key.StartsWith(prefix + ".", StringComparison.Ordinal)) && Array.IndexOf(keep, key) < 0)
                {
                    _unsaved.Remove(key);
                    Config.SetModConfigValue(FileId, key, _defaults[key]);
                }
            }
            _cache.Clear();
            _warned.Clear();
            Raise(prefix);
        }

        public static void Tick()
        {
            if (_saveAt >= 0f && Time.unscaledTime >= _saveAt)
            {
                Flush();
            }
        }

        public static void Flush()
        {
            _saveAt = -1f;
            if (_unsaved.Count == 0)
            {
                return;
            }

            var pending = new List<KeyValuePair<string, object>>(_unsaved);
            _unsaved.Clear();
            foreach (var pair in pending)
            {
                Config.SetModConfigValue(FileId, pair.Key, pair.Value);
            }
        }

        // Silk keeps the nested maps it is given and edits them in place, so every load gets fresh ones
        public static Dictionary<string, object> BuildTree(IEnumerable<KeyValuePair<string, object>> flat)
        {
            var root = new Dictionary<string, object>();
            foreach (var pair in flat)
            {
                SetPath(root, pair.Key, pair.Value);
            }
            return root;
        }

        public static void SetPath(Dictionary<string, object> root, string key, object value)
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

        private static T Get<T>(string key, Parser<T> parse)
        {
            if (_cache.TryGetValue(key, out object cached) && cached is T hit)
            {
                return hit;
            }

            T fallback = _defaults.TryGetValue(key, out object defined) && defined is T typed ? typed : default(T);
            T result = fallback;
            string raw = ReadRaw(key);
            if (raw != null)
            {
                if (parse(raw, out T parsed))
                {
                    result = parsed;
                }
                else
                {
                    Warn(key, raw, fallback);
                }
            }

            _cache[key] = result;
            return result;
        }

        // Values come back from the file as text and from the menus as typed objects
        private static string ReadRaw(string key)
        {
            object raw = _unsaved.TryGetValue(key, out object pending) ? pending : Config.GetModConfigValue<object>(FileId, key, null);
            if (raw == null || raw is IDictionary<string, object>)
            {
                return null;
            }
            return Convert.ToString(raw, CultureInfo.InvariantCulture).Trim();
        }

        private static bool TryParseBool(string raw, out bool value)
        {
            switch (raw.ToLowerInvariant())
            {
                case "true":
                case "yes":
                case "on":
                case "1":
                    value = true;
                    return true;
                case "false":
                case "no":
                case "off":
                case "0":
                    value = false;
                    return true;
                default:
                    value = false;
                    return false;
            }
        }

        private static bool TryParseInt(string raw, out int value)
        {
            if (int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
            {
                return true;
            }
            if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out double number) && number > int.MinValue && number < int.MaxValue)
            {
                value = (int)Math.Round(number);
                return true;
            }
            return false;
        }

        private static bool TryParseFloat(string raw, out float value)
        {
            return float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out value) && !float.IsNaN(value) && !float.IsInfinity(value);
        }

        private static bool TryParseString(string raw, out string value)
        {
            value = raw;
            return true;
        }

        private static void Warn(string key, string raw, object fallback)
        {
            if (_warned.Add(key + "=" + raw))
            {
                Logger.LogWarning($"Setting {key} '{raw}' is not valid, using {Convert.ToString(fallback, CultureInfo.InvariantCulture)}");
            }
        }

        private static void Raise(string key)
        {
            Action<string> handler = Changed;
            if (handler == null)
            {
                return;
            }

            foreach (Delegate listener in handler.GetInvocationList())
            {
                try
                {
                    ((Action<string>)listener)(key);
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Settings listener failed: {ex}");
                }
            }
        }
    }
}
