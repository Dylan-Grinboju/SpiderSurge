using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Logger = SpiderSurge.Common.ThreadSafeLog;

namespace SpiderSurge.Logging
{
    public class SpiderSurgeTelemetryUploader
    {
        private static readonly TimeSpan MinSendInterval = TimeSpan.FromMilliseconds(Consts.Telemetry.MinSendIntervalMs);
        private static readonly TimeSpan DuplicatePayloadWindow = TimeSpan.FromMinutes(Consts.Telemetry.DuplicatePayloadWindowMinutes);
        private static readonly HttpClient _httpClient = new HttpClient();

        private static readonly Lazy<SpiderSurgeTelemetryUploader> _lazy = new Lazy<SpiderSurgeTelemetryUploader>(() => new SpiderSurgeTelemetryUploader());
        public static SpiderSurgeTelemetryUploader Instance => _lazy.Value;

        private readonly string _pendingDirectory;
        private readonly string _anonymousIdPath;
        private readonly object _sendGuard = new object();
        private readonly Dictionary<string, DateTime> _recentPayloadHashes = new Dictionary<string, DateTime>();
        private readonly HashSet<string> _inflightPayloadHashes = new HashSet<string>();
        private static readonly Regex EscapedTimestampFieldRegex = new Regex("\\\\\"timestampUtc\\\\\":\\\\\"[^\\\\\"]*\\\\\"", RegexOptions.Compiled);
        private static readonly Regex PlainTimestampFieldRegex = new Regex("\"timestampUtc\":\"[^\"]*\"", RegexOptions.Compiled);
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
            if (snapshot == null) return;
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

        private string BuildRelayPayload(SpiderSurgeStatsSnapshot snapshot)
        {
            return BuildCompactEventJson(snapshot);
        }

        private string BuildCompactEventJson(SpiderSurgeStatsSnapshot snapshot)
        {
            var globalPerks = (snapshot.GlobalPerks ?? new List<string>())
                .Where(perk => !string.IsNullOrWhiteSpace(perk))
                .Select(perk => perk.Trim())
                .ToList();
            var playerStats = snapshot.PlayerStats ?? new List<PlayerStats>();
            var perkChoices = (snapshot.PerkChoices ?? new List<PerkChoiceRecord>()).ToList();
            var waves = (snapshot.Waves ?? new List<WaveRecord>()).ToList();

            bool globalPerksTruncated = globalPerks.Count > Consts.Telemetry.MaxGlobalPerks;
            bool playerStatsTruncated = playerStats.Count > Consts.Telemetry.MaxPlayerStats;
            bool perkChoicesTruncated = perkChoices.Count > Consts.Telemetry.MaxPerkChoiceRecords;
            bool wavesTruncated = waves.Count > Consts.Telemetry.MaxWaveRecords;
            if (globalPerksTruncated) globalPerks = globalPerks.Take(Consts.Telemetry.MaxGlobalPerks).ToList();
            if (playerStatsTruncated) playerStats = playerStats.Take(Consts.Telemetry.MaxPlayerStats).ToList();
            if (perkChoicesTruncated) perkChoices.RemoveRange(0, perkChoices.Count - Consts.Telemetry.MaxPerkChoiceRecords);
            if (wavesTruncated) waves.RemoveRange(0, waves.Count - Consts.Telemetry.MaxWaveRecords);

            var perkChoiceJsons = perkChoices.Select(choice => new KeyValuePair<int, string>(choice.Wave, BuildPerkChoiceJson(choice))).ToList();
            var waveJsons = waves.Select(wave => new KeyValuePair<int, string>(wave.Wave, BuildWaveJson(wave))).ToList();
            var eventJsons = (snapshot.Meter?.Events ?? new List<ChaosEventRecord>()).Select(entry => new KeyValuePair<int, string>(entry.Wave, BuildChaosEventJson(entry))).ToList();
            bool eventsTruncated = false;
            // The relay takes player indexes 0-7; with more than 8 players (InfiniteFriends) the ids can be higher, so the kept players are numbered in order instead
            bool idsFit = playerStats.All(p => p.PlayerId >= 0 && p.PlayerId < Consts.Telemetry.MaxPlayerStats);
            string playerStatsJson = "[" + string.Join(",", playerStats.Select((p, i) => BuildPlayerJson(p, idsFit ? p.PlayerId : i))) + "]";
            var playerIndexes = new Dictionary<int, int>();
            for (int i = 0; i < playerStats.Count; i++) playerIndexes[playerStats[i].PlayerId] = idsFit ? playerStats[i].PlayerId : i;
            string titlesJson = BuildTitlesJson(snapshot.Titles, playerIndexes);

            // The relay rejects bodies over its size limit, so the oldest waves, perk choices and chaos events are dropped until the report fits; a wave goes first on a tie
            var trimmable = new[] { waveJsons, perkChoiceJsons, eventJsons };
            while (true)
            {
                string json = BuildEventJson(snapshot, globalPerks, playerStatsJson, titlesJson, perkChoiceJsons, waveJsons, eventJsons,
                    globalPerksTruncated, playerStatsTruncated, perkChoicesTruncated, wavesTruncated, eventsTruncated);
                int excess = Encoding.UTF8.GetByteCount(json) - Consts.Telemetry.MaxPayloadBytes;
                if (excess <= 0 || trimmable.All(records => records.Count == 0)) return json;

                while (excess > 0 && trimmable.Any(records => records.Count > 0))
                {
                    var oldest = trimmable.Where(records => records.Count > 0).OrderBy(records => records[0].Key).First();
                    excess -= Encoding.UTF8.GetByteCount(oldest[0].Value) + 1;
                    oldest.RemoveAt(0);
                    if (oldest == perkChoiceJsons) perkChoicesTruncated = true;
                    else if (oldest == waveJsons) wavesTruncated = true;
                    else eventsTruncated = true;
                }
            }
        }

        private string BuildEventJson(
            SpiderSurgeStatsSnapshot snapshot,
            List<string> globalPerks,
            string playerStatsJson,
            string titlesJson,
            List<KeyValuePair<int, string>> perkChoiceJsons,
            List<KeyValuePair<int, string>> waveJsons,
            List<KeyValuePair<int, string>> eventJsons,
            bool globalPerksTruncated,
            bool playerStatsTruncated,
            bool perkChoicesTruncated,
            bool wavesTruncated,
            bool eventsTruncated)
        {
            string perkChoicesJson = "[" + string.Join(",", perkChoiceJsons.Select(entry => entry.Value)) + "]";
            string wavesJson = "[" + string.Join(",", waveJsons.Select(entry => entry.Value)) + "]";

            return "{" +
                "\"eventType\":\"spidersurge_match\"," +
                "\"schemaVersion\":" + Consts.Telemetry.SchemaVersion + "," +
                "\"anonId\":\"" + EscapeJson(GetOrCreateAnonymousId()) + "\"," +
                "\"timestampUtc\":\"" + DateTime.UtcNow.ToString("O") + "\"," +
                "\"modVersion\":\"" + EscapeJson(SpiderSurgeMod.Version) + "\"," +
                "\"matchDurationSeconds\":" + Math.Max(0, (int)snapshot.MatchDuration.TotalSeconds) + "," +
                "\"playerCount\":" + Math.Min(Consts.Telemetry.MaxPlayerStats, Math.Max(0, snapshot.PlayerCount)) + "," +
                "\"wavesSurvived\":" + Math.Max(0, snapshot.WavesSurvived) + "," +
                "\"painLevel\":" + Math.Max(1, snapshot.PainLevel) + "," +
                "\"endReason\":\"" + EscapeJson(snapshot.EndReason) + "\"," +
                "\"livesLeft\":" + Math.Max(0, snapshot.LivesLeft) + "," +
                "\"maxLives\":" + Math.Max(0, snapshot.MaxLives) + "," +
                "\"statsTracked\":" + JsonBool(snapshot.StatsTracked) + "," +
                "\"unlimitedPerkTime\":" + JsonBool(snapshot.UnlimitedPerkTime) + "," +
                "\"surge\":" + JsonBool(snapshot.SurgeMode) + "," +
                "\"abilities\":" + JsonBool(snapshot.AbilitiesOn) + "," +
                "\"globalPerksTruncated\":" + JsonBool(globalPerksTruncated) + "," +
                "\"playerStatsTruncated\":" + JsonBool(playerStatsTruncated) + "," +
                "\"perkChoicesTruncated\":" + JsonBool(perkChoicesTruncated) + "," +
                "\"wavesTruncated\":" + JsonBool(wavesTruncated) + "," +
                "\"globalPerks\":" + JsonStringArray(globalPerks) + "," +
                "\"playerStats\":" + playerStatsJson + "," +
                "\"titles\":" + titlesJson + "," +
                "\"meter\":" + BuildMeterJson(snapshot.Meter, eventJsons, eventsTruncated) + "," +
                "\"perkChoices\":" + perkChoicesJson + "," +
                "\"waves\":" + wavesJson +
                "}";
        }

        private static string BuildMeterJson(MeterReport meter, List<KeyValuePair<int, string>> eventJsons, bool eventsTruncated)
        {
            if (meter == null) return "null";
            string purchases = "[" + string.Join(",", (meter.Purchases ?? new List<MeterPurchaseRecord>()).Select(purchase => "{" +
                "\"wave\":" + purchase.Wave + "," +
                "\"item\":\"" + EscapeJson(purchase.Item) + "\"," +
                "\"detail\":\"" + EscapeJson(purchase.Detail) + "\"" +
                "}")) + "]";
            return "{" +
                "\"chargePerPoint\":" + meter.ChargePerPoint + "," +
                "\"totalCharge\":" + meter.TotalCharge + "," +
                "\"pointsEarned\":" + meter.PointsEarned + "," +
                "\"pointsSpent\":" + meter.PointsSpent + "," +
                "\"chargeBySource\":" + JsonCounts(meter.ChargeBySource) + "," +
                "\"eventsTruncated\":" + JsonBool(eventsTruncated) + "," +
                "\"events\":[" + string.Join(",", eventJsons.Select(entry => entry.Value)) + "]," +
                "\"purchases\":" + purchases +
                "}";
        }

        private static string BuildChaosEventJson(ChaosEventRecord entry)
        {
            return "{" +
                "\"wave\":" + entry.Wave + "," +
                "\"text\":\"" + EscapeJson(entry.Text) + "\"" +
                "}";
        }

        private static string BuildPlayerJson(PlayerStats player, int playerIndex)
        {
            return "{" +
                "\"playerIndex\":" + playerIndex + "," +
                "\"abilityActivationCount\":" + player.AbilityActivationCount + "," +
                "\"ultimateActivationCount\":" + player.UltimateActivationCount + "," +
                "\"deaths\":" + player.Deaths + "," +
                "\"lavaDeaths\":" + player.LavaDeaths + "," +
                "\"kills\":" + player.Kills + "," +
                "\"friendlyKills\":" + player.FriendlyKills + "," +
                "\"shotsFired\":" + player.ShotsFired + "," +
                "\"shotsHit\":" + player.ShotsHit + "," +
                "\"aliveSeconds\":" + Math.Max(0, player.AliveSeconds) + "," +
                "\"saves\":" + player.Saves + "," +
                "\"killsDuringAbility\":" + player.KillsDuringAbility + "," +
                "\"meterCharge\":" + player.MeterCharge + "," +
                "\"enemyKills\":" + JsonCounts(player.EnemyKills) + "," +
                "\"weaponHits\":" + JsonCounts(player.WeaponHits) +
                "}";
        }

        // Titles shown on the end screen come first, so a cut list still has all of them
        private static string BuildTitlesJson(TitlesReport titles, Dictionary<int, int> playerIndexes)
        {
            if (titles == null) return "null";
            var earned = (titles.Earned ?? new List<EarnedTitle>()).Where(title => !string.IsNullOrWhiteSpace(title.Name)).OrderByDescending(title => title.OnScreen).ToList();
            bool truncated = earned.Count > Consts.Telemetry.MaxTitleRecords;
            if (truncated) earned = earned.Take(Consts.Telemetry.MaxTitleRecords).ToList();

            string earnedJson = "[" + string.Join(",", earned.Select(title => "{" +
                "\"name\":\"" + EscapeJson(title.Name) + "\"," +
                "\"playerIndex\":" + (playerIndexes.TryGetValue(title.PlayerId, out int index) ? index.ToString(CultureInfo.InvariantCulture) : "null") + "," +
                "\"onScreen\":" + JsonBool(title.OnScreen) +
                "}")) + "]";

            return "{" +
                "\"shown\":" + JsonBool(titles.Shown) + "," +
                "\"newlyUnlocked\":" + JsonNumber(titles.NewlyUnlocked) + "," +
                "\"unlockedTotal\":" + JsonNumber(titles.UnlockedTotal) + "," +
                "\"catalogSize\":" + titles.CatalogSize + "," +
                "\"truncated\":" + JsonBool(truncated) + "," +
                "\"earned\":" + earnedJson +
                "}";
        }

        private static string JsonNumber(int? value) => value.HasValue ? value.Value.ToString(CultureInfo.InvariantCulture) : "null";

        private static string BuildPerkChoiceJson(PerkChoiceRecord choice)
        {
            return "{" +
                "\"playerId\":" + JsonNumber(choice.PlayerId) + "," +
                "\"wave\":" + choice.Wave + "," +
                "\"kind\":\"" + EscapeJson(choice.Kind) + "\"," +
                "\"offered\":" + JsonStringArray(choice.Offered) + "," +
                "\"lucky\":" + JsonStringArray(choice.LuckyOffered) + "," +
                "\"picked\":" + (choice.Picked != null ? "\"" + EscapeJson(choice.Picked) + "\"" : "null") + "," +
                "\"level\":" + choice.Level + "," +
                "\"timedOut\":" + JsonBool(choice.TimedOut) + "," +
                "\"rerolled\":" + JsonBool(choice.Rerolled) + "," +
                "\"shopOffer\":" + JsonStringArray(choice.ShopOffer) + "," +
                "\"decisionSeconds\":" + JsonSeconds(choice.DecisionSeconds) +
                "}";
        }

        private static string BuildWaveJson(WaveRecord wave)
        {
            return "{" +
                "\"wave\":" + wave.Wave + "," +
                "\"map\":\"" + EscapeJson(wave.Map) + "\"," +
                "\"result\":\"" + EscapeJson(wave.Result) + "\"," +
                "\"seconds\":" + JsonSeconds(wave.Seconds) + "," +
                "\"livesAtStart\":" + wave.LivesAtStart + "," +
                "\"abilityUses\":" + wave.AbilityUses + "," +
                "\"ultimateUses\":" + wave.UltimateUses + "," +
                "\"saves\":" + wave.Saves + "," +
                "\"meterCharge\":" + wave.MeterCharge +
                (wave.Deaths >= 0 ? ",\"deaths\":" + wave.Deaths : "") +
                "}";
        }

        private static string JsonStringArray(IEnumerable<string> values)
        {
            return "[" + string.Join(",", (values ?? Enumerable.Empty<string>()).Select(value => "\"" + EscapeJson(value) + "\"")) + "]";
        }

        private static string JsonCounts(Dictionary<string, int> counts)
        {
            return "{" + string.Join(",", (counts ?? new Dictionary<string, int>())
                .OrderBy(entry => entry.Key, StringComparer.Ordinal)
                .Select(entry => "\"" + EscapeJson(entry.Key) + "\":" + entry.Value)) + "}";
        }

        private static string JsonBool(bool value) => value ? "true" : "false";

        private static string JsonSeconds(float seconds) => Math.Max(0f, seconds).ToString("0.#", CultureInfo.InvariantCulture);

        public string GetAnonymousIdOrNull()
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
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to read telemetry anonymous ID: {ex.Message}");
            }
            return null;
        }

        public string GetOrCreateAnonymousId()
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

                return ResetAnonymousId();
            }
            catch (Exception ex)
            {
                Logger.LogError($"Failed to create/load telemetry anonymous ID: {ex.Message}");
                _anonymousId = "unknown";
                return _anonymousId;
            }
        }

        public string ResetAnonymousId()
        {
            try
            {
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
                Logger.LogError($"Failed to reset telemetry anonymous ID: {ex.Message}");
                return _anonymousId; // Returns the old one or "unknown" if it fails
            }
        }

        // Returns false only when sending again later could succeed; a payload the relay rejects as invalid counts as done
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

                using (var content = new StringContent(payload, Encoding.UTF8, "application/json"))
                using (var timeout = new CancellationTokenSource(Consts.Telemetry.RequestTimeoutMs))
                {
                    var response = await _httpClient.PostAsync(relayUrl, content, timeout.Token);
                    int status = (int)response.StatusCode;
                    if (status >= 400 && status < 500 && status != 408 && status != 429)
                    {
                        string body = await response.Content.ReadAsStringAsync();
                        ReleasePayloadInflightReservation(payload);
                        Logger.LogWarning($"Telemetry payload rejected by the relay ({status}) and dropped: {body}");
                        return true;
                    }
                    response.EnsureSuccessStatusCode();
                    RegisterPayloadAsSent(payload);
                    return true;
                }
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
            using (var sha = SHA256.Create())
            {
                byte[] bytes = Encoding.UTF8.GetBytes(NormalizePayloadForHashing(payload));
                byte[] hash = sha.ComputeHash(bytes);
                return Convert.ToBase64String(hash);
            }
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
            if (string.IsNullOrEmpty(value)) return string.Empty;

            return value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n");
        }

    }
}
