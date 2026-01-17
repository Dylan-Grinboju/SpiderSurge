using HarmonyLib;
using Silk;
using Logger = Silk.Logger;
using System;
using System.Reflection;
using UnityEngine;
using TMPro;
using I2.Loc;

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
                shieldData.title = "Shield Ability";
                shieldData.description = "Unlocks the shield ability for use in survival mode";
                shieldData.descriptionPlus = "Unlocks the shield ability for use in survival mode";
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
                AbilityManager.EnableShieldAbility();
            }
        }
    }

    [HarmonyPatch(typeof(SurvivalModifierChoiceCard), "SetupCard")]
    public class SurvivalModifierChoiceCard_SetupCard_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(SurvivalModifierChoiceCard __instance, Modifier m, GameLevel gl, int id, bool showTwitchVotes)
        {
            if (m.data.key == "shieldAbility")
            {

                // Fix localization for shield ability
                var perkNameText = __instance.GetType().GetField("perkNameText", BindingFlags.Public | BindingFlags.Instance)?.GetValue(__instance) as TMP_Text;
                var perkDescriptionText = __instance.GetType().GetField("perkDescriptionText", BindingFlags.Public | BindingFlags.Instance)?.GetValue(__instance) as TMP_Text;


                if (perkNameText != null)
                {
                    if (string.IsNullOrEmpty(perkNameText.text))
                    {
                        perkNameText.text = m.data.title.ToString();
                        if (string.IsNullOrEmpty(perkNameText.text))
                        {
                            perkNameText.text = "Shield Ability";
                        }
                    }
                    else
                    {
                        Logger.LogInfo("perkNameText was not empty, skipping");
                    }
                }
                else
                {
                    Logger.LogError("perkNameText field not found");
                }

                if (perkDescriptionText != null)
                {
                    if (string.IsNullOrEmpty(perkDescriptionText.text))
                    {
                        perkDescriptionText.text = m.data.description.ToString();
                        if (string.IsNullOrEmpty(perkDescriptionText.text))
                        {
                            perkDescriptionText.text = "Unlocks the shield ability for use in survival mode";
                        }
                    }
                    else
                    {
                        Logger.LogInfo("perkDescriptionText was not empty, skipping");
                    }
                }
                else
                {
                    Logger.LogError("perkDescriptionText field not found");
                }
            }
        }
    }
}