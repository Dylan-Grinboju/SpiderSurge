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
            TryRecordHit(__instance);
        }

        [HarmonyPatch("Disintegrate")]
        [HarmonyPrefix]
        public static void Disintegrate_Prefix(SpiderHealthSystem __instance)
        {
            TryRecordHit(__instance);
        }

        [HarmonyPatch("ExplodeInDirection")]
        [HarmonyPrefix]
        public static bool ExplodeInDirection_Prefix(SpiderHealthSystem __instance)
        {
            // If immune, prevent explosion/death
            var ability = GetAbilitySafe(__instance);
            if (ability != null && ability.IsImmune)
            {
                return false; // Skip execution
            }
            return true;
        }

        // Removed DisintegrateLegsAndDestroy patch as it returns IEnumerator and is tricky to patch correctly with bool return.
        // Blocking ExplodeInDirection covers the death path initiated by DisintegrateClientRpc -> DisintegrateSpiderAndDestroy.
        // And Disintegrate checks _immuneTill internally (which we will restore in ShieldAbility).

        private static void TryRecordHit(SpiderHealthSystem healthSystem)
        {
            ShieldAbility ability = GetAbilitySafe(healthSystem);
            if (ability != null)
            {
                ability.RegisterHit();
            }
        }

        private static ShieldAbility GetAbilitySafe(SpiderHealthSystem healthSystem)
        {
            if (healthSystem == null) return null;

            // Try dictionary lookup first
            var ability = ShieldAbility.GetByHealthSystem(healthSystem);

            // Fallback: Component search
            if (ability == null)
            {
                // Try on the health system's object
                ability = healthSystem.GetComponent<ShieldAbility>();

                // Try on root object
                if (ability == null && healthSystem.rootObject != null)
                {
                    ability = healthSystem.rootObject.GetComponentInChildren<ShieldAbility>();
                    if (ability == null)
                    {
                        ability = healthSystem.rootObject.GetComponentInParent<ShieldAbility>();
                    }
                }
            }
            return ability;
        }
    }
}
