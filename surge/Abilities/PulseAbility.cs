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
        public static Dictionary<int, PulseAbility> playerPulseAbilities = new Dictionary<int, PulseAbility>();

        public override string PerkName => Consts.PerkNames.PulseAbility;
        public override AbilityKind Kind => AbilityKind.Pulse;

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

            return strength;
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

            return speed;
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
        private readonly HashSet<EnemyHealthSystem> _enemiesCharged = new HashSet<EnemyHealthSystem>();
        protected override void Start()
        {
            base.Start();
            if (PlayerId >= 0)
            {
                playerPulseAbilities[PlayerId] = this;
            }
        }

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
            if (SoundManager.Instance != null && !IsRemote)
            {
                SoundManager.Instance.PlaySound(
                    Consts.SoundNames.PulseAbility,
                    Consts.SoundVolumes.PulseAbility * Consts.SoundVolumes.MasterVolume
                );
            }

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
            if (Net.IsHost)
            {
                SurgeNet.SendFx(SurgeFx.PulseRing, PlayerId, pulseParams.Position, pulseParams.KnockBackRadius, deadly);
            }

            if (IsAuthority)
            {
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

        private static void ApplyCameraEffects(bool deadly, float radius)
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

        internal static PlayerController CasterOf(PulseAbility ability) => ability.playerController;

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
            _enemiesCharged.Clear();
            if (deadly) ChargeDeathRadius(p, hitCount);

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
                }
            }

            LaunchEnemies(p, deadly);
            ApplyPulseToRailShotsWithoutColliders(p);
            _enemiesCharged.Clear();
        }

        // Charges for every enemy in the death radius before the explosion spawns, since it kills and destroys them at once
        private void ChargeDeathRadius(PulseParams p, int hitCount)
        {
            for (int i = 0; i < hitCount; i++)
            {
                Collider2D collider = _pulseResults[i];
                if (collider == null) continue;
                EnemyHealthSystem enemy = collider.GetComponentInParent<EnemyHealthSystem>();
                if (enemy == null || enemy.dead) continue;
                if (Vector2.Distance(p.Position, collider.ClosestPoint(p.Position)) <= p.DeathRadius)
                {
                    ChargeForEnemy(enemy, true, Consts.Values.Meter.DetonationChargePerKill);
                }
            }
        }

        // Each enemy charges the meter once per pulse: a lethal hit for 2, a launch for 1
        private void ChargeForEnemy(EnemyHealthSystem enemy, bool deadly, int amount)
        {
            if (enemy == null || !enemy.needToKillToCompleteLevel || !_enemiesCharged.Add(enemy)) return;
            if (deadly)
            {
                SurgeMeter.Add(MeterSource.ThermalDetonation, PlayerId, CastId, amount, Consts.Values.Meter.DetonationCapPerCast);
            }
            else
            {
                SurgeMeter.Add(MeterSource.KineticPulse, PlayerId, CastId, amount, Consts.Values.Meter.PulseCapPerCast);
            }
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
            foreach (EnemyHealthSystem enemy in _enemiesToLaunch)
            {
                if (enemy != null && !enemy.dead)
                {
                    PulseKnockback.LaunchEnemy(enemy, p.Position, p.EnemyLaunchSpeed);
                    ChargeForEnemy(enemy, deadly, Consts.Values.Meter.PulseChargePerEnemy);
                }
            }
            _enemiesToLaunch.Clear();
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
                if (railShot == null || !railShot.gameObject.activeInHierarchy)
                {
                    continue;
                }

                Rigidbody2D rb = railShot.GetComponent<Rigidbody2D>();
                if (rb == null)
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

        // ─── Explosion Reflection (for stats mod kill tracking) ─────────────────────
        private static bool _explosionReflectionInitialized;
        private static bool _canUseExplosion;
        private static FieldInfo _expKnockBackRadius;
        private static FieldInfo _expLayers;
        private static FieldInfo _expDeathRadius;
        private static FieldInfo _expPlayerDeathRadius;
        private static FieldInfo _expIsBoomSpear;
        private static FieldInfo _expPlayerExplosionID;
        private static FieldInfo _expOwnerId;
        private static MethodInfo _expKnockBack;

        private static void InitExplosionReflection()
        {
            if (_explosionReflectionInitialized) return;
            _explosionReflectionInitialized = true;

            try
            {
                _expKnockBackRadius = AccessTools.Field(typeof(Explosion), "knockBackRadius");
                _expLayers = AccessTools.Field(typeof(Explosion), "layers");
                _expDeathRadius = AccessTools.Field(typeof(Explosion), "deathRadius");
                _expPlayerDeathRadius = AccessTools.Field(typeof(Explosion), "_playerDeathRadius");
                _expIsBoomSpear = AccessTools.Field(typeof(Explosion), "isBoomSpear");
                _expPlayerExplosionID = AccessTools.Field(typeof(Explosion), "playerExplosionID");
                _expOwnerId = AccessTools.Field(typeof(Explosion), "explosionOwnerId");
                _expKnockBack = AccessTools.Method(typeof(Explosion), "KnockBack");

                _canUseExplosion = _expKnockBack != null
                    && _expKnockBackRadius != null
                    && _expDeathRadius != null
                    && _expOwnerId != null
                    && _expLayers != null
                    && _expPlayerDeathRadius != null
                    && _expIsBoomSpear != null
                    && _expPlayerExplosionID != null;

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

                _expKnockBackRadius.SetValue(explosion, p.DeathRadius);
                _expLayers?.SetValue(explosion, pulseLayers);
                _expDeathRadius.SetValue(explosion, p.DeathRadius);
                _expPlayerDeathRadius?.SetValue(explosion, p.DeathRadius);
                _expIsBoomSpear?.SetValue(explosion, true);
                _expPlayerExplosionID?.SetValue(explosion, playerID);
                _expOwnerId.SetValue(explosion, playerController.OwnerClientId);

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

        protected override void OnDestroy()
        {
            base.OnDestroy();

            if (playerPulseAbilities.TryGetValue(PlayerId, out var registered) && registered == this)
            {
                playerPulseAbilities.Remove(PlayerId);
            }
        }
    }

    internal sealed class PulseExplosionCaster : MonoBehaviour
    {
        internal SpiderHealthSystem Health;
    }

    [HarmonyPatch(typeof(Explosion), "KnockBack")]
    internal static class PulseExplosionScopePatch
    {
        internal static SpiderHealthSystem ProtectedSpider;

        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        private static void Prefix(Explosion __instance, out SpiderHealthSystem __state)
        {
            __state = ProtectedSpider;
            ProtectedSpider = __instance.GetComponent<PulseExplosionCaster>()?.Health;
        }

        [HarmonyFinalizer]
        private static void Finalizer(SpiderHealthSystem __state)
        {
            ProtectedSpider = __state;
        }
    }
}
