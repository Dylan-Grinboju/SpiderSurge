using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine.InputSystem;
using Logger = Silk.Logger;

namespace SpiderSurge.Integration;

/// <summary>
/// Reflection-based bridge to the Stats Mod API. Gracefully no-ops if the stats mod isn't loaded.
/// </summary>
public static class StatsModBridge
{
    private static bool _initialized = false;
    private static bool _statsModAvailable = false;
    private static MethodInfo _registerCustomStats;
    private static MethodInfo _registerCustomTitle;

    private static void Initialize()
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
                    if (apiType is not null) break;
                }
                catch
                {
                }
            }

            if (apiType is null)
            {
                Logger.LogInfo("StatsModBridge: Stats Mod not detected, integration disabled.");
                return;
            }

            _registerCustomStats = apiType.GetMethod("RegisterCustomStats", BindingFlags.Public | BindingFlags.Static);
            _registerCustomTitle = apiType.GetMethod("RegisterCustomTitle", BindingFlags.Public | BindingFlags.Static);

            _statsModAvailable = _registerCustomStats is not null && _registerCustomTitle is not null;

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
        if (!_statsModAvailable || snapshot is null) return;

        try
        {
            var lines = new List<string>
            {
                "  SpiderSurge Mod Data:"
            };

            foreach (var ps in snapshot.PlayerStats)
            {
                var playerInput = PlayerInput.all.FirstOrDefault(p => p.playerIndex == ps.PlayerIndex);
                string playerName = playerInput is not null ? $"Player {ps.PlayerIndex + 1}" : $"Player {ps.PlayerIndex + 1}";
                lines.Add($"  {playerName}:");
                lines.Add($"    Ability Activations: {ps.AbilityActivationCount}");
                lines.Add($"    Ultimate Activations: {ps.UltimateActivationCount}");
            }

            _registerCustomStats.Invoke(null, [lines]);
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
        if (PerksManager.Instance is null) return null;
        if (PerksManager.Instance.GetPerkLevel(Consts.PerkNames.ImmuneAbility) > 0) return "Immune";
        if (PerksManager.Instance.GetPerkLevel(Consts.PerkNames.PulseAbility) > 0) return "Pulse";
        return PerksManager.Instance.GetPerkLevel(Consts.PerkNames.AmmoAbility) > 0
            ? "Ammo"
            : PerksManager.Instance.GetPerkLevel(Consts.PerkNames.StorageAbility) > 0 ? "Storage" : null;
    }

    private static string GetActiveUltType()
    {
        if (PerksManager.Instance is null) return null;
        if (PerksManager.Instance.GetPerkLevel(Consts.PerkNames.ImmuneAbilityUltimate) > 0) return "Immune";
        if (PerksManager.Instance.GetPerkLevel(Consts.PerkNames.PulseAbilityUltimate) > 0) return "Pulse";
        return PerksManager.Instance.GetPerkLevel(Consts.PerkNames.AmmoAbilityUltimate) > 0
            ? "Ammo"
            : PerksManager.Instance.GetPerkLevel(Consts.PerkNames.StorageAbilityUltimate) > 0 ? "Storage" : null;
    }

    private class LeaderInfo
    {
        public PlayerInput Player;
        public int Value;
        public bool HasStat;
        public string Description;
    }

    public static void SendSurgeTitles(Logging.SpiderSurgeStatsSnapshot snapshot)
    {
        Initialize();
        if (!_statsModAvailable || snapshot is null) return;
        if (snapshot.PlayerStats.Count < 2) return;

        try
        {
            var mostAbility = GetLeader(snapshot, ps => ps.AbilityActivationCount, descending: true);
            var leastAbility = GetLeader(snapshot, ps => ps.AbilityActivationCount, descending: false);
            var mostUlt = GetLeader(snapshot, ps => ps.UltimateActivationCount, descending: true);
            var leastUlt = GetLeader(snapshot, ps => ps.UltimateActivationCount, descending: false);

            TryRegisterTitle("Enhanced", (mostAbility, ReqMostAbility));
            TryRegisterTitle("Ultimate Form", (mostUlt, ReqMostUlt));

            TryRegisterTitle("Powered Up", (mostUlt, ReqMostUlt), (mostAbility, ReqMostAbility));
            TryRegisterTitle("Maximum Power", (mostUlt, ReqMostUlt), (leastAbility, ReqLeastAbility));
            TryRegisterTitle("Simple is Better", (mostAbility, ReqMostAbility), (leastUlt, ReqLeastUlt));
            TryRegisterTitle("Vanilla", (leastUlt, ReqLeastUlt), (leastAbility, ReqLeastAbility));

            string abilityType = GetActiveAbilityType();
            string ultType = GetActiveUltType();

            TryRegisterTitle("Ability Destroyer", (mostAbility, ReqMostAbility), (null, "MostOffense"));

            if (abilityType == "Immune")
                TryRegisterTitle("Cautious", (mostAbility, ReqMostImmuneAbility), (null, "MostDamageTaken"));
            if (abilityType == "Pulse")
                TryRegisterTitle("Down with the Ship", (mostAbility, ReqMostPulseAbility), (null, "MostLavaDeaths"));
            if (ultType == "Pulse")
                TryRegisterTitle("Boom Boom", (mostUlt, ReqMostPulseUlt), (null, "MostExplosionsKills"));
            if (ultType == "Immune")
                TryRegisterTitle("Self Sacrifice", (mostUlt, ReqMostImmuneUlt), (null, "MostDamageTaken"));
        }
        catch (Exception ex)
        {
            Logger.LogError($"StatsModBridge: Error sending surge titles: {ex.Message}");
        }
    }

    private static LeaderInfo GetLeader(Logging.SpiderSurgeStatsSnapshot snapshot,
        Func<Logging.PlayerStats, int> selector, bool descending)
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
            Description = $"{label} ({topValue})"
        };
    }

    private static void TryRegisterTitle(string titleName, params (LeaderInfo Leader, string ReqName)[] requirements)
    {
        if (requirements.Length == 0) return;

        List<(LeaderInfo Leader, string ReqName)> leaderReqs = [];
        List<string> externalReqs = [];

        foreach (var (leader, reqName) in requirements)
        {
            if (leader is null)
                externalReqs.Add(reqName);
            else
                leaderReqs.Add((leader, reqName));
        }

        PlayerInput primaryPlayer = null;
        bool allHaveStats = true;
        List<string> allReqNames = [];
        List<string> descriptions = [];

        if (leaderReqs.Count > 0)
        {
            primaryPlayer = leaderReqs[0].Leader.Player;
            if (primaryPlayer is null) return;

            foreach (var (leader, _) in leaderReqs)
            {
                if (leader.Player is null || !leader.HasStat) { allHaveStats = false; break; }
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

        _registerCustomTitle.Invoke(null,
        [
            titleName,
            descriptions.Count > 0 ? string.Join("\n", descriptions) : "",
            allReqNames.ToArray(),
            primaryPlayer,
            true,
            0
        ]);
    }
}
