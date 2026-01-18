using HarmonyLib;
using Silk;
using Logger = Silk.Logger;
using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
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
                // Use an existing modifier's icon as placeholder
                var existingMods = ModifierManager.instance.GetNonMaxedSurvivalMods();
                Sprite icon = existingMods.Count > 0 ? existingMods[0].data.icon : null;

                // Define perks
                var perks = PerksManager.Instance.GetAllPerkNames().Select(name => (name, PerksManager.Instance.GetDisplayName(name), PerksManager.Instance.GetDescription(name), PerksManager.Instance.GetUpgradeDescription(name), PerksManager.Instance.GetMaxLevel(name))).ToArray();

                var modifiersList = __instance.GetType().GetField("_modifiers", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(__instance) as System.Collections.Generic.List<Modifier> ?? new System.Collections.Generic.List<Modifier>();
                var currModsState = __instance.GetType().GetField("_currModsState", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(__instance) as ModifierManager.NetworkModifier[];

                foreach (var (key, title, description, descriptionPlus, maxLevel) in perks)
                {
                    ModifierData data = ScriptableObject.CreateInstance<ModifierData>();
                    data.key = key;
                    data.title = title;
                    data.description = description;
                    data.descriptionPlus = descriptionPlus;
                    data.maxLevel = maxLevel;
                    data.survival = true;
                    data.versus = false;
                    data.customTiers = false;
                    data.mapEditor = false;
                    data.icon = icon;

                    Modifier modifier = new Modifier(data);
                    modifiersList.Add(modifier);

                    if (currModsState != null)
                    {
                        Array.Resize(ref currModsState, currModsState.Length + 1);
                        currModsState[currModsState.Length - 1] = new ModifierManager.NetworkModifier { levelInVersus = 0, levelInWaves = 0 };
                    }
                }

                __instance.GetType().GetField("_modifiers", BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(__instance, modifiersList);
                __instance.GetType().GetField("_currModsState", BindingFlags.NonPublic | BindingFlags.Instance)?.SetValue(__instance, currModsState);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error adding Shield Ability modifiers: {ex.Message}");
            }
        }
    }

    [HarmonyPatch(typeof(ModifierManager), "SetModLevel")]
    public class ModifierManager_SetModLevel_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(Modifier modifier, GameMode mode, int value)
        {
            if (mode == GameMode.Wave && value > 0)
            {
                PerksManager.Instance.SetPerkLevel(modifier.data.key, value);

                var surgeManager = SurgeGameModeManager.Instance;
                PerksManager.Instance.OnSelected(modifier.data.key);
            }
        }
    }

    [HarmonyPatch(typeof(SurvivalModifierChoiceCard), "SetupCard")]
    public class SurvivalModifierChoiceCard_SetupCard_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(SurvivalModifierChoiceCard __instance, Modifier m, GameLevel gl, int id, bool showTwitchVotes)
        {
            string key = m.data.key;
            var surgeManager = SurgeGameModeManager.Instance;
            if (surgeManager != null && PerksManager.Instance.GetAllPerkNames().Contains(key))
            {
                // Fix localization for custom perks
                var perkNameText = __instance.GetType().GetField("perkNameText", BindingFlags.Public | BindingFlags.Instance)?.GetValue(__instance) as TMP_Text;
                var perkDescriptionText = __instance.GetType().GetField("perkDescriptionText", BindingFlags.Public | BindingFlags.Instance)?.GetValue(__instance) as TMP_Text;

                if (perkNameText != null)
                {
                    perkNameText.text = PerksManager.Instance.GetDisplayName(key);
                }

                if (perkDescriptionText != null)
                {
                    perkDescriptionText.text = PerksManager.Instance.GetDescription(key);
                }
            }
        }
    }

    [HarmonyPatch(typeof(ModifierManager), "GetNonMaxedSurvivalMods")]
    public class ModifierManager_GetNonMaxedSurvivalMods_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(ref List<Modifier> __result)
        {
            var surgeManager = SurgeGameModeManager.Instance;
            if (surgeManager == null) return;

            var filtered = __result.Where(mod =>
            {
                string key = mod.data.key;
                if (PerksManager.Instance.GetAllPerkNames().Contains(key))
                {
                    return PerksManager.Instance.IsAvailable(key);
                }
                return true; // Other perks are always available
            }).ToList();

            // Separate mod and vanilla perks
            var modPerks = filtered.Where(mod => PerksManager.Instance.GetAllPerkNames().Contains(mod.data.key)).ToList();
            var vanillaPerks = filtered.Where(mod => !PerksManager.Instance.GetAllPerkNames().Contains(mod.data.key)).ToList();

            Logger.LogInfo($"Available mod perks: {string.Join(", ", modPerks.Select(m => m.data.key))}");

            var result = new List<Modifier>();

            // Position 1: always mod
            if (modPerks.Count > 0)
            {
                int index = UnityEngine.Random.Range(0, modPerks.Count);
                result.Add(modPerks[index]);
                modPerks.RemoveAt(index);
            }

            // For subsequent positions (up to 5 total perks)
            for (int pos = 2; pos <= 5 && result.Count < 5; pos++)
            {
                float prob = 1f / (1 << (pos - 1)); // 1 / 2^(pos-1)
                bool pickMod = UnityEngine.Random.value < prob;
                if (pickMod && modPerks.Count > 0)
                {
                    int index = UnityEngine.Random.Range(0, modPerks.Count);
                    result.Add(modPerks[index]);
                    modPerks.RemoveAt(index);
                }
                else if (vanillaPerks.Count > 0)
                {
                    int index = UnityEngine.Random.Range(0, vanillaPerks.Count);
                    result.Add(vanillaPerks[index]);
                    vanillaPerks.RemoveAt(index);
                }
            }

            __result = result;
        }
    }
}