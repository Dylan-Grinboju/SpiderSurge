using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;
using HarmonyLib;
using Logger = Silk.Logger;

namespace SpiderSurge.Logging;

public class SpiderSurgeStatsManager : MonoBehaviour
{
    private static SpiderSurgeStatsManager _instance;
    public static SpiderSurgeStatsManager Instance
    {
        get
        {
            if (_instance == null)
            {
                GameObject obj = new("SpiderSurgeStatsManager");
                _instance = obj.AddComponent<SpiderSurgeStatsManager>();
                DontDestroyOnLoad(obj);
            }
            return _instance;
        }
    }

    private readonly Dictionary<int, int> _abilityActivationsPerPlayer = [];
    private readonly Dictionary<int, int> _ultimateActivationsPerPlayer = [];
    private DateTime _matchStartTime;
    private bool _isTracking = false;
    private int _currentWave = 0;
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
        _isTracking = true;
        _currentWave = 0;
        Logger.LogInfo("SpiderSurge stats tracking started.");
    }

    public void StopTrackingAndLog()
    {
        if (!_isTracking) return;

        _isTracking = false;
        TimeSpan duration = DateTime.Now - _matchStartTime;

        // Collect stats
        var snapshot = new SpiderSurgeStatsSnapshot
        {
            MatchDuration = duration,
            PlayerCount = PlayerInput.all.Count,
            WavesSurvived = GetWavesSurvived(),
            PainLevel = GetCurrentPainLevel(),
            GlobalPerks = GetGlobalPerks()
        };

        foreach (var player in PlayerInput.all)
        {
            int pIndex = player.playerIndex;
            int abilityActivations = _abilityActivationsPerPlayer.ContainsKey(pIndex) ? _abilityActivationsPerPlayer[pIndex] : 0;
            int ultimateActivations = _ultimateActivationsPerPlayer.ContainsKey(pIndex) ? _ultimateActivationsPerPlayer[pIndex] : 0;

            snapshot.PlayerStats.Add(new PlayerStats
            {
                PlayerIndex = pIndex,
                AbilityActivationCount = abilityActivations,
                UltimateActivationCount = ultimateActivations
            });
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
        }
    }

    private int GetWavesSurvived() => _currentWave;

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
        return !ModConfig.TelemetryEnabled
            ? false
            : NetworkManager.Singleton == null ? true : NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsServer;
    }

    private List<string> GetGlobalPerks()
    {
        List<string> perks = [];

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
            var fields = typeof(ModifierManager).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            foreach (var field in fields)
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
    public static void Prefix() => SpiderSurgeStatsManager.Instance?.IncrementWave();
}
