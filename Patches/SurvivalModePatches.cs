using HarmonyLib;
using Silk;
using Logger = Silk.Logger;
using UnityEngine.SceneManagement;
using System;
using System.Reflection;
using UnityEngine.InputSystem;

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
                SurgeGameModeManager.Instance.SetActive(true);
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

    [HarmonyPatch(typeof(SurvivalMode), "set_CurrentWave")]
    public class SurvivalMode_set_CurrentWave_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(int value)
        {
            if (SurgeGameModeManager.Instance.IsActive && value > 0)
            {
                // Wave advanced, add charges to all players
                PlayerInput[] players = UnityEngine.Object.FindObjectsOfType<PlayerInput>();
                foreach (PlayerInput player in players)
                {
                    SurgeGameModeManager.Instance.AddShieldCharge(player);
                }
                Logger.LogInfo($"Added shield charges after wave {value}");
            }
        }
    }

    // [HarmonyPatch(typeof(SurvivalMode), "SetNextPerkWave")]
    // public class SurvivalMode_SetNextPerkWave_Patch
    // {
    //     [HarmonyPostfix]
    //     public static void Postfix(SurvivalMode __instance)
    //     {
    //         if (SurgeGameModeManager.Instance.IsActive)
    //         {
    //             // In surge mode, advance map and perks every 1 wave instead of 2 + players
    //             var nextPerkWaveField = typeof(SurvivalMode).GetField("_nextPerkChoiceWave", BindingFlags.NonPublic | BindingFlags.Instance);
    //             if (nextPerkWaveField != null)
    //             {
    //                 int currentWave = __instance.CurrentWave;
    //                 nextPerkWaveField.SetValue(__instance, currentWave + 1);
    //                 Logger.LogInfo($"Surge mode: Set next perk/map advancement to wave {currentWave + 1}");
    //             }
    //         }
    //     }
    // }
}