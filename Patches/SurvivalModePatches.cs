using HarmonyLib;
using System;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;

namespace SpiderSurge
{
    [HarmonyPatch(typeof(SurvivalMode), "StartGame")]
    public class SurvivalMode_StartGame_Patch
    {
        [HarmonyPrefix]
        public static void Prefix(ref SurvivalConfig survivalConfig)
        {
            if (ModConfig.enableSurgeMode && survivalConfig != null)
            {
                SurvivalConfig surgeConfig = UnityEngine.Object.Instantiate(survivalConfig);
                surgeConfig.name = survivalConfig.name + "_Surge";

                // Double the enemy budget to spawn twice as many enemies
                surgeConfig.startingBudget *= 2f;
                surgeConfig.budgetPerWave *= 2f;
                surgeConfig.budgetPerPlayer *= 2f;

                survivalConfig = surgeConfig;
            }
        }

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

                // Log all weapons for debugging
                var weapons = ReflectionHelper.GetPrivateField<List<SpawnableWeapon>>(SurvivalMode.instance, "_weapons");
                if (weapons != null)
                {
                    foreach (var sw in weapons)
                    {
                        if (sw != null && sw.weaponObject != null)
                        {
                            Weapon w = sw.weaponObject.GetComponent<Weapon>();
                            if (w != null)
                            {
                                string types = w.type != null ? string.Join(", ", w.type) : "None";
                                Silk.Logger.LogInfo($"Weapon: {w.serializationWeaponName}, Types: {types}");
                            }
                        }
                    }
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

                // At wave 30, set flag for special perk selection
                if (value == 30)
                {
                    PerksManager.Instance.IsPost30WavePerkSelection = true;
                }

                // At wave 60, set flag for special perk selection
                if (value == 60)
                {
                    PerksManager.Instance.IsPost60WavePerkSelection = true;
                }

                // Update InterdimensionalStorageAbility cache
                var abilities = UnityEngine.Object.FindObjectsOfType<InterdimensionalStorageAbility>();
                foreach (var ab in abilities)
                {
                    ab.UpdateCachedModifierLevels();
                }
            }
        }
    }

}