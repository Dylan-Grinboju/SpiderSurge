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

        public override string PerkName => "shieldAbility";

        public override float BaseDuration => 1f;
        public override float DurationPerPerkLevel => 1f;

        public override float BaseCooldown => 11f;
        public override float CooldownPerPerkLevel => 5f;

        // Upgrade: Immunity
        public override bool HasUpgrade => true;
        public override string UpgradePerkDisplayName => "Shield Immunity";
        public override string UpgradePerkDescription => "Grants complete damage immunity instead of a breakable shield.";

        // Cached reflection field for immunity
        private static FieldInfo immuneTimeField;

        protected override void Awake()
        {
            base.Awake();
            if (playerInput != null)
            {
                playerShields[playerInput] = this;
            }

            // Cache reflection field for immunity
            if (immuneTimeField == null)
            {
                immuneTimeField = typeof(SpiderHealthSystem).GetField("_immuneTime",
                    BindingFlags.NonPublic | BindingFlags.Instance);
            }
        }

        protected override void OnActivate()
        {
            if (spiderHealthSystem == null) return;

            if (spiderHealthSystem.HasShield())
            {
                return;
            }

            spiderHealthSystem.EnableShield();
        }

        protected override void OnDeactivate()
        {
            if (spiderHealthSystem != null)
            {
                DestroyShield();
                spiderHealthSystem.DisableShield();
            }
        }

        protected override void OnActivateUpgrade()
        {
            if (spiderHealthSystem == null) return;

            // Grant immunity by setting immune time to maximum
            try
            {
                if (immuneTimeField != null)
                {
                    immuneTimeField.SetValue(spiderHealthSystem, float.MaxValue);
                    Logger.LogInfo($"Shield Immunity ACTIVATED for player {playerInput?.playerIndex}");
                }
            }
            catch (System.Exception ex)
            {
                Logger.LogError($"Failed to set immunity: {ex.Message}");
            }

            // Also enable shield visual
            if (!spiderHealthSystem.HasShield())
            {
                spiderHealthSystem.EnableShield();
            }
        }

        protected override void OnDeactivateUpgrade()
        {
            if (spiderHealthSystem == null) return;

            // Reset immunity
            try
            {
                if (immuneTimeField != null)
                {
                    immuneTimeField.SetValue(spiderHealthSystem, 0f);
                    Logger.LogInfo($"Shield Immunity DEACTIVATED for player {playerInput?.playerIndex}");
                }
            }
            catch (System.Exception ex)
            {
                Logger.LogError($"Failed to reset immunity: {ex.Message}");
            }
        }

        private void LateUpdate()
        {
            // Only check for shield break in non-upgrade mode
            // In upgrade mode we maintain immunity
            if (isActive && !isUpgradeActive && spiderHealthSystem != null && !spiderHealthSystem.HasShield())
            {
                isActive = false;

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
                    BindingFlags.NonPublic | BindingFlags.Instance);

                if (breakShieldMethod != null)
                {
                    breakShieldMethod.Invoke(spiderHealthSystem, null);
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