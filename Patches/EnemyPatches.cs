using HarmonyLib;
using Unity.Netcode;
using System;

namespace SpiderSurge
{
    [HarmonyPatch(typeof(WaspBrain), "Start")]
    public class WaspBrain_Start_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(WaspBrain __instance)
        {
            if (SurgeGameModeManager.Instance == null || !SurgeGameModeManager.Instance.IsActive) return;
            // Exclude friendly wasps as they are not enemies
            if (__instance is FriendWaspBrain) return;
            __instance.movementForce *= Consts.Values.Enemies.SpeedMultiplier;
        }
    }

    [HarmonyPatch(typeof(RollerBrain), "Start")]
    public class RollerBrain_Start_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(RollerBrain __instance)
        {
            if (SurgeGameModeManager.Instance == null || !SurgeGameModeManager.Instance.IsActive) return;
            __instance.rollPower *= Consts.Values.Enemies.SpeedMultiplier;
        }
    }

    [HarmonyPatch(typeof(WhispBrain), "Start")]
    public class WhispBrain_Start_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(WhispBrain __instance)
        {
            if (SurgeGameModeManager.Instance == null || !SurgeGameModeManager.Instance.IsActive) return;
            __instance.movementForce *= Consts.Values.Enemies.SpeedMultiplier;
        }
    }

    [HarmonyPatch(typeof(KhepriBrain), "Start")]
    public class KhepriBrain_Start_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(KhepriBrain __instance)
        {
            if (SurgeGameModeManager.Instance == null || !SurgeGameModeManager.Instance.IsActive) return;
            __instance.movementForce *= Consts.Values.Enemies.SpeedMultiplier;
        }
    }

    [HarmonyPatch(typeof(HornetShamanBrain), "Start")]
    public class HornetShamanBrain_Start_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(HornetShamanBrain __instance)
        {
            if (SurgeGameModeManager.Instance == null || !SurgeGameModeManager.Instance.IsActive) return;
            __instance.movementForce *= Consts.Values.Enemies.SpeedMultiplier;
        }
    }
    [HarmonyPatch(typeof(NetworkObject), "Spawn", new Type[] { typeof(bool) })]
    public class NetworkObject_Spawn_Patch
    {
        [HarmonyPrefix]
        public static void Prefix(NetworkObject __instance)
        {
            if (SurgeGameModeManager.Instance == null || !SurgeGameModeManager.Instance.IsActive) return;

            // Ensure custom enemies (and any other modded objects) are active when spawned.
            // Many modded prefabs are kept inactive to avoid interference, but many spawners
            // in the game do not explicitly call SetActive(true) on the instantiated instances.
            if (__instance.gameObject != null && !__instance.gameObject.activeSelf)
            {
                // We only activate objects that have an EnemyHealthSystem to avoid side effects on other networked objects.
                if (__instance.TryGetComponent<EnemyHealthSystem>(out _))
                {
                    __instance.gameObject.SetActive(true);
                }
            }
        }
    }
}
