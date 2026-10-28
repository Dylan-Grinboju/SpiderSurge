using System.IO;
using System.Linq;

namespace SpiderSurge.Common
{
    public sealed class RunRecapData
    {
        public int Wave;
        public string Build = "";
        public int Challenges;
        public int Earned;
        public int Spent;
        public string Achievements = "";
        public string Summary => $"Wave {Wave} reached  ·  {Challenges} challenge{(Challenges == 1 ? "" : "s")} completed  ·  Chaos: +{Earned} earned / {Spent} spent";
    }

    internal static class RunRecap
    {
        private static string[] before = System.Array.Empty<string>();
        public static RunRecapData Last { get; private set; }

        public static void Begin()
        {
            before = Parts.Surge.Running ? SurgeAchievements.All.Where(a => SurgeAchievements.IsUnlocked(a.Id)).Select(a => a.Id).ToArray() : System.Array.Empty<string>();
        }

        public static void Capture()
        {
            Last = null;
            if (!SurgeGameModeManager.IsSurgeRunActive || StatsMod.GameSessionManager.Instance.CurrentGameMode != StatsMod.GameMode.Survival) return;
            var perks = PerksManager.Instance;
            var keys = perks == null ? System.Array.Empty<string>() : perks.GetAllPerkNames().Where(k => perks.GetPerkLevel(k) > 0).ToArray();
            var core = keys.Where(k => perks.IsAbilityPerk(k) || perks.GetAbilityUltimatePerkNames().Contains(k));
            var upgrades = keys.Where(k => perks.IsUpgradePerk(k)).OrderByDescending(k => perks.GetPerkLevel(k)).Take(2);
            Last = new RunRecapData
            {
                Wave = SurvivalMode.instance?.CurrentWave ?? 0,
                Build = SurgeGameModeManager.AbilitiesDisabled ? "No abilities" : string.Join(" + ", core.Concat(upgrades).Select(k => perks.GetDisplayName(k))),
                Challenges = ChaosWallet.ChallengesCompleted,
                Earned = ChaosWallet.PointsEarned,
                Spent = ChaosWallet.PointsSpent,
                Achievements = string.Join(", ", SurgeAchievements.All.Where(a => !before.Contains(a.Id) && SurgeAchievements.IsUnlocked(a.Id)).Select(a => a.Name))
            };
            if (Last.Build.Length == 0) Last.Build = "No ability chosen";
        }

        public static void Clear() => Last = null;

        public static void Write(BinaryWriter writer)
        {
            writer.Write(Last != null);
            if (Last == null) return;
            writer.Write(Last.Wave);
            writer.WriteText(Last.Build);
            writer.Write(Last.Challenges);
            writer.Write(Last.Earned);
            writer.Write(Last.Spent);
            writer.WriteText(Last.Achievements);
        }

        public static void Read(BinaryReader reader)
        {
            Last = reader.ReadBoolean() ? new RunRecapData
            {
                Wave = reader.ReadInt32(), Build = reader.ReadString(), Challenges = reader.ReadInt32(),
                Earned = reader.ReadInt32(), Spent = reader.ReadInt32(), Achievements = reader.ReadString()
            } : null;
        }
    }
}
