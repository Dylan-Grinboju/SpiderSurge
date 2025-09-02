using HarmonyLib;
using Silk;
using Logger = Silk.Logger;
using System;
using UnityEngine;
using System.Collections.Generic;

namespace StatsMod
{

    [HarmonyPatch(typeof(SurvivalMode), "StartGame")]
    public class SurvivalModeStartPatch
    {
        static void Postfix(SurvivalMode __instance, SurvivalConfig survivalConfig, bool __result)
        {
            try
            {
                if (__result)
                {
                    StatsManager.Instance.StartSurvivalSession();
                    Logger.LogInfo("Survival mode started via StatsManager");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error in SurvivalMode.StartGame patch: {ex.Message}");
            }
        }
    }

    // Patch SurvivalMode.StopGameMode to track when survival sessions end
    [HarmonyPatch(typeof(SurvivalMode), "StopGameMode")]
    public class SurvivalModeStopPatch
    {
        static void Prefix(SurvivalMode __instance)
        {
            try
            {
                if (__instance.GameModeActive())
                {
                    StatsManager.Instance.StopSurvivalSession();
                    Logger.LogInfo("Survival mode stopped via StatsManager");

                    UIManager.AutoPullHUD();
                    Logger.LogInfo("Stats HUD auto-pulled after survival mode ended");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error in SurvivalMode.StopGameMode patch: {ex.Message}");
            }
        }
    }

}
