using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Logger = Silk.Logger;

namespace SpiderSurge.Logging;

public class SpiderSurgeTelemetryUploader
{
    private static readonly TimeSpan MinSendInterval = TimeSpan.FromMilliseconds(Consts.Telemetry.MinSendIntervalMs);
    private static readonly TimeSpan DuplicatePayloadWindow = TimeSpan.FromMinutes(Consts.Telemetry.DuplicatePayloadWindowMinutes);
    private static readonly HttpClient _httpClient = new();

    private static readonly Lazy<SpiderSurgeTelemetryUploader> _lazy = new(() => new SpiderSurgeTelemetryUploader());
    public static SpiderSurgeTelemetryUploader Instance => _lazy.Value;

    private readonly string _pendingDirectory;
    private readonly string _anonymousIdPath;
    private readonly object _sendGuard = new();
    private readonly Dictionary<string, DateTime> _recentPayloadHashes = [];
    private readonly HashSet<string> _inflightPayloadHashes = [];
    private static readonly Regex EscapedTimestampFieldRegex = new("\\\\\"timestampUtc\\\\\":\\\\\"[^\\\\\"]*\\\\\"", RegexOptions.Compiled);
    private static readonly Regex PlainTimestampFieldRegex = new("\"timestampUtc\":\"[^\"]*\"", RegexOptions.Compiled);
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

    public void QueueAndSendSnapshot(SpiderSurgeStatsSnapshot snapshot)
    {
        if (snapshot is null) return;
        if (!ModConfig.TelemetryEnabled) return;

        string relayUrl = GetRelayUrl();
        if (string.IsNullOrEmpty(relayUrl))
        {
            Logger.LogWarning("Telemetry enabled but telemetry relay endpoint URL is not configured in code.");
            return;
        }

        string payload = BuildRelayPayload(snapshot);
        _ = Task.Run(async () =>
        {
            if (!await TryPostPayload(relayUrl, payload))
            {
                QueuePayload(payload);
            }
        });
    }

    public void FlushQueuedPayloads()
    {
        if (!ModConfig.TelemetryEnabled) return;

        string relayUrl = GetRelayUrl();
        if (string.IsNullOrEmpty(relayUrl)) return;

        _ = Task.Run(async () =>
        {
            try
            {
                if (!Directory.Exists(_pendingDirectory)) return;

                var files = Directory.GetFiles(_pendingDirectory, "*.json")
                    .OrderBy(path => path, StringComparer.Ordinal)
                    .ToList();

                if (files.Count == 0) return;

                int sentCount = 0;
                int failedCount = 0;
                foreach (var file in files)
                {
                    string payload = File.ReadAllText(file);
                    if (!await TryPostPayload(relayUrl, payload))
                    {
                        failedCount++;
                        continue;
                    }

                    File.Delete(file);
                    sentCount++;
                }

                if (sentCount > 0)
                {
                    Logger.LogInfo($"Flushed {sentCount} queued telemetry payload(s).");
                }

                if (failedCount > 0)
                {
                    Logger.LogWarning($"Failed to flush {failedCount} queued telemetry payload(s); they remain queued for retry.");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed while flushing queued telemetry payloads: {ex.Message}");
            }
        });
    }

    private string BuildRelayPayload(SpiderSurgeStatsSnapshot snapshot) => BuildCompactEventJson(snapshot);

    private string BuildCompactEventJson(SpiderSurgeStatsSnapshot snapshot)
    {
        var globalPerks = (snapshot.GlobalPerks ?? [])
            .Where(perk => !string.IsNullOrWhiteSpace(perk))
            .Select(perk => perk.Trim())
            .ToList();

        var playerStats = (snapshot.PlayerStats ?? [])
            .Select(player => new PlayerStats
            {
                PlayerIndex = player.PlayerIndex,
                AbilityActivationCount = player.AbilityActivationCount,
                UltimateActivationCount = player.UltimateActivationCount
            })
            .ToList();

        return BuildEventJson(snapshot, globalPerks, playerStats, false, false);
    }

    private string BuildEventJson(
        SpiderSurgeStatsSnapshot snapshot,
        List<string> globalPerks,
        List<PlayerStats> playerStats,
        bool perksTruncated,
        bool playerStatsTruncated)
    {
        string perksJson = "[" + string.Join(",", globalPerks.Select(perk => "\"" + EscapeJson(perk) + "\"")) + "]";
        string playerStatsJson = "[" + string.Join(",", playerStats.Select(player =>
            "{\"playerIndex\":" + player.PlayerIndex + ",\"abilityActivationCount\":" + player.AbilityActivationCount + ",\"ultimateActivationCount\":" + player.UltimateActivationCount + "}")) + "]";

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

    private async Task<bool> TryPostPayload(string relayUrl, string payload)
    {
        bool isReserved = false;
        try
        {
            if (!ReservePayloadAsInflight(payload))
            {
                Logger.LogInfo("Telemetry payload skipped: duplicate within dedupe window.");
                return true;
            }

            isReserved = true;

            await ApplySendThrottleAsync();

            using var content = new StringContent(payload, Encoding.UTF8, "application/json");
            using var timeout = new CancellationTokenSource(Consts.Telemetry.RequestTimeoutMs);
            var response = await _httpClient.PostAsync(relayUrl, content, timeout.Token);
            response.EnsureSuccessStatusCode();
            RegisterPayloadAsSent(payload);
            return true;
        }
        catch (Exception ex)
        {
            if (isReserved)
            {
                ReleasePayloadInflightReservation(payload);
            }

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

    private string GetRelayUrl()
    {
        var value = Consts.Telemetry.RelayEndpointUrl;
        return string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
    }

    private bool ReservePayloadAsInflight(string payload)
    {
        string hash = ComputePayloadHash(payload);
        DateTime now = DateTime.UtcNow;

        lock (_sendGuard)
        {
            PruneOldHashes(now);
            if (_inflightPayloadHashes.Contains(hash))
            {
                return false;
            }

            if (_recentPayloadHashes.TryGetValue(hash, out DateTime lastSeen))
            {
                if ((now - lastSeen) <= DuplicatePayloadWindow)
                {
                    return false;
                }
            }

            _inflightPayloadHashes.Add(hash);
            return true;
        }
    }

    private void ReleasePayloadInflightReservation(string payload)
    {
        string hash = ComputePayloadHash(payload);

        lock (_sendGuard)
        {
            _inflightPayloadHashes.Remove(hash);
        }
    }

    private void RegisterPayloadAsSent(string payload)
    {
        string hash = ComputePayloadHash(payload);
        DateTime now = DateTime.UtcNow;

        lock (_sendGuard)
        {
            _inflightPayloadHashes.Remove(hash);
            _recentPayloadHashes[hash] = now;
            _lastSuccessfulSendUtc = now;
            PruneOldHashes(now);
        }
    }

    private async Task ApplySendThrottleAsync()
    {
        int waitMs = 0;

        lock (_sendGuard)
        {
            DateTime now = DateTime.UtcNow;
            DateTime nextAllowed = _lastSuccessfulSendUtc + MinSendInterval;
            if (nextAllowed > now)
            {
                waitMs = (int)Math.Ceiling((nextAllowed - now).TotalMilliseconds);
            }
        }

        if (waitMs > 0)
        {
            await Task.Delay(waitMs);
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
        using var sha = SHA256.Create();
        byte[] bytes = Encoding.UTF8.GetBytes(NormalizePayloadForHashing(payload));
        byte[] hash = sha.ComputeHash(bytes);
        return Convert.ToBase64String(hash);
    }

    private string NormalizePayloadForHashing(string payload)
    {
        string normalized = payload ?? string.Empty;
        normalized = EscapedTimestampFieldRegex.Replace(normalized, "\\\"timestampUtc\\\":\\\"redacted\\\"");
        normalized = PlainTimestampFieldRegex.Replace(normalized, "\"timestampUtc\":\"redacted\"");
        return normalized;
    }

    private static string EscapeJson(string value)
    {
        return string.IsNullOrEmpty(value)
            ? string.Empty
            : value
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\r", "\\r")
            .Replace("\n", "\\n");
    }

}
