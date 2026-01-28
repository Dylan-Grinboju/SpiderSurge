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
                string key = modifier.data.key;
                // Handle all surge perks (abilities and upgrades)
                if (PerksManager.Instance.GetAllPerkNames().Contains(key))
                {
                    // If selecting level 2 when level was 0, also set level 1
                    if (value == 2 && PerksManager.Instance.GetPerkLevel(key) == 0)
                    {
                        PerksManager.Instance.SetPerkLevel(key, 1);
                    }
                    PerksManager.Instance.SetPerkLevel(key, value);
                    PerksManager.Instance.OnSelected(key);

                    // If this was an ability perk, mark ability selection as occurred
                    if (PerksManager.Instance.IsAbilityPerk(modifier.data.key))
                    {
                        if (PerksManager.Instance.IsFirstNormalPerkSelection)
                        {
                            PerksManager.Instance.IsFirstNormalPerkSelection = false;
                        }
                    }

                    // If this was selected during post-30 wave perk selection, reset the flag
                    if (PerksManager.Instance.IsPost30WavePerkSelection)
                    {
                        PerksManager.Instance.IsPost30WavePerkSelection = false;
                    }

                    // If this was selected during post-60 wave perk selection, reset the flag
                    if (PerksManager.Instance.IsPost60WavePerkSelection)
                    {
                        PerksManager.Instance.IsPost60WavePerkSelection = false;
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
                    string displayName = PerksManager.Instance.GetDisplayName(key);
                    if (m.levelInSurvival == 1 && PerksManager.Instance.GetPerkLevel(key) == 0)
                    {
                        displayName += " Level 2";
                    }
                    else if (m.levelInSurvival == 1)
                    {
                        displayName += " +";
                    }
                    perkNameText.text = displayName;
                }

                if (perkDescriptionText != null)
                {
                    string description = PerksManager.Instance.GetDescription(key);
                    if (m.levelInSurvival == 1 && PerksManager.Instance.GetPerkLevel(key) == 0)
                    {
                        description = "Unlocks both levels of " + description.ToLower();
                    }
                    else if (m.levelInSurvival == 1)
                    {
                        description = PerksManager.Instance.GetUpgradeDescription(key);
                    }
                    perkDescriptionText.text = description;
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

            HashSet<string> allowedPerks = null;

            if (PerksManager.Instance.IsPost60WavePerkSelection)
            {
                allowedPerks = new HashSet<string>();

                // Add ults of not unlocked abilities
                foreach (var ult in PerksManager.Instance.GetAbilityUltimatePerkNames())
                {
                    string baseAbility = ult.Replace("Ultimate", "");
                    if (PerksManager.Instance.GetPerkLevel(baseAbility) == 0)
                    {
                        allowedPerks.Add(ult);
                    }
                }

                // Add one random unchosen upgrade perk if available
                var unchosenUpgrades = PerksManager.Instance.GetUpgradePerkNames()
                    .Where(p => PerksManager.Instance.GetPerkLevel(p) == 0 && PerksManager.Instance.IsAvailable(p))
                    .ToList();
                if (unchosenUpgrades.Any())
                {
                    string randomUpgrade = unchosenUpgrades[UnityEngine.Random.Range(0, unchosenUpgrades.Count)];
                    allowedPerks.Add(randomUpgrade);
                }
            }

            var filtered = __result.Where(mod =>
            {
                string key = mod.data.key;

                if (PerksManager.Instance.GetAllPerkNames().Contains(key))
                {
                    if (allowedPerks != null)
                    {
                        // For post-60, only show allowed perks
                        return allowedPerks.Contains(key);
                    }
                    else if (PerksManager.Instance.IsFirstNormalPerkSelection)
                    {
                        // First perk selection: only show ability perks
                        return PerksManager.Instance.IsAbilityPerk(key) && PerksManager.Instance.IsAvailable(key);
                    }
                    else if (PerksManager.Instance.IsPost30WavePerkSelection)
                    {
                        // Post-30 wave perk selection: show ult and unchosen upgrade perks
                        string ult = PerksManager.Instance.GetChosenAbilityUltimate();
                        if (key == ult && PerksManager.Instance.IsAvailable(key))
                        {
                            return true; // Ult first
                        }
                        // Then unchosen upgrade perks
                        return PerksManager.Instance.IsUpgradePerk(key) && PerksManager.Instance.GetPerkLevel(key) == 0 && PerksManager.Instance.IsAvailable(key);
                    }
                    else
                    {
                        // Subsequent selections: show upgrade perks (non-ability mod perks)
                        return PerksManager.Instance.IsUpgradePerk(key) && PerksManager.Instance.IsAvailable(key);
                    }
                }

                // Vanilla perks: exclude on first selection (abilities only), include on subsequent or when no upgrade perks available for post-60
                return !PerksManager.Instance.IsFirstNormalPerkSelection || (allowedPerks != null && !allowedPerks.Any(p => PerksManager.Instance.IsUpgradePerk(p)));
            }).OrderBy(mod => 
            {
                string key = mod.data.key;
                if (PerksManager.Instance.IsPost30WavePerkSelection)
                {
                    string ult = PerksManager.Instance.GetChosenAbilityUltimate();
                    if (key == ult) return 0; // Ult first
                    else return 1; // Others after
                }
                else if (PerksManager.Instance.IsPost60WavePerkSelection)
                {
                    if (PerksManager.Instance.GetAbilityUltimatePerkNames().Contains(key)) return 0; // Ults first
                    else return 1; // Upgrade after
                }
                return 0; // Default order
            }).ToList();

            __result = filtered;

            // Apply perk luck: randomly upgrade some level 1 choices to level 2 choices
            if (PerksManager.Instance.GetPerkLevel("perkLuck") > 0)
            {
                float chance = PerksManager.Instance.GetPerkLuckChance();
                foreach (var mod in __result)
                {
                    string key = mod.data.key;
                    if (PerksManager.Instance.IsUpgradePerk(key) && PerksManager.Instance.GetMaxLevel(key) > 1 && PerksManager.Instance.GetPerkLevel(key) == 0 && UnityEngine.Random.value < chance)
                    {
                        mod.levelInSurvival = 1; // Make it a level 2 choice
                    }
                }
            }

            Logger.LogInfo($"Available perks after filtering: {string.Join(", ", __result.Select(m => m.data.key))}");
        }
    }
}