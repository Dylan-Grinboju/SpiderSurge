using System;
using System.Collections.Generic;
using System.Linq;
using SpiderSurge.Common;
using StatsMod;
using Logger = Silk.Logger;

namespace SpiderSurge.Integration
{
    public static class StatsModBridge
    {
        private static bool StatsRunning => Parts.Stats.Running;

        public static void SendSurgeStats(Logging.SpiderSurgeStatsSnapshot snapshot)
        {
            if (!StatsRunning || snapshot == null) return;

            try
            {
                var lines = new List<string>
                {
                    "  SpiderSurge Mod Data:"
                };

                foreach (var ps in snapshot.PlayerStats)
                {
                    string playerName = $"Player {ps.PlayerId + 1}";
                    lines.Add($"  {playerName}:");
                    lines.Add($"    Ability Activations: {ps.AbilityActivationCount}");
                    lines.Add($"    Ultimate Activations: {ps.UltimateActivationCount}");
                }

                StatsModApi.RegisterCustomStats(lines);
            }
            catch (Exception ex)
            {
                Logger.LogError($"StatsModBridge: Error sending surge stats: {ex.Message}");
            }
        }

        private const string ReqMostAbility = "SurgeMostAbility";
        private const string ReqLeastAbility = "SurgeLeastAbility";
        private const string ReqMostUlt = "SurgeMostUlt";
        private const string ReqLeastUlt = "SurgeLeastUlt";
        private const string ReqMostImmuneAbility = "SurgeMostImmuneAbility";
        private const string ReqMostPulseAbility = "SurgeMostPulseAbility";
        private const string ReqMostImmuneUlt = "SurgeMostImmuneUlt";
        private const string ReqMostPulseUlt = "SurgeMostPulseUlt";

        private static readonly (string Type, string Ability, string Ultimate)[] AbilityPerks =
        {
            ("Immune", Consts.PerkNames.ImmuneAbility, Consts.PerkNames.ImmuneAbilityUltimate),
            ("Pulse", Consts.PerkNames.PulseAbility, Consts.PerkNames.PulseAbilityUltimate),
            ("Ammo", Consts.PerkNames.AmmoAbility, Consts.PerkNames.AmmoAbilityUltimate),
            ("Storage", Consts.PerkNames.StorageAbility, Consts.PerkNames.StorageAbilityUltimate),
            ("Freeze", Consts.PerkNames.FreezeAbility, Consts.PerkNames.FreezeAbilityUltimate),
        };

        private static string GetActiveType(bool ultimate)
        {
            if (PerksManager.Instance == null) return null;
            foreach (var (type, ability, ultimatePerk) in AbilityPerks)
            {
                if (PerksManager.Instance.GetPerkLevel(ultimate ? ultimatePerk : ability) > 0) return type;
            }
            return null;
        }

        private class LeaderInfo
        {
            public PlayerController Player;
            public bool HasStat;
            public string Description;
        }

        private const string MostAbilityText = "Most Ability Activations";
        private const string LeastAbilityText = "Least Ability Activations";
        private const string MostUltText = "Most Ultimate Activations";
        private const string LeastUltText = "Least Ultimate Activations";

        // Leaders are named by the text the collection shows; a title with a Type needs that ability, or its ultimate with Ultimate
        private static readonly (string Name, string Type, bool Ultimate, int Bonus, (string Leader, string Req)[] Leaders, string StatsReq)[] SurgeTitles =
        {
            ("Enhanced", null, false, 20, new[] { (MostAbilityText, ReqMostAbility) }, null),
            ("Ultimate Form", null, false, 20, new[] { (MostUltText, ReqMostUlt) }, null),
            ("Powered Up", null, false, 20, new[] { (MostUltText, ReqMostUlt), (MostAbilityText, ReqMostAbility) }, null),
            ("Maximum Power", null, false, 20, new[] { (MostUltText, ReqMostUlt), (LeastAbilityText, ReqLeastAbility) }, null),
            ("Simple is Better", null, false, 20, new[] { (MostAbilityText, ReqMostAbility), (LeastUltText, ReqLeastUlt) }, null),
            ("Vanilla", null, false, 20, new[] { (LeastUltText, ReqLeastUlt), (LeastAbilityText, ReqLeastAbility) }, null),
            ("Ability Destroyer", null, false, 20, new[] { (MostAbilityText, ReqMostAbility) }, Req.MostOffense),
            ("Wasn't Fast Enough", "Immune", false, 20, new[] { (MostAbilityText, ReqMostImmuneAbility) }, Req.MostDamageTaken),
            ("Down with the Ship", "Pulse", false, 10, new[] { (MostAbilityText, ReqMostPulseAbility) }, Req.MostLavaDeaths),
            ("Boom Boom", "Pulse", true, 20, new[] { (MostUltText, ReqMostPulseUlt) }, Req.MostExplosionsKills),
            ("Self Sacrifice", "Immune", true, 20, new[] { (MostUltText, ReqMostImmuneUlt) }, Req.MostDamageTaken),
        };

        // Lists Surge's titles in the Stats title collection, so they show before anyone earns them
        public static void DescribeSurgeTitles()
        {
            foreach (var title in SurgeTitles)
            {
                string where = title.Type == null ? "Surge mode" : $"Surge mode with the {title.Type} {(title.Ultimate ? "ultimate" : "ability")}";
                var requirements = title.Leaders.Select(l => l.Leader).ToList();
                if (title.StatsReq != null) requirements.Add(TitleCatalog.Label(title.StatsReq));
                StatsModApi.DescribeTitle(title.Name, $"{where}: {string.Join(" + ", requirements)}");
            }
        }

        public static void SendSurgeTitles(Logging.SpiderSurgeStatsSnapshot snapshot)
        {
            if (!StatsRunning || snapshot == null) return;
            if (snapshot.PlayerStats.Count < 2) return;

            try
            {
                var leaders = new Dictionary<string, LeaderInfo>
                {
                    [MostAbilityText] = GetLeader(snapshot, ps => ps.AbilityActivationCount, descending: true, "Ability Activations"),
                    [LeastAbilityText] = GetLeader(snapshot, ps => ps.AbilityActivationCount, descending: false, "Ability Activations"),
                    [MostUltText] = GetLeader(snapshot, ps => ps.UltimateActivationCount, descending: true, "Ultimate Activations"),
                    [LeastUltText] = GetLeader(snapshot, ps => ps.UltimateActivationCount, descending: false, "Ultimate Activations"),
                };
                string abilityType = GetActiveType(ultimate: false);
                string ultType = GetActiveType(ultimate: true);

                foreach (var title in SurgeTitles)
                {
                    if (title.Type != null && title.Type != (title.Ultimate ? ultType : abilityType)) continue;
                    TryRegisterTitle(title.Name, title.Bonus, title.Leaders.Select(l => (leaders[l.Leader], l.Req)).ToArray(), title.StatsReq);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"StatsModBridge: Error sending surge titles: {ex.Message}");
            }
        }

        private static LeaderInfo GetLeader(Logging.SpiderSurgeStatsSnapshot snapshot,
            Func<Logging.PlayerStats, int> selector, bool descending, string statLabel)
        {
            var ranked = descending
                ? snapshot.PlayerStats.OrderByDescending(selector).ToList()
                : snapshot.PlayerStats.OrderBy(selector).ToList();

            int topValue = selector(ranked[0]);
            bool isTied = ranked.Count > 1 && selector(ranked[0]) == selector(ranked[1]);

            var player = isTied ? null : Players.ById(ranked[0].PlayerId);

            string label = descending ? "Most" : "Least";
            return new LeaderInfo
            {
                Player = player,
                HasStat = descending ? topValue > 0 : true,
                Description = $"{label} {statLabel} ({topValue})"
            };
        }

        // The title goes to a player only if they lead every Surge stat it needs; Stats checks its own requirement
        private static void TryRegisterTitle(string titleName, int bonusPriority, (LeaderInfo Leader, string Req)[] leaders, string statsReq)
        {
            PlayerController primaryPlayer = leaders[0].Leader.Player;
            if (primaryPlayer == null || leaders.Any(l => l.Leader.Player == null || !l.Leader.HasStat || l.Leader.Player != primaryPlayer)) return;

            var reqNames = leaders.Select(l => l.Req).ToList();
            if (statsReq != null) reqNames.Add(statsReq);

            try
            {
                bool success = StatsModApi.RegisterCustomTitle(
                    titleName,
                    string.Join("\n", leaders.Select(l => l.Leader.Description)),
                    reqNames.ToArray(),
                    primaryPlayer,
                    true,
                    bonusPriority);

                if (!success)
                {
                    Logger.LogWarning($"StatsModBridge: Failed to register title '{titleName}'.");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"StatsModBridge: Error invoking RegisterCustomTitle for '{titleName}': {ex.Message}");
            }
        }
    }
}
