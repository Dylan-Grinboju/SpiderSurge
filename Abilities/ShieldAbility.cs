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

        // Ultimate: Immunity
        public override bool HasUltimate => true;
        public override string UltimatePerkDisplayName => "Shield Immunity";
        public override string UltimatePerkDescription => "Grants complete damage immunity instead of a breakable shield.";

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

        protected override void OnActivateUltimate()
        {
            ApplyImmunity(true);

            // Also enable shield visual
            if (spiderHealthSystem != null && !spiderHealthSystem.HasShield())
            {
                spiderHealthSystem.EnableShield();
            }
            
            Logger.LogInfo($"Shield Immunity ACTIVATED for player {playerInput?.playerIndex}");
        }

        protected override void OnDeactivateUltimate()
        {
            ApplyImmunity(false);
            
            Logger.LogInfo($"Shield Immunity DEACTIVATED for player {playerInput?.playerIndex}");
        }

        private void ApplyImmunity(bool enable)
        {
            if (spiderHealthSystem == null) return;

            // Removed isTrigger modification to prevent falling through map or getting stuck in walls
            var colliders = spiderHealthSystem.GetComponents<Collider2D>();
            foreach (var collider in colliders)
            {
                if (collider != null)
                {
                    collider.isTrigger = enable;
                }
            }

            if (enable)
            {
                try
                {
                    var immuneTimeField = typeof(SpiderHealthSystem).GetField("_immuneTime",
                        BindingFlags.NonPublic | BindingFlags.Instance);
                    if (immuneTimeField != null)
                    {
                        immuneTimeField.SetValue(spiderHealthSystem, float.MaxValue);
                    }
                }
                catch (System.Exception ex)
                {
                    Logger.LogError($"Failed to set immune time: {ex.Message}");
                }
            }
            else
            {
                try
                {
                    var immuneTimeField = typeof(SpiderHealthSystem).GetField("_immuneTime",
                        BindingFlags.NonPublic | BindingFlags.Instance);
                    if (immuneTimeField != null)
                    {
                        immuneTimeField.SetValue(spiderHealthSystem, 0f);
                    }
                }
                catch (System.Exception ex)
                {
                    Logger.LogError($"Failed to reset immune time: {ex.Message}");
                }
            }
        }

        private void LateUpdate()
        {
            // Only check for shield break in non-Ultimate mode
            // In Ultimate mode we maintain immunity
            if (isActive && !isUltimateActive && spiderHealthSystem != null && !spiderHealthSystem.HasShield())
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