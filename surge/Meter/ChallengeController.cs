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
        private static readonly AccessTools.FieldRef<SurvivalMode, NetworkVariable<int>> Lives = AccessTools.FieldRefAccess<SurvivalMode, NetworkVariable<int>>("_lives");
        public static ChallengeKind Offered { get; private set; }
        public static ChallengeKind Active { get; private set; }
        public static bool Pending { get; private set; }
        public static bool WagerFailed { get; private set; }
        private static int _heldLives;
        private static Action _loadChosenMap;
        private static bool _wasPending;
        private static bool _panelFailed;
        public static float CooldownMultiplier => Active == ChallengeKind.DoubleCooldown ? 2f : 1f;
        public static string Status => Active == ChallengeKind.None ? "" :
            $"{ChaosRules.Name(Active)} · {(WagerFailed ? "Reward lost" : $"{ChaosRules.Points(ChaosRules.Reward(Active), signed: true)} on completion")}";

        // Keep the selected perk, but wait for the challenge decision before loading its map.
        public static void LoadChosenMap(LevelController controller, GameLevel level, float delay, string nameOverride, string authorName)
        {
            if (!Net.IsAuthority || !ChaosWallet.Enabled || AbilitySelection.IsChoosing)
            {
                controller.LoadLevelWithTransition(level, delay, nameOverride, authorName);
                return;
            }
            _loadChosenMap = () => controller.LoadLevelWithTransition(level, delay, nameOverride, authorName);
            Begin();
            ChaosNet.Flush();
        }

        private static void Begin()
        {
            bool abilities = !SurgeGameModeManager.AbilitiesDisabled && PerksManager.Instance != null &&
                PerksManager.Instance.GetAbilityPerkNames().Any(key => PerksManager.Instance.GetPerkLevel(key) > 0);
            bool ultimate = abilities && PerksManager.Instance.GetAbilityUltimatePerkNames().Any(key => PerksManager.Instance.GetPerkLevel(key) > 0);
            var eligible = ChaosRules.Eligible(SurvivalMode.instance.Lives, abilities, ultimate);
            Offered = eligible[UnityEngine.Random.Range(0, eligible.Count)];
            Pending = true;
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

        // Only the host decides; clients wait on the panel
        public static void Decide(ulong sender, int visit, bool accept)
        {
            if (!Net.IsAuthority || sender != Net.ServerId || !Pending || visit != PerkShop.VisitId) return;
            if (accept)
            {
                SetActive(Offered);
                WagerFailed = false;
                if (Active == ChallengeKind.PoisonedHeart)
                {
                    _heldLives = Math.Max(0, SurvivalMode.instance.Lives - 1);
                    Lives(SurvivalMode.instance).Value = 1;
                }
            }
            ChaosWallet.Record($"Challenge {(accept ? "accepted" : "skipped")}: {ChaosRules.Name(Offered)}");
            Pending = false;
            ChaosNet.MarkDirty();
            Tick();
            // Clients rescale their running cooldowns from this state, so it goes out before the map loads
            ChaosNet.Flush();
            var load = _loadChosenMap;
            _loadChosenMap = null;
            load?.Invoke();
        }

        public static void Settle()
        {
            if (!Net.IsAuthority || Active == ChallengeKind.None) return;
            var completed = Active;
            int reward = WagerFailed ? 0 : ChaosRules.Reward(completed);
            int returned = _heldLives;
            _heldLives = 0;
            SetActive(ChallengeKind.None);
            if (returned > 0 && SurvivalMode.instance != null && SurvivalMode.instance.Lives > 0)
                Lives(SurvivalMode.instance).Value = Math.Min(SurvivalMode.instance.MaxLives, SurvivalMode.instance.Lives + returned);
            ChaosWallet.Record($"Challenge {(reward > 0 ? "completed" : "failed")}: {ChaosRules.Name(completed)}; returned {returned} lives");
            // A failed Life Wager was announced when the life was lost
            if (reward > 0)
            {
                ChaosWallet.GrantPoints(reward, ChaosRules.Name(completed));
            }
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

        // Called after the press passed the ability's own checks
        public static bool CanActivate(bool ultimate)
        {
            return !Pending && (ultimate ? Active != ChallengeKind.NoUltimate : Active != ChallengeKind.NoRegularAbilities);
        }

        // Accepting Double Cooldown also doubles cooldowns still running from the old map; the new map's spiders start with the doubled multiplier. Its end needs no rescale:
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
            _loadChosenMap = null;
            Pending = false;
            Offered = ChallengeKind.None;
            SetActive(ChallengeKind.None);
            _heldLives = 0;
            WagerFailed = false;
            Tick();
            ChaosNet.MarkDirty();
        }

        internal static void ApplyRemote(ChallengeKind offered, ChallengeKind active, bool pending, bool failed)
        {
            Offered = offered;
            SetActive(active);
            Pending = pending;
            WagerFailed = failed;
            Tick();
        }

        // Every frame from SurgeGameModeManager.LateUpdate, after the state went out, and on every change
        public static void Tick()
        {
            if (Pending)
            {
                if (!_wasPending) SurvivalModeHud.instance?.ClosePerkChoiseView();
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
        }

        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var load = AccessTools.Method(typeof(LevelController), nameof(LevelController.LoadLevelWithTransition));
            var offer = AccessTools.Method(typeof(ChallengeController), nameof(ChallengeController.LoadChosenMap));
            bool replaced = false;
            foreach (var instruction in instructions)
            {
                if (instruction.Calls(load))
                {
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = offer;
                    replaced = true;
                }
                yield return instruction;
            }
            if (!replaced) throw new InvalidOperationException("Could not locate the selected map load for the challenge offer");
        }
    }

    [HarmonyPatch(typeof(SurvivalMode), "StartWave")]
    internal static class ChallengeWaveGatePatch
    {
        static bool Prefix() => !ChallengeController.Pending;
    }

    // Choices disable gameplay input, but every machine can still open the menu to change settings or leave.
    [HarmonyPatch(typeof(GameController), nameof(GameController.CanBePaused))]
    internal static class ChallengePausePatch
    {
        static void Postfix(GameController __instance, ref bool __result)
        {
            if (!__result && (ChallengeController.Pending || PerkChoiceMenu.Choosing) && !__instance.tempPauseBan) __result = true;
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
