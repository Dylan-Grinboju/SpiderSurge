using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using Logger = Silk.Logger;

namespace SpiderSurge.Abilities
{
    public class TempShield : MonoBehaviour
    {
        public static Dictionary<PlayerInput, TempShield> playerShields = new Dictionary<PlayerInput, TempShield>();

        private PlayerInput playerInput;
        private bool shieldActive = false;

        private void Awake()
        {
            playerInput = GetComponentInParent<PlayerInput>();
            if (playerInput != null)
            {
                playerShields[playerInput] = this;
                Logger.LogInfo($"TempShield initialized for player {playerInput.playerIndex}");
            }
        }

        public void ActivateShield()
        {
            if (!shieldActive)
            {
                shieldActive = true;
                Logger.LogInfo($"TempShield ACTIVATED for player {playerInput.playerIndex}!");

                // TODO: Add actual shield logic here
                // For now just log the activation
            }
        }

        public void DeactivateShield()
        {
            if (shieldActive)
            {
                shieldActive = false;
                Logger.LogInfo($"TempShield DEACTIVATED for player {playerInput.playerIndex}!");

                // TODO: Add actual shield deactivation logic here
            }
        }

        public bool IsShieldActive()
        {
            return shieldActive;
        }

        private void OnDestroy()
        {
            if (playerInput != null && playerShields.ContainsKey(playerInput))
            {
                playerShields.Remove(playerInput);
            }
        }

        public static TempShield GetPlayerShield(PlayerInput playerInput)
        {
            playerShields.TryGetValue(playerInput, out TempShield shield);
            return shield;
        }
    }
}
