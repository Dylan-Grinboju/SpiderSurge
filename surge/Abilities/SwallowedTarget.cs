using System.Collections.Generic;
using HarmonyLib;
using Unity.Netcode;
using UnityEngine;

namespace SpiderSurge
{
    // A target the Black Hole swallows: frozen and harmless, pulled into the caster while it shrinks, then gone for good.
    // An enemy is removed without a death, a spider dies. On a client an enemy only shrinks, since the host moves it
    public class SwallowedTarget : MonoBehaviour
    {
        private static readonly AccessTools.FieldRef<SpiderHealthSystem, bool> SpiderDying =
            AccessTools.FieldRefAccess<SpiderHealthSystem, bool>("_dying");
        private static readonly AccessTools.FieldRef<SpiderHealthSystem, float> SpiderImmuneTill =
            AccessTools.FieldRefAccess<SpiderHealthSystem, float>("_immuneTill");

        public PlayerController Caster { get; private set; }

        private EnemyHealthSystem _enemy;
        private SpiderHealthSystem _spider;
        private readonly List<Rigidbody2D> _pausedBodies = new List<Rigidbody2D>();
        private Transform _target;
        private Vector2 _targetPosition;
        private Vector2 _startPosition;
        private Vector3 _startScale;
        private float _elapsed;
        private float _pullTime;
        private bool _visualOnly;

        public static void BeginEnemy(EnemyHealthSystem enemy, Transform target, PlayerController caster, float pullTime)
        {
            if (enemy == null || IsSwallowed(enemy.gameObject)) return;

            SwallowedTarget swallowed = enemy.gameObject.AddComponent<SwallowedTarget>();
            swallowed._enemy = enemy;
            swallowed.Caster = caster;
            swallowed._pullTime = pullTime;
            swallowed.FreezeEnemy();
            swallowed.StartPull(target);
            SurgeNet.SendEnemyFx(EnemyFx.Swallowed, enemy.GetComponent<NetworkObject>(), pullTime);
        }

        public static void ShowSwallowed(EnemyHealthSystem enemy, float pullTime)
        {
            if (enemy == null || IsSwallowed(enemy.gameObject)) return;

            SwallowedTarget swallowed = enemy.gameObject.AddComponent<SwallowedTarget>();
            swallowed._enemy = enemy;
            swallowed._pullTime = pullTime;
            swallowed._visualOnly = true;
            swallowed._startScale = swallowed.transform.localScale;
        }

        public static void BeginSpider(SpiderHealthSystem spider, Transform target, PlayerController caster)
        {
            if (spider == null || spider.rootObject == null || IsSwallowed(spider.rootObject)) return;

            SwallowedTarget swallowed = spider.rootObject.AddComponent<SwallowedTarget>();
            swallowed._spider = spider;
            swallowed.Caster = caster;
            swallowed._pullTime = Consts.Values.Storage.BlackHoleTakeTime;
            swallowed.FreezeSpider();
            swallowed.StartPull(target);
        }

        public static bool IsSwallowed(GameObject root)
        {
            return root != null && root.GetComponent<SwallowedTarget>() != null;
        }

        // Nothing protects it: alive, not respawn immune and not inside a Null Field
        public static bool CanDie(SpiderHealthSystem spider)
        {
            if (spider == null || spider.dead || spider.astralDead || SpiderDying(spider) || Time.time < SpiderImmuneTill(spider)) return false;

            ImmuneAbility nullField = ImmuneAbility.GetByHealthSystem(spider);
            return nullField == null || !nullField.IsImmune;
        }

        // The Black Hole ignores shields; Stats credits the caster with this kill
        public static void KillSpider(SpiderHealthSystem spider, PlayerController caster)
        {
            spider.DisableShield();
            spider.Disintegrate();
        }

        // Its brain, bodies and colliders stop, so it can't hit anyone, shoot or light a fuse on the way in
        private void FreezeEnemy()
        {
            foreach (EnemyBrain brain in GetComponentsInChildren<EnemyBrain>(true))
            {
                brain.CancelInvoke();
                brain.enabled = false;
            }

            foreach (Rigidbody2D body in GetComponentsInChildren<Rigidbody2D>(true))
            {
                body.velocity = Vector2.zero;
                body.angularVelocity = 0f;
                body.constraints = RigidbodyConstraints2D.FreezeAll;
            }

            foreach (Collider2D collider in GetComponentsInChildren<Collider2D>(true))
            {
                collider.enabled = false;
            }

            _enemy.CancelInvoke();
            _enemy.enabled = false;
        }

        // It drops its weapon where it stood, loses its web and leaves the physics, so nothing can hit it on the way in
        private void FreezeSpider()
        {
            SpiderWeaponManager weaponManager = GetComponentInChildren<SpiderWeaponManager>();
            if (weaponManager != null && weaponManager.equippedWeapon != null) weaponManager.UnEquipWeapon();
            if (_spider.webMaker != null) _spider.webMaker.DisconnectWeb();

            SpiderController controller = GetComponent<SpiderController>();
            if (controller != null) controller.enabled = false;

            foreach (Rigidbody2D body in GetComponentsInChildren<Rigidbody2D>())
            {
                if (!body.simulated) continue;
                body.velocity = Vector2.zero;
                body.angularVelocity = 0f;
                body.simulated = false;
                _pausedBodies.Add(body);
            }
        }

        private void StartPull(Transform target)
        {
            _target = target;
            _startPosition = BodyPosition();
            _startScale = transform.localScale;
            _targetPosition = target != null ? (Vector2)target.position : _startPosition;
        }

        private Vector2 BodyPosition()
        {
            if (_spider != null) return _spider.transform.position;
            return _enemy.rb2D != null ? (Vector2)_enemy.rb2D.transform.position : (Vector2)transform.position;
        }

        private void Update()
        {
            if (_enemy == null && _spider == null) return;
            if (_spider != null && _spider.dead)
            {
                Destroy(this);
                return;
            }
            if (_target != null) _targetPosition = _target.position;

            _elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(_elapsed / _pullTime);
            float eased = t * t;

            transform.localScale = Vector3.Lerp(_startScale, _startScale * Consts.Values.Storage.BlackHoleEndScale, eased);
            if (_visualOnly)
            {
                if (t >= 1f) enabled = false;
                return;
            }
            Vector2 offset = Vector2.Lerp(_startPosition, _targetPosition, eased) - BodyPosition();
            transform.position += (Vector3)offset;

            if (t < 1f) return;

            enabled = false;
            if (_enemy != null) Destroy(gameObject);
            else if (CanDie(_spider)) KillSpider(_spider, Caster);
            else if (_spider.astralDead && Time.time >= SpiderImmuneTill(_spider)) DissolveGhost();
            else Release();
        }

        // A spider that died astral on the way in already counted its death, so its ghost goes the way lava takes one
        private void DissolveGhost()
        {
            _spider.DisableShield();
            _spider.Disintegrate();
        }

        // A spider that became unkillable on the way in comes back where it is
        private void Release()
        {
            Vector2 position = BodyPosition();
            transform.localScale = _startScale;
            transform.position += (Vector3)(position - BodyPosition());
            foreach (Rigidbody2D body in _pausedBodies)
            {
                if (body != null) body.simulated = true;
            }

            SpiderController controller = GetComponent<SpiderController>();
            if (controller != null) controller.enabled = true;
            Destroy(this);
        }
    }

    // The pull drags a spider through platforms, so the game's stuck-in-wall check must not kill it on the way in
    [HarmonyPatch(typeof(SpiderHealthSystem), "StuckInWallCheck")]
    public class SpiderHealthSystem_StuckInWallCheck_BlackHolePatch
    {
        static bool Prefix(SpiderHealthSystem __instance)
        {
            return !SwallowedTarget.IsSwallowed(__instance.rootObject);
        }
    }
}
