using HarmonyLib;
using UnityEngine;

namespace SpiderSurge
{
    [HarmonyPatch(typeof(Weapon), nameof(Weapon.CanEquip))]
    public class Weapon_CanEquip_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(Weapon __instance, ref bool __result)
        {
            // If the weapon is inactive (e.g., in Pocket Dimention storage), it should not be equippable
            // This prevents the PickUpVacuum from trying to recall it and showing indicators to the spawn point
            if (__result && !__instance.gameObject.activeInHierarchy)
            {
                __result = false;
            }
        }
    }
}
