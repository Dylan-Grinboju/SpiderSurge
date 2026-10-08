using System;
using System.Collections.Generic;
using System.Globalization;
using Silk;
using Logger = Silk.Logger;

namespace SpiderTeams
{
    public enum FriendlyFireMode
    {
        Push,
        Ignore,
        Damage
    }

    public static class ModConfig
    {
        private const string ModId = SpiderTeamsMod.ModId;
        private static readonly HashSet<string> _warned = new HashSet<string>();

        public static event Action Changed;

        public static bool TeamsEnabled { get; private set; } = Consts.Defaults.TeamsEnabled;
        public static int TeamCount { get; private set; } = Consts.Defaults.TeamCount;
        public static FriendlyFireMode FriendlyFire { get; private set; } = FriendlyFireMode.Push;
        public static bool RecolorSpiders { get; private set; } = Consts.Defaults.RecolorSpiders;
        public static bool ShowMarker { get; private set; } = Consts.Defaults.ShowMarker;
        public static bool GroupTeammates { get; private set; } = Consts.Defaults.GroupTeammates;
        public static string CycleTeamGamepad { get; private set; } = Consts.Defaults.CycleTeamGamepad;
        public static string CycleTeamKeyboard { get; private set; } = Consts.Defaults.CycleTeamKeyboard;
        public static bool CheckForUpdates { get; private set; } = Consts.Defaults.CheckForUpdates;

        public static string ConfigPath => Config.GetModConfigPath(ModId);

        public static void Load()
        {
            Config.LoadModConfig(ModId, Consts.Defaults.BuildNested());
            Refresh();
            Logger.LogInfo($"SpiderTeams config loaded from {ConfigPath}");
        }

        public static void ReloadFromFile()
        {
            _warned.Clear();
            Load();
        }

        public static void ResetToDefaults()
        {
            foreach (var pair in Consts.Defaults.Flat)
            {
                Config.SetModConfigValue(ModId, pair.Key, pair.Value);
            }
            _warned.Clear();
            Refresh();
            Logger.LogInfo("SpiderTeams config reset to defaults");
        }

        public static void SetTeamsEnabled(bool value) => Set(Consts.Keys.TeamsEnabled, value);
        public static void SetTeamCount(int value) => Set(Consts.Keys.TeamCount, ClampTeams(value));
        public static void SetFriendlyFire(FriendlyFireMode value) => Set(Consts.Keys.FriendlyFire, FriendlyFireName(value));
        public static void SetRecolorSpiders(bool value) => Set(Consts.Keys.RecolorSpiders, value);
        public static void SetShowMarker(bool value) => Set(Consts.Keys.ShowMarker, value);
        public static void SetGroupTeammates(bool value) => Set(Consts.Keys.GroupTeammates, value);
        public static void SetCycleTeamGamepad(string path) => Set(Consts.Keys.CycleTeamGamepad, path);
        public static void SetCycleTeamKeyboard(string path) => Set(Consts.Keys.CycleTeamKeyboard, path);
        public static void SetCheckForUpdates(bool value) => Set(Consts.Keys.CheckForUpdates, value);

        public static int ClampTeams(int value)
        {
            return Math.Max(Consts.MinTeams, Math.Min(Consts.MaxTeams, value));
        }

        public static string FriendlyFireName(FriendlyFireMode mode)
        {
            switch (mode)
            {
                case FriendlyFireMode.Ignore:
                    return "ignore";
                case FriendlyFireMode.Damage:
                    return "damage";
                default:
                    return "push";
            }
        }

        public static bool TryParseFriendlyFire(string raw, out FriendlyFireMode mode)
        {
            switch ((raw ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "push":
                    mode = FriendlyFireMode.Push;
                    return true;
                case "ignore":
                    mode = FriendlyFireMode.Ignore;
                    return true;
                case "damage":
                    mode = FriendlyFireMode.Damage;
                    return true;
                default:
                    mode = FriendlyFireMode.Push;
                    return false;
            }
        }

        private static void Set(string key, object value)
        {
            Config.SetModConfigValue(ModId, key, value);
            Refresh();
        }

        private static void Refresh()
        {
            TeamsEnabled = ReadBool(Consts.Keys.TeamsEnabled, Consts.Defaults.TeamsEnabled);
            TeamCount = ReadTeamCount();
            FriendlyFire = ReadFriendlyFire();
            RecolorSpiders = ReadBool(Consts.Keys.RecolorSpiders, Consts.Defaults.RecolorSpiders);
            ShowMarker = ReadBool(Consts.Keys.ShowMarker, Consts.Defaults.ShowMarker);
            GroupTeammates = ReadBool(Consts.Keys.GroupTeammates, Consts.Defaults.GroupTeammates);
            CycleTeamGamepad = ReadBinding(Consts.Keys.CycleTeamGamepad, Consts.Defaults.CycleTeamGamepad);
            CycleTeamKeyboard = ReadBinding(Consts.Keys.CycleTeamKeyboard, Consts.Defaults.CycleTeamKeyboard);
            CheckForUpdates = ReadBool(Consts.Keys.CheckForUpdates, Consts.Defaults.CheckForUpdates);
            RaiseChanged();
        }

        private static void RaiseChanged()
        {
            Action handler = Changed;
            if (handler == null)
            {
                return;
            }

            foreach (Delegate listener in handler.GetInvocationList())
            {
                try
                {
                    ((Action)listener)();
                }
                catch (Exception ex)
                {
                    Logger.LogError($"SpiderTeams config listener failed: {ex}");
                }
            }
        }

        // Read as raw text so a hand-edited value never trips Silk's typed conversion.
        private static string ReadRaw(string key)
        {
            object raw = Config.GetModConfigValue<object>(ModId, key, null);
            return raw == null ? null : Convert.ToString(raw, CultureInfo.InvariantCulture).Trim();
        }

        private static bool ReadBool(string key, bool fallback)
        {
            string raw = ReadRaw(key);
            if (raw == null)
            {
                return fallback;
            }

            switch (raw.ToLowerInvariant())
            {
                case "true":
                case "yes":
                case "on":
                case "1":
                    return true;
                case "false":
                case "no":
                case "off":
                case "0":
                    return false;
            }

            Warn(key, raw, $"is not true or false, using {(fallback ? "true" : "false")}");
            return fallback;
        }

        private static int ReadTeamCount()
        {
            string raw = ReadRaw(Consts.Keys.TeamCount);
            if (raw == null)
            {
                return Consts.Defaults.TeamCount;
            }

            int value;
            if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
            {
                Warn(Consts.Keys.TeamCount, raw, $"is not a whole number, using {Consts.Defaults.TeamCount}");
                return Consts.Defaults.TeamCount;
            }

            int clamped = ClampTeams(value);
            if (clamped != value)
            {
                Warn(Consts.Keys.TeamCount, raw, $"must be {Consts.MinTeams}-{Consts.MaxTeams}, using {clamped}");
            }
            return clamped;
        }

        private static FriendlyFireMode ReadFriendlyFire()
        {
            string raw = ReadRaw(Consts.Keys.FriendlyFire);
            if (raw == null)
            {
                return FriendlyFireMode.Push;
            }

            FriendlyFireMode mode;
            if (!TryParseFriendlyFire(raw, out mode))
            {
                Warn(Consts.Keys.FriendlyFire, raw, "is not push, ignore or damage, using push");
            }
            return mode;
        }

        private static string ReadBinding(string key, string fallback)
        {
            string raw = ReadRaw(key);
            if (raw == null)
            {
                return fallback;
            }
            if (raw.Length == 0 || raw.Equals(Consts.Bindings.None, StringComparison.OrdinalIgnoreCase))
            {
                return Consts.Bindings.None;
            }
            if (!raw.StartsWith("<") || raw.IndexOf('/') < 0)
            {
                Warn(key, raw, $"is not an Input System path like {fallback}, using {fallback}");
                return fallback;
            }
            return raw;
        }

        private static void Warn(string key, string value, string problem)
        {
            if (_warned.Add(key + "=" + value))
            {
                Logger.LogWarning($"SpiderTeams config: {key} '{value}' {problem}");
            }
        }
    }
}
