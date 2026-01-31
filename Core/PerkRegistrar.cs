using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using Logger = Silk.Logger;

namespace SpiderSurge
{
    public static class PerkRegistrar
    {
        public static void RegisterPerks(ModifierManager manager)
        {
            try
            {
                // Use an existing modifier's icon as placeholder
                var existingMods = manager.GetNonMaxedSurvivalMods();
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

                var modifiersList = ReflectionHelper.GetPrivateField<List<Modifier>>(manager, "_modifiers") ?? new List<Modifier>();
                var currModsState = ReflectionHelper.GetPrivateField<ModifierManager.NetworkModifier[]>(manager, "_currModsState");

                if (currModsState != null)
                {
                    int startIndex = currModsState.Length;
                    Array.Resize(ref currModsState, startIndex + allPerks.Length);

                    // Initialize the new entries
                    for (int i = 0; i < allPerks.Length; i++)
                    {
                        currModsState[startIndex + i] = new ModifierManager.NetworkModifier { levelInVersus = 0, levelInWaves = 0 };
                    }
                }

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
                }

                ReflectionHelper.SetPrivateField(manager, "_modifiers", modifiersList);
                ReflectionHelper.SetPrivateField(manager, "_currModsState", currModsState);

                Logger.LogInfo($"Registered {allPerks.Length} surge perks with ModifierManager (abilities + upgrades)");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error adding surge modifiers: {ex.Message}");
            }
        }
    }
}
