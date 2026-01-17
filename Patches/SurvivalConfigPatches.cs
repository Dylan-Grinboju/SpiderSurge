using HarmonyLib;
using Silk;
using Logger = Silk.Logger;
using UnityEngine.SceneManagement;
using System;
using System.Reflection;

namespace SpiderSurge
{
    [HarmonyPatch(typeof(SurvivalConfig), "GetHighScore")]
    public class SurvivalConfig_GetHighScore_Patch
    {
        [HarmonyPrefix]
        public static bool Prefix(SurvivalConfig __instance, ref float __result)
        {
            if (ModConfig.enableSurgeMode)
            {
                __result = GameSaveWrapper.Instance.Load<float>(__instance.name + "-SurgeHS", 0f);
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(SurvivalConfig), "SetHighScore")]
    public class SurvivalConfig_SetHighScore_Patch
    {
        [HarmonyPrefix]
        public static bool Prefix(SurvivalConfig __instance, float score)
        {
            if (ModConfig.enableSurgeMode)
            {
                GameSaveWrapper.Instance.Save<float>(__instance.name + "-SurgeHS", score);
                return false; // Prevent original save
            }
            return true;
        }
    }
}