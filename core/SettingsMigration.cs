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
    // Builds the one settings file from the separate SpiderSurge, Stats and SpiderTeams files of earlier versions
    internal static class SettingsMigration
    {
        private const string OldStatsId = "Stats_Mod";
        private const string OldTeamsId = "SpiderTeams";
        private const string BackupSuffix = ".old";

        private static readonly Dictionary<string, string> SurgeKeys = new Dictionary<string, string>
        {
            { "EnableSurgeMode", "surge.surgeMode" },
            { "UseDpadForUltimate", "surge.dpadUltimate" },
            { "UnlimitedPerkChoosingTime", "surge.unlimitedPerkTime" },
            { "EnableStatsLogging", "surge.statsLogging" },
            { "TelemetryEnabled", "surge.telemetry" },
            { "display.showTutorial", "surge.showTutorial" },
            { "display.showTelemetryConsentPrompt", "surge.showTelemetryPrompt" },
            { "indicator.radius", "surge.indicator.radius" },
            { "indicator.offset.x", "surge.indicator.offset.x" },
            { "indicator.offset.y", "surge.indicator.offset.y" },
            { "indicator.availableColor", "surge.indicator.availableColor" },
            { "indicator.cooldownColor", "surge.indicator.cooldownColor" },
            { "indicator.activeColor", "surge.indicator.activeColor" },
            { "indicator.showOnlyWhenReady", "surge.indicator.showOnlyWhenReady" },
        };

        private static readonly Dictionary<string, string> TeamsKeys = new Dictionary<string, string>
        {
            { "teams.enabled", "teams.enabled" },
            { "teams.count", "teams.count" },
            { "friendlyFire.mode", "teams.friendlyFire" },
            { "visuals.recolorSpiders", "teams.recolorSpiders" },
            { "visuals.showMarker", "teams.showMarker" },
            { "spawns.groupTeammates", "teams.groupTeammates" },
            { "input.cycleTeamGamepad", "teams.buttonGamepad" },
            { "input.cycleTeamKeyboard", "teams.buttonKeyboard" },
            { "surge.enabled", "teams.surge.enabled" },
            { "surge.ability", "teams.surge.ability" },
            { "surge.ultimate", "teams.surge.ultimate" },
            { "surge.abilityCooldown", "teams.surge.abilityCooldown" },
            { "surge.abilityDuration", "teams.surge.abilityDuration" },
            { "surge.shortTermInvestment", "teams.surge.shortTermInvestment" },
            { "surge.longTermInvestment", "teams.surge.longTermInvestment" },
        };

        public static void Run()
        {
            string path = Settings.FilePath;
            string statsPath = Config.GetModConfigPath(OldStatsId);
            string teamsPath = Config.GetModConfigPath(OldTeamsId);
            try
            {
                Dictionary<string, object> surge = Read(path);
                if (surge != null && surge.ContainsKey("parts"))
                {
                    return;
                }

                Dictionary<string, object> stats = Read(statsPath);
                Dictionary<string, object> teams = Read(teamsPath);
                if (surge == null && stats == null && teams == null)
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
                    moved += Map(stats, Settings.Keys.Where(key => key.StartsWith("stats.")).ToDictionary(key => key.Substring("stats.".Length)), values);
                }
                if (teams != null)
                {
                    moved += Map(teams, TeamsKeys, values);
                }

                values["parts.fixes"] = true;
                values["parts.surge"] = surge != null;
                values["parts.stats"] = stats != null && !IsFalse(Find(stats, "tracking.enabled"));
                values["parts.teams"] = teams != null;
                values["general.checkForUpdates"] = !IsFalse(Find(stats, "updater.checkForUpdates")) && !IsFalse(Find(teams, "updater.checkForUpdates"));

                var merged = Settings.Keys.Select(key => new KeyValuePair<string, object>(key, values.TryGetValue(key, out object value) ? value : Settings.Default(key)));
                string yaml = new SerializerBuilder().Build().Serialize(Settings.BuildTree(merged));

                var sources = new List<string>();
                if (surge != null)
                {
                    File.Copy(path, path + BackupSuffix, true);
                    sources.Add(Path.GetFileName(path));
                }
                File.WriteAllText(path, yaml);
                if (stats != null)
                {
                    MoveAside(statsPath);
                    sources.Add(Path.GetFileName(statsPath));
                }
                if (teams != null)
                {
                    MoveAside(teamsPath);
                    sources.Add(Path.GetFileName(teamsPath));
                }

                string on = string.Join(", ", new[] { "fixes", "surge", "stats", "teams" }.Where(part => values["parts." + part] is bool enabled && enabled));
                Logger.LogInfo($"Moved {moved} settings from {string.Join(", ", sources)} into {Path.GetFileName(path)}; parts on: {on}. The old files are kept with {BackupSuffix} added");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Could not move the old settings files into {path}: {ex.Message}");
            }
        }

        private static int Map(Dictionary<string, object> source, Dictionary<string, string> keys, Dictionary<string, object> values)
        {
            int moved = 0;
            foreach (var pair in keys)
            {
                if (!Settings.IsDefined(pair.Value))
                {
                    continue;
                }

                string raw = Find(source, pair.Key);
                if (raw != null && TryConvert(raw, Settings.Default(pair.Value), out object value))
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
                    if (bool.TryParse(raw, out bool flag))
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
            return raw != null && bool.TryParse(raw, out bool value) && !value;
        }

        // Older SpiderSurge versions wrote some keys flat ("indicator.radius") instead of in sections
        private static string Find(Dictionary<string, object> root, string key)
        {
            if (root == null)
            {
                return null;
            }

            object node = null;
            if (!root.TryGetValue(key, out node))
            {
                node = root;
                foreach (string part in key.Split('.'))
                {
                    if (!(node is Dictionary<string, object> section) || !section.TryGetValue(part, out node))
                    {
                        return null;
                    }
                }
            }

            if (node == null || node is Dictionary<string, object>)
            {
                return null;
            }
            return Convert.ToString(node, CultureInfo.InvariantCulture).Trim();
        }

        private static Dictionary<string, object> Read(string path)
        {
            if (!File.Exists(path))
            {
                return null;
            }
            object raw = new DeserializerBuilder().Build().Deserialize<object>(File.ReadAllText(path));
            return Normalize(raw) as Dictionary<string, object> ?? new Dictionary<string, object>();
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
