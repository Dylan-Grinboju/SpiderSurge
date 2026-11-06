using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace SpiderSurge
{
    [HarmonyPatch(typeof(DeathCloud), "OnTriggerStay2D")]
    public static class BlackHolePoisonPatch
    {
        private static readonly AccessTools.FieldRef<DeathCloud, Dictionary<GameObject, float>> Exposure =
            AccessTools.FieldRefAccess<DeathCloud, Dictionary<GameObject, float>>("inRange");

        private static bool Prefix(DeathCloud __instance, Collider2D other)
        {
            if (!BlackHoleField.ProtectsFromPoison(other.GetComponent<SpiderHealthSystem>())) return true;

            // Poison exposure starts fresh after the field closes.
            Exposure(__instance).Remove(other.gameObject);
            return false;
        }
    }
}
