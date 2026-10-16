using UnityEngine;
using UnityEngine.InputSystem;
using Logger = Silk.Logger;
using System.Collections.Generic;

namespace SpiderSurge
{
    public static class PlayerAbilityHandler
    {
        public static List<SpiderController> ActiveSpiderControllers = new List<SpiderController>();
        private static readonly HashSet<int> SeenPlayerSpawns = new HashSet<int>();

        public static void ResetSpawnTracking()
        {
            SeenPlayerSpawns.Clear();
        }

        public static void InitializePlayerAbilities(GameObject playerObject)
        {
            if (playerObject == null) return;

            try
            {
                if (SurgeGameModeManager.Instance == null || !SurgeGameModeManager.Instance.IsActive)
                {
                    Logger.LogWarning("Surge mode not active - skipping ability initialization");
                    return;
                }

                PlayerInput playerInput = playerObject.GetComponentInParent<PlayerInput>();
                if (playerInput == null)
                {
                    Logger.LogWarning("Could not find PlayerInput component on player object");
                    return;
                }

                int playerIndex = playerInput.playerIndex;
                bool isRespawn = SeenPlayerSpawns.Contains(playerIndex);
                SeenPlayerSpawns.Add(playerIndex);

                SpiderController spiderController = playerObject.GetComponent<SpiderController>();
                if (spiderController == null)
                {
                    Logger.LogWarning("Could not find SpiderController component on player object");
                    return;
                }

                ActiveSpiderControllers.RemoveAll(sc => sc == null);
                if (!ActiveSpiderControllers.Contains(spiderController))
                {
                    ActiveSpiderControllers.Add(spiderController);
                }

                if (spiderController.GetComponent<InputInterceptor>() == null)
                {
                    spiderController.gameObject.AddComponent<InputInterceptor>();
                }

                if (spiderController.GetComponent<ImmuneAbility>() == null)
                {
                    spiderController.gameObject.AddComponent<ImmuneAbility>();
                }

                var immuneAbility = spiderController.GetComponent<ImmuneAbility>();
                if (isRespawn && immuneAbility != null && immuneAbility.IsUnlocked())
                {
                    immuneAbility.ForceStartCooldown();
                }

                if (spiderController.GetComponent<AmmoAbility>() == null)
                {
                    spiderController.gameObject.AddComponent<AmmoAbility>();
                }

                if (spiderController.GetComponent<PulseAbility>() == null)
                {
                    spiderController.gameObject.AddComponent<PulseAbility>();
                }

                if (spiderController.GetComponent<StorageAbility>() == null)
                {
                    spiderController.gameObject.AddComponent<StorageAbility>();
                }
            }
            catch (System.Exception ex)
            {
                Logger.LogError($"Error initializing player abilities: {ex.Message}");
            }
        }
    }
}
