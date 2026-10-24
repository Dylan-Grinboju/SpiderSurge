using System;
using System.Collections.Generic;
using SpiderSurge.Common;

namespace SpiderSurge
{
    public static class ChaosWallet
    {
        public static int Points { get; private set; }
        public static int PointsEarned { get; private set; }
        public static int PointsSpent { get; private set; }
        public static readonly List<string> Events = new List<string>();
        private static bool _synced;
        public static bool Known => Net.IsAuthority || _synced;
        public static bool Enabled => SurgeGameModeManager.IsSurgeRunActive && SurvivalMode.instance != null && SurvivalMode.instance.GameModeActive();
        public static event Action Changed;
        public static event Action PointEarned;

        public static void ResetRun()
        {
            ChallengeController.Reset();
            PerkShop.ClearOffer();
            Points = PointsEarned = PointsSpent = 0;
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
            Record($"{source}: +{amount} points; balance {Points}");
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

        public static void Penalize(string source)
        {
            if (!Net.IsAuthority) return;
            int lost = Math.Min(1, Points);
            Points -= lost;
            Record($"{source}: -{lost} points; balance {Points}");
            Notify();
        }

        public static void Record(string text)
        {
            Events.Add($"Wave {SurvivalMode.instance?.CurrentWave ?? 0}: {text}");
            Silk.Logger.LogInfo("[Chaos] " + text);
        }

        internal static void ApplyRemote(int points, int earned, int spent)
        {
            bool gained = _synced && earned > PointsEarned;
            _synced = true;
            Points = Math.Max(0, points);
            PointsEarned = earned;
            PointsSpent = spent;
            Raise(Changed);
            if (gained) Raise(PointEarned);
        }

        public static void Notify()
        {
            Raise(Changed);
            ChaosNet.MarkDirty();
        }

        private static void Raise(Action handler)
        {
            if (handler == null) return;
            foreach (Delegate listener in handler.GetInvocationList())
            {
                try { ((Action)listener)(); }
                catch (Exception ex) { Silk.Logger.LogError($"[Chaos] UI update failed: {ex}"); }
            }
        }
    }
}
