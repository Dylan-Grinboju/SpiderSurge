using UnityEngine;
using UnityEngine.InputSystem;
using Logger = Silk.Logger;

namespace SpiderSurge
{
    public static class PlayerAbilityHandler
    {
        public static System.Collections.Generic.List<SpiderController> ActiveSpiderControllers = new System.Collections.Generic.List<SpiderController>();

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

                if (spiderController.GetComponent<ShieldAbility>() == null)
                {
                    spiderController.gameObject.AddComponent<ShieldAbility>();
                }

                if (spiderController.GetComponent<InfiniteAmmoAbility>() == null)
                {
                    spiderController.gameObject.AddComponent<InfiniteAmmoAbility>();
                }

                if (spiderController.GetComponent<ExplosionAbility>() == null)
                {
                    spiderController.gameObject.AddComponent<ExplosionAbility>();
                }

                if (spiderController.GetComponent<InterdimensionalStorageAbility>() == null)
                {
                    spiderController.gameObject.AddComponent<InterdimensionalStorageAbility>();
                }
            }
            catch (System.Exception ex)
            {
                Logger.LogError($"Error initializing player abilities: {ex.Message}");
            }
        }
    }
}
