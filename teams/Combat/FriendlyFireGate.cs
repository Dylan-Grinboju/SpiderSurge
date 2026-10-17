using UnityEngine;
using Logger = Silk.Logger;

namespace SpiderTeams
{
    internal static class FriendlyFireGate
    {
        // Returns true to let the hit run as vanilla. An unknown attacker always gets vanilla so a tracking gap never blocks a kill.
        public static bool Allow(SpiderHealthSystem victim, Vector3 force, Vector3 point, bool stun)
        {
            FriendlyFireMode mode = ModConfig.FriendlyFire;
            if (mode == FriendlyFireMode.Damage || victim == null)
            {
                return true;
            }

            int attacker = DamageContext.Current;
            if (attacker < 0 || !TeamManager.IsHost || !TeamManager.TeamMatchActive)
            {
                return true;
            }

            var victimPlayer = victim.GetComponentInParent<PlayerController>();
            if (victimPlayer == null || victimPlayer.playerID.Value == attacker)
            {
                return true;
            }

            var manager = TeamManager.Instance;
            int attackerTeam;
            if (!manager.TryGetTeamOfPlayerId(attacker, out attackerTeam) || attackerTeam != manager.GetTeam(victimPlayer))
            {
                return true;
            }

            bool pushed = false;
            if (mode == FriendlyFireMode.Push && Time.time >= ReflectionCache.ImmuneTill(victim))
            {
                victim.Impact(force, point, stun, false);
                pushed = true;
            }

            Logger.LogDebug($"SpiderTeams friendly fire: attacker=P{attacker + 1} victim=P{victimPlayer.playerID.Value + 1} ({TeamPalette.TeamName(attackerTeam)}) -> {(pushed ? "pushed" : "ignored")}");
            return false;
        }
    }
}
