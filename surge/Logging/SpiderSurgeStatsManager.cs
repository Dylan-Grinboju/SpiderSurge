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
                    }

                    snapshot.PlayerStats.Add(stats);
                }
            }

            // A pad that left before the end still counts its ability and ultimate uses
            foreach (int playerId in _abilityActivationsPerPlayer.Keys.Union(_ultimateActivationsPerPlayer.Keys).Union(_savesPerPlayer.Keys))
            {
                if (snapshot.PlayerStats.Exists(stats => stats.PlayerId == playerId)) continue;
                snapshot.PlayerStats.Add(new PlayerStats
                {
                    PlayerId = playerId,
                    AbilityActivationCount = _abilityActivationsPerPlayer.TryGetValue(playerId, out int abilities) ? abilities : 0,
                    UltimateActivationCount = _ultimateActivationsPerPlayer.TryGetValue(playerId, out int ultimates) ? ultimates : 0,
                    Saves = _savesPerPlayer.TryGetValue(playerId, out int saves) ? saves : 0,
                    MeterCharge = 0
                });
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
            if (_currentWaveRecord != null) _currentWaveRecord.AbilityUses++;
        }

        public void LogUltimateActivation(int playerId)
        {
            if (!_isTracking) return;

            if (!_ultimateActivationsPerPlayer.ContainsKey(playerId))
            {
                _ultimateActivationsPerPlayer[playerId] = 0;
            }
            _ultimateActivationsPerPlayer[playerId]++;
            if (_currentWaveRecord != null) _currentWaveRecord.UltimateUses++;
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
                Map = SceneManager.GetActiveScene().name,
                LivesAtStart = SurvivalMode.instance.Lives,
                Deaths = _countingDeaths ? 0 : -1
            };
            _currentWaveStartTime = Time.time;
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

        public void OnPerkChoicesShown(List<Modifier> modifiers)
        {
            if (!_isTracking || modifiers == null) return;

            int? rerolledWave = PerkShop.RerollInProgress && _pendingPerkChoice != null ? _pendingPerkChoice.Wave : (int?)null;
            RecordUnansweredPerkChoice();

            var lucky = ModifierManager_GetNonMaxedSurvivalMods_Patch.LuckyPerkKeys;
            var offered = modifiers.Where(mod => mod?.data != null).Select(mod => mod.data.key).ToList();

            _pendingPerkChoice = new PerkChoiceRecord
            {
                Wave = rerolledWave ?? (SurvivalMode.instance != null ? SurvivalMode.instance.CurrentWave : _currentWave),
                Kind = _surgeRun ? GetPerkChoiceKind(PerksManager.Instance) : PerkChoiceKinds.Perk,
                Offered = offered,
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
            _waves.Add(wave);
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
            if (perksManager.IsUltSwapPerkSelection) return PerkChoiceKinds.Swap;
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

    [HarmonyPatch(typeof(SurvivalMode), "StartGame")]
    public class SurvivalMode_StartGame_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(bool __result)
        {
            if (__result)
            {
                SpiderSurgeStatsManager.RunSafely("StartGame", stats => stats.StartTracking());
            }
        }
    }

    [HarmonyPatch(typeof(SurvivalMode), "StopGameMode")]
    public class SurvivalMode_StopGameMode_Patch
    {
        [HarmonyPrefix]
        public static void Prefix(SurvivalMode __instance)
        {
            if (__instance.GameModeActive())
            {
                SpiderSurgeStatsManager.RunSafely("StopGameMode", stats => stats.StopTrackingAndLog());
            }
        }

        [HarmonyPostfix]
        public static void Postfix()
        {
            SpiderSurgeStatsManager.RunSafely("StopGameMode", stats => stats.SendPendingTelemetry());
        }
    }

    [HarmonyPatch]
    public class SurvivalMode_CompleteWave_Patch
    {
        private static readonly AccessTools.FieldRef<SurvivalMode, bool> WaveInProgress =
            AccessTools.FieldRefAccess<SurvivalMode, bool>("_waveInProgress");

        private static System.Reflection.MethodBase TargetMethod() => AccessTools.EnumeratorMoveNext(AccessTools.Method(typeof(SurvivalMode), "CompleteWave"));

        [HarmonyPrefix]
        public static void Prefix()
        {
            if (!Net.IsAuthority || SurvivalMode.instance == null || !WaveInProgress(SurvivalMode.instance)) return;
            SpiderSurgeStatsManager.RunSafely("CompleteWave", stats => stats.IncrementWave());
        }
    }

    // The game calls StartWave every frame while a wave can't start yet (SpiderInspector's wave freeze), so a record opens only when the wave really starts
    [HarmonyPatch(typeof(SurvivalMode), "StartWave")]
    public class SurvivalMode_StartWave_Patch
    {
        private static readonly AccessTools.FieldRef<SurvivalMode, bool> WaveInProgressRef =
            AccessTools.FieldRefAccess<SurvivalMode, bool>("_waveInProgress");

        [HarmonyPrefix]
        public static void Prefix(SurvivalMode __instance, out bool __state)
        {
            __state = WaveInProgressRef(__instance);
        }

        [HarmonyPostfix]
        public static void Postfix(SurvivalMode __instance, bool __state)
        {
            if (!__state && WaveInProgressRef(__instance))
            {
                SpiderSurgeStatsManager.RunSafely("StartWave", stats => stats.OnWaveStarted());
            }
        }
    }

    [HarmonyPatch(typeof(SurvivalMode), "FailWave")]
    public class SurvivalMode_FailWave_Patch
    {
        [HarmonyPostfix]
        public static void Postfix()
        {
            SpiderSurgeStatsManager.RunSafely("FailWave", stats => stats.OnWaveFailed());
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
        public static void Postfix(List<Modifier> modifiers)
        {
            SpiderSurgeStatsManager.RunSafely("ShowPerkChoices", stats => stats.OnPerkChoicesShown(modifiers));
        }
    }

    [HarmonyPatch(typeof(SurvivalModeHud), nameof(SurvivalModeHud.PerkChoiceSubmit))]
    public class SurvivalModeHud_PerkChoiceSubmit_Patch
    {
        [HarmonyPrefix]
        public static void Prefix()
        {
            SpiderSurgeStatsManager.RunSafely("PerkChoiceSubmit", stats => stats.OnPerkSubmitted());
        }
    }

    [HarmonyPatch(typeof(SurvivalMode), nameof(SurvivalMode.ChoosePerkAndLevel))]
    public class SurvivalMode_ChoosePerkAndLevel_Patch
    {
        [HarmonyPrefix]
        public static void Prefix(NetworkVariable<bool> ____waitingForPerk, out bool __state)
        {
            __state = ____waitingForPerk != null && ____waitingForPerk.Value;
        }

        [HarmonyPostfix]
        public static void Postfix(Modifier modifier, bool __state)
        {
            if (__state)
            {
                SpiderSurgeStatsManager.RunSafely("ChoosePerkAndLevel", stats => stats.OnPerkChosen(modifier));
            }
        }
    }

}
