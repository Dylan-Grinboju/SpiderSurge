using System;
using HarmonyLib;
using SpiderSurge;
using SpiderSurge.Common;
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
            __state = PatchScopes.EnterAttack(__instance, ability => Players.IdOf(ability));
            FriendlyFireGate.PulseDepth++;
        }

        [HarmonyFinalizer]
        public static void Finalizer(bool __state)
        {
            FriendlyFireGate.PulseDepth--;
            PatchScopes.ExitAttack(__state);
        }
    }

    // The Black Hole swallows a teammate only when friendly fire does damage. Its pull is a push toward the caster,
    // so it pulls a teammate unless friendly fire is ignored.
    internal static class BlackHoleTeammates
    {
        public static bool Spares(StorageAbility ability, SpiderHealthSystem spider, bool pull)
        {
            if (pull ? ModConfig.FriendlyFire != FriendlyFireMode.Ignore : ModConfig.FriendlyFire == FriendlyFireMode.Damage) return false;
            return Same(ability, spider, pull ? "Black Hole pull" : "Black Hole friendly fire");
        }

        private static bool Same(StorageAbility ability, SpiderHealthSystem spider, string scope)
        {
            if (!TeamManager.TeamMatchActive)
            {
                return false;
            }

            try
            {
                var caster = ability.GetComponentInParent<PlayerController>();
                var victim = spider.GetComponentInParent<PlayerController>();
                var manager = TeamManager.Instance;
                return caster != null && victim != null && manager.TryGetTeamOfPlayerId(caster.playerID.Value, out int team) &&
                    manager.TryGetTeamOfPlayerId(victim.playerID.Value, out int victimTeam) && team == victimTeam;
            }
            catch (Exception ex)
            {
                PatchScopes.Report(scope, ex);
                return false;
            }
        }
    }

    // Hooks on Surge's abilities, set while Teams runs
    internal static class SurgeHooks
    {
        public static void Install()
        {
            StorageAbility.SparesTarget = BlackHoleTeammates.Spares;
            ImmuneAbility.OverrideRevive = ReviveTeammate;
            AmmoAbility.FallbackWeapon = VersusWeapon;
        }

        public static void Remove()
        {
            StorageAbility.SparesTarget = null;
            ImmuneAbility.OverrideRevive = null;
            AmmoAbility.FallbackWeapon = null;
        }

        // Surge revives any dead player; in versus that could be an opponent, so only teammates come back.
        private static bool ReviveTeammate(ImmuneAbility ability, out PlayerController revivedPlayer)
        {
            revivedPlayer = null;
            if (!SurgeVersus.RoundActive)
            {
                return false;
            }

            try
            {
                revivedPlayer = SurgeVersus.ReviveTeammate(ability);
            }
            catch (Exception ex)
            {
                PatchScopes.Report("Neural Backup revive", ex);
            }
            return true;
        }

        // Care Package only draws from survival's weapon list, so in versus it spawned nothing; it uses the match's weapon pool.
        private static GameObject VersusWeapon()
        {
            try
            {
                return SurgeVersus.RoundActive ? SurgeVersus.RandomWeapon() : null;
            }
            catch (Exception ex)
            {
                PatchScopes.Report("Care Package weapon", ex);
                return null;
            }
        }
    }
}
