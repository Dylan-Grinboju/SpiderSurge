using UnityEngine;
using UnityEngine.InputSystem;
using Logger = Silk.Logger;
using System.Collections.Generic;

namespace SpiderSurge;

public static class PlayerAbilityHandler
{
    public static List<SpiderController> ActiveSpiderControllers = [];
    private static readonly HashSet<int> SeenPlayerSpawns = [];

    public static void ResetSpawnTracking() => SeenPlayerSpawns.Clear();

    public static void InitializePlayerAbilities(GameObject playerObject)
    {
        if (playerObject is null) return;

        try
        {
            if (SurgeGameModeManager.Instance is null || !SurgeGameModeManager.Instance.IsActive)
            {
                Logger.LogWarning("Surge mode not active - skipping ability initialization");
                return;
            }

            PlayerInput playerInput = playerObject.GetComponentInParent<PlayerInput>();
            if (playerInput is null)
            {
                Logger.LogWarning("Could not find PlayerInput component on player object");
                return;
            }

            int playerIndex = playerInput.playerIndex;
            bool isRespawn = SeenPlayerSpawns.Contains(playerIndex);
            SeenPlayerSpawns.Add(playerIndex);

            SpiderController spiderController = playerObject.GetComponent<SpiderController>();
            if (spiderController is null)
            {
                Logger.LogWarning("Could not find SpiderController component on player object");
                return;
            }

            ActiveSpiderControllers.RemoveAll(sc => sc is null);
            if (!ActiveSpiderControllers.Contains(spiderController))
            {
                ActiveSpiderControllers.Add(spiderController);
            }

            if (spiderController.GetComponent<InputInterceptor>() is null)
            {
                spiderController.gameObject.AddComponent<InputInterceptor>();
            }

            if (spiderController.GetComponent<ImmuneAbility>() is null)
            {
                spiderController.gameObject.AddComponent<ImmuneAbility>();
            }

            var immuneAbility = spiderController.GetComponent<ImmuneAbility>();
            if (isRespawn && immuneAbility is not null && immuneAbility.IsUnlocked())
            {
                immuneAbility.ForceStartCooldown();
            }

            if (spiderController.GetComponent<AmmoAbility>() is null)
            {
                spiderController.gameObject.AddComponent<AmmoAbility>();
            }

            if (spiderController.GetComponent<PulseAbility>() is null)
            {
                spiderController.gameObject.AddComponent<PulseAbility>();
            }

            if (spiderController.GetComponent<StorageAbility>() is null)
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
