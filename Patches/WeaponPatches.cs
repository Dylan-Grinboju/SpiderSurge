using HarmonyLib;
using UnityEngine;

namespace SpiderSurge;

internal static class AmmoAbilityPatchUtils
{
    internal static void TryHandleWeaponRemoved(SpiderWeaponManager manager)
    {
        if (manager is null || manager.equippedWeapon is null)
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

[HarmonyPatch(typeof(SpiderWeaponManager), nameof(SpiderWeaponManager.UnEquipWeapon))]
public class SpiderWeaponManager_UnEquipWeapon_Patch
{
    [HarmonyPrefix]
    public static void Prefix(SpiderWeaponManager __instance) => AmmoAbilityPatchUtils.TryHandleWeaponRemoved(__instance);
}

[HarmonyPatch(typeof(SpiderWeaponManager), "ThrowWeapon")]
public class SpiderWeaponManager_ThrowWeapon_Patch
{
    [HarmonyPrefix]
    public static void Prefix(SpiderWeaponManager __instance) => AmmoAbilityPatchUtils.TryHandleWeaponRemoved(__instance);
}
