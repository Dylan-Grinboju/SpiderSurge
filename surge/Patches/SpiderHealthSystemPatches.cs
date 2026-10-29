using HarmonyLib;
using UnityEngine;

namespace SpiderSurge
{
    [HarmonyPatch(typeof(SpiderHealthSystem))]
    public static class SpiderHealthSystemPatches
    {
        // Pulse never knocks back its own caster
        internal static bool AllowImpact(SpiderHealthSystem spider, Vector3 force, Vector3 point, bool stun)
        {
            return SurgeAttacks.ProtectedSpider != spider;
        }

        // Only a hit that lands counts, so one Teams friendly fire cancelled doesn't
        internal static void RecordHit(SpiderHealthSystem spider, Vector3 force, Vector3 point, bool stun)
        {
            TryRecordHit(spider, direction: (point - spider.transform.position).sqrMagnitude > 0.01f ? point - spider.transform.position : -force);
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

        private static void TryRecordHit(SpiderHealthSystem healthSystem, bool lava = false, Vector3 direction = default)
        {
            ImmuneAbility ability = GetAbilitySafe(healthSystem);
            if (ability != null)
            {
                ability.RegisterHit(lava, lava ? Vector3.down : direction);
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
