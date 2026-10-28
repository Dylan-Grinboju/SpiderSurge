using System.Collections.Generic;

namespace SpiderSurge
{
    public static class Consts
    {
        public static class PerkNames
        {
            public const string ImmuneAbility = "immune";
            public const string AmmoAbility = "ammo";
            public const string PulseAbility = "pulse";
            public const string StorageAbility = "storage";
            public const string FreezeAbility = "freeze";

            public const string ImmuneAbilityUltimate = "immuneUltimate";
            public const string AmmoAbilityUltimate = "ammoUltimate";
            public const string PulseAbilityUltimate = "pulseUltimate";
            public const string StorageAbilityUltimate = "storageUltimate";
            public const string FreezeAbilityUltimate = "freezeUltimate";

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
            public const string KnockbackSuspension = "knockbacksuspension";
            public const string Adrenaline = "adrenaline";
            public const string Blademaster = "blademaster";
            public const string CoolExplosions = "coolexplosions";
        }

        // Sound file names (without extension) - must match embedded .wav filenames
        public static class SoundNames
        {
            public const string LuckyUpgrade = "lucky_upgrade";
            public const string AbilityNotReady = "ability_not_ready";
            public const string AbilityReady = "ability_ready";
            public const string AmmoAbility = "ammo_ability";
            public const string PulseAbility = "pulse_ability";
            public const string PowerUp = "power_up";
            public const string ImmuneAbility = "immune_ability";
            public const string ImmuneUlt = "immune_ult";
            public const string StorageSend = "storage_send";
            public const string StorageRetrieve = "storage_retrieve";
            public const string FreezeAbility = "freeze_ability";
            public const string FreezeUlt = "freeze_ult";
            public const string Unfreeze = "unfreeze";
            public const string AbilityEnded = "ability_ended";
        }

        public static class SoundVolumes
        {
            public static float LuckyUpgrade = 1f;
            public static float AbilityNotReady = 0.5f;
            public static float AbilityReady = 1f;
            public static float AmmoAbility = 0.75f;
            public static float PulseAbility = 0.75f;
            public static float PowerUp = 1f;
            public static float ImmuneAbility = 0.9f;
            public static float ImmuneUlt = 0.9f;
            public static float StorageSend = 0.7f;
            public static float StorageRetrieve = 0.7f;
            public static float FreezeAbility = 0.8f;
            public static float FreezeUlt = 0.7f;
            public static float Unfreeze = 0.8f;
            public static float AbilityEnded = 0.7f;

            // Master volume multiplier
            public static float MasterVolume = 1f;
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

        public static class Telemetry
        {
            public const string RelayEndpointUrl = "https://spidersurge-telemetry-relay.dylanyair.workers.dev";
            public const string PendingDirectoryName = "TelemetryPending";
            public const string AnonymousIdFileName = "telemetry_anonymous_id.txt";
            public const int AnonymousIdBytes = 32;
            public const int RequestTimeoutMs = 5000;
            public const int MaxQueuedPayloads = 50;
            public const int MinSendIntervalMs = 2000;
            public const int DuplicatePayloadWindowMinutes = 10;
            public const int SchemaVersion = 5;
            public const int MaxPerkChoiceRecords = 60;
            public const int MaxWaveRecords = 150;
            public const int MaxGlobalPerks = 64;
            public const int MaxPlayerStats = 8;
            public const int MaxTitleRecords = 64;
            public const int MaxPayloadBytes = 32 * 1024;
        }

        public static class ModUpdater
        {
            public const int RequestTimeoutMs = 5000;
        }

        public static class Values
        {
            public static class Immune
            {
                public const float AbilityBaseCooldown = 12f;
                public const float AbilityCooldownReductionPerLevel = 4f;
                public const float AbilityBaseDuration = 3f;
                public const float AbilityDurationIncreasePerLevel = 1.5f;
                public const float UltimateBaseCooldown = 75f;
                public const float UltimateCooldownReductionPerLevel = 15f;
                public const float UltimateBaseDuration = 10f;
                public const float UltimateDurationIncreasePerLevel = 3f;
                public const float UltimateShieldSynergyChance = 0.5f;
                public const int UltimateFriendlyWaspSpawnCount = 3;
                public const int UltimateFriendlyWaspShieldBonusCount = 1;
                public const float SaveConfirmSeconds = 1.5f;
            }

            public static class Ammo
            {
                public const float AbilityBaseCooldown = 12f;
                public const float AbilityCooldownReductionPerLevel = 4f;
                public const float AbilityBaseDuration = 5f;
                public const float AbilityDurationIncreasePerLevel = 2.5f;
                public const float UltimateBaseCooldown = 30f;
                public const float UltimateCooldownReductionPerLevel = 10f;
                public const float UltimateBaseDuration = 0f; // Instant ultimate, no duration coroutine
                public const float CheckInterval = 0.5f;
            }

            public static class Pulse
            {
                public const float AbilityBaseCooldown = 12f;
                public const float AbilityCooldownReductionPerLevel = 4f;
                public const float AbilityBaseDuration = 0f;
                public const float UltimateBaseCooldown = 20f;
                public const float UltimateCooldownReductionPerLevel = 5f;
                public const float UltimateBaseDuration = 0f;

                //duration perk
                public const float AbilityBaseKnockbackRadius = 100f;
                public const float AbilityKnockbackRadiusIncreasePerLevel = 30f;
                public const float UltimateBaseKnockbackRadius = 150f;
                public const float UltimateKnockbackRadiusIncreasePerLevel = 50f;

                //Bigger boom perk
                public const float AbilityBaseKnockbackStrength = 675f;
                public const float AbilityKnockbackStrengthIncreasePerLevel = 150f;
                public const float UltimateBaseKnockbackStrength = 900f;
                public const float UltimateKnockbackStrengthIncreasePerLevel = 225f;

                // Enemies are launched at a set speed, past the game's 150 speed cap for a moment
                public const float AbilityBaseEnemyLaunchSpeed = 300f;
                public const float AbilityEnemyLaunchSpeedIncreasePerLevel = 75f;
                public const float UltimateBaseEnemyLaunchSpeed = 375f;
                public const float UltimateEnemyLaunchSpeedIncreasePerLevel = 93.75f;
                public const float EnemyUncappedDuration = 0.35f;
                public const float ZeroGravityStrengthMultiplier = 0.65f;
                public const float ZeroGravityUncappedDuration = 0.2f;
                public const float EnemyStunDuration = 0.8f;

                //too cool perk
                public const float UltimateBaseDeathRadius = 100f;
                public const float UltimateDeathRadiusIncreasePerLevel = 30f;
                public const float CameraShakeDuration = 5f;
                public const float ReducedCameraEffectScale = 0.25f;

                // Visuals
                public static readonly UnityEngine.Color RingColor = new UnityEngine.Color(0f, 0.7f, 1f, 0.8f); // Light blue
                public const float RingSpeed = 500f;
            }

            public static class Storage
            {
                public const float AbilityBaseCooldown = 12f;
                public const float AbilityCooldownReductionPerLevel = 4f;
                public const float AbilityBaseDuration = 2.5f;
                public const float AbilityDurationReductionPerLevel = 1f;
                public const float UltimateBaseCooldown = 20f;
                public const float UltimateCooldownReductionPerLevel = 5f;
                // Thermal Detonation's knockback reach, moved by the same perks and by the same step
                public const float BlackHoleBaseRange = 150f;
                public const float BlackHoleRangeIncreasePerLevel = 50f;
                public const float BlackHolePullTime = 3f;
                // An enemy whose body or weapon the pull brings this close to the caster goes in, and so does a spider's body, shrinking into the caster over the take time
                public const float BlackHoleCoreRadius = 20f;
                public const float BlackHoleTakeTime = 0.3f;
                // The reference distance for the pull; the strength multiplier brings an enemy this far out in halfway through the field's lifetime
                public const float BlackHoleReachFraction = 0.75f;
                public const float BlackHolePullStrengthMultiplier = 4f;
                // The pull on spiders at the caster, applied the way the Gravity Grenade's Attractor applies its 1000
                public const float BlackHolePullForce = 250f;
                public const float BlackHoleCloseTime = 0.3f;
                public const float BlackHoleEndScale = 0.1f;
                public static readonly UnityEngine.Color RingColor = new UnityEngine.Color(0.65f, 0.25f, 1f, 0.8f);
                public const float SpawnDistance = 50f;
            }

            public static class Freeze
            {
                public const float SlowMotionCooldownRefund = 0.2f;
                public const float AbilityBaseCooldown = 15f;
                public const float AbilityCooldownReductionPerLevel = 4f;
                public const float AbilityBaseDuration = 5f;
                public const float AbilityDurationIncreasePerLevel = 2f;
                public const float UltimateBaseCooldown = 45f;
                public const float UltimateCooldownReductionPerLevel = 10f;
                public const float AbilityBaseRange = 87.5f;
                public const float AbilityRangeIncreasePerLevel = 25f;
                public const float UltimateBaseRange = 125f;
                public const float UltimateRangeIncreasePerLevel = 50f;
                public const float RewindRingTime = 0.4f;
                public const float TintStrength = 0.6f;
                public const string WaspName = "Wasp";
                public const string ShieldedWaspName = "Wasp Shielded";
                public static readonly UnityEngine.Color RingColor = new UnityEngine.Color(0.7f, 0.95f, 1f, 0.9f);
                public static readonly UnityEngine.Color RewindRingColor = new UnityEngine.Color(1f, 0.8f, 0.3f, 0.9f);
                public static readonly UnityEngine.Color TintColor = new UnityEngine.Color(0.55f, 0.85f, 1f, 1f);
            }

            public static class Vfx
            {
                public static readonly UnityEngine.Color PulseHotColor = new UnityEngine.Color(1f, 0.5f, 0.1f, 0.9f);
                public static readonly UnityEngine.Color FrostColor = new UnityEngine.Color(0.6f, 0.9f, 1f, 0.5f);
                public static readonly UnityEngine.Color ClipColor = new UnityEngine.Color(1f, 0.75f, 0.2f, 1f);
                public static readonly UnityEngine.Color NullFieldColor = new UnityEngine.Color(1f, 0.84f, 0f, 1f);
                public const float NullFieldShellAlpha = 0.02f;
                public static readonly UnityEngine.Color ReviveColor = new UnityEngine.Color(0.45f, 1f, 0.6f, 1f);
            }

            public static class Luck
            {
                public const float Level1Chance = 0.15f;
                public const float Level2Chance = 0.3f;
            }

            // Shared shop prices and card layout.
            public static class Chaos
            {
                public const int RerollCost = 1;
                public const int LifeCost = 2;
                public const int PerkCost = 3;
                public const int MaxLivesCost = 5;
                public const int SacrificeLives = 1;
                public const int SacrificePoints = 1;
                public const int SacrificeMinLives = 2;
                public const float MinLabelFontSize = 9f;
                public const float HudGainSeconds = 0.8f;
                public const float HudGainScale = 1.15f;
                public const float PanelWidth = 240f;
                public const float PanelGap = 16f;
                public const float SideMargin = 27f;
                public const float BottomMargin = 6f;
                public const float MinCardFactor = 0.5f;
                public const float WalletBlockHeight = 29f;
                public const float MinEntryHeight = 56f;
                public const float MaxEntryHeight = 96f;
                public const float EntrySpacing = 5f;
                public const int OfferSize = 2;
                public const float BuyHeight = 34f;
                public const float ConfirmWidth = 300f;
                public const float ConfirmHeight = 44f;
                public static readonly UnityEngine.Color ConfirmDisabledTint = new UnityEngine.Color(1f, 1f, 1f, 0.45f);
                public const float ConfirmGap = 14f;
                public const float LockedCardScale = 1.08f;
                public static readonly UnityEngine.Color LockedCardTint = new UnityEngine.Color(1f, 0.9f, 0.55f);
            }

            public static class Enemies
            {
                public const float SpeedMultiplier = 1.1f;
                public const float SpawnCountMultiplier = 2.1f;
                public const int WavesPerCapacityIncrease = 8;
                public const float SpawnInterval = 2.5f;
                public const float CapacityPollInterval = 0.1f;
                public const float MissileWhispShotForce = 40f;
                public const float TwinWhispShotMargin = 5f;
                public const float TwinWhispCoreOffset = 4f;
                public const float CocoonSpawnInterval = 7f;
                public const float CocoonFirstHatchDelay = 1f;
                public const int CocoonMaxWasps = 3;
                public const float CocoonPulseInterval = 2f;
                public const float CocoonPulseRadius = 40f;
                public const float CocoonPulseForce = 120f;
                public const float CocoonPulseSpeed = 100f;
                public static readonly UnityEngine.Color CocoonPulseColor = new UnityEngine.Color(0.7f, 0.25f, 1f, 0.8f);
            }

            public static class LaserRing
            {
                public const float Period = 4f;
                public const float WarningDuration = 1f;
                public const float ActiveDuration = 1f;
                public const float Radius = 35f;
                public const float Width = 3.5f;
                public const float WarningWidth = 1f;
                public const float WarningMinAlpha = 0.1f;
                public const float WarningMaxAlpha = 0.35f;
                public const float Power = 100f;
                public const int Segments = 72;
                public static readonly UnityEngine.Color Color = new UnityEngine.Color(1f, 0.09f, 0.09f, 1f);

                // Base enemy name -> laser variant name; enemies left out (ExplodingRoller, shielded ones) get no ring
                public static readonly Dictionary<string, string> Variants = new Dictionary<string, string>
                {
                    { "Wasp", "LaserWasp" },
                    { "Roller", "LaserRoller" },
                    { "Whisp", "LaserWhisp" },
                    { "MeleeWhisp", "LaserMeleeWhisp" },
                    { "Khepri", "LaserKhepri" },
                    { "PowerWhisp Variant", "LaserPowerWhisp" },
                    { "PowerWasp Variant", "LaserPowerWasp" },
                    { "TwinBladeMeleeWhisp", "LaserTwinBladeMeleeWhisp" },
                    { "PowerRoller Variant", "LaserPowerRoller" },
                    { "PowerKhepri Variant", "LaserPowerKhepri" },
                    { "PowerMeleeWhisp Variant", "LaserPowerMeleeWhisp" },
                    { "TwinWhisp", "LaserTwinWhisp" },
                    { "Hornet_Shaman Variant", "LaserHornetShaman" },
                    { "TwinBladePowerMeleeWhisp", "LaserTwinBladePowerMeleeWhisp" },
                    { "Hornet Variant", "LaserHornet" },
                    { "MissileWhisp", "LaserMissileWhisp" },
                };
            }

            public static class ShieldBubbles
            {
                // Base enemy name -> the shield bubble's height and scale on the enemy's root; Wasp, Hornet and Whisp values are the game's own shielded versions'
                public static readonly Dictionary<string, (float offsetY, float scale)> Fits = new Dictionary<string, (float, float)>
                {
                    { "Wasp", (0.1194f, 0.3673f) },
                    { "PowerWasp Variant", (0.1194f, 0.3673f) },
                    { "Hornet Variant", (0.8377f, 0.4979f) },
                    { "Hornet_Shaman Variant", (0.8377f, 0.4979f) },
                    { "Whisp", (0f, 1.7f) },
                    { "PowerWhisp Variant", (0f, 1.7f) },
                    { "MeleeWhisp", (0f, 1.7f) },
                    { "PowerMeleeWhisp Variant", (0f, 1.7f) },
                    { "TwinWhisp", (0f, 1.7f) },
                    { "TwinBladeMeleeWhisp", (0f, 1.7f) },
                    { "TwinBladePowerMeleeWhisp", (0f, 1.7f) },
                    { "MissileWhisp", (0f, 1.7f) },
                    { "Roller", (0f, 1.6f) },
                    { "PowerRoller Variant", (0f, 1.6f) },
                    { "Khepri", (4f, 1.6f) },
                    { "PowerKhepri Variant", (4f, 1.6f) },
                    { "WaspCocoon", (0f, 1.7f) },
                };
            }

            public static class Colors
            {
                public static readonly UnityEngine.Color ShieldedEnemyIndicatorColor = new UnityEngine.Color(0.7f, 0.25f, 1f, 1f);
                public static readonly UnityEngine.Color MissileWhispColor = new UnityEngine.Color(0.2f, 0f, 0f, 1f);
                public static readonly UnityEngine.Color MissileWhispLightColor = new UnityEngine.Color(1f, 0.8f, 0f, 1f);
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
                    { "Wasp", new EnemySpawnConfig(1.5f, 0, 7) },
                    { "Roller", new EnemySpawnConfig(1.5f, 1, 9) },
                    { "Whisp", new EnemySpawnConfig(2f, 2, 10) },
                    { "MeleeWhisp", new EnemySpawnConfig(2f, 3, 10) },
                    { "Khepri", new EnemySpawnConfig(2.5f, 4, 24) },
                    { "ExplodingRoller", new EnemySpawnConfig(3f, 5, 0) },
                    { "PowerWhisp Variant", new EnemySpawnConfig(3f, 6, 0) },
                    { "PowerWasp Variant", new EnemySpawnConfig(3f, 7, 16) },
                    { "TwinBladeMeleeWhisp", new EnemySpawnConfig(4f, 8, 16) },
                    { "WaspCocoon", new EnemySpawnConfig(4f, 9, 24) },
                    { "PowerRoller Variant", new EnemySpawnConfig(4f, 9, 0) },
                    { "PowerKhepri Variant", new EnemySpawnConfig(4f, 10, 0) },
                    { "PowerMeleeWhisp Variant", new EnemySpawnConfig(4f, 11, 20) },
                    { "TwinWhisp", new EnemySpawnConfig(4f, 12, 20) },
                    { "Wasp Shielded", new EnemySpawnConfig(4f, 13, 42) },
                    { "Hornet_Shaman Variant", new EnemySpawnConfig(6f, 14, 0) },
                    { "TwinBladePowerMeleeWhisp", new EnemySpawnConfig(6f, 15, 0) },
                    { "PowerWasp Variant Shield", new EnemySpawnConfig(6f, 16, 0) },
                    { "ShieldedTwinWhisp", new EnemySpawnConfig(6f, 17, 0) },
                    { "ShieldedWaspCocoon", new EnemySpawnConfig(7f, 18, 0) },
                    { "Hornet Variant", new EnemySpawnConfig(6f, 18, 25) },
                    { "MissileWhisp", new EnemySpawnConfig(7f, 19, 25) },
                    { "Shielded Hornet Variant", new EnemySpawnConfig(8f, 20, 0) },
                    { "ShieldedMissileWhisp", new EnemySpawnConfig(9f, 21, 0) },

                    { "LaserWasp", new EnemySpawnConfig(4.5f, 12, 19) },
                    { "LaserRoller", new EnemySpawnConfig(4.5f, 13, 21) },
                    { "LaserWhisp", new EnemySpawnConfig(6f, 14, 22) },
                    { "LaserMeleeWhisp", new EnemySpawnConfig(6f, 15, 22) },
                    { "LaserKhepri", new EnemySpawnConfig(7.5f, 16, 36) },
                    { "LaserPowerWhisp", new EnemySpawnConfig(9f, 18, 0) },
                    { "LaserPowerWasp", new EnemySpawnConfig(9f, 19, 28) },
                    { "LaserTwinBladeMeleeWhisp", new EnemySpawnConfig(12f, 20, 28) },
                    { "LaserPowerRoller", new EnemySpawnConfig(12f, 21, 0) },
                    { "LaserPowerKhepri", new EnemySpawnConfig(12f, 22, 0) },
                    { "LaserPowerMeleeWhisp", new EnemySpawnConfig(12f, 23, 32) },
                    { "LaserTwinWhisp", new EnemySpawnConfig(12f, 24, 32) },
                    { "LaserHornetShaman", new EnemySpawnConfig(18f, 26, 0) },
                    { "LaserTwinBladePowerMeleeWhisp", new EnemySpawnConfig(18f, 27, 0) },
                    { "LaserHornet", new EnemySpawnConfig(18f, 30, 37) },
                    { "LaserMissileWhisp", new EnemySpawnConfig(21f, 31, 37) },
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
                [PerkNames.ImmuneAbility] = "Null Field",
                [PerkNames.AmmoAbility] = "Bottomless Clip",
                [PerkNames.PulseAbility] = "Kinetic Pulse",
                [PerkNames.StorageAbility] = "Pocket Dimension",
                [PerkNames.FreezeAbility] = "Freeze Time",
                [PerkNames.AbilityCooldown] = "Ability Cooldown",
                [PerkNames.AbilityDuration] = "Ability Duration",
                [PerkNames.ShortTermInvestment] = "Short Term Investment",
                [PerkNames.LongTermInvestment] = "Long Term Investment",
                [PerkNames.PerkLuck] = "Lucky",
                // Ultimate perks - dynamic names based on which ability is active
                [PerkNames.ImmuneAbilityUltimate] = "Neural Backup",
                [PerkNames.AmmoAbilityUltimate] = "Care Package",
                [PerkNames.PulseAbilityUltimate] = "Thermal Detonation",
                [PerkNames.StorageAbilityUltimate] = "Black Hole",
                [PerkNames.FreezeAbilityUltimate] = "Rewind"
            };

            private static readonly Dictionary<string, string> descriptions = new Dictionary<string, string>
            {
                [PerkNames.ImmuneAbility] = "Become immune to all damage for a short duration",
                [PerkNames.AmmoAbility] = "Shooting will not consume ammo for a short duration",
                [PerkNames.PulseAbility] = "Release a pulse that knocks back nearby enemies",
                [PerkNames.StorageAbility] = "Store a weapon for later use",
                [PerkNames.FreezeAbility] = "Freeze enemies, other players and shots in an area for a few seconds",
                [PerkNames.AbilityCooldown] = "Reduces ability cooldown",
                [PerkNames.AbilityDuration] = "Increases ability duration",
                [PerkNames.ShortTermInvestment] = "Buffs ability duration and cooldown, but nerfs ultimate duration and cooldown",
                [PerkNames.LongTermInvestment] = "Buffs ultimate duration and cooldown, but nerfs ability duration and cooldown",
                [PerkNames.PerkLuck] = "Chance to see level 2 perks even without level 1",
                [PerkNames.ImmuneAbilityUltimate] = "<color=" + Formatting.ColorRed + ">After a cast time, revive one dead player or summon 3 friendly wasps</color>",
                [PerkNames.AmmoAbilityUltimate] = "<color=" + Formatting.ColorRed + ">Spawns weapons around the map</color>",
                [PerkNames.PulseAbilityUltimate] = "<color=" + Formatting.ColorRed + ">Kills nearby enemies; the wider pulse knocks the rest back</color>",
                [PerkNames.StorageAbilityUltimate] = "<color=" + Formatting.ColorRed + ">For 3 seconds, pull the enemies and spiders around you toward you; whatever reaches you goes into the pocket dimension</color>",
                [PerkNames.FreezeAbilityUltimate] = "<color=" + Formatting.ColorRed + ">Turn back time on nearby enemies, turning them into wasps. Shields stay</color>"
            };

            private static readonly Dictionary<string, string> upgradeDescriptions = new Dictionary<string, string>
            {
                [PerkNames.ImmuneAbility] = "",
                [PerkNames.AmmoAbility] = "",
                [PerkNames.PulseAbility] = "",
                [PerkNames.StorageAbility] = "",
                [PerkNames.FreezeAbility] = "",
                [PerkNames.AbilityCooldown] = "Reduces ultimate cooldown",
                [PerkNames.AbilityDuration] = "Increases ultimate duration",
                [PerkNames.ShortTermInvestment] = "",
                [PerkNames.LongTermInvestment] = "",
                [PerkNames.PerkLuck] = "Increases chance to see level 2 perks.",
                [PerkNames.ImmuneAbilityUltimate] = "",
                [PerkNames.AmmoAbilityUltimate] = "",
                [PerkNames.PulseAbilityUltimate] = "",
                [PerkNames.StorageAbilityUltimate] = "",
                [PerkNames.FreezeAbilityUltimate] = ""
            };

            // Custom display names for Duration perk based on active ability
            private const string DURATION_NAME_WITH_PULSE = "Bigger Pulse";
            private const string DURATION_NAME_WITH_STORAGE = "Faster Retrieval";
            private const string DURATION_NAME_WITH_FREEZE = "Deep Freeze";

            // Custom descriptions for Duration perk based on active ability
            private const string DURATION_DESC_WITH_PULSE = "Increases knockback ability area of effect";
            private const string DURATION_UPGRADE_DESC_WITH_PULSE = "Increases Thermal Detonation's knockback area";
            private const string DURATION_DESC_WITH_STORAGE = "Faster ability retrieval from the storage";
            private const string DURATION_UPGRADE_DESC_WITH_STORAGE = "Increases Black Hole range";
            private const string DURATION_DESC_WITH_FREEZE = "Freezes targets for longer and farther away";
            private const string DURATION_UPGRADE_DESC_WITH_FREEZE = "Increases rewind range";
            private const string DURATION_UPGRADE_DESC_WITH_AMMO = "Ultimate spawns weapons at more points around the map";
            private static readonly string DURATION_UPGRADE_DESC_WITH_IMMUNE = $"Neural Backup casts {Values.Immune.UltimateDurationIncreasePerLevel:0.#} seconds faster";

            public static string GetDisplayName(string name, PerksManager perksManager = null)
            {
                if (perksManager != null && name == PerkNames.AbilityDuration)
                {
                    if (perksManager.GetPerkLevel(PerkNames.PulseAbility) > 0)
                    {
                        return DURATION_NAME_WITH_PULSE;
                    }
                    if (perksManager.GetPerkLevel(PerkNames.StorageAbility) > 0)
                    {
                        return DURATION_NAME_WITH_STORAGE;
                    }
                    if (perksManager.GetPerkLevel(PerkNames.FreezeAbility) > 0)
                    {
                        return DURATION_NAME_WITH_FREEZE;
                    }
                }
                return displayNames.ContainsKey(name) ? displayNames[name] : name;
            }

            public static string GetDescription(string name, PerksManager perksManager)
            {
                if (perksManager == null) return descriptions.ContainsKey(name) ? descriptions[name] : "";

                if (name == PerkNames.AbilityDuration && perksManager.GetPerkLevel(PerkNames.PulseAbility) > 0)
                {
                    return DURATION_DESC_WITH_PULSE;
                }
                if (name == PerkNames.AbilityDuration && perksManager.GetPerkLevel(PerkNames.StorageAbility) > 0)
                {
                    return DURATION_DESC_WITH_STORAGE;
                }
                if (name == PerkNames.AbilityDuration && perksManager.GetPerkLevel(PerkNames.FreezeAbility) > 0)
                {
                    return DURATION_DESC_WITH_FREEZE;
                }
                return descriptions.ContainsKey(name) ? descriptions[name] : "";
            }

            public static string GetUpgradeDescription(string name, PerksManager perksManager)
            {
                if (perksManager == null) return upgradeDescriptions.ContainsKey(name) ? upgradeDescriptions[name] : "";

                if (name == PerkNames.AbilityDuration && perksManager.GetPerkLevel(PerkNames.PulseAbility) > 0)
                {
                    return DURATION_UPGRADE_DESC_WITH_PULSE;
                }
                if (name == PerkNames.AbilityDuration && perksManager.GetPerkLevel(PerkNames.StorageAbility) > 0)
                {
                    return DURATION_UPGRADE_DESC_WITH_STORAGE;
                }
                if (name == PerkNames.AbilityDuration && perksManager.GetPerkLevel(PerkNames.FreezeAbility) > 0)
                {
                    return DURATION_UPGRADE_DESC_WITH_FREEZE;
                }
                if (name == PerkNames.AbilityDuration && perksManager.GetPerkLevel(PerkNames.AmmoAbility) > 0)
                {
                    return DURATION_UPGRADE_DESC_WITH_AMMO;
                }
                if (name == PerkNames.AbilityDuration && perksManager.GetPerkLevel(PerkNames.ImmuneAbility) > 0)
                {
                    return DURATION_UPGRADE_DESC_WITH_IMMUNE;
                }
                return upgradeDescriptions.ContainsKey(name) ? upgradeDescriptions[name] : "";
            }
        }
    }
}
