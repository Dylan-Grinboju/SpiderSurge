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

        public override float BaseDuration => 2f;
        public override float DurationPerPerkLevel => 1f;

        public override float BaseCooldown => 11f;
        public override float CooldownPerPerkLevel => 5f;

        // Ultimate: Immunity
        public override bool HasUltimate => true;
        public override string UltimatePerkDisplayName => "Shield Ultimate";
        public override string UltimatePerkDescription => "Grants complete damage immunity instead of a breakable shield.";

        // Cached reflection field for immunity
        private static FieldInfo immuneTimeField;

        private bool hadShieldOnActivate = false;

        // Track if the current activation is an Ultimate
        private bool isUltSession = false;
        private bool wasHitDuringUltimate = false;

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
                // Field name is _immuneTill (was incorrectly _immuneTime)
                immuneTimeField = typeof(SpiderHealthSystem).GetField("_immuneTill",
                    BindingFlags.NonPublic | BindingFlags.Instance);
                    
                if (immuneTimeField == null)
                {
                    Logger.LogError("ShieldAbility: Checked for _immuneTill field but it was null! Immunity will not work.");
                }
            }
        }

        public static ShieldAbility GetByHealthSystem(SpiderHealthSystem healthSystem)
        {
            foreach (var ability in playerShields.Values)
            {
                if (ability.spiderHealthSystem == healthSystem)
                    return ability;
            }
            return null;
        }

        public void RegisterHit()
        {
            if (isActive && isUltSession)
            {
                wasHitDuringUltimate = true;
            }
        }

        public override void Activate()
        {         
            if (spiderHealthSystem != null && spiderHealthSystem.HasShield())
            {
                return;
            }

            base.Activate();
        }

        protected override void OnActivate()
        {
            isUltSession = false; // Normal activation
            
            if (spiderHealthSystem == null) return;

            spiderHealthSystem.EnableShield();
        }

        protected override void OnDeactivate()
        {
            if (spiderHealthSystem != null)
            {
                bool shouldKeepShield = false;

                // if player had shield before Ult and didn't get hit, keep it
                if (isUltSession && hadShieldOnActivate && !wasHitDuringUltimate)
                {
                    shouldKeepShield = true;
                }

                if (!shouldKeepShield)
                {
                    DestroyShield();
                    spiderHealthSystem.DisableShield();
                }
            }
            
            isUltSession = false;
            wasHitDuringUltimate = false;
        }

        protected override void OnActivateUltimate()
        {
            isUltSession = true;
            wasHitDuringUltimate = false;
            
            // Record state before applying anything
            hadShieldOnActivate = spiderHealthSystem != null && spiderHealthSystem.HasShield();

            ApplyImmunity(true);

            // Also enable shield visual
            if (spiderHealthSystem != null && !spiderHealthSystem.HasShield())
            {
                spiderHealthSystem.EnableShield();
            }
            
            Logger.LogInfo($"Shield Immunity ACTIVATED for player {playerInput?.playerIndex}. HadShield: {hadShieldOnActivate}");
        }

        protected override void OnDeactivateUltimate()
        {
            ApplyImmunity(false);
            
            Logger.LogInfo($"Shield Immunity DEACTIVATED for player {playerInput?.playerIndex}. WasHit: {wasHitDuringUltimate}");
        }

        private void ApplyImmunity(bool enable)
        {
            if (spiderHealthSystem == null) return;

            if (enable)
            {
                try
                {
                    if (immuneTimeField != null)
                    {
                        immuneTimeField.SetValue(spiderHealthSystem, float.MaxValue);
                    }
                    else
                    {
                         // Try to get it one more time if cached is null (fallback)
                         var field = typeof(SpiderHealthSystem).GetField("_immuneTill", BindingFlags.NonPublic | BindingFlags.Instance);
                         if (field != null)
                         {
                             immuneTimeField = field;
                             field.SetValue(spiderHealthSystem, float.MaxValue);
                         }
                         else
                         {
                             Logger.LogError("Could not find _immuneTill field in SpiderHealthSystem");
                         }
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
                    if (immuneTimeField != null)
                    {
                        // Reset immunity to current time (expired)
                        immuneTimeField.SetValue(spiderHealthSystem, Time.time);
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