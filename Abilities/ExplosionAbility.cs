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

        // Instant ability - no duration
        public override float BaseCooldown => Consts.Values.Explosion.BaseCooldown;
        public override float BaseDuration => Consts.Values.Explosion.BaseDuration;
        public override float CooldownPerPerkLevel => Consts.Values.Explosion.CooldownReductionPerLevel;
        public override float UltimateCooldownMultiplier => Consts.Values.Explosion.UltimateCooldownMultiplier;

        // Ultimate: Deadly Explosion
        public override bool HasUltimate => true;
        public override string UltimatePerkDisplayName => "Explosion Ultimate";
        public override string UltimatePerkDescription => "Explosion deals lethal damage in the death zone instead of just knockback.";

        // Computed explosion parameters based on duration perk
        private float ExplosionSizeMultiplier => 1f + (PerksManager.Instance?.GetPerkLevel(Consts.PerkNames.AbilityDuration) ?? 0) * Consts.Values.Explosion.SizeScalePerLevel;
        private float KnockBackRadius => Consts.Values.Explosion.BaseKnockbackRadius * ExplosionSizeMultiplier;
        private float KnockBackStrength => Consts.Values.Explosion.BaseKnockbackStrength * ExplosionSizeMultiplier;
        private float DeathRadius => Consts.Values.Explosion.BaseDeathRadius * ExplosionSizeMultiplier;

        // Layer mask for detecting damageable objects
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
            public float SynergyScaleMultiplier;
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
                SpawnExplosionVFX(explosionParams.Position, explosionParams.SynergyScaleMultiplier);
            }

            // Physics (Host only)
            if (NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsServer)
            {
                ApplyExplosionPhysics(explosionParams, deadly);
            }
        }

        private ExplosionParams CalculateExplosionParameters(bool deadly)
        {
            ExplosionParams p = new ExplosionParams
            {
                Position = spiderHealthSystem.transform.position,
                KnockBackRadius = KnockBackRadius,
                KnockBackStrength = KnockBackStrength,
                DeathRadius = DeathRadius,
                SynergyScaleMultiplier = 1f
            };

            if (PerksManager.Instance != null && PerksManager.Instance.GetPerkLevel(Consts.PerkNames.Synergy) > 0 && ModifierManager.instance != null)
            {
                int tooCool = ModifierManager.instance.GetModLevel(Consts.ModifierNames.TooCool);
                if (tooCool > 0 && deadly)
                {
                    float modifier = Consts.Values.Explosion.SynergyDeathZonePerLevel * (tooCool >= 2 ? 2f : 1f);
                    p.DeathRadius *= 1f + modifier;
                    p.SynergyScaleMultiplier = 1f + modifier;
                }

                int biggerBoom = ModifierManager.instance.GetModLevel(Consts.ModifierNames.BiggerBoom);
                if (biggerBoom > 0)
                {
                    float modifier = Consts.Values.Explosion.SynergyKnockbackPerLevel * (biggerBoom >= 2 ? 2f : 1f);
                    p.KnockBackStrength *= 1f + modifier;
                }
            }
            return p;
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

                float forceMultiplier = p.KnockBackStrength * Mathf.Clamp(p.KnockBackRadius / distance, 0f, 100f);
                Vector2 direction = (closestPoint - (Vector2)p.Position).normalized;
                Vector2 force = direction * forceMultiplier;

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
                    damageable.Impact(force * Consts.Values.Explosion.ForceMultiplierOutsideZone, closestPoint, true, true);
                }
                else
                {
                    if (deadly)
                    {
                        damageable.Damage(force, closestPoint, true);
                    }
                    else
                    {
                        damageable.Impact(force * Consts.Values.Explosion.ForceMultiplierInsideZone, closestPoint, true, true);
                    }
                }
            }
        }

        private void SpawnExplosionVFX(Vector3 position, float extraScale = 1f)
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
                explosionVFX.transform.localScale *= ExplosionSizeMultiplier * extraScale;

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
