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
        private const string OldTeamsId = "SpiderTeams";
        private const string BackupSuffix = ".old";
        private static readonly string[] PartKeys = { "fixes", "stats", "teams" };
        private static readonly string[] PixelPositionKeys = { "display.position.x", "display.position.y" };

        private static readonly Dictionary<string, string> SurgeKeys = new Dictionary<string, string>
        {
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

        private const string Sticks = "<Gamepad>/leftStickPress|<Gamepad>/rightStickPress";

        private static readonly (string from, string to, Func<string, string> convert)[] MovedKeys =
        {
            ("surge.dpadUltimate", Controls.UltimateController.Key, raw => Settings.TryParseBool(raw, out bool on) && on ? Controls.Dpad : null),
            ("stats.input.cycleInput", Controls.SwitchWindows.Key, StatsSwitchBinding),
            ("teams.buttonGamepad", Controls.TeamController.Key, raw => TeamBinding(Controls.TeamController, raw)),
            ("teams.buttonKeyboard", Controls.TeamKeyboard.Key, raw => TeamBinding(Controls.TeamKeyboard, raw)),
        };

        public static void Run()
        {
            MergeOldFiles();
            MoveKeys();
        }

        // Merges the files of earlier versions into the one settings file; a file whose old DLL is still installed stays where it is
        private static void MergeOldFiles()
        {
            string path = Settings.FilePath;
            string statsPath = Config.GetModConfigPath(OldStatsId);
            string teamsPath = Config.GetModConfigPath(OldTeamsId);
            try
            {
                Dictionary<string, object> current = Read(path);
                bool upgraded = current != null && IsCurrent(current);
                bool legacySurge = current != null && SurgeKeys.Keys.Any(key => Find(current, key) != null);
                Dictionary<string, object> surge = legacySurge && Parts.Surge.BlockedBy == null ? current : null;
                Dictionary<string, object> stats = Parts.Stats.BlockedBy == null ? Read(statsPath) : null;
                Dictionary<string, object> teams = Parts.Teams.BlockedBy == null ? Read(teamsPath) : null;
                bool pixelPosition = current != null && PixelPositionKeys.Any(key => Find(current, "stats." + key) != null);
                if (!legacySurge && stats == null && teams == null && !pixelPosition)
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
                    statsKeys["input.cycleInput"] = "stats.input.cycleInput";
                    moved += Map(stats, statsKeys, values);
                    MovePixelPosition(stats, "", values);
                }
                if (pixelPosition)
                {
                    MovePixelPosition(current, "stats.", values);
                }
                if (teams != null)
                {
                    moved += Map(teams, TeamsKeys, values);
                }

                if (!upgraded)
                {
                    values["parts.fixes"] = true;
                }
                if (stats != null || !upgraded)
                {
                    values["parts.stats"] = stats != null && !IsFalse(Find(stats, "tracking.enabled"));
                }
                if (teams != null || !upgraded)
                {
                    values["parts.teams"] = teams != null;
                }
                bool updates = !IsFalse(Find(stats, "updater.checkForUpdates")) && !IsFalse(Find(teams, "updater.checkForUpdates"));
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
                foreach (string key in PixelPositionKeys)
                {
                    Remove(tree, "stats." + key);
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
                if (teams != null)
                {
                    MoveAside(teamsPath);
                    sources.Add(Path.GetFileName(teamsPath));
                }

                if (sources.Count > 0)
                {
                    string on = string.Join(", ", PartKeys.Where(part => Settings.TryParseBool(Find(tree, "parts." + part) ?? "", out bool enabled) && enabled));
                    Logger.LogInfo($"Moved {moved} settings from {string.Join(", ", sources)} into {Path.GetFileName(path)}; parts on: {on}. The old files are kept with {BackupSuffix} added");
                }
                if (legacySurge && surge == null)
                {
                    Logger.LogInfo($"Kept the legacy Surge settings because {Parts.Surge.BlockedBy} is still in the Mods folder");
                }
                if (pixelPosition)
                {
                    Logger.LogInfo("The Stats window position is now saved as a percentage of the screen");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Could not move the old settings files into {path}: {ex.Message}");
            }
        }

        private static void MoveKeys()
        {
            string path = Settings.FilePath;
            try
            {
                Dictionary<string, object> tree = Read(path);
                if (tree == null || MovedKeys.All(moved => Find(tree, moved.from) == null))
                {
                    return;
                }

                var names = new List<string>();
                foreach (var moved in MovedKeys)
                {
                    string raw = Find(tree, moved.from);
                    if (raw == null)
                    {
                        continue;
                    }
                    string binding = moved.convert(raw);
                    if (binding != null && Find(tree, moved.to) == null)
                    {
                        Settings.SetPath(tree, moved.to, binding);
                    }
                    Remove(tree, moved.from);
                    names.Add(moved.from);
                }

                Backup(path);
                File.WriteAllText(path, new SerializerBuilder().Build().Serialize(tree));
                Logger.LogInfo($"Moved {string.Join(", ", names)} to the controls section; change them in Mods > SpiderSurge > Controls");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Could not move the old control settings in {path}: {ex.Message}");
            }
        }

        // Select was a team button before; the Controls page keeps it for the pause menu and the ability remap
        private static string TeamBinding(Control control, string raw)
        {
            if (Controls.Accepts(control, raw)) return raw;
            Logger.LogWarning($"The team button '{raw}' can't be used any more, so {control.Key} starts on {Controls.Label(control.Default)}");
            return null;
        }

        private static string StatsSwitchBinding(string raw)
        {
            switch (raw.ToLowerInvariant())
            {
                case "joystick":
                    return Sticks;
                case "both":
                    return Controls.Dpad + Controls.AlternativeSeparator + Sticks;
                case "none":
                    return Controls.None;
                default:
                    return null;
            }
        }

        private static bool TryMove(string key, string raw, out string to, out string binding)
        {
            foreach (var moved in MovedKeys)
            {
                if (moved.from == key)
                {
                    to = moved.to;
                    binding = moved.convert(raw);
                    return true;
                }
            }
            to = binding = null;
            return false;
        }

        // The small Stats window was placed in pixels; a percentage of the screen keeps its spot on any resolution
        private static void MovePixelPosition(Dictionary<string, object> source, string prefix, Dictionary<string, object> values)
        {
            int[] sizes = { UnityEngine.Screen.width, UnityEngine.Screen.height };
            string[] targets = { StatsMod.ModConfig.PositionXKey, StatsMod.ModConfig.PositionYKey };
            for (int i = 0; i < PixelPositionKeys.Length; i++)
            {
                string raw = Find(source, prefix + PixelPositionKeys[i]);
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

                if (TryMove(pair.Value, raw, out string to, out string binding))
                {
                    if (binding != null)
                    {
                        values[to] = binding;
                        moved++;
                    }
                }
                else if (Settings.IsDefined(pair.Value) && TryConvert(raw, Settings.Default(pair.Value), out object value))
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
