using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using SpiderSurge.Common;
using SpiderSurge.Enemies;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SpiderSurge
{
    internal sealed class EnemyAchievementOrigin : MonoBehaviour
    {
        internal readonly List<FreezeAchievementCast> Freezes = new List<FreezeAchievementCast>();
        internal bool Hatchling;
        internal PlayerController Summoner;
        internal PlayerController PulsePlayer;
        internal bool RegularPulse;

        internal static EnemyAchievementOrigin Of(EnemyHealthSystem enemy) =>
            enemy.GetComponent<EnemyAchievementOrigin>() ?? enemy.gameObject.AddComponent<EnemyAchievementOrigin>();
    }

    internal sealed class MissileAchievementOrigin : MonoBehaviour
    {
        internal EnemyHealthSystem Shooter;
        internal PlayerController ReturnedBy;
    }

    internal sealed class StoredWeaponAchievement : MonoBehaviour
    {
        internal StoredWeaponJourney Journey;

        internal static string CurrentMap => SceneManager.GetActiveScene().name == "MapEditorBase" && LevelController.instance != null
            ? LevelController.instance.activeCustomMapPath
            : SceneManager.GetActiveScene().name;

        internal static StoredWeaponJourney Of(Weapon weapon)
        {
            var marker = weapon.GetComponent<StoredWeaponAchievement>() ?? weapon.gameObject.AddComponent<StoredWeaponAchievement>();
            return marker.Journey ?? (marker.Journey = new StoredWeaponJourney(CurrentMap));
        }

        internal static void Restore(Weapon weapon, StoredWeaponJourney journey, int playerId)
        {
            if (journey == null) return;
            if (journey.Arrive(CurrentMap)) SurgeAchievements.Unlock(SurgeAchievements.FamilyHeirloom, Players.ById(playerId));
            var marker = weapon.gameObject.AddComponent<StoredWeaponAchievement>();
            marker.Journey = journey;
        }
    }

    internal static class AbilityAchievementTracker
    {
        private static readonly AccessTools.FieldRef<SurvivalMode, bool> WaveInProgress = GameEvents.WaveInProgress;
        internal static Component Attack;
        internal static EnemyHealthSystem LastWasp;
        internal static PlayerController ThermalCaster;
        private static PlayerController pendingOverkill;
        private static int pendingWave;

        internal static void ClearWave() => pendingOverkill = null;

        // Overkill waits for the wave to finish; a hostile spawn after the blast means that Wasp was not the wave's last enemy
        internal static void CompleteWave(SurvivalMode survival)
        {
            if (!Net.IsAuthority) return;
            if (pendingOverkill != null && survival.CurrentWave == pendingWave && WaveInProgress(survival))
                SurgeAchievements.Unlock(SurgeAchievements.Overkill, pendingOverkill);
            ClearWave();
        }

        internal static EnemyHealthSystem LastPlainWasp()
        {
            SurvivalMode survival = SurvivalMode.instance;
            if (survival == null || !survival.GameModeActive() || !WaveInProgress(survival)) return null;
            EnemyHealthSystem last = null;
            foreach (EnemyHealthSystem enemy in EnemyHealthSystem.s_activeEnemies)
            {
                if (enemy == null || enemy.IsDead() || !enemy.needToKillToCompleteLevel) continue;
                if (last != null) return null;
                last = enemy;
            }
            return last != null && last.name.Replace("(Clone)", "").Trim() == Consts.Values.Freeze.WaspName &&
                   (last.shield == null || !last.shield.activeInHierarchy) ? last : null;
        }

        internal static EnemyHealthSystem EnemyOf(Component source)
        {
            if (source == null) return null;
            if (source is WaspStinger stinger) return stinger.enemyHealthSystem;
            if (source is DeathCloud cloud) return cloud.origin != null ? cloud.origin.GetComponentInParent<EnemyHealthSystem>() : null;
            var missile = source.GetComponent<MissileAchievementOrigin>();
            if (missile != null) return missile.Shooter;
            if (source is RailShot shot)
            {
                foreach (GameObject ignored in shot.ignore)
                {
                    EnemyHealthSystem shooter = ignored != null ? ignored.GetComponentInParent<EnemyHealthSystem>() : null;
                    if (shooter != null) return shooter;
                }
            }
            Weapon weapon = source.GetComponentInParent<Weapon>();
            if (weapon != null && weapon.owner != null && weapon.owner.healthSystem is EnemyHealthSystem owner) return owner;
            return source.GetComponentInParent<EnemyHealthSystem>();
        }

        internal static void EnemyKilled(EnemyHealthSystem enemy)
        {
            if (enemy.needToKillToCompleteLevel) ClearWave();
            EnemyAchievementOrigin origin = enemy.GetComponent<EnemyAchievementOrigin>();
            if (Attack is ForceField field && FrozenEnemy.IsFrozen(enemy) && origin != null)
            {
                ParticleBlade blade = field.GetComponentInParent<ParticleBlade>();
                PlayerController player = blade != null ? StatsMod.HitLogic.WeaponPlayer(blade) : null;
                foreach (FreezeAchievementCast freeze in origin.Freezes)
                {
                    if (player != null && freeze.RecordKill(player.playerID.Value, enemy.GetInstanceID(), Time.time))
                        SurgeAchievements.Unlock(SurgeAchievements.ColdCuts, player);
                }
            }
            if (Attack is FriendlyWaspStinger friendly && origin != null && origin.Hatchling)
            {
                var summoned = friendly.enemyHealthSystem != null ? friendly.enemyHealthSystem.GetComponent<EnemyAchievementOrigin>() : null;
                if (summoned != null && summoned.Summoner != null)
                    SurgeAchievements.Unlock(SurgeAchievements.WeBroughtOurOwn, summoned.Summoner);
            }
            if (Attack is Explosion explosion)
            {
                var missile = explosion.GetComponent<MissileAchievementOrigin>();
                if (missile != null && missile.Shooter == enemy && missile.ReturnedBy != null)
                    SurgeAchievements.Unlock(SurgeAchievements.ReturnToSender, missile.ReturnedBy);
            }
            if (LastWasp == enemy && ThermalCaster != null &&
                (Attack is PulseAbility || (Attack is Explosion blast && blast.GetComponent<PulseExplosionCaster>() != null)))
            {
                pendingOverkill = ThermalCaster;
                pendingWave = SurvivalMode.instance.CurrentWave;
            }
        }

        internal static void FreezeEnemy(EnemyHealthSystem enemy, FreezeAchievementCast cast, bool firing)
        {
            var origin = EnemyAchievementOrigin.Of(enemy);
            origin.Freezes.RemoveAll(previous => Time.time >= previous.EndsAt);
            origin.Freezes.Add(cast);
            if (firing) SurgeAchievements.Unlock(SurgeAchievements.HoldThatThought, Players.ById(cast.PlayerId));
        }

        internal static void Evicted(EnemyHealthSystem enemy, PlayerController player)
        {
            var origin = enemy.GetComponent<EnemyAchievementOrigin>();
            if (origin != null && origin.RegularPulse && origin.PulsePlayer == player && enemy.GetComponent<SpawnerBrain>() != null)
                SurgeAchievements.Unlock(SurgeAchievements.EvictionNotice, player);
        }

        internal static void TransferMissile(Explosion explosion, NetworkBehaviour source)
        {
            var missile = source != null ? source.GetComponent<MissileAchievementOrigin>() : null;
            if (missile == null) return;
            var blast = explosion.gameObject.AddComponent<MissileAchievementOrigin>();
            blast.Shooter = missile.Shooter;
            blast.ReturnedBy = Attack == null && StatsMod.HitLogic.CurrentAttacker == null ? missile.ReturnedBy : null;
        }
    }

    [HarmonyPatch(typeof(EnemyHealthSystem), "OnEnable")]
    internal static class OverkillEnemySpawnPatch
    {
        [HarmonyPostfix]
        private static void Postfix(EnemyHealthSystem __instance)
        {
            if (Net.IsAuthority && __instance.needToKillToCompleteLevel) AbilityAchievementTracker.ClearWave();
        }
    }

    // Scope the actual damaging source so shields, unrelated deaths and chained explosions cannot count as a blade or stinger kill.
    [HarmonyPatch]
    internal static class AbilityAchievementAttackPatch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(ForceField), "Damage");
            yield return AccessTools.Method(typeof(WaspStinger), "OnCollisionEnter2D");
            yield return AccessTools.Method(typeof(FriendlyWaspStinger), "OnCollisionEnter2D");
            yield return AccessTools.Method(typeof(RollerStrut), "OnCollisionEnter2D");
            yield return AccessTools.Method(typeof(MeleeHazard), "OnCollisionEnter2D");
            yield return AccessTools.Method(typeof(SawDisc), "TryDamage");
            yield return AccessTools.Method(typeof(RailShot), "ProcessHit");
            yield return AccessTools.Method(typeof(DeathCloud), "OnTriggerStay2D");
            yield return AccessTools.Method(typeof(WhispBrain), "ProcessCollision");
            yield return AccessTools.Method(typeof(Explosion), "KnockBack");
            yield return AccessTools.Method(typeof(SpiderHealthSystem), "AfterlifeExplosion");
            yield return AccessTools.Method(typeof(PulseAbility), "ApplyPulsePhysics");
        }

        [HarmonyPrefix]
        private static void Prefix(Component __instance, out Component __state)
        {
            __state = AbilityAchievementTracker.Attack;
            AbilityAchievementTracker.Attack = __instance;
        }

        [HarmonyFinalizer]
        private static void Finalizer(Component __state) => AbilityAchievementTracker.Attack = __state;
    }

    [HarmonyPatch(typeof(EnemyHealthSystem), nameof(EnemyHealthSystem.Explode))]
    internal static class EnemyDeathAbilityAchievementPatch
    {
        [HarmonyPrefix]
        private static void Prefix(EnemyHealthSystem __instance, bool ____exploding)
        {
            if (!Net.IsAuthority || ____exploding || __instance.ignoreIndirectDamage) return;
            AchievementTracker.Run("Ability kill", static enemy => AbilityAchievementTracker.EnemyKilled(enemy), __instance);
        }
    }

    [HarmonyPatch(typeof(SpiderHealthSystem), nameof(SpiderHealthSystem.Damage))]
    internal static class FrozenEnemyDeathAchievementPatch
    {
        [HarmonyPrefix]
        private static void Prefix(SpiderHealthSystem __instance, out bool __state)
        {
            __state = Net.IsAuthority && FrozenEnemy.AnyFrozen && !AchievementTracker.Gone(__instance) &&
                FrozenEnemy.IsFrozen(AbilityAchievementTracker.EnemyOf(AbilityAchievementTracker.Attack));
        }

        [HarmonyPostfix]
        private static void Postfix(SpiderHealthSystem __instance, bool __state)
        {
            if (__state && AchievementTracker.Gone(__instance))
                AchievementTracker.Run("Still Sharp", static spider => SurgeAchievements.Unlock(SurgeAchievements.StillSharp, Players.Of(spider)), __instance);
        }
    }

    [HarmonyPatch(typeof(PulseAbility), "ApplyPulsePhysics")]
    internal static class ThermalOverkillAchievementPatch
    {
        [HarmonyPrefix]
        private static void Prefix(PulseAbility __instance, bool deadly, out (EnemyHealthSystem, PlayerController) __state)
        {
            __state = (AbilityAchievementTracker.LastWasp, AbilityAchievementTracker.ThermalCaster);
            AbilityAchievementTracker.LastWasp = deadly ? AbilityAchievementTracker.LastPlainWasp() : null;
            AbilityAchievementTracker.ThermalCaster = deadly ? PulseAbility.CasterOf(__instance) : null;
        }

        [HarmonyFinalizer]
        private static void Finalizer((EnemyHealthSystem, PlayerController) __state)
        {
            AbilityAchievementTracker.LastWasp = __state.Item1;
            AbilityAchievementTracker.ThermalCaster = __state.Item2;
        }
    }

    [HarmonyPatch(typeof(StatsMod.HitLogic), nameof(StatsMod.HitLogic.SetExplosionOwner))]
    internal static class MissileExplosionAchievementPatch
    {
        [HarmonyPostfix]
        private static void Postfix(Explosion explosion, NetworkBehaviour source)
        {
            if (Net.IsAuthority) AchievementTracker.Run("Returned missile", () => AbilityAchievementTracker.TransferMissile(explosion, source));
        }
    }
}
