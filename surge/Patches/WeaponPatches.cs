using HarmonyLib;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Logger = Silk.Logger;

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
            SpiderHealthSystem caster = PulseExplosionScopePatch.ProtectedSpider;
            return caster == null || !__instance.equipped || __instance.owner == null || __instance.owner.healthSystem != caster;
        }
    }

    internal static class AmmoAbilityPatchUtils
    {
        internal static void TryHandleWeaponRemoved(SpiderWeaponManager manager)
        {
            if (manager == null || manager.equippedWeapon == null)
            {
                return;
            }

            AmmoAbility.HandleWeaponRemoved(manager, manager.equippedWeapon);
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

    [HarmonyPatch(typeof(SpiderWeaponManager), "OnEquipWeapon")]
    public class SpiderWeaponManager_OnEquipWeapon_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(SpiderWeaponManager __instance, Weapon weapon)
        {
            if (weapon != null)
            {
                AmmoAbility.HandleWeaponEquipped(__instance, weapon);
            }
        }
    }

    [HarmonyPatch(typeof(SpiderWeaponManager), nameof(SpiderWeaponManager.UnEquipWeapon))]
    public class SpiderWeaponManager_UnEquipWeapon_Patch
    {
        [HarmonyPrefix]
        public static void Prefix(SpiderWeaponManager __instance)
        {
            AmmoAbilityPatchUtils.TryHandleWeaponRemoved(__instance);
        }
    }

    [HarmonyPatch(typeof(SpiderWeaponManager), "ThrowWeapon")]
    public class SpiderWeaponManager_ThrowWeapon_Patch
    {
        [HarmonyPrefix]
        public static void Prefix(SpiderWeaponManager __instance)
        {
            AmmoAbilityPatchUtils.TryHandleWeaponRemoved(__instance);
        }
    }

    [HarmonyPatch(typeof(Weapon), nameof(Weapon.ammo), MethodType.Setter)]
    public class Weapon_AmmoSetter_Patch
    {
        [HarmonyPrefix]
        public static void Prefix(Weapon __instance, ref float value)
        {
            AmmoAbility clip = AmmoAbility.ActiveFor(__instance, out int playerId);
            if (clip == null) return;

            if (SpiderSurge.Common.Net.IsAuthority && value < __instance.ammo)
            {
                // Caught and logged like SurgeVfx.Safely, without allocating a closure per shot
                try
                {
                    clip.AccentShot(__instance, playerId);
                }
                catch (System.Exception ex)
                {
                    Logger.LogWarning($"SurgeVfx: an effect failed and was skipped: {ex}");
                }
            }
            float floor = clip.GetHeldWeaponFloorAmmo(__instance);
            if (floor >= 0f && value < floor)
            {
                value = floor;
            }
        }
    }
}
