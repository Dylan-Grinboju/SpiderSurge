using System.Collections.Generic;
using SpiderSurge.Common;

namespace SpiderTeams
{
    // The attacker of the innermost Teams entry on the core attack scope
    internal static class DamageContext
    {
        private static AttackView<int> View => TeamAttacks.View;

        public static int Current => View.Active ? View.Slot(View.Top) : OwnerRegistry.Unknown;

        // The innermost explosion's instance id, 0 outside one
        public static int Blast
        {
            get
            {
                int index = View.Innermost<Explosion>();
                return index >= 0 ? View.SourceAt(index).GetInstanceID() : 0;
            }
        }

        public static bool InPulse => View.Innermost<SpiderSurge.PulseAbility>() >= 0;
    }

    // Owner for network objects spawned while a weapon fires.
    internal static class SpawnScope
    {
        private static readonly List<int> _owners = new List<int>(4);

        public static bool Active => _owners.Count > 0;

        public static int Owner => _owners.Count > 0 ? _owners[_owners.Count - 1] : OwnerRegistry.Unknown;

        public static void Push(int playerId)
        {
            _owners.Add(playerId);
        }

        public static void Pop()
        {
            if (_owners.Count > 0)
            {
                _owners.RemoveAt(_owners.Count - 1);
            }
        }

        public static void Clear()
        {
            _owners.Clear();
        }
    }
}
