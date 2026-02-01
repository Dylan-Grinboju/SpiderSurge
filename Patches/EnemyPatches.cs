using HarmonyLib;
using UnityEngine;

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
}
