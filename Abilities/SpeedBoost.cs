using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;
using Logger = Silk.Logger;

namespace SpiderSurge.Abilities
{
    public class SpeedBoost : Ability
    {
        public static Dictionary<PlayerInput, SpeedBoost> playerSpeedBoosts = new Dictionary<PlayerInput, SpeedBoost>();

        public override string ActivationButton => "<Gamepad>/buttonEast"; // B button
        public override float Duration => 3f;
        public override float CooldownTime => 8f;

        protected override void Awake()
        {
            base.Awake();
            if (playerInput != null)
            {
                playerSpeedBoosts[playerInput] = this;
            }
        }

        protected override bool CanActivate()
        {
            if (playerController == null)
            {
                Logger.LogError($"PlayerController not found for player {playerInput.playerIndex}!");
                return false;
            }

            return true;
        }

        protected override void OnActivate()
        {
            Logger.LogInfo($"Speed boost ACTIVATED for player {playerInput.playerIndex}! Player should now move faster for {Duration} seconds.");
            // TODO: Implement actual speed modification when we have access to the correct PlayerController properties
        }

        protected override void OnDeactivate()
        {
            Logger.LogInfo($"Speed boost DEACTIVATED for player {playerInput.playerIndex}! Speed returned to normal.");
            // TODO: Restore original speed when we have access to the correct PlayerController properties
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            if (playerInput != null && playerSpeedBoosts.ContainsKey(playerInput))
            {
                playerSpeedBoosts.Remove(playerInput);
            }
        }

        public static SpeedBoost GetPlayerSpeedBoost(PlayerInput playerInput)
        {
            playerSpeedBoosts.TryGetValue(playerInput, out SpeedBoost speedBoost);
            return speedBoost;
        }
    }
}
