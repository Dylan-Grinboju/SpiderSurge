using HarmonyLib;
using Logger = Silk.Logger;
using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
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
        [HarmonyPrefix]
        public static void Prefix(Modifier modifier, GameMode mode, ref int value)
        {
            if (mode == GameMode.Wave && value == 1 && modifier != null && modifier.data != null)
            {
                string key = modifier.data.key;
                bool isLucky = ModifierManager_GetNonMaxedSurvivalMods_Patch.LuckyPerkKeys.Contains(key);
                
                if (isLucky)
                {
                    bool isSurgePerk = PerksManager.Instance.GetAllPerkNames().Contains(key);
                    bool isLevel0 = isSurgePerk ? PerksManager.Instance.GetPerkLevel(key) == 0 : modifier.levelInSurvival == 0;

                    if (isLevel0)
                    {
                        value = 2;
                        Logger.LogInfo($"[Perk Luck] Applied lucky upgrade for '{key}', setting level to 2");
                    }
                }
            }
        }

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
            var surgeManager = SurgeGameModeManager.Instance;
            if (surgeManager == null || !surgeManager.IsActive) return;

            string key = m.data.key;
            bool isSurgePerk = PerksManager.Instance.GetAllPerkNames().Contains(key);
            bool isLucky = ModifierManager_GetNonMaxedSurvivalMods_Patch.LuckyPerkKeys.Contains(key);

            if (!isSurgePerk && !isLucky) return;

            // Helper to find TMP_Text with multiple possible names and binding flags
            TMP_Text GetTextComponent(string[] names)
            {
                foreach (var name in names)
                {
                    var field = __instance.GetType().GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    if (field != null)
                    {
                        return field.GetValue(__instance) as TMP_Text;
                    }
                }
                return null;
            }

            var perkNameText = GetTextComponent(new[] { "perkNameText" });
            var perkDescriptionText = GetTextComponent(new[] { "perkDescriptionText" });

            if (perkNameText == null) Logger.LogWarning($"[Surge] Could not find perkNameText for card {key}");
            if (perkDescriptionText == null) Logger.LogWarning($"[Surge] Could not find perkDescriptionText for card {key}");

            if (isSurgePerk)
            {
                if (perkNameText != null)
                {
                    string displayName = PerksManager.Instance.GetDisplayName(key);

                    if (m.levelInSurvival == 1)
                    {
                        displayName += " +";
                    }
                    perkNameText.text = displayName;
                }

                if (perkDescriptionText != null)
                {
                    string description = PerksManager.Instance.GetDescription(key);

                    if (m.levelInSurvival == 1)
                    {
                        description = PerksManager.Instance.GetUpgradeDescription(key);
                    }

                    if (isLucky)
                    {
                        description += "\n<color=#FFD700>Lucky Upgrade</color>";
                    }

                    if (PerksManager.Instance.GetAbilityUltimatePerkNames().Contains(key) && PerksManager.Instance.IsPost60WavePerkSelection)
                    {
                        description += "\n<color=#FFD700>Swap Ability!</color>";
                    }

                    perkDescriptionText.text = description;
                }
            }
            else if (isLucky) // Vanilla but Lucky
            {
                if (perkDescriptionText != null)
                {
                    perkDescriptionText.text += "\n<color=#FFD700>Lucky Upgrade</color>";
                }
            }
        }
    }

    [HarmonyPatch(typeof(ModifierManager), "GetNonMaxedSurvivalMods")]
    public class ModifierManager_GetNonMaxedSurvivalMods_Patch
    {
        public static HashSet<string> LuckyPerkKeys = new HashSet<string>();

        [HarmonyPostfix]
        public static void Postfix(ModifierManager __instance, ref List<Modifier> __result)
        {
            var surgeManager = SurgeGameModeManager.Instance;
            if (surgeManager == null || !surgeManager.IsActive) return;

            var perksManager = PerksManager.Instance;
            var filteredList = new List<Modifier>();
            var processedKeys = new HashSet<string>();

            // Only filter for validity/availability. DO NOT select/limit count here.
            foreach (var mod in __result)
            {
                if (mod == null || mod.data == null) continue;
                string key = mod.data.key;

                // Deduplicate
                if (processedKeys.Contains(key)) continue;
                processedKeys.Add(key);

                // Custom Availability Check (Dependencies, etc)
                if (perksManager.GetAllPerkNames().Contains(key))
                {
                    // Special exception: In Post-60 phase, allow Ultimate perks to appear
                    // even if their base ability is not unlocked (to allow swapping).
                    // We knowingly bypass IsAvailable's dependency check here.
                    bool isUlt = perksManager.GetAbilityUltimatePerkNames().Contains(key);
                    bool isPost60 = perksManager.IsPost60WavePerkSelection;

                    if (isPost60 && isUlt)
                    {
                        // Specifically check max level, since we are bypassing IsAvailable
                        if (perksManager.GetPerkLevel(key) >= perksManager.GetMaxLevel(key)) continue;
                    }
                    else
                    {
                        if (!perksManager.IsAvailable(key)) continue;
                    }
                }

                // Add to the valid list (Vanilla perks pass through here)
                filteredList.Add(mod);
            }

            __result = filteredList;
        }
    }

    [HarmonyPatch(typeof(SelectionHelper), "PickRandomModifiers")]
    public class SelectionHelper_PickRandomModifiers_Patch
    {
        [HarmonyPrefix]
        public static bool Prefix(int count, List<Modifier> availableModifiers, ref List<Modifier> __result)
        {
            // If list is null or empty, return empty
            if (availableModifiers == null || availableModifiers.Count == 0)
            {
                __result = new List<Modifier>();
                return false;
            }

            var surgeManager = SurgeGameModeManager.Instance;
            // If not surge mode, treat as standard robust shuffle (fix duplicates bug)
            if (surgeManager == null || !surgeManager.IsActive)
            {
                var mn_selected = new List<Modifier>();
                var mn_seenKeys = new HashSet<string>();
                var mn_shuffled = availableModifiers.OrderBy(x => UnityEngine.Random.value);

                foreach (var mod in mn_shuffled)
                {
                    if (mn_selected.Count >= count) break;
                    if (mod != null && mod.data != null && !mn_seenKeys.Contains(mod.data.key))
                    {
                        mn_selected.Add(mod);
                        mn_seenKeys.Add(mod.data.key);
                    }
                }
                __result = mn_selected;
                return false;
            }

            // --- SURGE MODE SELECTION LOGIC ---
            var perksManager = PerksManager.Instance;

            // 1. Categorize available modifiers
            List<Modifier> selectedAbilityUlt = new List<Modifier>();
            List<Modifier> otherAbilityUlts = new List<Modifier>();
            List<Modifier> abilities = new List<Modifier>();
            List<Modifier> modPerks = new List<Modifier>();
            List<Modifier> vanillaPerks = new List<Modifier>();

            string currentUlt = perksManager.GetChosenAbilityUltimate();

            foreach (var mod in availableModifiers)
            {
                string key = mod.data.key;

                if (perksManager.IsAbilityPerk(key))
                {
                    abilities.Add(mod);
                }
                else if (perksManager.GetAbilityUltimatePerkNames().Contains(key))
                {
                    if (key == currentUlt) selectedAbilityUlt.Add(mod);
                    else otherAbilityUlts.Add(mod);
                }
                else if (perksManager.IsUpgradePerk(key))
                {
                    modPerks.Add(mod);
                }
                else
                {
                    vanillaPerks.Add(mod);
                }
            }

            // 2. Shuffle sub-lists
            vanillaPerks = vanillaPerks.OrderBy(_ => UnityEngine.Random.value).ToList();
            modPerks = modPerks.OrderBy(_ => UnityEngine.Random.value).ToList();
            abilities = abilities.OrderBy(_ => UnityEngine.Random.value).ToList();
            selectedAbilityUlt = selectedAbilityUlt.OrderBy(_ => UnityEngine.Random.value).ToList();
            otherAbilityUlts = otherAbilityUlts.OrderBy(_ => UnityEngine.Random.value).ToList();

            // 3. Construct Final Selection based on Phases
            List<Modifier> finalSelection = new List<Modifier>();
            int targetCount = count; // Use the requested count, always between 2-5 inclusive

            if (perksManager.IsPost60WavePerkSelection)
            {
                Logger.LogInfo("Phase 1: Post 60 - Other Ults (Swap) -> Current Ult (if avail) -> Mods -> Vanilla");
                // Reserve 1 slot for the current ult or mod perk
                AddUpTo(finalSelection, otherAbilityUlts, Math.Max(0, targetCount - 1));
                Logger.LogInfo($"Selected Other Ults: {string.Join(", ", finalSelection.Select(m => m.data.key))}");
                // Try to fill the last slot with the current ability's ultimate if it wasn't picked yet
                AddUpTo(finalSelection, selectedAbilityUlt, targetCount);
                Logger.LogInfo($"Selected Current Ult: {string.Join(", ", finalSelection.Select(m => m.data.key))}");
                // If current ult is maxed/picked, fill with mod perks
                AddUpTo(finalSelection, modPerks, targetCount);
                AddUpTo(finalSelection, vanillaPerks, targetCount);

                // Fill remainder with any remaining otherUlts if we reserved space and didn't fill it
                AddUpTo(finalSelection, otherAbilityUlts, targetCount);
            }
            else if (perksManager.IsPost30WavePerkSelection)
            {
                Logger.LogInfo("Phase 2: Post 30 - Current Ult -> Mods -> Vanilla");
                AddUpTo(finalSelection, selectedAbilityUlt, targetCount);
                AddUpTo(finalSelection, modPerks, targetCount);
                AddUpTo(finalSelection, vanillaPerks, targetCount);
            }
            else if (perksManager.IsFirstNormalPerkSelection)
            {
                Logger.LogInfo("Phase 3: First - Abilities Only");
                AddUpTo(finalSelection, abilities, targetCount);
                // Fallback if not enough abilities (couldn't happen)
                AddUpTo(finalSelection, modPerks, targetCount);
                AddUpTo(finalSelection, vanillaPerks, targetCount);
            }
            else
            {
                Logger.LogInfo("Phase 4: Standard - Mix");
                List<Modifier> mixedPerks = new List<Modifier>();
                mixedPerks.AddRange(modPerks);
                mixedPerks.AddRange(vanillaPerks);

                mixedPerks = mixedPerks.OrderBy(_ => UnityEngine.Random.value).ToList();
                AddUpTo(finalSelection, mixedPerks, targetCount);
            }

            // 4. Apply Perk Luck
            ApplyPerkLuck(perksManager, finalSelection);

            string logMsg = $"[{perksManager.IsFirstNormalPerkSelection}/{perksManager.IsPost30WavePerkSelection}/{perksManager.IsPost60WavePerkSelection}] ";
            logMsg += $"Target: {targetCount}, Selected: {finalSelection.Count} ({string.Join(", ", finalSelection.Select(m => m.data.key))})";
            Logger.LogInfo(logMsg);

            __result = finalSelection;
            return false;
        }

        private static void AddUpTo(List<Modifier> targetList, List<Modifier> sourceList, int limit)
        {
            int needed = limit - targetList.Count;
            if (needed <= 0) return;

            // Ensure uniqueness in targetList
            var existingKeys = new HashSet<string>(targetList.Select(m => m.data.key));

            foreach (var mod in sourceList)
            {
                if (targetList.Count >= limit) break;
                if (!existingKeys.Contains(mod.data.key))
                {
                    targetList.Add(mod);
                    existingKeys.Add(mod.data.key);
                }
            }
        }

        private static void ApplyPerkLuck(PerksManager perksManager, List<Modifier> perks)
        {
            ModifierManager_GetNonMaxedSurvivalMods_Patch.LuckyPerkKeys.Clear();
            int luckLevel = perksManager.GetPerkLevel("perkLuck");
            if (luckLevel <= 0) return;

            float chance = perksManager.GetPerkLuckChance();

            foreach (var mod in perks)
            {
                if (mod == null || mod.data == null) continue;

                // Only apply luck to perks with max level > 1 
                if (mod.data.maxLevel <= 1) continue;
                if (mod.levelInSurvival > 0) continue;

                if (UnityEngine.Random.value < chance)
                {
                    ModifierManager_GetNonMaxedSurvivalMods_Patch.LuckyPerkKeys.Add(mod.data.key);
                    Logger.LogInfo($"[Perk Luck] Upgraded '{mod.data.key}' to level 2 choice");
                }
            }
        }
    }

}