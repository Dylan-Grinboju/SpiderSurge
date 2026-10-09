using System.Collections.Generic;

namespace SpiderTeams
{
    public static class Consts
    {
        public const string HarmonyId = "com.SpiderTeams.Mod";
        public const string ManagerObjectName = "SpiderTeamsManager";
        public const int MinTeams = 2;
        public const int MaxTeams = 32;

        public static class Keys
        {
            public const string TeamsEnabled = "teams.enabled";
            public const string TeamCount = "teams.count";
            public const string FriendlyFire = "friendlyFire.mode";
            public const string RecolorSpiders = "visuals.recolorSpiders";
            public const string ShowMarker = "visuals.showMarker";
            public const string GroupTeammates = "spawns.groupTeammates";
            public const string CycleTeamGamepad = "input.cycleTeamGamepad";
            public const string CycleTeamKeyboard = "input.cycleTeamKeyboard";
            public const string CheckForUpdates = "updater.checkForUpdates";
            public const string SurgeEnabled = "surge.enabled";
            public const string SurgeAbility = "surge.ability";
            public const string SurgeUltimate = "surge.ultimate";
            public const string SurgeCooldown = "surge.abilityCooldown";
            public const string SurgeDuration = "surge.abilityDuration";
            public const string SurgeShortTerm = "surge.shortTermInvestment";
            public const string SurgeLongTerm = "surge.longTermInvestment";
        }

        public static class Defaults
        {
            public const bool TeamsEnabled = true;
            public const int TeamCount = 2;
            public const string FriendlyFire = "push";
            public const bool RecolorSpiders = true;
            public const bool ShowMarker = false;
            public const bool GroupTeammates = true;
            public const string CycleTeamGamepad = "<Gamepad>/buttonNorth";
            public const string CycleTeamKeyboard = "<Keyboard>/t";
            public const bool CheckForUpdates = true;
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
                new KeyValuePair<string, object>(Keys.CycleTeamGamepad, CycleTeamGamepad),
                new KeyValuePair<string, object>(Keys.CycleTeamKeyboard, CycleTeamKeyboard),
                new KeyValuePair<string, object>(Keys.CheckForUpdates, CheckForUpdates),
                new KeyValuePair<string, object>(Keys.SurgeEnabled, SurgeEnabled),
                new KeyValuePair<string, object>(Keys.SurgeAbility, SurgeAbility),
                new KeyValuePair<string, object>(Keys.SurgeUltimate, SurgeUltimate),
                new KeyValuePair<string, object>(Keys.SurgeCooldown, SurgeCooldown),
                new KeyValuePair<string, object>(Keys.SurgeDuration, SurgeDuration),
                new KeyValuePair<string, object>(Keys.SurgeShortTerm, SurgeShortTerm),
                new KeyValuePair<string, object>(Keys.SurgeLongTerm, SurgeLongTerm),
            };

            // Silk copies defaults shallowly and edits nested maps in place, so every load needs fresh dictionaries.
            public static Dictionary<string, object> BuildNested()
            {
                var root = new Dictionary<string, object>();
                foreach (var pair in Flat)
                {
                    string[] parts = pair.Key.Split('.');
                    Dictionary<string, object> node = root;
                    for (int i = 0; i < parts.Length - 1; i++)
                    {
                        object next;
                        if (!node.TryGetValue(parts[i], out next) || !(next is Dictionary<string, object>))
                        {
                            next = new Dictionary<string, object>();
                            node[parts[i]] = next;
                        }
                        node = (Dictionary<string, object>)next;
                    }
                    node[parts[parts.Length - 1]] = pair.Value;
                }
                return root;
            }
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
            public static readonly string[] UltimateNames = { "Neural Backup", "Care Package", "Thermal Detonation", "Pocket Dimension^2" };

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
                "Adds a second storage slot (3x cooldown).",
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
            public const string ShortTermDescription = "Buffs the ability's duration and cooldown, but nerfs the ultimate's.";
            public const string LongTermDescription = "Buffs the ultimate's duration and cooldown, but nerfs the ability's.";
        }

        public static class Bindings
        {
            public const string None = "none";

            public static readonly string[] GamepadPaths = { "<Gamepad>/buttonNorth", "<Gamepad>/leftStickPress", "<Gamepad>/rightStickPress", "<Gamepad>/select", None };
            public static readonly string[] GamepadLabels = { "Y / Triangle", "L3", "R3", "Select / Back", "Off" };

            public static readonly string[] KeyboardPaths = { "<Keyboard>/t", "<Keyboard>/g", "<Keyboard>/r", "<Keyboard>/tab", None };
            public static readonly string[] KeyboardLabels = { "T", "G", "R", "Tab", "Off" };
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
            public const float AnnounceSpacing = 0.1f;
            public const float LobbyLabelInterval = 1f;
            public const float ExplosionMatchWindow = 2f;
            public const float ExplosionMatchDistance = 0.05f;
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
            public const float Width = 3f;
            public const float HeightOffset = 10f;
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

        public static class ModUpdater
        {
            public const string LatestVersionUrl = "https://raw.githubusercontent.com/Dylan-Grinboju/SpiderTeams/main/version.txt";
            public const string DownloadUrl = "https://github.com/Dylan-Grinboju/SpiderTeams/releases/tag/v{0}";
            public const string ReleasesUrl = "https://github.com/Dylan-Grinboju/SpiderTeams/releases";
            public const int StartDelayMs = 15000;
            public const int RequestTimeoutMs = 5000;
        }
    }
}
