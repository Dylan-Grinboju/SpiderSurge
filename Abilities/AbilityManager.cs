using UnityEngine;
using UnityEngine.InputSystem;
using HarmonyLib;
using System.Collections.Generic;
using SpiderSurge.Abilities;
using Logger = Silk.Logger;

namespace SpiderSurge
{
    public class AbilityManager : MonoBehaviour
    {
        public static AbilityManager Instance { get; private set; }

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                Logger.LogInfo("AbilityManager initialized");
            }
            else
            {
                Destroy(gameObject);
            }
        }

        public static void InitializePlayerAbilities(GameObject playerObject)
        {
            if (playerObject == null) return;

            try
            {
                // Check if abilities should be enabled
                if (!SurgeModeManager.AbilitiesEnabled)
                {
                    Logger.LogInfo("Abilities disabled - skipping initialization");
                    return;
                }

                PlayerInput playerInput = playerObject.GetComponentInParent<PlayerInput>();
                if (playerInput == null)
                {
                    Logger.LogWarning("Could not find PlayerInput component on player object");
                    return;
                }

                // Find the SpiderController (this is where we want to add our components)
                SpiderController spiderController = playerObject.GetComponent<SpiderController>();
                if (spiderController == null)
                {
                    Logger.LogWarning("Could not find SpiderController component on player object");
                    return;
                }

                // Add InputInterceptor if it doesn't exist
                if (spiderController.GetComponent<InputInterceptor>() == null)
                {
                    spiderController.gameObject.AddComponent<InputInterceptor>();
                }

                // Add TempShield if it doesn't exist
                if (spiderController.GetComponent<TempShield>() == null)
                {
                    spiderController.gameObject.AddComponent<TempShield>();
                }


            }
            catch (System.Exception ex)
            {
                Logger.LogError($"Error initializing player abilities: {ex.Message}");
            }
        }

        public static void EnableAllAbilities()
        {
            try
            {
                SpiderController[] players = FindObjectsOfType<SpiderController>();
                foreach (SpiderController player in players)
                {
                    EnablePlayerAbilities(player.gameObject);
                }
                Logger.LogInfo("All player abilities enabled");
            }
            catch (System.Exception ex)
            {
                Logger.LogError($"Error enabling all abilities: {ex.Message}");
            }
        }

        public static void DisableAllAbilities()
        {
            try
            {
                SpiderController[] players = FindObjectsOfType<SpiderController>();
                foreach (SpiderController player in players)
                {
                    DisablePlayerAbilities(player.gameObject);
                }
                Logger.LogInfo("All player abilities disabled");
            }
            catch (System.Exception ex)
            {
                Logger.LogError($"Error disabling all abilities: {ex.Message}");
            }
        }

        private static void EnablePlayerAbilities(GameObject playerObject)
        {
            if (playerObject == null) return;

            SpiderController spiderController = playerObject.GetComponent<SpiderController>();
            if (spiderController == null) return;

            var inputInterceptor = spiderController.GetComponent<InputInterceptor>();
            if (inputInterceptor != null)
                inputInterceptor.enabled = true;

            var tempShield = spiderController.GetComponent<TempShield>();
            if (tempShield != null)
                tempShield.enabled = true;
        }

        private static void DisablePlayerAbilities(GameObject playerObject)
        {
            if (playerObject == null) return;

            SpiderController spiderController = playerObject.GetComponent<SpiderController>();
            if (spiderController == null) return;

            var inputInterceptor = spiderController.GetComponent<InputInterceptor>();
            if (inputInterceptor != null)
                inputInterceptor.enabled = false;

            var tempShield = spiderController.GetComponent<TempShield>();
            if (tempShield != null)
                tempShield.enabled = false;
        }
    }

    // Harmony patch to initialize abilities when players spawn
    [HarmonyPatch(typeof(SpiderController), "Start")]
    public class SpiderController_Start_Patch
    {
        static void Postfix(SpiderController __instance)
        {
            try
            {
                AbilityManager.InitializePlayerAbilities(__instance.gameObject);
            }
            catch (System.Exception ex)
            {
                Logger.LogError($"Error in SpiderController Start patch: {ex.Message}");
            }
        }
    }
}
