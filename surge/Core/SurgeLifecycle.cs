using SpiderSurge.Common;
using SpiderSurge.Logging;
using UnityEngine.SceneManagement;

namespace SpiderSurge
{
    // Surge's part of the game lifecycle; within each event the handlers run in the order they are added here
    internal static class SurgeLifecycle
    {
        private static SpiderSurgeStatsManager Stats => SpiderSurgeStatsManager.Instance;

        internal static void Subscribe() => Hook(true);

        internal static void Unsubscribe() => Hook(false);

        private static void Hook(bool on)
        {
            if (on)
            {
                GameEvents.RunStarted += SurvivalMode_StartGame_Patch.OnRunStarted;
                GameEvents.RunStarted += StartTracking;
                GameEvents.RunStopping += StopTracking;
                GameEvents.RunStopped += EndRun;
                GameEvents.RunStopped += SendTelemetry;
                GameEvents.WaveStarted += ResetRevives;
                GameEvents.WaveStarted += OpenWave;
                GameEvents.WaveCompleted += AbilityAchievementTracker.CompleteWave;
                GameEvents.WaveCompleted += CloseWave;
                GameEvents.WaveFailed += ChallengeController.OnWaveFailed;
                GameEvents.WaveFailed += FailWave;
                GameEvents.PerkSubmitted += LuckAchievement.OnPerkSubmitted;
                GameEvents.PerkSubmitted += PerkSubmitted;
                GameEvents.PerkChoosing += LuckAchievement.OnPerkChoosing;
                GameEvents.PerkChoosing += ChallengeMapChoicePatch.OnPerkChoosing;
                GameEvents.PerkChosen += PerkChosen;
                GameEvents.SceneLoaded += OnSceneLoaded;
                GameEvents.RoundStarting += ResetRevives;
            }
            else
            {
                GameEvents.RunStarted -= SurvivalMode_StartGame_Patch.OnRunStarted;
                GameEvents.RunStarted -= StartTracking;
                GameEvents.RunStopping -= StopTracking;
                GameEvents.RunStopped -= EndRun;
                GameEvents.RunStopped -= SendTelemetry;
                GameEvents.WaveStarted -= ResetRevives;
                GameEvents.WaveStarted -= OpenWave;
                GameEvents.WaveCompleted -= AbilityAchievementTracker.CompleteWave;
                GameEvents.WaveCompleted -= CloseWave;
                GameEvents.WaveFailed -= ChallengeController.OnWaveFailed;
                GameEvents.WaveFailed -= FailWave;
                GameEvents.PerkSubmitted -= LuckAchievement.OnPerkSubmitted;
                GameEvents.PerkSubmitted -= PerkSubmitted;
                GameEvents.PerkChoosing -= LuckAchievement.OnPerkChoosing;
                GameEvents.PerkChoosing -= ChallengeMapChoicePatch.OnPerkChoosing;
                GameEvents.PerkChosen -= PerkChosen;
                GameEvents.SceneLoaded -= OnSceneLoaded;
                GameEvents.RoundStarting -= ResetRevives;
            }
        }

        // After the run's Surge state is set, since tracking records whether this is a Surge run
        private static void StartTracking(SurvivalMode survival, SurvivalConfig config) => Stats.StartTracking();

        private static void StopTracking(SurvivalMode survival) => Stats.StopTrackingAndLog();

        private static void EndRun() => SurgeGameModeManager.EndRun(RunEnd.Stopped);

        // Stats worked out the titles while the run was stopping, so the telemetry report can carry them
        private static void SendTelemetry() => Stats.SendPendingTelemetry();

        private static void ResetRevives(SurvivalMode survival) => AchievementTracker.ResetRevives();

        private static void ResetRevives(VersusMode versus) => AchievementTracker.ResetRevives();

        private static void OpenWave(SurvivalMode survival) => Stats.OnWaveStarted();

        private static void CloseWave(SurvivalMode survival)
        {
            if (Net.IsAuthority) Stats.IncrementWave();
        }

        private static void FailWave(SurvivalMode survival, bool lostLife) => Stats.OnWaveFailed();

        private static void PerkSubmitted() => Stats.OnPerkSubmitted();

        private static void PerkChosen(Modifier modifier) => Stats.OnPerkChosen(modifier);

        private static void OnSceneLoaded(Scene scene)
        {
            if (scene.name == "Lobby")
            {
                SurgeGameModeManager.EndRun(RunEnd.Lobby);
                SurgeLobbyPlatform.EnsureCreated();
            }
        }
    }
}
