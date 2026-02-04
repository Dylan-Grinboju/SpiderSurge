using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using System.Reflection;
using Unity.Netcode;
using Interfaces;
using Logger = Silk.Logger;

namespace SpiderSurge
{
    public class ExplosionAbility : BaseAbility
    {
        public static Dictionary<PlayerInput, ExplosionAbility> playerExplosions = new Dictionary<PlayerInput, ExplosionAbility>();

        public override string PerkName => Consts.PerkNames.ExplosionAbility;

        public override float AbilityBaseCooldown => Consts.Values.Explosion.AbilityBaseCooldown;
        public override float AbilityBaseDuration => Consts.Values.Explosion.AbilityBaseDuration;
        public override float UltimateBaseDuration => Consts.Values.Explosion.UltimateBaseDuration;
        public override float UltimateBaseCooldown => Consts.Values.Explosion.UltimateBaseCooldown;
        public override float AbilityCooldownPerPerkLevel => Consts.Values.Explosion.AbilityCooldownReductionPerLevel;
        public override float UltimateCooldownPerPerkLevel => Consts.Values.Explosion.UltimateCooldownReductionPerLevel;

        public override bool HasUltimate => true;
        public override string UltimatePerkDisplayName => "Explosion Ultimate";
        public override string UltimatePerkDescription => "Explosion deals lethal damage in the death zone instead of just knockback.";

        private float GetKnockBackRadius(bool isUlt)
        {
            int durationLevel = PerksManager.Instance?.GetPerkLevel(Consts.PerkNames.AbilityDuration) ?? 0;
            float radius;
            int shortTermLevel = PerksManager.Instance?.GetPerkLevel(Consts.PerkNames.ShortTermInvestment) ?? 0;
            int longTermLevel = PerksManager.Instance?.GetPerkLevel(Consts.PerkNames.LongTermInvestment) ?? 0;
            if (isUlt)
            {
                radius = Consts.Values.Explosion.UltimateBaseKnockbackRadius;
                if (durationLevel >= 2) radius += Consts.Values.Explosion.UltimateKnockbackRadiusIncreasePerLevel;
                if (shortTermLevel > 0) radius -= Consts.Values.Explosion.UltimateKnockbackRadiusIncreasePerLevel;
                if (longTermLevel > 0) radius += Consts.Values.Explosion.UltimateKnockbackRadiusIncreasePerLevel;
            }
            else
            {
                radius = Consts.Values.Explosion.AbilityBaseKnockbackRadius;
                if (durationLevel >= 1) radius += Consts.Values.Explosion.AbilityKnockbackRadiusIncreasePerLevel;
                if (shortTermLevel > 0) radius += Consts.Values.Explosion.AbilityKnockbackRadiusIncreasePerLevel;
                if (longTermLevel > 0) radius -= Consts.Values.Explosion.AbilityKnockbackRadiusIncreasePerLevel;
            }
            return radius;
        }

        private float GetKnockBackStrength(bool isUlt)
        {
            float strength;
            int biggerBoom = 0;
            if (ModifierManager.instance != null)
            {
                biggerBoom = ModifierManager.instance.GetModLevel(Consts.ModifierNames.BiggerBoom);
            }

            if (isUlt)
            {
                strength = Consts.Values.Explosion.UltimateBaseKnockbackStrength;
                if (biggerBoom > 1) strength += Consts.Values.Explosion.UltimateKnockbackStrengthIncreasePerLevel;
            }
            else
            {
                strength = Consts.Values.Explosion.AbilityBaseKnockbackStrength;
                if (biggerBoom > 0) strength += Consts.Values.Explosion.AbilityKnockbackStrengthIncreasePerLevel;
            }

            return strength;
        }

        private float GetDeathRadius(bool isUlt)
        {
            if (!isUlt)
            {
                return 0f;
            }

            float deathRadius = Consts.Values.Explosion.UltimateBaseDeathRadius;

            if (ModifierManager.instance != null)
            {
                int tooCool = ModifierManager.instance.GetModLevel(Consts.ModifierNames.TooCool);
                deathRadius += Consts.Values.Explosion.UltimateDeathRadiusIncreasePerLevel * tooCool;
            }

            return deathRadius;
        }

        private LayerMask explosionLayers;

        // Cached explosion VFX prefab (obtained from SpiderHealthSystem at runtime)
        private static GameObject cachedExplosionPrefab;

        // Pre-allocated buffer for physics queries to avoid GC allocation
        private Collider2D[] _explosionResults = new Collider2D[64];

        protected override void Awake()
        {
            base.Awake();
            if (playerInput != null)
            {
                playerExplosions[playerInput] = this;
            }

            explosionLayers = LayerMask.GetMask("Player", "Item", "Enemy", "DynamicWorld");

            if (explosionLayers == 0)
            {
                explosionLayers = ~0;// All layers
                Logger.LogWarning("ExplosionAbility: Could not find expected layers, using all layers");
            }
        }

        protected override void OnActivate()
        {
            // Play explosion ability sound (only for regular ability, not ultimate)
            if (SoundManager.Instance != null)
            {
                SoundManager.Instance.PlaySound(
                    Consts.SoundNames.ExplosionAbility,
                    Consts.SoundVolumes.ExplosionAbility * Consts.SoundVolumes.MasterVolume
                );
            }

            TriggerExplosion(deadly: false);
            // Start cooldown immediately since this is an instant ability
            isActive = false;
            StartCooldown();
        }

        protected override void OnActivateUltimate()
        {
            TriggerExplosion(deadly: true);
            // Start cooldown immediately since this is an instant ability
            isActive = false;
            isUltimateActive = false;
            StartCooldown();
        }

        protected override void OnDeactivate()
        {
            // Nothing to do - explosion is instant
        }

        private struct ExplosionParams
        {
            public Vector3 Position;
            public float KnockBackRadius;
            public float DeathRadius;
            public float KnockBackStrength;
        }

        private void TriggerExplosion(bool deadly)
        {
            if (playerController == null || spiderHealthSystem == null)
            {
                Logger.LogWarning($"ExplosionAbility: Missing playerController or spiderHealthSystem for player {playerInput?.playerIndex}");
                return;
            }

            ExplosionParams explosionParams = CalculateExplosionParameters(deadly);

            // Visual effects
            ApplyCameraEffects(deadly, explosionParams.KnockBackRadius);

            if (deadly)
            {
                SpawnExplosionVFX(explosionParams.Position, GetDeathRadius(deadly));
            }

            // Physics (Host only)
            if (NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsServer)
            {
                ApplyExplosionPhysics(explosionParams, deadly);
            }
        }

        private ExplosionParams CalculateExplosionParameters(bool deadly)
        {
            return new ExplosionParams
            {
                Position = spiderHealthSystem.transform.position,
                KnockBackRadius = GetKnockBackRadius(deadly),
                KnockBackStrength = GetKnockBackStrength(deadly),
                DeathRadius = GetDeathRadius(deadly),
            };
        }

        private void ApplyCameraEffects(bool deadly, float radius)
        {
            try
            {
                CameraEffects.instance?.DoChromaticAberration(0.5f, 0.02f);
                float shakeStrength = deadly ? (radius / 2f) : (radius / 4f);
                CameraEffects.instance?.DoScreenShake(shakeStrength, Consts.Values.Explosion.CameraShakeDuration);
            }
            catch (System.Exception ex)
            {
                Logger.LogWarning($"ExplosionAbility: Could not trigger camera effects: {ex.Message}");
            }
        }

        private void ApplyExplosionPhysics(ExplosionParams p, bool deadly)
        {
            int playerID = playerController.playerID.Value;

            // Use NonAlloc to avoid allocating new array every time
            int hitCount = Physics2D.OverlapCircleNonAlloc(p.Position, p.KnockBackRadius, _explosionResults, explosionLayers);

            for (int i = 0; i < hitCount; i++)
            {
                Collider2D collider = _explosionResults[i];
                if (collider == null) continue;
                if (collider.gameObject == gameObject) continue;

                // Optimization: TryGetComponent on the object first (common case), fallback to Parent
                if (!collider.TryGetComponent<IDamageable>(out var damageable))
                {
                    damageable = collider.GetComponentInParent<IDamageable>();
                }

                if (damageable == null) continue;

                Vector2 closestPoint = collider.ClosestPoint(p.Position);
                float distance = Vector2.Distance(p.Position, closestPoint);
                if (distance < 0.1f) distance = 0.1f;

                Vector2 direction = (closestPoint - (Vector2)p.Position).normalized;
                Vector2 force = direction * p.KnockBackStrength;

                if (collider.CompareTag("PlayerRigidbody"))
                {
                    PlayerController hitPlayerController = collider.transform.parent?.parent?.GetComponent<PlayerController>();
                    if (hitPlayerController != null && hitPlayerController.playerID.Value == playerID)
                    {
                        continue;
                    }
                }

                if (distance > p.DeathRadius)
                {
                    damageable.Impact(force, closestPoint, true, true);
                }
                else
                {
                    if (deadly)
                    {
                        damageable.Damage(force, closestPoint, true);
                    }
                    else
                    {
                        damageable.Impact(force, closestPoint, true, true);
                    }
                }
            }
        }

        private void SpawnExplosionVFX(Vector3 position, float radius = 1f)
        {
            try
            {
                GameObject explosionPrefab = GetExplosionPrefab();
                if (explosionPrefab == null)
                {
                    Logger.LogWarning("ExplosionAbility: Could not get explosion prefab");
                    return;
                }

                GameObject explosionVFX = Instantiate(explosionPrefab, position, Quaternion.identity);
                float vfxScale = Mathf.Clamp(radius / Consts.Values.Explosion.UltimateBaseDeathRadius, 0.5f, 2f);
                explosionVFX.transform.localScale *= vfxScale;

                if (playerController != null)
                {
                    try
                    {
                        Color playerColor = playerController.playerColor.Value;
                        var changeColor = explosionVFX.GetComponent<ChangeExplosionColor>();
                        if (changeColor != null)
                        {
                            changeColor.SetExplosionColor(playerColor);
                        }
                    }
                    catch (System.Exception ex)
                    {
                        Logger.LogWarning($"ExplosionAbility: Could not set explosion color: {ex.Message}");
                    }
                }
            }
            catch (System.Exception ex)
            {
                Logger.LogError($"ExplosionAbility: Failed to spawn explosion VFX: {ex.Message}");
            }
        }

        private static FieldInfo _deadExplosionPrefabField;

        private GameObject GetExplosionPrefab()
        {
            if (cachedExplosionPrefab != null) return cachedExplosionPrefab;

            if (spiderHealthSystem != null)
            {
                try
                {
                    if (_deadExplosionPrefabField == null)
                    {
                        _deadExplosionPrefabField = typeof(SpiderHealthSystem).GetField("DeadExplosionParticlePrefab",
                            BindingFlags.NonPublic | BindingFlags.Instance);
                    }

                    if (_deadExplosionPrefabField != null)
                    {
                        cachedExplosionPrefab = _deadExplosionPrefabField.GetValue(spiderHealthSystem) as GameObject;
                        if (cachedExplosionPrefab != null)
                        {
                            return cachedExplosionPrefab;
                        }
                    }
                }
                catch (System.Exception ex)
                {
                    Logger.LogWarning($"ExplosionAbility: Failed to get DeadExplosionParticlePrefab via reflection: {ex.Message}");
                }
            }
            return null;
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            if (playerInput != null && playerExplosions.ContainsKey(playerInput))
            {
                playerExplosions.Remove(playerInput);
            }
        }
    }
}
