using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;
using HarmonyLib;
using SpiderSurge.Common;
using Logger = Silk.Logger;

namespace SpiderSurge.Logging
{
    public class SpiderSurgeStatsManager : MonoBehaviour
    {
        public static class EndReasons
        {
            public const string LivesOut = "livesOut";
            public const string Restart = "restart";
            public const string Quit = "quit";
        }

        public static class PerkChoiceKinds
        {
            public const string Ability = "ability";
            public const string Ultimate = "ultimate";
            public const string Swap = "swap";
            public const string Perk = "perk";
        }

        public static class WaveResults
        {
            public const string Won = "won";
            public const string Lost = "lost";
            public const string Quit = "quit";
            public const string Reset = "reset";
        }

        private static SpiderSurgeStatsManager _instance;
        public static SpiderSurgeStatsManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    GameObject obj = new GameObject("SpiderSurgeStatsManager");
                    _instance = obj.AddComponent<SpiderSurgeStatsManager>();
                    DontDestroyOnLoad(obj);
                }
                return _instance;
            }
        }

        private static readonly AccessTools.FieldRef<SurvivalModeHud, NetworkVariable<uint>> PerkChoiceTimerRef =
            AccessTools.FieldRefAccess<SurvivalModeHud, NetworkVariable<uint>>("perkChoiseTimer");

        private readonly Dictionary<int, int> _abilityActivationsPerPlayer = new Dictionary<int, int>();
        private readonly Dictionary<int, int> _ultimateActivationsPerPlayer = new Dictionary<int, int>();
        private readonly Dictionary<int, int> _savesPerPlayer = new Dictionary<int, int>();
        private readonly Dictionary<int, float> _readySeconds = new Dictionary<int, float>();
        private readonly Dictionary<int, float> _activeSeconds = new Dictionary<int, float>();
        private readonly Dictionary<int, int> _firstUltimateWave = new Dictionary<int, int>();
        private readonly Dictionary<int, Dictionary<string, int>> _abilityEffects = new Dictionary<int, Dictionary<string, int>>();
        private readonly Dictionary<int, WavePlayerRecord> _wavePlayers = new Dictionary<int, WavePlayerRecord>();
        private readonly Dictionary<int, (int Kills, int Deaths, float Alive)> _waveStartTotals = new Dictionary<int, (int, int, float)>();
        private readonly List<BaseAbility> _abilityBuffer = new List<BaseAbility>();
        private readonly List<PerkChoiceRecord> _perkChoices = new List<PerkChoiceRecord>();
        private readonly List<WaveRecord> _waves = new List<WaveRecord>();
        private readonly List<MeterPurchaseRecord> _meterPurchases = new List<MeterPurchaseRecord>();
        private PerkChoiceRecord _pendingPerkChoice;
        private float _pendingPerkOfferedAt;
        private bool _pendingPerkSubmitted;
        private WaveRecord _currentWaveRecord;
        private float _currentWaveStartTime;
        private bool _countingDeaths = false;
        private DateTime _matchStartTime;
        private bool _isTracking = false;
        private bool _livesOut = false;
        private bool _restartRequested = false;
        private int _currentWave = 0;
        private SpiderSurgeStatsSnapshot _pendingTelemetry;
        private bool _surgeRun;
        private int _titleCalculationsAtStart;
        private static System.Reflection.FieldInfo[] _modifierManagerFields;
        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public SpiderSurgeStatsSnapshot LastFinished { get; private set; }

        public void StartTracking()
        {
            LastFinished = null;
            if ((!ModConfig.EnableStatsLogging && !ModConfig.TelemetryEnabled && !MatchHistory.Enabled) || !Net.IsAuthority) return;

            SendPendingTelemetry();
            _titleCalculationsAtStart = StatsMod.TitlesManager.Instance.CalculationCount;
            _surgeRun = SurgeGameModeManager.IsSurgeRunActive;
            _matchStartTime = DateTime.Now;
            _abilityActivationsPerPlayer.Clear();
            _ultimateActivationsPerPlayer.Clear();
            _savesPerPlayer.Clear();
            _readySeconds.Clear();
            _activeSeconds.Clear();
            _firstUltimateWave.Clear();
            _abilityEffects.Clear();
            _wavePlayers.Clear();
            _waveStartTotals.Clear();
            AbilityEffects.Clear();
            _meterPurchases.Clear();
            _perkChoices.Clear();
            _waves.Clear();
            _pendingPerkChoice = null;
            _currentWaveRecord = null;
            _livesOut = false;
            _restartRequested = false;
            _isTracking = true;
            _currentWave = 0;
            SetDeathCounting(Parts.Stats.Running);
            Logger.LogInfo("SpiderSurge stats tracking started.");
        }

        // The finished run's snapshot, stopping the run first if it is still tracked
        public SpiderSurgeStatsSnapshot Finish()
        {
            StopTrackingAndLog();
            return LastFinished;
        }

        public void StopTrackingAndLog()
        {
            if (!_isTracking) return;

            CloseWave(WaveResults.Quit);
            RecordUnansweredPerkChoice();
            _isTracking = false;
            SetDeathCounting(false);
            TimeSpan duration = DateTime.Now - _matchStartTime;

            bool statsTracked = Parts.Stats.Running;
            var players = Players.All;
            var snapshot = new SpiderSurgeStatsSnapshot
            {
                MatchDuration = duration,
                PlayerCount = players != null ? players.Count : 0,
                WavesSurvived = GetWavesSurvived(),
                PainLevel = GetCurrentPainLevel(),
                GlobalPerks = GetGlobalPerks(),
                EndReason = _livesOut ? EndReasons.LivesOut : _restartRequested ? EndReasons.Restart : EndReasons.Quit,
                LivesLeft = SurvivalMode.instance != null ? SurvivalMode.instance.Lives : 0,
                MaxLives = GetMaxLives(),
                StatsTracked = statsTracked,
                UnlimitedPerkTime = ModConfig.UnlimitedPerkChoosingTime,
                SurgeMode = _surgeRun,
                AbilitiesOn = _surgeRun && (SurgeGameModeManager.Instance == null || !SurgeGameModeManager.Instance.NoAbilitiesThisRun),
                PerkChoices = new List<PerkChoiceRecord>(_perkChoices),
                Waves = new List<WaveRecord>(_waves),
                Meter = _surgeRun ? BuildMeterReport() : null
            };

            if (players != null)
            {
                foreach (var player in players)
                {
                    if (player == null) continue;
                    int playerId = player.playerID.Value;
                    var stats = new PlayerStats
                    {
                        PlayerId = playerId,
                        AbilityActivationCount = _abilityActivationsPerPlayer.TryGetValue(playerId, out int abilities) ? abilities : 0,
                        UltimateActivationCount = _ultimateActivationsPerPlayer.TryGetValue(playerId, out int ultimates) ? ultimates : 0,
                        Saves = _savesPerPlayer.TryGetValue(playerId, out int saves) ? saves : 0,
                        MeterCharge = 0
                    };
                    AddAbilityStats(stats);

                    if (statsTracked && StatsMod.PlayerTracker.Instance.TryGetPlayer(player, out var data))
                    {
                        stats.Deaths = data.Deaths;
                        stats.LavaDeaths = data.LavaDeaths;
                        stats.Kills = data.Kills;
                        stats.FriendlyKills = data.FriendlyKills;
                        stats.ShotsFired = data.ShootingAccuracy.ShotsFired;
                        stats.ShotsHit = data.ShootingAccuracy.ShotsHit;
                        stats.AliveSeconds = (int)data.GetCurrentAliveTime().TotalSeconds;
                        stats.KillsDuringAbility = data.KillsDuringAbility;
                        foreach (var entry in data.DeathCauses) stats.DeathCauses[entry.Key] = entry.Value;
                        if (data.EnemyKills != null)
                        {
                            foreach (var entry in data.EnemyKills.Where(entry => entry.Value > 0))
                            {
                                stats.EnemyKills[entry.Key] = entry.Value;
                            }
                        }
                        if (data.WeaponHits != null)
                        {
                            foreach (var entry in data.WeaponHits.Where(entry => entry.Value > 0))
                            {
                                stats.WeaponHits[entry.Key] = entry.Value;
                            }
                        }
                        AddWeapons(stats, data);
                    }

                    snapshot.PlayerStats.Add(stats);
                }
            }

            // A pad that left before the end still counts its ability and ultimate uses
            foreach (int playerId in _abilityActivationsPerPlayer.Keys.Union(_ultimateActivationsPerPlayer.Keys).Union(_savesPerPlayer.Keys))
            {
                if (snapshot.PlayerStats.Exists(stats => stats.PlayerId == playerId)) continue;
                var stats = new PlayerStats
                {
                    PlayerId = playerId,
                    AbilityActivationCount = _abilityActivationsPerPlayer.TryGetValue(playerId, out int abilities) ? abilities : 0,
                    UltimateActivationCount = _ultimateActivationsPerPlayer.TryGetValue(playerId, out int ultimates) ? ultimates : 0,
                    Saves = _savesPerPlayer.TryGetValue(playerId, out int saves) ? saves : 0,
                    MeterCharge = 0
                };
                AddAbilityStats(stats);
                snapshot.PlayerStats.Add(stats);
            }

            // A plain Survival run is only reported to telemetry; the local report and Surge's stats and titles stay with Surge runs
            LastFinished = snapshot;
            if (_surgeRun && ModConfig.EnableStatsLogging) SpiderSurgeLogger.Instance.LogMatchStats(snapshot);

            if (ModConfig.TelemetryEnabled && ShouldUploadTelemetryFromThisClient())
            {
                Logger.LogInfo("SpiderSurge telemetry upload scheduled for this match.");
                SendPendingTelemetry();
                _pendingTelemetry = snapshot;
            }
            else
            {
                if (!ModConfig.TelemetryEnabled)
                {
                    Logger.LogInfo("SpiderSurge telemetry skipped: telemetry is disabled in config.");
                }
                else
                {
                    Logger.LogInfo("SpiderSurge telemetry skipped: this client is not host/server.");
                }
            }
        }

        // Stats works out the titles in its StopGameMode prefix, so the report goes out once the run has fully stopped
        public void SendPendingTelemetry()
        {
            var snapshot = _pendingTelemetry;
            if (snapshot == null) return;
            _pendingTelemetry = null;
            snapshot.Titles = BuildTitlesReport();
            SpiderSurgeTelemetryUploader.Instance.QueueAndSendSnapshot(snapshot);
        }

        private TitlesReport BuildTitlesReport()
        {
            if (!Parts.Stats.Running) return null;
            var titles = StatsMod.TitlesManager.Instance;
            if (titles.CalculationCount == _titleCalculationsAtStart) return null;
            var earned = titles.AllTitles;
            if (earned.Count == 0) return null;

            var onScreen = new HashSet<StatsMod.TitleEntry>(titles.CurrentTitles);
            return new TitlesReport
            {
                Shown = titles.LastTitlesShown,
                NewlyUnlocked = titles.LastNewlyUnlocked,
                UnlockedTotal = StatsMod.TitleCollection.Readable ? StatsMod.TitleCollection.Unlocked.Count : (int?)null,
                CatalogSize = StatsMod.TitleCatalog.All.Count,
                Earned = earned.Select(title => new EarnedTitle { Name = title.TitleName, PlayerId = title.PlayerId, OnScreen = onScreen.Contains(title) }).ToList()
            };
        }

        public void LogAbilityActivation(int playerId)
        {
            if (!_isTracking) return;

            if (!_abilityActivationsPerPlayer.ContainsKey(playerId))
            {
                _abilityActivationsPerPlayer[playerId] = 0;
            }
            _abilityActivationsPerPlayer[playerId]++;
            if (_currentWaveRecord != null)
            {
                _currentWaveRecord.AbilityUses++;
                WavePlayer(playerId).AbilityUses++;
            }
        }

        public void LogUltimateActivation(int playerId)
        {
            if (!_isTracking) return;

            if (!_ultimateActivationsPerPlayer.ContainsKey(playerId))
            {
                _ultimateActivationsPerPlayer[playerId] = 0;
            }
            _ultimateActivationsPerPlayer[playerId]++;
            if (!_firstUltimateWave.ContainsKey(playerId)) _firstUltimateWave[playerId] = _currentWaveRecord != null ? _currentWaveRecord.Wave : _currentWave;
            if (_currentWaveRecord != null)
            {
                _currentWaveRecord.UltimateUses++;
                WavePlayer(playerId).UltimateUses++;
            }
        }

        public void LogAbilityEffect(int playerId, string effect, int amount)
        {
            if (!_isTracking) return;

            if (!_abilityEffects.TryGetValue(playerId, out var effects)) _abilityEffects[playerId] = effects = new Dictionary<string, int>();
            effects.TryGetValue(effect, out int count);
            effects[effect] = count + amount;
        }

        // The host runs every player's ability, so it samples each one through the wave: ready (alive, off cooldown, unused) or running
        private void Update()
        {
            if (!_isTracking || !_surgeRun || _currentWaveRecord == null) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            foreach (var controller in PlayerAbilityHandler.ActiveSpiderControllers)
            {
                if (controller == null) continue;
                controller.GetComponents(_abilityBuffer);
                foreach (var ability in _abilityBuffer)
                {
                    if (!ability.IsRegularUnlocked()) continue;
                    if (!ability.IsPlayerAlive()) break;
                    int playerId = ability.PlayerId;
                    if (ability.StatusSource.IsActive())
                    {
                        _activeSeconds.TryGetValue(playerId, out float active);
                        _activeSeconds[playerId] = active + dt;
                    }
                    else if (!ability.StatusSource.IsOnCooldown() && ChallengeController.CanActivate(false))
                    {
                        _readySeconds.TryGetValue(playerId, out float ready);
                        _readySeconds[playerId] = ready + dt;
                        WavePlayer(playerId).ReadySeconds += dt;
                    }
                    break;
                }
            }
        }

        private WavePlayerRecord WavePlayer(int playerId)
        {
            if (!_wavePlayers.TryGetValue(playerId, out var record)) _wavePlayers[playerId] = record = new WavePlayerRecord { PlayerId = playerId };
            return record;
        }

        private void AddAbilityStats(PlayerStats stats)
        {
            int playerId = stats.PlayerId;
            stats.ReadySeconds = _readySeconds.TryGetValue(playerId, out float ready) ? ready : 0f;
            stats.ActiveSeconds = _activeSeconds.TryGetValue(playerId, out float active) ? active : 0f;
            stats.FirstUltimateWave = _firstUltimateWave.TryGetValue(playerId, out int wave) ? wave : -1;
            if (_abilityEffects.TryGetValue(playerId, out var effects))
            {
                foreach (var entry in effects) stats.AbilityEffects[entry.Key] = entry.Value;
            }
        }

        public WaveRecord CurrentWaveRecord => _isTracking ? _currentWaveRecord : null;

        public void LogMeterPurchase(ShopItem item, string detail)
        {
            if (!_isTracking) return;

            // Numbered like the perk choice it was bought on, which is the wave that just ended
            int wave = _pendingPerkChoice != null ? _pendingPerkChoice.Wave
                : SurvivalMode.instance != null ? SurvivalMode.instance.CurrentWave : _currentWave;
            _meterPurchases.Add(new MeterPurchaseRecord
            {
                Wave = wave,
                Item = item.ToString(),
                Detail = detail
            });
        }

        // The offer on screen is replaced by new cards, so it closes as rerolled and the new offer opens as its own record
        public void OnPerkChoiceRerolled()
        {
            if (!_isTracking || _pendingPerkChoice == null || _pendingPerkSubmitted) return;

            _pendingPerkChoice.Rerolled = true;
            _pendingPerkChoice.DecisionSeconds = Time.realtimeSinceStartup - _pendingPerkOfferedAt;
            _pendingPerkSubmitted = true;
        }

        private MeterReport BuildMeterReport()
        {
            var report = new MeterReport
            {
                ChargePerPoint = 0,
                TotalCharge = 0,
                PointsEarned = ChaosWallet.PointsEarned,
                PointsSpent = ChaosWallet.PointsSpent,
                Events = new List<ChaosEventRecord>(ChaosWallet.Events),
                Purchases = new List<MeterPurchaseRecord>(_meterPurchases)
            };
            return report;
        }

        private static void AddWeapons(PlayerStats stats, StatsMod.PlayerTracker.PlayerData data)
        {
            foreach (var entry in data.UseByWeapon)
            {
                if (entry.Value.Kills == 0 && entry.Value.Shields == 0 && entry.Value.Pickups == 0) continue;
                stats.Weapons[entry.Key] = new WeaponRecord { Kills = entry.Value.Kills, Shields = entry.Value.Shields, Pickups = entry.Value.Pickups };
            }
            foreach (var entry in data.AccuracyByWeapon)
            {
                if (entry.Value.ShotsFired == 0) continue;
                if (!stats.Weapons.TryGetValue(entry.Key, out WeaponRecord record)) stats.Weapons[entry.Key] = record = new WeaponRecord();
                record.Fired = entry.Value.ShotsFired;
                record.Hit = entry.Value.ShotsHit;
            }
        }

        // A Null Field that blocked a hit while the spider had no shield, so the hit would have killed it; it counts in the wave the hit landed in
        public void LogAbilitySave(int playerId, WaveRecord wave)
        {
            if (!_isTracking) return;

            _savesPerPlayer.TryGetValue(playerId, out int saves);
            _savesPerPlayer[playerId] = saves + 1;
            if (wave != null && (wave == _currentWaveRecord || _waves.Contains(wave))) wave.Saves++;
        }

        public void IncrementWave()
        {
            if (_isTracking && _currentWaveRecord != null)
            {
                _currentWave++;
                CloseWave(WaveResults.Won);
            }
        }

        public void OnWaveStarted()
        {
            if (!_isTracking || SurvivalMode.instance == null) return;

            CloseWave(WaveResults.Reset);
            _currentWaveRecord = new WaveRecord
            {
                Wave = SurvivalMode.instance.CurrentWave,
                Map = Maps.SurgeMaps.MapKey(SceneManager.GetActiveScene().name),
                LivesAtStart = SurvivalMode.instance.Lives,
                Deaths = _countingDeaths ? 0 : -1
            };
            _currentWaveStartTime = Time.time;
            _wavePlayers.Clear();
            _waveStartTotals.Clear();
            AbilityEffects.Clear();
            ForEachTrackedPlayer((playerId, data) => _waveStartTotals[playerId] = (data.Kills, data.Deaths, (float)data.GetCurrentAliveTime().TotalSeconds));
        }

        public void OnWaveReset()
        {
            if (_isTracking)
            {
                CloseWave(WaveResults.Reset);
            }
        }

        public void OnWaveFailed()
        {
            if (!_isTracking) return;

            CloseWave(WaveResults.Lost);
            if (SurvivalMode.instance != null && SurvivalMode.instance.Lives <= 0)
            {
                _livesOut = true;
            }
        }

        public void OnRestartRequested()
        {
            if (_isTracking)
            {
                _restartRequested = true;
            }
        }

        internal void OnPersonalChoice(int playerId, int wave, bool ultimate, IEnumerable<string> offered, string picked, float seconds)
        {
            if (!_isTracking) return;
            _perkChoices.Add(new PerkChoiceRecord {
                PlayerId = playerId, Wave = wave, Kind = ultimate ? PerkChoiceKinds.Ultimate : PerkChoiceKinds.Ability,
                Offered = offered.ToList(), Picked = picked, Level = 1, DecisionSeconds = seconds
            });
        }

        public void OnPerkChoicesShown(List<Modifier> modifiers, List<GameLevel> levels)
        {
            if (!_isTracking || modifiers == null) return;

            int? rerolledWave = PerkShop.RerollInProgress && _pendingPerkChoice != null ? _pendingPerkChoice.Wave : (int?)null;
            RecordUnansweredPerkChoice();

            var lucky = ModifierManager_GetNonMaxedSurvivalMods_Patch.LuckyPerkKeys;
            var offered = new List<string>();
            var offeredMaps = new List<string>();
            for (int i = 0; i < modifiers.Count; i++)
            {
                if (modifiers[i]?.data == null) continue;
                offered.Add(modifiers[i].data.key);
                offeredMaps.Add(levels != null && i < levels.Count ? Maps.SurgeMaps.LevelKey(levels[i]) : "");
            }

            _pendingPerkChoice = new PerkChoiceRecord
            {
                Wave = rerolledWave ?? (SurvivalMode.instance != null ? SurvivalMode.instance.CurrentWave : _currentWave),
                Kind = _surgeRun ? GetPerkChoiceKind(PerksManager.Instance) : PerkChoiceKinds.Perk,
                Offered = offered,
                OfferedMaps = offeredMaps,
                LuckyOffered = offered.Where(key => lucky.Contains(key)).ToList(),
                ShopOffer = ChaosWallet.Enabled ? PerkShop.Offer.Select(item => item.ToString()).ToList() : new List<string>()
            };
            _pendingPerkOfferedAt = Time.realtimeSinceStartup;
            _pendingPerkSubmitted = false;
        }

        // The pick is submitted before the game's 0.25 s delay, so the decision time and timeout are read here
        public void OnPerkSubmitted()
        {
            if (!_isTracking || _pendingPerkChoice == null || _pendingPerkSubmitted) return;

            _pendingPerkSubmitted = true;
            _pendingPerkChoice.DecisionSeconds = Time.realtimeSinceStartup - _pendingPerkOfferedAt;
            _pendingPerkChoice.TimedOut = PerkChoiceTimerRanOut();
        }

        public void OnPerkChosen(Modifier modifier)
        {
            if (!_isTracking || _pendingPerkChoice == null || modifier?.data == null) return;

            OnPerkSubmitted();
            var choice = _pendingPerkChoice;
            _pendingPerkChoice = null;
            choice.Picked = modifier.data.key;
            choice.Level = modifier.levelInSurvival;
            _perkChoices.Add(choice);
        }

        internal static bool PerkChoiceTimerRanOut()
        {
            if (AbilitySelection.IsChoosing || ModConfig.UnlimitedPerkChoosingTime || SurvivalModeHud.instance == null) return false;

            try
            {
                return PerkChoiceTimerRef(SurvivalModeHud.instance).Value == 0;
            }
            catch
            {
                return false;
            }
        }

        private void RecordUnansweredPerkChoice()
        {
            if (_pendingPerkChoice == null) return;

            if (!_pendingPerkSubmitted)
            {
                _pendingPerkChoice.DecisionSeconds = Time.realtimeSinceStartup - _pendingPerkOfferedAt;
            }
            _perkChoices.Add(_pendingPerkChoice);
            _pendingPerkChoice = null;
        }

        private void CloseWave(string result)
        {
            if (_currentWaveRecord == null) return;

            var wave = _currentWaveRecord;
            _currentWaveRecord = null;
            wave.Result = result;
            wave.Seconds = Time.time - _currentWaveStartTime;
            // Stats' running totals minus those at the wave's start give each player's share of it
            ForEachTrackedPlayer((playerId, data) =>
            {
                if (!_waveStartTotals.TryGetValue(playerId, out var start)) return;
                var record = WavePlayer(playerId);
                record.Kills = data.Kills - start.Kills;
                record.Deaths = data.Deaths - start.Deaths;
                record.AliveSeconds = Mathf.Max(0f, (float)data.GetCurrentAliveTime().TotalSeconds - start.Alive);
            });
            wave.Players = _wavePlayers.Values.OrderBy(record => record.PlayerId).ToList();
            _wavePlayers.Clear();
            _waveStartTotals.Clear();
            _waves.Add(wave);
        }

        private static void ForEachTrackedPlayer(Action<int, StatsMod.PlayerTracker.PlayerData> action)
        {
            var players = Players.All;
            if (!Parts.Stats.Running || players == null) return;
            foreach (var player in players)
            {
                if (player != null && StatsMod.PlayerTracker.Instance.TryGetPlayer(player, out var data)) action(player.playerID.Value, data);
            }
        }

        private void SetDeathCounting(bool enabled)
        {
            if (_countingDeaths == enabled) return;

            _countingDeaths = enabled;
            if (enabled)
            {
                StatsMod.PlayerTracker.Instance.DeathCountChanged += OnTrackedDeathCountChanged;
            }
            else
            {
                StatsMod.PlayerTracker.Instance.DeathCountChanged -= OnTrackedDeathCountChanged;
            }
        }

        private void OnTrackedDeathCountChanged(int change)
        {
            if (_currentWaveRecord != null && _currentWaveRecord.Deaths >= 0)
            {
                _currentWaveRecord.Deaths = Math.Max(0, _currentWaveRecord.Deaths + change);
            }
        }

        // Same order as the Surge perk screens are built in, so the kind matches the cards shown
        private static string GetPerkChoiceKind(PerksManager perksManager)
        {
            if (perksManager == null) return PerkChoiceKinds.Perk;
            if (perksManager.IsUltUpgradePerkSelection) return PerkChoiceKinds.Ultimate;
            if (perksManager.IsFirstNormalPerkSelection) return PerkChoiceKinds.Ability;
            return PerkChoiceKinds.Perk;
        }

        public static void RunSafely(string hook, Action<SpiderSurgeStatsManager> action)
        {
            try
            {
                action(Instance);
            }
            catch (Exception ex)
            {
                Logger.LogError($"SpiderSurge stats failed in {hook}: {ex}");
            }
        }

        private static int GetMaxLives()
        {
            try
            {
                return SurvivalMode.instance != null && ModifierManager.instance != null ? SurvivalMode.instance.MaxLives : 0;
            }
            catch
            {
                return 0;
            }
        }

        private int GetWavesSurvived()
        {
            return _currentWave;
        }

        private int GetCurrentPainLevel()
        {
            try
            {
                if (SurvivalModeHud.instance != null)
                {
                    return Math.Max(1, SurvivalModeHud.instance.currentPainLevel.Value);
                }
            }
            catch
            {
            }

            return 1;
        }

        private bool ShouldUploadTelemetryFromThisClient()
        {
            if (!ModConfig.TelemetryEnabled) return false;

            if (NetworkManager.Singleton == null)
            {
                return true;
            }

            return NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsServer;
        }

        private List<string> GetGlobalPerks()
        {
            List<string> perks = new List<string>();

            // Add Surge Perks
            if (PerksManager.Instance != null)
            {
                foreach (var perkName in PerksManager.Instance.GetAllPerkNames())
                {
                    if (PerksManager.Instance.GetPerkLevel(perkName) > 0)
                    {
                        perks.Add(perkName + $" (Lvl {PerksManager.Instance.GetPerkLevel(perkName)})");
                    }
                }
            }

            // Add Vanilla Perks (Modifiers)
            var modManager = FindObjectOfType<ModifierManager>();
            if (modManager != null)
            {
                if (_modifierManagerFields == null)
                {
                    _modifierManagerFields = typeof(ModifierManager).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                }
                foreach (var field in _modifierManagerFields)
                {
                    if (typeof(List<Modifier>).IsAssignableFrom(field.FieldType) || typeof(Modifier[]).IsAssignableFrom(field.FieldType))
                    {
                        var collection = field.GetValue(modManager);
                        if (collection is IEnumerable<Modifier> mods)
                        {
                            foreach (var mod in mods)
                            {
                                if (mod != null && mod.levelInSurvival > 0)
                                {
                                    // Avoid duplicates with Surge perks if they are also registered as modifiers
                                    if (PerksManager.Instance != null && PerksManager.Instance.GetAllPerkNames().Contains(mod.data.key))
                                        continue;

                                    perks.Add(mod.data.key + $" (Lvl {mod.levelInSurvival})");
                                }
                            }
                            if (perks.Count > 0) break;
                        }
                    }
                }
            }

            return perks;
        }

    }

    [HarmonyPatch(typeof(SurvivalMode), nameof(SurvivalMode.ResetWaveIntoRandomLevel))]
    public class SurvivalMode_ResetWaveIntoRandomLevel_Patch
    {
        [HarmonyPrefix]
        public static void Prefix()
        {
            SpiderSurgeStatsManager.RunSafely("ResetWaveIntoRandomLevel", stats => stats.OnWaveReset());
        }
    }

    [HarmonyPatch(typeof(SurvivalMode), nameof(SurvivalMode.ResetWaveIntoCurrentLevel))]
    public class SurvivalMode_ResetWaveIntoCurrentLevel_Patch
    {
        [HarmonyPrefix]
        public static void Prefix()
        {
            SpiderSurgeStatsManager.RunSafely("ResetWaveIntoCurrentLevel", stats => stats.OnWaveReset());
        }
    }

    [HarmonyPatch(typeof(SurvivalMode), nameof(SurvivalMode.Restart))]
    public class SurvivalMode_Restart_Patch
    {
        [HarmonyPrefix]
        public static void Prefix()
        {
            SpiderSurgeStatsManager.RunSafely("Restart", stats => stats.OnRestartRequested());
        }
    }

    [HarmonyPatch(typeof(SurvivalModeHud), nameof(SurvivalModeHud.ShowPerkChoices))]
    public class SurvivalModeHud_ShowPerkChoices_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(List<Modifier> modifiers, List<GameLevel> gameLevels, bool __runOriginal)
        {
            if (!__runOriginal) return;
            SpiderSurgeStatsManager.RunSafely("ShowPerkChoices", stats => stats.OnPerkChoicesShown(modifiers, gameLevels));
        }
    }

}
