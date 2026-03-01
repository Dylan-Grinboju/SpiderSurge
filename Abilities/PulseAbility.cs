using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using System.Reflection;
using Unity.Netcode;
using Interfaces;
using Logger = Silk.Logger;

namespace SpiderSurge;

public class PulseAbility : BaseAbility
{
    public static Dictionary<PlayerInput, PulseAbility> playerPulseAbilities = [];

    public override string PerkName => Consts.PerkNames.PulseAbility;

    public override float AbilityBaseCooldown => Consts.Values.Pulse.AbilityBaseCooldown;
    public override float AbilityBaseDuration => Consts.Values.Pulse.AbilityBaseDuration;
    public override float UltimateBaseDuration => Consts.Values.Pulse.UltimateBaseDuration;
    public override float UltimateBaseCooldown => Consts.Values.Pulse.UltimateBaseCooldown;
    public override float AbilityCooldownPerPerkLevel => Consts.Values.Pulse.AbilityCooldownReductionPerLevel;
    public override float UltimateCooldownPerPerkLevel => Consts.Values.Pulse.UltimateCooldownReductionPerLevel;

    public override bool HasUltimate => true;
    public override string UltimatePerkDisplayName => "Thermal Detonation";
    public override string UltimatePerkDescription => "Pulse deals lethal damage in the death zone instead of just knockback.";

    private float GetKnockBackRadius(bool isUlt)
    {
        int durationLevel = PerksManager.Instance?.GetPerkLevel(Consts.PerkNames.AbilityDuration) ?? 0;
        float radius;
        int shortTermLevel = PerksManager.Instance?.GetPerkLevel(Consts.PerkNames.ShortTermInvestment) ?? 0;
        int longTermLevel = PerksManager.Instance?.GetPerkLevel(Consts.PerkNames.LongTermInvestment) ?? 0;
        if (isUlt)
        {
            radius = Consts.Values.Pulse.UltimateBaseKnockbackRadius;
            if (durationLevel >= 2) radius += Consts.Values.Pulse.UltimateKnockbackRadiusIncreasePerLevel;
            if (shortTermLevel > 0) radius -= Consts.Values.Pulse.UltimateKnockbackRadiusIncreasePerLevel;
            if (longTermLevel > 0) radius += Consts.Values.Pulse.UltimateKnockbackRadiusIncreasePerLevel;
        }
        else
        {
            radius = Consts.Values.Pulse.AbilityBaseKnockbackRadius;
            if (durationLevel >= 1) radius += Consts.Values.Pulse.AbilityKnockbackRadiusIncreasePerLevel;
            if (shortTermLevel > 0) radius += Consts.Values.Pulse.AbilityKnockbackRadiusIncreasePerLevel;
            if (longTermLevel > 0) radius -= Consts.Values.Pulse.AbilityKnockbackRadiusIncreasePerLevel;
        }
        return radius;
    }

    private float GetKnockBackStrength(bool isUlt)
    {
        float strength;
        int biggerBoom = 0;
        if (ModifierManager.instance is not null)
        {
            biggerBoom = ModifierManager.instance.GetModLevel(Consts.ModifierNames.BiggerBoom);
        }

        if (isUlt)
        {
            strength = Consts.Values.Pulse.UltimateBaseKnockbackStrength;
            if (biggerBoom > 1) strength += Consts.Values.Pulse.UltimateKnockbackStrengthIncreasePerLevel;
        }
        else
        {
            strength = Consts.Values.Pulse.AbilityBaseKnockbackStrength;
            if (biggerBoom > 0) strength += Consts.Values.Pulse.AbilityKnockbackStrengthIncreasePerLevel;
        }

        return strength;
    }

    private float GetDeathRadius(bool isUlt)
    {
        if (!isUlt)
        {
            return 0f;
        }

        float deathRadius = Consts.Values.Pulse.UltimateBaseDeathRadius;

        if (ModifierManager.instance is not null)
        {
            int tooCool = ModifierManager.instance.GetModLevel(Consts.ModifierNames.TooCool);
            deathRadius += Consts.Values.Pulse.UltimateDeathRadiusIncreasePerLevel * tooCool;
        }

        return deathRadius;
    }

    private LayerMask pulseLayers;

    // Cached pulse VFX prefab (obtained from SpiderHealthSystem at runtime)
    private static GameObject cachedExplosionPrefab;

    // Pre-allocated buffer for physics queries to avoid GC allocation
    private Collider2D[] _pulseResults = new Collider2D[64];

    protected override void Awake()
    {
        base.Awake();
        if (playerInput is not null)
        {
            playerPulseAbilities[playerInput] = this;
        }

        pulseLayers = LayerMask.GetMask("Player", "Item", "Enemy", "EnemyWeapon", "DynamicWorld");

        if (pulseLayers == 0)
        {
            pulseLayers = ~0;// All layers
            Logger.LogWarning("PulseAbility: Could not find expected layers, using all layers");
        }
    }

    protected override void OnActivate()
    {
        // Play pulse ability sound (only for regular ability, not ultimate)
        SoundManager.Instance?.PlaySound(
                Consts.SoundNames.PulseAbility,
                Consts.SoundVolumes.PulseAbility * Consts.SoundVolumes.MasterVolume
            );

        TriggerPulse(deadly: false);
        // Start cooldown immediately since this is an instant ability
        isActive = false;
        StartCooldown();
    }

    protected override void OnActivateUltimate()
    {
        TriggerPulse(deadly: true);
        // Start cooldown immediately since this is an instant ability
        isActive = false;
        isUltimateActive = false;
        StartCooldown(wasUltimate: true);
    }

    protected override void OnDeactivate()
    {
        // Nothing to do - pulse is instant
    }

    private struct PulseParams
    {
        public Vector3 Position;
        public float KnockBackRadius;
        public float DeathRadius;
        public float KnockBackStrength;
    }

    private void TriggerPulse(bool deadly)
    {
        if (playerController is null || spiderHealthSystem is null)
        {
            Logger.LogWarning($"PulseAbility: Missing playerController or spiderHealthSystem for player {playerInput?.playerIndex}");
            return;
        }

        PulseParams pulseParams = CalculatePulseParameters(deadly);

        // Visual effects
        ApplyCameraEffects(deadly, pulseParams.KnockBackRadius);
        SpawnRingVisual(pulseParams.Position, pulseParams.KnockBackRadius);

        if (deadly)
        {
            SpawnExplosionVFX(pulseParams.Position, GetDeathRadius(deadly));
        }

        // Physics (Host only)
        if (NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsServer)
        {
            ApplyPulsePhysics(pulseParams, deadly);
        }
    }

    private PulseParams CalculatePulseParameters(bool deadly)
    {
        return new PulseParams
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
            CameraEffects.instance?.DoScreenShake(shakeStrength, Consts.Values.Pulse.CameraShakeDuration);
        }
        catch (System.Exception ex)
        {
            Logger.LogWarning($"PulseAbility: Could not trigger camera effects: {ex.Message}");
        }
    }

    private void ApplyPulsePhysics(PulseParams p, bool deadly)
    {
        int playerID = playerController.playerID.Value;

        // Use NonAlloc to avoid allocating new array every time
        int hitCount = Physics2D.OverlapCircleNonAlloc(p.Position, p.KnockBackRadius, _pulseResults, pulseLayers);

        for (int i = 0; i < hitCount; i++)
        {
            Collider2D collider = _pulseResults[i];
            if (collider is null) continue;
            if (collider.gameObject == gameObject) continue;

            // Optimization: TryGetComponent on the object first (common case), fallback to Parent
            if (!collider.TryGetComponent<IDamageable>(out var damageable))
            {
                damageable = collider.GetComponentInParent<IDamageable>();
            }

            if (damageable is null) continue;

            Vector2 closestPoint = collider.ClosestPoint(p.Position);
            float distance = Vector2.Distance(p.Position, closestPoint);
            if (distance < 0.1f) distance = 0.1f;

            Vector2 direction = (closestPoint - (Vector2)p.Position).normalized;
            Vector2 force = direction * p.KnockBackStrength;

            Rigidbody2D rb = collider.attachedRigidbody;

            if (collider.CompareTag("PlayerRigidbody"))
            {
                PlayerController hitPlayerController = collider.transform.parent?.parent?.GetComponent<PlayerController>();
                if (hitPlayerController is not null && hitPlayerController.playerID.Value == playerID)
                {
                    continue;
                }
            }

            rb?.velocity = Vector2.zero;

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

        ApplyPulseToRailShotsWithoutColliders(p);
    }

    private void ApplyPulseToRailShotsWithoutColliders(PulseParams p)
    {
        IReadOnlyList<RailShot> railShots = RailShotTracker.All;
        if (railShots.Count == 0)
        {
            return;
        }

        for (int i = 0; i < railShots.Count; i++)
        {
            RailShot railShot = railShots[i];
            if (railShot is null || !railShot.gameObject.activeInHierarchy)
            {
                continue;
            }

            Rigidbody2D rb = railShot.GetComponent<Rigidbody2D>();
            if (rb is null)
            {
                continue;
            }

            if (rb.attachedColliderCount > 0)
            {
                continue;
            }

            Vector2 railShotPosition = rb.position;
            float distance = Vector2.Distance((Vector2)p.Position, railShotPosition);
            if (distance > p.KnockBackRadius)
            {
                continue;
            }

            Vector2 direction = (railShotPosition - (Vector2)p.Position).normalized;
            if (direction.sqrMagnitude < 0.0001f)
            {
                direction = Vector2.up;
            }

            rb.velocity = Vector2.zero;
            rb.AddForce(direction * p.KnockBackStrength, ForceMode2D.Impulse);
        }
    }

    private void SpawnRingVisual(Vector3 position, float radius)
    {
        try
        {
            GameObject ringObj = new("PulseRing");
            ringObj.transform.position = position;
            var ringEffect = ringObj.AddComponent<PulseRingEffect>();
            // Match the visual expansion to the actual physics radius
            ringEffect.Setup(radius);
        }
        catch (System.Exception ex)
        {
            Logger.LogWarning($"PulseAbility: Failed to spawn ring visual: {ex.Message}");
        }
    }

    private void SpawnExplosionVFX(Vector3 position, float radius = 1f)
    {
        try
        {
            GameObject explosionPrefab = GetExplosionPrefab();
            if (explosionPrefab is null)
            {
                Logger.LogWarning("PulseAbility: Could not get pulse prefab");
                return;
            }

            GameObject explosionVFX = Instantiate(explosionPrefab, position, Quaternion.identity);
            float vfxScale = Mathf.Clamp(radius / Consts.Values.Pulse.UltimateBaseDeathRadius, 0.5f, 2f);
            explosionVFX.transform.localScale *= vfxScale;

            if (playerController is not null)
            {
                try
                {
                    Color playerColor = playerController.playerColor.Value;
                    var changeColor = explosionVFX.GetComponent<ChangeExplosionColor>();
                    changeColor?.SetExplosionColor(playerColor);
                }
                catch (System.Exception ex)
                {
                    Logger.LogWarning($"PulseAbility: Could not set pulse color: {ex.Message}");
                }
            }
        }
        catch (System.Exception ex)
        {
            Logger.LogError($"PulseAbility: Failed to spawn pulse VFX: {ex.Message}");
        }
    }

    private static FieldInfo _deadExplosionPrefabField;

    private GameObject GetExplosionPrefab()
    {
        if (cachedExplosionPrefab is not null) return cachedExplosionPrefab;

        if (spiderHealthSystem is not null)
        {
            try
            {
                _deadExplosionPrefabField ??= typeof(SpiderHealthSystem).GetField("DeadExplosionParticlePrefab",
                    BindingFlags.NonPublic | BindingFlags.Instance);

                if (_deadExplosionPrefabField is not null)
                {
                    cachedExplosionPrefab = _deadExplosionPrefabField.GetValue(spiderHealthSystem) as GameObject;
                    if (cachedExplosionPrefab is not null)
                    {
                        return cachedExplosionPrefab;
                    }
                }
            }
            catch (System.Exception ex)
            {
                Logger.LogWarning($"PulseAbility: Failed to get DeadExplosionParticlePrefab via reflection: {ex.Message}");
            }
        }
        return null;
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();

        if (playerInput is not null && playerPulseAbilities.ContainsKey(playerInput))
        {
            playerPulseAbilities.Remove(playerInput);
        }
    }
}
