using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Logger = Silk.Logger;

namespace SpiderSurge
{
    public class ShieldAbility : BaseAbility
    {
        public static Dictionary<PlayerInput, ShieldAbility> playerShields = new Dictionary<PlayerInput, ShieldAbility>();

        public override string[] ActivationButtons => new string[] { "<keyboard>/q", "<Gamepad>/leftshoulder" };
        public override float Duration => PerksManager.Instance.GetShieldDuration();
        public override float CooldownTime => PerksManager.Instance.GetShieldCooldown();

        protected override void Awake()
        {
            base.Awake();
            if (playerInput != null)
            {
                playerShields[playerInput] = this;
            }
        }

        protected override bool ShouldRegister()
        {
            return PerksManager.Instance.IsShieldAbilityUnlocked;
        }

        protected override bool CanActivate()
        {
            return PerksManager.Instance.IsShieldAbilityUnlocked;
        }

        public override void Activate()
        {
            if (!CanActivate())
            {
                Logger.LogInfo($"Player {playerInput.playerIndex} tried to activate shield but ability is not unlocked");
                return;
            }
            base.Activate();
        }

        protected override void OnActivate()
        {
            if (spiderHealthSystem == null) return;
            if (spiderHealthSystem.HasShield())
            {
                Logger.LogInfo($"Player {playerInput.playerIndex} already has shield active");
            }
            else
            {
                // Normal activation
                spiderHealthSystem.EnableShield();
                Logger.LogInfo($"Player {playerInput.playerIndex} activated shield");
            }
        }

        protected override void OnDeactivate()
        {
            if (spiderHealthSystem != null)
            {
                DestroyShield();
                spiderHealthSystem.DisableShield();
            }
        }

        private void LateUpdate()
        {
            if (isActive && spiderHealthSystem != null && !spiderHealthSystem.HasShield())
            {
                isActive = false;
                Logger.LogInfo($"ShieldAbility was broken by damage for player {playerInput.playerIndex}!");

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

        private void DestroyShield()
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
    }
}