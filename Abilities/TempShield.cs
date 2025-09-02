using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Logger = Silk.Logger;

namespace SpiderSurge.Abilities
{
    public class TempShield : Ability
    {
        public static Dictionary<PlayerInput, TempShield> playerShields = new Dictionary<PlayerInput, TempShield>();

        public override float Duration => 5f;
        public override float CooldownTime => 10f;

        protected override void Awake()
        {
            base.Awake();
            if (playerInput != null)
            {
                playerShields[playerInput] = this;
            }
        }

        protected override bool CanActivate()
        {
            if (spiderHealthSystem != null && spiderHealthSystem.HasShield())
            {
                Logger.LogError($"Player {playerInput.playerIndex} already has a shield active!");
                return false;
            }

            if (spiderHealthSystem == null)
            {
                Logger.LogError($"SpiderHealthSystem not found for player {playerInput.playerIndex}!");
                return false;
            }

            return true;
        }

        protected override void OnActivate()
        {
            spiderHealthSystem.EnableShield();
        }

        protected override void OnDeactivate()
        {
            if (spiderHealthSystem != null)
            {
                PlayShieldExplosion();
                spiderHealthSystem.DisableShield();
            }
        }

        private void LateUpdate()
        {
            if (isActive && spiderHealthSystem != null && !spiderHealthSystem.HasShield())
            {
                isActive = false;
                Logger.LogInfo($"TempShield was broken by damage for player {playerInput.playerIndex}!");

                if (durationCoroutine != null)
                {
                    StopCoroutine(durationCoroutine);
                    durationCoroutine = null;
                }

                StartCooldown();
            }
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            if (playerInput != null && playerShields.ContainsKey(playerInput))
            {
                playerShields.Remove(playerInput);
            }
        }

        private void PlayShieldExplosion()
        {
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
