using HarmonyLib;
using Logger = Silk.Logger;
using System;
using System.Reflection;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using System.Collections;
using Unity.Netcode;

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
                // Reset perk selection for new game
                PerksManager.Instance.ResetPerks();
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
                // Reset shield cooldown for all players after each wave
                foreach (var kvp in ShieldAbility.playerShields)
                {
                    kvp.Value.SetCooldownToZero();
                }
            }
        }
    }

}