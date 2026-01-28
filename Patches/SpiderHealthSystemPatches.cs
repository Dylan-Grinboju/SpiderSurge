using HarmonyLib;
using SpiderSurge;
using UnityEngine;

namespace SpiderSurge
{
    [HarmonyPatch(typeof(SpiderHealthSystem))]
    public static class SpiderHealthSystemPatches
    {
        [HarmonyPatch("Damage")]
        [HarmonyPrefix]
        public static void Damage_Prefix(SpiderHealthSystem __instance)
        {
            RecordHit(__instance);
        }

        [HarmonyPatch("Disintegrate")]
        [HarmonyPrefix]
        public static void Disintegrate_Prefix(SpiderHealthSystem __instance)
        {
            RecordHit(__instance);
        }

        private static void RecordHit(SpiderHealthSystem healthSystem)
        {
            // Find the ShieldAbility associated with this spider
            ShieldAbility ability = ShieldAbility.GetByHealthSystem(healthSystem);
            if (ability != null)
            {
                ability.RegisterHit();
            }
        }
    }
}
