using System.Collections.Generic;

namespace SpiderTeams
{
    // Damage() has no attacker argument, so each damage source pushes its attacker for the duration of its call.
    internal static class DamageContext
    {
        private static readonly List<int> _attackers = new List<int>(8);

        public static int Current => _attackers.Count > 0 ? _attackers[_attackers.Count - 1] : OwnerRegistry.Unknown;

        // Instance id of the explosion whose knockback is running, or 0
        public static int Blast { get; set; }

        public static void Push(int playerId)
        {
            _attackers.Add(playerId);
        }

        public static void Pop()
        {
            if (_attackers.Count > 0)
            {
                _attackers.RemoveAt(_attackers.Count - 1);
            }
        }

        public static void Clear()
        {
            _attackers.Clear();
            Blast = 0;
        }
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
