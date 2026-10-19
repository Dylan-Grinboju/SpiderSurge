using UnityEngine;
using UnityEngine.InputSystem;
using SpiderSurge.Common;
using Logger = Silk.Logger;
using System.Collections.Generic;

namespace SpiderSurge
{
    public static class PlayerAbilityHandler
    {
        public static List<SpiderController> ActiveSpiderControllers = new List<SpiderController>();
        private static readonly HashSet<int> SeenPlayerSpawns = new HashSet<int>();

        public static int SpawnEpoch { get; private set; }

        // The host counts each reset and sends the count, so clients forget the same spawns at the same time
        public static void ResetSpawnTracking()
        {
            SeenPlayerSpawns.Clear();
            if (Net.IsAuthority)
            {
                SpawnEpoch++;
                SurgeNet.SendStateNow();
            }
        }

        public static void ApplyRemoteEpoch(int epoch)
        {
            if (epoch == SpawnEpoch)
            {
                return;
            }
            SpawnEpoch = epoch;
            SeenPlayerSpawns.Clear();
        }

        public static void AttachToExistingSpiders()
        {
            foreach (var spider in Object.FindObjectsOfType<SpiderController>())
            {
                InitializePlayerAbilities(spider.gameObject);
            }
        }

        // Every spider gets abilities: a local player's take input, a remote player's are proxies the host runs and clients draw
        public static void InitializePlayerAbilities(GameObject playerObject)
        {
            if (playerObject == null) return;

            try
            {
                if (SurgeGameModeManager.Instance == null || !SurgeGameModeManager.Instance.IsActive)
                {
                    return;
                }

                if (SurgeGameModeManager.AbilitiesDisabled)
                {
                    return;
                }

                PlayerController playerController = playerObject.GetComponentInParent<PlayerController>();
                if (playerController == null)
                {
                    Logger.LogWarning("Could not find PlayerController component on player object");
                    return;
                }

                PlayerInput playerInput = playerObject.GetComponentInParent<PlayerInput>();
                if (playerInput == null && Players.IsLocal(playerController))
                {
                    Logger.LogWarning("Could not find PlayerInput component on a local player object");
                    return;
                }

                SpiderController spiderController = playerObject.GetComponent<SpiderController>();
                if (spiderController == null)
                {
                    Logger.LogWarning("Could not find SpiderController component on player object");
                    return;
                }

                ActiveSpiderControllers.RemoveAll(sc => sc == null);
                if (ActiveSpiderControllers.Contains(spiderController))
                {
                    return;
                }
                ActiveSpiderControllers.Add(spiderController);

                int playerId = playerController.playerID.Value;
                bool isRespawn = SeenPlayerSpawns.Contains(playerId);
                SeenPlayerSpawns.Add(playerId);

                if (playerInput != null && spiderController.GetComponent<InputInterceptor>() == null)
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

                if (spiderController.GetComponent<FreezeAbility>() == null)
                {
                    spiderController.gameObject.AddComponent<FreezeAbility>();
                }
            }
            catch (System.Exception ex)
            {
                Logger.LogError($"Error initializing player abilities: {ex.Message}");
            }
        }

        // A new wave refunds every ability's cooldown; the host runs this from the wave setter, clients from the replicated wave
        public static void OnWaveStarted(int wave)
        {
            if (wave <= 0 || ActiveSpiderControllers == null)
            {
                return;
            }

            for (int i = ActiveSpiderControllers.Count - 1; i >= 0; i--)
            {
                var controller = ActiveSpiderControllers[i];
                if (controller == null)
                {
                    ActiveSpiderControllers.RemoveAt(i);
                    continue;
                }

                foreach (var ability in controller.GetComponents<BaseAbility>())
                {
                    if (ability is ImmuneAbility)
                    {
                        ability.ReduceCooldown(Consts.Values.Immune.AbilityBaseCooldown);
                        continue;
                    }
                    ability.SetCooldownToZero();
                }
            }

            foreach (var storage in Object.FindObjectsOfType<StorageAbility>())
            {
                storage.UpdateCachedModifierLevels();
            }
        }

        public static BaseAbility FindAbility(int playerId, AbilityKind kind)
        {
            ActiveSpiderControllers.RemoveAll(sc => sc == null);
            foreach (var controller in ActiveSpiderControllers)
            {
                if (Players.IdOf(controller) != playerId)
                {
                    continue;
                }
                foreach (var ability in controller.GetComponents<BaseAbility>())
                {
                    if (ability.Kind == kind)
                    {
                        return ability;
                    }
                }
            }
            return null;
        }

        // Whether the player's ability or ultimate effect is running right now
        public static bool HasActiveAbility(int playerId)
        {
            foreach (var controller in ActiveSpiderControllers)
            {
                if (controller == null || Players.IdOf(controller) != playerId)
                {
                    continue;
                }
                foreach (var ability in controller.GetComponents<BaseAbility>())
                {
                    if (ability.IsActive())
                    {
                        return true;
                    }
                }
            }
            return false;
        }
    }
}
