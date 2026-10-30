using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace SpiderSurge
{
    [HarmonyPatch]
    internal static class PulseExplosionWeapon_Patch
    {
        // Weapon overrides can drop, fire or explode before reaching the base hit handler.
        [HarmonyTargetMethods]
        private static IEnumerable<MethodBase> TargetMethods()
        {
            foreach (var type in typeof(Weapon).Assembly.GetTypes())
            {
                if (!typeof(Weapon).IsAssignableFrom(type)) continue;
                foreach (string name in new[] { nameof(Weapon.Damage), nameof(Weapon.Impact), nameof(Weapon.Disarm) })
                {
                    MethodInfo method = AccessTools.DeclaredMethod(type, name);
                    if (method != null) yield return method;
                }
            }
        }

        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static bool Prefix(Weapon __instance)
        {
            SpiderHealthSystem caster = SurgeAttacks.ProtectedSpider;
            return caster == null || !__instance.equipped || __instance.owner == null || __instance.owner.healthSystem != caster;
        }
    }

    [HarmonyPatch(typeof(Weapon), nameof(Weapon.CanEquip))]
    public class Weapon_CanEquip_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(Weapon __instance, ref bool __result)
        {
            // If the weapon is inactive (e.g., in Pocket Dimension storage), it should not be equippable
            // This prevents the PickUpVacuum from trying to recall it and showing indicators to the spawn point
            if (__result && !__instance.gameObject.activeInHierarchy)
            {
                __result = false;
            }
        }
    }

    [HarmonyPatch(typeof(Weapon), nameof(Weapon.UnEquip))]
    internal static class Overcharge_UnEquip_Patch
    {
        [HarmonyPostfix]
        private static void Postfix(Weapon __instance) => AmmoAbility.MarkUnequipped(__instance);
    }

    [HarmonyPatch(typeof(Weapon), nameof(Weapon.ammo), MethodType.Setter)]
    internal static class Overcharge_AmmoSetter_Patch
    {
        [HarmonyPrefix]
        private static void Prefix(Weapon __instance, ref float value)
        {
            float floor = AmmoAbility.AmmoFloorFor(__instance);
            if (floor >= 0f && value < floor) value = floor;
        }
    }

}
