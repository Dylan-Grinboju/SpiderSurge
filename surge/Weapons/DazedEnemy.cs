using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using SpiderSurge.Common;
using SpiderSurge.Vfx;
using Unity.Netcode;
using UnityEngine;
using Values = SpiderSurge.Consts.Values.PoisonDart;

namespace SpiderSurge.Weapons
{
    // An enemy hit by the Poison Dart: its brain is skipped and it drifts in small random circles. It doesn't shoot, charge, cast or
    // explode, but its stinger, saw, struts and poison still hurt on contact, and a shaman's cloud waits where it is
    public class DazedEnemy : MonoBehaviour
    {
        private static readonly HashSet<int> DazedEnemyIds = new HashSet<int>();
        private static readonly HashSet<int> DazedBrainIds = new HashSet<int>();
        private static readonly AccessTools.FieldRef<ExplodingRoller, bool> RollerTriggered =
            AccessTools.FieldRefAccess<ExplodingRoller, bool>("_triggered");
        private static readonly AccessTools.FieldRef<ExplodingRoller, float> RollerTimeToExplode =
            AccessTools.FieldRefAccess<ExplodingRoller, float>("_timeToExplode");

        private EnemyHealthSystem _enemy;
        private int _enemyId;
        private float _until;
        private bool _visualOnly;
        private bool _held;
        private readonly List<EnemyBrain> _brains = new List<EnemyBrain>();
        private RollerBrain _roller;
        private float? _fuseLeft;
        private DazeVfx _vfx;
        private readonly Wander _wander = new Wander();

        public float TimeLeft => Mathf.Max(0f, _until - Time.time);

        public static bool IsDazed(EnemyHealthSystem enemy) => enemy != null && DazedEnemyIds.Contains(enemy.GetInstanceID());

        public static bool IsDazedBrain(EnemyBrain brain) => brain != null && DazedBrainIds.Contains(brain.GetInstanceID());

        // Another dart on a dazed enemy only lasts until the later of the two ends
        public static bool Daze(EnemyHealthSystem enemy, float duration)
        {
            if (!Apply(enemy, duration, visualOnly: false)) return false;
            SurgeNet.SendEnemyFx(EnemyFx.Dazed, enemy.GetComponent<NetworkObject>(), duration);
            return true;
        }

        // Online clients only show the motes; the host moves the enemy and its position syncs
        public static void ShowDazed(EnemyHealthSystem enemy, float duration) => Apply(enemy, duration, visualOnly: true);

        private static bool Apply(EnemyHealthSystem enemy, float duration, bool visualOnly)
        {
            if (enemy == null || enemy.IsDead() || duration <= 0f || SwallowedTarget.IsSwallowed(enemy.gameObject)) return false;
            DazedEnemy dazed = enemy.GetComponent<DazedEnemy>();
            if (dazed != null)
            {
                dazed._until = Mathf.Max(dazed._until, Time.time + duration);
                return true;
            }
            dazed = enemy.gameObject.AddComponent<DazedEnemy>();
            dazed._enemy = enemy;
            dazed._visualOnly = visualOnly;
            dazed._until = Time.time + duration;
            dazed.Hold();
            return true;
        }

        private void Hold()
        {
            _enemyId = _enemy.GetInstanceID();
            DazedEnemyIds.Add(_enemyId);
            _held = true;
            if (!_visualOnly) HoldBrains();
            Transform body = _enemy.rb2D != null ? _enemy.rb2D.transform : _enemy.transform;
            _vfx = DazeVfx.Attach(body, Radius());
        }

        private void HoldBrains()
        {
            foreach (EnemyBrain brain in FrozenEnemy.Brains(_enemy))
            {
                // A charge cut short keeps its particles and sound running, so they stop the way the game stops them
                if (brain.IsInvoking("LaunchChargeAttack")) FrozenEnemy.StopChargeCosmetics(brain);
                brain.CancelInvoke("LaunchChargeAttack");
                if (brain is WhispBrain whisp)
                {
                    FrozenEnemy.CalmChargeLight(whisp);
                    SurgeNet.SendEnemyFx(EnemyFx.ChargeCalmed, whisp.NetworkObject, 0f);
                }
                if (brain is RollerBrain roller) _roller = roller;
                if (brain is ExplodingRoller exploding && RollerTriggered(exploding)) _fuseLeft = RollerTimeToExplode(exploding) - Time.time;
                _brains.Add(brain);
                DazedBrainIds.Add(brain.GetInstanceID());
            }
        }

        private float Radius()
        {
            float radius = 5f;
            foreach (Renderer renderer in _enemy.GetComponentsInChildren<Renderer>())
            {
                if (renderer is SpriteRenderer) radius = Mathf.Max(radius, renderer.bounds.extents.y);
            }
            return Mathf.Min(radius, 14f);
        }

        private void Update()
        {
            if (_enemy == null || _enemy.IsDead() || Time.time >= _until)
            {
                Destroy(this);
                return;
            }
            // A lit fuse waits until the daze ends
            if (_fuseLeft.HasValue && _roller is ExplodingRoller exploding) RollerTimeToExplode(exploding) = Time.time + _fuseLeft.Value;
        }

        private void FixedUpdate()
        {
            if (_visualOnly || !_held || _enemy == null || FrozenEnemy.IsHeld(_enemy)) return;
            Rigidbody2D body = _enemy.rb2D;
            if (body == null || body.bodyType != RigidbodyType2D.Dynamic || !body.simulated) return;

            Vector2 direction = _wander.Step(Time.fixedDeltaTime);
            if (_roller != null)
            {
                body.AddForce(direction * (_roller.rollPower * Values.RollerWanderShare));
                return;
            }
            // Flyers hold their height and steer toward the circling heading
            Vector2 steer = (direction * Values.WanderSpeed - body.velocity) * Values.WanderSteer;
            body.AddForce((steer - Physics2D.gravity * body.gravityScale) * body.mass);
            body.angularVelocity *= 0.9f;
        }

        private void Release()
        {
            if (!_held) return;
            _held = false;
            DazedEnemyIds.Remove(_enemyId);
            foreach (EnemyBrain brain in _brains)
            {
                if (brain != null) DazedBrainIds.Remove(brain.GetInstanceID());
            }
            if (_vfx != null) _vfx.Release();
            _vfx = null;
        }

        private void OnDestroy() => Release();
    }

    // A heading that turns at a steady rate, re-rolling its rate and direction now and then, so the path is small uneven circles
    internal sealed class Wander
    {
        private float _angle = float.NaN;
        private float _turn;
        private float _reroll;

        public Vector2 Step(float deltaTime)
        {
            // Unity's Random can't run in a component's constructor, where this is made
            if (float.IsNaN(_angle)) _angle = Random.Range(0f, Mathf.PI * 2f);
            _reroll -= deltaTime;
            if (_reroll <= 0f)
            {
                _turn = Random.Range(Values.WanderTurnMin, Values.WanderTurnMax) * (Random.value < 0.5f ? -1f : 1f);
                _reroll = Random.Range(Values.WanderRerollMin, Values.WanderRerollMax);
            }
            _angle += _turn * deltaTime;
            return new Vector2(Mathf.Cos(_angle), Mathf.Sin(_angle));
        }
    }

    // The brains' movement and attacks run in these; a dazed enemy's are skipped and it wanders instead
    [HarmonyPatch]
    internal static class DazedEnemy_Brain_Patch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            foreach (var type in new[] { typeof(WaspBrain), typeof(HornetBrain), typeof(WhispBrain), typeof(KhepriBrain), typeof(HornetShamanBrain) })
            {
                yield return AccessTools.DeclaredMethod(type, nameof(EnemyBrain.FixedUpdate));
            }
            // A roller keeps its pull to the ground, strut checks and hazard avoidance; only its chase stops
            yield return AccessTools.DeclaredMethod(typeof(RollerBrain), "FollowPath");
            // A dazed exploding roller doesn't light its fuse on touch
            yield return AccessTools.DeclaredMethod(typeof(ExplodingRoller), "OnTriggerEnter2D");
        }

        private static bool Prefix(EnemyBrain __instance) => !DazedEnemy.IsDazedBrain(__instance);
    }
}
