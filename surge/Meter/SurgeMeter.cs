using System;
using System.Collections.Generic;
using HarmonyLib;
using SpiderSurge.Common;
using UnityEngine;
using Logger = Silk.Logger;

namespace SpiderSurge
{
    public enum MeterSource : byte
    {
        KineticPulse = 0,
        ThermalDetonation = 1,
        BottomlessClip = 2,
        CarePackage = 3,
        NullField = 4,
        NeuralBackup = 5,
        PocketDimension = 6,
        BlackHole = 7,
        FreezeTime = 8,
        Rewind = 9,
    }

    // The team's shared Surge meter, owned by the host: ability charge during a wave fills it, and every full meter is a shop point
    public static class SurgeMeter
    {
        private static readonly AccessTools.FieldRef<SurvivalMode, bool> WaveInProgress = AccessTools.FieldRefAccess<SurvivalMode, bool>("_waveInProgress");
        private static readonly AccessTools.FieldRef<SurvivalMode, SurvivalConfig> ActiveConfig = AccessTools.FieldRefAccess<SurvivalMode, SurvivalConfig>("_config");

        private static readonly Dictionary<MeterSource, int> _chargeBySource = new Dictionary<MeterSource, int>();

        public static int Charge { get; private set; }
        public static int Points { get; private set; }
        public static int ChargePerPoint { get; private set; } = 1;
        public static int TotalCharge { get; private set; }
        public static int PointsEarned { get; private set; }
        public static int PointsSpent { get; private set; }
        public static int PointsEarnedThisWave { get; private set; }
        private static int _wave;
        public static bool WaveCapped => PointsEarnedThisWave >= Consts.Values.Meter.PointsPerWave;

        public static IReadOnlyDictionary<MeterSource, int> ChargeBySource => _chargeBySource;

        private static bool _remoteSynced;

        public static event Action Changed;
        public static event Action PointEarned;

        public static float Fill => WaveCapped ? 1f : ChargePerPoint > 0 ? Mathf.Clamp01(Charge / (float)ChargePerPoint) : 0f;

        // A client only knows the threshold once the host's first state arrives
        public static bool Known => Net.IsAuthority || _remoteSynced;

        // True on every machine during a Surge Survival run with abilities, which is the only time the meter exists
        public static bool Enabled =>
            SurgeGameModeManager.IsSurgeRunActive
            && !SurgeGameModeManager.AbilitiesDisabled
            && SurvivalMode.instance != null
            && SurvivalMode.instance.GameModeActive();

        public static bool CanEarn => Net.IsAuthority && Enabled && WaveInProgress(SurvivalMode.instance);

        public static void ResetRun()
        {
            Charge = 0;
            Points = 0;
            TotalCharge = 0;
            PointsEarned = 0;
            PointsSpent = 0;
            PointsEarnedThisWave = 0;
            _wave = 0;
            _remoteSynced = false;
            _chargeBySource.Clear();
            RecomputeThreshold();
            Raise(Changed);
            MeterNet.MarkDirty();
        }

        // Retrying a failed wave keeps its cap; only reaching a later wave opens another point.
        public static void OnWaveStarted(int wave)
        {
            if (!Net.IsAuthority || wave <= _wave) return;
            _wave = wave;
            PointsEarnedThisWave = 0;
            RecomputeThreshold();
            Raise(Changed);
            MeterNet.MarkDirty();
        }

        // chargePerWave × wavesPerPoint for one player, times the extra enemies the wave brings for the team, so a point costs the same per enemy whatever the team size
        public static void RecomputeThreshold()
        {
            if (!Net.IsAuthority) return;
            float perPoint = ModConfig.MeterChargePerWave * ModConfig.MeterWavesPerPoint * TeamEnemyFactor();
            int threshold = Mathf.Max(Consts.Values.Meter.MinChargePerPoint, Mathf.RoundToInt(perPoint));
            if (threshold == ChargePerPoint) return;
            ChargePerPoint = threshold;
            ConvertCharge();
            Raise(Changed);
            MeterNet.MarkDirty();
        }

        // The game's enemy budget for this wave and team over the same wave's budget for one player
        private static float TeamEnemyFactor()
        {
            int players = Mathf.Max(1, LobbyController.instance != null ? LobbyController.instance.GetPlayerCount() : 1);
            SurvivalConfig config = SurvivalMode.instance != null ? ActiveConfig(SurvivalMode.instance) : null;
            if (players == 1 || config == null) return 1f;
            float waveBudget = config.startingBudget + Mathf.Max(1, SurvivalMode.instance.CurrentWave) * config.budgetPerWave;
            float solo = waveBudget + config.budgetPerPlayer;
            return solo > 0f ? (waveBudget + players * config.budgetPerPlayer) / solo : 1f;
        }

        public static void Add(MeterSource source, int playerId, int amount)
        {
            if (amount <= 0 || !CanEarn) return;
            OnWaveStarted(SurvivalMode.instance.CurrentWave);
            if (WaveCapped) return;
            amount = Mathf.Min(amount, Mathf.Max(0, ChargePerPoint - Charge));
            if (amount == 0) return;

            Charge += amount;
            TotalCharge += amount;
            _chargeBySource.TryGetValue(source, out int bySource);
            _chargeBySource[source] = bySource + amount;
            Logging.SpiderSurgeStatsManager.Instance?.LogMeterCharge(playerId, amount);

            ConvertCharge();
            Raise(Changed);
            MeterNet.MarkDirty();
        }

        public static bool CanAfford(int cost) => Points >= cost;

        // Points that come from the shop itself (the Blood Pact), not from charge
        public static void GrantPoints(int amount)
        {
            if (!Net.IsAuthority || amount <= 0) return;
            Points += amount;
            PointsEarned += amount;
            Logger.LogInfo($"[Surge meter] {amount} chaos point(s) granted, the team has {Points}");
            Raise(Changed);
            Raise(PointEarned);
            MeterNet.MarkDirty();
        }

        public static bool TrySpend(int cost)
        {
            if (!Net.IsAuthority || cost <= 0 || Points < cost) return false;
            Points -= cost;
            PointsSpent += cost;
            Raise(Changed);
            MeterNet.MarkDirty();
            return true;
        }

        // The first state after joining or a reset only catches up, so it doesn't play the point sound
        internal static void ApplyRemote(int charge, int points, int chargePerPoint, int totalCharge, int pointsEarnedThisWave)
        {
            bool earned = _remoteSynced && (points > Points || (points == Points && totalCharge > TotalCharge && charge < Charge));
            _remoteSynced = true;
            Charge = charge;
            Points = points;
            ChargePerPoint = Mathf.Max(1, chargePerPoint);
            TotalCharge = totalCharge;
            PointsEarnedThisWave = pointsEarnedThisWave;
            Raise(Changed);
            if (earned) Raise(PointEarned);
        }

        private static void ConvertCharge()
        {
            if (!WaveCapped && Charge >= ChargePerPoint)
            {
                Charge = 0;
                Points++;
                PointsEarned++;
                PointsEarnedThisWave++;
                Logger.LogInfo($"[Surge meter] Chaos point earned, the team has {Points} (threshold {ChargePerPoint})");
                Raise(PointEarned);
            }
        }

        private static void Raise(Action handler)
        {
            if (handler == null) return;
            foreach (Delegate listener in handler.GetInvocationList())
            {
                try
                {
                    ((Action)listener)();
                }
                catch (Exception ex)
                {
                    Logger.LogError($"[Surge meter] listener failed: {ex}");
                }
            }
        }
    }

    // Friends join a run in the waiting room, after its first threshold was set, so a join or a leave sets it again at once instead of at the next wave
    [HarmonyPatch(typeof(LobbyController), nameof(LobbyController.AddPlayer))]
    public class LobbyController_AddPlayer_MeterPatch
    {
        [HarmonyPostfix]
        public static void Postfix()
        {
            if (SurgeGameModeManager.Instance != null && SurgeGameModeManager.Instance.IsActive) SurgeMeter.RecomputeThreshold();
        }
    }

    [HarmonyPatch(typeof(LobbyController), nameof(LobbyController.RemovePlayer))]
    public class LobbyController_RemovePlayer_MeterPatch
    {
        [HarmonyPostfix]
        public static void Postfix()
        {
            if (SurgeGameModeManager.Instance != null && SurgeGameModeManager.Instance.IsActive) SurgeMeter.RecomputeThreshold();
        }
    }
}
