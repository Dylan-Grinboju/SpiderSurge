using System;
using HarmonyLib;
using Unity.Netcode;
using Logger = Silk.Logger;

namespace SpiderTeams
{
    [HarmonyPatch(typeof(VersusMode), nameof(VersusMode.StartMatch))]
    public static class VersusMode_StartMatch_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(VersusMode __instance)
        {
            try
            {
                var manager = TeamManager.Instance;
                if (manager != null && __instance.GameModeActive())
                {
                    manager.BeginMatch();
                }
                SurgeVersus.OnMatchStarted();
            }
            catch (Exception ex)
            {
                Logger.LogError($"SpiderTeams StartMatch patch failed: {ex}");
            }
        }
    }

    [HarmonyPatch(typeof(VersusMode), nameof(VersusMode.StartRound))]
    public static class VersusMode_StartRound_Patch
    {
        [HarmonyPrefix]
        public static void Prefix(VersusMode __instance)
        {
            try
            {
                var manager = TeamManager.Instance;
                if (manager == null || !TeamManager.IsHost)
                {
                    return;
                }

                manager.OnRoundStarting();
                bool waitingRoom = ReflectionCache.IsWaitingRoom(__instance);
                if (TeamVisuals.Instance != null && !waitingRoom)
                {
                    TeamVisuals.Instance.ApplyBeforeSpawn();
                }
                SurgeVersus.OnRoundStarting(__instance, waitingRoom);
            }
            catch (Exception ex)
            {
                Logger.LogError($"SpiderTeams StartRound patch failed: {ex}");
            }
        }
    }

    [HarmonyPatch(typeof(VersusMode), nameof(VersusMode.LateUpdate))]
    public static class VersusMode_LateUpdate_Patch
    {
        private static bool _loggedError;

        [HarmonyPrefix]
        public static bool Prefix(VersusMode __instance)
        {
            try
            {
                if (!RoundRules.Available || !TeamManager.TeamMatchActive)
                {
                    return true;
                }

                RoundRules.LateUpdate(__instance);
                return false;
            }
            catch (Exception ex)
            {
                if (!_loggedError)
                {
                    _loggedError = true;
                    Logger.LogError($"SpiderTeams round check failed, using the vanilla rules: {ex}");
                }
                return true;
            }
        }
    }

    [HarmonyPatch(typeof(VersusMode), "DeclareWinner")]
    public static class VersusMode_DeclareWinner_Patch
    {
        [HarmonyPrefix]
        public static bool Prefix(VersusMode __instance)
        {
            bool active;
            try
            {
                active = RoundRules.Available && TeamManager.TeamMatchActive;
            }
            catch (Exception ex)
            {
                Logger.LogError($"SpiderTeams DeclareWinner check failed: {ex}");
                return true;
            }

            if (!active)
            {
                return true;
            }

            try
            {
                RoundRules.DeclareWinner(__instance);
            }
            catch (Exception ex)
            {
                Logger.LogError($"SpiderTeams could not settle the round: {ex}");
                RoundRules.ScheduleNextRoundAfterFailure(__instance);
            }
            return false;
        }
    }

    [HarmonyPatch(typeof(VersusMode), nameof(VersusMode.ShowScorePrompts))]
    public static class VersusMode_ShowScorePrompts_Patch
    {
        [HarmonyPrefix]
        public static bool Prefix()
        {
            return !RoundRules.SuppressScorePrompts;
        }
    }

    [HarmonyPatch(typeof(VersusMode), nameof(VersusMode.EndMatch))]
    public static class VersusMode_EndMatch_Patch
    {
        [HarmonyPrefix]
        public static void Prefix(out bool __state)
        {
            __state = false;
            try
            {
                __state = RoundRules.Available && TeamManager.TeamMatchActive;
            }
            catch (Exception ex)
            {
                Logger.LogError($"SpiderTeams EndMatch check failed: {ex}");
            }
        }

        // Snapshot first: it reads the cached original colors that RestoreAll then clears.
        [HarmonyPostfix]
        public static void Postfix(VersusMode __instance, bool celebrate, bool __state)
        {
            try
            {
                if (__instance.GameModeActive())
                {
                    return;
                }

                if (__state)
                {
                    MatchSnapshot.Capture(celebrate);
                }

                var manager = TeamManager.Instance;
                if (manager != null)
                {
                    manager.EndMatch();
                }
                if (TeamVisuals.Instance != null)
                {
                    TeamVisuals.Instance.RestoreAll();
                }
                SurgeVersus.Stop();
            }
            catch (Exception ex)
            {
                Logger.LogError($"SpiderTeams EndMatch patch failed: {ex}");
            }
        }
    }

    [HarmonyPatch(typeof(VersusMode), "StartMatchClientRpc")]
    public static class VersusMode_StartMatchClientRpc_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(VersusMode __instance)
        {
            try
            {
                var network = NetworkManager.Singleton;
                if (network == null || !network.IsListening || network.IsHost || ReflectionCache.IsRpcSendStage(__instance))
                {
                    return;
                }

                var manager = TeamManager.Instance;
                if (manager != null)
                {
                    manager.ShowClientNotice();
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"SpiderTeams StartMatchClientRpc patch failed: {ex}");
            }
        }
    }
}
