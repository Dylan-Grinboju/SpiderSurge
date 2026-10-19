using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using SpiderSurge.Common;
using Unity.Netcode;
using UnityEngine;

namespace SpiderSurge
{
    // An enemy held by Freeze Time: brains off, bodies and the shaman's cloud locked, fuses held, tinted ice blue until it thaws.
    // It doesn't move, shoot or explode, but its blades, saws, struts and poison cloud still hurt on contact
    public class FrozenEnemy : MonoBehaviour
    {
        private static readonly HashSet<int> FrozenEnemyIds = new HashSet<int>();
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
        private float _thawTime;
        private float? _fuseLeft;
        private bool _held;
        private bool _visualOnly;
        private readonly List<EnemyBrain> _brains = new List<EnemyBrain>();
        private readonly List<EnemyBrain> _disabledBrains = new List<EnemyBrain>();
        private readonly List<KeyValuePair<Rigidbody2D, RigidbodyConstraints2D>> _bodies = new List<KeyValuePair<Rigidbody2D, RigidbodyConstraints2D>>();
        private readonly Dictionary<SpriteRenderer, Tint> _tints = new Dictionary<SpriteRenderer, Tint>();
        private readonly List<SpriteRenderer> _recolored = new List<SpriteRenderer>();

        private struct Tint
        {
            public Color Base;
            public Color Shown;
        }

        public float TimeLeft => Mathf.Max(0f, _thawTime - Time.time);

        public static bool IsFrozen(EnemyHealthSystem enemy) => enemy != null && FrozenEnemyIds.Contains(enemy.GetInstanceID());

        public static bool IsFrozenBrain(EnemyBrain brain) => brain != null && FrozenBrainIds.Contains(brain.GetInstanceID());

        public static bool CanFreeze(EnemyHealthSystem enemy)
        {
            return enemy != null && !enemy.IsDead() && enemy.needToKillToCompleteLevel && !BanishedTarget.IsBanished(enemy.gameObject);
        }

        public static float TimeLeftOn(EnemyHealthSystem enemy)
        {
            FrozenEnemy frozen = enemy != null ? enemy.GetComponent<FrozenEnemy>() : null;
            return frozen != null ? frozen.TimeLeft : 0f;
        }

        internal static void SetBaseColor(SpriteRenderer sprite, Color color)
        {
            FrozenEnemy frozen = sprite.GetComponentInParent<FrozenEnemy>();
            if (frozen != null && frozen._held)
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
        public static bool Freeze(EnemyHealthSystem enemy, float duration)
        {
            if (!Apply(enemy, duration, visualOnly: false)) return false;
            SurgeNet.SendEnemyFx(EnemyFx.Frozen, enemy.GetComponent<NetworkObject>(), duration);
            return true;
        }

        // Online clients only tint the enemy; the host holds it and its position syncs
        public static void ShowFrozen(EnemyHealthSystem enemy, float duration)
        {
            Apply(enemy, duration, visualOnly: true);
        }

        private static bool Apply(EnemyHealthSystem enemy, float duration, bool visualOnly)
        {
            if (!CanFreeze(enemy) || duration <= 0f) return false;

            FrozenEnemy frozen = enemy.GetComponent<FrozenEnemy>();
            if (frozen != null)
            {
                frozen._thawTime = Mathf.Max(frozen._thawTime, Time.time + duration);
                return true;
            }

            frozen = enemy.gameObject.AddComponent<FrozenEnemy>();
            frozen._enemy = enemy;
            frozen._visualOnly = visualOnly;
            frozen._thawTime = Time.time + duration;
            frozen.Hold();
            return true;
        }

        private void Hold()
        {
            _enemyId = _enemy.GetInstanceID();
            FrozenEnemyIds.Add(_enemyId);
            _held = true;

            if (_visualOnly)
            {
                TintAll();
                return;
            }

            foreach (EnemyBrain brain in Brains(_enemy))
            {
                // A charge cut short keeps its particles and sound running, so they stop the way the game stops them
                if (brain.IsInvoking("LaunchChargeAttack")) StopChargeCosmetics(brain);
                brain.CancelInvoke("LaunchChargeAttack");
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

            foreach (Rigidbody2D body in _enemy.GetComponentsInChildren<Rigidbody2D>(true))
            {
                _bodies.Add(new KeyValuePair<Rigidbody2D, RigidbodyConstraints2D>(body, body.constraints));
                body.velocity = Vector2.zero;
                body.angularVelocity = 0f;
                body.constraints = RigidbodyConstraints2D.FreezeAll;
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
            Thaw();
            Destroy(this);
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
                if (alive) PlayThawSound();
                return;
            }

            // Banish took it while frozen and holds it itself until it is gone
            if (BanishedTarget.IsBanished(gameObject)) return;
            if (alive) PlayThawSound();

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
            SoundManager.Instance?.PlaySound(Consts.SoundNames.Unfreeze, Consts.SoundVolumes.Unfreeze * Consts.SoundVolumes.MasterVolume);
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
            foreach (EnemyBrain brain in _brains)
            {
                if (brain != null) FrozenBrainIds.Remove(brain.GetInstanceID());
            }
        }

        private void OnDestroy()
        {
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

    // The cloud chases its target on its own, so it stays where it is while its shaman is frozen; its poison still hurts
    [HarmonyPatch(typeof(DeathCloud), "MoveTowardsGoal")]
    public static class FrozenEnemy_DeathCloud_Patch
    {
        private static bool Prefix(DeathCloud __instance)
        {
            if (__instance.origin == null || !FrozenEnemy.IsFrozen(__instance.origin.GetComponentInParent<EnemyHealthSystem>())) return true;
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
