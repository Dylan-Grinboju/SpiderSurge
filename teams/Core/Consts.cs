using System.Collections.Generic;

namespace SpiderTeams
{
    public static class Consts
    {
        public const string ManagerObjectName = "SpiderTeamsManager";
        public const int MinTeams = 2;
        public const int MaxTeams = 32;

        public static class Keys
        {
            public const string TeamsEnabled = "teams.enabled";
            public const string TeamCount = "teams.count";
            public const string FriendlyFire = "teams.friendlyFire";
            public const string RecolorSpiders = "teams.recolorSpiders";
            public const string ShowMarker = "teams.showMarker";
            public const string GroupTeammates = "teams.groupTeammates";
            public const string SurgeEnabled = "teams.surge.enabled";
            public const string SurgeAbility = "teams.surge.ability";
            public const string SurgeUltimate = "teams.surge.ultimate";
            public const string SurgeCooldown = "teams.surge.abilityCooldown";
            public const string SurgeDuration = "teams.surge.abilityDuration";
            public const string SurgeShortTerm = "teams.surge.shortTermInvestment";
            public const string SurgeLongTerm = "teams.surge.longTermInvestment";
        }

        public static class Defaults
        {
            public const bool TeamsEnabled = false;
            public const int TeamCount = 2;
            public const string FriendlyFire = "push";
            public const bool RecolorSpiders = true;
            public const bool ShowMarker = false;
            public const bool GroupTeammates = true;
            public const bool SurgeEnabled = false;
            public const string SurgeAbility = "pulse";
            public const bool SurgeUltimate = false;
            public const int SurgeCooldown = 0;
            public const int SurgeDuration = 0;
            public const bool SurgeShortTerm = false;
            public const bool SurgeLongTerm = false;

            public static readonly KeyValuePair<string, object>[] Flat =
            {
                new KeyValuePair<string, object>(Keys.TeamsEnabled, TeamsEnabled),
                new KeyValuePair<string, object>(Keys.TeamCount, TeamCount),
                new KeyValuePair<string, object>(Keys.FriendlyFire, FriendlyFire),
                new KeyValuePair<string, object>(Keys.RecolorSpiders, RecolorSpiders),
                new KeyValuePair<string, object>(Keys.ShowMarker, ShowMarker),
                new KeyValuePair<string, object>(Keys.GroupTeammates, GroupTeammates),
                new KeyValuePair<string, object>(Keys.SurgeEnabled, SurgeEnabled),
                new KeyValuePair<string, object>(Keys.SurgeAbility, SurgeAbility),
                new KeyValuePair<string, object>(Keys.SurgeUltimate, SurgeUltimate),
                new KeyValuePair<string, object>(Keys.SurgeCooldown, SurgeCooldown),
                new KeyValuePair<string, object>(Keys.SurgeDuration, SurgeDuration),
                new KeyValuePair<string, object>(Keys.SurgeShortTerm, SurgeShortTerm),
                new KeyValuePair<string, object>(Keys.SurgeLongTerm, SurgeLongTerm),
            };
        }

        public static class Surge
        {
            public const int MaxPerkLevel = 2;
            public const float AnnounceDelay = 2.5f;
            public const string Color = "#00FFFF";

            // Index order matches SurgeAbility.
            public static readonly string[] AbilityConfigNames = { "immune", "ammo", "pulse", "storage", "random" };
            public static readonly string[] AbilityPerks = { "immune", "ammo", "pulse", "storage" };
            public static readonly string[] UltimatePerks = { "immuneUltimate", "ammoUltimate", "pulseUltimate", "storageUltimate" };
            public static readonly string[] AbilityNames = { "Null Field", "Bottomless Clip", "Kinetic Pulse", "Pocket Dimension", "Random" };
            public static readonly string[] UltimateNames = { "Neural Backup", "Care Package", "Thermal Detonation", "Black Hole" };

            public static readonly string[] AbilityDescriptions =
            {
                "Become immune to all damage for a short time.",
                "Shooting uses no ammo for a short time.",
                "Release a pulse that knocks back everyone nearby.",
                "Store your weapon and swap it back later.",
                "A new ability every round, the same for everyone.",
            };

            public static readonly string[] UltimateDescriptions =
            {
                "After a cast time, revive a dead teammate next to you. With nobody to revive it summons friendly wasps, which only chase AI enemies.",
                "Spawns weapons at half of the map's weapon spawners, or all of them with Ability Duration level 2.",
                "The pulse kills anyone close to you; teammates follow the friendly fire rule.",
                "For 3 seconds, pulls the spiders around you toward you, and any that reaches your inner circle goes into the pocket dimension, forever. Teammates are pulled unless friendly fire is ignored, and swallowed only with it set to damage.",
            };

            public const string CooldownPerk = "abilityCooldown";
            public const string DurationPerk = "abilityDuration";
            public const string ShortTermPerk = "shortTermInvestment";
            public const string LongTermPerk = "longTermInvestment";
            public const string CooldownName = "Ability Cooldown";
            public const string ShortTermName = "Short Term Investment";
            public const string LongTermName = "Long Term Investment";

            // The Duration perk is renamed for the abilities where it changes something other than time.
            public static readonly string[] DurationNames = { "Ability Duration", "Ability Duration", "Bigger Pulse", "Faster Retrieval", "Ability Duration" };

            public const string CooldownDescription = "Level 1 shortens the ability's cooldown, level 2 also the ultimate's.";
            public const string DurationDescription = "Level 1 strengthens the ability, level 2 also the ultimate.";
            public const string ShortTermDescription = "Your ability becomes stronger and recharges sooner, but your ultimate becomes weaker and recharges more slowly.";
            public const string LongTermDescription = "Your ultimate becomes stronger and recharges sooner, but your ability becomes weaker and recharges more slowly.";
        }

        public static class Timing
        {
            public const float RoundEndDelay = 2.5f;
            public const float NextRoundDelay = 2f;
            public const float WinScreenDelay = 4f;
            public const float RecolorInterval = 0.5f;
            public const float RecolorSendCooldown = 1f;
            public const float MarkerSyncInterval = 0.25f;
            public const float PickAnnounceDebounce = 0.6f;
            public const float PickAnnounceMaxWait = 2f;
            public const float AnnounceSpacing = 0.1f;
            public const float LobbyLabelInterval = 1f;
            public const float ExplosionMatchWindow = 2f;
            public const float ExplosionMatchDistance = 0.05f;
            public const float FriendlyPushGap = 0.25f;
            public const float FriendlyPushMemory = 2f;
        }

        public static class Palette
        {
            public static readonly string[] NamedColors = { "#E63946", "#3A86FF", "#2DC653", "#FFD60A", "#9B5DE5", "#FF8C1A", "#00D1D1", "#FF5FA2" };
            public static readonly string[] NamedTeams = { "Red", "Blue", "Green", "Yellow", "Purple", "Orange", "Cyan", "Pink" };
            public const float GoldenRatio = 0.618033988749895f;
            public const float GeneratedHueStart = 0.11f;
            public const float ShadeStep = 0.14f;
            public const float MaxShade = 0.42f;
        }

        public static class Marker
        {
            public const int TextureSize = 64;
            public const float PixelsPerUnit = 100f;
            public const int OutlinePixels = 5;
            public const float Width = 5f;
            public const float HeightOffset = 13f;
            public const string SortingLayer = "Foreground";
            public const int SortingOrder = 32765;
        }

        public static class LobbyLabel
        {
            public const string Path = "Level/VersusStartPlatform/Text/Versus Mode Text/ModeText";
            public const string PlatformPath = "Level/VersusStartPlatform";
            public const string Term = "Versus/versus_battle";
            public const string TeamText = "TEAM VERSUS";
        }

        public static class Formatting
        {
            public const string Gold = "#FFD700";
            public const string Green = "#00FF00";
            public const string Red = "#FF5555";
            public const string Gray = "#AAAAAA";
            public const string Orange = "#FFAA66";
        }

    }
}
