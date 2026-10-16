using System;
using HarmonyLib;
using SpiderSurge;
using UnityEngine;

namespace SpiderTeams
{
    // The pulse knocks back and, as an ultimate, kills; its caster becomes the attacker so the friendly fire rule applies.
    [HarmonyPatch(typeof(PulseAbility), "ApplyPulsePhysics")]
    public static class PulseAbility_ApplyPulsePhysics_Patch
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        public static void Prefix(Component __instance, out bool __state)
        {
            __state = PatchScopes.EnterAttack(__instance, ability => OwnerRegistry.OfPlayer(ability.GetComponentInParent<PlayerController>()));
        }

        [HarmonyFinalizer]
        public static void Finalizer(bool __state)
        {
            PatchScopes.ExitAttack(__state);
        }
    }

    // Surge revives any dead player; in versus that could be an opponent, so only teammates come back.
    [HarmonyPatch(typeof(ImmuneAbility), "TryRespawnDeadPlayer")]
    public static class ImmuneAbility_TryRespawnDeadPlayer_Patch
    {
        [HarmonyPrefix]
        public static bool Prefix(Component __instance, ref PlayerController revivedPlayer, ref bool __result)
        {
            if (!SurgeVersus.RoundActive)
            {
                return true;
            }

            revivedPlayer = null;
            __result = false;
            try
            {
                revivedPlayer = SurgeVersus.ReviveTeammate(__instance);
                __result = revivedPlayer != null;
            }
            catch (Exception ex)
            {
                PatchScopes.Report("Neural Backup revive", ex);
            }
            return false;
        }
    }

    // Care Package only draws from survival's weapon list, so in versus it spawned nothing; it now uses the match's weapon pool.
    [HarmonyPatch(typeof(AmmoAbility), "GetRandomWeaponPrefab")]
    public static class AmmoAbility_GetRandomWeaponPrefab_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(ref GameObject __result)
        {
            try
            {
                if (__result == null && SurgeVersus.RoundActive)
                {
                    __result = SurgeVersus.RandomWeapon();
                }
            }
            catch (Exception ex)
            {
                PatchScopes.Report("Care Package weapon", ex);
            }
        }
    }
}
