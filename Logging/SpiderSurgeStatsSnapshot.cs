using System;
using System.Collections.Generic;

namespace SpiderSurge.Logging;

public class SpiderSurgeStatsSnapshot
{
    public TimeSpan MatchDuration { get; set; }
    public int PlayerCount { get; set; }
    public int WavesSurvived { get; set; }
    public int PainLevel { get; set; }
    public List<PlayerStats> PlayerStats { get; set; } = [];
    public List<string> GlobalPerks { get; set; } = [];
}

public class PlayerStats
{
    public int PlayerIndex { get; set; }
    public int AbilityActivationCount { get; set; }
    public int UltimateActivationCount { get; set; }
}
