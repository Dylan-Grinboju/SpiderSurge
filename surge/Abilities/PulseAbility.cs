using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using System.Reflection;
using Unity.Netcode;
using Interfaces;
using HarmonyLib;
using SpiderSurge.Common;
using SpiderSurge.Vfx;
using Logger = Silk.Logger;

namespace SpiderSurge
{
    public class PulseAbility : BaseAbility
    {
        public override string PerkName => Consts.PerkNames.PulseAbility;
        public override AbilityKind Kind => AbilityKind.Pulse;

        public override float AbilityBaseCooldown => Consts.Values.Pulse.AbilityBaseCooldown;
        public override float AbilityBaseDuration => Consts.Values.Pulse.AbilityBaseDuration;
        public override float UltimateBaseDuration => Consts.Values.Pulse.UltimateBaseDuration;
        public override float UltimateBaseCooldown => Consts.Values.Pulse.UltimateBaseCooldown;
        public override float AbilityCooldownPerPerkLevel => Consts.Values.Pulse.AbilityCooldownReductionPerLevel;
        public override float UltimateCooldownPerPerkLevel => Consts.Values.Pulse.UltimateCooldownReductionPerLevel;

        public override bool HasUltimate => true;

        private float GetKnockBackRadius(bool isUlt)
        {
            return isUlt
                ? PerkScaled(Consts.Values.Pulse.UltimateBaseKnockbackRadius, Consts.Values.Pulse.UltimateKnockbackRadiusIncreasePerLevel, true)
                : PerkScaled(Consts.Values.Pulse.AbilityBaseKnockbackRadius, Consts.Values.Pulse.AbilityKnockbackRadiusIncreasePerLevel, false);
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
                strength = Consts.Values.Pulse.UltimateBaseKnockbackStrength;
                if (biggerBoom > 1) strength += Consts.Values.Pulse.UltimateKnockbackStrengthIncreasePerLevel;
            }
            else
            {
                strength = Consts.Values.Pulse.AbilityBaseKnockbackStrength;
                if (biggerBoom > 0) strength += Consts.Values.Pulse.AbilityKnockbackStrengthIncreasePerLevel;
            }

            return strength * PulseKnockback.GravityStrengthMultiplier;
        }

        private float GetEnemyLaunchSpeed(bool isUlt)
        {
            float speed;
            int biggerBoom = 0;
            if (ModifierManager.instance != null)
            {
                biggerBoom = ModifierManager.instance.GetModLevel(Consts.ModifierNames.BiggerBoom);
            }

            if (isUlt)
            {
                speed = Consts.Values.Pulse.UltimateBaseEnemyLaunchSpeed;
                if (biggerBoom > 1) speed += Consts.Values.Pulse.UltimateEnemyLaunchSpeedIncreasePerLevel;
            }
            else
            {
                speed = Consts.Values.Pulse.AbilityBaseEnemyLaunchSpeed;
                if (biggerBoom > 0) speed += Consts.Values.Pulse.AbilityEnemyLaunchSpeedIncreasePerLevel;
            }

            return speed * PulseKnockback.GravityStrengthMultiplier;
        }

        private static float GetDeathRadius(bool isUlt)
        {
            if (!isUlt)
            {
                return 0f;
            }

            float deathRadius = Consts.Values.Pulse.UltimateBaseDeathRadius;

            if (ModifierManager.instance != null)
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

        private readonly HashSet<EnemyHealthSystem> _enemiesToLaunch = new HashSet<EnemyHealthSystem>();

        protected override void Awake()
        {
            base.Awake();

            pulseLayers = LayerMask.GetMask("Player", "Item", "Weapon", "Enemy", "EnemyWeapon", "DynamicWorld");

            if (pulseLayers == 0)
            {
                pulseLayers = ~0;// All layers
                Logger.LogWarning("PulseAbility: Could not find expected layers, using all layers");
            }

            InitExplosionReflection();
        }

        protected override void OnActivate()
        {
            PlayCasterSound(Consts.SoundNames.PulseAbility, Consts.SoundVolumes.PulseAbility);

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

        private struct PulseParams
        {
            public Vector3 Position;
            public float KnockBackRadius;
            public float DeathRadius;
            public float KnockBackStrength;
            public float EnemyLaunchSpeed;
        }

        private void TriggerPulse(bool deadly)
        {
            if (playerController == null || spiderHealthSystem == null)
            {
                Logger.LogWarning($"PulseAbility: Missing playerController or spiderHealthSystem for player {playerInput?.playerIndex}");
                return;
            }

            PulseParams pulseParams = CalculatePulseParameters(deadly);

            DrawPulse(pulseParams.Position, pulseParams.KnockBackRadius, deadly, GetExplosionPrefab(spiderHealthSystem), playerController);
            SurgeNet.SendFx(SurgeFx.PulseRing, PlayerId, pulseParams.Position, pulseParams.KnockBackRadius, deadly);

            if (IsAuthority)
            {
                deadlyPulse = deadly;
                ApplyPulsePhysics(pulseParams, deadly);
            }
        }

        // Everything a machine shows for a pulse; the physics stays with the host
        private static void DrawPulse(Vector3 position, float radius, bool deadly, GameObject explosionPrefab, PlayerController owner)
        {
            ApplyCameraEffects(deadly, radius);
            PulseVfx.Draw(position, radius, deadly);
            if (deadly)
            {
                SpawnExplosionVFX(position, GetDeathRadius(true), explosionPrefab, owner);
            }
        }

        public static void DrawRemotePulse(int playerId, Vector3 position, float radius, bool deadly)
        {
            var owner = Players.ById(playerId);
            DrawPulse(position, radius, deadly, GetExplosionPrefab(Players.HealthOf(playerId)), owner);
        }

        private PulseParams CalculatePulseParameters(bool deadly)
        {
            return new PulseParams
            {
                Position = spiderHealthSystem.transform.position,
                KnockBackRadius = GetKnockBackRadius(deadly),
                KnockBackStrength = GetKnockBackStrength(deadly),
                DeathRadius = GetDeathRadius(deadly),
                EnemyLaunchSpeed = GetEnemyLaunchSpeed(deadly),
            };
        }

        // Reduced visual effects keeps a quarter of the shake and the colour fringe
        private static void ApplyCameraEffects(bool deadly, float radius)
        {
            try
            {
                float scale = ModConfig.ReducedEffects ? Consts.Values.Pulse.ReducedCameraEffectScale : 1f;
                CameraEffects.instance?.DoChromaticAberration(0.5f * scale, 0.02f);
                float shakeStrength = (deadly ? (radius / 2f) : (radius / 4f)) * scale;
                CameraEffects.instance?.DoScreenShake(shakeStrength, Consts.Values.Pulse.CameraShakeDuration);
            }
            catch (System.Exception ex)
            {
                Logger.LogWarning($"PulseAbility: Could not trigger camera effects: {ex.Message}");
            }
        }

        internal static PlayerController CasterOf(PulseAbility ability) => ability.playerController;

        private bool deadlyPulse;

        internal static bool IsDeadly(PulseAbility ability) => ability.deadlyPulse;

        private readonly HashSet<int> impactAccents = new HashSet<int>();

        private void ApplyPulsePhysics(PulseParams p, bool deadly)
        {
            int playerID = playerController.playerID.Value;

            bool _attemptedLethalExplosion = false;

            float queryRadius = deadly ? Mathf.Max(p.KnockBackRadius, p.DeathRadius) : p.KnockBackRadius;
            int hitCount;
            while ((hitCount = Physics2D.OverlapCircleNonAlloc(p.Position, queryRadius, _pulseResults, pulseLayers)) == _pulseResults.Length)
            {
                System.Array.Resize(ref _pulseResults, _pulseResults.Length * 2);
            }
            CollectEnemiesToLaunch(hitCount, p);
            impactAccents.Clear();

            for (int i = 0; i < hitCount; i++)
            {
                Collider2D collider = _pulseResults[i];
                if (collider == null) continue;
                if (collider.gameObject == gameObject) continue;

                if (BelongsToActivator(collider, playerID)) continue;

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

                Rigidbody2D rb = collider.attachedRigidbody;

                // When deadly and within death radius, let the Explosion component handle damage
                // (the stats mod tracks kills from Explosion.KnockBack automatically)
                if (deadly && distance <= p.DeathRadius)
                {
                    if (_canUseExplosion && !_attemptedLethalExplosion)
                    {
                        _attemptedLethalExplosion = true;
                        if (!SpawnLethalExplosion(p))
                        {
                            _canUseExplosion = false;
                        }
                    }

                    if (!_canUseExplosion)
                    {
                        // Fallback: apply damage directly if Explosion reflection not available
                        if (rb != null)
                            rb.velocity = Vector2.zero;
                        damageable.Damage(force, closestPoint, true);
                    }
                    continue;
                }
                else if (distance <= p.KnockBackRadius)
                {
                    if (rb != null)
                        rb.velocity = Vector2.zero;
                    damageable.Impact(force, closestPoint, true, true);
                    var component = damageable as Component;
                    if (component != null && impactAccents.Add(component.GetInstanceID()))
                    {
                        SurgeVfx.Safely(() => AccentVfx.Impact(closestPoint, direction, deadly));
                        SurgeNet.SendFx(SurgeFx.PulseImpact, PlayerId, closestPoint, Mathf.Atan2(direction.y, direction.x), deadly);
                    }
                    if (!deadly && rb != null)
                    {
                        var missile = rb.GetComponent<MissileAchievementOrigin>();
                        if (missile != null && rb.velocity.sqrMagnitude > 0f) missile.ReturnedBy = playerController;
                    }
                }
            }

            LaunchEnemies(p, deadly);
        }

        // Taken before any hit lands, so an enemy whose shield this pulse breaks still counts as hit rather than immune
        private void CollectEnemiesToLaunch(int hitCount, PulseParams p)
        {
            _enemiesToLaunch.Clear();
            for (int i = 0; i < hitCount; i++)
            {
                Collider2D collider = _pulseResults[i];
                if (collider == null) continue;
                if (Vector2.Distance(collider.ClosestPoint(p.Position), p.Position) > p.KnockBackRadius) continue;

                EnemyHealthSystem enemy = collider.GetComponentInParent<EnemyHealthSystem>();
                if (PulseKnockback.CanLaunch(enemy))
                {
                    _enemiesToLaunch.Add(enemy);
                }
            }
        }

        private void LaunchEnemies(PulseParams p, bool deadly)
        {
            int launched = 0;
            foreach (EnemyHealthSystem enemy in _enemiesToLaunch)
            {
                if (enemy != null && !enemy.dead)
                {
                    PulseKnockback.LaunchEnemy(enemy, p.Position, p.EnemyLaunchSpeed);
                    if (enemy.needToKillToCompleteLevel)
                    {
                        launched++;
                        var origin = EnemyAchievementOrigin.Of(enemy);
                        origin.PulsePlayer = playerController;
                        origin.RegularPulse = !deadly;
                    }
                }
            }
            _enemiesToLaunch.Clear();
            if (!deadly && launched >= 5)
                AchievementTracker.Run("Personal Space", () => SurgeAchievements.Unlock(SurgeAchievements.PersonalSpace, playerController));
        }

        // The activating player must never be affected by their own pulse.
        // Their spider parts resolve through the PlayerController in the parent chain, but an
        // equipped weapon is a separate network object that is only linked back through
        // Weapon.owner - knocking it back pushes the holder via WeaponManager.AbsorbImpact.
        private bool BelongsToActivator(Collider2D collider, int playerID)
        {
            PlayerController hitPlayerController = collider.GetComponentInParent<PlayerController>();
            if (hitPlayerController != null && hitPlayerController.playerID.Value == playerID)
            {
                return true;
            }

            SpiderHealthSystem hitHealthSystem = collider.GetComponentInParent<SpiderHealthSystem>();
            if (hitHealthSystem != null && hitHealthSystem == spiderHealthSystem)
            {
                return true;
            }

            if (!collider.TryGetComponent<Weapon>(out var weapon))
            {
                weapon = collider.GetComponentInParent<Weapon>();
            }

            if (weapon != null && weapon.equipped && weapon.owner != null && weapon.owner.healthSystem == spiderHealthSystem)
            {
                return true;
            }

            return false;
        }

        // ─── Explosion Reflection (for stats mod kill tracking) ─────────────────────
        private static bool _explosionReflectionInitialized;
        private static bool _canUseExplosion;
        private static FieldInfo _expPlayerDeathRadius;
        private static MethodInfo _expKnockBack;

        private static void InitExplosionReflection()
        {
            if (_explosionReflectionInitialized) return;
            _explosionReflectionInitialized = true;

            try
            {
                _expPlayerDeathRadius = AccessTools.Field(typeof(Explosion), "_playerDeathRadius");
                _expKnockBack = AccessTools.Method(typeof(Explosion), "KnockBack");

                _canUseExplosion = _expKnockBack != null && _expPlayerDeathRadius != null;

                if (!_canUseExplosion)
                    Logger.LogWarning("PulseAbility: Explosion reflection incomplete — pulse ult kills will use fallback (not tracked)");
            }
            catch (System.Exception ex)
            {
                Logger.LogWarning($"PulseAbility: Explosion reflection failed: {ex.Message}");
                _canUseExplosion = false;
            }
        }

        private bool SpawnLethalExplosion(PulseParams p)
        {
            GameObject tempObj = null;
            try
            {
                tempObj = new GameObject("PulseExplosion");
                tempObj.transform.position = p.Position;

                var explosion = tempObj.AddComponent<Explosion>();
                explosion.enabled = false;
                tempObj.AddComponent<PulseExplosionCaster>().Health = spiderHealthSystem;

                int playerID = playerController.playerID.Value;

                explosion.knockBackRadius = p.DeathRadius;
                explosion.layers = pulseLayers;
                explosion.deathRadius = p.DeathRadius;
                _expPlayerDeathRadius?.SetValue(explosion, p.DeathRadius);
                explosion.isBoomSpear = true;
                explosion.playerExplosionID = playerID;
                explosion.explosionOwnerId = playerController.OwnerClientId;

                explosion.enabled = true;
                _expKnockBack.Invoke(explosion, null);

                return true;
            }
            catch (System.Exception ex)
            {
                Logger.LogError($"PulseAbility: Failed to spawn lethal explosion for stats tracking: {ex.Message}");
                return false;
            }
            finally
            {
                if (tempObj != null)
                {
                    Destroy(tempObj);
                }
            }
        }

        private static void SpawnExplosionVFX(Vector3 position, float radius, GameObject explosionPrefab, PlayerController owner)
        {
            try
            {
                if (explosionPrefab == null)
                {
                    Logger.LogWarning("PulseAbility: Could not get pulse prefab");
                    return;
                }

                GameObject explosionVFX = Instantiate(explosionPrefab, position, Quaternion.identity);
                float vfxScale = Mathf.Clamp(radius / Consts.Values.Pulse.UltimateBaseDeathRadius, 0.5f, 2f);
                explosionVFX.transform.localScale *= vfxScale;

                if (owner != null)
                {
                    try
                    {
                        Color playerColor = owner.playerColor.Value;
                        var changeColor = explosionVFX.GetComponent<ChangeExplosionColor>();
                        if (changeColor != null)
                        {
                            changeColor.SetExplosionColor(playerColor);
                        }
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

        private static GameObject GetExplosionPrefab(SpiderHealthSystem spiderHealthSystem)
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
                    Logger.LogWarning($"PulseAbility: Failed to get DeadExplosionParticlePrefab via reflection: {ex.Message}");
                }
            }
            return null;
        }
    }

    internal sealed class PulseExplosionCaster : MonoBehaviour
    {
        internal SpiderHealthSystem Health;
    }

}
