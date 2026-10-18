using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using SpiderSurge.Common;
using Unity.Netcode;
using UnityEngine;

namespace SpiderSurge
{
    // An enemy held by Freeze Time: brains off, bodies locked, contact damage and fuses held, tinted ice blue until it thaws
    public class FrozenEnemy : MonoBehaviour
    {
        private static readonly HashSet<int> FrozenEnemyIds = new HashSet<int>();
        private static readonly HashSet<int> FrozenBrainIds = new HashSet<int>();
        private static readonly AccessTools.FieldRef<ExplodingRoller, bool> RollerTriggered =
            AccessTools.FieldRefAccess<ExplodingRoller, bool>("_triggered");
        private static readonly AccessTools.FieldRef<ExplodingRoller, float> RollerTimeToExplode =
            AccessTools.FieldRefAccess<ExplodingRoller, float>("_timeToExplode");
        private static readonly AccessTools.FieldRef<HornetShamanBrain, DeathCloud> ShamanCloud =
            AccessTools.FieldRefAccess<HornetShamanBrain, DeathCloud>("_deathCloud");
        private static readonly AccessTools.FieldRef<DeathCloud, Dictionary<GameObject, float>> CloudPlayersInRange =
            AccessTools.FieldRefAccess<DeathCloud, Dictionary<GameObject, float>>("inRange");
        private const float RollerFuse = 2f;

        private EnemyHealthSystem _enemy;
        private int _enemyId;
        private float _thawTime;
        private float? _fuseLeft;
        private bool _held;
        private bool _visualOnly;
        private readonly List<EnemyBrain> _brains = new List<EnemyBrain>();
        private readonly List<EnemyBrain> _disabledBrains = new List<EnemyBrain>();
        private readonly List<KeyValuePair<Rigidbody2D, RigidbodyConstraints2D>> _bodies = new List<KeyValuePair<Rigidbody2D, RigidbodyConstraints2D>>();
        private readonly Dictionary<SpriteRenderer, Color> _tints = new Dictionary<SpriteRenderer, Color>();

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
                frozen._tints[sprite] = color;
                Color tinted = Color.Lerp(color, Consts.Values.Freeze.TintColor, Consts.Values.Freeze.TintStrength);
                tinted.a = color.a;
                sprite.color = tinted;
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
                brain.CancelInvoke("LaunchChargeAttack");
                _brains.Add(brain);
                FrozenBrainIds.Add(brain.GetInstanceID());
                if (brain is ExplodingRoller roller && RollerTriggered(roller))
                {
                    _fuseLeft = RollerTimeToExplode(roller) - Time.time;
                }
                // The shaman's OnDisable destroys its death cloud, so it stays enabled and its updates are skipped by a patch instead
                if (brain is HornetShamanBrain shaman)
                {
                    DeathCloud cloud = ShamanCloud(shaman);
                    if (cloud != null) CloudPlayersInRange(cloud).Clear();
                    continue;
                }
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

        private void Thaw()
        {
            if (!_held) return;
            _held = false;
            Release();

            if (_visualOnly)
            {
                RestoreTints();
                return;
            }

            // Banish took it while frozen and holds it itself until it is gone
            if (BanishedTarget.IsBanished(gameObject)) return;

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

        private void RestoreTints()
        {
            foreach (var pair in _tints)
            {
                if (pair.Key != null) pair.Key.color = pair.Value;
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

    [HarmonyPatch(typeof(WaspStinger), nameof(WaspStinger.OnCollisionEnter2D))]
    public static class FrozenEnemy_WaspStinger_Patch
    {
        private static bool Prefix(WaspStinger __instance) => !FrozenEnemy.IsFrozen(__instance.enemyHealthSystem);
    }

    [HarmonyPatch(typeof(RollerStrut), nameof(RollerStrut.OnCollisionEnter2D))]
    public static class FrozenEnemy_RollerStrut_Patch
    {
        private static bool Prefix(RollerStrut __instance) => !FrozenEnemy.IsFrozen(__instance.enemyHealthSystem);
    }

    [HarmonyPatch(typeof(DeathCloud), "OnTriggerStay2D")]
    public static class FrozenEnemy_DeathCloud_Patch
    {
        private static bool Prefix(DeathCloud __instance) =>
            __instance.origin == null || !FrozenEnemy.IsFrozen(__instance.origin.GetComponentInParent<EnemyHealthSystem>());
    }

    // Contact damage from saws and bodies runs on the brain even while it is disabled
    [HarmonyPatch]
    public static class FrozenEnemy_BrainContact_Patch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            MethodBase processCollision = AccessTools.Method(typeof(WhispBrain), "ProcessCollision");
            if (processCollision != null) yield return processCollision;
            foreach (var type in new[] { typeof(WhispBrain), typeof(WaspBrain), typeof(RollerBrain), typeof(KhepriBrain), typeof(HornetShamanBrain) })
            {
                MethodBase method = AccessTools.DeclaredMethod(type, "OnTriggerStay2D");
                if (method != null) yield return method;
            }
        }

        private static bool Prefix(EnemyBrain __instance) => !FrozenEnemy.IsFrozenBrain(__instance);
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
