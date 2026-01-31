using HarmonyLib;

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

        [HarmonyPatch("DisintegrateLegsAndDestroy")]
        [HarmonyPrefix]
        public static void DisintegrateLegsAndDestroy_Prefix(SpiderHealthSystem __instance)
        {
            var ability = InterdimensionalStorageAbility.GetByHealthSystem(__instance);
            if (ability != null)
            {
                ability.OnCharacterDied();
            }
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
