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
                    Logger.LogInfo($"Added InputInterceptor to player {playerInput.playerIndex}");
                }

                // Add TempShield if it doesn't exist
                if (spiderController.GetComponent<TempShield>() == null)
                {
                    spiderController.gameObject.AddComponent<TempShield>();
                    Logger.LogInfo($"Added TempShield to player {playerInput.playerIndex}");
                }

                Logger.LogInfo($"Initialized abilities for player {playerInput.playerIndex}");
            }
            catch (System.Exception ex)
            {
                Logger.LogError($"Error initializing player abilities: {ex.Message}");
            }
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
