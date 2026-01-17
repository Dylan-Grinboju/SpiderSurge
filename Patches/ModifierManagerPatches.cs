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
                var perks = new (string key, string title, string description, string descriptionPlus, int maxLevel)[]
                {
                    ("shieldAbility", "Shield Ability", "Unlocks the shield ability for use in survival mode", null, 1),
                    ("shieldCap2", "Shield Capacity +1", "Increases shield charge capacity to 2", null, 1),
                    ("shieldCap3", "Shield Capacity +2", "Increases shield charge capacity to 3", null, 1),
                    ("stillness10s", "Stillness Charge (10s)", "Gain a shield charge after standing still for 10 seconds", "Upgrade: Gain a shield charge after standing still for 5 seconds", 2),
                    ("airborne10s", "Airborne Charge (10s)", "Gain a shield charge after being airborne for 10 seconds", "Upgrade: Gain a shield charge after being airborne for 5 seconds", 2),
                    ("explosionImmunity", "Explosion Immunity", "Activating shield while shielded causes explosion and grants 1 second immunity", null, 1)
                };

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
                SurgeGameModeManager.Instance.SetPerkLevel(modifier.data.key, value);

                if (modifier.data.key == "shieldAbility")
                {
                    AbilityManager.EnableShieldAbility();
                }
                else if (modifier.data.key == "explosionImmunity")
                {
                    AbilityManager.EnableExplosionImmunity();
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
            if (key == "shieldAbility" || key == "shieldCap2" || key == "shieldCap3" ||
                key == "stillness10s" || key == "stillness5s" || key == "airborne10s" ||
                key == "airborne5s" || key == "explosionImmunity")
            {
                // Fix localization for custom perks
                var perkNameText = __instance.GetType().GetField("perkNameText", BindingFlags.Public | BindingFlags.Instance)?.GetValue(__instance) as TMP_Text;
                var perkDescriptionText = __instance.GetType().GetField("perkDescriptionText", BindingFlags.Public | BindingFlags.Instance)?.GetValue(__instance) as TMP_Text;

                // Define titles and descriptions
                var perkTexts = new Dictionary<string, (string title, string description)>
                {
                    { "shieldAbility", ("Shield Ability", "Unlocks the shield ability for use in survival mode") },
                    { "shieldCap2", ("Shield Capacity +1", "Increases shield charge capacity to 2") },
                    { "shieldCap3", ("Shield Capacity +2", "Increases shield charge capacity to 3") },
                    { "stillness10s", ("Stillness Charge (10s)", "Gain a shield charge after standing still for 10 seconds") },
                    { "airborne10s", ("Airborne Charge (10s)", "Gain a shield charge after being airborne for 10 seconds") },
                    { "explosionImmunity", ("Explosion Immunity", "Activating shield while shielded causes explosion and grants 1 second immunity") }
                };

                if (perkTexts.TryGetValue(key, out var texts))
                {
                    if (perkNameText != null)
                    {
                        perkNameText.text = texts.title;
                    }

                    if (perkDescriptionText != null)
                    {
                        perkDescriptionText.text = texts.description;
                    }
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
                if (key == "shieldCap2" || key == "explosionImmunity" || key == "stillness10s" || key == "airborne10s")
                    return surgeManager.GetPerkLevel("shieldAbility") > 0;
                if (key == "shieldCap3")
                    return surgeManager.GetPerkLevel("shieldCap2") > 0;
                return true; // Other perks are always available
            }).ToList();

            // Separate mod and vanilla perks
            var modPerks = filtered.Where(mod => IsModPerk(mod.data.key)).ToList();
            var vanillaPerks = filtered.Where(mod => !IsModPerk(mod.data.key)).ToList();

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

        private static bool IsModPerk(string key)
        {
            return key == "shieldAbility" || key == "shieldCap2" || key == "shieldCap3" ||
                   key == "stillness10s" || key == "airborne10s" || key == "explosionImmunity";
        }
    }
}