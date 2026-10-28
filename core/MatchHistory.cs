using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using StatsMod;
using SpiderSurge.Logging;
using UnityEngine;

namespace SpiderSurge.Common
{
    public sealed class HistorySummary
    {
        public string Id;
        public string DateUtc;
        public string Version;
        public string Mode;
        public string Outcome;
        public bool Surge;
        public double Seconds;
        public int Wave;
        public int Pain;
        public string Players;
    }
    public sealed class HistoryValue
    {
        public string Name;
        public double Value;
        public HistoryValue() { }
        public HistoryValue(string name, double value) { Name = name; Value = value; }
    }
    public sealed class HistoryAccuracy
    {
        public string Weapon;
        public int Fired;
        public int Hit;
    }
    public sealed class HistoryPlayer
    {
        public int Id;
        public string Name;
        public string Color;
        public int Team = -1;
        public int Score;
        public bool LeftEarly;
        public List<HistoryValue> Stats = new List<HistoryValue>();
        public List<HistoryValue> Enemies = new List<HistoryValue>();
        public List<HistoryValue> Weapons = new List<HistoryValue>();
        public List<HistoryValue> DeathsPerMap = new List<HistoryValue>();
        public List<HistoryAccuracy> Accuracy = new List<HistoryAccuracy>();
    }
    public sealed class HistoryTitle
    {
        public string Name;
        public string Description;
        public int Player;
        public bool Shown;
        public bool New;
        public long PendingUnlockTicks;
    }
    public sealed class HistoryTeam
    {
        public int Id;
        public string Name;
        public int Score;
    }
    public sealed class HistoryMatch
    {
        public int Schema = 1;
        public HistorySummary Summary;
        public List<HistoryPlayer> Players = new List<HistoryPlayer>();
        public List<HistoryTeam> Teams = new List<HistoryTeam>();
        public List<string> Maps = new List<string>();
        public List<string> Perks = new List<string>();
        public List<HistoryTitle> Titles = new List<HistoryTitle>();
        public SpiderSurgeStatsSnapshot SurgeStats;
        public int EnemiesKilled;
        public RunRecapData Recap;
    }

    internal static class MatchHistory
    {
        private static string id;
        private static DateTime started;
        private static bool surgeVersus;
        private static bool completedSurvival;
        private static readonly List<HistoryPlayer> departed = new List<HistoryPlayer>();
        private static readonly PropertyInfo[] Numbers = typeof(PlayerTracker.PlayerData).GetProperties()
            .Where(p => p.PropertyType == typeof(int) || p.PropertyType == typeof(float))
            .Where(p => p.Name != "PlayerId" && p.Name != "JoinTime" && p.Name != "TotalAliveTime" && p.Name != "WebSwingTime" && p.Name != "AirborneTime").ToArray();
        public static bool Enabled => Parts.Stats.Running && Settings.GetBool("stats.tracking.saveHistory");

        public static void Begin()
        {
            id = Guid.NewGuid().ToString("N");
            started = DateTime.UtcNow;
            surgeVersus = false;
            completedSurvival = false;
            departed.Clear();
        }
        public static void MarkSurgeVersus() => surgeVersus = true;
        public static void MarkSurvivalCompletion(SurvivalMode mode)
        {
            var config = HarmonyLib.AccessTools.Field(typeof(SurvivalMode), "_config").GetValue(mode) as SurvivalConfig;
            if (config != null && config.totalWaves > 0 && mode.CurrentWave == config.totalWaves) completedSurvival = true;
        }
        public static void PlayerLeaving(PlayerController player, PlayerTracker.PlayerData data)
        {
            if (id == null || !Enabled) return;
            try
            {
                var saved = CapturePlayer(player, data, GameSessionManager.Instance.CurrentGameMode == StatsMod.GameMode.Survival);
                saved.LeftEarly = true;
                departed.Add(saved);
            }
            catch (Exception ex) { HistoryStore.Report("Could not record departing player: " + ex.Message); }
        }

        public static void Complete(GameStatsSnapshot snapshot, bool restarted)
        {
            if (id == null) return;
            string finishedId = id;
            id = null;
            if (!Enabled) return;
            try
            {
                bool survival = snapshot.GameMode == StatsMod.GameMode.Survival;
                var surge = survival ? SpiderSurgeStatsManager.Instance?.LastFinished : null;
                var summary = new HistorySummary
                {
                    Id = finishedId, DateUtc = started.ToString("O", CultureInfo.InvariantCulture), Version = SpiderSurgeMod.Version,
                    Mode = survival ? "survival" : "versus", Surge = survival ? SurgeGameModeManager.IsSurgeRunActive : surgeVersus,
                    Seconds = snapshot.LastGameDuration.TotalSeconds, Pain = snapshot.PainLevel,
                    Wave = survival && SurvivalMode.instance != null ? SurvivalMode.instance.CurrentWave : 0,
                    Outcome = restarted ? "Restarted" : survival ? (completedSurvival ? "Completed" : SurvivalMode.instance != null && SurvivalMode.instance.Lives <= 0 ? "Lost" : "Quit") : "Ended",
                    Players = string.Join(", ", snapshot.ActivePlayers.Values.Select(p => p.PlayerName))
                };
                var match = new HistoryMatch { Recap = RunRecap.Last, Summary = summary, Maps = snapshot.MapsPlayed.ToList(), Perks = snapshot.PerksChosen.ToList(), SurgeStats = surge, EnemiesKilled = snapshot.EnemiesKilled };
                bool teams = !survival && SpiderTeams.TeamManager.TeamMatchActive;
                var manager = SpiderTeams.TeamManager.Instance;
                match.Players.AddRange(departed);
                foreach (var pair in snapshot.ActivePlayers) match.Players.Add(CapturePlayer(pair.Key, pair.Value, survival));
                match.Players = match.Players.OrderBy(p => p.Id).ThenBy(p => p.LeftEarly ? 0 : 1).ToList();
                summary.Players = string.Join(", ", match.Players.Select(p => p.Name));
                if (teams && manager != null)
                {
                    foreach (int team in match.Players.Select(p => p.Team).Distinct())
                        match.Teams.Add(new HistoryTeam { Id = team, Name = SpiderTeams.TeamPalette.TeamName(team), Score = manager.GetTeamScore(team) });
                }
                if (!survival && !restarted)
                {
                    int highest = teams ? match.Teams.Select(t => t.Score).DefaultIfEmpty().Max() : match.Players.Select(p => p.Score).DefaultIfEmpty().Max();
                    summary.Outcome = highest >= GameSettings.Instance.VersusWinningScore() ? "Completed" : "Quit";
                }
                var shown = new HashSet<TitleEntry>(TitlesManager.Instance.CurrentTitles);
                foreach (var title in snapshot.Titles)
                {
                    bool displayed = TitlesManager.Instance.LastTitlesShown && shown.Contains(title);
                    match.Titles.Add(new HistoryTitle { Name = title.TitleName, Description = title.Description, Player = title.PlayerId,
                        Shown = displayed, New = title.IsNew,
                        PendingUnlockTicks = displayed && !TitlesManager.Instance.LastNewlyUnlocked.HasValue ? title.EarnedUtc.Ticks : 0 });
                }
                HistoryStore.Save(match);
            }
            catch (Exception ex) { HistoryStore.Report("Could not record this match: " + ex.Message); }
        }
        private static HistoryPlayer CapturePlayer(PlayerController player, PlayerTracker.PlayerData p, bool survival)
        {
            var saved = new HistoryPlayer { Id = p.PlayerId, Name = p.PlayerName, Color = ColorUtility.ToHtmlStringRGBA(p.PlayerColor) };
            foreach (var number in Numbers) saved.Stats.Add(new HistoryValue(number.Name, Convert.ToDouble(number.GetValue(p), CultureInfo.InvariantCulture)));
            saved.Stats.Add(new HistoryValue("Alive seconds", p.GetCurrentAliveTime().TotalSeconds));
            saved.Stats.Add(new HistoryValue("Swinging seconds", p.GetCurrentWebSwingTime().TotalSeconds));
            saved.Stats.Add(new HistoryValue("Airborne seconds", p.GetCurrentAirborneTime().TotalSeconds));
            saved.Enemies = Values(p.EnemyKills);
            saved.Weapons = Values(p.WeaponHits);
            saved.DeathsPerMap = Values(p.DeathsPerMap);
            saved.Accuracy.Add(Accuracy("Overall", p.ShootingAccuracy));
            saved.Accuracy.AddRange(p.AccuracyByWeapon.Select(a => Accuracy(a.Key, a.Value)));
            if (!survival) saved.Score = ScoreKeeper.GetScores()?.FirstOrDefault(score => score.player == player)?.score ?? 0;
            if (!survival && SpiderTeams.TeamManager.TeamMatchActive && SpiderTeams.TeamManager.Instance != null)
                saved.Team = SpiderTeams.TeamManager.Instance.GetTeam(player);
            return saved;
        }
        private static List<HistoryValue> Values(Dictionary<string, int> values) => values == null ? new List<HistoryValue>() : values.Select(v => new HistoryValue(v.Key, v.Value)).ToList();
        private static HistoryAccuracy Accuracy(string weapon, WeaponAccuracy value) => new HistoryAccuracy { Weapon = weapon, Fired = value.ShotsFired, Hit = value.ShotsHit };
    }
}
