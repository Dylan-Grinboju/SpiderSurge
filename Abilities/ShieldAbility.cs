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
        private static Dictionary<PlayerInput, ShieldAbility> playerShields = new Dictionary<PlayerInput, ShieldAbility>();

        public override string[] ActivationButtons => new string[] { "<keyboard>/q", "<Gamepad>/leftshoulder" };
        public override float Duration => 5f;
        public override float CooldownTime => 1f;

        private bool explosionImmunityPerk = false;

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
            return SurgeGameModeManager.Instance.IsShieldAbilityUnlocked;
        }

        protected override bool CanActivate()
        {
            return SurgeGameModeManager.Instance.IsShieldAbilityUnlocked && SurgeGameModeManager.Instance.GetShieldCharges(playerInput) > 0;
        }

        public override void Activate()
        {
            if (!CanActivate())
            {
                int charges = SurgeGameModeManager.Instance.GetShieldCharges(playerInput);
                if (charges == 0)
                {
                    Logger.LogInfo($"Player {playerInput.playerIndex} tried to activate shield but has 0 charges");
                }
                return;
            }
            base.Activate();
        }

        protected override void OnActivate()
        {
            if (spiderHealthSystem != null && spiderHealthSystem.HasShield())
            {
                // Already has shield, perform explosion
                PlayShieldExplosion();
                Logger.LogInfo($"ShieldAbility explosion for player {playerInput.playerIndex}");
                int remainingCharges = SurgeGameModeManager.Instance.GetShieldCharges(playerInput);
                Logger.LogInfo($"Player {playerInput.playerIndex} has {remainingCharges} shield charges remaining after explosion");
            }
            else
            {
                // Normal activation
                spiderHealthSystem.EnableShield();
                SurgeGameModeManager.Instance.ConsumeShieldCharge(playerInput);
                int remainingCharges = SurgeGameModeManager.Instance.GetShieldCharges(playerInput);
                Logger.LogInfo($"Player {playerInput.playerIndex} activated shield, {remainingCharges} charges remaining");
            }
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
                Logger.LogInfo($"ShieldAbility was broken by damage for player {playerInput.playerIndex}!");

                if (durationCoroutine != null)
                {
                    StopCoroutine(durationCoroutine);
                    durationCoroutine = null;
                }

                StartCooldown();
            }
        }

        private void FixedUpdate()
        {
            if (!SurgeGameModeManager.Instance.IsActive || playerInput == null) return;

            var tracker = PlayerStateTracker.Instance;
            if (tracker == null) return;

            // Check stillness charges
            if (SurgeGameModeManager.Instance.GetPerkLevel("stillness10s") > 0 && tracker.HasTime(playerInput, "stillness", 10f))
            {
                SurgeGameModeManager.Instance.AddShieldCharge(playerInput);
                tracker.ResetTime(playerInput, "stillness");
                Logger.LogInfo($"Player {playerInput.playerIndex} gained shield charge from 10s stillness");
            }
            else if (SurgeGameModeManager.Instance.GetPerkLevel("stillness5s") > 0 && tracker.HasTime(playerInput, "stillness", 5f))
            {
                SurgeGameModeManager.Instance.AddShieldCharge(playerInput);
                tracker.ResetTime(playerInput, "stillness");
                Logger.LogInfo($"Player {playerInput.playerIndex} gained shield charge from 5s stillness");
            }

            // Check airborne charges
            if (SurgeGameModeManager.Instance.GetPerkLevel("airborne10s") > 0 && tracker.HasTime(playerInput, "airborne", 10f))
            {
                SurgeGameModeManager.Instance.AddShieldCharge(playerInput);
                tracker.ResetTime(playerInput, "airborne");
                Logger.LogInfo($"Player {playerInput.playerIndex} gained shield charge from 10s airborne");
            }
            else if (SurgeGameModeManager.Instance.GetPerkLevel("airborne5s") > 0 && tracker.HasTime(playerInput, "airborne", 5f))
            {
                SurgeGameModeManager.Instance.AddShieldCharge(playerInput);
                tracker.ResetTime(playerInput, "airborne");
                Logger.LogInfo($"Player {playerInput.playerIndex} gained shield charge from 5s airborne");
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

        public void SetExplosionImmunityPerk(bool enabled)
        {
            explosionImmunityPerk = enabled;
        }

        public static ShieldAbility GetPlayerShield(PlayerInput playerInput)
        {
            playerShields.TryGetValue(playerInput, out ShieldAbility shield);
            return shield;
        }
    }
}