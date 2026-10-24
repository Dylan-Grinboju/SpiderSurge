using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using SpiderSurge.Common;
using Unity.Netcode;
using UnityEngine;
using Logger = Silk.Logger;

namespace SpiderSurge
{
    public static class ChallengeController
    {
        public const string OutcomeAccepted = "Accepted";
        public const string OutcomeSkipped = "Skipped";
        public const string OutcomeCompleted = "Completed";
        public const string OutcomeFailed = "Failed";

        private static readonly AccessTools.FieldRef<SurvivalMode, NetworkVariable<int>> Lives = AccessTools.FieldRefAccess<SurvivalMode, NetworkVariable<int>>("_lives");
        private static readonly Dictionary<ulong, bool> Votes = new Dictionary<ulong, bool>();
        public static ChallengeKind Offered { get; private set; }
        public static ChallengeKind Active { get; private set; }
        public static bool Pending { get; private set; }
        public static bool WagerFailed { get; private set; }
        public static int HeldLives { get; private set; }
        public static string Outcome { get; private set; } = "";
        public static int AcceptVotes { get; private set; }
        public static int SkipVotes { get; private set; }
        // Set by a genuine map choice; the offer opens on the new map before its first wave
        private static bool _armed;
        private static float _lastBlocked;
        private static bool _wasPending;
        private static bool _panelFailed;
        public static float CooldownMultiplier => Active == ChallengeKind.DoubleCooldown ? 2f : 1f;
        public static string Status => Active == ChallengeKind.None ? "" :
            $"{ChaosRules.Name(Active)} · {(WagerFailed ? "Reward lost" : $"{ChaosRules.Points(ChaosRules.Reward(Active), signed: true)} on completion")}";

        public static void Arm()
        {
            if (Net.IsAuthority) _armed = true;
        }

        // Host only, from the StartWave prefix: holds the wave while the offer is open, the way the starting ability pick holds wave 1
        public static bool BeforeWave()
        {
            if (Pending) return false;
            if (!_armed) return true;
            // Pausing during the loading screen would stop it, since its fade-out waits on game time
            if (LevelController.IsLoading) return false;
            _armed = false;
            if (!ChaosWallet.Enabled || SurvivalMode.instance == null) return true;
            Begin();
            return !Pending;
        }

        private static void Begin()
        {
            bool abilities = !SurgeGameModeManager.AbilitiesDisabled && PerksManager.Instance != null &&
                PerksManager.Instance.GetAbilityPerkNames().Any(key => PerksManager.Instance.GetPerkLevel(key) > 0);
            bool ultimate = abilities && PerksManager.Instance.GetAbilityUltimatePerkNames().Any(key => PerksManager.Instance.GetPerkLevel(key) > 0);
            var eligible = ChaosRules.Eligible(SurvivalMode.instance.Lives, abilities, ultimate);
            Offered = eligible[UnityEngine.Random.Range(0, eligible.Count)];
            Pending = true;
            Outcome = "";
            Votes.Clear();
            AcceptVotes = SkipVotes = 0;
            ChaosWallet.Record($"Challenge offered: {ChaosRules.Name(Offered)}");
            ChaosNet.MarkDirty();
            Tick();
            // Without the panel nobody could answer, so the run goes on without a challenge
            if (Pending && !ChallengePanel.IsShown)
            {
                Logger.LogError("[Chaos] The challenge panel could not be shown; skipping the challenge");
                Decide(Net.ServerId, PerkShop.VisitId, false);
            }
        }

        public static void Decide(ulong sender, int visit, bool accept)
        {
            if (!Net.IsAuthority || !Pending || visit != PerkShop.VisitId) return;
            if (sender != Net.ServerId)
            {
                Votes[sender] = accept;
                UpdateVotes();
                return;
            }
            if (accept)
            {
                SetActive(Offered);
                WagerFailed = false;
                if (Active == ChallengeKind.LastHeart)
                {
                    HeldLives = Math.Max(0, SurvivalMode.instance.Lives - 1);
                    Lives(SurvivalMode.instance).Value = 1;
                }
            }
            Outcome = accept ? OutcomeAccepted : OutcomeSkipped;
            ChaosWallet.Record($"Challenge {Outcome.ToLowerInvariant()}: {ChaosRules.Name(Offered)}");
            Pending = false;
            Votes.Clear();
            AcceptVotes = SkipVotes = 0;
            ChaosNet.MarkDirty();
            Tick();
            // Clients rescale their running cooldowns from this state, so it goes out before play resumes
            ChaosNet.Flush();
        }

        public static void PeerLeft(ulong peer)
        {
            if (Votes.Remove(peer)) UpdateVotes();
        }

        private static void UpdateVotes()
        {
            AcceptVotes = Votes.Values.Count(value => value);
            SkipVotes = Votes.Count - AcceptVotes;
            ChaosNet.MarkDirty();
        }

        public static void Settle()
        {
            if (!Net.IsAuthority || Active == ChallengeKind.None) return;
            var completed = Active;
            int reward = WagerFailed ? 0 : ChaosRules.Reward(completed);
            int returned = HeldLives;
            HeldLives = 0;
            SetActive(ChallengeKind.None);
            if (returned > 0 && SurvivalMode.instance != null && SurvivalMode.instance.Lives > 0)
                Lives(SurvivalMode.instance).Value = Math.Min(SurvivalMode.instance.MaxLives, SurvivalMode.instance.Lives + returned);
            Outcome = reward > 0 ? OutcomeCompleted : OutcomeFailed;
            ChaosWallet.Record($"Challenge {Outcome.ToLowerInvariant()}: {ChaosRules.Name(completed)}; returned {returned} lives");
            if (reward > 0) ChaosWallet.GrantPoints(reward, ChaosRules.Name(completed));
            ChaosNet.Announce($"{ChaosRules.Name(completed)}: {(reward > 0 ? ChaosRules.Points(reward, signed: true) : "no reward")}");
            WagerFailed = false;
            ChaosWallet.Notify();
        }

        public static void LostLife()
        {
            if (!Net.IsAuthority) return;
            if (Active == ChallengeKind.LifeWager && !WagerFailed)
            {
                WagerFailed = true;
                int lost = ChaosWallet.Penalize("Life Wager failed");
                if (SurvivalMode.instance.Lives > 0) ChaosNet.Announce($"Life Wager lost: {(lost > 0 ? ChaosRules.Points(-lost, signed: true) + ", " : "")}no reward this map");
            }
            if (SurvivalMode.instance.Lives <= 0)
            {
                if (Active != ChallengeKind.None) ChaosWallet.Record($"Challenge ended with the run: {ChaosRules.Name(Active)}");
                Reset();
            }
        }

        // Called after the press passed the ability's own checks; feedback is only for presses made on this machine
        public static bool CanActivate(bool ultimate, bool feedback)
        {
            bool blocked = Pending || (ultimate ? Active == ChallengeKind.NoUltimate : Active == ChallengeKind.NoRegularAbilities);
            if (blocked && feedback && !Pending && Time.unscaledTime - _lastBlocked > 1f)
            {
                _lastBlocked = Time.unscaledTime;
                Toast.Show($"{(ultimate ? "Ultimates" : "Regular abilities")} disabled until the next map choice", Hud.Pink);
            }
            return !blocked;
        }

        // Accepting Double Cooldown also doubles cooldowns already running, like Null Field's after the map change. Its end needs no rescale:
        // it ends at a wave's completion, and the next wave's start and the new map's spiders reset every cooldown before anyone can cast again
        private static void SetActive(ChallengeKind kind)
        {
            bool doubling = kind == ChallengeKind.DoubleCooldown && Active != ChallengeKind.DoubleCooldown;
            Active = kind;
            if (doubling)
                foreach (var ability in UnityEngine.Object.FindObjectsOfType<BaseAbility>()) ability.RescaleChallengeCooldown(2f);
        }

        public static void Reset()
        {
            _armed = false;
            Pending = false;
            Offered = ChallengeKind.None;
            SetActive(ChallengeKind.None);
            HeldLives = 0;
            WagerFailed = false;
            Votes.Clear();
            AcceptVotes = SkipVotes = 0;
            Outcome = "";
            Tick();
            ChaosNet.MarkDirty();
        }

        internal static void ApplyRemote(ChallengeKind offered, ChallengeKind active, bool pending, bool failed, int held, int accepts, int skips, string outcome)
        {
            Offered = offered;
            SetActive(active);
            Pending = pending;
            WagerFailed = failed;
            HeldLives = held;
            AcceptVotes = accepts;
            SkipVotes = skips;
            Outcome = outcome;
            Tick();
        }

        // Every frame from SurgeGameModeManager.LateUpdate, after the state went out, and on every change
        public static void Tick()
        {
            if (Pending)
            {
                GameController.instance?.DisableInputs();
                if (Net.IsAuthority) TimeScaleController.instance?.Pause();
                // The pause menu stays usable, so a player can still leave while the host decides
                bool menu = HudController.instance != null && HudController.instance.paused;
                ShowPanel(!menu);
            }
            else if (_wasPending)
            {
                ChallengePanel.Hide();
                if (Net.IsAuthority) TimeScaleController.instance?.Reset();
                GameController.instance?.EnableInputs();
            }
            _wasPending = Pending;
        }

        private static void ShowPanel(bool visible)
        {
            if (_panelFailed) return;
            try
            {
                ChallengePanel.Show(visible);
            }
            catch (Exception ex)
            {
                _panelFailed = true;
                Logger.LogError($"[Chaos] could not build the challenge panel: {ex}");
            }
        }

        public static float AdjustBudget(float budget) => Active == ChallengeKind.Overrun ? budget * 1.5f : budget;
    }

    [HarmonyPatch(typeof(SurvivalMode), nameof(SurvivalMode.ChoosePerkAndLevel))]
    internal static class ChallengeMapChoicePatch
    {
        static void Prefix(Modifier modifier, GameLevel level, NetworkVariable<bool> ____waitingForPerk)
        {
            if (!Net.IsAuthority || !ChaosWallet.Enabled || AbilitySelection.IsChoosing || !____waitingForPerk.Value) return;
            PerkShop.ResolveMap(modifier, level);
            ChallengeController.Arm();
        }
    }

    [HarmonyPatch(typeof(SurvivalMode), "StartWave")]
    internal static class ChallengeWaveGatePatch
    {
        static bool Prefix() => ChallengeController.BeforeWave();
    }

    // The game only pauses while it takes input, which the offer turns off. The offer has no countdown, so every machine can still open the pause menu, for instance to leave
    [HarmonyPatch(typeof(GameController), nameof(GameController.CanBePaused))]
    internal static class ChallengePausePatch
    {
        static void Postfix(GameController __instance, ref bool __result)
        {
            if (!__result && ChallengeController.Pending && !__instance.tempPauseBan) __result = true;
        }
    }

    [HarmonyPatch(typeof(SurvivalMode), "TryGrantExtraLife")]
    internal static class ChallengeMapBoundaryPatch
    {
        static void Prefix(SurvivalMode __instance, int ____nextPerkChoiceWave)
        {
            if (ChaosWallet.Enabled && __instance.CurrentWave == ____nextPerkChoiceWave) ChallengeController.Settle();
        }
    }

    [HarmonyPatch(typeof(SurvivalMode), "FailWave")]
    internal static class ChallengeLostLifePatch
    {
        static void Prefix(SurvivalMode __instance, out int __state) => __state = __instance.Lives;
        static void Postfix(SurvivalMode __instance, int __state)
        {
            if (ChaosWallet.Enabled && __instance.Lives < __state) ChallengeController.LostLife();
        }
    }

    [HarmonyPatch]
    internal static class ChallengeSpawnBudgetPatch
    {
        static MethodBase TargetMethod() => AccessTools.EnumeratorMoveNext(AccessTools.Method(typeof(SurvivalMode), "SpawnEnemies"));
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            bool injected = false;
            foreach (var instruction in instructions)
            {
                if (!injected && instruction.opcode == OpCodes.Stfld && instruction.operand is FieldInfo field && field.Name.Contains("spawnBudget"))
                {
                    var call = new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(ChallengeController), nameof(ChallengeController.AdjustBudget)));
                    call.labels.AddRange(instruction.labels);
                    instruction.labels.Clear();
                    yield return call;
                    injected = true;
                }
                yield return instruction;
            }
            if (!injected) throw new InvalidOperationException("Could not locate survival enemy budget for Overrun");
        }
    }
}
