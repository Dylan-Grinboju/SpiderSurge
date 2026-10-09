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

    public enum SurgeAbility
    {
        Immune,
        Ammo,
        Pulse,
        Storage,
        Random
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
        public static bool SurgeEnabled { get; private set; } = Consts.Defaults.SurgeEnabled;
        public static SurgeAbility SurgeAbility { get; private set; } = DefaultSurgeAbility;
        public static bool SurgeUltimate { get; private set; } = Consts.Defaults.SurgeUltimate;
        public static int SurgeCooldown { get; private set; } = Consts.Defaults.SurgeCooldown;
        public static int SurgeDuration { get; private set; } = Consts.Defaults.SurgeDuration;
        public static bool SurgeShortTerm { get; private set; } = Consts.Defaults.SurgeShortTerm;
        public static bool SurgeLongTerm { get; private set; } = Consts.Defaults.SurgeLongTerm;

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
        public static void SetSurgeEnabled(bool value) => Set(Consts.Keys.SurgeEnabled, value);
        public static void SetSurgeAbility(SurgeAbility value) => Set(Consts.Keys.SurgeAbility, SurgeAbilityName(value));
        public static void SetSurgeUltimate(bool value) => Set(Consts.Keys.SurgeUltimate, value);
        public static void SetSurgeCooldown(int value) => Set(Consts.Keys.SurgeCooldown, ClampPerk(value));
        public static void SetSurgeDuration(int value) => Set(Consts.Keys.SurgeDuration, ClampPerk(value));
        public static void SetSurgeShortTerm(bool value) => Set(Consts.Keys.SurgeShortTerm, value);
        public static void SetSurgeLongTerm(bool value) => Set(Consts.Keys.SurgeLongTerm, value);

        public static int ClampPerk(int value)
        {
            return Math.Max(0, Math.Min(Consts.Surge.MaxPerkLevel, value));
        }

        public static string SurgeAbilityName(SurgeAbility ability)
        {
            return Consts.Surge.AbilityConfigNames[(int)ability];
        }

        public static bool TryParseSurgeAbility(string raw, out SurgeAbility ability)
        {
            string name = (raw ?? string.Empty).Trim();
            for (int i = 0; i < Consts.Surge.AbilityConfigNames.Length; i++)
            {
                if (name.Equals(Consts.Surge.AbilityConfigNames[i], StringComparison.OrdinalIgnoreCase) ||
                    name.Equals(Consts.Surge.AbilityNames[i], StringComparison.OrdinalIgnoreCase))
                {
                    ability = (SurgeAbility)i;
                    return true;
                }
            }
            ability = default(SurgeAbility);
            return false;
        }

        private static SurgeAbility DefaultSurgeAbility
        {
            get
            {
                int index = Array.IndexOf(Consts.Surge.AbilityConfigNames, Consts.Defaults.SurgeAbility);
                return index >= 0 ? (SurgeAbility)index : SurgeAbility.Random;
            }
        }

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
            SurgeEnabled = ReadBool(Consts.Keys.SurgeEnabled, Consts.Defaults.SurgeEnabled);
            SurgeAbility = ReadSurgeAbility();
            SurgeUltimate = ReadBool(Consts.Keys.SurgeUltimate, Consts.Defaults.SurgeUltimate);
            SurgeCooldown = ReadPerkLevel(Consts.Keys.SurgeCooldown, Consts.Defaults.SurgeCooldown);
            SurgeDuration = ReadPerkLevel(Consts.Keys.SurgeDuration, Consts.Defaults.SurgeDuration);
            SurgeShortTerm = ReadBool(Consts.Keys.SurgeShortTerm, Consts.Defaults.SurgeShortTerm);
            SurgeLongTerm = ReadBool(Consts.Keys.SurgeLongTerm, Consts.Defaults.SurgeLongTerm);
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

        private static SurgeAbility ReadSurgeAbility()
        {
            string raw = ReadRaw(Consts.Keys.SurgeAbility);
            if (raw == null)
            {
                return DefaultSurgeAbility;
            }

            SurgeAbility ability;
            if (!TryParseSurgeAbility(raw, out ability))
            {
                Warn(Consts.Keys.SurgeAbility, raw, $"is not {string.Join(", ", Consts.Surge.AbilityConfigNames)}, using {Consts.Defaults.SurgeAbility}");
                return DefaultSurgeAbility;
            }
            return ability;
        }

        private static int ReadPerkLevel(string key, int fallback)
        {
            string raw = ReadRaw(key);
            if (raw == null)
            {
                return fallback;
            }

            int value;
            if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value))
            {
                Warn(key, raw, $"is not a whole number, using {fallback}");
                return fallback;
            }

            int clamped = ClampPerk(value);
            if (clamped != value)
            {
                Warn(key, raw, $"must be 0-{Consts.Surge.MaxPerkLevel}, using {clamped}");
            }
            return clamped;
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
