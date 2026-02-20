using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Logger = Silk.Logger;

namespace SpiderSurge.Logging
{
    public class SpiderSurgeTelemetryUploader
    {
        private static readonly TimeSpan MinSendInterval = TimeSpan.FromMilliseconds(Consts.Telemetry.MinSendIntervalMs);
        private static readonly TimeSpan DuplicatePayloadWindow = TimeSpan.FromMinutes(Consts.Telemetry.DuplicatePayloadWindowMinutes);

        private static readonly Lazy<SpiderSurgeTelemetryUploader> _lazy = new Lazy<SpiderSurgeTelemetryUploader>(() => new SpiderSurgeTelemetryUploader());
        public static SpiderSurgeTelemetryUploader Instance => _lazy.Value;

        private readonly string _pendingDirectory;
        private readonly string _anonymousIdPath;
        private readonly object _sendGuard = new object();
        private readonly Dictionary<string, DateTime> _recentPayloadHashes = new Dictionary<string, DateTime>();
        private string _anonymousId;
        private DateTime _lastSuccessfulSendUtc = DateTime.MinValue;

        private SpiderSurgeTelemetryUploader()
        {
            string baseDirectory = SpiderSurgeLogger.Instance.LogDirectory;
            _pendingDirectory = Path.Combine(baseDirectory, Consts.Telemetry.PendingDirectoryName);
            _anonymousIdPath = Path.Combine(baseDirectory, Consts.Telemetry.AnonymousIdFileName);

            try
            {
                if (!Directory.Exists(_pendingDirectory))
                {
                    Directory.CreateDirectory(_pendingDirectory);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to initialize telemetry pending directory: {ex.Message}");
            }
        }

        public void QueueAndSendSnapshotAsync(SpiderSurgeStatsSnapshot snapshot)
        {
            if (snapshot == null) return;
            if (!ModConfig.TelemetryEnabled) return;

            string webhookUrl = GetWebhookUrl();
            if (string.IsNullOrEmpty(webhookUrl))
            {
                Logger.LogWarning("Telemetry enabled but telemetry webhook URL is not configured in code.");
                return;
            }

            string payload = BuildDiscordPayload(snapshot);
            _ = Task.Run(() =>
            {
                if (!TryPostPayload(webhookUrl, payload))
                {
                    QueuePayload(payload);
                }
            });
        }

        public void FlushQueuedPayloadsAsync()
        {
            if (!ModConfig.TelemetryEnabled) return;

            string webhookUrl = GetWebhookUrl();
            if (string.IsNullOrEmpty(webhookUrl)) return;

            _ = Task.Run(() =>
            {
                try
                {
                    if (!Directory.Exists(_pendingDirectory)) return;

                    var files = Directory.GetFiles(_pendingDirectory, "*.json")
                        .OrderBy(path => path, StringComparer.Ordinal)
                        .ToList();

                    if (files.Count == 0) return;

                    int sentCount = 0;
                    foreach (var file in files)
                    {
                        string payload = File.ReadAllText(file);
                        if (!TryPostPayload(webhookUrl, payload))
                        {
                            break;
                        }

                        File.Delete(file);
                        sentCount++;
                    }

                    if (sentCount > 0)
                    {
                        Logger.LogInfo($"Flushed {sentCount} queued telemetry payload(s).");
                    }
                }
                catch (Exception ex)
                {
                    Logger.LogError($"Failed while flushing queued telemetry payloads: {ex.Message}");
                }
            });
        }

        private string BuildDiscordPayload(SpiderSurgeStatsSnapshot snapshot)
        {
            string message = BuildCompactEventJson(snapshot);
            return "{\"content\":\"" + EscapeJson(message) + "\"}";
        }

        private string BuildCompactEventJson(SpiderSurgeStatsSnapshot snapshot)
        {
            var globalPerks = (snapshot.GlobalPerks ?? new List<string>())
                .Where(perk => !string.IsNullOrWhiteSpace(perk))
                .Select(perk => perk.Trim())
                .ToList();

            var playerStats = (snapshot.PlayerStats ?? new List<PlayerStats>())
                .Select(player => new PlayerStats
                {
                    PlayerIndex = player.PlayerIndex,
                    ActivationCount = player.ActivationCount
                })
                .ToList();

            bool perksTruncated = false;
            bool playerStatsTruncated = false;

            string json = BuildEventJson(snapshot, globalPerks, playerStats, perksTruncated, playerStatsTruncated);

            while (json.Length > Consts.Telemetry.MaxDiscordMessageLength && globalPerks.Count > 0)
            {
                globalPerks.RemoveAt(globalPerks.Count - 1);
                perksTruncated = true;
                json = BuildEventJson(snapshot, globalPerks, playerStats, perksTruncated, playerStatsTruncated);
            }

            while (json.Length > Consts.Telemetry.MaxDiscordMessageLength && playerStats.Count > 0)
            {
                playerStats.RemoveAt(playerStats.Count - 1);
                playerStatsTruncated = true;
                json = BuildEventJson(snapshot, globalPerks, playerStats, perksTruncated, playerStatsTruncated);
            }

            if (json.Length > Consts.Telemetry.MaxDiscordMessageLength)
            {
                globalPerks.Clear();
                playerStats.Clear();
                perksTruncated = true;
                playerStatsTruncated = true;
                json = BuildEventJson(snapshot, globalPerks, playerStats, perksTruncated, playerStatsTruncated);
            }

            return json;
        }

        private string BuildEventJson(
            SpiderSurgeStatsSnapshot snapshot,
            List<string> globalPerks,
            List<PlayerStats> playerStats,
            bool perksTruncated,
            bool playerStatsTruncated)
        {
            int totalActivations = playerStats.Sum(player => player.ActivationCount);

            string perksJson = "[" + string.Join(",", globalPerks.Select(perk => "\"" + EscapeJson(perk) + "\"")) + "]";
            string playerStatsJson = "[" + string.Join(",", playerStats.Select(player =>
                "{\"playerIndex\":" + player.PlayerIndex + ",\"activationCount\":" + player.ActivationCount + "}")) + "]";

            return "{" +
                "\"eventType\":\"spidersurge_match\"," +
                "\"schemaVersion\":1," +
                "\"anonId\":\"" + EscapeJson(GetOrCreateAnonymousId()) + "\"," +
                "\"timestampUtc\":\"" + DateTime.UtcNow.ToString("O") + "\"," +
                "\"modVersion\":\"" + EscapeJson(SpiderSurgeMod.Version) + "\"," +
                "\"matchDurationSeconds\":" + Math.Max(0, (int)snapshot.MatchDuration.TotalSeconds) + "," +
                "\"playerCount\":" + Math.Max(0, snapshot.PlayerCount) + "," +
                "\"wavesSurvived\":" + Math.Max(0, snapshot.WavesSurvived) + "," +
                "\"painLevel\":" + Math.Max(1, snapshot.PainLevel) + "," +
                "\"globalPerksTruncated\":" + (perksTruncated ? "true" : "false") + "," +
                "\"playerStatsTruncated\":" + (playerStatsTruncated ? "true" : "false") + "," +
                "\"totalActivations\":" + Math.Max(0, totalActivations) + "," +
                "\"globalPerks\":" + perksJson + "," +
                "\"playerStats\":" + playerStatsJson +
                "}";
        }

        private string GetOrCreateAnonymousId()
        {
            if (!string.IsNullOrEmpty(_anonymousId))
            {
                return _anonymousId;
            }

            try
            {
                if (File.Exists(_anonymousIdPath))
                {
                    string existing = File.ReadAllText(_anonymousIdPath).Trim();
                    if (!string.IsNullOrEmpty(existing))
                    {
                        _anonymousId = existing;
                        return _anonymousId;
                    }
                }

                byte[] bytes = new byte[Consts.Telemetry.AnonymousIdBytes];
                using (var random = RandomNumberGenerator.Create())
                {
                    random.GetBytes(bytes);
                }

                _anonymousId = Convert.ToBase64String(bytes)
                    .Replace("+", "")
                    .Replace("/", "")
                    .Replace("=", "");

                File.WriteAllText(_anonymousIdPath, _anonymousId);
                return _anonymousId;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to create/load telemetry anonymous ID: {ex.Message}");
                _anonymousId = "unknown";
                return _anonymousId;
            }
        }

        private bool TryPostPayload(string webhookUrl, string payload)
        {
            try
            {
                if (IsDuplicatePayload(payload))
                {
                    Logger.LogInfo("Telemetry payload skipped: duplicate within dedupe window.");
                    return true;
                }

                ApplySendThrottle();

                using (var client = new TimeoutWebClient(Consts.Telemetry.RequestTimeoutMs))
                {
                    client.Headers[HttpRequestHeader.ContentType] = "application/json";
                    client.UploadString(webhookUrl, "POST", payload);
                    RegisterPayloadAsSent(payload);
                    return true;
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Telemetry upload failed: {ex.Message}");
                return false;
            }
        }

        private void QueuePayload(string payload)
        {
            try
            {
                if (!Directory.Exists(_pendingDirectory))
                {
                    Directory.CreateDirectory(_pendingDirectory);
                }

                string name = $"telemetry_{DateTime.UtcNow:yyyyMMdd_HHmmss_fff}_{Guid.NewGuid():N}.json";
                string path = Path.Combine(_pendingDirectory, name);
                File.WriteAllText(path, payload, Encoding.UTF8);
                TrimQueueIfNeeded();
                Logger.LogInfo($"Queued telemetry payload: {name}");
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to queue telemetry payload: {ex.Message}");
            }
        }

        private void TrimQueueIfNeeded()
        {
            try
            {
                var files = Directory.GetFiles(_pendingDirectory, "*.json")
                    .OrderBy(path => path, StringComparer.Ordinal)
                    .ToList();

                int overflow = files.Count - Consts.Telemetry.MaxQueuedPayloads;
                if (overflow <= 0) return;

                for (int i = 0; i < overflow; i++)
                {
                    File.Delete(files[i]);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to trim telemetry queue: {ex.Message}");
            }
        }

        private string GetWebhookUrl()
        {
            var value = Consts.Telemetry.DiscordWebhookUrl;
            return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
        }

        private bool IsDuplicatePayload(string payload)
        {
            string hash = ComputePayloadHash(payload);
            DateTime now = DateTime.UtcNow;

            lock (_sendGuard)
            {
                PruneOldHashes(now);
                if (_recentPayloadHashes.TryGetValue(hash, out DateTime lastSeen))
                {
                    if ((now - lastSeen) <= DuplicatePayloadWindow)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private void RegisterPayloadAsSent(string payload)
        {
            string hash = ComputePayloadHash(payload);
            DateTime now = DateTime.UtcNow;

            lock (_sendGuard)
            {
                _recentPayloadHashes[hash] = now;
                _lastSuccessfulSendUtc = now;
                PruneOldHashes(now);
            }
        }

        private void ApplySendThrottle()
        {
            TimeSpan waitTime = TimeSpan.Zero;

            lock (_sendGuard)
            {
                DateTime now = DateTime.UtcNow;
                DateTime nextAllowed = _lastSuccessfulSendUtc + MinSendInterval;
                if (nextAllowed > now)
                {
                    waitTime = nextAllowed - now;
                }
            }

            if (waitTime > TimeSpan.Zero)
            {
                int waitMs = (int)Math.Ceiling(waitTime.TotalMilliseconds);
                if (waitMs > 0)
                {
                    Thread.Sleep(waitMs);
                }
            }
        }

        private void PruneOldHashes(DateTime now)
        {
            var expired = _recentPayloadHashes
                .Where(entry => (now - entry.Value) > DuplicatePayloadWindow)
                .Select(entry => entry.Key)
                .ToList();

            foreach (var key in expired)
            {
                _recentPayloadHashes.Remove(key);
            }
        }

        private string ComputePayloadHash(string payload)
        {
            using (var sha = SHA256.Create())
            {
                byte[] bytes = Encoding.UTF8.GetBytes(payload ?? string.Empty);
                byte[] hash = sha.ComputeHash(bytes);
                return Convert.ToBase64String(hash);
            }
        }

        private static string EscapeJson(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;

            return value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n");
        }

        private class TimeoutWebClient : WebClient
        {
            private readonly int _timeout;

            public TimeoutWebClient(int timeout)
            {
                _timeout = timeout;
            }
        }
    }
}
