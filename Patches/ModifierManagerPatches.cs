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

                // Register ALL perks (abilities + upgrades) with ModifierManager so they have valid IDs
                var allPerks = PerksManager.Instance.GetAllPerkNames()
                    .Select(name => (
                        name,
                        PerksManager.Instance.GetDisplayName(name),
                        PerksManager.Instance.GetDescription(name),
                        PerksManager.Instance.GetUpgradeDescription(name),
                        PerksManager.Instance.GetMaxLevel(name)
                    )).ToArray();

                var modifiersList = __instance.GetType().GetField("_modifiers", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(__instance) as System.Collections.Generic.List<Modifier> ?? new System.Collections.Generic.List<Modifier>();
                var currModsState = __instance.GetType().GetField("_currModsState", BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(__instance) as ModifierManager.NetworkModifier[];

                foreach (var (key, title, description, descriptionPlus, maxLevel) in allPerks)
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

                Logger.LogInfo($"Registered {allPerks.Length} surge perks with ModifierManager (abilities + upgrades)");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error adding surge modifiers: {ex.Message}");
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
                // Handle all surge perks (abilities and upgrades)
                if (PerksManager.Instance.GetAllPerkNames().Contains(modifier.data.key))
                {
                    PerksManager.Instance.SetPerkLevel(modifier.data.key, value);
                    PerksManager.Instance.OnSelected(modifier.data.key);

                    // If this was an ability perk, mark ability selection as occurred
                    if (PerksManager.Instance.IsAbilityPerk(modifier.data.key))
                    {
                        if (PerksManager.Instance.IsFirstNormalPerkSelection)
                        {
                            PerksManager.Instance.IsFirstNormalPerkSelection = false;
                            Logger.LogInfo("First perk selection completed");
                        }
                        Logger.LogInfo($"Ability {modifier.data.key} selected");
                    }
                }
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
            if (surgeManager == null || !surgeManager.IsActive) return;

            var filtered = __result.Where(mod =>
            {
                string key = mod.data.key;

                // Check if this is a surge perk
                if (PerksManager.Instance.GetAllPerkNames().Contains(key))
                {
                    if (PerksManager.Instance.IsFirstNormalPerkSelection)
                    {
                        // First perk selection: only show ability perks
                        return PerksManager.Instance.IsAbilityPerk(key) && PerksManager.Instance.IsAvailable(key);
                    }
                    else
                    {
                        // Subsequent selections: show upgrade perks (non-ability mod perks)
                        return PerksManager.Instance.IsUpgradePerk(key) && PerksManager.Instance.IsAvailable(key);
                    }
                }

                // Vanilla perks: exclude on first selection (abilities only), include on subsequent
                return !PerksManager.Instance.IsFirstNormalPerkSelection;
            }).ToList();

            __result = filtered;

            Logger.LogInfo($"Available perks after filtering: {string.Join(", ", __result.Select(m => m.data.key))}");
        }
    }
}