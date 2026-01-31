using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using System.Reflection;
using Logger = Silk.Logger;

namespace SpiderSurge
{
    public class ShieldAbility : BaseAbility
    {
        public static Dictionary<PlayerInput, ShieldAbility> playerShields = new Dictionary<PlayerInput, ShieldAbility>();
        private static Dictionary<SpiderHealthSystem, ShieldAbility> shieldsByHealth = new Dictionary<SpiderHealthSystem, ShieldAbility>();

        public override string PerkName => Consts.PerkNames.ShieldAbility;

        public override float BaseDuration => Consts.Values.Shield.BaseDuration;
        public override float DurationPerPerkLevel => Consts.Values.Shield.DurationIncreasePerLevel;

        public override float BaseCooldown => Consts.Values.Shield.BaseCooldown;
        public override float CooldownPerPerkLevel => Consts.Values.Shield.CooldownReductionPerLevel;
        public override float UltimateCooldownMultiplier => Consts.Values.Shield.UltimateCooldownMultiplier;

        // Ultimate: Immunity
        public override bool HasUltimate => true;
        public override string UltimatePerkDisplayName => "Shield Ultimate";
        public override string UltimatePerkDescription => "Grants complete damage immunity instead of a breakable shield.";

        private static FieldInfo immuneTimeField;
        private static MethodInfo _breakShieldMethod;

        private bool hadShieldOnActivate = false;

        private bool isUltSession = false;
        private bool wasHitDuringUltimate = false;

        public bool IsImmune { get; private set; }

        protected override void Awake()
        {
            base.Awake();
            if (playerInput != null)
            {
                playerShields[playerInput] = this;
            }

            if (spiderHealthSystem != null)
            {
                shieldsByHealth[spiderHealthSystem] = this;
            }

            if (immuneTimeField == null)
            {
                immuneTimeField = typeof(SpiderHealthSystem).GetField("_immuneTill",
                    BindingFlags.NonPublic | BindingFlags.Instance);

                if (immuneTimeField == null)
                {
                    Logger.LogError("ShieldAbility: Checked for _immuneTill field but it was null! Immunity will not work.");
                }
            }

            if (_breakShieldMethod == null)
            {
                _breakShieldMethod = typeof(SpiderHealthSystem).GetMethod("BreakShieldClientRpc",
                    BindingFlags.NonPublic | BindingFlags.Instance);
            }
        }

        public static ShieldAbility GetByHealthSystem(SpiderHealthSystem healthSystem)
        {
            if (healthSystem == null) return null;
            shieldsByHealth.TryGetValue(healthSystem, out var ability);
            return ability;
        }

        public void RegisterHit()
        {
            if (isActive && isUltSession)
            {
                wasHitDuringUltimate = true;
                Logger.LogInfo($"[ShieldAbility] Registered Hit during Ultimate for player {playerInput?.playerIndex}!");
            }
            else
            {
                Logger.LogInfo($"[ShieldAbility] Registered Hit but not processed. Active: {isActive}, Ult: {isUltSession}");
            }
        }

        protected override void OnActivate()
        {
            // Synergy Check: If Synergy active and we have a shield -> Immunity (Ultimate-like behavior)
            if (PerksManager.Instance != null && PerksManager.Instance.GetPerkLevel(Consts.PerkNames.Synergy) > 0 &&
                spiderHealthSystem != null && spiderHealthSystem.HasShield())
            {
                isUltSession = true; // Use Ultimate session logic for immunity tracking
                wasHitDuringUltimate = false;
                hadShieldOnActivate = true;

                ApplyImmunity(true);
                Logger.LogInfo($"Shield Synergy ACTIVATED for player {playerInput?.playerIndex}!");
            }
            else
            {
                // Normal Activation
                isUltSession = false;
                if (spiderHealthSystem != null) spiderHealthSystem.EnableShield();
            }
        }

        protected override void OnDeactivate()
        {
            if (spiderHealthSystem != null)
            {
                bool shouldKeepShield = false;

                // if player had shield before Ult/Synergy and didn't get hit, keep it
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

            if (isUltSession)
            {
                ApplyImmunity(false);
            }

            isUltSession = false;
            wasHitDuringUltimate = false;
        }

        protected override void OnActivateUltimate()
        {
            isUltSession = true;
            wasHitDuringUltimate = false;
            hadShieldOnActivate = spiderHealthSystem != null && spiderHealthSystem.HasShield();

            ApplyImmunity(true);

            if (spiderHealthSystem != null && !spiderHealthSystem.HasShield())
            {
                spiderHealthSystem.EnableShield();
            }
        }

        protected override void OnDeactivateUltimate()
        {
            ApplyImmunity(false);
        }


        private void ApplyImmunity(bool enable)
        {
            if (spiderHealthSystem == null) return;
            IsImmune = enable;

            if (enable)
            {
                try
                {
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

            if (spiderHealthSystem != null && shieldsByHealth.ContainsKey(spiderHealthSystem))
            {
                shieldsByHealth.Remove(spiderHealthSystem);
            }
        }

        private void DestroyShield()
        {
            try
            {
                if (_breakShieldMethod != null)
                {
                    _breakShieldMethod.Invoke(spiderHealthSystem, null);
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