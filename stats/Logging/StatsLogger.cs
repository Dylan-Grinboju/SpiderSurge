using Logger = Silk.Logger;
using System.Collections.Generic;
using System;
using System.IO;
using System.Linq;
using System.Text;
namespace StatsMod
{
    // Handles logging game statistics to files.
    public class StatsLogger
    {
        private static readonly Lazy<StatsLogger> _lazy = new Lazy<StatsLogger>(() => new StatsLogger());
        public static StatsLogger Instance => _lazy.Value;

        private readonly string logDirectory;

        private StatsLogger()
        {
            try
            {
                string gameDirectory = Path.GetDirectoryName(UnityEngine.Application.dataPath);
                if (string.IsNullOrEmpty(gameDirectory))
                    gameDirectory = Environment.CurrentDirectory;

                logDirectory = Path.Combine(gameDirectory, "Silk", "Logs", "SpiderStats");

                if (!Directory.Exists(logDirectory))
                {
                    Directory.CreateDirectory(logDirectory);
                    Logger.LogInfo($"Created stats log directory: {logDirectory}");
                }
            }
            catch (Exception ex)
            {
                logDirectory = Environment.CurrentDirectory;
                Logger.LogError($"Failed to create Silk/Logs/SpiderStats directory, using current directory: {ex.Message}");
            }

            Logger.LogInfo($"Stats logger initialized. Log directory: {logDirectory}");
        }
        public void LogGameStats(GameStatsSnapshot statsSnapshot)
        {
            try
            {
                Logger.LogInfo($"Saving stats for {statsSnapshot.GameMode} game, Duration: {FormatTimeSpan(statsSnapshot.LastGameDuration)}");
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
                string fileName = $"Spiderheck_stats_{timestamp}.txt";
                string filePath = Path.Combine(logDirectory, fileName);

                string formattedStats = FormatGameStats(statsSnapshot);

                File.WriteAllText(filePath, formattedStats);

                Logger.LogInfo($"Game stats logged to: {fileName}");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to log game stats: {ex.Message}");
            }
        }

        private string FormatGameStats(GameStatsSnapshot statsSnapshot)
        {
            string modeHeader = statsSnapshot.GameMode == GameMode.Versus ? "VERSUS MODE" : "SURVIVAL MODE";

            var sb = new StringBuilder(2048);
            sb.AppendLine("=".PadRight(60, '='));
            sb.AppendLine($"SPIDERHECK {modeHeader} STATISTICS");
            sb.AppendLine("=".PadRight(60, '='));
            sb.AppendLine("");
            sb.AppendLine("GAME INFORMATION:");
            sb.AppendLine($"  Game End Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"  Game Duration: {FormatTimeSpan(statsSnapshot.LastGameDuration)}");

            if (statsSnapshot.PainLevel >= 1)
            {
                sb.AppendLine($"  Pain Level: {statsSnapshot.PainLevel}");
            }

            sb.AppendLine("");
            sb.AppendLine("ENEMY STATISTICS:");
            sb.AppendLine($"  Total Enemies Killed: {statsSnapshot.EnemiesKilled}");
            sb.AppendLine("");
            sb.AppendLine("PLAYER STATISTICS:");
            if (statsSnapshot.ActivePlayers != null && statsSnapshot.ActivePlayers.Any())
            {
                var sortedPlayers = statsSnapshot.ActivePlayers
                    .OrderByDescending(p => p.Value.Kills)
                    .ThenBy(p => p.Value.Deaths)
                    .ToList();

                int totalKills = statsSnapshot.ActivePlayers.Sum(p => p.Value.Kills);
                int totalDeaths = statsSnapshot.ActivePlayers.Sum(p => p.Value.Deaths);
                int totalFriendlyKills = statsSnapshot.ActivePlayers.Sum(p => p.Value.FriendlyKills);
                int totalEnemyShieldsTakenDown = statsSnapshot.ActivePlayers.Sum(p => p.Value.EnemyShieldsTakenDown);
                int totalFriendlyShieldsHit = statsSnapshot.ActivePlayers.Sum(p => p.Value.FriendlyShieldsHit);
                int totalShieldsLost = statsSnapshot.ActivePlayers.Sum(p => p.Value.ShieldsLost);

                sb.AppendLine($"  Total Players: {statsSnapshot.ActivePlayers.Count}");
                sb.AppendLine($"  Total Player Kills: {totalKills}");
                sb.AppendLine($"  Total Friendly Kills (PvP): {totalFriendlyKills}");
                sb.AppendLine($"  Total Player Deaths: {totalDeaths}");
                sb.AppendLine($"  Total Enemy Shields Taken Down: {totalEnemyShieldsTakenDown}");
                sb.AppendLine($"  Total Friendly Shields Hit: {totalFriendlyShieldsHit}");
                sb.AppendLine($"  Total Shields Lost: {totalShieldsLost}");
                sb.AppendLine("");

                sb.AppendLine("  Individual Player Performance:");
                sb.AppendLine("  " + "-".PadRight(50, '-'));

                foreach (var playerEntry in sortedPlayers)
                {
                    var playerData = playerEntry.Value;

                    sb.AppendLine($"  {playerData.PlayerName}:");
                    sb.AppendLine($"    Player ID: {playerData.PlayerId}");
                    sb.AppendLine($"    Color: #{(int)(playerData.PlayerColor.r * 255):X2}{(int)(playerData.PlayerColor.g * 255):X2}{(int)(playerData.PlayerColor.b * 255):X2}");
                    sb.AppendLine($"    Kills: {playerData.Kills}");
                    sb.AppendLine($"    Kills While Airborne: {playerData.KillsWhileAirborne}");
                    sb.AppendLine($"    Kills While Solo: {playerData.KillsWhileSolo}");
                    sb.AppendLine($"    Wave Clutches: {playerData.WaveClutches}");
                    sb.AppendLine($"    Max Kill Streak: {playerData.MaxKillStreak}");
                    sb.AppendLine($"    Max Solo Kill Streak: {playerData.MaxKillStreakWhileSolo}");
                    sb.AppendLine($"    Friendly Kills (PvP): {playerData.FriendlyKills}");
                    sb.AppendLine($"    Deaths: {playerData.Deaths}");
                    sb.AppendLine($"    Lava Deaths: {playerData.LavaDeaths}");
                    sb.AppendLine($"    Astral Returns: {playerData.AstralReturns}");

                    // Deaths per map breakdown
                    if (playerData.DeathsPerMap != null && playerData.DeathsPerMap.Any())
                    {
                        sb.AppendLine($"    Deaths Per Map:");
                        foreach (var mapEntry in playerData.DeathsPerMap.OrderByDescending(m => m.Value))
                        {
                            sb.AppendLine($"      {mapEntry.Key}: {mapEntry.Value}");
                        }
                    }

                    sb.AppendLine($"    Enemy Shields Taken Down: {playerData.EnemyShieldsTakenDown}");
                    sb.AppendLine($"    Friendly Shields Hit: {playerData.FriendlyShieldsHit}");
                    sb.AppendLine($"    Shields Lost: {playerData.ShieldsLost}");
                    sb.AppendLine($"    Alive Time: {FormatTimeSpan(playerData.GetCurrentAliveTime())}");
                    sb.AppendLine($"    Web Swings: {playerData.WebSwings}");
                    sb.AppendLine($"    Time Swinging: {FormatTimeSpan(playerData.GetCurrentWebSwingTime())}");
                    sb.AppendLine($"    Time Airborne: {FormatTimeSpan(playerData.GetCurrentAirborneTime())}");
                    sb.AppendLine($"    Highest Point: {playerData.HighestPoint:F1}m");

                    // Weapon hits breakdown
                    if (playerData.WeaponHits != null && playerData.WeaponHits.Any())
                    {
                        sb.AppendLine($"    Weapon Hits (Kills + Shield Hits):");
                        var sortedWeaponHits = playerData.WeaponHits
                            .Where(w => w.Value > 0)
                            .OrderByDescending(w => w.Value)
                            .ToList();

                        if (sortedWeaponHits.Any())
                        {
                            foreach (var weapon in sortedWeaponHits)
                            {
                                sb.AppendLine($"      {weapon.Key}: {weapon.Value}");
                            }
                        }
                        else
                        {
                            sb.AppendLine($"      No weapon hits recorded");
                        }
                    }

                    // Enemy kills breakdown
                    if (playerData.EnemyKills != null && playerData.EnemyKills.Any())
                    {
                        sb.AppendLine($"    Kills by Enemy Type:");
                        var sortedEnemyKills = playerData.EnemyKills
                            .Where(e => e.Value > 0)
                            .OrderByDescending(e => e.Value)
                            .ToList();

                        if (sortedEnemyKills.Any())
                        {
                            foreach (var enemy in sortedEnemyKills)
                            {
                                sb.AppendLine($"      {enemy.Key}: {enemy.Value}");
                            }
                        }
                        else
                        {
                            sb.AppendLine($"      No enemy kills recorded");
                        }
                    }

                    sb.AppendLine("");
                }
            }
            else
            {
                sb.AppendLine("  No player data available");
                sb.AppendLine("");
            }

            // Add titles section
            if (statsSnapshot.Titles != null && statsSnapshot.Titles.Count > 0)
            {
                sb.AppendLine("TITLES AWARDED:");
                foreach (var title in statsSnapshot.Titles)
                {
                    sb.AppendLine($"  {title.TitleName}: {title.PlayerName}");
                }
                sb.AppendLine("");
            }

            // Add maps played section with aggregated deaths per map
            if (statsSnapshot.MapsPlayed != null && statsSnapshot.MapsPlayed.Any())
            {
                // Aggregate deaths across all players per map
                var totalDeathsPerMap = new Dictionary<string, int>();
                if (statsSnapshot.ActivePlayers != null)
                {
                    foreach (var player in statsSnapshot.ActivePlayers)
                    {
                        if (player.Value.DeathsPerMap != null)
                        {
                            foreach (var mapEntry in player.Value.DeathsPerMap)
                            {
                                if (totalDeathsPerMap.ContainsKey(mapEntry.Key))
                                    totalDeathsPerMap[mapEntry.Key] += mapEntry.Value;
                                else
                                    totalDeathsPerMap[mapEntry.Key] = mapEntry.Value;
                            }
                        }
                    }
                }

                sb.AppendLine("MAPS PLAYED:");
                foreach (var map in statsSnapshot.MapsPlayed.Distinct())
                {
                    if (totalDeathsPerMap.TryGetValue(map, out int deaths) && deaths > 0)
                        sb.AppendLine($"  - {map} ({deaths} {(deaths == 1 ? "death" : "deaths")})");
                    else
                        sb.AppendLine($"  - {map}");
                }
                sb.AppendLine("");
            }

            if (statsSnapshot.PerksChosen is not null && statsSnapshot.PerksChosen.Any())
            {
                sb.AppendLine("PERKS CHOSEN:");
                foreach (var perk in statsSnapshot.PerksChosen)
                {
                    sb.AppendLine($"  - {perk}");
                }
                sb.AppendLine("");
            }

            var externalStats = StatsModApi.GetExternalStats();
            if (externalStats != null && externalStats.Count > 0)
            {
                sb.AppendLine("EXTERNAL MOD STATISTICS:");
                foreach (var stat in externalStats) sb.AppendLine(stat);
                sb.AppendLine("");
            }

            sb.AppendLine("=".PadRight(60, '='));
            sb.AppendLine("End of Statistics Report");
            sb.AppendLine("=".PadRight(60, '='));

            return sb.ToString();
        }

        private string FormatTimeSpan(TimeSpan timeSpan)
        {
            if (timeSpan.TotalDays >= 1)
                return $"{timeSpan.Days}d {timeSpan.Hours:00}:{timeSpan.Minutes:00}:{timeSpan.Seconds:00}";
            else
                return $"{timeSpan.Hours:00}:{timeSpan.Minutes:00}:{timeSpan.Seconds:00}";
        }
    }
}

