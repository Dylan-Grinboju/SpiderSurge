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

    public bool IsNew { get; set; }
    internal DateTime EarnedUtc { get; set; }
    public string TitleName { get; set; }
    public string Description { get; set; }
    public string PlayerName { get; set; }
    public Color PrimaryColor { get; set; }
    public Color SecondaryColor { get; set; }
    public PlayerController Player { get; set; }
    public int PlayerId { get; set; } = -1;
    public Texture Portrait { get; set; }
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
}

public static class Req
{
    public const string MostWebSwings = nameof(MostWebSwings);
    public const string LeastWebSwings = nameof(LeastWebSwings);
    public const string HighestPoint = nameof(HighestPoint);
    public const string LowestPoint = nameof(LowestPoint);
    public const string MostAirborneTime = nameof(MostAirborneTime);
    public const string LeastAirborneTime = nameof(LeastAirborneTime);
    public const string MostKillsWhileAirborne = nameof(MostKillsWhileAirborne);
    public const string MostKillsWhileSolo = nameof(MostKillsWhileSolo);
    public const string MostWaveClutches = nameof(MostWaveClutches);
    public const string MaxKillStreak = nameof(MaxKillStreak);
    public const string MaxKillStreakWhileSolo = nameof(MaxKillStreakWhileSolo);
    public const string MostAliveTime = nameof(MostAliveTime);
    public const string MostOffense = nameof(MostOffense);
    public const string LeastOffense = nameof(LeastOffense);
    public const string MostDamageTaken = nameof(MostDamageTaken);
    public const string LeastDamageTaken = nameof(LeastDamageTaken);
    public const string MostFriendlyFire = nameof(MostFriendlyFire);
    public const string LeastFriendlyFire = nameof(LeastFriendlyFire);
    public const string MostShieldsLost = nameof(MostShieldsLost);
    public const string LeastShieldsLost = nameof(LeastShieldsLost);
    public const string MostDeaths = nameof(MostDeaths);
    public const string LeastDeaths = nameof(LeastDeaths);
    public const string MostLavaDeaths = nameof(MostLavaDeaths);
    public const string LeastLavaDeaths = nameof(LeastLavaDeaths);
    public const string MostGunsKills = nameof(MostGunsKills);
    public const string MostExplosionsKills = nameof(MostExplosionsKills);
    public const string MostBladeKills = nameof(MostBladeKills);
    public const string MostHornetKills = nameof(MostHornetKills);
    public const string MostWhispKills = nameof(MostWhispKills);
    public const string MostKhepriKills = nameof(MostKhepriKills);
    public const string MostAstralReturns = nameof(MostAstralReturns);
    public const string BestAccuracy = nameof(BestAccuracy);
    public const string BestRailvolverAccuracy = nameof(BestRailvolverAccuracy);
    public const string WorstAccuracy = nameof(WorstAccuracy);
    public const string MostShotsFired = nameof(MostShotsFired);
}

public class StatLeaders
{
    private readonly Dictionary<string, KeyValuePair<PlayerController, PlayerTracker.PlayerData>> leaders = new Dictionary<string, KeyValuePair<PlayerController, PlayerTracker.PlayerData>>();

    public KeyValuePair<PlayerController, PlayerTracker.PlayerData> this[string requirement]
    {
        get => leaders[requirement];
        internal set => leaders[requirement] = value;
    }
}

internal enum StatFormat { Count, Meters, Seconds }

internal sealed class StatDef
{
    private static readonly Func<PlayerTracker.PlayerData, double> AliveTime = p => p.TotalAliveTime;
    private static readonly Func<PlayerTracker.PlayerData, double> Kills = p => p.Kills;

    private static readonly string[] GunWeapons = { "Shotgun", "RailShot", "DeathRay", "EnergyBall", "Laser Cannon", "SawDisc" };
    private static readonly string[] ExplosiveWeapons = { "Explosions", "Laser Cube", "DeathCube", "Snowball", "Pulse" };
    private static readonly string[] BladeWeapons = { "Particle Blade", "KhepriStaff" };

    // Every title stat but the accuracy ones; ranked descending by Value then TieBreak, the Least side is the last in that order
    internal static readonly StatDef[] All =
    {
        new StatDef(Req.MostWebSwings, "Most Web Swings", p => p.WebSwings, AliveTime, leastReq: Req.LeastWebSwings, leastLabel: "Least Web Swings"),
        new StatDef(Req.HighestPoint, "Highest Point", p => p.HighestPoint, AliveTime, StatFormat.Meters, Req.LowestPoint, "Lowest Point"),
        new StatDef(Req.MostAirborneTime, "Most Airborne Time", p => p.AirborneTime, AliveTime, StatFormat.Seconds, Req.LeastAirborneTime, "Least Airborne Time"),
        new StatDef(Req.MostKillsWhileAirborne, "Most Kills While Airborne", p => p.KillsWhileAirborne, Kills),
        new StatDef(Req.MostKillsWhileSolo, "Most Kills While Solo", p => p.KillsWhileSolo, Kills),
        new StatDef(Req.MostWaveClutches, "Most Wave Clutches", p => p.WaveClutches, Kills),
        new StatDef(Req.MaxKillStreak, "Max Kill Streak", p => p.MaxKillStreak, AliveTime),
        new StatDef(Req.MaxKillStreakWhileSolo, "Max Kill Streak While Solo", p => p.MaxKillStreakWhileSolo, AliveTime),
        new StatDef(Req.MostAliveTime, "Most Alive Time", p => p.TotalAliveTime, null, StatFormat.Seconds),
        new StatDef(Req.MostOffense, "Most Offense", p => p.Kills + p.EnemyShieldsTakenDown, AliveTime, leastReq: Req.LeastOffense, leastLabel: "Least Offense"),
        new StatDef(Req.MostDamageTaken, "Most Damage Taken", p => p.Deaths + p.ShieldsLost, AliveTime, leastReq: Req.LeastDamageTaken, leastLabel: "Least Damage Taken"),
        new StatDef(Req.MostFriendlyFire, "Most Friendly Fire", p => p.FriendlyKills + p.FriendlyShieldsHit, AliveTime, leastReq: Req.LeastFriendlyFire, leastLabel: "Least Friendly Fire"),
        new StatDef(Req.MostShieldsLost, "Most Shields Lost", p => p.ShieldsLost, AliveTime, leastReq: Req.LeastShieldsLost, leastLabel: "Least Shields Lost"),
        new StatDef(Req.MostDeaths, "Most Deaths", p => p.Deaths, AliveTime, leastReq: Req.LeastDeaths, leastLabel: "Least Deaths"),
        new StatDef(Req.MostLavaDeaths, "Most Lava Deaths", p => p.LavaDeaths, AliveTime, leastReq: Req.LeastLavaDeaths, leastLabel: "Least Lava Deaths"),
        new StatDef(Req.MostGunsKills, "Most Gun Kills", p => WeaponKills(p, GunWeapons), AliveTime),
        new StatDef(Req.MostExplosionsKills, "Most Explosive Kills", p => WeaponKills(p, ExplosiveWeapons), AliveTime),
        new StatDef(Req.MostBladeKills, "Most Blade Kills", p => WeaponKills(p, BladeWeapons), AliveTime),
        new StatDef(Req.MostHornetKills, "Most Hornets Killed", p => p.EnemyKills["Hornet"], AliveTime),
        new StatDef(Req.MostWhispKills, "Most Whisps Killed", p => p.EnemyKills["Whisp"] + p.EnemyKills["Power Whisp"], AliveTime),
        new StatDef(Req.MostKhepriKills, "Most Khepris Killed", p => p.EnemyKills["Khepri"] + p.EnemyKills["Power Khepri"], AliveTime),
        new StatDef(Req.MostAstralReturns, "Most Astral Returns", p => p.AstralReturns, AliveTime),
        new StatDef(Req.MostShotsFired, "Most Shots Fired", p => p.ShootingAccuracy.ShotsFired, AliveTime),
    };

    private StatDef(string req, string label, Func<PlayerTracker.PlayerData, double> value, Func<PlayerTracker.PlayerData, double> tieBreak,
        StatFormat format = StatFormat.Count, string leastReq = null, string leastLabel = null)
    {
        Requirement = req;
        Label = label;
        Value = value;
        TieBreak = tieBreak;
        Format = format;
        LeastRequirement = leastReq;
        LeastLabel = leastLabel;
    }

    public string Requirement { get; }
    public string Label { get; }
    public Func<PlayerTracker.PlayerData, double> Value { get; }
    public Func<PlayerTracker.PlayerData, double> TieBreak { get; }
    public StatFormat Format { get; }
    public string LeastRequirement { get; }
    public string LeastLabel { get; }

    // Time and height stats come from floats, so they are formatted as floats again
    public string Display(double value) => Format switch
    {
        StatFormat.Meters => $"{(float)value:F1}m",
        StatFormat.Seconds => $"{(float)value:F1}s",
        _ => ((long)value).ToString(),
    };

    private static int WeaponKills(PlayerTracker.PlayerData data, string[] weapons) => weapons.Sum(weapon => data.WeaponHits[weapon]);
}

public class TitlesManager
{
    private static readonly Lazy<TitlesManager> _lazy = new Lazy<TitlesManager>(() => new TitlesManager());
    public static TitlesManager Instance => _lazy.Value;
    private const int MaxDisplayedTitles = 8;

    private List<TitleEntry> currentTitles = new List<TitleEntry>();
    private List<TitleEntry> allTitles = new List<TitleEntry>();
    private readonly Dictionary<int, RenderTexture> portraits = new Dictionary<int, RenderTexture>();

    public List<TitleEntry> CurrentTitles => currentTitles.ToList();
    public List<TitleEntry> AllTitles => allTitles.ToList();
    public bool HasGameEndedTitles => currentTitles.Count > 0;
    public int TitleCount => currentTitles.Count;
    public event Action OnTitlesUpdated;

    // Goes up every time a run's titles are worked out, so a reader can tell whether they belong to the run it saw end
    public int CalculationCount { get; private set; }
    public bool LastTitlesShown { get; private set; }
    public int? LastNewlyUnlocked { get; private set; }

    public void SetRunOutcome(bool shown, int? newlyUnlocked)
    {
        LastTitlesShown = shown;
        LastNewlyUnlocked = newlyUnlocked;
    }

    public void CalculateAndStoreTitles(GameStatsSnapshot snapshot)
    {
        CalculationCount++;
        LastTitlesShown = false;
        LastNewlyUnlocked = null;
        ReleasePortraits();
        currentTitles.Clear();
        allTitles.Clear();

        if (snapshot?.ActivePlayers == null || snapshot.GameMode != GameMode.Survival || snapshot.ActivePlayers.Count <= 1)
        {
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
                    TitleName = TitleCatalog.AverageJoe,
                    Description = "Participated",
                    Priority = 100
                });
            }
        }
        BalanceTitlePriorities();

        allTitles = currentTitles.OrderByDescending(t => t.Priority).ToList();

        currentTitles = SelectTopAndShuffleTitles(currentTitles, MaxDisplayedTitles);
        CapturePortraits();

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

        foreach (var stat in StatDef.All)
        {
            var ordered = players.OrderByDescending(p => stat.Value(p.Value));
            var ranked = (stat.TieBreak == null ? ordered : ordered.ThenByDescending(p => stat.TieBreak(p.Value))).ToList();
            leaders[stat.Requirement] = ranked[0];
            if (stat.LeastRequirement != null) leaders[stat.LeastRequirement] = ranked[ranked.Count - 1];
        }

        leaders[Req.BestAccuracy] = RankByAccuracy(players, p => p.ShootingAccuracy, MinAccuracyShots)[0];
        leaders[Req.BestRailvolverAccuracy] = RankByAccuracy(players, RailvolverAccuracy, MinRailvolverShots)[0];
        leaders[Req.WorstAccuracy] = RankByAccuracy(players, p => p.ShootingAccuracy, MinAccuracyShots, lowestFirst: true)[0];

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

    private void CapturePortraits()
    {
        foreach (TitleEntry title in currentTitles)
        {
            if (!portraits.TryGetValue(title.PlayerId, out RenderTexture portrait))
            {
                portrait = SpiderPortraits.CopyOf(title.Player);
                portraits[title.PlayerId] = portrait;
            }
            title.Portrait = portrait;
        }
    }

    internal void ReleasePortraits()
    {
        foreach (RenderTexture portrait in portraits.Values) SpiderPortraits.DisposeTexture(portrait);
        portraits.Clear();
        foreach (TitleEntry title in currentTitles) title.Portrait = null;
        foreach (TitleEntry title in allTitles) title.Portrait = null;
    }

    public void ClearTitles()
    {
        SetRunOutcome(false, null);
        ReleasePortraits();
        currentTitles.Clear();
        allTitles.Clear();
        OnTitlesUpdated?.Invoke();
    }

    public void SetRemoteTitles(List<TitleEntry> current, List<TitleEntry> all)
    {
        SetRunOutcome(false, null);
        ReleasePortraits();
        currentTitles = current ?? new List<TitleEntry>();
        allTitles = all ?? new List<TitleEntry>();
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

    // Titles from other parts, kept only when the leader also meets their native requirements
    private void AddExternalTitles(List<TitleEntry> titles, Dictionary<string, StatCondition> conditions)
    {
        var externalTitles = RunReportSources.Titles();
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
            Logger.LogWarning($"ValidateExternalRequirements: Failed to resolve player for title '{title.TitleName}'. A title with only custom requirements needs a leader.");
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

        void Add(string req, KeyValuePair<PlayerController, PlayerTracker.PlayerData> leader, bool hasStat, string value)
        {
            conditions[req] = new StatCondition(req, leader, hasStat, $"{TitleCatalog.Label(req)} ({value})");
        }

        foreach (var stat in StatDef.All)
        {
            var most = leaders[stat.Requirement];
            double mostValue = stat.Value(most.Value);
            Add(stat.Requirement, most, mostValue > 0, stat.Display(mostValue));
            if (stat.LeastRequirement != null)
            {
                var least = leaders[stat.LeastRequirement];
                Add(stat.LeastRequirement, least, true, stat.Display(stat.Value(least.Value)));
            }
        }

        var bestAccuracy = leaders[Req.BestAccuracy];
        var accuracy = bestAccuracy.Value.ShootingAccuracy;
        Add(Req.BestAccuracy, bestAccuracy, HasAccuracy(accuracy, MinAccuracyShots), accuracy.Display);

        var bestRailvolver = leaders[Req.BestRailvolverAccuracy];
        var railvolverAccuracy = RailvolverAccuracy(bestRailvolver.Value);
        Add(Req.BestRailvolverAccuracy, bestRailvolver, HasAccuracy(railvolverAccuracy, MinRailvolverShots), railvolverAccuracy.Display);

        var worst = leaders[Req.WorstAccuracy];
        var worstAccuracy = worst.Value.ShootingAccuracy;
        bool hasWorstAccuracy = worstAccuracy.ShotsFired >= MinAccuracyShots && worstAccuracy.Percentage < accuracy.Percentage;
        Add(Req.WorstAccuracy, worst, hasWorstAccuracy, worstAccuracy.Display);

        return conditions;
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
        foreach (var title in TitleCatalog.Native)
        {
            if (title.Requirements.Length == 0 || (title.Condition != null && !title.Condition(leaders))) continue;
            TryAddTitle(titles, leaders, conditions, title.Name, title.BonusPriority, title.Requirements);
        }
    }
}
