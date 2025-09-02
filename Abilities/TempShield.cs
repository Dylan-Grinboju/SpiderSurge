using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Logger = Silk.Logger;

namespace SpiderSurge.Abilities
{
    public class TempShield : MonoBehaviour
    {
        public static Dictionary<PlayerInput, TempShield> playerShields = new Dictionary<PlayerInput, TempShield>();

        private PlayerInput playerInput;
        private PlayerController playerController;
        private SpiderHealthSystem spiderHealthSystem;

        private bool shieldActive = false;
        private bool onCooldown = false;
        private Coroutine shieldDurationCoroutine;
        private Coroutine cooldownCoroutine;

        private const float SHIELD_DURATION = 5f;
        private const float COOLDOWN_DURATION = 10f;

        private void Awake()
        {
            playerInput = GetComponentInParent<PlayerInput>();
            if (playerInput != null)
            {
                playerShields[playerInput] = this;
                Logger.LogInfo($"TempShield initialized for player {playerInput.playerIndex}");
            }
        }

        private void Start()
        {
            // Get PlayerController from the same GameObject or parent
            playerController = GetComponentInParent<PlayerController>();
            if (playerController == null)
            {
                Logger.LogError($"PlayerController not found for TempShield on player {playerInput?.playerIndex}");
            }
        }

        private void Update()
        {
            // Update SpiderHealthSystem reference if we don't have it yet
            if (spiderHealthSystem == null && playerController != null)
            {
                spiderHealthSystem = playerController.spiderHealthSystem;
            }
        }

        public void ActivateShield()
        {
            // Check if on cooldown
            if (onCooldown)
            {
                Logger.LogError($"TempShield ability is on cooldown for player {playerInput.playerIndex}!");
                return;
            }

            // Check if player already has a shield
            if (spiderHealthSystem != null && spiderHealthSystem.HasShield())
            {
                Logger.LogError($"Player {playerInput.playerIndex} already has a shield active!");
                return;
            }

            if (spiderHealthSystem == null)
            {
                Logger.LogError($"SpiderHealthSystem not found for player {playerInput.playerIndex}!");
                return;
            }

            // Activate the shield
            shieldActive = true;
            spiderHealthSystem.EnableShield();
            Logger.LogInfo($"TempShield ACTIVATED for player {playerInput.playerIndex}!");

            // Start the shield duration timer
            if (shieldDurationCoroutine != null)
            {
                StopCoroutine(shieldDurationCoroutine);
            }
            shieldDurationCoroutine = StartCoroutine(ShieldDurationCoroutine());
        }

        public void DeactivateShield()
        {
            if (shieldActive && spiderHealthSystem != null)
            {
                shieldActive = false;

                // Play the shield explosion animation even when shield expires naturally
                PlayShieldExplosion();

                spiderHealthSystem.DisableShield();
                Logger.LogInfo($"TempShield DEACTIVATED for player {playerInput.playerIndex}!");
            }
        }

        public bool IsShieldActive()
        {
            return shieldActive;
        }

        public bool IsOnCooldown()
        {
            return onCooldown;
        }

        private IEnumerator ShieldDurationCoroutine()
        {
            yield return new WaitForSeconds(SHIELD_DURATION);

            // Only deactivate if the shield is still active from our ability
            // (it might have been broken by damage)
            if (shieldActive)
            {
                DeactivateShield();
            }

            // Start cooldown regardless of whether shield was broken or expired
            StartCooldown();
        }

        private void StartCooldown()
        {
            if (cooldownCoroutine != null)
            {
                StopCoroutine(cooldownCoroutine);
            }
            cooldownCoroutine = StartCoroutine(CooldownCoroutine());
        }

        private IEnumerator CooldownCoroutine()
        {
            onCooldown = true;
            Logger.LogInfo($"TempShield cooldown started for player {playerInput.playerIndex} ({COOLDOWN_DURATION}s)");

            yield return new WaitForSeconds(COOLDOWN_DURATION);

            onCooldown = false;
            Logger.LogInfo($"TempShield cooldown finished for player {playerInput.playerIndex}");
        }

        // Check if the shield was broken by damage (not by our timer)
        private void LateUpdate()
        {
            if (shieldActive && spiderHealthSystem != null && !spiderHealthSystem.HasShield())
            {
                // Shield was broken by damage, not by our timer
                shieldActive = false;
                Logger.LogInfo($"TempShield was broken by damage for player {playerInput.playerIndex}!");

                // Stop the duration coroutine if it's running
                if (shieldDurationCoroutine != null)
                {
                    StopCoroutine(shieldDurationCoroutine);
                    shieldDurationCoroutine = null;
                }

                // Start cooldown
                StartCooldown();
            }
        }

        private void OnDestroy()
        {
            if (playerInput != null && playerShields.ContainsKey(playerInput))
            {
                playerShields.Remove(playerInput);
            }

            // Clean up coroutines
            if (shieldDurationCoroutine != null)
            {
                StopCoroutine(shieldDurationCoroutine);
            }
            if (cooldownCoroutine != null)
            {
                StopCoroutine(cooldownCoroutine);
            }
        }

        private void PlayShieldExplosion()
        {
            // Access the private method via reflection to trigger shield break effects
            try
            {
                var breakShieldMethod = typeof(SpiderHealthSystem).GetMethod("BreakShieldClientRpc",
                    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                if (breakShieldMethod != null)
                {
                    breakShieldMethod.Invoke(spiderHealthSystem, null);
                    Logger.LogInfo($"Shield explosion animation triggered for player {playerInput.playerIndex}!");
                }
                else
                {
                    Logger.LogWarning($"Could not find BreakShieldClientRpc method for player {playerInput.playerIndex}");
                }
            }
            catch (System.Exception e)
            {
                Logger.LogError($"Failed to trigger shield explosion for player {playerInput.playerIndex}: {e.Message}");
            }
        }

        public static TempShield GetPlayerShield(PlayerInput playerInput)
        {
            playerShields.TryGetValue(playerInput, out TempShield shield);
            return shield;
        }
    }
}
