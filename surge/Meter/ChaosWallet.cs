using System;
using System.Collections.Generic;
using SpiderSurge.Common;
using SpiderSurge.Logging;

namespace SpiderSurge
{
    public static class ChaosWallet
    {
        public static int Points { get; private set; }
        public static int PointsEarned { get; private set; }
        public static int PointsSpent { get; private set; }
        public static int ChallengesCompleted { get; private set; }
        internal static void ChallengeCompleted() { ChallengesCompleted++; Notify(); }
        public static readonly List<ChaosEventRecord> Events = new List<ChaosEventRecord>();
        private static bool _synced;
        public static bool Enabled => SurgeGameModeManager.IsSurgeRunActive && SurvivalMode.instance != null && SurvivalMode.instance.GameModeActive();
        public static event Action Changed;
        public static event Action PointEarned;

        public static void ResetRun()
        {
            ChallengeController.Reset();
            PerkShop.ClearOffer();
            Points = PointsEarned = PointsSpent = ChallengesCompleted = 0;
            Events.Clear();
            _synced = false;
            Notify();
        }

        public static bool CanAfford(int cost) => cost >= 0 && Points >= cost;

        public static void GrantPoints(int amount, string source = "Grant")
        {
            if (!Net.IsAuthority || amount <= 0) return;
            Points += amount;
            PointsEarned += amount;
            Record($"{source}: {ChaosRules.Points(amount, signed: true)}; balance {Points}");
            Notify();
            Raise(PointEarned);
        }

        public static bool TrySpend(int cost)
        {
            if (!Net.IsAuthority || !CanAfford(cost)) return false;
            Points -= cost;
            PointsSpent += cost;
            Notify();
            return true;
        }

        // Returns the points taken: one, or none from an empty wallet
        public static int Penalize(string source)
        {
            if (!Net.IsAuthority) return 0;
            int lost = Math.Min(1, Points);
            Points -= lost;
            Record($"{source}: {ChaosRules.Points(-lost, signed: true)}; balance {Points}");
            Notify();
            return lost;
        }

        internal static void RefundPenalty(int amount, string source)
        {
            if (!Net.IsAuthority || amount <= 0) return;
            Points += amount;
            Record($"{source}: refunded {ChaosRules.Points(amount)}; balance {Points}");
            Notify();
        }

        public static void Record(string text)
        {
            Events.Add(new ChaosEventRecord { Wave = SurvivalMode.instance?.CurrentWave ?? 0, Text = text });
            Silk.Logger.LogInfo("[Chaos] " + text);
        }

        internal static void ApplyRemote(int points, int earned, int spent, int challenges)
        {
            bool gained = _synced && earned > PointsEarned;
            _synced = true;
            Points = Math.Max(0, points);
            PointsEarned = earned;
            PointsSpent = spent;
            ChallengesCompleted = Math.Max(0, challenges);
            Raise(Changed);
            if (gained) Raise(PointEarned);
        }

        public static void Notify()
        {
            Raise(Changed);
            ChaosNet.MarkDirty();
        }

        private static void Raise(Action handler) => Listeners.Raise(handler, "[Chaos] UI update failed");
    }
}
