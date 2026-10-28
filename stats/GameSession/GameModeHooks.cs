using HarmonyLib;
using Logger = Silk.Logger;
using System;
using SpiderSurge.Common;
using UnityEngine.SceneManagement;

namespace StatsMod
{
    // Stats' part of the game lifecycle; the versus match start and end and the restart keep patches of their own
    public static class GameModeHooks
    {
        public static void Subscribe()
        {
            GameEvents.RunStarting += OnRunStarting;
            GameEvents.RunStopping += OnRunStopping;
            GameEvents.WaveCompleted += OnWaveCompleted;
            GameEvents.PerkApplying += OnPerkApplying;
            GameEvents.SceneLoaded += OnSceneLoaded;
            GameEvents.RoundStarting += OnRoundStarting;
        }

        public static void Unsubscribe()
        {
            GameEvents.RunStarting -= OnRunStarting;
            GameEvents.RunStopping -= OnRunStopping;
            GameEvents.WaveCompleted -= OnWaveCompleted;
            GameEvents.PerkApplying -= OnPerkApplying;
            GameEvents.SceneLoaded -= OnSceneLoaded;
            GameEvents.RoundStarting -= OnRoundStarting;
        }

        private static void OnRunStarting(SurvivalMode survival)
        {
            GameSessionManager.Instance.StartSurvivalSession();
            GameSessionManager.Instance.RecordPainLevel();
            Logger.LogInfo("Survival mode started");
        }

        private static void OnRunStopping(SurvivalMode survival)
        {
            GameSessionManager.Instance.StopSurvivalSession();
            Logger.LogInfo("Survival mode stopped");
        }

        private static void OnWaveCompleted(SurvivalMode survival)
        {
            MatchHistory.MarkSurvivalCompletion(survival);
            GameSessionManager.Instance.CheckWaveClutch();
        }

        private static void OnPerkApplying(Modifier modifier)
        {
            if (GameSessionManager.Instance.IsActive && modifier != null && modifier.data != null)
            {
                string perkName = modifier.data.title;
                if (string.IsNullOrEmpty(perkName))
                {
                    perkName = modifier.data.key;
                }
                GameSessionManager.Instance.RecordPerk(perkName);
            }
        }

        private static void OnRoundStarting(VersusMode versus)
        {
            if (GameSessionManager.Instance.CurrentGameMode == GameMode.Versus)
            {
                HitLogic.Clear();
            }
        }

        private static void OnSceneLoaded(Scene scene)
        {
            if (scene.name.Equals("Lobby"))
            {
                bool hasPendingTitles = TitlesManager.Instance.HasGameEndedTitles;

                if (hasPendingTitles || RunRecap.Last != null)
                {
                    UIManager.AutoShowTitles(animate: true);
                }
                else
                {
                    UIManager.AutoPullHUD();
                }
            }
        }
    }

    #region Versus Mode Hooks

    [HarmonyPatch(typeof(VersusMode), "StartMatch")]
    public class VersusModeStartPatch
    {
        static void Prefix(VersusMode __instance)
        {
            try
            {
                GameSessionManager.Instance.StartVersusSession();
                Logger.LogInfo("Versus mode started");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error in VersusMode.StartMatch patch: {ex.Message}");
            }
        }
    }

    [HarmonyPatch(typeof(VersusMode), "EndMatch")]
    public class VersusModeEndPatch
    {
        [HarmonyPriority(Priority.First)]
        static void Prefix(VersusMode __instance)
        {
            try
            {
                if (GameSessionManager.Instance.CurrentGameMode != GameMode.Versus) return;
                GameSessionManager.Instance.StopVersusSession();
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error in VersusMode.EndMatch patch: {ex.Message}");
            }
        }
    }

    // Surge's "Are you sure?" popup and the pause menu both restart through here, and only once the restart really happens
    [HarmonyPatch(typeof(GameController), nameof(GameController.Restart))]
    public class GameControllerRestartPatch
    {
        static void Prefix()
        {
            try
            {
                GameSessionManager.Instance.MarkRestartRequested();
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error in GameController.Restart patch: {ex.Message}");
            }
        }
    }

    // GameController.Restart is small enough for Mono to inline into HudController.Restart, so that path is marked here too
    [HarmonyPatch(typeof(HudController), nameof(HudController.Restart))]
    public class HudControllerRestartPatch
    {
        [HarmonyPriority(Priority.Last)]
        static void Prefix(bool __runOriginal)
        {
            try
            {
                if (__runOriginal)
                {
                    GameSessionManager.Instance.MarkRestartRequested();
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error in HudController.Restart patch: {ex.Message}");
            }
        }
    }

    #endregion
}
