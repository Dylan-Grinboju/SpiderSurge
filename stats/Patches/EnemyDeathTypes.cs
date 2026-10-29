using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Logger = Silk.Logger;
using HarmonyLib;
using SpiderSurge.Common;


namespace StatsMod
{
    // Every enemy death ends here: direct damage, lava (Disintegrate), roller struts, stuck in walls.
    // Explode can be called more than once, the same checks as the original keep only the call that kills.
    [HarmonyPatch(typeof(EnemyHealthSystem), "Explode")]
    class EnemyDeathCountPatch
    {
        static void Prefix(EnemyHealthSystem __instance)
        {
            try
            {
                if (GameFields.EnemyExploding(__instance) || __instance.ignoreIndirectDamage) return;
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

    // A launched saw disc is a shot of the launcher's player
    [HarmonyPatch(typeof(DiscLauncher), "LaunchDisc")]
    class DiscLauncherLaunchDiscPatch
    {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase original) =>
            AccuracyProjectilePatch.TrackSpawns(instructions, original, AccessTools.Method(typeof(DiscLauncherLaunchDiscPatch), nameof(Launched)),
                "saw disc accuracy is not tracked");

        static GameObject Launched(GameObject disc, DiscLauncher launcher)
        {
            try
            {
                if (!disc.TryGetComponent<SawDisc>(out _)) return disc;
                AccuracyTracker.Fire(launcher, disc);
            }
            catch (System.Exception ex)
            {
                Logger.LogError($"Error tracking a launched saw disc: {ex.Message}");
            }
            return disc;
        }
    }

}
