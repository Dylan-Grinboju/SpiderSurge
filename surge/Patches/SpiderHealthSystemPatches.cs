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

            var storageAbility = StorageAbility.GetByHealthSystem(healthSystem);
            if (storageAbility == null && healthSystem.rootObject != null)
            {
                storageAbility = healthSystem.rootObject.GetComponent<StorageAbility>();
            }

            storageAbility?.OnCharacterDied();
        }

        private static void TryRecordHit(SpiderHealthSystem healthSystem)
        {
            ImmuneAbility ability = GetAbilitySafe(healthSystem);
            if (ability != null)
            {
                ability.RegisterHit();
            }
        }

        private static ImmuneAbility GetAbilitySafe(SpiderHealthSystem healthSystem)
        {
            if (healthSystem == null) return null;

            var ability = ImmuneAbility.GetByHealthSystem(healthSystem);

            if (ability == null)
            {
                ability = healthSystem.GetComponent<ImmuneAbility>();

                if (ability == null && healthSystem.rootObject != null)
                {
                    ability = healthSystem.rootObject.GetComponentInChildren<ImmuneAbility>();
                    if (ability == null)
                    {
                        ability = healthSystem.rootObject.GetComponentInParent<ImmuneAbility>();
                    }
                }
            }
            return ability;
        }
    }
}
