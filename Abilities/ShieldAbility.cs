using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using System.Collections;
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

        protected override void Start()
        {
            base.Start();
            StartCoroutine(RegisterWithHealthSystem());
        }

        private IEnumerator RegisterWithHealthSystem()
        {
            while (spiderHealthSystem == null)
            {
                yield return null;
            }
            shieldsByHealth[spiderHealthSystem] = this;
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
            }
        }

        protected override void OnActivate()
        {
            // Synergy Check: If we have a shield -> Immunity (Ultimate-like behavior)
            if (spiderHealthSystem != null && spiderHealthSystem.HasShield())
            {
                isUltSession = true; // Use Ultimate session logic for immunity tracking
                wasHitDuringUltimate = false;
                hadShieldOnActivate = true;

                ApplyImmunity(true);
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


        private Dictionary<SpriteRenderer, Color> _originalColors = new Dictionary<SpriteRenderer, Color>();
        private Color? _originalLightColor;
        private float? _originalLightIntensity;

        private void ApplyRadiance(bool enable)
        {
            if (spiderHealthSystem == null) return;

            if (enable)
            {
                // -- 1. Apply to Sprites --
                if (spiderHealthSystem.spritesRoot != null)
                {
                    var renderers = spiderHealthSystem.spritesRoot.GetComponentsInChildren<SpriteRenderer>(true);
                    foreach (var sr in renderers)
                    {
                        if (sr == null) continue;
                        if (!_originalColors.ContainsKey(sr))
                        {
                            _originalColors[sr] = sr.color;
                        }
                        sr.color = new Color(1f, 0.84f, 0.0f, 1f); // Gold
                    }
                }

                // Also the head
                if (spiderHealthSystem.head != null)
                {
                    if (!_originalColors.ContainsKey(spiderHealthSystem.head))
                    {
                        _originalColors[spiderHealthSystem.head] = spiderHealthSystem.head.color;
                    }
                    spiderHealthSystem.head.color = new Color(1f, 0.84f, 0.0f, 1f);
                }

                // -- 2. Apply to Light (Via Reflection) --
                ApplyLightRadiance(true);
            }
            else
            {
                // -- Restore Sprites --
                foreach (var kvp in _originalColors)
                {
                    if (kvp.Key != null)
                    {
                        kvp.Key.color = kvp.Value;
                    }
                }
                _originalColors.Clear();

                // -- Restore Light --
                ApplyLightRadiance(false);
            }
        }

        private void ApplyLightRadiance(bool enable)
        {
            try
            {
                var lightField = typeof(SpiderHealthSystem).GetField("spiderLight");
                if (lightField == null) return;

                var lightObj = lightField.GetValue(spiderHealthSystem);
                if (lightObj == null) return;

                var lightType = lightObj.GetType();
                var colorProp = lightType.GetProperty("color");
                var intensityProp = lightType.GetProperty("intensity");

                if (enable)
                {
                    if (colorProp != null && _originalLightColor == null)
                        _originalLightColor = (Color)colorProp.GetValue(lightObj, null);

                    if (intensityProp != null && _originalLightIntensity == null)
                        _originalLightIntensity = (float)intensityProp.GetValue(lightObj, null);

                    if (colorProp != null) colorProp.SetValue(lightObj, new Color(1f, 0.6f, 0.0f), null);
                    if (intensityProp != null) intensityProp.SetValue(lightObj, 4.0f, null);
                }
                else
                {
                    if (colorProp != null && _originalLightColor != null)
                    {
                        colorProp.SetValue(lightObj, _originalLightColor.Value, null);
                        _originalLightColor = null;
                    }

                    if (intensityProp != null && _originalLightIntensity != null)
                    {
                        intensityProp.SetValue(lightObj, _originalLightIntensity.Value, null);
                        _originalLightIntensity = null;
                    }
                }
            }
            catch (System.Exception ex)
            {
                Logger.LogWarning($"[SpiderSurge] Failed to apply light radiance: {ex.Message}");
            }
        }

        private void ApplyImmunity(bool enable)
        {
            if (spiderHealthSystem == null) return;
            IsImmune = enable;

            ApplyRadiance(enable);

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