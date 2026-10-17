using System;
using System.Collections.Generic;

namespace StatsMod
{
    public class GameStatsSnapshot
    {
        public bool IsSurvivalActive { get; set; }
        public bool IsVersusActive { get; set; }
        public GameMode GameMode { get; set; }
        public TimeSpan CurrentSessionTime { get; set; }
        public TimeSpan LastGameDuration { get; set; }
        public Dictionary<PlayerController, PlayerTracker.PlayerData> ActivePlayers { get; set; } = new Dictionary<PlayerController, PlayerTracker.PlayerData>();
        public int EnemiesKilled { get; set; }
        public List<TitleEntry> Titles { get; set; } = new List<TitleEntry>();
        public int PainLevel { get; set; }
        public List<string> MapsPlayed { get; set; } = new List<string>();
        public List<string> PerksChosen { get; set; } = new List<string>();
    }
}
