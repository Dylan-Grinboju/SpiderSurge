using HarmonyLib;

namespace SpiderSurge;

[HarmonyPatch(typeof(SpiderHealthSystem))]
public static class SpiderHealthSystemPatches
{
    [HarmonyPatch("Damage")]
    [HarmonyPrefix]
    public static void Damage_Prefix(SpiderHealthSystem __instance) => TryRecordHit(__instance);

    [HarmonyPatch("Disintegrate")]
    [HarmonyPrefix]
    public static void Disintegrate_Prefix(SpiderHealthSystem __instance) => TryRecordHit(__instance);

    [HarmonyPatch("ExplodeInDirection")]
    [HarmonyPrefix]
    public static bool ExplodeInDirection_Prefix(SpiderHealthSystem __instance)
    {
        // If immune, prevent explosion/death
        var ability = GetAbilitySafe(__instance);
        if (ability is not null && ability.IsImmune)
        {
            return false;
        }

        NotifyStorageAbilityOfDeath(__instance);
        return true;
    }

    private static void NotifyStorageAbilityOfDeath(SpiderHealthSystem healthSystem)
    {
        if (healthSystem is null) return;

        var storageAbility = StorageAbility.GetByHealthSystem(healthSystem);
        if (storageAbility is null && healthSystem.rootObject is not null)
        {
            storageAbility = healthSystem.rootObject.GetComponent<StorageAbility>();
        }

        storageAbility?.OnCharacterDied();
    }

    private static void TryRecordHit(SpiderHealthSystem healthSystem)
    {
        ImmuneAbility ability = GetAbilitySafe(healthSystem);
        ability?.RegisterHit();
    }

    private static ImmuneAbility GetAbilitySafe(SpiderHealthSystem healthSystem)
    {
        if (healthSystem is null) return null;

        var ability = ImmuneAbility.GetByHealthSystem(healthSystem);

        if (ability is null)
        {
            ability = healthSystem.GetComponent<ImmuneAbility>();

            if (ability is null && healthSystem.rootObject is not null)
            {
                ability = healthSystem.rootObject.GetComponentInChildren<ImmuneAbility>();
                ability ??= healthSystem.rootObject.GetComponentInParent<ImmuneAbility>();
            }
        }
        return ability;
    }
}
