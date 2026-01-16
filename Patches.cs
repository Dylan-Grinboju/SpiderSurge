using HarmonyLib;
using Silk;
using Logger = Silk.Logger;
using UnityEngine.SceneManagement;
using System;
using System.Reflection;

namespace SpiderSurge
{
    [HarmonyPatch(typeof(SurvivalMode), "StartGame")]
    public class SurvivalMode_StartGame_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(bool __result, SurvivalConfig survivalConfig)
        {
            if (__result && ModConfig.enableSurgeMode)
            {
                SurgeModeManager.Instance.SetActive(true);
                Logger.LogInfo("Surge mode started");
                // Refresh high score display to show Surge scores
                var eventField = typeof(SurvivalMode).GetField("onHighScoreUpdated", BindingFlags.Public | BindingFlags.Static);
                if (eventField != null)
                {
                    var action = (Action<int>)eventField.GetValue(null);
                    action?.Invoke(SurvivalMode.instance.GetHighScore());
                }
            }
        }
    }

    [HarmonyPatch(typeof(SurvivalMode), "StopGameMode")]
    public class SurvivalMode_StopGameMode_Patch
    {
        [HarmonyPostfix]
        public static void Postfix()
        {
            if (SurgeModeManager.Instance.IsActive)
            {
                Logger.LogInfo("Surge mode ended");
                SurgeModeManager.Instance.SetActive(false);
            }
        }
    }

    [HarmonyPatch(typeof(LobbyController), "OnSceneLoaded")]
    public class LobbyController_OnSceneLoaded_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(Scene scene)
        {
            if (scene.name == "Lobby" && SurgeModeManager.Instance.IsActive)
            {
                Logger.LogInfo("Surge mode ended (lobby entered)");
                SurgeModeManager.Instance.SetActive(false);
            }
        }
    }

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

    [HarmonyPatch(typeof(EOSLeaderboards), "SetRecord", new Type[] { typeof(uint), typeof(int) })]
    public class EOSLeaderboards_SetRecord_Patch
    {
        [HarmonyPrefix]
        public static bool Prefix()
        {
            if (SurgeModeManager.Instance.IsActive)
            {
                return false; // Skip leaderboard submission for Surge mode
            }
            return true;
        }
    }
}