using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Logger = Silk.Logger;
using HarmonyLib;


namespace StatsMod
{
    // All AccessTools calls are done once at static initialization to avoid per-call overhead.
    internal static class EnemyReflectionCache
    {
        // EnemyHealthSystem fields
        public static readonly AccessTools.FieldRef<EnemyHealthSystem, bool> EnemyExploding =
            AccessTools.FieldRefAccess<EnemyHealthSystem, bool>("_exploding");
        public static readonly AccessTools.FieldRef<EnemyHealthSystem, float> EnemyImmuneTime =
            AccessTools.FieldRefAccess<EnemyHealthSystem, float>("_immuneTime");
    }

    // Every enemy death ends here: direct damage, lava (Disintegrate), roller struts, stuck in walls.
    // Explode can be called more than once, the same checks as the original keep only the call that kills.
    [HarmonyPatch(typeof(EnemyHealthSystem), "Explode")]
    class EnemyDeathCountPatch
    {
        static void Prefix(EnemyHealthSystem __instance)
        {
            try
            {
                if (EnemyReflectionCache.EnemyExploding(__instance) || __instance.ignoreIndirectDamage) return;
                HitLogic.OnEnemyDeath(__instance);
            }
            catch (System.Exception ex)
            {
                Logger.LogError($"Error recording enemy kill: {ex.Message}");
            }
        }
    }

    [HarmonyPatch(typeof(EnemyHealthSystem), "Disintegrate")]
    class EnemyDisintegratePatch
    {
        static void Prefix(EnemyHealthSystem __instance)
        {
            try
            {
                if (__instance.IsDead() || __instance.ignoreIndirectDamage) return;
                HitLogic.OnEnemyDisintegrating(__instance);
            }
            catch (System.Exception ex)
            {
                Logger.LogError($"Error tracking enemy lava contact: {ex.Message}");
            }
        }
    }

    [HarmonyPatch(typeof(SpiderSurge.SwallowedTarget), nameof(SpiderSurge.SwallowedTarget.BeginEnemy))]
    class EnemySwallowedPatch
    {
        static void Prefix(EnemyHealthSystem enemy, PlayerController caster)
        {
            try
            {
                if (enemy == null || SpiderSurge.SwallowedTarget.IsSwallowed(enemy.gameObject)) return;
                HitLogic.OnEnemySwallowed(enemy, caster);
            }
            catch (System.Exception ex)
            {
                Logger.LogError($"Error recording an enemy swallowed by the Black Hole: {ex.Message}");
            }
        }
    }

    [HarmonyPatch(typeof(EnemyHealthSystem), "DestroyShield")]
    class EnemyShieldDestroyedPatch
    {
        static void Prefix(EnemyHealthSystem __instance)
        {
            try
            {
                HitLogic.OnEnemyShieldDestroyed(__instance);
            }
            catch (System.Exception ex)
            {
                Logger.LogError($"Error recording enemy shield break: {ex.Message}");
            }
        }
    }

    // The launcher's holder owns the disc, recorded as soon as LaunchDisc creates it
    [HarmonyPatch(typeof(DiscLauncher), "LaunchDisc")]
    class DiscLauncherLaunchDiscPatch
    {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase original) =>
            AccuracyProjectilePatch.TrackSpawns(instructions, original, AccessTools.Method(typeof(DiscLauncherLaunchDiscPatch), nameof(Launched)),
                "saw disc kills and accuracy are not tracked");

        static GameObject Launched(GameObject disc, DiscLauncher launcher)
        {
            try
            {
                if (!disc.TryGetComponent<SawDisc>(out _)) return disc;
                HitLogic.RegisterDiscOwner(disc, HitLogic.WeaponPlayer(launcher), launcher);
                AccuracyTracker.Fire(launcher, disc);
            }
            catch (System.Exception ex)
            {
                Logger.LogError($"Error tracking a launched saw disc: {ex.Message}");
            }
            return disc;
        }
    }

    // The player who equips a weapon owns it until someone else equips it, also after it is thrown
    [HarmonyPatch(typeof(Weapon), "Equip")]
    class WeaponEquipPatch
    {
        static void Postfix(Weapon __instance, WeaponManager wm)
        {
            try
            {
                AccuracyTracker.Forget(__instance);
                PlayerController playerController = wm != null && wm.healthSystem != null ? wm.healthSystem.GetComponentInParent<PlayerController>() : null;
                HitLogic.SetWeaponPlayer(__instance, playerController);
            }
            catch (System.Exception ex)
            {
                Logger.LogError($"Error in WeaponEquipPatch: {ex.Message}");
            }
        }
    }

}
