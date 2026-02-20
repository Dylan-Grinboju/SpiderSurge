using System;
using System.IO;
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
                    sb.AppendLine($"  Player {player.PlayerIndex + 1}:");
                    sb.AppendLine($"    Ability Activations: {player.ActivationCount}");
                    sb.AppendLine();
                }
            }
            else
            {
                sb.AppendLine("  No player data collected.");
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
