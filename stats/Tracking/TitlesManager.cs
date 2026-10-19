using UnityEngine;
using Logger = Silk.Logger;
using System;
using System.Collections.Generic;
using System.Linq;

namespace StatsMod;

public class TitleEntry
{
    public TitleEntry() { }

    public TitleEntry(KeyValuePair<PlayerController, PlayerTracker.PlayerData> playerData)
    {
        if (playerData.Value != null)
        {
            PlayerName = playerData.Value.PlayerName;
            PrimaryColor = playerData.Value.PlayerColor;
            SecondaryColor = playerData.Value.SecondaryColor;
            Player = playerData.Key;
            PlayerId = Player != null ? Player.playerID.Value : -1;
        }
    }

    public string TitleName { get; set; }
    public string Description { get; set; }
    public string PlayerName { get; set; }
    public Color PrimaryColor { get; set; }
    public Color SecondaryColor { get; set; }
    public PlayerController Player { get; set; }
    public int PlayerId { get; set; } = -1;
    public int Priority { get; set; }
    public HashSet<string> Requirements { get; set; } = new HashSet<string>();
}

public class TitleBuilder
{
    private readonly StatLeaders _leaders;
    private KeyValuePair<PlayerController, PlayerTracker.PlayerData> _primaryLeader;
    private readonly HashSet<string> _requirements = new HashSet<string>();
    private string _titleName;
    private string _description;
    private int _priority;

    public TitleBuilder(StatLeaders leaders)
    {
        _leaders = leaders;
    }

    public TitleBuilder ForLeader(Func<StatLeaders, KeyValuePair<PlayerController, PlayerTracker.PlayerData>> leaderSelector, string requirementName)
    {
        _primaryLeader = leaderSelector(_leaders);
        _requirements.Add(requirementName);
        return this;
    }
    public TitleBuilder AndLeader(string requirementName)
    {
        _requirements.Add(requirementName);
        return this;
    }

    public TitleBuilder WithName(string name)
    {
        _titleName = name;
        return this;
    }

    public TitleBuilder WithDescription(string description)
    {
        _description = description;
        return this;
    }

    public TitleBuilder WithPriority(int priority)
    {
        _priority = priority;
        return this;
    }

    public TitleEntry Build()
    {
        return new TitleEntry(_primaryLeader)
        {
            TitleName = _titleName,
            Description = _description,
            Priority = _priority,
            Requirements = new HashSet<string>(_requirements)
        };
    }

    public static bool SamePlayer(params KeyValuePair<PlayerController, PlayerTracker.PlayerData>[] leaders)
    {
        if (leaders.Length == 0) return false;
        var firstPlayer = leaders[0].Key;
        return leaders.All(l => l.Key == firstPlayer);
    }
}

public static class Req
{
    public const string MostWebSwings = nameof(StatLeaders.MostWebSwings);
    public const string LeastWebSwings = nameof(StatLeaders.LeastWebSwings);
    public const string HighestPoint = nameof(StatLeaders.HighestPoint);
    public const string LowestPoint = nameof(StatLeaders.LowestPoint);
    public const string MostAirborneTime = nameof(StatLeaders.MostAirborneTime);
    public const string LeastAirborneTime = nameof(StatLeaders.LeastAirborneTime);
    public const string MostKillsWhileAirborne = nameof(StatLeaders.MostKillsWhileAirborne);
    public const string MostKillsWhileSolo = nameof(StatLeaders.MostKillsWhileSolo);
    public const string MostWaveClutches = nameof(StatLeaders.MostWaveClutches);
    public const string MaxKillStreak = nameof(StatLeaders.MaxKillStreak);
    public const string MaxKillStreakWhileSolo = nameof(StatLeaders.MaxKillStreakWhileSolo);
    public const string MostAliveTime = nameof(StatLeaders.MostAliveTime);
    public const string MostOffense = nameof(StatLeaders.MostOffense);
    public const string LeastOffense = nameof(StatLeaders.LeastOffense);
    public const string MostDamageTaken = nameof(StatLeaders.MostDamageTaken);
    public const string LeastDamageTaken = nameof(StatLeaders.LeastDamageTaken);
    public const string MostFriendlyFire = nameof(StatLeaders.MostFriendlyFire);
    public const string LeastFriendlyFire = nameof(StatLeaders.LeastFriendlyFire);
    public const string MostShieldsLost = nameof(StatLeaders.MostShieldsLost);
    public const string LeastShieldsLost = nameof(StatLeaders.LeastShieldsLost);
    public const string MostDeaths = nameof(StatLeaders.MostDeaths);
    public const string LeastDeaths = nameof(StatLeaders.LeastDeaths);
    public const string MostLavaDeaths = nameof(StatLeaders.MostLavaDeaths);
    public const string LeastLavaDeaths = nameof(StatLeaders.LeastLavaDeaths);
    public const string MostGunsKills = nameof(StatLeaders.MostGunsKills);
    public const string MostExplosionsKills = nameof(StatLeaders.MostExplosionsKills);
    public const string MostBladeKills = nameof(StatLeaders.MostBladeKills);
    public const string MostHornetKills = nameof(StatLeaders.MostHornetKills);
    public const string MostWhispKills = nameof(StatLeaders.MostWhispKills);
    public const string MostKhepriKills = nameof(StatLeaders.MostKhepriKills);
    public const string MostAstralReturns = nameof(StatLeaders.MostAstralReturns);
    public const string BestAccuracy = nameof(StatLeaders.BestAccuracy);
    public const string BestRailvolverAccuracy = nameof(StatLeaders.BestRailvolverAccuracy);
    public const string WorstAccuracy = nameof(StatLeaders.WorstAccuracy);
    public const string MostShotsFired = nameof(StatLeaders.MostShotsFired);
}

public class StatLeaders
{
    public KeyValuePair<PlayerController, PlayerTracker.PlayerData> MostWebSwings { get; set; }
    public KeyValuePair<PlayerController, PlayerTracker.PlayerData> LeastWebSwings { get; set; }
    public KeyValuePair<PlayerController, PlayerTracker.PlayerData> HighestPoint { get; set; }
    public KeyValuePair<PlayerController, PlayerTracker.PlayerData> LowestPoint { get; set; }
    public KeyValuePair<PlayerController, PlayerTracker.PlayerData> MostAirborneTime { get; set; }
    public KeyValuePair<PlayerController, PlayerTracker.PlayerData> LeastAirborneTime { get; set; }
    public KeyValuePair<PlayerController, PlayerTracker.PlayerData> MostKillsWhileAirborne { get; set; }
    public KeyValuePair<PlayerController, PlayerTracker.PlayerData> MostKillsWhileSolo { get; set; }
    public KeyValuePair<PlayerController, PlayerTracker.PlayerData> MostWaveClutches { get; set; }
    public KeyValuePair<PlayerController, PlayerTracker.PlayerData> MaxKillStreak { get; set; }
    public KeyValuePair<PlayerController, PlayerTracker.PlayerData> MaxKillStreakWhileSolo { get; set; }
    public KeyValuePair<PlayerController, PlayerTracker.PlayerData> MostAliveTime { get; set; }

    public KeyValuePair<PlayerController, PlayerTracker.PlayerData> MostOffense { get; set; }
    public KeyValuePair<PlayerController, PlayerTracker.PlayerData> LeastOffense { get; set; }
    public KeyValuePair<PlayerController, PlayerTracker.PlayerData> MostDamageTaken { get; set; }
    public KeyValuePair<PlayerController, PlayerTracker.PlayerData> LeastDamageTaken { get; set; }
    public KeyValuePair<PlayerController, PlayerTracker.PlayerData> MostFriendlyFire { get; set; }
    public KeyValuePair<PlayerController, PlayerTracker.PlayerData> LeastFriendlyFire { get; set; }
    public KeyValuePair<PlayerController, PlayerTracker.PlayerData> MostShieldsLost { get; set; }
    public KeyValuePair<PlayerController, PlayerTracker.PlayerData> LeastShieldsLost { get; set; }
    public KeyValuePair<PlayerController, PlayerTracker.PlayerData> MostDeaths { get; set; }
    public KeyValuePair<PlayerController, PlayerTracker.PlayerData> LeastDeaths { get; set; }
    public KeyValuePair<PlayerController, PlayerTracker.PlayerData> MostLavaDeaths { get; set; }
    public KeyValuePair<PlayerController, PlayerTracker.PlayerData> LeastLavaDeaths { get; set; }

    public KeyValuePair<PlayerController, PlayerTracker.PlayerData> MostGunsKills { get; set; }
    public KeyValuePair<PlayerController, PlayerTracker.PlayerData> MostExplosionsKills { get; set; }
    public KeyValuePair<PlayerController, PlayerTracker.PlayerData> MostBladeKills { get; set; }
    public KeyValuePair<PlayerController, PlayerTracker.PlayerData> MostHornetKills { get; set; }
    public KeyValuePair<PlayerController, PlayerTracker.PlayerData> MostWhispKills { get; set; }
    public KeyValuePair<PlayerController, PlayerTracker.PlayerData> MostKhepriKills { get; set; }
    public KeyValuePair<PlayerController, PlayerTracker.PlayerData> MostAstralReturns { get; set; }

    public KeyValuePair<PlayerController, PlayerTracker.PlayerData> BestAccuracy { get; set; }
    public KeyValuePair<PlayerController, PlayerTracker.PlayerData> BestRailvolverAccuracy { get; set; }
    public KeyValuePair<PlayerController, PlayerTracker.PlayerData> WorstAccuracy { get; set; }
    public KeyValuePair<PlayerController, PlayerTracker.PlayerData> MostShotsFired { get; set; }
}

public class TitlesManager
{
    private static readonly Lazy<TitlesManager> _lazy = new Lazy<TitlesManager>(() => new TitlesManager());
    public static TitlesManager Instance => _lazy.Value;
    private const int MaxDisplayedTitles = 8;

    private List<TitleEntry> currentTitles = new List<TitleEntry>();
    private List<TitleEntry> allTitles = new List<TitleEntry>();
    private bool hasGameEndedTitles = false;

    public List<TitleEntry> CurrentTitles => currentTitles.ToList();
    public List<TitleEntry> AllTitles => allTitles.ToList();
    public bool HasGameEndedTitles => hasGameEndedTitles;
    public int TitleCount => currentTitles.Count;
    public event Action OnTitlesUpdated;

    public void CalculateAndStoreTitles(GameStatsSnapshot snapshot)
    {
        currentTitles.Clear();
        allTitles.Clear();

        if (snapshot?.ActivePlayers == null || snapshot.GameMode != GameMode.Survival || snapshot.ActivePlayers.Count <= 1)
        {
            hasGameEndedTitles = false;
            OnTitlesUpdated?.Invoke();
            return;
        }

        var players = snapshot.ActivePlayers.ToList();
        var leaders = CalculateStatLeaders(players);
        var conditions = GetStatConditions(leaders);

        AddTitles(currentTitles, leaders, conditions);

        AddExternalTitles(currentTitles, conditions);

        RemoveDominatedTitles();

        foreach (var player in players)
        {
            if (!currentTitles.Any(t => t.Player == player.Key))
            {
                currentTitles.Add(new TitleEntry(player)
                {
                    TitleName = "Average Joe",
                    Description = "Participated",
                    Priority = 100
                });
            }
        }
        BalanceTitlePriorities();

        allTitles = currentTitles.OrderByDescending(t => t.Priority).ToList();

        currentTitles = SelectTopAndShuffleTitles(currentTitles, MaxDisplayedTitles);

        hasGameEndedTitles = currentTitles.Count > 0;
        Logger.LogInfo($"Calculated {currentTitles.Count} titles for {players.Count} players");
        OnTitlesUpdated?.Invoke();
    }

    private List<TitleEntry> SelectTopAndShuffleTitles(List<TitleEntry> titles, int maxTitles)
    {
        var orderedTitles = titles
            .OrderByDescending(t => t.Priority)
            .ToList();

        var selectedTitles = orderedTitles
            .Take(maxTitles)
            .ToList();

        var remainingTitles = orderedTitles
            .Skip(selectedTitles.Count)
            .ToList();

        RedistributeTitlesByCountGap(selectedTitles, remainingTitles, orderedTitles);

        if (selectedTitles.Count <= 1)
        {
            return selectedTitles;
        }

        var random = new System.Random();
        for (int i = selectedTitles.Count - 1; i > 0; i--)
        {
            int swapIndex = random.Next(i + 1);
            (selectedTitles[i], selectedTitles[swapIndex]) = (selectedTitles[swapIndex], selectedTitles[i]);
        }

        return selectedTitles;
    }

    private void RedistributeTitlesByCountGap(List<TitleEntry> selectedTitles, List<TitleEntry> remainingTitles, List<TitleEntry> allTitles)
    {
        var players = allTitles
            .Select(t => t.Player)
            .Where(p => p != null)
            .Distinct()
            .ToList();

        if (players.Count < 2)
        {
            return;
        }

        int maxIterations = selectedTitles.Count + remainingTitles.Count;
        int iterations = 0;
        while (iterations++ < maxIterations)
        {
            var countsByPlayer = players.ToDictionary(
                player => player,
                player => selectedTitles.Count(t => t.Player == player));

            var highestCounts = countsByPlayer
                .OrderByDescending(kvp => kvp.Value)
                .ToList();

            var lowestCounts = countsByPlayer
                .OrderBy(kvp => kvp.Value)
                .ToList();

            if (highestCounts.Count < 2 || lowestCounts.Count < 2)
            {
                return;
            }

            int topGap = highestCounts[0].Value - highestCounts[1].Value;
            int bottomGap = lowestCounts[1].Value - lowestCounts[0].Value;

            if (topGap <= 1 || bottomGap <= 1)
            {
                return;
            }

            var topPlayer = highestCounts[0].Key;
            var bottomPlayer = lowestCounts[0].Key;

            if (topPlayer == bottomPlayer)
            {
                return;
            }

            var titleToRemove = selectedTitles
                .Where(t => t.Player == topPlayer)
                .OrderBy(t => t.Priority)
                .FirstOrDefault();

            var replacementTitle = remainingTitles
                .Where(t => t.Player == bottomPlayer)
                .OrderByDescending(t => t.Priority)
                .FirstOrDefault();

            if (titleToRemove == null || replacementTitle == null)
            {
                return;
            }

            selectedTitles.Remove(titleToRemove);
            remainingTitles.Remove(replacementTitle);

            selectedTitles.Add(replacementTitle);
            remainingTitles.Add(titleToRemove);
        }
    }

    private StatLeaders CalculateStatLeaders(List<KeyValuePair<PlayerController, PlayerTracker.PlayerData>> players)
    {
        var leaders = new StatLeaders();

        var swingsRanked = players.OrderByDescending(p => p.Value.WebSwings).ThenByDescending(p => p.Value.TotalAliveTime).ToList();
        leaders.MostWebSwings = swingsRanked[0];
        leaders.LeastWebSwings = swingsRanked[swingsRanked.Count - 1];

        var altitudeRanked = players.OrderByDescending(p => p.Value.HighestPoint).ThenByDescending(p => p.Value.TotalAliveTime).ToList();
        leaders.HighestPoint = altitudeRanked[0];
        leaders.LowestPoint = altitudeRanked[altitudeRanked.Count - 1];

        var airborneRanked = players.OrderByDescending(p => p.Value.AirborneTime).ThenByDescending(p => p.Value.TotalAliveTime).ToList();
        leaders.MostAirborneTime = airborneRanked[0];
        leaders.LeastAirborneTime = airborneRanked[airborneRanked.Count - 1];

        var airborneKillsRanked = players.OrderByDescending(p => p.Value.KillsWhileAirborne).ThenByDescending(p => p.Value.Kills).ToList();
        leaders.MostKillsWhileAirborne = airborneKillsRanked[0];

        var soloKillsRanked = players.OrderByDescending(p => p.Value.KillsWhileSolo).ThenByDescending(p => p.Value.Kills).ToList();
        leaders.MostKillsWhileSolo = soloKillsRanked[0];

        var waveClutchesRanked = players.OrderByDescending(p => p.Value.WaveClutches).ThenByDescending(p => p.Value.Kills).ToList();
        leaders.MostWaveClutches = waveClutchesRanked[0];

        var streakRanked = players.OrderByDescending(p => p.Value.MaxKillStreak).ThenByDescending(p => p.Value.TotalAliveTime).ToList();
        leaders.MaxKillStreak = streakRanked[0];

        var streakWhileSoloRanked = players.OrderByDescending(p => p.Value.MaxKillStreakWhileSolo).ThenByDescending(p => p.Value.TotalAliveTime).ToList();
        leaders.MaxKillStreakWhileSolo = streakWhileSoloRanked[0];

        var aliveTimeRanked = players.OrderByDescending(p => p.Value.TotalAliveTime).ToList();
        leaders.MostAliveTime = aliveTimeRanked[0];

        var offenseRanked = players.OrderByDescending(p => p.Value.Kills + p.Value.EnemyShieldsTakenDown).ThenByDescending(p => p.Value.TotalAliveTime).ToList();
        leaders.MostOffense = offenseRanked[0];
        leaders.LeastOffense = offenseRanked[offenseRanked.Count - 1];

        var damageTakenRanked = players.OrderByDescending(p => p.Value.Deaths + p.Value.ShieldsLost).ThenByDescending(p => p.Value.TotalAliveTime).ToList();
        leaders.MostDamageTaken = damageTakenRanked[0];
        leaders.LeastDamageTaken = damageTakenRanked[damageTakenRanked.Count - 1];

        var friendlyFireRanked = players.OrderByDescending(p => p.Value.FriendlyKills + p.Value.FriendlyShieldsHit).ThenByDescending(p => p.Value.TotalAliveTime).ToList();
        leaders.MostFriendlyFire = friendlyFireRanked[0];
        leaders.LeastFriendlyFire = friendlyFireRanked[friendlyFireRanked.Count - 1];

        var shieldsLostRanked = players.OrderByDescending(p => p.Value.ShieldsLost).ThenByDescending(p => p.Value.TotalAliveTime).ToList();
        leaders.MostShieldsLost = shieldsLostRanked[0];
        leaders.LeastShieldsLost = shieldsLostRanked[shieldsLostRanked.Count - 1];

        var deathsRanked = players.OrderByDescending(p => p.Value.Deaths).ThenByDescending(p => p.Value.TotalAliveTime).ToList();
        leaders.MostDeaths = deathsRanked[0];
        leaders.LeastDeaths = deathsRanked[deathsRanked.Count - 1];

        var lavaDeathsRanked = players.OrderByDescending(p => p.Value.LavaDeaths).ThenByDescending(p => p.Value.TotalAliveTime).ToList();
        leaders.MostLavaDeaths = lavaDeathsRanked[0];
        leaders.LeastLavaDeaths = lavaDeathsRanked[lavaDeathsRanked.Count - 1];

        var gunsRanked = players.OrderByDescending(p => WeaponKills(p.Value, GunWeapons)).ThenByDescending(p => p.Value.TotalAliveTime).ToList();
        leaders.MostGunsKills = gunsRanked[0];

        var explosionsRanked = players.OrderByDescending(p => WeaponKills(p.Value, ExplosiveWeapons)).ThenByDescending(p => p.Value.TotalAliveTime).ToList();
        leaders.MostExplosionsKills = explosionsRanked[0];

        var bladeRanked = players.OrderByDescending(p => WeaponKills(p.Value, BladeWeapons)).ThenByDescending(p => p.Value.TotalAliveTime).ToList();
        leaders.MostBladeKills = bladeRanked[0];

        var hornetsRanked = players.OrderByDescending(p => p.Value.EnemyKills["Hornet"]).ThenByDescending(p => p.Value.TotalAliveTime).ToList();
        leaders.MostHornetKills = hornetsRanked[0];

        var whispsRanked = players.OrderByDescending(p => p.Value.EnemyKills["Whisp"] + p.Value.EnemyKills["Power Whisp"]).ThenByDescending(p => p.Value.TotalAliveTime).ToList();
        leaders.MostWhispKills = whispsRanked[0];

        var kheprisRanked = players.OrderByDescending(p => p.Value.EnemyKills["Khepri"] + p.Value.EnemyKills["Power Khepri"]).ThenByDescending(p => p.Value.TotalAliveTime).ToList();
        leaders.MostKhepriKills = kheprisRanked[0];

        var astralReturnsRanked = players.OrderByDescending(p => p.Value.AstralReturns).ThenByDescending(p => p.Value.TotalAliveTime).ToList();
        leaders.MostAstralReturns = astralReturnsRanked[0];

        leaders.BestAccuracy = RankByAccuracy(players, p => p.ShootingAccuracy, MinAccuracyShots)[0];
        leaders.BestRailvolverAccuracy = RankByAccuracy(players, RailvolverAccuracy, MinRailvolverShots)[0];
        leaders.WorstAccuracy = RankByAccuracy(players, p => p.ShootingAccuracy, MinAccuracyShots, lowestFirst: true)[0];

        var shotsFiredRanked = players.OrderByDescending(p => p.Value.ShootingAccuracy.ShotsFired).ThenByDescending(p => p.Value.TotalAliveTime).ToList();
        leaders.MostShotsFired = shotsFiredRanked[0];

        return leaders;
    }

    private const int MinAccuracyShots = 10;
    private const int MinRailvolverShots = 5;
    private static readonly string[] RailvolverWeapons = { "Railvolver", "DoubleRailvolver" };

    // Players below the shot minimum rank last, so a couple of lucky shots can't win an accuracy title.
    private static List<KeyValuePair<PlayerController, PlayerTracker.PlayerData>> RankByAccuracy(
        List<KeyValuePair<PlayerController, PlayerTracker.PlayerData>> players, Func<PlayerTracker.PlayerData, WeaponAccuracy> accuracy, int minShots, bool lowestFirst = false)
    {
        var ranked = players
            .Select(p => (Player: p, Accuracy: accuracy(p.Value)))
            .OrderByDescending(p => p.Accuracy.ShotsFired >= minShots);
        ranked = lowestFirst ? ranked.ThenBy(p => p.Accuracy.Percentage ?? -1) : ranked.ThenByDescending(p => p.Accuracy.Percentage ?? -1);
        return ranked
            .ThenByDescending(p => p.Accuracy.ShotsFired)
            .ThenByDescending(p => p.Player.Value.TotalAliveTime)
            .Select(p => p.Player)
            .ToList();
    }

    private static WeaponAccuracy RailvolverAccuracy(PlayerTracker.PlayerData data)
    {
        int fired = 0, hit = 0;
        foreach (string weapon in RailvolverWeapons)
        {
            if (data.AccuracyByWeapon.TryGetValue(weapon, out var accuracy))
            {
                fired += accuracy.ShotsFired;
                hit += accuracy.ShotsHit;
            }
        }
        var combined = new WeaponAccuracy();
        combined.Set(fired, hit);
        return combined;
    }

    private static bool HasAccuracy(WeaponAccuracy accuracy, int minShots) => accuracy.ShotsFired >= minShots && accuracy.ShotsHit > 0;

    private static readonly string[] GunWeapons = { "Shotgun", "RailShot", "DeathRay", "EnergyBall", "Laser Cannon", "SawDisc" };
    private static readonly string[] ExplosiveWeapons = { "Explosions", "Laser Cube", "DeathCube", "Snowball", "Pulse" };
    private static readonly string[] BladeWeapons = { "Particle Blade", "KhepriStaff" };

    private static int WeaponKills(PlayerTracker.PlayerData data, string[] weapons)
    {
        return weapons.Sum(weapon => data.WeaponHits[weapon]);
    }

    public void ClearTitles()
    {
        currentTitles.Clear();
        allTitles.Clear();
        hasGameEndedTitles = false;
        OnTitlesUpdated?.Invoke();
    }

    public void SetRemoteTitles(List<TitleEntry> current, List<TitleEntry> all)
    {
        currentTitles = current ?? new List<TitleEntry>();
        allTitles = all ?? new List<TitleEntry>();
        hasGameEndedTitles = currentTitles.Count > 0;
        Logger.LogInfo($"Received {currentTitles.Count} titles from the host");
        OnTitlesUpdated?.Invoke();
    }


    private void RemoveDominatedTitles()
    {
        var titlesByPlayer = currentTitles.GroupBy(t => t.Player);
        var toRemove = new HashSet<TitleEntry>();

        foreach (var playerTitles in titlesByPlayer)
        {
            // Sort titles by requirement count (descending) for optimization
            // Titles with more requirements can only dominate titles with fewer requirements
            var titles = playerTitles.OrderByDescending(t => t.Requirements.Count).ToList();

            for (int i = 0; i < titles.Count; i++)
            {
                if (toRemove.Contains(titles[i]))
                    continue;

                var currentRequirementCount = titles[i].Requirements.Count;

                for (int j = 0; j < i; j++)
                {
                    if (titles[j].Requirements.Count <= currentRequirementCount)
                        break;

                    if (titles[i].Requirements.IsSubsetOf(titles[j].Requirements))
                    {
                        toRemove.Add(titles[i]);
                        break;
                    }
                }
            }
        }

        currentTitles.RemoveAll(t => toRemove.Contains(t));
    }

    // To prevent one player from dominating all titles due to sheer number of categories,
    // we increase the priority of titles held by other players. Makes it more fun.
    private void BalanceTitlePriorities()
    {
        var titlesByPlayer = currentTitles.GroupBy(t => t.Player)
                                          .ToDictionary(g => g.Key, g => g.Count());

        foreach (var title in currentTitles)
        {
            int otherPlayersTitleCount = titlesByPlayer
                .Where(kvp => kvp.Key != title.Player)
                .Sum(kvp => kvp.Value);

            title.Priority += otherPlayersTitleCount * 5;
        }
    }

    /// <summary>
    /// Processes titles registered by external mods, validating their native requirements against the stats mod's conditions
    /// </summary>
    private void AddExternalTitles(List<TitleEntry> titles, Dictionary<string, StatCondition> conditions)
    {
        var externalTitles = StatsModApi.GetExternalTitles();
        if (externalTitles == null || externalTitles.Count == 0) return;

        foreach (var extTitle in externalTitles)
        {
            var (player, descriptions, valid) = ValidateExternalRequirements(extTitle, conditions);
            if (!valid) continue;

            if (extTitle.Player == null)
                ResolvePlayerInfo(extTitle, player);

            AppendDescriptions(extTitle, descriptions);
            titles.Add(extTitle);
        }
    }

    private (PlayerController player, List<string> descriptions, bool valid) ValidateExternalRequirements(
        TitleEntry title, Dictionary<string, StatCondition> conditions)
    {
        PlayerController resolvedPlayer = title.Player;
        var descriptions = new List<string>();

        foreach (var req in title.Requirements)
        {
            // Custom requirements from external mods won't be in `conditions`,
            // so we skip them here (we assume the external mod validated its own custom stats).
            if (!conditions.TryGetValue(req, out var cond))
                continue;

            // If a native requirement exists but wasn't achieved (> 0), reject the title.
            if (!cond.HasStat)
                return (null, null, false);

            // Dynamically assign the player from the *first* matched native requirement
            resolvedPlayer ??= cond.Leader.Key;

            // Make sure all native requirements actually applied to the exact same player
            if (cond.Leader.Key != resolvedPlayer)
                return (null, null, false);

            descriptions.Add(cond.FormattedDescription);
        }

        // Check if we still don't have a player after checking all requirements.
        // This handles the edge case where:
        // 1. The external mod didn't explicitly request a player (title.Player was null)
        // 2. All of the title's requirements were custom (so it skipped every loop iteration above).
        if (resolvedPlayer == null)
        {
            Logger.LogWarning($"ValidateExternalRequirements: Failed to resolve player for title '{title.TitleName}'. If a title uses entirely custom requirements, you must explicitly provide the 'leader' parameter when registering it through StatsModApi.");
            return (null, null, false);
        }

        return (resolvedPlayer, descriptions, true);
    }

    private void ResolvePlayerInfo(TitleEntry title, PlayerController player)
    {
        title.Player = player;
        title.PlayerId = player != null ? player.playerID.Value : -1;
        var activePlayers = PlayerTracker.Instance.GetActivePlayers();
        if (activePlayers.TryGetValue(player, out var pData))
        {
            title.PlayerName = pData.PlayerName;
            title.PrimaryColor = pData.PlayerColor;
            title.SecondaryColor = pData.SecondaryColor;
        }
    }

    private void AppendDescriptions(TitleEntry title, List<string> newParts)
    {
        if (newParts == null || newParts.Count == 0) return;

        string baseDesc = string.Join("\n", newParts);
        title.Description = string.IsNullOrEmpty(title.Description)
            ? baseDesc
            : title.Description + "\n" + baseDesc;
    }

    private class StatCondition
    {
        public StatCondition(string requirementName, KeyValuePair<PlayerController, PlayerTracker.PlayerData> leader, bool hasStat, string formattedDescription)
        {
            RequirementName = requirementName;
            Leader = leader;
            HasStat = hasStat;
            FormattedDescription = formattedDescription;
        }

        public string RequirementName { get; }
        public KeyValuePair<PlayerController, PlayerTracker.PlayerData> Leader { get; }
        public bool HasStat { get; }
        public string FormattedDescription { get; }
    }

    private Dictionary<string, StatCondition> GetStatConditions(StatLeaders leaders)
    {
        var conditions = new Dictionary<string, StatCondition>();

        void Add(string req, KeyValuePair<PlayerController, PlayerTracker.PlayerData> leader, bool hasStat, string desc)
        {
            conditions[req] = new StatCondition(req, leader, hasStat, desc);
        }

        Add(Req.MostWebSwings, leaders.MostWebSwings, leaders.MostWebSwings.Value.WebSwings > 0, $"Most Web Swings ({leaders.MostWebSwings.Value.WebSwings})");
        Add(Req.LeastWebSwings, leaders.LeastWebSwings, true, $"Least Web Swings ({leaders.LeastWebSwings.Value.WebSwings})");
        Add(Req.HighestPoint, leaders.HighestPoint, leaders.HighestPoint.Value.HighestPoint > 0, $"Highest Point ({leaders.HighestPoint.Value.HighestPoint:F1}m)");
        Add(Req.LowestPoint, leaders.LowestPoint, true, $"Lowest Point ({leaders.LowestPoint.Value.HighestPoint:F1}m)");
        Add(Req.MostAirborneTime, leaders.MostAirborneTime, leaders.MostAirborneTime.Value.AirborneTime > 0f, $"Most Airborne Time ({leaders.MostAirborneTime.Value.AirborneTime:F1}s)");
        Add(Req.LeastAirborneTime, leaders.LeastAirborneTime, true, $"Least Airborne Time ({leaders.LeastAirborneTime.Value.AirborneTime:F1}s)");
        Add(Req.MostKillsWhileAirborne, leaders.MostKillsWhileAirborne, leaders.MostKillsWhileAirborne.Value.KillsWhileAirborne > 0, $"Most Kills While Airborne ({leaders.MostKillsWhileAirborne.Value.KillsWhileAirborne})");
        Add(Req.MostKillsWhileSolo, leaders.MostKillsWhileSolo, leaders.MostKillsWhileSolo.Value.KillsWhileSolo > 0, $"Most Kills While Solo ({leaders.MostKillsWhileSolo.Value.KillsWhileSolo})");
        Add(Req.MostWaveClutches, leaders.MostWaveClutches, leaders.MostWaveClutches.Value.WaveClutches > 0, $"Most Wave Clutches ({leaders.MostWaveClutches.Value.WaveClutches})");
        Add(Req.MaxKillStreak, leaders.MaxKillStreak, leaders.MaxKillStreak.Value.MaxKillStreak > 0, $"Max Kill Streak ({leaders.MaxKillStreak.Value.MaxKillStreak})");
        Add(Req.MaxKillStreakWhileSolo, leaders.MaxKillStreakWhileSolo, leaders.MaxKillStreakWhileSolo.Value.MaxKillStreakWhileSolo > 0, $"Max Kill Streak While Solo ({leaders.MaxKillStreakWhileSolo.Value.MaxKillStreakWhileSolo})");
        Add(Req.MostAliveTime, leaders.MostAliveTime, leaders.MostAliveTime.Value.TotalAliveTime > 0f, $"Most Alive Time ({leaders.MostAliveTime.Value.TotalAliveTime:F1}s)");

        long mostOffenseVal = leaders.MostOffense.Value.Kills + leaders.MostOffense.Value.EnemyShieldsTakenDown;
        Add(Req.MostOffense, leaders.MostOffense, mostOffenseVal > 0, $"Most Offense ({mostOffenseVal})");

        long leastOffenseVal = leaders.LeastOffense.Value.Kills + leaders.LeastOffense.Value.EnemyShieldsTakenDown;
        Add(Req.LeastOffense, leaders.LeastOffense, true, $"Least Offense ({leastOffenseVal})");

        long mostDamageTakenVal = leaders.MostDamageTaken.Value.Deaths + leaders.MostDamageTaken.Value.ShieldsLost;
        Add(Req.MostDamageTaken, leaders.MostDamageTaken, mostDamageTakenVal > 0, $"Most Damage Taken ({mostDamageTakenVal})");

        long leastDamageTakenVal = leaders.LeastDamageTaken.Value.Deaths + leaders.LeastDamageTaken.Value.ShieldsLost;
        Add(Req.LeastDamageTaken, leaders.LeastDamageTaken, true, $"Least Damage Taken ({leastDamageTakenVal})");

        long mostFriendlyFireVal = leaders.MostFriendlyFire.Value.FriendlyKills + leaders.MostFriendlyFire.Value.FriendlyShieldsHit;
        Add(Req.MostFriendlyFire, leaders.MostFriendlyFire, mostFriendlyFireVal > 0, $"Most Friendly Fire ({mostFriendlyFireVal})");

        long leastFriendlyFireVal = leaders.LeastFriendlyFire.Value.FriendlyKills + leaders.LeastFriendlyFire.Value.FriendlyShieldsHit;
        Add(Req.LeastFriendlyFire, leaders.LeastFriendlyFire, true, $"Least Friendly Fire ({leastFriendlyFireVal})");

        Add(Req.MostShieldsLost, leaders.MostShieldsLost, leaders.MostShieldsLost.Value.ShieldsLost > 0, $"Most Shields Lost ({leaders.MostShieldsLost.Value.ShieldsLost})");
        Add(Req.LeastShieldsLost, leaders.LeastShieldsLost, true, $"Least Shields Lost ({leaders.LeastShieldsLost.Value.ShieldsLost})");
        Add(Req.MostDeaths, leaders.MostDeaths, leaders.MostDeaths.Value.Deaths > 0, $"Most Deaths ({leaders.MostDeaths.Value.Deaths})");
        Add(Req.LeastDeaths, leaders.LeastDeaths, true, $"Least Deaths ({leaders.LeastDeaths.Value.Deaths})");
        Add(Req.MostLavaDeaths, leaders.MostLavaDeaths, leaders.MostLavaDeaths.Value.LavaDeaths > 0, $"Most Lava Deaths ({leaders.MostLavaDeaths.Value.LavaDeaths})");
        Add(Req.LeastLavaDeaths, leaders.LeastLavaDeaths, true, $"Least Lava Deaths ({leaders.LeastLavaDeaths.Value.LavaDeaths})");

        long gunsVal = WeaponKills(leaders.MostGunsKills.Value, GunWeapons);
        Add(Req.MostGunsKills, leaders.MostGunsKills, gunsVal > 0, $"Most Gun Kills ({gunsVal})");

        long expVal = WeaponKills(leaders.MostExplosionsKills.Value, ExplosiveWeapons);
        Add(Req.MostExplosionsKills, leaders.MostExplosionsKills, expVal > 0, $"Most Explosive Kills ({expVal})");

        long bladeVal = WeaponKills(leaders.MostBladeKills.Value, BladeWeapons);
        Add(Req.MostBladeKills, leaders.MostBladeKills, bladeVal > 0, $"Most Blade Kills ({bladeVal})");

        Add(Req.MostHornetKills, leaders.MostHornetKills, leaders.MostHornetKills.Value.EnemyKills["Hornet"] > 0, $"Most Hornets Killed ({leaders.MostHornetKills.Value.EnemyKills["Hornet"]})");

        long whispVal = leaders.MostWhispKills.Value.EnemyKills["Whisp"] + leaders.MostWhispKills.Value.EnemyKills["Power Whisp"];
        Add(Req.MostWhispKills, leaders.MostWhispKills, whispVal > 0, $"Most Whisps Killed ({whispVal})");

        long khepriVal = leaders.MostKhepriKills.Value.EnemyKills["Khepri"] + leaders.MostKhepriKills.Value.EnemyKills["Power Khepri"];
        Add(Req.MostKhepriKills, leaders.MostKhepriKills, khepriVal > 0, $"Most Khepris Killed ({khepriVal})");

        Add(Req.MostAstralReturns, leaders.MostAstralReturns, leaders.MostAstralReturns.Value.AstralReturns > 0, $"Most Astral Returns ({leaders.MostAstralReturns.Value.AstralReturns})");

        var accuracy = leaders.BestAccuracy.Value.ShootingAccuracy;
        Add(Req.BestAccuracy, leaders.BestAccuracy, HasAccuracy(accuracy, MinAccuracyShots), $"Best Accuracy ({accuracy.Display})");

        var railvolverAccuracy = RailvolverAccuracy(leaders.BestRailvolverAccuracy.Value);
        Add(Req.BestRailvolverAccuracy, leaders.BestRailvolverAccuracy, HasAccuracy(railvolverAccuracy, MinRailvolverShots), $"Best Railvolver Accuracy ({railvolverAccuracy.Display})");

        var worstAccuracy = leaders.WorstAccuracy.Value.ShootingAccuracy;
        bool hasWorstAccuracy = worstAccuracy.ShotsFired >= MinAccuracyShots && worstAccuracy.Percentage < accuracy.Percentage;
        Add(Req.WorstAccuracy, leaders.WorstAccuracy, hasWorstAccuracy, $"Worst Accuracy ({worstAccuracy.Display})");

        int mostShotsFired = leaders.MostShotsFired.Value.ShootingAccuracy.ShotsFired;
        Add(Req.MostShotsFired, leaders.MostShotsFired, mostShotsFired > 0, $"Most Shots Fired ({mostShotsFired})");

        return conditions;
    }

    private void TryAddTitle(List<TitleEntry> titles, StatLeaders leaders, Dictionary<string, StatCondition> conditions, string name, params string[] reqNames)
    {
        TryAddTitle(titles, leaders, conditions, name, 0, reqNames);
    }

    private void TryAddTitle(List<TitleEntry> titles, StatLeaders leaders, Dictionary<string, StatCondition> conditions, string name, int bonusPriority, params string[] reqNames)
    {
        var relevantConds = reqNames.Select(r => conditions[r]).ToList();
        if (relevantConds.Count == 0) return;

        if (!relevantConds.All(c => c.HasStat)) return;

        var firstLeader = relevantConds.First().Leader;
        if (firstLeader.Key == null || !relevantConds.All(c => c.Leader.Key == firstLeader.Key)) return;

        var desc = string.Join("\n", relevantConds.Select(c => c.FormattedDescription));

        var builder = new TitleBuilder(leaders)
            .ForLeader(_ => firstLeader, relevantConds.First().RequirementName)
            .WithName(name)
            .WithDescription(desc)
            .WithPriority(10 * relevantConds.Count + bonusPriority);

        for (int i = 1; i < relevantConds.Count; i++)
        {
            builder.AndLeader(relevantConds[i].RequirementName);
        }

        titles.Add(builder.Build());
    }

    private void AddTitles(List<TitleEntry> titles, StatLeaders leaders, Dictionary<string, StatCondition> conditions)
    {
        TryAddTitle(titles, leaders, conditions, "Peter Parker", Req.MostWebSwings);
        TryAddTitle(titles, leaders, conditions, leaders.HighestPoint.Value.HighestPoint >= 1000 ? "1000 Meters Club" : "Sky Scraper", leaders.HighestPoint.Value.HighestPoint >= 1000 ? 40 : 0, Req.HighestPoint);
        TryAddTitle(titles, leaders, conditions, "Air Dancer", Req.MostAirborneTime);
        TryAddTitle(titles, leaders, conditions, "Sky Hunter", Req.MostKillsWhileAirborne);
        TryAddTitle(titles, leaders, conditions, "Lone Wolf", Req.MostKillsWhileSolo);
        TryAddTitle(titles, leaders, conditions, "Clutch Master", Req.MostWaveClutches);
        TryAddTitle(titles, leaders, conditions, "Serial Killer", Req.MaxKillStreak);
        TryAddTitle(titles, leaders, conditions, "Solo Rampage", Req.MaxKillStreakWhileSolo);
        TryAddTitle(titles, leaders, conditions, "Survivor", Req.MostAliveTime);
        TryAddTitle(titles, leaders, conditions, "Destroyer", Req.MostOffense);
        TryAddTitle(titles, leaders, conditions, "Punching Bag", Req.MostDamageTaken);
        TryAddTitle(titles, leaders, conditions, "Shadow", Req.LeastDamageTaken);
        TryAddTitle(titles, leaders, conditions, "Confused", Req.MostFriendlyFire);
        TryAddTitle(titles, leaders, conditions, "Team Player", Req.LeastFriendlyFire);
        TryAddTitle(titles, leaders, conditions, "Pacifist", Req.LeastOffense);
        TryAddTitle(titles, leaders, conditions, "Gunslinger", Req.MostGunsKills);
        TryAddTitle(titles, leaders, conditions, "Demolitionist", Req.MostExplosionsKills);
        TryAddTitle(titles, leaders, conditions, "Blade Master", Req.MostBladeKills);
        TryAddTitle(titles, leaders, conditions, "Slippery", Req.MostLavaDeaths);
        TryAddTitle(titles, leaders, conditions, "Floor is Lava", Req.LeastLavaDeaths);
        TryAddTitle(titles, leaders, conditions, "Resurrection", Req.MostAstralReturns);
        TryAddTitle(titles, leaders, conditions, "Bullseye", Req.BestAccuracy);
        TryAddTitle(titles, leaders, conditions, "Sniper", Req.BestRailvolverAccuracy);
        TryAddTitle(titles, leaders, conditions, "Stormtrooper", Req.WorstAccuracy);

        TryAddTitle(titles, leaders, conditions, "Orbital Strike", Req.MostOffense, Req.HighestPoint);
        TryAddTitle(titles, leaders, conditions, "Satellite", Req.MostAirborneTime, Req.HighestPoint);
        TryAddTitle(titles, leaders, conditions, "Glass Cannon", Req.MostOffense, Req.MostDamageTaken);
        TryAddTitle(titles, leaders, conditions, "Sword and Shield", Req.MostOffense, Req.LeastDamageTaken);
        TryAddTitle(titles, leaders, conditions, "Nothing Burger", Req.LeastOffense, Req.LeastFriendlyFire);
        TryAddTitle(titles, leaders, conditions, "On Death's Bed", Req.MostShieldsLost, Req.LeastDeaths);
        TryAddTitle(titles, leaders, conditions, "Perfectly Balanced", Req.MostOffense, Req.MostFriendlyFire);
        TryAddTitle(titles, leaders, conditions, "Spider-Man", Req.MostWebSwings, Req.MostAirborneTime);
        TryAddTitle(titles, leaders, conditions, "Lawn-mower", Req.MostOffense, Req.LowestPoint);
        TryAddTitle(titles, leaders, conditions, "Hit & Run", Req.MostOffense, Req.MostWebSwings);
        TryAddTitle(titles, leaders, conditions, "Last Stand Hero", Req.MostWaveClutches, Req.MostKillsWhileSolo);
        TryAddTitle(titles, leaders, conditions, "Kamikaze", Req.MostExplosionsKills, Req.MostDamageTaken);
        TryAddTitle(titles, leaders, conditions, "War Machine", Req.MostGunsKills, Req.MostOffense);
        TryAddTitle(titles, leaders, conditions, "Silent Assassin", Req.MostBladeKills, Req.LeastDamageTaken);
        TryAddTitle(titles, leaders, conditions, "I have the High Ground", Req.MostBladeKills, Req.HighestPoint);
        TryAddTitle(titles, leaders, conditions, "Nine Lives", Req.MostDamageTaken, Req.MostAliveTime);
        TryAddTitle(titles, leaders, conditions, "Jedi Master", Req.MostHornetKills, Req.MostBladeKills);
        TryAddTitle(titles, leaders, conditions, "Sharpshooter", Req.MostWhispKills, Req.MostGunsKills);
        TryAddTitle(titles, leaders, conditions, "Pharaoh", Req.MostKhepriKills, Req.MostDamageTaken);
        TryAddTitle(titles, leaders, conditions, "Cockroach", Req.MostAliveTime, Req.LowestPoint);
        TryAddTitle(titles, leaders, conditions, "Icarus", Req.MostLavaDeaths, Req.HighestPoint);
        TryAddTitle(titles, leaders, conditions, "Firewalker", Req.LeastLavaDeaths, Req.MostAliveTime);
        TryAddTitle(titles, leaders, conditions, "Gravity Police", Req.MostKillsWhileAirborne, Req.LowestPoint);
        TryAddTitle(titles, leaders, conditions, "Slow and Steady", Req.LeastWebSwings, Req.LeastDamageTaken);
        TryAddTitle(titles, leaders, conditions, "Overkill", Req.MaxKillStreak, Req.MostOffense);
        TryAddTitle(titles, leaders, conditions, "Insurance Policy", Req.MostShieldsLost, Req.MostAliveTime);
        TryAddTitle(titles, leaders, conditions, "Second Chances", Req.MostAstralReturns, Req.MostDeaths);
        TryAddTitle(titles, leaders, conditions, "Guns Blazing", Req.MaxKillStreakWhileSolo, Req.MostGunsKills);
        TryAddTitle(titles, leaders, conditions, "Came to Finish the Job", Req.MaxKillStreakWhileSolo, Req.MostAstralReturns);
        TryAddTitle(titles, leaders, conditions, "Defying Gravity", Req.LeastLavaDeaths, Req.MostAirborneTime);
        TryAddTitle(titles, leaders, conditions, "Phoenix", Req.LeastLavaDeaths, Req.MostAstralReturns);
        TryAddTitle(titles, leaders, conditions, "Death from Above", Req.BestAccuracy, Req.HighestPoint);
        TryAddTitle(titles, leaders, conditions, "Non-Lethal Rounds", Req.BestAccuracy, Req.LeastOffense);
        TryAddTitle(titles, leaders, conditions, "Aimbot", Req.BestAccuracy, Req.MostOffense);
        TryAddTitle(titles, leaders, conditions, "Steady", Req.BestAccuracy, Req.LeastAirborneTime);
        TryAddTitle(titles, leaders, conditions, "Spray and Pray", Req.WorstAccuracy, Req.MostShotsFired);

        TryAddTitle(titles, leaders, conditions, "ICBM", Req.MostExplosionsKills, Req.HighestPoint, Req.MostAirborneTime);
        TryAddTitle(titles, leaders, conditions, "Phantom Blade", Req.MostBladeKills, Req.HighestPoint, Req.LeastDamageTaken);
        TryAddTitle(titles, leaders, conditions, "Elegant Barbarian", Req.MostOffense, Req.MostDamageTaken, Req.LeastFriendlyFire);
        TryAddTitle(titles, leaders, conditions, "MVP", Req.MostOffense, Req.LeastDamageTaken, Req.LeastFriendlyFire);
        TryAddTitle(titles, leaders, conditions, "Agent of Chaos", Req.MostOffense, Req.LeastDamageTaken, Req.MostFriendlyFire);
        TryAddTitle(titles, leaders, conditions, "Ordered Chaos", Req.MostFriendlyFire, Req.MaxKillStreakWhileSolo, Req.MostWaveClutches);
        TryAddTitle(titles, leaders, conditions, "Traitor", Req.LeastOffense, Req.LeastDamageTaken, Req.MostFriendlyFire);
        TryAddTitle(titles, leaders, conditions, "AFK", Req.MostDamageTaken, Req.LeastOffense, Req.LeastFriendlyFire);
        TryAddTitle(titles, leaders, conditions, "Spooderman", Req.MostWebSwings, Req.MostAirborneTime, Req.MostDamageTaken);
        TryAddTitle(titles, leaders, conditions, "Basement Dweller", Req.LowestPoint, Req.LeastAirborneTime, Req.LeastWebSwings);
        TryAddTitle(titles, leaders, conditions, "Marksman", Req.MostGunsKills, Req.MostOffense, Req.LeastFriendlyFire);
        TryAddTitle(titles, leaders, conditions, "Mutually Assured Destruction", Req.MostExplosionsKills, Req.MostOffense, Req.MostFriendlyFire);
        TryAddTitle(titles, leaders, conditions, "Master of Arms", Req.MostExplosionsKills, Req.MostGunsKills, Req.MostBladeKills);
        TryAddTitle(titles, leaders, conditions, "Sith Lord", Req.MostHornetKills, Req.MostBladeKills, Req.MostFriendlyFire);
        TryAddTitle(titles, leaders, conditions, "One Man Army", Req.MaxKillStreakWhileSolo, Req.MostKillsWhileSolo, Req.MostWaveClutches);
        TryAddTitle(titles, leaders, conditions, "Iron Spider", Req.MostWebSwings, Req.MostAirborneTime, Req.MostBladeKills);
        TryAddTitle(titles, leaders, conditions, "Perseverance", Req.MostAstralReturns, Req.MostShieldsLost, Req.MostAliveTime);
        TryAddTitle(titles, leaders, conditions, "A Gust of Wind", Req.MostAstralReturns, Req.LeastOffense, Req.LeastFriendlyFire);
        TryAddTitle(titles, leaders, conditions, "Anubis", Req.MostAstralReturns, Req.MostKhepriKills, Req.MostDamageTaken);
        TryAddTitle(titles, leaders, conditions, "Surgical Strike", Req.BestAccuracy, Req.MostOffense, Req.LeastFriendlyFire);
        TryAddTitle(titles, leaders, conditions, "Camper", Req.BestAccuracy, Req.LeastAirborneTime, Req.LeastWebSwings);
        TryAddTitle(titles, leaders, conditions, "Bullet Time", Req.BestAccuracy, Req.MostAirborneTime, Req.MostKillsWhileAirborne);
        TryAddTitle(titles, leaders, conditions, "High Noon", Req.BestAccuracy, Req.MostGunsKills, Req.MostKillsWhileSolo);
        TryAddTitle(titles, leaders, conditions, "Deadeye", Req.BestAccuracy, Req.MostDamageTaken, Req.MostDeaths);
        TryAddTitle(titles, leaders, conditions, "Trigger Happy", Req.WorstAccuracy, Req.MostShotsFired, Req.MostFriendlyFire);
        TryAddTitle(titles, leaders, conditions, "Blind Rage", Req.WorstAccuracy, Req.MostOffense, Req.MaxKillStreak);

        TryAddTitle(titles, leaders, conditions, "God Complex", Req.MostOffense, Req.LeastDamageTaken, Req.HighestPoint, Req.LeastFriendlyFire);
        TryAddTitle(titles, leaders, conditions, "The Untouchable", Req.HighestPoint, Req.MostAirborneTime, Req.LeastDamageTaken, Req.MostWebSwings);
        TryAddTitle(titles, leaders, conditions, "Nuclear Warhead", Req.MostOffense, Req.HighestPoint, Req.MostAirborneTime, Req.MostExplosionsKills);
        TryAddTitle(titles, leaders, conditions, "Inside Job", Req.LeastOffense, Req.LeastDamageTaken, Req.MostFriendlyFire, Req.MostExplosionsKills);
        TryAddTitle(titles, leaders, conditions, "Rambo", Req.MostOffense, Req.LeastDamageTaken, Req.LeastFriendlyFire, Req.MostGunsKills);
        TryAddTitle(titles, leaders, conditions, "Darth Vader", Req.MostHornetKills, Req.MostBladeKills, Req.MostFriendlyFire, Req.MostLavaDeaths);
        TryAddTitle(titles, leaders, conditions, "Bunker", Req.LowestPoint, Req.LeastAirborneTime, Req.LeastWebSwings, Req.MostAliveTime);
        TryAddTitle(titles, leaders, conditions, "Air Superiority", Req.HighestPoint, Req.MostAirborneTime, Req.MostKillsWhileAirborne, Req.MostWebSwings);
        TryAddTitle(titles, leaders, conditions, "Spectral Blade", Req.MostAstralReturns, Req.MostBladeKills, Req.HighestPoint, Req.LeastDamageTaken);
        TryAddTitle(titles, leaders, conditions, "Turret", Req.BestAccuracy, Req.LeastWebSwings, Req.LeastAirborneTime, Req.LowestPoint);
    }
}
