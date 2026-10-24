using System;
using System.IO;
using System.Linq;
using System.Text;
using Logger = Silk.Logger;
using UnityEngine;

namespace SpiderSurge.Logging
{
    public class SpiderSurgeLogger
    {
        private static readonly Lazy<SpiderSurgeLogger> _lazy = new Lazy<SpiderSurgeLogger>(() => new SpiderSurgeLogger());
        public static SpiderSurgeLogger Instance => _lazy.Value;

        private readonly string logDirectory;
        public string LogDirectory => logDirectory;

        private SpiderSurgeLogger()
        {
            try
            {
                string gameDirectory = Path.GetDirectoryName(Application.dataPath);
                if (string.IsNullOrEmpty(gameDirectory))
                    gameDirectory = Environment.CurrentDirectory;

                logDirectory = Path.Combine(gameDirectory, "Silk", "Logs", "SpiderSurge");

                if (!Directory.Exists(logDirectory))
                {
                    Directory.CreateDirectory(logDirectory);
                    Logger.LogInfo($"Created SpiderSurge log directory: {logDirectory}");
                }
            }
            catch (Exception ex)
            {
                logDirectory = Environment.CurrentDirectory;
                Logger.LogError($"Failed to create Silk/Logs/SpiderSurge directory, using current directory: {ex.Message}");
            }
        }

        public void LogMatchStats(SpiderSurgeStatsSnapshot stats)
        {
            if (stats == null)
            {
                Logger.LogError("SpiderSurge match stats are null");
                return;
            }
            try
            {
                string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
                string fileName = $"SpiderSurge_Match_{timestamp}.txt";
                string filePath = Path.Combine(logDirectory, fileName);

                string content = FormatStats(stats);
                File.WriteAllText(filePath, content);

                Logger.LogInfo($"SpiderSurge match stats logged to: {fileName}");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to log SpiderSurge match stats: {ex.Message}");
            }
        }

        private string FormatStats(SpiderSurgeStatsSnapshot stats)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("=".PadRight(60, '='));
            sb.AppendLine("SPIDER SURGE MATCH STATISTICS");
            sb.AppendLine("=".PadRight(60, '='));
            sb.AppendLine();
            sb.AppendLine($"Match Time: {FormatTimeSpan(stats.MatchDuration)}");
            sb.AppendLine($"Players: {stats.PlayerCount}");
            sb.AppendLine($"Waves Survived: {stats.WavesSurvived}");
            sb.AppendLine($"Pain Level: {stats.PainLevel}");
            sb.AppendLine($"Ended By: {stats.EndReason}");
            sb.AppendLine($"Lives Left: {stats.LivesLeft}/{stats.MaxLives}");
            sb.AppendLine();

            sb.AppendLine("GLOBAL PERKS (Active):");
            if (stats.GlobalPerks != null && stats.GlobalPerks.Count > 0)
            {
                foreach (var perk in stats.GlobalPerks)
                {
                    sb.AppendLine($"  - {perk}");
                }
            }
            else
            {
                sb.AppendLine("  None");
            }
            sb.AppendLine();

            sb.AppendLine("PLAYER STATISTICS:");
            if (stats.PlayerStats != null && stats.PlayerStats.Count > 0)
            {
                foreach (var player in stats.PlayerStats)
                {
                    sb.AppendLine($"  Player {player.PlayerId + 1}:");
                    sb.AppendLine($"    Ability Activations: {player.AbilityActivationCount}");
                    sb.AppendLine($"    Ultimate Activations: {player.UltimateActivationCount}");
                    sb.AppendLine($"    Null Field Saves: {player.Saves}");
                    if (stats.StatsTracked)
                    {
                        sb.AppendLine($"    Deaths: {player.Deaths} ({player.LavaDeaths} in lava)");
                        sb.AppendLine($"    Kills: {player.Kills} ({player.KillsDuringAbility} while the ability was on)");
                        if (player.WeaponHits.Count > 0)
                        {
                            sb.AppendLine($"    Kills and Shields Broken by Weapon: {string.Join(", ", player.WeaponHits.Select(entry => $"{entry.Key} {entry.Value}"))}");
                        }
                        sb.AppendLine($"    Shots Hit: {player.ShotsHit}/{player.ShotsFired}");
                        sb.AppendLine($"    Time Alive: {FormatTimeSpan(TimeSpan.FromSeconds(player.AliveSeconds))}");
                    }
                    sb.AppendLine();
                }
            }
            else
            {
                sb.AppendLine("  No player data collected.");
            }

            sb.AppendLine("PERK CHOICES:");
            if (stats.PerkChoices != null && stats.PerkChoices.Count > 0)
            {
                foreach (var choice in stats.PerkChoices)
                {
                    string timedOut = choice.TimedOut ? ", timer ran out" : "";
                    string picked = choice.Picked != null ? $"{choice.Picked} Lvl {choice.Level}" : choice.Rerolled ? "nothing (rerolled)" : "nothing (run ended)";
                    string shop = choice.ShopOffer != null && choice.ShopOffer.Count > 0 ? $", shop [{string.Join(", ", choice.ShopOffer)}]" : "";
                    sb.AppendLine($"  Wave {choice.Wave} ({choice.Kind}): {picked} from [{string.Join(", ", choice.Offered)}]{shop}{timedOut}");
                }
            }
            else
            {
                sb.AppendLine("  None");
            }
            sb.AppendLine();

            sb.AppendLine("WAVES:");
            if (stats.Waves != null && stats.Waves.Count > 0)
            {
                foreach (var wave in stats.Waves)
                {
                    string deaths = wave.Deaths >= 0 ? $", {wave.Deaths} deaths" : "";
                    string saves = wave.Saves > 0 ? $", {wave.Saves} saves" : "";
                    sb.AppendLine($"  Wave {wave.Wave} on {wave.Map}: {wave.Result} in {wave.Seconds:0.0}s{deaths}, {wave.AbilityUses} abilities, {wave.UltimateUses} ultimates{saves}");
                }
            }
            else
            {
                sb.AppendLine("  None");
            }
            sb.AppendLine();

            if (stats.Meter != null)
            {
                var meter = stats.Meter;
                sb.AppendLine("CHAOS POINTS:");
                sb.AppendLine($"  Earned: {meter.PointsEarned}, spent: {meter.PointsSpent}");
                foreach (var entry in meter.Events) sb.AppendLine($"  Wave {entry.Wave}: {entry.Text}");
                if (meter.Purchases.Count > 0)
                {
                    foreach (var purchase in meter.Purchases)
                    {
                        string detail = string.IsNullOrEmpty(purchase.Detail) ? "" : $" ({purchase.Detail})";
                        sb.AppendLine($"  Wave {purchase.Wave}: bought {purchase.Item}{detail}");
                    }
                }
                else
                {
                    sb.AppendLine("  Nothing bought");
                }
                sb.AppendLine();
            }

            sb.AppendLine("=".PadRight(60, '='));
            return sb.ToString();
        }

        private string FormatTimeSpan(TimeSpan timeSpan)
        {
            if (timeSpan.TotalHours >= 1)
                return $"{timeSpan.Hours:00}:{timeSpan.Minutes:00}:{timeSpan.Seconds:00}";
            else
                return $"{timeSpan.Minutes:00}:{timeSpan.Seconds:00}";
        }
    }
}
