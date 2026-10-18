using HarmonyLib;
using Logger = Silk.Logger;
using System;
using UnityEngine.SceneManagement;

namespace StatsMod
{
    // Consolidated Harmony patches for game mode lifecycle events.
    // Handles survival and versus mode start/stop, lobby transitions.

    #region Survival Mode Hooks

    [HarmonyPatch(typeof(SurvivalMode), "StartGame")]
    public class SurvivalModeStartPatch
    {
        static void Prefix(SurvivalMode __instance, SurvivalConfig survivalConfig)
        {
            try
            {
                if (survivalConfig != null && !__instance.GameModeActive())
                {
                    GameSessionManager.Instance.StartSurvivalSession();
                    GameSessionManager.Instance.RecordPainLevel();
                    Logger.LogInfo("Survival mode started");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error in SurvivalMode.StartGame patch: {ex.Message}");
            }
        }
    }

    [HarmonyPatch(typeof(SurvivalMode), "StopGameMode")]
    public class SurvivalModeStopPatch
    {
        static void Prefix(SurvivalMode __instance)
        {
            try
            {
                if (__instance.GameModeActive())
                {
                    GameSessionManager.Instance.StopSurvivalSession();
                    Logger.LogInfo("Survival mode stopped");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error in SurvivalMode.StopGameMode patch: {ex.Message}");
            }
        }
    }

    [HarmonyPatch(typeof(SurvivalMode), "CompleteWave")]
    public class SurvivalModeCompleteWavePatch
    {
        static void Prefix(SurvivalMode __instance)
        {
            try
            {
                GameSessionManager.Instance.CheckWaveClutch();
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error in SurvivalMode.CompleteWave patch: {ex.Message}");
            }
        }
    }

    #endregion

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

    [HarmonyPatch(typeof(VersusMode), "StartRound")]
    public class VersusModeStartRoundPatch
    {
        static void Prefix()
        {
            if (GameSessionManager.Instance.CurrentGameMode == GameMode.Versus)
            {
                HitLogic.Clear();
            }
        }
    }

    [HarmonyPatch(typeof(VersusMode), "EndMatch")]
    public class VersusModeEndPatch
    {
        static void Prefix(VersusMode __instance)
        {
            try
            {
                // GameController.CancelEverything ends the match on every survival start and lobby return too
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

    #region Lobby Transition Hooks

    [HarmonyPatch(typeof(LobbyController), "OnSceneLoaded")]
    public class LobbySceneLoadedPatch
    {
        static void Postfix(Scene scene, LoadSceneMode mode)
        {
            try
            {
                if (scene.name.Equals("Lobby"))
                {
                    bool hasPendingTitles = TitlesManager.Instance.HasGameEndedTitles && TitlesManager.Instance.TitleCount > 0;

                    if (hasPendingTitles)
                    {
                        UIManager.AutoShowTitles(animate: true);
                    }
                    else
                    {
                        UIManager.AutoPullHUD();
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error in LobbyController.OnSceneLoaded patch: {ex.Message}");
            }
        }
    }

    #endregion
}
