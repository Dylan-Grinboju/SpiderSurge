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
            public const string ColorGreen = "#00FF00";
            public const string ColorRed = "#FF0000";

            public const string TextLuckyUpgrade = "<color=" + ColorGold + ">Lucky Upgrade</color>";
            public const string TextSwapAbility = "<color=" + ColorGreen + ">Swap Ability!</color>";
            public const string TextSynergized = "<color=" + ColorCyan + ">Synergized</color>";
        }

        public static class Values
        {
            public static class Shield
            {
                public const float AbilityBaseCooldown = 20f;
                public const float AbilityCooldownReductionPerLevel = 5f;
                public const float AbilityBaseDuration = 3f;
                public const float AbilityDurationIncreasePerLevel = 1f;
                public const float UltimateBaseCooldown = 40f;
                public const float UltimateCooldownReductionPerLevel = 10f;
                public const float UltimateBaseDuration = 3f;
                public const float UltimateDurationIncreasePerLevel = 1f;
            }

            public static class InfiniteAmmo
            {
                public const float AbilityBaseCooldown = 30f;
                public const float AbilityCooldownReductionPerLevel = 7.5f;
                public const float AbilityBaseDuration = 7.5f;
                public const float AbilityDurationIncreasePerLevel = 2.5f;
                public const float UltimateBaseCooldown = 60f;
                public const float UltimateCooldownReductionPerLevel = 15f;
                public const float UltimateBaseDuration = 10f;
                public const float UltimateDurationIncreasePerLevel = 3f;
                public const float CheckInterval = 0.5f;
            }

            public static class Explosion
            {
                public const float AbilityBaseCooldown = 20f;
                public const float AbilityBaseDuration = 0f;
                public const float UltimateBaseCooldown = 40f;
                public const float UltimateBaseDuration = 0f;
                public const float AbilityCooldownReductionPerLevel = 5f;
                public const float UltimateCooldownReductionPerLevel = 10f;

                //duration perk
                public const float AbilityBaseKnockbackRadius = 90f;
                public const float AbilityKnockbackRadiusIncreasePerLevel = 30f;
                public const float UltimateBaseKnockbackRadius = 120f;
                public const float UltimateKnockbackRadiusIncreasePerLevel = 400f;

                //Bigger boom perk
                public const float AbilityBaseKnockbackStrength = 900f;
                public const float AbilityKnockbackStrengthIncreasePerLevel = 200f;
                public const float UltimateBaseKnockbackStrength = 1200f;
                public const float UltimateKnockbackStrengthIncreasePerLevel = 300f;

                //too cool perk
                public const float UltimateBaseDeathRadius = 70f;
                public const float UltimateDeathRadiusIncreasePerLevel = 20f;
                public const float CameraShakeDuration = 5f;
            }

            public static class Storage
            {
                public const float AbilityBaseCooldown = 20f;
                public const float AbilityCooldownReductionPerLevel = 5f;
                public const float AbilityBaseDuration = 3f;
                public const float AbilityDurationReductionPerLevel = 1f;
                public const float UltimateBaseCooldown = 20f;
                public const float UltimateCooldownReductionPerLevel = 5f;
                public const float UltimateBaseDuration = 4f;
                public const float UltimateDurationReductionPerLevel = 0.75f;
                public const float SpawnDistance = 50f;
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
                public const float TwinWhispShotMargin = 5f;
            }

            public static class Colors
            {
                public static readonly UnityEngine.Color MissileWhispColor = new UnityEngine.Color(0.2f, 0f, 0f, 1f); // Red
                public static readonly UnityEngine.Color TwinWhispColor = new UnityEngine.Color(1f, 0.75f, 0f, 1f); // Orange
            }

            public static class Waves
            {
                public const int UltUpgradeWave = 30;
                public const int UltSwapWave = 60;
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
                    { "TwinWhisp", new EnemySpawnConfig(4f, 24, 44) },
                    { "Wasp Shielded", new EnemySpawnConfig(4f, 26, 0) },
                    { "Hornet_Shaman Variant", new EnemySpawnConfig(6f, 28, 0) },
                    { "TwinBladePowerMeleeWhisp", new EnemySpawnConfig(6f, 30, 0) },
                    { "PowerWasp Variant Shield", new EnemySpawnConfig(6f, 32, 0) },
                    { "ShieldedTwinWhisp", new EnemySpawnConfig(6f, 34, 0) },
                    { "Hornet Variant", new EnemySpawnConfig(6f, 36, 0) },
                    { "MissileWhisp", new EnemySpawnConfig(7f, 38, 0) },
                    { "Shielded Hornet Variant", new EnemySpawnConfig(8f, 40, 0) },
                    { "ShieldedMissileWhisp", new EnemySpawnConfig(9f, 42, 0) },
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
                public const string KeyboardC = "<Keyboard>/c";
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
                public const string UltimateDisplayName = "<color=" + Formatting.ColorRed + ">Ultimate</color>";
                public const string UltimateDefaultDescription = "<color=" + Formatting.ColorRed + ">Ultimate version of the ability.</color>";
            }
        }

        public static class Descriptions
        {
            private static readonly Dictionary<string, string> displayNames = new Dictionary<string, string>
            {
                [PerkNames.ShieldAbility] = "Parry",
                [PerkNames.InfiniteAmmoAbility] = "Keep Shooting",
                [PerkNames.ExplosionAbility] = "The Force",
                [PerkNames.InterdimensionalStorageAbility] = "Interdimensional Storage",
                [PerkNames.AbilityCooldown] = "Ability Cooldown",
                [PerkNames.AbilityDuration] = "Ability Duration",
                [PerkNames.ShortTermInvestment] = "Short Term Investment",
                [PerkNames.LongTermInvestment] = "Long Term Investment",
                [PerkNames.PerkLuck] = "Lucky",
                // Ultimate perks - dynamic names based on which ability is active
                [PerkNames.ShieldAbilityUltimate] = "God Mode",
                [PerkNames.InfiniteAmmoAbilityUltimate] = "Care Package",
                [PerkNames.ExplosionAbilityUltimate] = "Unstoppable Force",
                [PerkNames.InterdimensionalStorageAbilityUltimate] = "More Dimensions"
            };

            private static readonly Dictionary<string, string> descriptions = new Dictionary<string, string>
            {
                [PerkNames.ShieldAbility] = "Unlocks the shield ability",
                [PerkNames.InfiniteAmmoAbility] = "Unlocks the infinite ammo ability",
                [PerkNames.ExplosionAbility] = "Unlocks the knockback ability",
                [PerkNames.InterdimensionalStorageAbility] = "Unlocks the storage ability",
                [PerkNames.AbilityCooldown] = "Reduces ability cooldown",
                [PerkNames.AbilityDuration] = "Increases ability duration",
                [PerkNames.ShortTermInvestment] = "Buffs ability duration and cooldown, but nerfs ultimate duration and cooldown",
                [PerkNames.LongTermInvestment] = "Buffs ultimate duration and cooldown, but nerfs ability duration and cooldown",
                [PerkNames.PerkLuck] = "Chance to see level 2 perks even without level 1",
                [PerkNames.ShieldAbilityUltimate] = "<color=" + Formatting.ColorRed + ">Grants complete damage immunity</color>",
                [PerkNames.InfiniteAmmoAbilityUltimate] = "<color=" + Formatting.ColorRed + ">Spawns weapons at all spawn points</color>",
                [PerkNames.ExplosionAbilityUltimate] = "<color=" + Formatting.ColorRed + ">Knockback deals lethal damage</color>",
                [PerkNames.InterdimensionalStorageAbilityUltimate] = "<color=" + Formatting.ColorRed + ">Adds a second storage slot</color>"
            };

            private static readonly Dictionary<string, string> upgradeDescriptions = new Dictionary<string, string>
            {
                [PerkNames.ShieldAbility] = "",
                [PerkNames.InfiniteAmmoAbility] = "",
                [PerkNames.ExplosionAbility] = "",
                [PerkNames.InterdimensionalStorageAbility] = "",
                [PerkNames.AbilityCooldown] = "Reduces ultimate cooldown. (Requires ultimate unlocked)",
                [PerkNames.AbilityDuration] = "Increases ultimate duration. (Requires ultimate unlocked)",
                [PerkNames.ShortTermInvestment] = "",
                [PerkNames.LongTermInvestment] = "",
                [PerkNames.PerkLuck] = "Increases chance to see level 2 perks.",
                [PerkNames.ShieldAbilityUltimate] = "",
                [PerkNames.InfiniteAmmoAbilityUltimate] = "",
                [PerkNames.ExplosionAbilityUltimate] = "",
                [PerkNames.InterdimensionalStorageAbilityUltimate] = ""
            };

            // Custom display names for Duration perk based on active ability
            private const string DURATION_NAME_WITH_EXPLOSION = "Bigger Explosion";
            private const string DURATION_NAME_WITH_STORAGE = "Faster Retrieval";

            // Custom descriptions for Duration perk based on active ability
            private const string DURATION_DESC_WITH_EXPLOSION = "Increases ability explosion size";
            private const string DURATION_UPGRADE_DESC_WITH_EXPLOSION = "Increases ultimate explosion size";
            private const string DURATION_DESC_WITH_STORAGE = "Faster ability retrieval from the void";
            private const string DURATION_UPGRADE_DESC_WITH_STORAGE = "Faster ultimate retrieval from the void";

            public static string GetDisplayName(string name, PerksManager perksManager = null)
            {
                if (perksManager != null && name == PerkNames.AbilityDuration)
                {
                    if (perksManager.GetPerkLevel(PerkNames.ExplosionAbility) > 0)
                    {
                        return DURATION_NAME_WITH_EXPLOSION;
                    }
                    if (perksManager.GetPerkLevel(PerkNames.InterdimensionalStorageAbility) > 0)
                    {
                        return DURATION_NAME_WITH_STORAGE;
                    }
                }
                return displayNames.ContainsKey(name) ? displayNames[name] : name;
            }

            public static string GetDescription(string name, PerksManager perksManager)
            {
                if (perksManager == null) return descriptions.ContainsKey(name) ? descriptions[name] : "";

                if (name == PerkNames.AbilityDuration && perksManager.GetPerkLevel(PerkNames.ExplosionAbility) > 0)
                {
                    return DURATION_DESC_WITH_EXPLOSION;
                }
                if (name == PerkNames.AbilityDuration && perksManager.GetPerkLevel(PerkNames.InterdimensionalStorageAbility) > 0)
                {
                    return DURATION_DESC_WITH_STORAGE;
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
                if (name == PerkNames.AbilityDuration && perksManager.GetPerkLevel(PerkNames.InterdimensionalStorageAbility) > 0)
                {
                    return DURATION_UPGRADE_DESC_WITH_STORAGE;
                }
                return upgradeDescriptions.ContainsKey(name) ? upgradeDescriptions[name] : "";
            }
        }
    }
}
