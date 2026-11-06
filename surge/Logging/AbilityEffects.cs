using System.Collections.Generic;
using UnityEngine;
using SpiderSurge.Common;

namespace SpiderSurge.Logging
{
    // What each ability did, counted per player on the host for the run report
    internal static class AbilityEffects
    {
        public const string HitsBlocked = "hitsBlocked";
        public const string EnemiesFrozen = "enemiesFrozen";
        public const string FrozenKills = "frozenKills";
        public const string EnemiesLaunched = "enemiesLaunched";
        public const string LaunchedKills = "launchedKills";
        public const string WeaponsStored = "weaponsStored";
        public const string WeaponsRetrieved = "weaponsRetrieved";
        public const string OverchargedShots = "overchargedShots";

        private const float LaunchCreditSeconds = 3f;
        private static readonly Dictionary<int, (int PlayerId, float Time)> Launches = new Dictionary<int, (int, float)>();

        public static void Count(int playerId, string effect, int amount = 1)
        {
            if (!Net.IsAuthority || playerId < 0 || amount <= 0) return;
            SpiderSurgeStatsManager.RunSafely("ability effect", stats => stats.LogAbilityEffect(playerId, effect, amount));
        }

        public static void Launched(EnemyHealthSystem enemy, int playerId)
        {
            if (!Net.IsAuthority || enemy == null) return;
            Launches[enemy.GetInstanceID()] = (playerId, Time.time);
            Count(playerId, EnemiesLaunched);
        }

        // An enemy that dies while frozen, or soon after a pulse launched it, counts for that ability's player, whoever killed it
        public static void EnemyDied(EnemyHealthSystem enemy)
        {
            if (FrozenEnemy.IsFrozen(enemy)) Count(FrozenEnemy.FreezerOf(enemy), FrozenKills);
            int id = enemy.GetInstanceID();
            if (Launches.TryGetValue(id, out var launch))
            {
                Launches.Remove(id);
                if (Time.time - launch.Time <= LaunchCreditSeconds) Count(launch.PlayerId, LaunchedKills);
            }
        }

        public static void Clear() => Launches.Clear();
    }
}
