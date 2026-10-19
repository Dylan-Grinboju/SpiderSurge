using System;
using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using SpiderSurge.Common;
using SpiderTeams;
using Unity.Netcode;
using UnityEngine;
using Logger = Silk.Logger;

namespace SpiderSurge
{
    internal static class AchievementTracker
    {
        public const string BigGrenadeExplosion = "BigExplosion";
        public const string TwinBladePowerWhisp = "TwinBladePowerMeleeWhisp";
        public const string NothingPerk = "nothing";
        public const float LowBladeDurability = 0.15f;
        public const int ThermalTeammateKills = 3;
        public const float BlastSurvivalMargin = 0.25f;

        private static readonly AccessTools.FieldRef<SpiderHealthSystem, bool> SpiderDying = AccessTools.FieldRefAccess<SpiderHealthSystem, bool>("_dying");

        public static Explosion CurrentExplosion;

        private static readonly Dictionary<(int caster, int revived), int> Revives = new Dictionary<(int, int), int>();
        private static readonly HashSet<ParticleBlade> FullyUpgradedBlades = new HashSet<ParticleBlade>();

        // Every player is a teammate in survival; in versus only players of the same team, when Teams runs a team match
        public static bool AreTeammates(PlayerController player, PlayerController other)
        {
            if (player == null || other == null || player == other) return false;
            VersusMode versus = VersusMode.instance;
            if (versus == null || !versus.GameModeActive()) return true;
            return Parts.Teams.Running && SameTeam(player, other);
        }

        private static bool SameTeam(PlayerController player, PlayerController other)
        {
            TeamManager manager = TeamManager.Instance;
            return manager != null && TeamManager.TeamMatchActive &&
                   manager.TryGetTeamOfPlayerId(player.playerID.Value, out int team) &&
                   manager.TryGetTeamOfPlayerId(other.playerID.Value, out int otherTeam) &&
                   team == otherTeam;
        }

        public static bool Gone(SpiderHealthSystem spider) => spider == null || spider.dead || spider.astralDead || SpiderDying(spider);

        public static void Run(string what, Action action)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                Logger.LogError($"Achievement tracking failed ({what}): {ex.Message}");
            }
        }

        public static void ResetRevives() => Revives.Clear();

        public static int CountRevive(PlayerController caster, PlayerController revived)
        {
            var key = (caster.playerID.Value, revived.playerID.Value);
            Revives.TryGetValue(key, out int count);
            Revives[key] = ++count;
            return count;
        }

        public static void RememberBlade(ParticleBlade blade, bool fullyUpgraded)
        {
            FullyUpgradedBlades.RemoveWhere(destroyed => destroyed == null);
            if (fullyUpgraded) FullyUpgradedBlades.Add(blade);
            else FullyUpgradedBlades.Remove(blade);
        }

        public static bool IsFullyUpgraded(Weapon weapon) => weapon is ParticleBlade blade && FullyUpgradedBlades.Contains(blade);

        public static PlayerController HolderOf(Weapon weapon) => weapon != null && weapon.equipped && weapon.owner != null ? Players.Of(weapon.owner) : null;
    }

    // Black Hole and I Will Keep You Safe
    [HarmonyPatch(typeof(BanishedTarget))]
    public static class BanishedTarget_Achievement_Patches
    {
        [HarmonyPatch(nameof(BanishedTarget.BeginEnemy))]
        [HarmonyPostfix]
        public static void BeginEnemy_Postfix(EnemyHealthSystem enemy, PlayerController caster)
        {
            AchievementTracker.Run("Banish", () =>
            {
                if (enemy != null && enemy.GetComponent<HornetShamanBrain>() != null && BanishedTarget.IsBanished(enemy.gameObject))
                {
                    SurgeAchievements.Unlock(SurgeAchievements.BlackHole, caster);
                }
            });
        }

        [HarmonyPatch(nameof(BanishedTarget.BeginSpider))]
        [HarmonyPostfix]
        public static void BeginSpider_Postfix(SpiderHealthSystem spider, PlayerController caster)
        {
            AchievementTracker.Run("Banish", () =>
            {
                if (spider != null && BanishedTarget.IsBanished(spider.rootObject) && AchievementTracker.AreTeammates(caster, Players.Of(spider)))
                {
                    SurgeAchievements.Unlock(SurgeAchievements.KeepYouSafe, caster);
                }
            });
        }
    }

    // Total Destruction counts the teammates one Thermal Detonation kills
    [HarmonyPatch(typeof(PulseAbility), "ApplyPulsePhysics")]
    public static class PulseAbility_Achievement_Patch
    {
        [HarmonyPrefix]
        public static void Prefix(PulseAbility __instance, bool deadly, out List<SpiderHealthSystem> __state)
        {
            __state = null;
            PlayerController caster = PulseAbility.CasterOf(__instance);
            if (!deadly || caster == null) return;

            var teammates = new List<SpiderHealthSystem>();
            AchievementTracker.Run("Thermal Detonation", () =>
            {
                foreach (PlayerController player in Players.All)
                {
                    SpiderHealthSystem spider = player != null ? player.spiderHealthSystem : null;
                    if (AchievementTracker.AreTeammates(caster, player) && !AchievementTracker.Gone(spider)) teammates.Add(spider);
                }
            });
            if (teammates.Count >= AchievementTracker.ThermalTeammateKills) __state = teammates;
        }

        [HarmonyFinalizer]
        public static void Finalizer(PulseAbility __instance, List<SpiderHealthSystem> __state)
        {
            if (__state == null) return;

            AchievementTracker.Run("Thermal Detonation", () =>
            {
                int killed = __state.FindAll(AchievementTracker.Gone).Count;
                if (killed >= AchievementTracker.ThermalTeammateKills)
                {
                    SurgeAchievements.Unlock(SurgeAchievements.TotalDestruction, PulseAbility.CasterOf(__instance));
                }
            });
        }
    }

    [HarmonyPatch(typeof(Explosion), "KnockBack")]
    public static class Explosion_KnockBack_Achievement_Patch
    {
        [HarmonyPrefix]
        public static void Prefix(Explosion __instance, out Explosion __state)
        {
            __state = AchievementTracker.CurrentExplosion;
            AchievementTracker.CurrentExplosion = __instance;
        }

        [HarmonyFinalizer]
        public static void Finalizer(Explosion __state)
        {
            AchievementTracker.CurrentExplosion = __state;
        }
    }

    // An explosion only calls Damage on a spider inside its death radius, so this is a Big Grenade blast that would have killed
    [HarmonyPatch(typeof(SpiderHealthSystem), nameof(SpiderHealthSystem.Damage))]
    public static class SpiderHealthSystem_Damage_Achievement_Patch
    {
        private static readonly AccessTools.FieldRef<Explosion, float> BlastEnd = AccessTools.FieldRefAccess<Explosion, float>("_stopAt");
        private static readonly HashSet<(int spider, int blast)> Watched = new HashSet<(int, int)>();

        [HarmonyPrefix]
        public static void Prefix(SpiderHealthSystem __instance, bool __runOriginal)
        {
            Explosion explosion = AchievementTracker.CurrentExplosion;
            if (!__runOriginal || explosion == null || !explosion.name.StartsWith(AchievementTracker.BigGrenadeExplosion, StringComparison.Ordinal)) return;

            AchievementTracker.Run("Null Field", () =>
            {
                ImmuneAbility nullField = ImmuneAbility.GetByHealthSystem(__instance);
                if (nullField == null || !nullField.IsImmune || !nullField.isActiveAndEnabled || AchievementTracker.Gone(__instance)) return;

                var key = (__instance.GetInstanceID(), explosion.GetInstanceID());
                if (Watched.Add(key))
                {
                    nullField.StartCoroutine(UnlockIfSurvived(__instance, key, BlastEnd(explosion)));
                }
            });
        }

        // The blast keeps hitting every tick and Null Field can run out first, so the spider has to outlive the blast
        private static IEnumerator UnlockIfSurvived(SpiderHealthSystem spider, (int, int) key, float blastEnd)
        {
            while (Time.time < blastEnd + AchievementTracker.BlastSurvivalMargin)
            {
                yield return null;
            }
            Watched.Remove(key);

            AchievementTracker.Run("Null Field", () =>
            {
                if (!AchievementTracker.Gone(spider))
                {
                    SurgeAchievements.Unlock(SurgeAchievements.UnstoppableForce, Players.Of(spider));
                }
            });
        }
    }

    // Healer and You Are Not Done Yet; Teams replaces the revive in versus, and its result still reaches the postfix
    [HarmonyPatch(typeof(ImmuneAbility), "TryRespawnDeadPlayer")]
    public static class ImmuneAbility_Revive_Achievement_Patch
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        public static void Prefix(out List<PlayerController> __state)
        {
            var ghosts = new List<PlayerController>();
            AchievementTracker.Run("Neural Backup", () =>
            {
                foreach (PlayerController player in Players.All)
                {
                    if (ImmuneAbility.IsGhost(player)) ghosts.Add(player);
                }
            });
            __state = ghosts;
        }

        [HarmonyPostfix]
        public static void Postfix(ImmuneAbility __instance, bool __result, ref PlayerController revivedPlayer, List<PlayerController> __state)
        {
            PlayerController revived = revivedPlayer;
            if (!__result || revived == null) return;

            AchievementTracker.Run("Neural Backup", () =>
            {
                PlayerController caster = __instance.GetComponentInParent<PlayerController>() ?? Players.ById(__instance.PlayerId);
                if (caster == null) return;
                if (__state.Contains(revived))
                {
                    SurgeAchievements.Unlock(SurgeAchievements.NotDoneYet, caster);
                }
                if (AchievementTracker.CountRevive(caster, revived) >= 2)
                {
                    SurgeAchievements.Unlock(SurgeAchievements.Healer, caster);
                }
            });
        }
    }

    // LateUpdate keeps calling StartWave until a wave is running, so only the call that starts one counts
    [HarmonyPatch(typeof(SurvivalMode), "StartWave")]
    public static class SurvivalMode_StartWave_Achievement_Patch
    {
        private static readonly AccessTools.FieldRef<SurvivalMode, bool> WaveInProgress = AccessTools.FieldRefAccess<SurvivalMode, bool>("_waveInProgress");

        [HarmonyPrefix]
        public static void Prefix(SurvivalMode __instance, out bool __state) => __state = WaveInProgress(__instance);

        [HarmonyPostfix]
        public static void Postfix(SurvivalMode __instance, bool __state)
        {
            if (!__state && WaveInProgress(__instance)) AchievementTracker.ResetRevives();
        }
    }

    [HarmonyPatch(typeof(VersusMode), nameof(VersusMode.StartRound))]
    public static class VersusMode_StartRound_Achievement_Patch
    {
        [HarmonyPrefix]
        public static void Prefix() => AchievementTracker.ResetRevives();
    }

    // Doomsday Scenario: Pocket Dimension takes a Big Grenade whose fuse is already lit
    [HarmonyPatch(typeof(StorageAbility), "StoreWeapon")]
    public static class StorageAbility_StoreWeapon_Achievement_Patch
    {
        private static readonly AccessTools.FieldRef<Grenade, bool> Armed = AccessTools.FieldRefAccess<Grenade, bool>("_armed");

        [HarmonyPrefix]
        public static void Prefix(StorageAbility __instance, GameObject heldWeaponObj)
        {
            if (heldWeaponObj == null) return;
            AchievementTracker.Run("Pocket Dimension", () =>
            {
                Grenade grenade = heldWeaponObj.GetComponent<Grenade>();
                if (grenade != null && grenade.serializationWeaponName == SerializationWeaponName.BigGrenade && Armed(grenade))
                {
                    SurgeAchievements.Unlock(SurgeAchievements.Doomsday, Players.ById(__instance.PlayerId));
                }
            });
        }
    }

    // A blade takes its Size Matters and Thick Blades levels when it spawns, so they are read there
    [HarmonyPatch(typeof(ParticleBlade), nameof(ParticleBlade.OnNetworkSpawn))]
    public static class ParticleBlade_OnNetworkSpawn_Achievement_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(ParticleBlade __instance)
        {
            AchievementTracker.Run("Particle Blade", () =>
            {
                ModifierManager modifiers = ModifierManager.instance;
                if (modifiers == null || !Net.IsAuthority) return;
                AchievementTracker.RememberBlade(__instance, modifiers.GetModLevel("sizematters") >= 2 && modifiers.GetModLevel("thickblades") >= 2);
            });
        }
    }

    [HarmonyPatch(typeof(AmmoAbility), "OnActivate")]
    public static class AmmoAbility_OnActivate_Achievement_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(AmmoAbility __instance)
        {
            AchievementTracker.Run("Bottomless Clip", () =>
            {
                PlayerController caster = Players.ById(__instance.PlayerId);
                SpiderHealthSystem spider = caster != null ? caster.spiderHealthSystem : null;
                SpiderWeaponManager weapons = spider != null ? spider.GetComponentInChildren<SpiderWeaponManager>() : null;
                if (weapons != null && AchievementTracker.IsFullyUpgraded(weapons.equippedWeapon))
                {
                    SurgeAchievements.Unlock(SurgeAchievements.UnlimitedPower, caster);
                }
            });
        }
    }

    // Kiss of Death: a player's blade field touches the whisp's body while the blade is nearly spent
    [HarmonyPatch(typeof(ForceField), "Damage")]
    public static class ForceField_Damage_Achievement_Patch
    {
        [HarmonyPrefix]
        public static void Prefix(ForceField __instance, Collision2D other)
        {
            if (other == null || other.gameObject == null) return;
            AchievementTracker.Run("Particle Blade hit", () =>
            {
                EnemyHealthSystem enemy = other.gameObject.GetComponent<EnemyHealthSystem>();
                if (enemy == null || enemy.IsDead() || !enemy.name.StartsWith(AchievementTracker.TwinBladePowerWhisp, StringComparison.Ordinal)) return;

                ParticleBlade blade = __instance.GetComponentInParent<ParticleBlade>();
                if (blade == null || blade.maxAmmo <= 0f || blade.ammo <= 0f || blade.ammo >= blade.maxAmmo * AchievementTracker.LowBladeDurability) return;

                SurgeAchievements.Unlock(SurgeAchievements.KissOfDeath, AchievementTracker.HolderOf(blade));
            });
        }
    }

    // The perk timer picks a card itself when it runs out, and that pick is nobody's; the submit comes 0.25 s before the choice lands
    [HarmonyPatch(typeof(SurvivalModeHud), nameof(SurvivalModeHud.PerkChoiceSubmit))]
    public static class SurvivalModeHud_PerkChoiceSubmit_Achievement_Patch
    {
        public static bool TimedOut;

        [HarmonyPrefix]
        public static void Prefix() => AchievementTracker.Run("Luck", () => TimedOut = Logging.SpiderSurgeStatsManager.PerkChoiceTimerRanOut());
    }

    // Yay I Guess?: Luck made the Nothing perk a Lucky Upgrade and a player picked it
    [HarmonyPatch(typeof(SurvivalMode), nameof(SurvivalMode.ChoosePerkAndLevel))]
    public static class SurvivalMode_ChoosePerk_Achievement_Patch
    {
        private static readonly AccessTools.FieldRef<SurvivalMode, NetworkVariable<bool>> WaitingForPerk = AccessTools.FieldRefAccess<SurvivalMode, NetworkVariable<bool>>("_waitingForPerk");

        [HarmonyPrefix]
        public static void Prefix(SurvivalMode __instance, Modifier modifier)
        {
            AchievementTracker.Run("Luck", () =>
            {
                if (SurgeGameModeManager.IsSurgeRunActive && WaitingForPerk(__instance).Value && !SurvivalModeHud_PerkChoiceSubmit_Achievement_Patch.TimedOut &&
                    modifier != null && modifier.data != null && modifier.data.key == AchievementTracker.NothingPerk &&
                    modifier.levelInSurvival == 0 && ModifierManager_GetNonMaxedSurvivalMods_Patch.LuckyPerkKeys.Contains(AchievementTracker.NothingPerk))
                {
                    SurgeAchievements.Unlock(SurgeAchievements.YayIGuess, null);
                }
            });
        }
    }
}
