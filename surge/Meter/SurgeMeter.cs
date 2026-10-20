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
        Banish = 7,
        FreezeTime = 8,
        Rewind = 9,
    }

    // The team's shared Surge meter. The host owns it: charge comes in from the abilities while a wave runs, every full meter
    // becomes a point, and points are spent in the perk shop. Clients get the numbers from MeterNet.
    public static class SurgeMeter
    {
        private static readonly AccessTools.FieldRef<SurvivalMode, bool> WaveInProgress = AccessTools.FieldRefAccess<SurvivalMode, bool>("_waveInProgress");
        private static readonly AccessTools.FieldRef<SurvivalMode, SurvivalConfig> Config = AccessTools.FieldRefAccess<SurvivalMode, SurvivalConfig>("_config");

        private static readonly Dictionary<(int playerId, MeterSource source), (int castId, int used)> _castUse = new Dictionary<(int, MeterSource), (int, int)>();
        private static readonly Dictionary<int, int> _chargeByPlayer = new Dictionary<int, int>();
        private static readonly Dictionary<MeterSource, int> _chargeBySource = new Dictionary<MeterSource, int>();

        public static int Charge { get; private set; }
        public static int Points { get; private set; }
        public static int ChargePerPoint { get; private set; } = 1;
        public static int TotalCharge { get; private set; }
        public static int PointsEarned { get; private set; }
        public static int PointsSpent { get; private set; }

        public static IReadOnlyDictionary<int, int> ChargeByPlayer => _chargeByPlayer;
        public static IReadOnlyDictionary<MeterSource, int> ChargeBySource => _chargeBySource;

        public static event Action Changed;
        public static event Action PointEarned;

        public static float Fill => ChargePerPoint > 0 ? Mathf.Clamp01(Charge / (float)ChargePerPoint) : 0f;

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
            _castUse.Clear();
            _chargeByPlayer.Clear();
            _chargeBySource.Clear();
            RecomputeThreshold();
            Raise(Changed);
            MeterNet.MarkDirty();
        }

        public static void Clear()
        {
            ResetRun();
        }

        // chargePerPoint = activeChargePerWave × players × wavesPerScreen × screensPerPoint, so a team of any size needs the
        // same effort per player to reach a point by the next perk screen
        public static void RecomputeThreshold()
        {
            if (!Net.IsAuthority) return;
            int players = Mathf.Max(1, LobbyController.instance != null ? LobbyController.instance.GetPlayerCount() : 1);
            int wavesPerLevel = 3;
            var mode = SurvivalMode.instance;
            if (mode != null)
            {
                SurvivalConfig config = Config(mode);
                if (config != null && config.wavesPerLevel > 0) wavesPerLevel = config.wavesPerLevel;
            }
            int wavesPerScreen = Mathf.Max(1, wavesPerLevel + players - 1);
            float perPoint = ModConfig.MeterActiveChargePerWave * players * wavesPerScreen * ModConfig.MeterScreensPerPoint;
            int threshold = Mathf.Max(Consts.Values.Meter.MinChargePerPoint, Mathf.RoundToInt(perPoint));
            if (threshold == ChargePerPoint) return;
            ChargePerPoint = threshold;
            ConvertCharge();
            Raise(Changed);
            MeterNet.MarkDirty();
        }

        // Caps are per cast: the same source on the same player's current cast can't pass its cap
        public static void Add(MeterSource source, int playerId, int castId, int amount, int capPerCast)
        {
            if (amount <= 0 || !CanEarn) return;

            var key = (playerId, source);
            if (!_castUse.TryGetValue(key, out var use) || use.castId != castId)
            {
                use = (castId, 0);
            }
            int room = capPerCast - use.used;
            if (room <= 0) return;
            amount = Mathf.Min(amount, room);
            _castUse[key] = (castId, use.used + amount);

            Charge += amount;
            TotalCharge += amount;
            _chargeByPlayer.TryGetValue(playerId, out int byPlayer);
            _chargeByPlayer[playerId] = byPlayer + amount;
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
            Logger.LogInfo($"[Surge meter] {amount} point(s) granted, the team has {Points}");
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

        internal static void ApplyRemote(int charge, int points, int chargePerPoint, int totalCharge)
        {
            bool earned = points > Points || (points == Points && totalCharge > TotalCharge && charge < Charge);
            Charge = charge;
            Points = points;
            ChargePerPoint = Mathf.Max(1, chargePerPoint);
            TotalCharge = totalCharge;
            Raise(Changed);
            if (earned) Raise(PointEarned);
        }

        private static void ConvertCharge()
        {
            bool earned = false;
            while (Charge >= ChargePerPoint)
            {
                Charge -= ChargePerPoint;
                Points++;
                PointsEarned++;
                earned = true;
            }
            if (earned)
            {
                Logger.LogInfo($"[Surge meter] Point earned, the team has {Points} (threshold {ChargePerPoint})");
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
}
