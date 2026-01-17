using UnityEngine;
using UnityEngine.InputSystem;
using HarmonyLib;
using System.Collections.Generic;
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
                // Check if surge mode is active
                if (!SurgeGameModeManager.Instance.IsActive)
                {
                    Logger.LogInfo("Surge mode not active - skipping ability initialization");
                    return;
                }

                PlayerInput playerInput = playerObject.GetComponentInParent<PlayerInput>();
                if (playerInput == null)
                {
                    Logger.LogWarning("Could not find PlayerInput component on player object");
                    return;
                }

                SpiderController spiderController = playerObject.GetComponent<SpiderController>();
                if (spiderController == null)
                {
                    Logger.LogWarning("Could not find SpiderController component on player object");
                    return;
                }

                if (spiderController.GetComponent<InputInterceptor>() == null)
                {
                    spiderController.gameObject.AddComponent<InputInterceptor>();
                }

                if (spiderController.GetComponent<ShieldAbility>() == null)
                {
                    ShieldAbility shield = spiderController.gameObject.AddComponent<ShieldAbility>();
                    shield.enabled = SurgeGameModeManager.Instance.IsShieldAbilityUnlocked;
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

        public static void EnableShieldAbility()
        {
            try
            {
                SurgeGameModeManager.Instance.UnlockShieldAbility();
                SpiderController[] players = FindObjectsOfType<SpiderController>();
                foreach (SpiderController player in players)
                {
                    var shieldAbility = player.GetComponent<ShieldAbility>();
                    if (shieldAbility != null)
                        shieldAbility.enabled = true;
                }
                Logger.LogInfo("Shield ability enabled for all players");
            }
            catch (System.Exception ex)
            {
                Logger.LogError($"Error enabling shield ability: {ex.Message}");
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

            var shieldAbility = spiderController.GetComponent<ShieldAbility>();
            if (shieldAbility != null)
                shieldAbility.enabled = SurgeGameModeManager.Instance.IsShieldAbilityUnlocked;
        }

        private static void DisablePlayerAbilities(GameObject playerObject)
        {
            if (playerObject == null) return;

            SpiderController spiderController = playerObject.GetComponent<SpiderController>();
            if (spiderController == null) return;

            var inputInterceptor = spiderController.GetComponent<InputInterceptor>();
            if (inputInterceptor != null)
                inputInterceptor.enabled = false;

            var shieldAbility = spiderController.GetComponent<ShieldAbility>();
            if (shieldAbility != null)
                shieldAbility.enabled = false;
        }
    }
}