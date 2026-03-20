using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine.InputSystem;
using Logger = Silk.Logger;

namespace SpiderSurge.Integration
{
    /// <summary>
    /// Reflection-based bridge to the Stats Mod API. Gracefully no-ops if the stats mod isn't loaded.
    /// </summary>
    public static class StatsModBridge
    {
        private static bool _initialized = false;
        private static bool _statsModAvailable = false;
        private static MethodInfo _registerCustomStats;
        private static MethodInfo _registerCustomTitle;

        public static void Initialize()
        {
            if (_initialized) return;
            _initialized = true;

            try
            {
                Type apiType = null;
                foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    try
                    {
                        apiType = assembly.GetType("StatsMod.StatsModApi");
                        if (apiType != null) break;
                    }
                    catch
                    {
                    }
                }

                if (apiType == null)
                {
                    Logger.LogInfo("StatsModBridge: Stats Mod not detected, integration disabled.");
                    return;
                }

                _registerCustomStats = apiType.GetMethod("RegisterCustomStats", BindingFlags.Public | BindingFlags.Static);
                _registerCustomTitle = apiType.GetMethod("RegisterCustomTitle", BindingFlags.Public | BindingFlags.Static);

                _statsModAvailable = _registerCustomStats != null && _registerCustomTitle != null;

                if (_statsModAvailable)
                    Logger.LogInfo("StatsModBridge: Stats Mod API found, integration enabled.");
                else
                    Logger.LogWarning("StatsModBridge: Stats Mod found but API methods missing. Integration disabled.");
            }
            catch (Exception ex)
            {
                Logger.LogError($"StatsModBridge: Error during initialization: {ex.Message}");
                _statsModAvailable = false;
            }
        }

        public static void SendSurgeStats(Logging.SpiderSurgeStatsSnapshot snapshot)
        {
            Initialize();
            if (!_statsModAvailable || snapshot == null) return;

            try
            {
                var lines = new List<string>
                {
                    "  SpiderSurge Mod Data:"
                };

                foreach (var ps in snapshot.PlayerStats)
                {
                    string playerName = $"Player {ps.PlayerIndex + 1}";
                    lines.Add($"  {playerName}:");
                    lines.Add($"    Ability Activations: {ps.AbilityActivationCount}");
                    lines.Add($"    Ultimate Activations: {ps.UltimateActivationCount}");
                }

                _registerCustomStats.Invoke(null, new object[] { lines });
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
            return null;
        }

        private static string GetActiveUltType()
        {
            if (PerksManager.Instance == null) return null;
            if (PerksManager.Instance.GetPerkLevel(Consts.PerkNames.ImmuneAbilityUltimate) > 0) return "Immune";
            if (PerksManager.Instance.GetPerkLevel(Consts.PerkNames.PulseAbilityUltimate) > 0) return "Pulse";
            if (PerksManager.Instance.GetPerkLevel(Consts.PerkNames.AmmoAbilityUltimate) > 0) return "Ammo";
            if (PerksManager.Instance.GetPerkLevel(Consts.PerkNames.StorageAbilityUltimate) > 0) return "Storage";
            return null;
        }

        private class LeaderInfo
        {
            public PlayerInput Player;
            public int Value;
            public bool HasStat;
            public string Description;
        }

        private class TitleRequirement
        {
            public LeaderInfo Leader;
            public string ReqName;
        }

        public static void SendSurgeTitles(Logging.SpiderSurgeStatsSnapshot snapshot)
        {
            Initialize();
            if (!_statsModAvailable || snapshot == null) return;
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

            var player = isTied ? null : PlayerInput.all.FirstOrDefault(p => p.playerIndex == ranked[0].PlayerIndex);

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
            TryRegisterTitle(titleName, 10, requirements);
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

            PlayerInput primaryPlayer = null;
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
                bool success = (bool)_registerCustomTitle.Invoke(null, new object[]
                {
                    titleName,
                    descriptions.Count > 0 ? string.Join("\n", descriptions) : "",
                    allReqNames.ToArray(),
                    primaryPlayer,
                    true,
                    bonusPriority
                });

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
