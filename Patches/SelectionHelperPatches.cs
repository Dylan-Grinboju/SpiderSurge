using HarmonyLib;
using Silk;
using Logger = Silk.Logger;
using System.Collections.Generic;
using UnityEngine;

namespace SpiderSurge
{
    [HarmonyPatch(typeof(SelectionHelper), "PickRandomModifiers")]
    public class SelectionHelper_PickRandomModifiers_Patch
    {
        [HarmonyPrefix]
        public static bool Prefix(int count, List<Modifier> availableModifiers, ref List<Modifier> __result)
        {
            try
            {
                List<Modifier> modifiedList = new List<Modifier>(availableModifiers);
                Modifier shieldMod = null;

                // Find the shield modifier if it exists and is not maxed
                foreach (var mod in modifiedList)
                {
                    if (mod.data.key == "shieldAbility" && mod.levelInSurvival < mod.data.maxLevel)
                    {
                        shieldMod = mod;
                        break;
                    }
                }

                if (shieldMod != null)
                {
                    // Remove shield from list temporarily
                    modifiedList.Remove(shieldMod);

                    // Create result list starting with shield
                    __result = new List<Modifier> { shieldMod };

                    // Pick the remaining random modifiers
                    for (int i = 1; i < count && modifiedList.Count > 0; i++)
                    {
                        Modifier item = modifiedList[UnityEngine.Random.Range(0, modifiedList.Count)];
                        if (__result.Contains(item))
                        {
                            i--; // Try again
                        }
                        else
                        {
                            __result.Add(item);
                        }
                    }

                    return false; // Skip original method
                }
            }
            catch (System.Exception ex)
            {
                Logger.LogError($"Error in PickRandomModifiers patch: {ex.Message}");
            }

            return true; // Run original method
        }
    }
}