using HarmonyLib;
using Logger = Silk.Logger;
using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using TMPro;

namespace SpiderSurge;

[HarmonyPatch(typeof(ModifierManager), "Awake")]
public class ModifierManager_Awake_Patch
{
    [HarmonyPostfix]
    public static void Postfix(ModifierManager __instance)
    {
        PerkRegistrar.RegisterPerks(__instance);
        // Reset the power up sound flag when modifier manager initializes
        ModifierManager_SetModLevel_Patch.PowerUpSoundPlayedThisSelection = false;
    }
}

[HarmonyPatch(typeof(ModifierManager), "SetModLevel")]
public class ModifierManager_SetModLevel_Patch
{
    // Flag to track if PowerUp sound was played this selection round
    public static bool PowerUpSoundPlayedThisSelection = false;

    [HarmonyPrefix]
    public static void Prefix(Modifier modifier, GameMode mode, ref int value)
    {
        if (mode == GameMode.Wave && value == 1 && modifier is not null && modifier.data is not null)
        {
            if (!SurgeGameModeManager.IsSurgeRunActive)
            {
                if (ModifierManager_GetNonMaxedSurvivalMods_Patch.LuckyPerkKeys.Count > 0)
                    ModifierManager_GetNonMaxedSurvivalMods_Patch.LuckyPerkKeys.Clear();
                return;
            }

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
        if (!SurgeGameModeManager.IsSurgeRunActive)
        {
            PowerUpSoundPlayedThisSelection = false;
            return;
        }

        if (mode == GameMode.Wave && value > 0)
        {
            string key = modifier.data.key;
            if (PerksManager.Instance.GetAllPerkNames().Contains(key))
            {
                if (value == 2 && PerksManager.Instance.GetPerkLevel(key) == 0)
                {
                    PerksManager.Instance.SetPerkLevel(key, 1);
                }
                PerksManager.Instance.SetPerkLevel(key, value);
                PerksManager.Instance.OnSelected(key);

                if (PerksManager.Instance.IsAbilityPerk(modifier.data.key))
                {
                    if (PerksManager.Instance.IsFirstNormalPerkSelection)
                    {
                        PerksManager.Instance.IsFirstNormalPerkSelection = false;
                    }
                }

                if (PerksManager.Instance.IsUltUpgradePerkSelection)
                {
                    PerksManager.Instance.IsUltUpgradePerkSelection = false;
                }

                if (PerksManager.Instance.IsUltSwapPerkSelection)
                {
                    PerksManager.Instance.IsUltSwapPerkSelection = false;
                }
            }
        }

        // Reset the PowerUp sound flag after selection is complete
        PowerUpSoundPlayedThisSelection = false;
    }

    public static void PlayPowerUpSound()
    {
        if (SoundManager.Instance is not null)
        {
            SoundManager.Instance.PlaySound(
                Consts.SoundNames.PowerUp,
                Consts.SoundVolumes.PowerUp * Consts.SoundVolumes.MasterVolume
            );
            PowerUpSoundPlayedThisSelection = true;
        }
    }
}

[HarmonyPatch(typeof(SurvivalModifierChoiceCard), "SetupCard")]
public class SurvivalModifierChoiceCard_SetupCard_Patch
{
    private static readonly FieldInfo _perkNameTextField;
    private static readonly FieldInfo _perkDescriptionTextField;

    static SurvivalModifierChoiceCard_SetupCard_Patch()
    {
        var t = typeof(SurvivalModifierChoiceCard);
        _perkNameTextField = t.GetField("perkNameText", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        _perkDescriptionTextField = t.GetField("perkDescriptionText", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
    }

    [HarmonyPostfix]
    public static void Postfix(SurvivalModifierChoiceCard __instance, Modifier m, GameLevel gl, int id, bool showTwitchVotes)
    {
        if (!SurgeGameModeManager.IsSurgeRunActive) return;

        string key = m.data.key;
        bool isSurgePerk = PerksManager.Instance.GetAllPerkNames().Contains(key);
        bool isLucky = ModifierManager_GetNonMaxedSurvivalMods_Patch.LuckyPerkKeys.Contains(key);

        // Perform Synergy Checks
        bool isSynergized = false;
        if (PerksManager.Instance.GetPerkLevel(Consts.PerkNames.ImmuneAbility) > 0 && (key == Consts.ModifierNames.StartShields || key == Consts.ModifierNames.PositiveEncouragement || key == Consts.ModifierNames.SafetyNet)) isSynergized = true;
        if (PerksManager.Instance.GetPerkLevel(Consts.PerkNames.AmmoAbility) > 0 && key == Consts.ModifierNames.Efficiency) isSynergized = true;
        if (PerksManager.Instance.GetPerkLevel(Consts.PerkNames.PulseAbility) > 0 && (key == Consts.ModifierNames.TooCool || key == Consts.ModifierNames.BiggerBoom)) isSynergized = true;
        if (PerksManager.Instance.GetPerkLevel(Consts.PerkNames.StorageAbility) > 0 && (key == Consts.ModifierNames.MoreGuns || key == Consts.ModifierNames.MoreBoom || key == Consts.ModifierNames.MoreParticles)) isSynergized = true;

        if (!isSurgePerk && !isLucky && !isSynergized) return;

        var perkNameText = _perkNameTextField?.GetValue(__instance) as TMP_Text;
        var perkDescriptionText = _perkDescriptionTextField?.GetValue(__instance) as TMP_Text;

        if (perkNameText is null) Logger.LogWarning($"[Surge] Could not find perkNameText for card {key}");
        if (perkDescriptionText is null) Logger.LogWarning($"[Surge] Could not find perkDescriptionText for card {key}");

        if (isSurgePerk)
        {
            if (perkNameText is not null)
            {
                string displayName = PerksManager.Instance.GetDisplayName(key);

                if (m.levelInSurvival == 1)
                {
                    displayName += " +";
                }
                perkNameText.text = displayName;
            }

            if (perkDescriptionText is not null)
            {
                string description = PerksManager.Instance.GetDescription(key);

                if (m.levelInSurvival == 1)
                {
                    description = PerksManager.Instance.GetUpgradeDescription(key);
                }

                if (isLucky)
                {
                    description += "\n" + Consts.Formatting.TextLuckyUpgrade;
                }

                if (PerksManager.Instance.GetAbilityUltimatePerkNames().Contains(key) && PerksManager.Instance.IsUltSwapPerkSelection)
                {
                    description += "\n" + Consts.Formatting.TextSwapAbility;
                }

                perkDescriptionText.text = description;
            }
        }
        else
        {
            // Vanilla Modifier Handling
            if (perkDescriptionText is not null)
            {
                if (isLucky)
                {
                    perkDescriptionText.text += "\n" + Consts.Formatting.TextLuckyUpgrade;
                }
                if (isSynergized)
                {
                    perkDescriptionText.text += "\n" + Consts.Formatting.TextSynergized;
                }
            }
        }
    }
}

[HarmonyPatch(typeof(ModifierManager), "GetNonMaxedSurvivalMods")]
public class ModifierManager_GetNonMaxedSurvivalMods_Patch
{
    public static HashSet<string> LuckyPerkKeys = [];

    [HarmonyPostfix]
    public static void Postfix(ModifierManager __instance, ref List<Modifier> __result)
    {
        var perksManager = PerksManager.Instance;
        if (perksManager is null)
        {
            return;
        }

        if (!SurgeGameModeManager.IsSurgeRunActive)
        {
            LuckyPerkKeys.Clear();
            __result = __result
                .Where(mod => mod?.data is not null && !perksManager.GetAllPerkNames().Contains(mod.data.key))
                .ToList();
            return;
        }
        List<Modifier> filteredList = [];
        List<string> processedKeys = [];

        // Only filter for validity/availability. DO NOT select/limit count here.
        foreach (var mod in __result)
        {
            if (mod is null || mod.data is null) continue;
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
                bool isSwapPhase = perksManager.IsUltSwapPerkSelection;

                if (isSwapPhase && isUlt)
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
        if (availableModifiers is null || availableModifiers.Count == 0)
        {
            __result = [];
            return false;
        }

        if (!SurgeGameModeManager.IsSurgeRunActive) return true;

        // --- SURGE MODE SELECTION LOGIC ---
        var perksManager = PerksManager.Instance;

        // 1. Categorize
        ClassifyModifiers(availableModifiers, perksManager, out var selectedAbilityUlt, out var otherAbilityUlts, out var abilities, out var modPerks, out var vanillaPerks);

        // 2. Shuffle sub-lists
        vanillaPerks = vanillaPerks.OrderBy(_ => UnityEngine.Random.value).ToList();
        modPerks = modPerks.OrderBy(_ => UnityEngine.Random.value).ToList();
        abilities = abilities.OrderBy(_ => UnityEngine.Random.value).ToList();
        selectedAbilityUlt = selectedAbilityUlt.OrderBy(_ => UnityEngine.Random.value).ToList();
        otherAbilityUlts = otherAbilityUlts.OrderBy(_ => UnityEngine.Random.value).ToList();

        // 3. Select based on phase
        List<Modifier> finalSelection = [];
        SelectPhasePerks(perksManager, count, finalSelection, selectedAbilityUlt, otherAbilityUlts, abilities, modPerks, vanillaPerks);

        // 4. Apply Perk Luck
        ApplyPerkLuck(perksManager, finalSelection);

        __result = finalSelection;
        return false;
    }

    private static void ClassifyModifiers(
        List<Modifier> source,
        PerksManager pm,
        out List<Modifier> selectedAbilityUlt,
        out List<Modifier> otherAbilityUlts,
        out List<Modifier> abilities,
        out List<Modifier> modPerks,
        out List<Modifier> vanillaPerks)
    {
        selectedAbilityUlt = [];
        otherAbilityUlts = [];
        abilities = [];
        modPerks = [];
        vanillaPerks = [];

        string currentUlt = pm.GetChosenAbilityUltimate();

        foreach (var mod in source)
        {
            string key = mod.data.key;

            if (pm.IsAbilityPerk(key))
            {
                abilities.Add(mod);
            }
            else if (pm.GetAbilityUltimatePerkNames().Contains(key))
            {
                if (key == currentUlt) selectedAbilityUlt.Add(mod);
                else otherAbilityUlts.Add(mod);
            }
            else if (pm.IsUpgradePerk(key))
            {
                modPerks.Add(mod);
            }
            else
            {
                vanillaPerks.Add(mod);
            }
        }
    }

    private static void SelectPhasePerks(PerksManager pm, int targetCount, List<Modifier> finalSelection, List<Modifier> selectedAbilityUlt, List<Modifier> otherAbilityUlts, List<Modifier> abilities, List<Modifier> modPerks, List<Modifier> vanillaPerks)
    {
        if (pm.IsUltSwapPerkSelection)
        {
            // Phase 1: Post 60 - Other Ults (Swap) -> Current Ult (if avail) -> Mods -> Vanilla
            ModifierManager_SetModLevel_Patch.PlayPowerUpSound();
            AddUpTo(finalSelection, otherAbilityUlts, Math.Max(0, targetCount - 1));
            AddUpTo(finalSelection, selectedAbilityUlt, targetCount);
            AddUpTo(finalSelection, modPerks, targetCount);
            AddUpTo(finalSelection, vanillaPerks, targetCount);
        }
        else if (pm.IsUltUpgradePerkSelection)
        {
            // Phase 2: Post 30 - Current Ult -> Mods -> Vanilla
            ModifierManager_SetModLevel_Patch.PlayPowerUpSound();
            AddUpTo(finalSelection, selectedAbilityUlt, targetCount);
            AddUpTo(finalSelection, modPerks, targetCount);
            AddUpTo(finalSelection, vanillaPerks, targetCount);
        }
        else if (pm.IsFirstNormalPerkSelection)
        {
            // Phase 3: First Selection - Abilities Only
            ModifierManager_SetModLevel_Patch.PlayPowerUpSound();
            AddUpTo(finalSelection, abilities, targetCount);
        }
        else
        {
            // Phase 4: Standard - Mix
            List<Modifier> mixedPerks = [.. modPerks, .. vanillaPerks];

            mixedPerks = mixedPerks.OrderBy(_ => UnityEngine.Random.value).ToList();
            AddUpTo(finalSelection, mixedPerks, targetCount);
        }
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
        int luckLevel = perksManager.GetPerkLevel(Consts.PerkNames.PerkLuck);
        if (luckLevel <= 0) return;

        float chance = perksManager.GetPerkLuckChance();
        bool anyLuckyPerk = false;

        foreach (var mod in perks)
        {
            if (mod is null || mod.data is null) continue;

            // Only apply luck to perks with max level > 1 
            if (mod.data.maxLevel <= 1) continue;
            if (mod.levelInSurvival > 0) continue;

            if (UnityEngine.Random.value < chance)
            {
                ModifierManager_GetNonMaxedSurvivalMods_Patch.LuckyPerkKeys.Add(mod.data.key);
                anyLuckyPerk = true;
            }
        }

        if (anyLuckyPerk && !ModifierManager_SetModLevel_Patch.PowerUpSoundPlayedThisSelection && SoundManager.Instance is not null)
        {
            SoundManager.Instance.PlaySound(
                Consts.SoundNames.LuckyUpgrade,
                Consts.SoundVolumes.LuckyUpgrade * Consts.SoundVolumes.MasterVolume
            );
        }
    }
}