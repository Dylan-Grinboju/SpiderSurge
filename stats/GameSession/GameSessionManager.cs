using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SpiderSurge.Common;
using UnityEngine;
using Logger = Silk.Logger;

namespace StatsMod
{
    /// <summary>
    /// Manages game session lifecycle, timing, and state.
    /// Coordinates between trackers and handles session start/stop events.
    /// </summary>
    public class GameSessionManager
    {
        private static readonly Lazy<GameSessionManager> _lazy = new Lazy<GameSessionManager>(() => new GameSessionManager());
        public static GameSessionManager Instance => _lazy.Value;

        private readonly PlayerTracker playerTracker;
        private readonly EnemiesTracker enemiesTracker;

        private bool isSurvivalActive = false;
        private bool isVersusActive = false;
        private DateTime sessionStartTime;
        private TimeSpan lastGameDuration;
        private GameMode currentGameMode = GameMode.None;
        private GameMode lastGameMode = GameMode.None;

        private bool isPaused = false;
        private DateTime pauseStartTime;

        private readonly List<string> mapsPlayed = new List<string>();
        private const int MaxListSent = 32;
        private readonly List<string> perksChosen = new List<string>();
        private int painLevel = -1;
        private bool restartRequested = false;
        private bool pauseAfterRestartOnNextSessionStart = false;

        private GameStatsSnapshot displaySnapshot;
        private float displaySnapshotTime;
        private int displaySnapshotRoster = -1;
        private const float DisplaySnapshotSeconds = 0.25f;

        private float remoteSessionSeconds;
        private float remoteReceivedAt;
        private bool remoteRestartRequested;
        private bool isMirrored;

        public bool IsActive => isSurvivalActive || isVersusActive;
        public GameMode CurrentGameMode => currentGameMode;
        public GameMode LastGameMode => lastGameMode;
        public string CurrentMapName => mapsPlayed.Count > 0 ? mapsPlayed[mapsPlayed.Count - 1] : "Unknown";

        private GameSessionManager()
        {
            playerTracker = PlayerTracker.Instance;
            enemiesTracker = EnemiesTracker.Instance;
        }

        #region Session Lifecycle

        public void StartSurvivalSession()
        {
            StartSession(GameMode.Survival);
        }

        public void StartVersusSession()
        {
            StartSession(GameMode.Versus);
        }

        private void StartSession(GameMode mode)
        {
            if (Net.IsRemoteClient) return;
            if (IsActive)
            {
                Logger.LogWarning($"Attempting to start {mode} session while one is already active");
                return;
            }

            isSurvivalActive = mode == GameMode.Survival;
            isVersusActive = mode == GameMode.Versus;
            currentGameMode = mode;
            sessionStartTime = DateTime.Now;

            MatchHistory.Begin();
            RunRecap.Begin();
            SpiderPortraits.ClearFinished();
            HitLogic.Clear();
            playerTracker.RefreshIdentities();
            playerTracker.ResetPlayerStats();
            enemiesTracker.ResetEnemiesKilled();
            playerTracker.StartAllAliveTimers();
            StatsModApi.ClearExternalData();

            if (mode == GameMode.Versus)
            {
                TitlesManager.Instance.ClearTitles();
            }

            mapsPlayed.Clear();
            perksChosen.Clear();
            painLevel = -1;

            bool keepEndOfGameTitlesVisible = pauseAfterRestartOnNextSessionStart &&
                                             TitlesManager.Instance.HasGameEndedTitles &&
                                             TitlesManager.Instance.TitleCount > 0;

            UIManager.OnGameSessionStarted(keepEndOfGameTitlesVisible);

            if (pauseAfterRestartOnNextSessionStart)
            {
                UIManager.PauseGameLikeEscDelayed(3f);
                pauseAfterRestartOnNextSessionStart = false;
            }

            StatsNet.SendSnapshot(SnapshotReason.Started);
        }

        public void StopSurvivalSession()
        {
            StopSession(GameMode.Survival);
        }

        public void StopVersusSession()
        {
            StopSession(GameMode.Versus);
        }

        private void StopSession(GameMode mode)
        {
            if (Net.IsRemoteClient || isMirrored) return;
            bool isCorrectMode = (mode == GameMode.Survival && isSurvivalActive) ||
                                 (mode == GameMode.Versus && isVersusActive);

            if (!isCorrectMode)
            {
                Logger.LogWarning($"Attempting to stop {mode} session when none is active");
                return;
            }

            playerTracker.StopAllAliveTimers();
            playerTracker.StopAllWebSwingTimers();
            playerTracker.StopAllAirborneTimers();

            TimeSpan sessionTime = (isPaused ? pauseStartTime : DateTime.Now) - sessionStartTime;
            lastGameDuration = sessionTime;
            isSurvivalActive = false;
            isVersusActive = false;
            isPaused = false;
            playerTracker.ClearPause();

            GameStatsSnapshot statsSnapshot = GetStatsSnapshot();

            SpiderPortraits.CaptureFinished(statsSnapshot.ActivePlayers.Keys);
            TitlesManager.Instance.CalculateAndStoreTitles(statsSnapshot);
            statsSnapshot.Titles = TitlesManager.Instance.AllTitles;
            RunRecap.Capture();
            lastGameMode = currentGameMode;
            currentGameMode = GameMode.None;
            StatsNet.SendSnapshot(SnapshotReason.Stopped);
            StatsNet.SendTitles();

            if (ModConfig.SaveStatsToFile)
            {
                StatsLogger.Instance.LogGameStats(statsSnapshot);
            }

            int playerCount = statsSnapshot.ActivePlayers?.Count ?? 0;
            bool shouldShowTitles = ModConfig.TitlesEnabled &&
                                    playerCount > 1 &&
                                    TitlesManager.Instance.HasGameEndedTitles &&
                                    TitlesManager.Instance.TitleCount > 0;
            int? newlyUnlocked = shouldShowTitles ? TitleCollection.Record(TitlesManager.Instance.CurrentTitles.Select(title => title.TitleName)) : null;
            TitlesManager.Instance.SetRunOutcome(shouldShowTitles, newlyUnlocked);
            MatchHistory.Complete(statsSnapshot, restartRequested);

            if (shouldShowTitles && ModConfig.ShowStatsWindow && restartRequested)
            {
                UIManager.AutoShowTitles(animate: false);
                pauseAfterRestartOnNextSessionStart = true;
            }
            else if (!shouldShowTitles)
            {
                UIManager.AutoPullHUD();
            }

            restartRequested = false;
            Logger.LogInfo($"{mode} session stopped. Duration: {TimeFormatUtils.FormatTimeSpan(sessionTime)}");
        }

        public void MarkRestartRequested()
        {
            if (Net.IsRemoteClient || !isSurvivalActive) return;
            restartRequested = true;
        }

        #endregion

        #region Timing

        public TimeSpan GetCurrentSessionTime()
        {
            if (Net.IsRemoteClient)
                return TimeSpan.FromSeconds(remoteSessionSeconds + (IsActive ? Time.unscaledTime - remoteReceivedAt : 0f));
            if (!IsActive)
                return TimeSpan.Zero;

            DateTime endTime = isPaused ? pauseStartTime : DateTime.Now;
            return endTime - sessionStartTime;
        }

        public void PauseTimers()
        {
            if (isPaused || !IsActive || Net.IsRemoteClient)
                return;

            isPaused = true;
            pauseStartTime = DateTime.Now;
            playerTracker.PauseTimers();
        }

        public void ResumeTimers()
        {
            if (!isPaused || !IsActive || Net.IsRemoteClient)
                return;

            TimeSpan pausedDuration = DateTime.Now - pauseStartTime;
            sessionStartTime = sessionStartTime.Add(pausedDuration);
            isPaused = false;
            playerTracker.ResumeTimers();
        }

        #endregion

        #region Map and Perk Tracking

        public void RecordMap(string mapName)
        {
            if (!IsActive || !Net.IsAuthority) return;
            if (string.IsNullOrEmpty(mapName)) return;

            mapsPlayed.Add(mapName);
        }

        public void RecordPerk(string perkName)
        {
            if (!IsActive || !Net.IsAuthority) return;
            if (string.IsNullOrEmpty(perkName)) return;

            perksChosen.Add(perkName);
        }

        public void RecordPainLevel()
        {
            if (!IsActive || !Net.IsAuthority) return;

            try
            {
                if (PainLevelsScreen.instance != null)
                {
                    painLevel = PainLevelsScreen.instance.GetPainLevel();
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Could not read pain level: {ex.Message}");
            }
        }

        #endregion

        #region Wave Clutch (Survival-specific)

        public void CheckWaveClutch()
        {
            if (!isSurvivalActive || !Net.IsAuthority)
                return;

            var players = playerTracker.GetActivePlayers();
            if (players.Count < 2)
                return;

            PlayerController clutchPlayer = playerTracker.GetOnlyAlivePlayer();
            if (clutchPlayer != null)
            {
                playerTracker.IncrementWaveClutch(clutchPlayer);
            }
        }

        #endregion

        #region Stats Snapshot

        public GameStatsSnapshot GetStatsSnapshot()
        {
            var snapshot = new GameStatsSnapshot
            {
                ActivePlayers = playerTracker.GetActivePlayers(),
                MapsPlayed = new List<string>(mapsPlayed),
                PerksChosen = new List<string>(perksChosen)
            };
            FillSessionValues(snapshot);
            return snapshot;
        }

        // The stats windows draw every frame, so they share one snapshot whose lists are rebuilt when players change and a few times a second
        public GameStatsSnapshot GetDisplaySnapshot()
        {
            if (displaySnapshot == null || displaySnapshotRoster != playerTracker.RosterVersion ||
                Time.unscaledTime - displaySnapshotTime >= DisplaySnapshotSeconds)
            {
                displaySnapshot = GetStatsSnapshot();
                displaySnapshotRoster = playerTracker.RosterVersion;
                displaySnapshotTime = Time.unscaledTime;
            }
            else
            {
                FillSessionValues(displaySnapshot);
            }
            return displaySnapshot;
        }

        private void FillSessionValues(GameStatsSnapshot snapshot)
        {
            snapshot.IsSurvivalActive = isSurvivalActive;
            snapshot.IsVersusActive = isVersusActive;
            snapshot.GameMode = currentGameMode;
            snapshot.CurrentSessionTime = GetCurrentSessionTime();
            snapshot.LastGameDuration = lastGameDuration;
            snapshot.EnemiesKilled = enemiesTracker.EnemiesKilled;
            snapshot.PainLevel = painLevel;
        }

        #endregion

        #region Online

        public void WriteSession(BinaryWriter writer)
        {
            writer.Write(isSurvivalActive);
            writer.Write(isVersusActive);
            writer.Write((int)currentGameMode);
            writer.Write((int)lastGameMode);
            writer.Write((float)GetCurrentSessionTime().TotalSeconds);
            writer.Write((float)lastGameDuration.TotalSeconds);
            writer.Write(painLevel);
            writer.Write(enemiesTracker.EnemiesKilled);
            writer.Write(restartRequested);
            // A long session sends only its last maps and perks, so the snapshot stays under the message size limit
            WriteTail(writer, mapsPlayed);
            WriteTail(writer, perksChosen);
        }

        public void ReadSession(BinaryReader reader)
        {
            isMirrored = true;
            isSurvivalActive = reader.ReadBoolean();
            isVersusActive = reader.ReadBoolean();
            currentGameMode = (GameMode)reader.ReadInt32();
            lastGameMode = (GameMode)reader.ReadInt32();
            remoteSessionSeconds = reader.ReadSingle();
            remoteReceivedAt = Time.unscaledTime;
            lastGameDuration = TimeSpan.FromSeconds(reader.ReadSingle());
            painLevel = reader.ReadInt32();
            enemiesTracker.SetEnemiesKilled(reader.ReadInt32());
            remoteRestartRequested = reader.ReadBoolean();
            mapsPlayed.Clear();
            int maps = reader.ReadInt32();
            for (int i = 0; i < maps; i++) mapsPlayed.Add(reader.ReadString());
            perksChosen.Clear();
            int perks = reader.ReadInt32();
            for (int i = 0; i < perks; i++) perksChosen.Add(reader.ReadString());
        }

        private static void WriteTail(BinaryWriter writer, List<string> list)
        {
            int first = Math.Max(0, list.Count - MaxListSent);
            writer.Write(list.Count - first);
            for (int i = first; i < list.Count; i++) writer.WriteText(list[i]);
        }

        // Mirrors what StartSession does to the UI; the end-of-session UI waits for the titles message
        public void OnRemoteSnapshot(SnapshotReason reason)
        {
            if (reason != SnapshotReason.Started) return;
            RunRecap.Begin();

            if (currentGameMode == GameMode.Versus)
            {
                TitlesManager.Instance.ClearTitles();
            }

            bool keepEndOfGameTitlesVisible = pauseAfterRestartOnNextSessionStart &&
                                             TitlesManager.Instance.HasGameEndedTitles &&
                                             TitlesManager.Instance.TitleCount > 0;
            UIManager.OnGameSessionStarted(keepEndOfGameTitlesVisible);
            pauseAfterRestartOnNextSessionStart = false;
        }

        public void OnRemoteTitles(int titleCount)
        {
            int playerCount = playerTracker.GetActivePlayers().Count;
            bool shouldShowTitles = ModConfig.TitlesEnabled &&
                                    playerCount > 1 &&
                                    TitlesManager.Instance.HasGameEndedTitles &&
                                    TitlesManager.Instance.TitleCount > 0;

            if (shouldShowTitles && ModConfig.ShowStatsWindow && remoteRestartRequested)
            {
                UIManager.AutoShowTitles(animate: false);
                pauseAfterRestartOnNextSessionStart = true;
            }
            else if (!shouldShowTitles)
            {
                UIManager.AutoPullHUD();
            }
        }

        public void ClearRemote()
        {
            isSurvivalActive = false;
            isVersusActive = false;
            isPaused = false;
            currentGameMode = GameMode.None;
            lastGameMode = GameMode.None;
            lastGameDuration = TimeSpan.Zero;
            remoteSessionSeconds = 0f;
            remoteRestartRequested = false;
            restartRequested = false;
            isMirrored = false;
            pauseAfterRestartOnNextSessionStart = false;
            painLevel = -1;
            mapsPlayed.Clear();
            perksChosen.Clear();
            enemiesTracker.ResetEnemiesKilled();
        }

        #endregion
    }
}
