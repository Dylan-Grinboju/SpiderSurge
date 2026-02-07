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
                return false;
            }

            NotifyStorageAbilityOfDeath(__instance);
            return true;
        }

        private static void NotifyStorageAbilityOfDeath(SpiderHealthSystem healthSystem)
        {
            if (healthSystem == null) return;

            var storageAbility = InterdimensionalStorageAbility.GetByHealthSystem(healthSystem);
            if (storageAbility == null && healthSystem.rootObject != null)
            {
                storageAbility = healthSystem.rootObject.GetComponent<InterdimensionalStorageAbility>();
            }

            storageAbility?.OnCharacterDied();
        }

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

            var ability = ShieldAbility.GetByHealthSystem(healthSystem);

            if (ability == null)
            {
                ability = healthSystem.GetComponent<ShieldAbility>();

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
