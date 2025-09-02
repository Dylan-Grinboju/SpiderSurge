using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Silk;
using Logger = Silk.Logger;

namespace StatsMod
{
    public class StatsManager
    {
        private static readonly Lazy<StatsManager> _lazy = new Lazy<StatsManager>(() => new StatsManager());
        public static StatsManager Instance => _lazy.Value;

        private PlayerTracker playerTracker;
        private EnemiesTracker enemiesTracker;

        private bool isSurvivalActive = false;
        private DateTime survivalStartTime;
        private TimeSpan lastGameDuration;

        public bool IsSurvivalActive => isSurvivalActive;
        public DateTime SurvivalStartTime => survivalStartTime;
        public TimeSpan LastGameDuration => lastGameDuration;

        public PlayerTracker PlayerTracker => playerTracker;
        public EnemiesTracker EnemiesTracker => enemiesTracker;

        private StatsManager()
        {
            playerTracker = PlayerTracker.Instance;
            enemiesTracker = EnemiesTracker.Instance;
            Logger.LogInfo("Stats manager initialized");
        }

        public void StartSurvivalSession()
        {
            if (isSurvivalActive)
            {
                Logger.LogWarning("Attempting to start survival session while one is already active");
                return;
            }

            isSurvivalActive = true;
            survivalStartTime = DateTime.Now;

            playerTracker.ResetPlayerStats();
            enemiesTracker.ResetEnemiesKilled();

            Logger.LogInfo($"Survival session started");
        }

        public void StopSurvivalSession()
        {
            if (!isSurvivalActive)
            {
                Logger.LogWarning("Attempting to stop survival session when none is active");
                return;
            }

            TimeSpan sessionTime = DateTime.Now - survivalStartTime;
            lastGameDuration = sessionTime;
            isSurvivalActive = false;

            GameStatsSnapshot statsSnapshot = GetStatsSnapshot();

            if (ModConfig.SaveStatsToFile)
            {
                StatsLogger.Instance.LogGameStats(statsSnapshot);
            }

            Logger.LogInfo($"Survival session stopped. Duration: {FormatTimeSpan(sessionTime)}");
        }

        public TimeSpan GetCurrentSessionTime()
        {
            if (!isSurvivalActive)
                return TimeSpan.Zero;

            return DateTime.Now - survivalStartTime;
        }

        public Dictionary<PlayerInput, PlayerTracker.PlayerData> GetActivePlayers()
        {
            return playerTracker.GetActivePlayers();
        }

        public int GetEnemiesKilled()
        {
            return enemiesTracker.GetEnemiesKilled();
        }

        public void RegisterPlayer(PlayerInput player)
        {
            playerTracker.RegisterPlayer(player);
        }

        public void UnregisterPlayer(PlayerInput player)
        {
            playerTracker.UnregisterPlayer(player);
        }

        public void IncrementPlayerDeath(PlayerInput player)
        {
            playerTracker.IncrementPlayerDeath(player);
        }

        public void IncrementPlayerKill(PlayerInput player)
        {
            playerTracker.IncrementPlayerKill(player);
        }

        public void UpdatePlayerColor(PlayerInput player, Color color)
        {
            playerTracker.UpdatePlayerColor(player, color);
        }

        public void IncrementEnemyKilled()
        {
            enemiesTracker.IncrementEnemiesKilled();
        }

        public void RecordPlayerDeath(SpiderHealthSystem spiderHealth)
        {
            playerTracker.RecordPlayerDeath(spiderHealth);
        }

        public void UndoPlayerDeath(SpiderHealthSystem spiderHealth)
        {
            playerTracker.UndoPlayerDeath(spiderHealth);
        }

        public GameStatsSnapshot GetStatsSnapshot()
        {
            return new GameStatsSnapshot
            {
                IsSurvivalActive = isSurvivalActive,
                CurrentSessionTime = GetCurrentSessionTime(),
                LastGameDuration = lastGameDuration,
                ActivePlayers = new Dictionary<PlayerInput, PlayerTracker.PlayerData>(GetActivePlayers()),
                EnemiesKilled = GetEnemiesKilled()
            };
        }

        private string FormatTimeSpan(TimeSpan timeSpan)
        {
            return $"{timeSpan.Hours:00}:{timeSpan.Minutes:00}:{timeSpan.Seconds:00}";
        }
    }

    public class GameStatsSnapshot
    {
        public bool IsSurvivalActive { get; set; }
        public TimeSpan CurrentSessionTime { get; set; }
        public TimeSpan LastGameDuration { get; set; }
        public int TotalGamesPlayed { get; set; }
        public Dictionary<PlayerInput, PlayerTracker.PlayerData> ActivePlayers { get; set; }
        public int TotalPlayerDeaths { get; set; }
        public int TotalPlayerKills { get; set; }
        public int EnemiesKilled { get; set; }
    }
}
