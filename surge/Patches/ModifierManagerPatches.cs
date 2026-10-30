using HarmonyLib;
using Logger = Silk.Logger;
using System;
using System.Linq;
using System.Collections.Generic;
using TMPro;

namespace SpiderSurge
{
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
            if (mode == GameMode.Wave && value == 1 && modifier != null && modifier.data != null)
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

            if (mode == GameMode.Wave && value > 0 && modifier != null && modifier.data != null)
            {
                string key = modifier.data.key;
                if (PerksManager.Instance.GetAllPerkNames().Contains(key))
                {
                    PerksManager.Instance.SetPerkLevel(key, value);
                    PerksManager.Instance.OnSelected(key);

                    if (PerksManager.Instance.IsAbilityPerk(modifier.data.key))
                    {
                        if (PerksManager.Instance.IsFirstNormalPerkSelection)
                        {
                            PerksManager.Instance.IsFirstNormalPerkSelection = false;
                        }
                    }
                }


            }

            // Reset the PowerUp sound flag after selection is complete
            PowerUpSoundPlayedThisSelection = false;
        }

        public static void PlayPowerUpSound()
        {
            SoundManager.Play(Consts.SoundNames.PowerUp, Consts.SoundVolumes.PowerUp);
            PowerUpSoundPlayedThisSelection = true;
        }
    }

    [HarmonyPatch(typeof(SurvivalModifierChoiceCard), "SetupCard")]
    public class SurvivalModifierChoiceCard_SetupCard_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(SurvivalModifierChoiceCard __instance, Modifier m, GameLevel gl, int id, bool showTwitchVotes)
        {
            if (!SurgeGameModeManager.IsSurgeRunActive) return;

            string key = m.data.key;
            if (AbilitySelection.StartingChoiceOpen)
            {
                __instance.mapNameText.text = "AVAILABLE FROM WAVE 1";
                __instance.mapImage.gameObject.SetActive(false);
            }
            bool isSurgePerk = PerksManager.Instance.GetAllPerkNames().Contains(key);
            bool isLucky = ModifierManager_GetNonMaxedSurvivalMods_Patch.LuckyPerkKeys.Contains(key);

            bool isSynergized = PerkSynergy.Describe(key, m.levelInSurvival + 1) != null;

            if (!isSurgePerk && !isLucky && !isSynergized) return;

            TMP_Text perkNameText = __instance.perkNameText;
            TMP_Text perkDescriptionText = __instance.perkDescriptionText;

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
                        description += "\n" + Consts.Formatting.TextLuckyUpgrade;
                    }

                    perkDescriptionText.text = description;
                }
            }
            else
            {
                // Vanilla Modifier Handling
                if (perkDescriptionText != null)
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
        public static HashSet<string> LuckyPerkKeys = new HashSet<string>();

        [HarmonyPostfix]
        public static void Postfix(ModifierManager __instance, ref List<Modifier> __result)
        {
            var perksManager = PerksManager.Instance;
            if (perksManager == null)
            {
                return;
            }

            if (!SurgeGameModeManager.IsSurgeRunActive)
            {
                LuckyPerkKeys.Clear();
                __result = __result
                    .Where(mod => mod?.data != null && !perksManager.GetAllPerkNames().Contains(mod.data.key))
                    .ToList();
                return;
            }
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
                    if (perksManager.IsAbilityPerk(key) || perksManager.GetAbilityUltimatePerkNames().Contains(key)) continue;
                    if (!perksManager.IsAvailable(key)) continue;
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

            if (!SurgeGameModeManager.IsSurgeRunActive) return true;

            var perksManager = PerksManager.Instance;
            var selected = availableModifiers.Where(mod => mod?.data != null
                && !perksManager.IsAbilityPerk(mod.data.key)
                && !perksManager.GetAbilityUltimatePerkNames().Contains(mod.data.key))
                .GroupBy(mod => mod.data.key).Select(group => group.First())
                .OrderBy(_ => UnityEngine.Random.value).Take(Math.Max(0, count)).ToList();
            ApplyPerkLuck(perksManager, selected);
            __result = selected;
            return false;
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
                if (mod == null || mod.data == null) continue;

                // Only apply luck to perks with max level > 1
                if (mod.data.maxLevel <= 1) continue;
                if (mod.levelInSurvival > 0) continue;

                if (UnityEngine.Random.value < chance)
                {
                    ModifierManager_GetNonMaxedSurvivalMods_Patch.LuckyPerkKeys.Add(mod.data.key);
                    anyLuckyPerk = true;
                }
            }

            if (anyLuckyPerk && !ModifierManager_SetModLevel_Patch.PowerUpSoundPlayedThisSelection)
            {
                SoundManager.Play(Consts.SoundNames.LuckyUpgrade, Consts.SoundVolumes.LuckyUpgrade);
            }

            // The cards go out as a ClientRpc right after this, so clients need the lucky keys and phase first
            SurgeNet.SendStateNow();
        }
    }
}
