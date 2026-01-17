using HarmonyLib;
using Silk;
using Logger = Silk.Logger;
using System;
using System.Reflection;
using UnityEngine;
using TMPro;

namespace SpiderSurge
{
    [HarmonyPatch(typeof(ModifierManager), "Awake")]
    public class ModifierManager_Awake_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(ModifierManager __instance)
        {
            try
            {
                // Create a new ModifierData for Shield Ability
                ModifierData shieldData = ScriptableObject.CreateInstance<ModifierData>();
                shieldData.key = "shieldAbility";
                shieldData.title = new I2.Loc.LocalizedString { mTerm = "Shield Ability" };
                shieldData.description = new I2.Loc.LocalizedString { mTerm = "Unlocks the shield ability for use in survival mode" };
                shieldData.descriptionPlus = new I2.Loc.LocalizedString { mTerm = "Unlocks the shield ability for use in survival mode" };
                shieldData.maxLevel = 1;
                shieldData.survival = true;
                shieldData.versus = false;
                shieldData.customTiers = false;
                shieldData.mapEditor = false;

                // Use an existing modifier's icon as placeholder
                var existingMods = ModifierManager.instance.GetNonMaxedSurvivalMods();
                if (existingMods.Count > 0)
                {
                    shieldData.icon = existingMods[0].data.icon;
                }
                else
                {
                    shieldData.icon = null;
                }

                // Add to the modifiers list
                Modifier shieldModifier = new Modifier(shieldData);
                __instance.GetType().GetField("_modifiers", BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(__instance,
                    new System.Collections.Generic.List<Modifier>(__instance.GetType().GetField("_modifiers", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(__instance) as System.Collections.Generic.List<Modifier> ?? new System.Collections.Generic.List<Modifier>()) { shieldModifier });

                // Also update the currModsState array
                var currModsState = __instance.GetType().GetField("_currModsState", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(__instance) as ModifierManager.NetworkModifier[];
                if (currModsState != null)
                {
                    Array.Resize(ref currModsState, currModsState.Length + 1);
                    currModsState[currModsState.Length - 1] = new ModifierManager.NetworkModifier { levelInVersus = 0, levelInWaves = 0 };
                    __instance.GetType().GetField("_currModsState", BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(__instance, currModsState);
                }

                Logger.LogInfo("Shield Ability modifier added to ModifierManager");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error adding Shield Ability modifier: {ex.Message}");
            }
        }
    }

    [HarmonyPatch(typeof(ModifierManager), "SetModLevel")]
    public class ModifierManager_SetModLevel_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(Modifier modifier, GameMode mode, int value)
        {
            if (modifier.data.key == "shieldAbility" && mode == GameMode.Wave && value > 0)
            {
                Logger.LogInfo("Shield ability modifier selected - enabling shield ability");
                AbilityManager.EnableShieldAbility();
            }
        }
    }
}