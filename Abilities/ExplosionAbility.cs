using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using Unity.Netcode;
using Interfaces;
using Logger = Silk.Logger;

namespace SpiderSurge
{
    public class ExplosionAbility : BaseAbility
    {
        public static Dictionary<PlayerInput, ExplosionAbility> playerExplosions = new Dictionary<PlayerInput, ExplosionAbility>();

        public override string PerkName => "explosionAbility";

        // Instant ability - no duration
        public override float BaseCooldown => 11f;
        public override float CooldownPerPerkLevel => 5f;

        // Base explosion parameters - matching afterlife explosion from SpiderHealthSystem
        private const float BASE_KNOCKBACK_RADIUS = 80f;
        private const float BASE_KNOCKBACK_STRENGTH = 50f;
        private const float BASE_DEATH_RADIUS = 42f;

        // Explosion size scaling per duration perk level (25% increase per level)
        private const float SIZE_SCALE_PER_LEVEL = 0.25f;

        // Computed explosion parameters based on duration perk
        private float ExplosionSizeMultiplier => 1f + (PerksManager.Instance?.GetPerkLevel("abilityDuration") ?? 0) * SIZE_SCALE_PER_LEVEL;
        private float KnockBackRadius => BASE_KNOCKBACK_RADIUS * ExplosionSizeMultiplier;
        private float KnockBackStrength => BASE_KNOCKBACK_STRENGTH * ExplosionSizeMultiplier;
        private float DeathRadius => BASE_DEATH_RADIUS * ExplosionSizeMultiplier;

        // Layer mask for detecting damageable objects
        private LayerMask explosionLayers;

        protected override void Awake()
        {
            base.Awake();
            if (playerInput != null)
            {
                playerExplosions[playerInput] = this;
            }

            // Layer names verified via Unity Explorer
            explosionLayers = LayerMask.GetMask("Player", "Item", "Enemy");

            // If the layer mask is 0, try to include everything that's typically damageable
            if (explosionLayers == 0)
            {
                // Fallback: use all layers except Ignore Raycast
                explosionLayers = ~0; // All layers
                Logger.LogWarning("ExplosionAbility: Could not find expected layers, using all layers");
            }
        }

        protected override void OnActivate()
        {
            Logger.LogInfo($"ExplosionAbility ACTIVATED for player {playerInput.playerIndex}!");

            // Trigger the explosion immediately
            TriggerExplosion();

            // Start cooldown immediately since this is an instant ability
            isActive = false;
            StartCooldown();
        }

        protected override void OnDeactivate()
        {
            // Nothing to do - explosion is instant
        }

        private void TriggerExplosion()
        {
            if (playerController == null || spiderHealthSystem == null)
            {
                Logger.LogWarning($"ExplosionAbility: Missing playerController or spiderHealthSystem for player {playerInput?.playerIndex}");
                return;
            }

            Vector3 explosionPosition = spiderHealthSystem.transform.position;
            int playerID = playerController.playerID.Value;

            // Visual effects - screen shake and chromatic aberration
            try
            {
                CameraEffects.instance?.DoChromaticAberration(0.5f, 0.02f);
                CameraEffects.instance?.DoScreenShake(KnockBackRadius / 2f, 5f);
            }
            catch (System.Exception ex)
            {
                Logger.LogWarning($"ExplosionAbility: Could not trigger camera effects: {ex.Message}");
            }

            // Only process damage/knockback on the host/server
            if (!NetworkManager.Singleton.IsHost && !NetworkManager.Singleton.IsServer)
            {
                Logger.LogInfo($"ExplosionAbility: Client-side only, skipping damage processing");
                return;
            }

            int hitCount = 0;
            int damageCount = 0;

            // Find all colliders in the explosion radius
            Collider2D[] hitColliders = Physics2D.OverlapCircleAll(explosionPosition, KnockBackRadius, explosionLayers);

            foreach (Collider2D collider in hitColliders)
            {
                if (collider == null) continue;

                // Skip our own collider
                if (collider.gameObject == gameObject) continue;

                // Get the damageable component
                IDamageable damageable = collider.GetComponentInParent<IDamageable>();
                if (damageable == null) continue;

                hitCount++;

                // Calculate explosion force
                Vector2 closestPoint = collider.ClosestPoint(explosionPosition);
                float distance = Vector2.Distance(explosionPosition, closestPoint);

                // Avoid division by very small numbers
                if (distance < 0.1f) distance = 0.1f;

                float forceMultiplier = KnockBackStrength * Mathf.Clamp(KnockBackRadius / distance, 0f, 100f);
                Vector2 direction = (closestPoint - (Vector2)explosionPosition).normalized;
                Vector2 force = direction * forceMultiplier;

                // Check if this is our own player - if so, skip damage entirely
                if (collider.CompareTag("PlayerRigidbody"))
                {
                    // Check if this is the player who triggered the explosion
                    PlayerController hitPlayerController = collider.transform.parent?.parent?.GetComponent<PlayerController>();
                    if (hitPlayerController != null && hitPlayerController.playerID.Value == playerID)
                    {
                        Logger.LogInfo($"Skip damage to own player {playerID} in ExplosionAbility");
                        continue;
                    }
                }

                if (distance > DeathRadius)
                {
                    // Outside death radius - just knockback
                    damageable.Impact(force * 4f, closestPoint, true, true);
                }
                else
                {
                    // Inside death radius - full damage
                    damageable.Damage(force, closestPoint, true);
                    damageCount++;
                }
            }

            Logger.LogInfo($"ExplosionAbility: Explosion hit {hitCount} targets, damaged {damageCount} at position {explosionPosition}");

            // Show visual circles for explosion radius
            CreateExplosionCircle(explosionPosition, DeathRadius, Color.red, 0.5f);
            CreateExplosionCircle(explosionPosition, KnockBackRadius, new Color(1f, 0.5f, 0f, 1f), 0.5f); // Orange for knockback
        }

        private void CreateExplosionCircle(Vector3 center, float radius, Color color, float duration)
        {
            // Create a new GameObject for the circle
            GameObject circleObj = new GameObject("ExplosionCircle");
            circleObj.transform.position = center;

            // Add a LineRenderer to draw the circle
            LineRenderer lineRenderer = circleObj.AddComponent<LineRenderer>();

            // Configure the line renderer
            int segments = 64;
            lineRenderer.positionCount = segments + 1;
            lineRenderer.useWorldSpace = true;
            lineRenderer.startWidth = 2f;
            lineRenderer.endWidth = 2f;
            lineRenderer.loop = true;

            // Create a simple material
            lineRenderer.material = new Material(Shader.Find("Sprites/Default"));
            lineRenderer.startColor = color;
            lineRenderer.endColor = color;

            // Calculate circle points
            float angle = 0f;
            for (int i = 0; i <= segments; i++)
            {
                float x = Mathf.Sin(Mathf.Deg2Rad * angle) * radius;
                float y = Mathf.Cos(Mathf.Deg2Rad * angle) * radius;
                lineRenderer.SetPosition(i, new Vector3(center.x + x, center.y + y, center.z));
                angle += 360f / segments;
            }

            // Start fade out coroutine
            StartCoroutine(FadeOutCircle(circleObj, lineRenderer, duration));
        }

        private System.Collections.IEnumerator FadeOutCircle(GameObject circleObj, LineRenderer lineRenderer, float duration)
        {
            float elapsed = 0f;
            Color startColor = lineRenderer.startColor;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float alpha = Mathf.Lerp(1f, 0f, elapsed / duration);
                Color fadedColor = new Color(startColor.r, startColor.g, startColor.b, alpha);
                lineRenderer.startColor = fadedColor;
                lineRenderer.endColor = fadedColor;
                yield return null;
            }

            Destroy(circleObj);
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
