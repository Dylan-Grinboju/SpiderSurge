using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
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
        private readonly List<PerkChoiceRecord> _perkChoices = new List<PerkChoiceRecord>();
        private readonly List<WaveRecord> _waves = new List<WaveRecord>();
        private PerkChoiceRecord _pendingPerkChoice;
        private WaveRecord _currentWaveRecord;
        private DateTime _matchStartTime;
        private bool _isTracking = false;
        private bool _livesOut = false;
        private bool _restartRequested = false;
        private int _currentWave = 0;
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

        public void StartTracking()
        {
            if (!ModConfig.EnableStatsLogging) return;

            _matchStartTime = DateTime.Now;
            _abilityActivationsPerPlayer.Clear();
            _ultimateActivationsPerPlayer.Clear();
            _perkChoices.Clear();
            _waves.Clear();
            _pendingPerkChoice = null;
            _currentWaveRecord = null;
            _livesOut = false;
            _restartRequested = false;
            _isTracking = true;
            _currentWave = 0;
            Logger.LogInfo("SpiderSurge stats tracking started.");
        }

        public void StopTrackingAndLog()
        {
            if (!_isTracking) return;

            CloseWave(WaveResults.Quit);
            _isTracking = false;
            TimeSpan duration = DateTime.Now - _matchStartTime;

            bool statsTracked = Parts.Stats.Running;
            var trackedPlayers = statsTracked ? StatsMod.PlayerTracker.Instance.GetActivePlayers() : null;

            var snapshot = new SpiderSurgeStatsSnapshot
            {
                MatchDuration = duration,
                PlayerCount = PlayerInput.all.Count,
                WavesSurvived = GetWavesSurvived(),
                PainLevel = GetCurrentPainLevel(),
                GlobalPerks = GetGlobalPerks(),
                EndReason = _livesOut ? EndReasons.LivesOut : _restartRequested ? EndReasons.Restart : EndReasons.Quit,
                LivesLeft = SurvivalMode.instance != null ? SurvivalMode.instance.Lives : 0,
                MaxLives = GetMaxLives(),
                StatsTracked = statsTracked,
                UnlimitedPerkTime = ModConfig.UnlimitedPerkChoosingTime,
                PerkChoices = new List<PerkChoiceRecord>(_perkChoices),
                Waves = new List<WaveRecord>(_waves)
            };

            foreach (var player in PlayerInput.all)
            {
                int pIndex = player.playerIndex;
                var stats = new PlayerStats
                {
                    PlayerIndex = pIndex,
                    AbilityActivationCount = _abilityActivationsPerPlayer.TryGetValue(pIndex, out int abilities) ? abilities : 0,
                    UltimateActivationCount = _ultimateActivationsPerPlayer.TryGetValue(pIndex, out int ultimates) ? ultimates : 0
                };

                if (trackedPlayers != null && trackedPlayers.TryGetValue(player, out var data))
                {
                    stats.Deaths = data.Deaths;
                    stats.LavaDeaths = data.LavaDeaths;
                    stats.Kills = data.Kills;
                    stats.FriendlyKills = data.FriendlyKills;
                    stats.ShotsFired = data.ShootingAccuracy.ShotsFired;
                    stats.ShotsHit = data.ShootingAccuracy.ShotsHit;
                    stats.AliveSeconds = (int)data.GetCurrentAliveTime().TotalSeconds;
                    if (data.EnemyKills != null)
                    {
                        foreach (var entry in data.EnemyKills.Where(entry => entry.Value > 0))
                        {
                            stats.EnemyKills[entry.Key] = entry.Value;
                        }
                    }
                }

                snapshot.PlayerStats.Add(stats);
            }

            SpiderSurgeLogger.Instance.LogMatchStats(snapshot);

            Integration.StatsModBridge.SendSurgeStats(snapshot);
            Integration.StatsModBridge.SendSurgeTitles(snapshot);

            if (ShouldUploadTelemetryFromThisClient())
            {
                Logger.LogInfo("SpiderSurge telemetry upload scheduled for this match.");
                SpiderSurgeTelemetryUploader.Instance.QueueAndSendSnapshot(snapshot);
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

        public void LogAbilityActivation(int playerIndex)
        {
            if (!_isTracking) return;

            if (!_abilityActivationsPerPlayer.ContainsKey(playerIndex))
            {
                _abilityActivationsPerPlayer[playerIndex] = 0;
            }
            _abilityActivationsPerPlayer[playerIndex]++;
        }

        public void LogUltimateActivation(int playerIndex)
        {
            if (!_isTracking) return;

            if (!_ultimateActivationsPerPlayer.ContainsKey(playerIndex))
            {
                _ultimateActivationsPerPlayer[playerIndex] = 0;
            }
            _ultimateActivationsPerPlayer[playerIndex]++;
        }

        public void IncrementWave()
        {
            if (_isTracking)
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
                StartTime = Time.time,
                DeathsAtStart = GetTotalTrackedDeaths()
            };
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

            var lucky = ModifierManager_GetNonMaxedSurvivalMods_Patch.LuckyPerkKeys;
            var offered = modifiers.Where(mod => mod?.data != null).Select(mod => mod.data.key).ToList();

            _pendingPerkChoice = new PerkChoiceRecord
            {
                Wave = SurvivalMode.instance != null ? SurvivalMode.instance.CurrentWave : _currentWave,
                Kind = GetPerkChoiceKind(PerksManager.Instance),
                Offered = offered,
                LuckyOffered = offered.Where(key => lucky.Contains(key)).ToList(),
                OfferedAtRealtime = Time.realtimeSinceStartup
            };
        }

        public void OnPerkChosen(Modifier modifier, bool timedOut)
        {
            if (!_isTracking || _pendingPerkChoice == null || modifier?.data == null) return;

            var choice = _pendingPerkChoice;
            _pendingPerkChoice = null;
            choice.Picked = modifier.data.key;
            choice.Level = modifier.levelInSurvival;
            choice.DecisionSeconds = Time.realtimeSinceStartup - choice.OfferedAtRealtime;
            choice.TimedOut = timedOut;
            _perkChoices.Add(choice);
        }

        public static bool PerkChoiceTimerRanOut()
        {
            if (ModConfig.UnlimitedPerkChoosingTime || SurvivalModeHud.instance == null) return false;

            try
            {
                return PerkChoiceTimerRef(SurvivalModeHud.instance).Value == 0;
            }
            catch
            {
                return false;
            }
        }

        private void CloseWave(string result)
        {
            if (_currentWaveRecord == null) return;

            var wave = _currentWaveRecord;
            _currentWaveRecord = null;
            wave.Result = result;
            wave.Seconds = Time.time - wave.StartTime;
            int deathsNow = GetTotalTrackedDeaths();
            if (wave.DeathsAtStart >= 0 && deathsNow >= 0)
            {
                wave.Deaths = Math.Max(0, deathsNow - wave.DeathsAtStart);
            }
            _waves.Add(wave);
        }

        private static string GetPerkChoiceKind(PerksManager perksManager)
        {
            if (perksManager == null) return PerkChoiceKinds.Perk;
            if (perksManager.IsFirstNormalPerkSelection) return PerkChoiceKinds.Ability;
            if (perksManager.IsUltUpgradePerkSelection) return PerkChoiceKinds.Ultimate;
            if (perksManager.IsUltSwapPerkSelection) return PerkChoiceKinds.Swap;
            return PerkChoiceKinds.Perk;
        }

        // Total deaths the Stats part has counted this run, or -1 when it isn't running
        private static int GetTotalTrackedDeaths()
        {
            if (!Parts.Stats.Running) return -1;
            return StatsMod.PlayerTracker.Instance.GetActivePlayers().Values.Sum(data => data.Deaths);
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
            if (__result && SurgeGameModeManager.Instance != null && SurgeGameModeManager.Instance.IsActive)
            {
                SpiderSurgeStatsManager.Instance.StartTracking();
            }
        }
    }

    [HarmonyPatch(typeof(SurvivalMode), "StopGameMode")]
    public class SurvivalMode_StopGameMode_Patch
    {
        [HarmonyPrefix]
        public static void Prefix(SurvivalMode __instance)
        {
            if (__instance.GameModeActive() && SpiderSurgeStatsManager.Instance != null)
            {
                SpiderSurgeStatsManager.Instance.StopTrackingAndLog();
            }
        }
    }

    [HarmonyPatch(typeof(SurvivalMode), "CompleteWave")]
    public class SurvivalMode_CompleteWave_Patch
    {
        [HarmonyPrefix]
        public static void Prefix()
        {
            if (SpiderSurgeStatsManager.Instance != null)
            {
                SpiderSurgeStatsManager.Instance.IncrementWave();
            }
        }
    }

    [HarmonyPatch(typeof(SurvivalMode), "StartWave")]
    public class SurvivalMode_StartWave_Patch
    {
        [HarmonyPrefix]
        public static void Prefix()
        {
            SpiderSurgeStatsManager.Instance.OnWaveStarted();
        }
    }

    [HarmonyPatch(typeof(SurvivalMode), "FailWave")]
    public class SurvivalMode_FailWave_Patch
    {
        [HarmonyPostfix]
        public static void Postfix()
        {
            SpiderSurgeStatsManager.Instance.OnWaveFailed();
        }
    }

    [HarmonyPatch(typeof(SurvivalMode), nameof(SurvivalMode.Restart))]
    public class SurvivalMode_Restart_Patch
    {
        [HarmonyPrefix]
        public static void Prefix()
        {
            SpiderSurgeStatsManager.Instance.OnRestartRequested();
        }
    }

    [HarmonyPatch(typeof(SurvivalModeHud), nameof(SurvivalModeHud.ShowPerkChoices))]
    public class SurvivalModeHud_ShowPerkChoices_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(List<Modifier> modifiers)
        {
            SpiderSurgeStatsManager.Instance.OnPerkChoicesShown(modifiers);
        }
    }

    [HarmonyPatch(typeof(SurvivalMode), nameof(SurvivalMode.ChoosePerkAndLevel))]
    public class SurvivalMode_ChoosePerkAndLevel_Patch
    {
        [HarmonyPrefix]
        public static void Prefix(out bool __state)
        {
            __state = SpiderSurgeStatsManager.PerkChoiceTimerRanOut();
        }

        [HarmonyPostfix]
        public static void Postfix(Modifier modifier, bool __state)
        {
            SpiderSurgeStatsManager.Instance.OnPerkChosen(modifier, __state);
        }
    }

}
