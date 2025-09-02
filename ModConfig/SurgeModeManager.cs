using HarmonyLib;
using Silk;
using Logger = Silk.Logger;
using System;
using UnityEngine;
using Unity.Netcode;

namespace SpiderSurge
{
    public class SurgeModeManager : MonoBehaviour
    {
        public static SurgeModeManager Instance { get; private set; }

        private static bool _isSurgeModeActive = false;
        private static bool _abilitiesEnabled = false;
        private static bool _surgeInitiated = false; // Flag to track if surge was initiated from surge platform

        public static bool IsSurgeModeActive => _isSurgeModeActive;
        public static bool AbilitiesEnabled => _abilitiesEnabled;
        public static bool SurgeInitiated => _surgeInitiated;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                Logger.LogInfo("SurgeModeManager initialized");

                // Enable abilities by default in lobby
                // EnableAbilities();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        public static void EnableAbilities()
        {
            _abilitiesEnabled = true;
            AbilityManager.EnableAllAbilities();
        }

        public static void DisableAbilities()
        {
            _abilitiesEnabled = false;
            AbilityManager.DisableAllAbilities();
        }

        public static void StartSurgeMode()
        {
            _isSurgeModeActive = true;
            EnableAbilities();
        }

        public static void StopSurgeMode()
        {
            _isSurgeModeActive = false;
            _surgeInitiated = false; // Reset the flag when stopping
            DisableAbilities();
        }

        public static void SetSurgeInitiated()
        {
            _surgeInitiated = true;
        }

        public static void ResetSurgeInitiated()
        {
            _surgeInitiated = false;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }
    }

    // Patch to handle scene transitions and re-enable abilities in lobby
    [HarmonyPatch(typeof(UnityEngine.SceneManagement.SceneManager), "Internal_SceneLoaded")]
    public static class SurgeSceneTransitionPatch
    {
        [HarmonyPostfix]
        static void Internal_SceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            try
            {
                if (scene.name.ToLower().Contains("lobby") || scene.name.ToLower().Contains("main") || scene.name.ToLower().Contains("menu"))
                {
                    // Reset surge initiated flag when returning to lobby
                    SurgeModeManager.ResetSurgeInitiated();

                    if (SurgeModeManager.Instance != null)
                    {
                        // SurgeModeManager.EnableAbilities();
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error in SurgeSceneTransitionPatch: {ex.Message}");
            }
        }
    }

    [HarmonyPatch(typeof(SurvivalMode), "StartGame")]
    public class SurgeModeDetectionPatch
    {
        static void Postfix(SurvivalMode __instance, SurvivalConfig survivalConfig, bool __result)
        {
            try
            {
                if (__result)
                {
                    // Check if surge mode was initiated from the surge platform
                    if (SurgeModeManager.SurgeInitiated)
                    {
                        SurgeModeManager.StartSurgeMode();
                    }
                    else
                    {
                        // Regular survival mode - disable abilities
                        SurgeModeManager.DisableAbilities();
                    }

                    StatsManager.Instance.StartSurvivalSession();
                    Logger.LogInfo("Survival mode started via StatsManager");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error in SurgeModeDetectionPatch: {ex.Message}");
            }
        }
    }

    [HarmonyPatch(typeof(SurvivalMode), "StopGameMode")]
    public class SurgeModeStopPatch
    {
        static void Prefix(SurvivalMode __instance)
        {
            try
            {
                if (__instance.GameModeActive())
                {
                    if (SurgeModeManager.IsSurgeModeActive)
                    {
                        SurgeModeManager.StopSurgeMode();
                    }
                    else
                    {
                        // Re-enable abilities when returning to lobby
                        // SurgeModeManager.EnableAbilities();
                    }

                    StatsManager.Instance.StopSurvivalSession();

                    UIManager.AutoPullHUD();
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error in SurgeModeStopPatch: {ex.Message}");
            }
        }
    }

    // Patch to detect when the surge platform button activates survival mode
    [HarmonyPatch(typeof(GameModeStartButton), "ShowGameModePrompt")]
    public class SurgeButtonDetectionPatch
    {
        static void Prefix(GameModeStartButton __instance)
        {
            try
            {
                if (__instance.targetMode == GameModeStartButton.Mode.Survival)
                {
                    // Check if this button belongs to a surge platform by checking parent GameObject names
                    Transform platform = __instance.transform.parent;
                    bool isSurgePlatform = false;

                    if (platform.gameObject.name.Contains("_Duplicate_SpiderSurge"))
                    {
                        isSurgePlatform = true;
                    }

                    if (isSurgePlatform)
                    {
                        SurgeModeManager.SetSurgeInitiated();
                    }
                    else
                    {
                        // This is the regular survival platform
                        SurgeModeManager.ResetSurgeInitiated();
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error in SurgeButtonDetectionPatch: {ex.Message}");
            }
        }
    }
}
