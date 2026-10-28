using System.Collections.Generic;

namespace SpiderSurge
{
    public sealed class FreezeAchievementCast
    {
        public int PlayerId { get; }
        public float EndsAt { get; }
        private readonly HashSet<int> kills = new HashSet<int>();

        public FreezeAchievementCast(int playerId, float endsAt)
        {
            PlayerId = playerId;
            EndsAt = endsAt;
        }

        public bool RecordKill(int playerId, int enemyId, float now)
        {
            return playerId >= 0 && playerId == PlayerId && now < EndsAt && kills.Add(enemyId) && kills.Count == 3;
        }
    }

    public sealed class StoredWeaponJourney
    {
        public string LastMap { get; private set; }
        public int MapChanges { get; private set; }

        public StoredWeaponJourney(string map) => LastMap = map;

        public bool Arrive(string map)
        {
            if (string.IsNullOrEmpty(map) || map == "Lobby" || map == LastMap) return false;
            LastMap = map;
            return ++MapChanges == 3;
        }
    }

    public sealed class BlackHoleMeal
    {
        private readonly HashSet<string> types = new HashSet<string>();

        public bool Eat(string enemyName)
        {
            string kind = enemyName.Replace("(Clone)", "").Replace("Shielded", "").Replace("Shield", "").Replace("Laser", "").Trim();
            return kind.Length > 0 && types.Add(kind) && types.Count == 3;
        }
    }
}
