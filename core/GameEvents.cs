using System;
using System.Reflection;
using HarmonyLib;
using Unity.Netcode;
using UnityEngine.SceneManagement;

namespace SpiderSurge.Common
{
    // The game's survival, perk, lobby and versus round lifecycle, each raised from one patch with its condition worked out once.
    // Parts subscribe in OnStart, so every handler runs in Parts.All order, the order the parts' own patches used to run in.
    public static class GameEvents
    {
        internal static readonly AccessTools.FieldRef<SurvivalMode, bool> WaveInProgress = AccessTools.FieldRefAccess<SurvivalMode, bool>("_waveInProgress");
        internal static readonly AccessTools.FieldRef<SurvivalMode, NetworkVariable<bool>> WaitingForPerk = AccessTools.FieldRefAccess<SurvivalMode, NetworkVariable<bool>>("_waitingForPerk");

        // StartGame was called with a config while no run is active
        public static event Action<SurvivalMode> RunStarting;
        // StartGame started a run, with the config the run really uses
        public static event Action<SurvivalMode, SurvivalConfig> RunStarted;
        // StopGameMode is ending an active run: every part reads its run state here, before any part resets it
        public static event Action<SurvivalMode> RunStopping;
        public static event Action RunStopped;
        // StartWave really started a wave; the game calls it every frame while a wave can't start yet
        public static event Action<SurvivalMode> WaveStarted;
        // The first step of CompleteWave, while the wave still counts as in progress
        public static event Action<SurvivalMode> WaveCompleted;
        // FailWave returned; the flag says whether it cost a life
        public static event Action<SurvivalMode, bool> WaveFailed;
        // Every ChoosePerkAndLevel call, before the level changes
        public static event Action<Modifier> PerkApplying;
        // A perk chosen at a perk prompt, before and after it applies
        public static event Action<Modifier, GameLevel> PerkChoosing;
        public static event Action<Modifier> PerkChosen;
        public static event Action PerkSubmitted;
        // LobbyController.OnSceneLoaded, for every scene it hears about
        public static event Action<Scene> SceneLoaded;
        public static event Action<VersusMode> RoundStarting;

        [HarmonyPatch(typeof(SurvivalMode), "StartGame")]
        private static class StartGamePatch
        {
            static void Prefix(SurvivalMode __instance, SurvivalConfig survivalConfig)
            {
                if (survivalConfig != null && !__instance.GameModeActive()) Raise(RunStarting, __instance, nameof(RunStarting));
            }

            static void Postfix(SurvivalMode __instance, bool __result, SurvivalConfig survivalConfig)
            {
                if (__result) Raise(RunStarted, __instance, survivalConfig, nameof(RunStarted));
            }
        }

        [HarmonyPatch(typeof(SurvivalMode), "StopGameMode")]
        private static class StopGameModePatch
        {
            static void Prefix(SurvivalMode __instance)
            {
                if (__instance.GameModeActive()) Raise(RunStopping, __instance, nameof(RunStopping));
            }

            static void Postfix() => Listeners.Raise(RunStopped, Failure(nameof(RunStopped)));
        }

        [HarmonyPatch(typeof(SurvivalMode), "StartWave")]
        private static class StartWavePatch
        {
            static void Prefix(SurvivalMode __instance, out bool __state) => __state = WaveInProgress(__instance);

            static void Postfix(SurvivalMode __instance, bool __state)
            {
                if (!__state && WaveInProgress(__instance)) Raise(WaveStarted, __instance, nameof(WaveStarted));
            }
        }

        // Patched on the coroutine body, not its stub: the game's call to the tiny stub is inlined, so a prefix there never runs in play.
        // The first MoveNext respawns the dead and clears _waveInProgress, so handlers run while the flag is still set
        [HarmonyPatch]
        private static class CompleteWavePatch
        {
            static MethodBase TargetMethod() => AccessTools.EnumeratorMoveNext(AccessTools.Method(typeof(SurvivalMode), "CompleteWave"));

            static void Prefix()
            {
                SurvivalMode survival = SurvivalMode.instance;
                if (survival != null && WaveInProgress(survival)) Raise(WaveCompleted, survival, nameof(WaveCompleted));
            }
        }

        [HarmonyPatch(typeof(SurvivalMode), "FailWave")]
        private static class FailWavePatch
        {
            static void Prefix(SurvivalMode __instance, out int __state) => __state = __instance.Lives;

            static void Postfix(SurvivalMode __instance, int __state) => Raise(WaveFailed, __instance, __instance.Lives < __state, nameof(WaveFailed));
        }

        [HarmonyPatch(typeof(SurvivalMode), nameof(SurvivalMode.ChoosePerkAndLevel))]
        private static class ChoosePerkAndLevelPatch
        {
            static void Prefix(SurvivalMode __instance, Modifier modifier, GameLevel level, out bool __state)
            {
                Raise(PerkApplying, modifier, nameof(PerkApplying));
                var waiting = WaitingForPerk(__instance);
                __state = waiting != null && waiting.Value;
                if (__state) Raise(PerkChoosing, modifier, level, nameof(PerkChoosing));
            }

            static void Postfix(Modifier modifier, bool __state)
            {
                if (__state) Raise(PerkChosen, modifier, nameof(PerkChosen));
            }
        }

        [HarmonyPatch(typeof(SurvivalModeHud), nameof(SurvivalModeHud.PerkChoiceSubmit))]
        private static class PerkChoiceSubmitPatch
        {
            static void Prefix() => Listeners.Raise(PerkSubmitted, Failure(nameof(PerkSubmitted)));
        }

        [HarmonyPatch(typeof(LobbyController), "OnSceneLoaded")]
        private static class SceneLoadedPatch
        {
            static void Postfix(Scene scene) => Raise(SceneLoaded, scene, nameof(SceneLoaded));
        }

        [HarmonyPatch(typeof(VersusMode), nameof(VersusMode.StartRound))]
        private static class StartRoundPatch
        {
            static void Prefix(VersusMode __instance) => Raise(RoundStarting, __instance, nameof(RoundStarting));
        }

        private static string Failure(string name) => $"SpiderSurge {name} handler failed";

        private static void Raise<T>(Action<T> handler, T value, string name)
        {
            if (handler != null) Listeners.Raise(handler, value, Failure(name));
        }

        private static void Raise<T1, T2>(Action<T1, T2> handler, T1 first, T2 second, string name)
        {
            if (handler != null) Listeners.Raise(handler, first, second, Failure(name));
        }
    }
}
