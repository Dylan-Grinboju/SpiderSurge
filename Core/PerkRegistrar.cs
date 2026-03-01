using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using Logger = Silk.Logger;

namespace SpiderSurge;

public static class PerkRegistrar
{
    public static void RegisterPerks(ModifierManager manager)
    {
        try
        {
            // Load icons from the Icons folder
            IconLoader.LoadAllIcons();

            // Map perk keys to icon filenames (without extension)
            Dictionary<string, string> iconMapping = new()
            {
                { Consts.PerkNames.ImmuneAbility, "immune_ability" },
                { Consts.PerkNames.ImmuneAbilityUltimate, "immune_ability" },
                { Consts.PerkNames.AmmoAbility, "ammo_ability" },
                { Consts.PerkNames.AmmoAbilityUltimate, "ammo_ability" },
                { Consts.PerkNames.StorageAbility, "storage_ability" },
                { Consts.PerkNames.StorageAbilityUltimate, "storage_ability" },
                { Consts.PerkNames.PulseAbility, "pulse_ability" },
                { Consts.PerkNames.PulseAbilityUltimate, "pulse_ability" },
                { Consts.PerkNames.AbilityCooldown, "cooldown_perk" },
                { Consts.PerkNames.AbilityDuration, "duration_perk" },
                { Consts.PerkNames.ShortTermInvestment, "short_term_perk" },
                { Consts.PerkNames.LongTermInvestment, "long_term_perk" },
                { Consts.PerkNames.PerkLuck, "luck_perk" }
            };

            // Use an existing modifier's icon as fallback/placeholder
            var existingMods = manager.GetNonMaxedSurvivalMods();
            Sprite defaultIcon = existingMods.Count > 0 ? existingMods[0].data.icon : null;

            // Register ALL perks (abilities + upgrades) with ModifierManager so they have valid IDs
            var allPerks = PerksManager.Instance.GetAllPerkNames()
                .Select(name => (
                    name,
                    PerksManager.Instance.GetDisplayName(name),
                    PerksManager.Instance.GetDescription(name),
                    PerksManager.Instance.GetUpgradeDescription(name),
                    PerksManager.Instance.GetMaxLevel(name)
                )).ToArray();

            var modifiersList = ReflectionHelper.GetPrivateField<List<Modifier>>(manager, "_modifiers") ?? [];
            var currModsState = ReflectionHelper.GetPrivateField<ModifierManager.NetworkModifier[]>(manager, "_currModsState");

            if (currModsState is not null)
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

                // Try to finding specific icon, otherwise use default
                if (iconMapping.TryGetValue(key, out string iconName))
                {
                    Sprite specificIcon = IconLoader.GetIcon(iconName);
                    data.icon = specificIcon ?? defaultIcon;
                }
                else
                {
                    data.icon = defaultIcon;
                }

                Modifier modifier = new(data);
                modifiersList.Add(modifier);
            }

            ReflectionHelper.SetPrivateField(manager, "_modifiers", modifiersList);
            ReflectionHelper.SetPrivateField(manager, "_currModsState", currModsState);
        }
        catch (Exception ex)
        {
            Logger.LogError($"Error adding surge modifiers: {ex.Message}");
        }
    }
}
