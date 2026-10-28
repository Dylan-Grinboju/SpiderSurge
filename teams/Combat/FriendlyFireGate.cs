using System.Collections.Generic;
using UnityEngine;
using Logger = Silk.Logger;

namespace SpiderTeams
{
    internal static class FriendlyFireGate
    {
        private struct Push
        {
            public int Blast;
            public int Victim;
            public float Time;
        }

        private static readonly List<Push> _pushes = new List<Push>(8);

        public static int PulseDepth { get; set; }

        // Returns true to let the hit run as vanilla. An unknown attacker always gets vanilla so a tracking gap never blocks a kill.
        public static bool Allow(SpiderHealthSystem victim, Vector3 force, Vector3 point, bool stun)
        {
            FriendlyFireMode mode = ModConfig.FriendlyFire;
            if (mode == FriendlyFireMode.Damage || !IsTeammateHit(victim, out int attacker, out PlayerController victimPlayer, out int attackerTeam))
            {
                return true;
            }

            bool pushed = false;
            if (mode == FriendlyFireMode.Push && Time.time >= ReflectionCache.ImmuneTill(victim) && ClaimPush(victim.GetInstanceID()))
            {
                victim.Impact(force, point, stun, false);
                pushed = true;
            }

            Logger.LogDebug($"SpiderTeams friendly fire: attacker=P{attacker + 1} victim=P{victimPlayer.playerID.Value + 1} ({TeamPalette.TeamName(attackerTeam)}) -> {(pushed ? "pushed" : "ignored")}");
            return false;
        }

        // A blast's outer ring, a saw or a zap only knocks back and stuns, so Ignore drops that too; the Kinetic Pulse pushes everyone by design
        public static bool AllowImpact(SpiderHealthSystem victim)
        {
            return ModConfig.FriendlyFire != FriendlyFireMode.Ignore || PulseDepth > 0 || !IsTeammateHit(victim, out _, out _, out _);
        }

        private static bool IsTeammateHit(SpiderHealthSystem victim, out int attacker, out PlayerController victimPlayer, out int attackerTeam)
        {
            attacker = DamageContext.Current;
            victimPlayer = null;
            attackerTeam = -1;
            if (victim == null || attacker < 0 || !TeamManager.HostTeamMatch)
            {
                return false;
            }

            victimPlayer = victim.GetComponentInParent<PlayerController>();
            if (victimPlayer == null || victimPlayer.playerID.Value == attacker)
            {
                return false;
            }

            var manager = TeamManager.Instance;
            return manager.TryGetTeamOfPlayerId(attacker, out attackerTeam) && attackerTeam == manager.GetTeam(victimPlayer);
        }

        // An explosion knocks back every tick it lasts, so a teammate gets one shove per explosion, and one per shield-break window from anything else
        private static bool ClaimPush(int victim)
        {
            float now = Time.time;
            int blast = DamageContext.Blast;
            bool claimed = true;
            int keep = 0;
            for (int i = 0; i < _pushes.Count; i++)
            {
                Push push = _pushes[i];
                if (now - push.Time > Consts.Timing.FriendlyPushMemory)
                {
                    continue;
                }

                _pushes[keep++] = push;
                if (push.Victim == victim && (blast != 0 ? push.Blast == blast : now - push.Time < Consts.Timing.FriendlyPushGap))
                {
                    claimed = false;
                }
            }
            _pushes.RemoveRange(keep, _pushes.Count - keep);

            if (claimed)
            {
                _pushes.Add(new Push { Blast = blast, Victim = victim, Time = now });
            }
            return claimed;
        }
    }
}
