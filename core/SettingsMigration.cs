using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Silk;
using YamlDotNet.Serialization;
using Logger = Silk.Logger;

namespace SpiderSurge.Common
{
    internal static class SettingsMigration
    {
        private const string OldStatsId = "Stats_Mod";
        private const string BackupSuffix = ".old";
        private static readonly string[] PixelPositionKeys = { "display.position.x", "display.position.y" };

        private static readonly Dictionary<string, string> SurgeKeys = new Dictionary<string, string>
        {
            { "UnlimitedPerkChoosingTime", "surge.unlimitedPerkTime" },
            { "EnableStatsLogging", "surge.statsLogging" },
            { "TelemetryEnabled", "surge.telemetry" },
            { "display.showTelemetryConsentPrompt", "surge.showTelemetryPrompt" },
            { "indicator.radius", "surge.indicator.radius" },
            { "indicator.offset.x", "surge.indicator.offset.x" },
            { "indicator.offset.y", "surge.indicator.offset.y" },
            { "indicator.availableColor", "surge.indicator.availableColor" },
            { "indicator.cooldownColor", "surge.indicator.cooldownColor" },
            { "indicator.activeColor", "surge.indicator.activeColor" },
            { "indicator.showOnlyWhenReady", "surge.indicator.showOnlyWhenReady" },
        };

        public static void Run()
        {
            MergeOldFiles();
        }

        // Imports released SpiderSurge and SpiderStats settings; a file whose old DLL is still installed stays where it is
        private static void MergeOldFiles()
        {
            string path = Settings.FilePath;
            string statsPath = Config.GetModConfigPath(OldStatsId);
            try
            {
                Dictionary<string, object> current = Read(path);
                bool upgraded = current != null && IsCurrent(current);
                bool legacySurge = current != null && SurgeKeys.Keys.Any(key => Find(current, key) != null);
                Dictionary<string, object> surge = legacySurge && Parts.Surge.BlockedBy == null ? current : null;
                Dictionary<string, object> stats = Parts.Stats.BlockedBy == null ? Read(statsPath) : null;
                if (!legacySurge && stats == null)
                {
                    return;
                }

                var values = new Dictionary<string, object>();
                int moved = 0;
                if (surge != null)
                {
                    moved += Map(surge, SurgeKeys, values);
                }
                if (stats != null)
                {
                    var statsKeys = Settings.Keys.Where(key => key.StartsWith("stats.")).ToDictionary(key => key.Substring("stats.".Length));
                    moved += Map(stats, statsKeys, values);
                    MovePixelPosition(stats, values);
                }

                if (!upgraded)
                {
                    values["parts.fixes"] = true;
                }
                if (IsFalse(Find(stats, "tracking.enabled")))
                {
                    values["stats.display.showStatsWindow"] = false;
                }
                bool updates = !IsFalse(Find(stats, "updater.checkForUpdates"));
                if (!upgraded || !updates)
                {
                    values[ModUpdater.CheckForUpdatesKey] = updates;
                }

                Dictionary<string, object> tree = current ?? Settings.BuildTree(Settings.Keys.Select(key => new KeyValuePair<string, object>(key, Settings.Default(key))));
                foreach (var pair in values)
                {
                    Settings.SetPath(tree, pair.Key, pair.Value);
                }
                if (surge != null)
                {
                    foreach (string key in SurgeKeys.Keys)
                    {
                        Remove(tree, key);
                    }
                }
                string yaml = new SerializerBuilder().Build().Serialize(tree);

                var sources = new List<string>();
                if (current != null && (!upgraded || surge != null))
                {
                    Backup(path);
                }
                if (surge != null)
                {
                    sources.Add(Path.GetFileName(path));
                }
                File.WriteAllText(path, yaml);
                if (stats != null)
                {
                    MoveAside(statsPath);
                    sources.Add(Path.GetFileName(statsPath));
                }

                if (sources.Count > 0)
                {
                    string on = string.Join(", ", Parts.All.Where(part => !part.CanDisable || (Settings.TryParseBool(Find(tree, part.SettingKey) ?? "", out bool enabled) && enabled)).Select(part => part.Key));
                    Logger.LogInfo($"Moved {moved} settings from {string.Join(", ", sources)} into {Path.GetFileName(path)}; parts on: {on}. The old files are kept with {BackupSuffix} added");
                }
                if (legacySurge && surge == null)
                {
                    Logger.LogInfo($"Kept the legacy Surge settings because {Parts.Surge.BlockedBy} is still in the Mods folder");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Could not move the old settings files into {path}: {ex.Message}");
            }
        }

        // The small Stats window was placed in pixels; a percentage of the screen keeps its spot on any resolution
        private static void MovePixelPosition(Dictionary<string, object> source, Dictionary<string, object> values)
        {
            int[] sizes = { UnityEngine.Screen.width, UnityEngine.Screen.height };
            string[] targets = { StatsMod.ModConfig.PositionXKey, StatsMod.ModConfig.PositionYKey };
            for (int i = 0; i < PixelPositionKeys.Length; i++)
            {
                string raw = Find(source, PixelPositionKeys[i]);
                if (raw != null && double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out double pixels))
                {
                    values[targets[i]] = StatsMod.ModConfig.Percent(pixels, sizes[i]);
                }
            }
        }

        private static bool IsCurrent(Dictionary<string, object> root)
        {
            return Settings.Keys.Select(key => key.Split('.')[0]).Distinct().Any(section => root.TryGetValue(section, out object node) && node is Dictionary<string, object>);
        }

        private static int Map(Dictionary<string, object> source, Dictionary<string, string> keys, Dictionary<string, object> values)
        {
            int moved = 0;
            foreach (var pair in keys)
            {
                string raw = Find(source, pair.Key);
                if (raw == null)
                {
                    continue;
                }

                if (Settings.IsDefined(pair.Value) && TryConvert(raw, Settings.Default(pair.Value), out object value))
                {
                    values[pair.Value] = value;
                    moved++;
                }
            }
            return moved;
        }

        // Typed like the default, so the file keeps plain values instead of quoted text
        private static bool TryConvert(string raw, object fallback, out object value)
        {
            value = null;
            switch (fallback)
            {
                case bool _:
                    if (Settings.TryParseBool(raw, out bool flag))
                    {
                        value = flag;
                    }
                    break;
                case int _:
                    if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out double whole))
                    {
                        value = (int)Math.Round(whole);
                    }
                    break;
                case float _:
                    if (float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out float number))
                    {
                        value = number;
                    }
                    break;
                default:
                    value = raw;
                    break;
            }
            return value != null;
        }

        private static bool IsFalse(string raw)
        {
            return raw != null && Settings.TryParseBool(raw, out bool value) && !value;
        }

        // Older SpiderSurge versions also wrote some keys flat ("indicator.radius") but read only the section, so its value wins
        private static string Find(Dictionary<string, object> root, string key)
        {
            if (root == null)
            {
                return null;
            }

            object node = root;
            foreach (string part in key.Split('.'))
            {
                node = node is Dictionary<string, object> section && section.TryGetValue(part, out object next) ? next : null;
            }
            return Scalar(node) ?? (root.TryGetValue(key, out object flat) ? Scalar(flat) : null);
        }

        private static string Scalar(object node)
        {
            return node == null || node is Dictionary<string, object> ? null : Convert.ToString(node, CultureInfo.InvariantCulture).Trim();
        }

        private static void Remove(Dictionary<string, object> root, string key)
        {
            root.Remove(key);
            int dot = key.IndexOf('.');
            if (dot < 0)
            {
                return;
            }

            string name = key.Substring(0, dot);
            if (root.TryGetValue(name, out object node) && node is Dictionary<string, object> section)
            {
                Remove(section, key.Substring(dot + 1));
                if (section.Count == 0)
                {
                    root.Remove(name);
                }
            }
        }

        private static Dictionary<string, object> Read(string path)
        {
            if (!File.Exists(path))
            {
                return null;
            }
            try
            {
                object raw = new DeserializerBuilder().Build().Deserialize<object>(File.ReadAllText(path));
                return Normalize(raw) as Dictionary<string, object> ?? new Dictionary<string, object>();
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Could not read {path}, so its settings were not moved: {ex.Message}");
                return null;
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

        // The first backup is the file as the old version left it, so a later one doesn't replace it
        private static void Backup(string path)
        {
            string backup = path + BackupSuffix;
            if (!File.Exists(backup))
            {
                File.Copy(path, backup);
            }
        }

        private static void MoveAside(string path)
        {
            string backup = path + BackupSuffix;
            if (File.Exists(backup))
            {
                File.Delete(backup);
            }
            File.Move(path, backup);
        }
    }
}
