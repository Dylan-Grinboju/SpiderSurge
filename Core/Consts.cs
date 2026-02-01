using System.Collections.Generic;

namespace SpiderSurge
{
    public static class Consts
    {
        public static class PerkNames
        {
            public const string ShieldAbility = "shieldAbility";
            public const string InfiniteAmmoAbility = "infiniteAmmoAbility";
            public const string ExplosionAbility = "explosionAbility";
            public const string InterdimensionalStorageAbility = "interdimensionalStorageAbility";

            public const string ShieldAbilityUltimate = "shieldAbilityUltimate";
            public const string InfiniteAmmoAbilityUltimate = "infiniteAmmoAbilityUltimate";
            public const string ExplosionAbilityUltimate = "explosionAbilityUltimate";
            public const string InterdimensionalStorageAbilityUltimate = "interdimensionalStorageAbilityUltimate";

            public const string AbilityCooldown = "abilityCooldown";
            public const string AbilityDuration = "abilityDuration";
            public const string ShortTermInvestment = "shortTermInvestment";
            public const string LongTermInvestment = "longTermInvestment";
            public const string PerkLuck = "perkLuck";
        }

        public static class ModifierNames
        {
            public const string TooCool = "tooCool";
            public const string BiggerBoom = "biggerBoom";
            public const string MoreBoom = "moreBoom";
            public const string MoreGuns = "moreGuns";
            public const string MoreParticles = "moreParticles";
            public const string Efficiency = "efficiency";
            public const string StartShields = "startShields";
            public const string PositiveEncouragement = "positiveencouragement";
            public const string SafetyNet = "safetynet";
        }

        public static class Formatting
        {
            public const string ColorGold = "#FFD700";
            public const string ColorCyan = "#00FFFF";

            public const string TextLuckyUpgrade = "<color=" + ColorGold + ">Lucky Upgrade</color>";
            public const string TextSwapAbility = "<color=" + ColorGold + ">Swap Ability!</color>";
            public const string TextSynergized = "<color=" + ColorCyan + ">Synergized</color>";
        }

        public static class Values
        {
            public static class Shield
            {
                public const float BaseCooldown = 20f;
                public const float BaseDuration = 2f;
                public const float CooldownReductionPerLevel = 4f;
                public const float DurationIncreasePerLevel = 1f;
                public const float UltimateCooldownMultiplier = 2f;
            }

            public static class InfiniteAmmo
            {
                public const float BaseCooldown = 35f;
                public const float BaseDuration = 7.5f;
                public const float CooldownReductionPerLevel = 5f;
                public const float DurationIncreasePerLevel = 2.5f;
                public const float CheckInterval = 0.5f;
                public const float UltimateCooldownMultiplier = 2f;
            }

            public static class Explosion
            {
                public const float BaseCooldown = 20f;
                public const float BaseDuration = 0f;
                public const float CooldownReductionPerLevel = 4f;
                public const float UltimateCooldownMultiplier = 2f;

                public const float BaseKnockbackRadius = 80f;
                public const float BaseKnockbackStrength = 50f;
                public const float BaseDeathRadius = 42f;
                public const float SizeScalePerLevel = 0.33f;
                public const float SynergyDeathZonePerLevel = 0.33f;
                public const float SynergyKnockbackPerLevel = 0.33f;

                public const float CameraShakeDuration = 5f;

                public const float ForceMultiplierOutsideZone = 4f;
                public const float ForceMultiplierInsideZone = 6f;
            }

            public static class Storage
            {
                public const float BaseCooldown = 20f;
                public const float CooldownReductionPerLevel = 4f;
                public const float BaseDuration = 3f;
                public const float DurationReductionPerLevel = 0.5f;
                public const float UltimateCooldownMultiplier = 1.5f;
            }

            public static class Luck
            {
                public const float Level1Chance = 0.15f;
                public const float Level2Chance = 0.3f;
            }

            public static class Enemies
            {
                public const float SpeedMultiplier = 1.1f;
                public const float SpawnCountMultiplier = 2f;
                public const float MissileWhispShotForce = 40f;
            }

            public static class Colors
            {
                public static readonly UnityEngine.Color MissileWhispColor = new UnityEngine.Color(1f, 0f, 0f, 1f); // Red
            }

            //ORIGINAL SPAWN CONFIGS, DO NOT DELETE
            // Wasp, Cost: 1.5, MinWave: 0, MaxWave: 22
            // Roller, Cost: 1.5, MinWave: 2, MaxWave: 26
            // Whisp, Cost: 2, MinWave: 4, MaxWave: 30
            // MeleeWhisp, Cost: 2, MinWave: 6, MaxWave: 34
            // Khepri, Cost: 2.5, MinWave: 8, MaxWave: 36
            // ExplodingRoller, Cost: 3, MinWave: 10, MaxWave: 0
            // PowerWhisp Variant, Cost: 3, MinWave: 12, MaxWave: 0
            // PowerWasp Variant, Cost: 3, MinWave: 14, MaxWave: 0
            // PowerRoller Variant, Cost: 4, MinWave: 16, MaxWave: 0
            // PowerKhepri Variant, Cost: 4, MinWave: 18, MaxWave: 0
            // PowerMeleeWhisp Variant, Cost: 4, MinWave: 20, MaxWave: 0
            // Wasp Shielded, Cost: 4, MinWave: 22, MaxWave: 0
            // Hornet_Shaman Variant, Cost: 6, MinWave: 24, MaxWave: 0
            // PowerWasp Variant Shield, Cost: 6, MinWave: 26, MaxWave: 0
            // Hornet Variant, Cost: 6, MinWave: 28, MaxWave: 0
            // Shielded Hornet Variant, Cost: 8, MinWave: 32, MaxWave: 0


            public static readonly Dictionary<string, EnemySpawnConfig> CustomEnemyStats = new Dictionary<string, EnemySpawnConfig>
                {
                    { "Wasp", new EnemySpawnConfig(1.5f, 0, 22) },
                    { "Roller", new EnemySpawnConfig(1.5f, 2, 26) },
                    { "Whisp", new EnemySpawnConfig(2f, 4, 30) },
                    { "MeleeWhisp", new EnemySpawnConfig(2f, 6, 20) },
                    { "Khepri", new EnemySpawnConfig(2.5f, 8, 36) },
                    { "ExplodingRoller", new EnemySpawnConfig(3f, 10, 0) },
                    { "PowerWhisp Variant", new EnemySpawnConfig(3f, 12, 0) },
                    { "PowerWasp Variant", new EnemySpawnConfig(3f, 14, 0) },
                    { "TwinBladeMeleeWhisp", new EnemySpawnConfig(4f, 16, 30) },
                    { "PowerRoller Variant", new EnemySpawnConfig(4f, 18, 0) },
                    { "PowerKhepri Variant", new EnemySpawnConfig(4f, 20, 0) },
                    { "PowerMeleeWhisp Variant", new EnemySpawnConfig(4f, 22, 0) },
                    { "Wasp Shielded", new EnemySpawnConfig(4f, 24, 0) },
                    { "Hornet_Shaman Variant", new EnemySpawnConfig(6f, 26, 0) },
                    { "TwinBladePowerMeleeWhisp", new EnemySpawnConfig(6f, 28, 0) },
                    { "PowerWasp Variant Shield", new EnemySpawnConfig(6f, 30, 0) },
                    { "Hornet Variant", new EnemySpawnConfig(6f, 32, 0) },
                    { "MissileWhisp", new EnemySpawnConfig(7f, 34, 0) },
                    { "Shielded Hornet Variant", new EnemySpawnConfig(8f, 36, 0) },
                };

            public struct EnemySpawnConfig
            {
                public float Cost;
                public int MinWave;
                public int MaxWave;

                public EnemySpawnConfig(float cost, int minWave, int maxWave)
                {
                    Cost = cost;
                    MinWave = minWave;
                    MaxWave = maxWave;
                }
            }

            public static class Inputs
            {
                public const string KeyboardQ = "<keyboard>/q";
                public const string GamepadLeftShoulder = "<Gamepad>/leftshoulder";
                public const string KeyboardF = "<Keyboard>/f";
                public const string GamepadDpadUp = "<Gamepad>/dpad/up";
                public const string GamepadDpadDown = "<Gamepad>/dpad/down";
                public const string GamepadDpadLeft = "<Gamepad>/dpad/left";
                public const string GamepadDpadRight = "<Gamepad>/dpad/right";
                public const string GamepadLeftStickPress = "<Gamepad>/leftStickPress";
                public const string GamepadRightStickPress = "<Gamepad>/rightStickPress";
                public const float ComboWindow = 0.15f;
            }

            public static class UI
            {
                public const string UltimateDisplayName = "Ultimate";
                public const string UltimateDefaultDescription = "Ultimate version of the ability.";
            }
        }

        public static class Descriptions
        {
            private static readonly Dictionary<string, string> displayNames = new Dictionary<string, string>
            {
                [PerkNames.ShieldAbility] = "Shield Ability",
                [PerkNames.InfiniteAmmoAbility] = "Infinite Ammo",
                [PerkNames.ExplosionAbility] = "Explosion Ability",
                [PerkNames.AbilityCooldown] = "Ability Cooldown",
                [PerkNames.AbilityDuration] = "Ability Duration",
                [PerkNames.ShortTermInvestment] = "Short Term Investment",
                [PerkNames.LongTermInvestment] = "Long Term Investment",
                [PerkNames.PerkLuck] = "Perk Luck",
                // Ultimate perks - dynamic names based on which ability is active
                [PerkNames.ShieldAbilityUltimate] = "Shield Ultimate",
                [PerkNames.InfiniteAmmoAbilityUltimate] = "Weapon Arsenal Ultimate",
                [PerkNames.ExplosionAbilityUltimate] = "Explosion Ultimate",
                [PerkNames.InterdimensionalStorageAbility] = "Interdimensional Storage",
                [PerkNames.InterdimensionalStorageAbilityUltimate] = "Storage Ultimate"
            };

            private static readonly Dictionary<string, string> descriptions = new Dictionary<string, string>
            {
                [PerkNames.ShieldAbility] = "Unlocks the shield ability.",
                [PerkNames.InfiniteAmmoAbility] = "Unlocks the infinite ammo ability.",
                [PerkNames.ExplosionAbility] = "Unlocks the explosion ability.",
                [PerkNames.InterdimensionalStorageAbility] = "Unlocks the interdimensional storage ability.",
                [PerkNames.AbilityCooldown] = "Reduces ability cooldown.",
                [PerkNames.AbilityDuration] = "Increases ability duration.",
                [PerkNames.ShortTermInvestment] = "Increases ability duration by 2 levels, but increases cooldown by 1 level.",
                [PerkNames.LongTermInvestment] = "Decreases cooldown by 2 levels, but decreases ability duration by 1 level.",
                [PerkNames.PerkLuck] = "Chance to see level 2 perks even without level 1.",
                // Ultimate perks
                [PerkNames.ShieldAbilityUltimate] = "Grants complete damage immunity (3x cooldown).",
                [PerkNames.InfiniteAmmoAbilityUltimate] = "Spawns weapons at all spawn points (3x cooldown).",
                [PerkNames.ExplosionAbilityUltimate] = "Explosion deals lethal damage (3x cooldown).",
                [PerkNames.InterdimensionalStorageAbilityUltimate] = "Adds a second storage slot (3x cooldown)."
            };

            private static readonly Dictionary<string, string> upgradeDescriptions = new Dictionary<string, string>
            {
                [PerkNames.ShieldAbility] = "",
                [PerkNames.InfiniteAmmoAbility] = "",
                [PerkNames.ExplosionAbility] = "",
                [PerkNames.InterdimensionalStorageAbility] = "",
                [PerkNames.AbilityCooldown] = "Further reduces ability cooldown.",
                [PerkNames.AbilityDuration] = "Further increases ability duration.",
                [PerkNames.ShortTermInvestment] = "",
                [PerkNames.LongTermInvestment] = "",
                [PerkNames.PerkLuck] = "Increases chance to see level 2 perks.",
                // Ultimate perks don't have upgrade descriptions (max level 1)
                [PerkNames.ShieldAbilityUltimate] = "",
                [PerkNames.InfiniteAmmoAbilityUltimate] = "",
                [PerkNames.ExplosionAbilityUltimate] = "",
                [PerkNames.InterdimensionalStorageAbilityUltimate] = ""
            };

            // Descriptions when explosion ability is unlocked (duration also affects explosion size)
            private const string DURATION_DESC_WITH_EXPLOSION = "Increases explosion size.";
            private const string DURATION_UPGRADE_DESC_WITH_EXPLOSION = "Further increases explosion size.";

            public static string GetDisplayName(string name) => displayNames.ContainsKey(name) ? displayNames[name] : name;

            public static string GetDescription(string name, PerksManager perksManager)
            {
                if (perksManager == null) return descriptions.ContainsKey(name) ? descriptions[name] : "";

                if (name == PerkNames.AbilityDuration && perksManager.GetPerkLevel(PerkNames.ExplosionAbility) > 0)
                {
                    return DURATION_DESC_WITH_EXPLOSION;
                }
                return descriptions.ContainsKey(name) ? descriptions[name] : "";
            }

            public static string GetUpgradeDescription(string name, PerksManager perksManager)
            {
                if (perksManager == null) return upgradeDescriptions.ContainsKey(name) ? upgradeDescriptions[name] : "";

                if (name == PerkNames.AbilityDuration && perksManager.GetPerkLevel(PerkNames.ExplosionAbility) > 0)
                {
                    return DURATION_UPGRADE_DESC_WITH_EXPLOSION;
                }
                return upgradeDescriptions.ContainsKey(name) ? upgradeDescriptions[name] : "";
            }
        }
    }
}
