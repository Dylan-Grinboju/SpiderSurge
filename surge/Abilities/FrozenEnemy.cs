using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using SpiderSurge.Common;
using SpiderSurge.Vfx;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Experimental.Rendering.Universal;

namespace SpiderSurge
{
    // An enemy held by Freeze Time: brains off, bodies and the shaman's cloud locked, fuses held, tinted ice blue until it thaws.
    // It doesn't move, shoot or explode, but its blades, saws, struts and poison cloud still hurt on contact. The Black Hole's pull holds one the same way without locking or tinting it
    public class FrozenEnemy : MonoBehaviour
    {
        private static readonly HashSet<int> FrozenEnemyIds = new HashSet<int>();
        private static readonly HashSet<int> PulledEnemyIds = new HashSet<int>();
        private static readonly HashSet<int> FrozenBrainIds = new HashSet<int>();
        private static readonly AccessTools.FieldRef<ExplodingRoller, bool> RollerTriggered =
            AccessTools.FieldRefAccess<ExplodingRoller, bool>("_triggered");
        private static readonly AccessTools.FieldRef<ExplodingRoller, float> RollerTimeToExplode =
            AccessTools.FieldRefAccess<ExplodingRoller, float>("_timeToExplode");
        private static readonly MethodInfo WhispStopCharge = AccessTools.Method(typeof(WhispBrain), "StopChargeCosmeticsClientRpc");
        private static readonly MethodInfo KhepriSwitchCharge = AccessTools.Method(typeof(KhepriBrain), "SwitchChargeAttackCosmeticsClientRpc");
        private const float RollerFuse = 2f;
        // Enemies frozen by one cast thaw together, so their sound plays once
        private const float ThawSoundGap = 0.15f;
        private static float _lastThawSound = float.NegativeInfinity;

        private EnemyHealthSystem _enemy;
        private int _enemyId;
        private int _frozenBy = -1;
        private float _thawTime;
        private float _pullEnd;
        private float? _fuseLeft;
        private bool _held;
        private bool _visualOnly;
        private bool _pulled;
        private readonly List<EnemyBrain> _brains = new List<EnemyBrain>();
        private readonly List<EnemyBrain> _disabledBrains = new List<EnemyBrain>();
        private readonly List<KeyValuePair<Rigidbody2D, RigidbodyConstraints2D>> _bodies = new List<KeyValuePair<Rigidbody2D, RigidbodyConstraints2D>>();
        private readonly Dictionary<SpriteRenderer, Tint> _tints = new Dictionary<SpriteRenderer, Tint>();
        private readonly List<SpriteRenderer> _recolored = new List<SpriteRenderer>();
        private FrostAura _frost;

        private struct Tint
        {
            public Color Base;
            public Color Shown;
        }

        public float TimeLeft => Mathf.Max(0f, _thawTime - Time.time);

        public static bool IsFrozen(EnemyHealthSystem enemy) => enemy != null && FrozenEnemyIds.Contains(enemy.GetInstanceID());

        public static bool AnyFrozen => FrozenEnemyIds.Count > 0;

        public static bool IsFrozenBrain(EnemyBrain brain) => brain != null && FrozenBrainIds.Contains(brain.GetInstanceID());

        public static bool IsHeld(EnemyHealthSystem enemy) => IsFrozen(enemy) || (enemy != null && PulledEnemyIds.Contains(enemy.GetInstanceID()));

        public static bool CanFreeze(EnemyHealthSystem enemy)
        {
            return enemy != null && !enemy.IsDead() && enemy.needToKillToCompleteLevel && !SwallowedTarget.IsSwallowed(enemy.gameObject);
        }

        public static float TimeLeftOn(EnemyHealthSystem enemy)
        {
            FrozenEnemy frozen = enemy != null ? enemy.GetComponent<FrozenEnemy>() : null;
            return frozen != null && !frozen._pulled ? frozen.TimeLeft : 0f;
        }

        public static float ThawTimeOf(EnemyHealthSystem enemy)
        {
            FrozenEnemy frozen = enemy != null ? enemy.GetComponent<FrozenEnemy>() : null;
            return frozen != null ? frozen._thawTime : 0f;
        }

        public static int FreezerOf(EnemyHealthSystem enemy)
        {
            FrozenEnemy frozen = enemy != null ? enemy.GetComponent<FrozenEnemy>() : null;
            return frozen != null ? frozen._frozenBy : -1;
        }

        internal static void SetBaseColor(SpriteRenderer sprite, Color color)
        {
            FrozenEnemy frozen = sprite.GetComponentInParent<FrozenEnemy>();
            if (frozen != null && frozen._held && !frozen._pulled)
            {
                Color tinted = Color.Lerp(color, Consts.Values.Freeze.TintColor, Consts.Values.Freeze.TintStrength);
                tinted.a = color.a;
                sprite.color = tinted;
                frozen._tints[sprite] = new Tint { Base = color, Shown = sprite.color };
            }
            else
            {
                sprite.color = color;
            }
        }

        // A freeze on an enemy that is already frozen only lasts until the later of the two thaws, so casts never stack
        public static bool Freeze(EnemyHealthSystem enemy, float duration, int playerId)
        {
            if (!Apply(enemy, duration, visualOnly: false)) return false;
            enemy.GetComponent<FrozenEnemy>()._frozenBy = playerId;
            SurgeNet.SendEnemyFx(EnemyFx.Frozen, enemy.GetComponent<NetworkObject>(), duration);
            return true;
        }

        // Online clients only tint the enemy; the host holds it and its position syncs
        public static void ShowFrozen(EnemyHealthSystem enemy, float duration)
        {
            Apply(enemy, duration, visualOnly: true);
        }

        // The Black Hole's pull stops the enemy's brain, fuse and charge for its pull time but leaves its bodies free for the pull; one already frozen stays locked and thaws into the pull
        public static void HoldForPull(EnemyHealthSystem enemy, float duration)
        {
            if (!CanFreeze(enemy) || duration <= 0f) return;
            FrozenEnemy frozen = enemy.GetComponent<FrozenEnemy>();
            if (frozen != null)
            {
                if (!frozen._pulled && !frozen._visualOnly) frozen._pullEnd = Mathf.Max(frozen._pullEnd, Time.time + duration);
                return;
            }

            FrozenEnemy held = enemy.gameObject.AddComponent<FrozenEnemy>();
            held._enemy = enemy;
            held._pulled = true;
            held._thawTime = Time.time + duration;
            held.Hold();
        }

        public static void EndPull(EnemyHealthSystem enemy)
        {
            FrozenEnemy held = enemy != null ? enemy.GetComponent<FrozenEnemy>() : null;
            if (held == null) return;
            held._pullEnd = 0f;
            if (held._pulled) held._thawTime = Time.time;
        }

        private static bool Apply(EnemyHealthSystem enemy, float duration, bool visualOnly)
        {
            if (!CanFreeze(enemy) || duration <= 0f) return false;

            FrozenEnemy frozen = enemy.GetComponent<FrozenEnemy>();
            if (frozen != null)
            {
                frozen._thawTime = Mathf.Max(frozen._thawTime, Time.time + duration);
                if (frozen._pulled && !visualOnly) frozen.Harden();
            }
            else
            {
                frozen = enemy.gameObject.AddComponent<FrozenEnemy>();
                frozen._enemy = enemy;
                frozen._visualOnly = visualOnly;
                frozen._thawTime = Time.time + duration;
                frozen.Hold();
            }
            if (!visualOnly) FrozenParticles.FreezeUnder(enemy.gameObject, frozen._thawTime);
            return true;
        }

        private void Hold()
        {
            _enemyId = _enemy.GetInstanceID();
            (_pulled ? PulledEnemyIds : FrozenEnemyIds).Add(_enemyId);
            _held = true;

            if (!_visualOnly) HoldBrains();
            if (!_pulled) Lock();
        }

        // A freeze on an enemy the Black Hole is pulling locks it where it is
        private void Harden()
        {
            PulledEnemyIds.Remove(_enemyId);
            FrozenEnemyIds.Add(_enemyId);
            _pulled = false;
            Lock();
        }

        private void HoldBrains()
        {
            foreach (EnemyBrain brain in Brains(_enemy))
            {
                // A charge cut short keeps its particles and sound running, so they stop the way the game stops them
                if (brain.IsInvoking("LaunchChargeAttack")) StopChargeCosmetics(brain);
                brain.CancelInvoke("LaunchChargeAttack");
                if (brain is WhispBrain whisp)
                {
                    CalmChargeLight(whisp);
                    SurgeNet.SendEnemyFx(EnemyFx.ChargeCalmed, whisp.NetworkObject, 0f);
                }
                _brains.Add(brain);
                FrozenBrainIds.Add(brain.GetInstanceID());
                if (brain is ExplodingRoller roller && RollerTriggered(roller))
                {
                    _fuseLeft = RollerTimeToExplode(roller) - Time.time;
                }
                // The shaman's OnDisable destroys its death cloud, so it stays enabled and its updates are skipped by a patch instead
                if (brain is HornetShamanBrain) continue;
                if (!brain.enabled) continue;
                brain.enabled = false;
                _disabledBrains.Add(brain);
            }
        }

        private void Lock()
        {
            SurgeVfx.Safely(AttachFrost);
            if (!_visualOnly)
            {
                foreach (Rigidbody2D body in _enemy.GetComponentsInChildren<Rigidbody2D>(true))
                {
                    _bodies.Add(new KeyValuePair<Rigidbody2D, RigidbodyConstraints2D>(body, body.constraints));
                    body.velocity = Vector2.zero;
                    body.angularVelocity = 0f;
                    body.constraints = RigidbodyConstraints2D.FreezeAll;
                }
            }
            TintAll();
        }

        private static void StopChargeCosmetics(EnemyBrain brain)
        {
            try
            {
                if (brain is WhispBrain) WhispStopCharge?.Invoke(brain, null);
                else if (brain is KhepriBrain) KhepriSwitchCharge?.Invoke(brain, new object[] { false });
            }
            catch (System.Exception ex)
            {
                Silk.Logger.LogWarning($"[SpiderSurge] Could not stop {brain.name}'s charge effects: {ex.GetBaseException().Message}");
            }
        }

        // Only the whisp's brain turns its aiming light back from the charge's red beam, on the host and through its RPC on clients, so a held whisp's light is set to the resting look the brain returns it to
        internal static void CalmChargeLight(WhispBrain whisp)
        {
            Light2D light = whisp != null ? whisp.chargeLight : null;
            if (light == null) return;
            light.intensity = 1f;
            light.pointLightInnerAngle = 50f;
            light.pointLightOuterAngle = 100f;
            light.pointLightOuterRadius = 100f;
            light.color = whisp.energyColor;
        }

        // The frost follows the enemy's moving body and spreads as wide as its sprites
        private void AttachFrost()
        {
            Transform body = _enemy.rb2D != null ? _enemy.rb2D.transform : _enemy.transform;
            float radius = 5f;
            foreach (Renderer renderer in _enemy.GetComponentsInChildren<Renderer>())
            {
                if (renderer is SpriteRenderer) radius = Mathf.Max(radius, renderer.bounds.extents.magnitude);
            }
            _frost = FrostAura.Attach(body, Mathf.Min(radius, 14f));
        }

        private void ReleaseFrost(bool thawed)
        {
            if (_frost == null) return;
            _frost.Release(thawed);
            _frost = null;
        }

        private void TintAll()
        {
            foreach (SpriteRenderer sprite in _enemy.GetComponentsInChildren<SpriteRenderer>(true))
            {
                SetBaseColor(sprite, sprite.color);
            }
        }

        private void Update()
        {
            if (Time.time < _thawTime) return;
            if (Time.time < _pullEnd && Soften()) return;
            Thaw();
            Destroy(this);
        }

        // A freeze that ends while the Black Hole still pulls the enemy frees its bodies for the pull and keeps its brain held until the pull ends
        private bool Soften()
        {
            if (!_held || _pulled || _visualOnly || _enemy == null || _enemy.IsDead() || SwallowedTarget.IsSwallowed(gameObject)) return false;

            FrozenEnemyIds.Remove(_enemyId);
            PulledEnemyIds.Add(_enemyId);
            _pulled = true;
            _frozenBy = -1;
            _thawTime = _pullEnd;
            _pullEnd = 0f;

            if (_frost != null)
            {
                _frost.Release(true);
                _frost = null;
            }
            PlayThawSound();
            foreach (var pair in _bodies)
            {
                if (pair.Key != null) pair.Key.constraints = pair.Value;
            }
            _bodies.Clear();
            RestoreTints();
            _tints.Clear();
            return true;
        }

        // The game recolors parts while they are frozen (a damaged roller strut turns orange); the new color becomes the one tinted and restored
        private void LateUpdate()
        {
            if (!_held) return;
            foreach (var pair in _tints)
            {
                if (pair.Key != null && pair.Key.color != pair.Value.Shown) _recolored.Add(pair.Key);
            }
            foreach (SpriteRenderer sprite in _recolored)
            {
                SetBaseColor(sprite, sprite.color);
            }
            _recolored.Clear();
        }

        private void Thaw()
        {
            if (!_held) return;
            _held = false;
            Release();

            bool alive = _enemy != null && !_enemy.IsDead();
            if (_visualOnly)
            {
                RestoreTints();
                ReleaseFrost(alive);
                if (alive) PlayThawSound();
                return;
            }

            // The Black Hole took it while frozen and holds it itself until it is gone
            if (SwallowedTarget.IsSwallowed(gameObject))
            {
                ReleaseFrost(false);
                return;
            }
            ReleaseFrost(alive);
            if (alive && !_pulled) PlayThawSound();

            foreach (var pair in _bodies)
            {
                if (pair.Key != null) pair.Key.constraints = pair.Value;
            }
            foreach (EnemyBrain brain in _disabledBrains)
            {
                if (brain != null) brain.enabled = true;
            }
            foreach (EnemyBrain brain in _brains)
            {
                // Its fuse is an absolute time; one lit before the freeze keeps what was left, one lit while frozen starts in full
                if (brain is ExplodingRoller roller && RollerTriggered(roller))
                {
                    RollerTimeToExplode(roller) = Time.time + (_fuseLeft ?? RollerFuse);
                }
            }
            RestoreTints();
        }

        public static void PlayThawSound()
        {
            if (Time.unscaledTime - _lastThawSound < ThawSoundGap) return;
            _lastThawSound = Time.unscaledTime;
            SoundManager.Play(Consts.SoundNames.Unfreeze, Consts.SoundVolumes.Unfreeze);
        }

        private void RestoreTints()
        {
            foreach (var pair in _tints)
            {
                if (pair.Key != null && pair.Key.color == pair.Value.Shown) pair.Key.color = pair.Value.Base;
            }
        }

        private void Release()
        {
            FrozenEnemyIds.Remove(_enemyId);
            PulledEnemyIds.Remove(_enemyId);
            foreach (EnemyBrain brain in _brains)
            {
                if (brain != null) FrozenBrainIds.Remove(brain.GetInstanceID());
            }
        }

        // A dead enemy is hidden for 2 s before it is destroyed, so its frost goes with it
        private void OnDisable()
        {
            if (_frost == null) return;
            _frost.Release(false);
            _frost = null;
        }

        private void OnDestroy()
        {
            ReleaseFrost(false);
            if (_held)
            {
                _held = false;
                Release();
            }
        }

        private static IEnumerable<EnemyBrain> Brains(EnemyHealthSystem enemy)
        {
            EnemyBrain[] brains = enemy.GetComponentsInChildren<EnemyBrain>(true);
            if (brains.Length == 0)
            {
                EnemyBrain parent = enemy.GetComponentInParent<EnemyBrain>();
                if (parent != null) brains = new[] { parent };
            }
            return brains;
        }
    }

    // A shaman's poison cloud held by Freeze Time, caught by the ring or frozen with its shaman: it stops where it is with its smoke and glow, and its poison still hurts
    public class FrozenCloud : MonoBehaviour
    {
        private static readonly HashSet<int> FrozenIds = new HashSet<int>();
        private static readonly AccessTools.FieldRef<DeathCloud, bool> Dying = AccessTools.FieldRefAccess<DeathCloud, bool>("_dying");

        private DeathCloud _cloud;
        private int _id;
        private float _thawTime;
        private float _smokeSpeed;
        private Color _glowBase;
        private Color _glowShown;
        private FrostAura _frost;

        public float TimeLeft => Mathf.Max(0f, _thawTime - Time.time);

        public static bool IsFrozen(DeathCloud cloud) => cloud != null && FrozenIds.Contains(cloud.GetInstanceID());

        // A cloud is frozen whole once its kill zone reaches into the ring, so no part that can hurt a player is left behind
        public static void FreezeAround(Vector2 origin, float range, float until)
        {
            foreach (DeathCloud cloud in Object.FindObjectsOfType<DeathCloud>())
            {
                if (Vector2.Distance(origin, cloud.transform.position) <= range + cloud.killRadius &&
                    FreezeAbility.HasLineOfSight(origin, cloud.transform.position)) Freeze(cloud, until);
            }
        }

        public static void Freeze(DeathCloud cloud, float until)
        {
            if (Apply(cloud, until)) SurgeNet.SendEnemyFx(EnemyFx.CloudFrozen, cloud.GetComponent<NetworkObject>(), until - Time.time);
        }

        // Online clients only stop its smoke; the host holds it and its position syncs
        public static void ShowFrozen(DeathCloud cloud, float duration)
        {
            Apply(cloud, Time.time + duration);
        }

        // True when the hold starts or gets longer
        private static bool Apply(DeathCloud cloud, float until)
        {
            if (cloud == null || Dying(cloud) || until <= Time.time) return false;
            FrozenCloud frozen = cloud.GetComponent<FrozenCloud>();
            if (frozen != null)
            {
                if (until <= frozen._thawTime) return false;
                frozen._thawTime = until;
                return true;
            }
            frozen = cloud.gameObject.AddComponent<FrozenCloud>();
            frozen._cloud = cloud;
            frozen._thawTime = until;
            frozen.Hold();
            return true;
        }

        private void Hold()
        {
            _id = _cloud.GetInstanceID();
            FrozenIds.Add(_id);
            if (_cloud.rb2d != null) _cloud.rb2d.velocity = Vector2.zero;
            if (_cloud.mainEffect != null)
            {
                ParticleSystem.MainModule main = _cloud.mainEffect.main;
                _smokeSpeed = main.simulationSpeed;
                main.simulationSpeed = 0f;
            }
            if (_cloud.glow != null)
            {
                _glowBase = _cloud.glow.color;
                _glowShown = Color.Lerp(_glowBase, Consts.Values.Freeze.TintColor, Consts.Values.Freeze.TintStrength);
                _glowShown.a = _glowBase.a;
                _cloud.glow.color = _glowShown;
            }
            _frost = FrostAura.Attach(_cloud.transform, _cloud.killRadius);
        }

        // A cloud whose shaman died fades out the way the game fades it, so it thaws first
        private void Update()
        {
            if (Time.time < _thawTime && !Dying(_cloud)) return;
            Thaw();
            Destroy(this);
        }

        private void Thaw()
        {
            FrozenIds.Remove(_id);
            if (_cloud.mainEffect != null)
            {
                ParticleSystem.MainModule main = _cloud.mainEffect.main;
                main.simulationSpeed = _smokeSpeed;
            }
            if (_cloud.glow != null && _cloud.glow.color == _glowShown) _cloud.glow.color = _glowBase;
            if (_frost != null) _frost.Release(!Dying(_cloud));
            _frost = null;
        }

        private void OnDestroy()
        {
            FrozenIds.Remove(_id);
            if (_frost != null) _frost.Release(false);
        }
    }

    // The cloud chases its target on its own, so it stays where it is while it is frozen; it freezes with its shaman, even one frozen before the cloud spawned
    [HarmonyPatch(typeof(DeathCloud), "MoveTowardsGoal")]
    public static class FrozenEnemy_DeathCloud_Patch
    {
        private static bool Prefix(DeathCloud __instance)
        {
            EnemyHealthSystem shaman = __instance.origin != null ? __instance.origin.GetComponentInParent<EnemyHealthSystem>() : null;
            if (FrozenEnemy.IsFrozen(shaman) && FreezeAbility.HasLineOfSight(shaman.transform.position, __instance.transform.position))
                FrozenCloud.Freeze(__instance, FrozenEnemy.ThawTimeOf(shaman));
            if (!FrozenCloud.IsFrozen(__instance)) return true;
            if (__instance.rb2d != null) __instance.rb2d.velocity = Vector2.zero;
            return false;
        }
    }

    // Hazard avoidance pushes the body and runs on the brain even while it is disabled
    [HarmonyPatch]
    public static class FrozenEnemy_HazardAvoidance_Patch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            foreach (var type in new[] { typeof(WhispBrain), typeof(WaspBrain), typeof(RollerBrain), typeof(KhepriBrain), typeof(HornetShamanBrain) })
            {
                MethodBase method = AccessTools.DeclaredMethod(type, "OnTriggerStay2D");
                if (method != null) yield return method;
            }
        }

        private static bool Prefix(EnemyBrain __instance) => !FrozenEnemy.IsFrozenBrain(__instance);
    }

    // A whisp's saw still hits while frozen, but one frozen before its Start ran has no body to hit from
    [HarmonyPatch(typeof(WhispBrain), "ProcessCollision")]
    public static class FrozenEnemy_WhispSaw_Patch
    {
        private static readonly AccessTools.FieldRef<WhispBrain, Rigidbody2D> Body = AccessTools.FieldRefAccess<WhispBrain, Rigidbody2D>("_rigidBody2D");

        private static bool Prefix(WhispBrain __instance) => !FrozenEnemy.IsFrozenBrain(__instance) || Body(__instance) != null;
    }

    [HarmonyPatch(typeof(HornetShamanBrain), nameof(HornetShamanBrain.FixedUpdate))]
    public static class FrozenEnemy_ShamanFixedUpdate_Patch
    {
        private static bool Prefix(HornetShamanBrain __instance) => !FrozenEnemy.IsFrozenBrain(__instance);
    }

    [HarmonyPatch(typeof(HornetShamanBrain), "Update")]
    public static class FrozenEnemy_ShamanUpdate_Patch
    {
        private static bool Prefix(HornetShamanBrain __instance) => !FrozenEnemy.IsFrozenBrain(__instance);
    }
}
