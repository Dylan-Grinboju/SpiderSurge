using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Silk;
using UnityEngine;
using YamlDotNet.Core;
using YamlDotNet.Serialization;
using Logger = Silk.Logger;

namespace SpiderSurge.Common
{
    public static class Settings
    {
        public const string FileId = "SpiderSurge";
        private const string BrokenSuffix = ".broken";
        private const float SaveDelay = 0.75f;
        // A file that can't be written (read-only, or locked by a sync tool) is tried again this often
        private const float RetryDelay = 5f;
        private static bool _writeFailing;

        private static readonly Dictionary<string, object> _defaults = new Dictionary<string, object>();
        private static readonly List<string> _keys = new List<string>();
        private static readonly Dictionary<string, object> _cache = new Dictionary<string, object>();
        private static readonly Dictionary<string, object> _unsaved = new Dictionary<string, object>();
        private static readonly HashSet<string> _warned = new HashSet<string>();
        private static readonly Dictionary<string, (Func<bool> locked, string reason)> _locks = new Dictionary<string, (Func<bool> locked, string reason)>();
        private static readonly Dictionary<string, string> _remote = new Dictionary<string, string>();
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

        public static bool Affects(string changed, string key)
        {
            return changed == null || key == changed || key.StartsWith(changed + ".", StringComparison.Ordinal);
        }

        // While locked returns true, Reset and Reload leave the key as it is
        public static void Lock(string key, Func<bool> locked, string reason)
        {
            _locks[key] = (locked, reason);
        }

        public static void Unlock(string key)
        {
            _locks.Remove(key);
        }

        // Silk replaces a file it can't parse with the defaults on the next save, so a broken file is copied aside first
        public static void Load()
        {
            string problem = FileProblem();
            if (problem == null)
            {
                SettingsMigration.Run();
            }
            else
            {
                KeepBrokenFile(problem);
            }

            Config.LoadModConfig(FileId, BuildTree(_defaults));
            _cache.Clear();
            if (problem == null)
            {
                Logger.LogInfo($"SpiderSurge settings loaded from {FilePath}");
            }
        }

        // Returns why the file was not taken as it is, or null
        public static string Reload()
        {
            string problem = FileProblem();
            if (problem != null)
            {
                Logger.LogWarning($"{FilePath} was not reloaded because it has an error: {problem}");
                return $"The settings file has an error, so nothing was reloaded:\n{problem}";
            }

            var held = _locks.Where(pair => pair.Value.locked()).Select(pair => (key: pair.Key, value: Typed(pair.Key), pair.Value.reason)).ToList();
            _unsaved.Clear();
            _saveAt = -1f;
            _warned.Clear();
            Config.LoadModConfig(FileId, BuildTree(_defaults));
            _cache.Clear();

            var reasons = new List<string>();
            foreach (var hold in held)
            {
                if (!Equals(Typed(hold.key), hold.value))
                {
                    Config.SetModConfigValue(FileId, hold.key, hold.value);
                    _cache.Remove(hold.key);
                    reasons.Add(hold.reason);
                }
            }
            Raise(null);
            return reasons.Count > 0 ? string.Join("\n", reasons) : null;
        }

        public static bool HasRemote => _remote.Count > 0;

        public static bool IsRemote(string key) => _remote.ContainsKey(key);

        // The host's value shows while connected, so a change would only rewrite this player's file unseen
        private static bool RefuseRemote(string key)
        {
            if (!_remote.ContainsKey(key))
            {
                return false;
            }
            Logger.LogInfo($"{key} is set by the host while you are connected");
            Raise(key);
            return true;
        }

        // The host's values for shared keys win while connected to it; nothing is written to this machine's file
        public static void ApplyRemote(IDictionary<string, string> values)
        {
            foreach (var pair in values)
            {
                if (IsDefined(pair.Key))
                {
                    _remote[pair.Key] = pair.Value;
                    _cache.Remove(pair.Key);
                }
            }
            Raise(null);
        }

        public static void ClearRemote()
        {
            if (_remote.Count == 0)
            {
                return;
            }
            _remote.Clear();
            _cache.Clear();
            Raise(null);
        }

        public static string Raw(string key) => ReadRaw(key, false);

        public static bool GetBool(string key) => Get<bool>(key, TryParseBool);

        public static int GetInt(string key) => Get<int>(key, TryParseInt);

        public static float GetFloat(string key) => Get<float>(key, TryParseFloat);

        public static string GetString(string key) => Get<string>(key, TryParseString);

        public static void Set(string key, object value)
        {
            if (RefuseRemote(key))
            {
                return;
            }
            _unsaved.Remove(key);
            if (!TryWrite(key, value))
            {
                Retry(new[] { new KeyValuePair<string, object>(key, value) });
            }
            _cache.Remove(key);
            Raise(key);
        }

        // For values that change many times a second (sliders, dragged windows): applied now, written once they settle
        public static void SetLater(string key, object value)
        {
            if (RefuseRemote(key))
            {
                return;
            }
            _unsaved[key] = value;
            _cache.Remove(key);
            _saveAt = Time.unscaledTime + SaveDelay;
            Raise(key);
        }

        public static void Reset(string prefix, params string[] keep)
        {
            var values = new List<KeyValuePair<string, object>>();
            foreach (string key in _keys)
            {
                if (Affects(prefix, key) && Array.IndexOf(keep, key) < 0 && !IsLocked(key))
                {
                    _unsaved.Remove(key);
                    values.Add(new KeyValuePair<string, object>(key, _defaults[key]));
                }
            }
            if (!Save(values))
            {
                Retry(values);
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

            var pending = _unsaved.ToList();
            _unsaved.Clear();
            if (!Save(pending))
            {
                Retry(pending);
            }
        }

        // Values that weren't written stay in effect and are written on a later tick, so a failed write never loses them
        private static void Retry(IEnumerable<KeyValuePair<string, object>> values)
        {
            foreach (var pair in values)
            {
                if (!_unsaved.ContainsKey(pair.Key))
                {
                    _unsaved[pair.Key] = pair.Value;
                }
            }
            _saveAt = Time.unscaledTime + RetryDelay;
        }

        private static bool TryWrite(string key, object value)
        {
            try
            {
                Config.SetModConfigValue(FileId, key, value);
                if (_writeFailing)
                {
                    _writeFailing = false;
                    Logger.LogInfo($"{FilePath} could be written again");
                }
                return true;
            }
            catch (Exception ex)
            {
                if (!_writeFailing)
                {
                    _writeFailing = true;
                    Logger.LogError($"Could not write {FilePath} ({ex.GetBaseException().Message}); trying again every {RetryDelay:0} s");
                }
                return false;
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

        internal static bool TryParseBool(string raw, out bool value)
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

        // Silk writes the whole file on every set, so all values but the last go straight into the sections it holds
        private static bool Save(IList<KeyValuePair<string, object>> values)
        {
            for (int i = 0; i < values.Count; i++)
            {
                string key = values[i].Key;
                int dot = key.LastIndexOf('.');
                var section = i < values.Count - 1 && dot > 0 ? Config.GetModConfigValue<Dictionary<string, object>>(FileId, key.Substring(0, dot), null) : null;
                if (section != null)
                {
                    section[key.Substring(dot + 1)] = values[i].Value;
                }
                else if (!TryWrite(key, values[i].Value))
                {
                    return false;
                }
            }
            return true;
        }

        private static bool IsLocked(string key)
        {
            return _locks.TryGetValue(key, out var entry) && entry.locked();
        }

        private static object Typed(string key)
        {
            switch (Default(key))
            {
                case bool _:
                    return GetBool(key);
                case int _:
                    return GetInt(key);
                case float _:
                    return GetFloat(key);
                default:
                    return GetString(key);
            }
        }

        private static string FileProblem()
        {
            try
            {
                if (!File.Exists(FilePath))
                {
                    return null;
                }
                object root = new DeserializerBuilder().Build().Deserialize<object>(File.ReadAllText(FilePath));
                return root == null || root is IDictionary<object, object> ? null : "it holds no settings";
            }
            catch (YamlException ex)
            {
                string message = ex.Message;
                int cut = message.IndexOf("): ", StringComparison.Ordinal);
                return $"line {ex.Start.Line}: {(cut >= 0 ? message.Substring(cut + 3) : message)}";
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
        }

        private static void KeepBrokenFile(string problem)
        {
            string backup = FilePath + BrokenSuffix;
            try
            {
                File.Copy(FilePath, backup, true);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Could not copy {FilePath} to {backup}: {ex.Message}");
            }
            Logger.LogError($"{FilePath} has an error ({problem}), so every setting starts at its default. The file was copied to {backup}");
            MainThread.PopupLater($"SpiderSurge's settings file has an error, so every setting starts at its default:\n{problem}\nYour file was copied to {Path.GetFileName(backup)} in Silk/Config/Mods.");
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
        private static string ReadRaw(string key) => ReadRaw(key, true);

        private static string ReadRaw(string key, bool allowRemote)
        {
            if (allowRemote && _remote.TryGetValue(key, out string remote))
            {
                return remote;
            }
            object raw = _unsaved.TryGetValue(key, out object pending) ? pending : Config.GetModConfigValue<object>(FileId, key, null);
            if (raw == null || raw is IDictionary<string, object>)
            {
                return null;
            }
            return Convert.ToString(raw, CultureInfo.InvariantCulture).Trim();
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
