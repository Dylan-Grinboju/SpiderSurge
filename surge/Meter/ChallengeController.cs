using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using SpiderSurge.Common;
using Unity.Netcode;
using UnityEngine;

namespace SpiderSurge
{
    public static class ChallengeController
    {
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
        public static bool SelectingMap;
        private static Action _continue;
        private static float _lastBlocked;
        private static bool _wasPending;
        public static float CooldownMultiplier => Active == ChallengeKind.DoubleCooldown ? 2f : 1f;
        public static string Status => Active == ChallengeKind.None ? "" :
            $"{ChaosRules.Name(Active)} · {(WagerFailed ? "Reward lost" : $"+{ChaosRules.Reward(Active)} on completion")}";

        public static void Begin(Action resume)
        {
            if (!Net.IsAuthority || Pending) return;
            bool abilities = !SurgeGameModeManager.AbilitiesDisabled && PerksManager.Instance != null &&
                PerksManager.Instance.GetAbilityPerkNames().Any(key => PerksManager.Instance.GetPerkLevel(key) > 0);
            bool ultimate = abilities && PerksManager.Instance.GetAbilityUltimatePerkNames().Any(key => PerksManager.Instance.GetPerkLevel(key) > 0);
            var eligible = ChaosRules.Eligible(SurvivalMode.instance.Lives, abilities, ultimate);
            Offered = eligible[UnityEngine.Random.Range(0, eligible.Count)];
            _continue = resume;
            Pending = true;
            Outcome = "";
            Votes.Clear();
            AcceptVotes = SkipVotes = 0;
            ChaosWallet.Record($"Challenge offered: {ChaosRules.Name(Offered)}");
            ChaosNet.MarkDirty();
            Tick();
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
            Action resume = _continue;
            _continue = null;
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
            Outcome = accept ? "Accepted" : "Skipped";
            ChaosWallet.Record($"Challenge {Outcome.ToLowerInvariant()}: {ChaosRules.Name(Offered)}");
            Pending = false;
            Votes.Clear();
            AcceptVotes = SkipVotes = 0;
            ChaosNet.MarkDirty();
            Tick();
            ChaosNet.Flush();
            resume?.Invoke();
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
            Outcome = reward > 0 ? "Completed" : "Failed";
            ChaosWallet.Record($"Challenge {Outcome.ToLowerInvariant()}: {ChaosRules.Name(completed)}; returned {returned} lives");
            if (reward > 0) ChaosWallet.GrantPoints(reward, ChaosRules.Name(completed));
            Toast.Show($"{ChaosRules.Name(completed)}: {(reward > 0 ? $"+{reward} chaos points" : "no reward")}", Hud.Pink);
            WagerFailed = false;
            ChaosWallet.Notify();
        }

        public static void LostLife()
        {
            if (!Net.IsAuthority) return;
            if (Active == ChallengeKind.LifeWager && !WagerFailed)
            {
                WagerFailed = true;
                ChaosWallet.Penalize("Life Wager failed");
            }
            if (SurvivalMode.instance.Lives <= 0)
            {
                if (Active != ChallengeKind.None) ChaosWallet.Record($"Challenge ended with the run: {ChaosRules.Name(Active)}");
                Reset();
            }
        }

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

        private static void SetActive(ChallengeKind kind)
        {
            float old = CooldownMultiplier;
            Active = kind;
            float factor = CooldownMultiplier / old;
            if (!Mathf.Approximately(factor, 1f))
                foreach (var ability in UnityEngine.Object.FindObjectsOfType<BaseAbility>()) ability.RescaleChallengeCooldown(factor);
        }

        public static void Reset()
        {
            _continue = null;
            Pending = false;
            Offered = ChallengeKind.None;
            SetActive(ChallengeKind.None);
            HeldLives = 0;
            WagerFailed = false;
            SelectingMap = false;
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

        public static void Tick()
        {
            if (Pending)
            {
                GameController.instance?.DisableInputs();
                if (Net.IsAuthority) TimeScaleController.instance?.Pause();
                ChallengePanel.Show();
            }
            else if (_wasPending)
            {
                ChallengePanel.Hide();
                if (Net.IsAuthority) TimeScaleController.instance?.Reset();
            }
            _wasPending = Pending;
            MapBonusLabels.Refresh();
        }

        public static float AdjustBudget(float budget) => Active == ChallengeKind.Overrun ? budget * 1.5f : budget;
    }

    [HarmonyPatch(typeof(SurvivalMode), nameof(SurvivalMode.ChoosePerkAndLevel))]
    internal static class ChallengeMapChoicePatch
    {
        static void Prefix(Modifier modifier, GameLevel level, NetworkVariable<bool> ____waitingForPerk)
        {
            ChallengeController.SelectingMap = Net.IsAuthority && ChaosWallet.Enabled && !AbilitySelection.IsChoosing && ____waitingForPerk.Value;
            if (ChallengeController.SelectingMap) PerkShop.ResolveMap(modifier, level);
        }
        static Exception Finalizer(Exception __exception)
        {
            ChallengeController.SelectingMap = false;
            return __exception;
        }
    }

    [HarmonyPatch(typeof(LevelController), nameof(LevelController.LoadLevelWithTransition))]
    internal static class ChallengeTransitionPatch
    {
        static bool Prefix(LevelController __instance, GameLevel level, float delay, string nameOverride, string authorName)
        {
            if (!ChallengeController.SelectingMap) return true;
            ChallengeController.Begin(() => __instance.LoadLevelWithTransition(level, delay, nameOverride, authorName));
            return false;
        }
    }

    [HarmonyPatch(typeof(SurvivalMode), "StartWave")]
    internal static class ChallengeWaveGatePatch
    {
        static bool Prefix() => !ChallengeController.Pending;
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
