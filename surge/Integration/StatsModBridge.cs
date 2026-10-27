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

        private static string GetActiveAbilityType()
        {
            if (PerksManager.Instance == null) return null;
            if (PerksManager.Instance.GetPerkLevel(Consts.PerkNames.ImmuneAbility) > 0) return "Immune";
            if (PerksManager.Instance.GetPerkLevel(Consts.PerkNames.PulseAbility) > 0) return "Pulse";
            if (PerksManager.Instance.GetPerkLevel(Consts.PerkNames.AmmoAbility) > 0) return "Ammo";
            if (PerksManager.Instance.GetPerkLevel(Consts.PerkNames.StorageAbility) > 0) return "Storage";
            if (PerksManager.Instance.GetPerkLevel(Consts.PerkNames.FreezeAbility) > 0) return "Freeze";
            return null;
        }

        private static string GetActiveUltType()
        {
            if (PerksManager.Instance == null) return null;
            if (PerksManager.Instance.GetPerkLevel(Consts.PerkNames.ImmuneAbilityUltimate) > 0) return "Immune";
            if (PerksManager.Instance.GetPerkLevel(Consts.PerkNames.PulseAbilityUltimate) > 0) return "Pulse";
            if (PerksManager.Instance.GetPerkLevel(Consts.PerkNames.AmmoAbilityUltimate) > 0) return "Ammo";
            if (PerksManager.Instance.GetPerkLevel(Consts.PerkNames.StorageAbilityUltimate) > 0) return "Storage";
            if (PerksManager.Instance.GetPerkLevel(Consts.PerkNames.FreezeAbilityUltimate) > 0) return "Freeze";
            return null;
        }

        private class LeaderInfo
        {
            public PlayerController Player;
            public int Value;
            public bool HasStat;
            public string Description;
        }

        private class TitleRequirement
        {
            public LeaderInfo Leader;
            public string ReqName;
        }

        private const string MostAbilityText = "Most Ability Activations";
        private const string LeastAbilityText = "Least Ability Activations";
        private const string MostUltText = "Most Ultimate Activations";
        private const string LeastUltText = "Least Ultimate Activations";

        private static readonly (string name, string mode, string[] requirements)[] SurgeTitles =
        {
            ("Enhanced", null, new[] { MostAbilityText }),
            ("Ultimate Form", null, new[] { MostUltText }),
            ("Powered Up", null, new[] { MostUltText, MostAbilityText }),
            ("Maximum Power", null, new[] { MostUltText, LeastAbilityText }),
            ("Simple is Better", null, new[] { MostAbilityText, LeastUltText }),
            ("Vanilla", null, new[] { LeastUltText, LeastAbilityText }),
            ("Ability Destroyer", null, new[] { MostAbilityText, TitleCatalog.Label(Req.MostOffense) }),
            ("Wasn't Fast Enough", "Immune ability", new[] { MostAbilityText, TitleCatalog.Label(Req.MostDamageTaken) }),
            ("Down with the Ship", "Pulse ability", new[] { MostAbilityText, TitleCatalog.Label(Req.MostLavaDeaths) }),
            ("Boom Boom", "Pulse ultimate", new[] { MostUltText, TitleCatalog.Label(Req.MostExplosionsKills) }),
            ("Self Sacrifice", "Immune ultimate", new[] { MostUltText, TitleCatalog.Label(Req.MostDamageTaken) }),
        };

        // Lists Surge's titles in the Stats title collection, so they show before anyone earns them
        public static void DescribeSurgeTitles()
        {
            foreach (var (name, mode, requirements) in SurgeTitles)
            {
                string where = mode == null ? "Surge mode" : $"Surge mode with the {mode}";
                StatsModApi.DescribeTitle(name, $"{where}: {string.Join(" + ", requirements)}");
            }
        }

        public static void SendSurgeTitles(Logging.SpiderSurgeStatsSnapshot snapshot)
        {
            if (!StatsRunning || snapshot == null) return;
            if (snapshot.PlayerStats.Count < 2) return;

            try
            {
                var mostAbility = GetLeader(snapshot, ps => ps.AbilityActivationCount, descending: true, "Ability Activations");
                var leastAbility = GetLeader(snapshot, ps => ps.AbilityActivationCount, descending: false, "Ability Activations");
                var mostUlt = GetLeader(snapshot, ps => ps.UltimateActivationCount, descending: true, "Ultimate Activations");
                var leastUlt = GetLeader(snapshot, ps => ps.UltimateActivationCount, descending: false, "Ultimate Activations");

                TryRegisterTitle("Enhanced", LeaderReq(mostAbility, ReqMostAbility));
                TryRegisterTitle("Ultimate Form", LeaderReq(mostUlt, ReqMostUlt));
                TryRegisterTitle("Powered Up", LeaderReq(mostUlt, ReqMostUlt), LeaderReq(mostAbility, ReqMostAbility));
                TryRegisterTitle("Maximum Power", LeaderReq(mostUlt, ReqMostUlt), LeaderReq(leastAbility, ReqLeastAbility));
                TryRegisterTitle("Simple is Better", LeaderReq(mostAbility, ReqMostAbility), LeaderReq(leastUlt, ReqLeastUlt));
                TryRegisterTitle("Vanilla", LeaderReq(leastUlt, ReqLeastUlt), LeaderReq(leastAbility, ReqLeastAbility));
                TryRegisterTitle("Ability Destroyer", LeaderReq(mostAbility, ReqMostAbility), ExternalReq("MostOffense"));

                string abilityType = GetActiveAbilityType();
                string ultType = GetActiveUltType();


                if (abilityType == "Immune")
                    TryRegisterTitle("Wasn't Fast Enough", LeaderReq(mostAbility, ReqMostImmuneAbility), ExternalReq("MostDamageTaken"));
                if (abilityType == "Pulse")
                    TryRegisterTitle("Down with the Ship", 10, LeaderReq(mostAbility, ReqMostPulseAbility), ExternalReq("MostLavaDeaths"));
                if (ultType == "Pulse")
                    TryRegisterTitle("Boom Boom", LeaderReq(mostUlt, ReqMostPulseUlt), ExternalReq("MostExplosionsKills"));
                if (ultType == "Immune")
                    TryRegisterTitle("Self Sacrifice", 20, LeaderReq(mostUlt, ReqMostImmuneUlt), ExternalReq("MostDamageTaken"));
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
                Value = topValue,
                HasStat = descending ? topValue > 0 : true,
                Description = $"{label} {statLabel} ({topValue})"
            };
        }

        private static TitleRequirement LeaderReq(LeaderInfo leader, string reqName)
        {
            return new TitleRequirement
            {
                Leader = leader,
                ReqName = reqName
            };
        }

        private static TitleRequirement ExternalReq(string reqName)
        {
            return new TitleRequirement
            {
                Leader = null,
                ReqName = reqName
            };
        }

        private static void TryRegisterTitle(string titleName, params TitleRequirement[] requirements)
        {
            TryRegisterTitle(titleName, 20, requirements);
        }

        private static void TryRegisterTitle(string titleName, int bonusPriority, params TitleRequirement[] requirements)
        {
            if (requirements.Length == 0) return;

            var leaderReqs = new List<(LeaderInfo Leader, string ReqName)>();
            var externalReqs = new List<string>();

            foreach (var requirement in requirements)
            {
                if (requirement.Leader == null)
                    externalReqs.Add(requirement.ReqName);
                else
                    leaderReqs.Add((requirement.Leader, requirement.ReqName));
            }

            PlayerController primaryPlayer = null;
            bool allHaveStats = true;
            var allReqNames = new List<string>();
            var descriptions = new List<string>();

            if (leaderReqs.Count > 0)
            {
                primaryPlayer = leaderReqs[0].Leader.Player;
                if (primaryPlayer == null) return;

                foreach (var (leader, _) in leaderReqs)
                {
                    if (leader.Player == null || !leader.HasStat) { allHaveStats = false; break; }
                    if (leader.Player != primaryPlayer) { allHaveStats = false; break; }
                }
                if (!allHaveStats) return;

                foreach (var (leader, reqName) in leaderReqs)
                {
                    allReqNames.Add(reqName);
                    descriptions.Add(leader.Description);
                }
            }

            allReqNames.AddRange(externalReqs);

            try
            {
                bool success = StatsModApi.RegisterCustomTitle(
                    titleName,
                    descriptions.Count > 0 ? string.Join("\n", descriptions) : "",
                    allReqNames.ToArray(),
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
