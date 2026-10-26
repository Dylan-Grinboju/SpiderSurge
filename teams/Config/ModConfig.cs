using System;
using System.Collections.Generic;
using SpiderSurge.Common;
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
        private static readonly HashSet<string> _warned = new HashSet<string>();

        public static event Action Changed;

        public static bool TeamsEnabled { get; private set; } = Consts.Defaults.TeamsEnabled;
        public static int TeamCount { get; private set; } = Consts.Defaults.TeamCount;
        public static FriendlyFireMode FriendlyFire { get; private set; } = FriendlyFireMode.Push;
        public static bool RecolorSpiders { get; private set; } = Consts.Defaults.RecolorSpiders;
        public static bool ShowMarker { get; private set; } = Consts.Defaults.ShowMarker;
        public static bool GroupTeammates { get; private set; } = Consts.Defaults.GroupTeammates;
        public static string CycleTeamGamepad => Controls.TeamController;
        public static string CycleTeamKeyboard => Controls.TeamKeyboard;
        public static bool SurgeEnabled { get; private set; } = Consts.Defaults.SurgeEnabled;
        public static SurgeAbility SurgeAbility { get; private set; } = DefaultSurgeAbility;
        public static bool SurgeUltimate { get; private set; } = Consts.Defaults.SurgeUltimate;
        public static int SurgeCooldown { get; private set; } = Consts.Defaults.SurgeCooldown;
        public static int SurgeDuration { get; private set; } = Consts.Defaults.SurgeDuration;
        public static bool SurgeShortTerm { get; private set; } = Consts.Defaults.SurgeShortTerm;
        public static bool SurgeLongTerm { get; private set; } = Consts.Defaults.SurgeLongTerm;

        public static KeyValuePair<string, object>[] Defaults => Consts.Defaults.Flat;

        public static void Initialize()
        {
            Settings.Changed += OnSettingsChanged;
            Refresh();
        }

        public static void Shutdown()
        {
            Settings.Changed -= OnSettingsChanged;
        }

        public static void ResetToDefaults()
        {
            Settings.Reset("teams");
            Logger.LogInfo("Teams settings reset to defaults");
        }

        private static void OnSettingsChanged(string key)
        {
            if (key == null || key == "teams")
            {
                _warned.Clear();
            }
            if (key == null || key == "teams" || key.StartsWith("teams.", StringComparison.Ordinal))
            {
                Refresh();
            }
        }

        public static void SetTeamsEnabled(bool value) => Set(Consts.Keys.TeamsEnabled, value);
        public static void SetTeamCount(int value) => Set(Consts.Keys.TeamCount, ClampTeams(value));
        public static void SetFriendlyFire(FriendlyFireMode value) => Set(Consts.Keys.FriendlyFire, FriendlyFireName(value));
        public static void SetRecolorSpiders(bool value) => Set(Consts.Keys.RecolorSpiders, value);
        public static void SetShowMarker(bool value) => Set(Consts.Keys.ShowMarker, value);
        public static void SetGroupTeammates(bool value) => Set(Consts.Keys.GroupTeammates, value);
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
            Settings.Set(key, value);
        }

        private static void Refresh()
        {
            TeamsEnabled = Settings.GetBool(Consts.Keys.TeamsEnabled);
            TeamCount = ReadTeamCount();
            FriendlyFire = ReadFriendlyFire();
            RecolorSpiders = Settings.GetBool(Consts.Keys.RecolorSpiders);
            ShowMarker = Settings.GetBool(Consts.Keys.ShowMarker);
            GroupTeammates = Settings.GetBool(Consts.Keys.GroupTeammates);
            SurgeEnabled = Settings.GetBool(Consts.Keys.SurgeEnabled);
            SurgeAbility = ReadSurgeAbility();
            SurgeUltimate = Settings.GetBool(Consts.Keys.SurgeUltimate);
            SurgeCooldown = ReadPerkLevel(Consts.Keys.SurgeCooldown);
            SurgeDuration = ReadPerkLevel(Consts.Keys.SurgeDuration);
            SurgeShortTerm = Settings.GetBool(Consts.Keys.SurgeShortTerm);
            SurgeLongTerm = Settings.GetBool(Consts.Keys.SurgeLongTerm);
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
                    Logger.LogError($"Teams settings listener failed: {ex}");
                }
            }
        }

        private static int ReadTeamCount()
        {
            int value = Settings.GetInt(Consts.Keys.TeamCount);
            int clamped = ClampTeams(value);
            if (clamped != value)
            {
                Warn(Consts.Keys.TeamCount, value.ToString(), $"must be {Consts.MinTeams}-{Consts.MaxTeams}, using {clamped}");
            }
            return clamped;
        }

        private static FriendlyFireMode ReadFriendlyFire()
        {
            string raw = Settings.GetString(Consts.Keys.FriendlyFire);
            if (!TryParseFriendlyFire(raw, out FriendlyFireMode mode))
            {
                Warn(Consts.Keys.FriendlyFire, raw, "is not push, ignore or damage, using push");
            }
            return mode;
        }

        private static SurgeAbility ReadSurgeAbility()
        {
            string raw = Settings.GetString(Consts.Keys.SurgeAbility);
            if (TryParseSurgeAbility(raw, out SurgeAbility ability))
            {
                return ability;
            }
            Warn(Consts.Keys.SurgeAbility, raw, $"is not {string.Join(", ", Consts.Surge.AbilityConfigNames)}, using {Consts.Defaults.SurgeAbility}");
            return DefaultSurgeAbility;
        }

        private static int ReadPerkLevel(string key)
        {
            int value = Settings.GetInt(key);
            int clamped = ClampPerk(value);
            if (clamped != value)
            {
                Warn(key, value.ToString(), $"must be 0-{Consts.Surge.MaxPerkLevel}, using {clamped}");
            }
            return clamped;
        }

        private static void Warn(string key, string value, string problem)
        {
            if (_warned.Add(key + "=" + value))
            {
                Logger.LogWarning($"Teams setting {key} '{value}' {problem}");
            }
        }
    }
}
