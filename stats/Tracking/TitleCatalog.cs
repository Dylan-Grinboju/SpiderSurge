using System;
using System.Collections.Generic;
using System.Linq;

namespace StatsMod;

public sealed class TitleDefinition
{
    public TitleDefinition(string name, string[] requirements, int bonusPriority = 0, Func<StatLeaders, bool> condition = null, string conditionText = null)
    {
        Name = name;
        Requirements = requirements;
        BonusPriority = bonusPriority;
        Condition = condition;
        HowToEarn = string.Join(" + ", requirements.Select(TitleCatalog.HowToEarnLabel).Concat(conditionText != null ? new[] { conditionText } : Array.Empty<string>()));
    }

    public TitleDefinition(string name, string howToEarn)
    {
        Name = name;
        Requirements = Array.Empty<string>();
        HowToEarn = howToEarn;
    }

    public string Name { get; }
    public string[] Requirements { get; }
    public int BonusPriority { get; }
    public Func<StatLeaders, bool> Condition { get; }
    public string HowToEarn { get; }
}

public static class TitleCatalog
{
    public const string AverageJoe = "Average Joe";
    private const float HighAltitude = 1000f;

    private static readonly Dictionary<string, string> Labels = new Dictionary<string, string>
    {
        [Req.MostWebSwings] = "Most Web Swings",
        [Req.LeastWebSwings] = "Least Web Swings",
        [Req.HighestPoint] = "Highest Point",
        [Req.LowestPoint] = "Lowest Point",
        [Req.MostAirborneTime] = "Most Airborne Time",
        [Req.LeastAirborneTime] = "Least Airborne Time",
        [Req.MostKillsWhileAirborne] = "Most Kills While Airborne",
        [Req.MostKillsWhileSolo] = "Most Kills While Solo",
        [Req.MostWaveClutches] = "Most Wave Clutches",
        [Req.MaxKillStreak] = "Max Kill Streak",
        [Req.MaxKillStreakWhileSolo] = "Max Kill Streak While Solo",
        [Req.MostAliveTime] = "Most Alive Time",
        [Req.MostOffense] = "Most Offense",
        [Req.LeastOffense] = "Least Offense",
        [Req.MostDamageTaken] = "Most Damage Taken",
        [Req.LeastDamageTaken] = "Least Damage Taken",
        [Req.MostFriendlyFire] = "Most Friendly Fire",
        [Req.LeastFriendlyFire] = "Least Friendly Fire",
        [Req.MostShieldsLost] = "Most Shields Lost",
        [Req.LeastShieldsLost] = "Least Shields Lost",
        [Req.MostDeaths] = "Most Deaths",
        [Req.LeastDeaths] = "Least Deaths",
        [Req.MostLavaDeaths] = "Most Lava Deaths",
        [Req.LeastLavaDeaths] = "Least Lava Deaths",
        [Req.MostGunsKills] = "Most Gun Kills",
        [Req.MostExplosionsKills] = "Most Explosive Kills",
        [Req.MostBladeKills] = "Most Blade Kills",
        [Req.MostHornetKills] = "Most Hornets Killed",
        [Req.MostWhispKills] = "Most Whisps Killed",
        [Req.MostKhepriKills] = "Most Khepris Killed",
        [Req.MostAstralReturns] = "Most Astral Returns",
        [Req.BestAccuracy] = "Best Accuracy",
        [Req.BestRailvolverAccuracy] = "Best Railvolver Accuracy",
        [Req.WorstAccuracy] = "Worst Accuracy",
        [Req.MostShotsFired] = "Most Shots Fired",
    };

    public static string Label(string requirement) => Labels.TryGetValue(requirement, out string label) ? label : requirement;

    internal static string HowToEarnLabel(string requirement) => requirement switch
    {
        Req.BestAccuracy => "Best Accuracy (at least 10 shots and 1 hit)",
        Req.BestRailvolverAccuracy => "Best Railvolver Accuracy (at least 5 combined Railvolver or Double Railvolver shots and 1 hit)",
        Req.WorstAccuracy => "Worst Accuracy (at least 10 shots, below the best player's accuracy)",
        _ => Label(requirement),
    };

    public static bool IsHighAltitude(float highestPoint) => highestPoint >= HighAltitude;

    // In the order they are checked; the end-of-run screen picks among the earned ones by priority, not by this order
    public static readonly TitleDefinition[] Native =
    {
        Title("Peter Parker", Req.MostWebSwings),
        new TitleDefinition("Sky Scraper", new[] { Req.HighestPoint }, 0, l => !IsHighAltitude(l.HighestPoint.Value.HighestPoint)),
        new TitleDefinition("1000 Meters Club", new[] { Req.HighestPoint }, 40, l => IsHighAltitude(l.HighestPoint.Value.HighestPoint), "Reach 1000m"),
        Title("Air Dancer", Req.MostAirborneTime),
        Title("Sky Hunter", Req.MostKillsWhileAirborne),
        Title("Lone Wolf", Req.MostKillsWhileSolo),
        Title("Clutch Master", Req.MostWaveClutches),
        Title("Serial Killer", Req.MaxKillStreak),
        Title("Solo Rampage", Req.MaxKillStreakWhileSolo),
        Title("Survivor", Req.MostAliveTime),
        Title("Destroyer", Req.MostOffense),
        Title("Punching Bag", Req.MostDamageTaken),
        Title("Shadow", Req.LeastDamageTaken),
        Title("Confused", Req.MostFriendlyFire),
        Title("Team Player", Req.LeastFriendlyFire),
        Title("Pacifist", Req.LeastOffense),
        Title("Gunslinger", Req.MostGunsKills),
        Title("Demolitionist", Req.MostExplosionsKills),
        Title("Blade Master", Req.MostBladeKills),
        Title("Slippery", Req.MostLavaDeaths),
        Title("Floor is Lava", Req.LeastLavaDeaths),
        Title("Resurrection", Req.MostAstralReturns),
        Title("Bullseye", Req.BestAccuracy),
        Title("Sniper", Req.BestRailvolverAccuracy),
        Title("Stormtrooper", Req.WorstAccuracy),

        Title("Orbital Strike", Req.MostOffense, Req.HighestPoint),
        Title("Satellite", Req.MostAirborneTime, Req.HighestPoint),
        Title("Glass Cannon", Req.MostOffense, Req.MostDamageTaken),
        Title("Sword and Shield", Req.MostOffense, Req.LeastDamageTaken),
        Title("Nothing Burger", Req.LeastOffense, Req.LeastFriendlyFire),
        Title("On Death's Bed", Req.MostShieldsLost, Req.LeastDeaths),
        Title("Perfectly Balanced", Req.MostOffense, Req.MostFriendlyFire),
        Title("Spider-Man", Req.MostWebSwings, Req.MostAirborneTime),
        Title("Lawn-mower", Req.MostOffense, Req.LowestPoint),
        Title("Hit & Run", Req.MostOffense, Req.MostWebSwings),
        Title("Last Stand Hero", Req.MostWaveClutches, Req.MostKillsWhileSolo),
        Title("Kamikaze", Req.MostExplosionsKills, Req.MostDamageTaken),
        Title("War Machine", Req.MostGunsKills, Req.MostOffense),
        Title("Silent Assassin", Req.MostBladeKills, Req.LeastDamageTaken),
        Title("I have the High Ground", Req.MostBladeKills, Req.HighestPoint),
        Title("Nine Lives", Req.MostDamageTaken, Req.MostAliveTime),
        Title("Jedi Master", Req.MostHornetKills, Req.MostBladeKills),
        Title("Sharpshooter", Req.MostWhispKills, Req.MostGunsKills),
        Title("Pharaoh", Req.MostKhepriKills, Req.MostDamageTaken),
        Title("Cockroach", Req.MostAliveTime, Req.LowestPoint),
        Title("Icarus", Req.MostLavaDeaths, Req.HighestPoint),
        Title("Firewalker", Req.LeastLavaDeaths, Req.MostAliveTime),
        Title("Gravity Police", Req.MostKillsWhileAirborne, Req.LowestPoint),
        Title("Slow and Steady", Req.LeastWebSwings, Req.LeastDamageTaken),
        Title("Overkill", Req.MaxKillStreak, Req.MostOffense),
        Title("Insurance Policy", Req.MostShieldsLost, Req.MostAliveTime),
        Title("Second Chances", Req.MostAstralReturns, Req.MostDeaths),
        Title("Guns Blazing", Req.MaxKillStreakWhileSolo, Req.MostGunsKills),
        Title("Came to Finish the Job", Req.MaxKillStreakWhileSolo, Req.MostAstralReturns),
        Title("Defying Gravity", Req.LeastLavaDeaths, Req.MostAirborneTime),
        Title("Phoenix", Req.LeastLavaDeaths, Req.MostAstralReturns),
        Title("Death from Above", Req.BestAccuracy, Req.HighestPoint),
        Title("Non-Lethal Rounds", Req.BestAccuracy, Req.LeastOffense),
        Title("Aimbot", Req.BestAccuracy, Req.MostOffense),
        Title("Steady", Req.BestAccuracy, Req.LeastAirborneTime),
        Title("Spray and Pray", Req.WorstAccuracy, Req.MostShotsFired),

        Title("ICBM", Req.MostExplosionsKills, Req.HighestPoint, Req.MostAirborneTime),
        Title("Phantom Blade", Req.MostBladeKills, Req.HighestPoint, Req.LeastDamageTaken),
        Title("Elegant Barbarian", Req.MostOffense, Req.MostDamageTaken, Req.LeastFriendlyFire),
        Title("MVP", Req.MostOffense, Req.LeastDamageTaken, Req.LeastFriendlyFire),
        Title("Agent of Chaos", Req.MostOffense, Req.LeastDamageTaken, Req.MostFriendlyFire),
        Title("Ordered Chaos", Req.MostFriendlyFire, Req.MaxKillStreakWhileSolo, Req.MostWaveClutches),
        Title("Traitor", Req.LeastOffense, Req.LeastDamageTaken, Req.MostFriendlyFire),
        Title("AFK", Req.MostDamageTaken, Req.LeastOffense, Req.LeastFriendlyFire),
        Title("Spooderman", Req.MostWebSwings, Req.MostAirborneTime, Req.MostDamageTaken),
        Title("Basement Dweller", Req.LowestPoint, Req.LeastAirborneTime, Req.LeastWebSwings),
        Title("Marksman", Req.MostGunsKills, Req.MostOffense, Req.LeastFriendlyFire),
        Title("Mutually Assured Destruction", Req.MostExplosionsKills, Req.MostOffense, Req.MostFriendlyFire),
        Title("Master of Arms", Req.MostExplosionsKills, Req.MostGunsKills, Req.MostBladeKills),
        Title("Sith Lord", Req.MostHornetKills, Req.MostBladeKills, Req.MostFriendlyFire),
        Title("One Man Army", Req.MaxKillStreakWhileSolo, Req.MostKillsWhileSolo, Req.MostWaveClutches),
        Title("Iron Spider", Req.MostWebSwings, Req.MostAirborneTime, Req.MostBladeKills),
        Title("Perseverance", Req.MostAstralReturns, Req.MostShieldsLost, Req.MostAliveTime),
        Title("A Gust of Wind", Req.MostAstralReturns, Req.LeastOffense, Req.LeastFriendlyFire),
        Title("Anubis", Req.MostAstralReturns, Req.MostKhepriKills, Req.MostDamageTaken),
        Title("Surgical Strike", Req.BestAccuracy, Req.MostOffense, Req.LeastFriendlyFire),
        Title("Camper", Req.BestAccuracy, Req.LeastAirborneTime, Req.LeastWebSwings),
        Title("Bullet Time", Req.BestAccuracy, Req.MostAirborneTime, Req.MostKillsWhileAirborne),
        Title("High Noon", Req.BestAccuracy, Req.MostGunsKills, Req.MostKillsWhileSolo),
        Title("Deadeye", Req.BestAccuracy, Req.MostDamageTaken, Req.MostDeaths),
        Title("Trigger Happy", Req.WorstAccuracy, Req.MostShotsFired, Req.MostFriendlyFire),
        Title("Blind Rage", Req.WorstAccuracy, Req.MostOffense, Req.MaxKillStreak),

        Title("God Complex", Req.MostOffense, Req.LeastDamageTaken, Req.HighestPoint, Req.LeastFriendlyFire),
        Title("The Untouchable", Req.HighestPoint, Req.MostAirborneTime, Req.LeastDamageTaken, Req.MostWebSwings),
        Title("Nuclear Warhead", Req.MostOffense, Req.HighestPoint, Req.MostAirborneTime, Req.MostExplosionsKills),
        Title("Inside Job", Req.LeastOffense, Req.LeastDamageTaken, Req.MostFriendlyFire, Req.MostExplosionsKills),
        Title("Rambo", Req.MostOffense, Req.LeastDamageTaken, Req.LeastFriendlyFire, Req.MostGunsKills),
        Title("Darth Vader", Req.MostHornetKills, Req.MostBladeKills, Req.MostFriendlyFire, Req.MostLavaDeaths),
        Title("Bunker", Req.LowestPoint, Req.LeastAirborneTime, Req.LeastWebSwings, Req.MostAliveTime),
        Title("Air Superiority", Req.HighestPoint, Req.MostAirborneTime, Req.MostKillsWhileAirborne, Req.MostWebSwings),
        Title("Spectral Blade", Req.MostAstralReturns, Req.MostBladeKills, Req.HighestPoint, Req.LeastDamageTaken),
        Title("Turret", Req.BestAccuracy, Req.LeastWebSwings, Req.LeastAirborneTime, Req.LowestPoint),

        new TitleDefinition(AverageJoe, "Earn no other title in a run"),
    };

    // Native titles first, then the ones other parts and mods described through StatsModApi
    public static List<TitleDefinition> All
    {
        get
        {
            var all = Native.ToList();
            foreach (var described in StatsModApi.GetDescribedTitles())
            {
                if (!all.Any(title => title.Name == described.Name)) all.Add(described);
            }
            return all;
        }
    }

    private static TitleDefinition Title(string name, params string[] requirements) => new TitleDefinition(name, requirements);
}
