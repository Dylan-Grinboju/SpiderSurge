using HarmonyLib;

namespace SpiderSurge
{
    [HarmonyPatch(typeof(SpiderHealthSystem))]
    public static class SpiderHealthSystemPatches
    {
        [HarmonyPatch("Impact")]
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        public static bool Impact_Prefix(SpiderHealthSystem __instance)
        {
            return PulseExplosionScopePatch.ProtectedSpider != __instance;
        }

        // A hit another prefix cancelled (Teams friendly fire) never reached the spider, so Null Field doesn't count it
        [HarmonyPatch("Damage")]
        [HarmonyPrefix]
        public static void Damage_Prefix(SpiderHealthSystem __instance, bool __runOriginal)
        {
            if (__runOriginal) TryRecordHit(__instance);
        }

        // Lava disintegrates a spider inside a Null Field (the Black Hole spares it); so does falling below the map, which only delays the death and isn't a hit
        [HarmonyPatch("Disintegrate")]
        [HarmonyPrefix]
        public static void Disintegrate_Prefix(SpiderHealthSystem __instance, bool __runOriginal)
        {
            if (__runOriginal && !BelowMap(__instance)) TryRecordHit(__instance, lava: true);
        }

        private static bool BelowMap(SpiderHealthSystem healthSystem)
        {
            return healthSystem.transform.position.y < (CustomMapEditor.ParkourActive() ? -700f : -4000f);
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

        private static void TryRecordHit(SpiderHealthSystem healthSystem, bool lava = false)
        {
            ImmuneAbility ability = GetAbilitySafe(healthSystem);
            if (ability != null)
            {
                ability.RegisterHit(lava);
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
