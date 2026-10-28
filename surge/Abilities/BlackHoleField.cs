using System.Collections.Generic;
using SpiderSurge.Vfx;
using UnityEngine;

namespace SpiderSurge
{
    // The Black Hole's pull, centred on the caster and moving with them for the pull time. It holds and pulls the enemies that were inside it when it opened,
    // and pulls every spider inside it the way the Gravity Grenade does; whatever reaches the inner circle before it closes goes into the pocket dimension.
    // The host moves every spider and enemy, so only its field pulls; other machines only draw theirs
    public class BlackHoleField : MonoBehaviour
    {
        private StorageAbility _ability;
        private SpiderHealthSystem _caster;
        private PlayerController _casterController;
        private Transform _body;
        private float _range;
        private float _elapsed;
        private bool _authority;
        private BlackHoleVfx _vfx;
        private Vector3 _lastPosition;
        private float _enemyPull;
        private readonly List<CaughtEnemy> _caught = new List<CaughtEnemy>();
        private readonly BlackHoleMeal _achievementMeal = new BlackHoleMeal();
        private static readonly RaycastHit2D[] WallHits = new RaycastHit2D[8];

        private class CaughtEnemy
        {
            public EnemyHealthSystem Enemy;
            public Rigidbody2D[] Bodies;
            public Collider2D[] Colliders;
            public Vector2 Drift;
        }

        public static BlackHoleField Begin(StorageAbility ability, SpiderHealthSystem caster, PlayerController casterController, float range, bool authority)
        {
            if (ability == null || caster == null) return null;

            BlackHoleField field = new GameObject("BlackHoleField").AddComponent<BlackHoleField>();
            field._ability = ability;
            field._caster = caster;
            field._casterController = casterController;
            field._body = caster.transform;
            field._range = range;
            field._authority = authority;
            field._lastPosition = caster.transform.position;
            field.transform.position = field._lastPosition;
            field._enemyPull = EnemyPull(range);
            field._vfx = StorageVfx.BlackHoleOpen(field._body, range, Consts.Values.Storage.BlackHolePullTime);
            if (authority) field.CatchEnemies();
            return field;
        }

        private bool CasterAlive => _caster != null && !_caster.dead && !_caster.astralDead;

        // Scale the acceleration from the reference distance so the pull brings enemies in faster at every range
        private static float EnemyPull(float range)
        {
            float time = Consts.Values.Storage.BlackHolePullTime;
            float distance = range * Consts.Values.Storage.BlackHoleReachFraction - Consts.Values.Storage.BlackHoleCoreRadius;
            return Mathf.Max(0f, 2f * distance / (time * time)) * Consts.Values.Storage.BlackHolePullStrengthMultiplier;
        }

        private static Vector2 EnemyPosition(EnemyHealthSystem enemy)
        {
            return enemy.rb2D != null ? enemy.rb2D.position : (Vector2)enemy.transform.position;
        }

        private void CatchEnemies()
        {
            if (EnemyHealthSystem.s_activeEnemies == null) return;

            Vector2 origin = _body.position;
            foreach (EnemyHealthSystem enemy in EnemyHealthSystem.s_activeEnemies)
            {
                if (enemy == null || enemy.IsDead() || !enemy.needToKillToCompleteLevel || SwallowedTarget.IsSwallowed(enemy.gameObject)) continue;
                if (Vector2.Distance(origin, EnemyPosition(enemy)) > _range) continue;

                // Each one starts still, so the pull time decides which ones make it in
                Rigidbody2D[] bodies = enemy.GetComponentsInChildren<Rigidbody2D>();
                Stop(bodies);
                _caught.Add(new CaughtEnemy { Enemy = enemy, Bodies = bodies, Colliders = enemy.GetComponentsInChildren<Collider2D>() });
                FrozenEnemy.HoldForPull(enemy, Consts.Values.Storage.BlackHolePullTime);
            }
        }

        private static void Stop(Rigidbody2D[] bodies)
        {
            foreach (Rigidbody2D body in bodies)
            {
                if (body == null) continue;
                body.velocity = Vector2.zero;
                body.angularVelocity = 0f;
            }
        }

        // How close the nearest solid part of an enemy is, so one stopped against the caster's ground still counts, and a stinger is taken before it lands
        private static float Reach(CaughtEnemy caught, Vector2 center, float distance)
        {
            foreach (Collider2D collider in caught.Colliders)
            {
                if (collider == null || !collider.isActiveAndEnabled || collider.isTrigger) continue;
                distance = Mathf.Min(distance, Vector2.Distance(collider.ClosestPoint(center), center));
            }
            return distance;
        }

        private void Update()
        {
            if (CasterAlive) _lastPosition = _body.position;
            transform.position = _lastPosition;

            _elapsed += Time.deltaTime;
            if (_elapsed >= Consts.Values.Storage.BlackHolePullTime || !CasterAlive) Close();
        }

        private void FixedUpdate()
        {
            if (!_authority || !CasterAlive) return;

            Vector2 center = _body.position;
            PullEnemies(center);
            PullSpiders(center);
        }

        // Every body speeds up alike, so light and heavy enemies come in together
        private void PullEnemies(Vector2 center)
        {
            for (int i = _caught.Count - 1; i >= 0; i--)
            {
                CaughtEnemy caught = _caught[i];
                EnemyHealthSystem enemy = caught.Enemy;
                if (enemy == null || enemy.IsDead() || SwallowedTarget.IsSwallowed(enemy.gameObject))
                {
                    _caught.RemoveAt(i);
                    continue;
                }

                Vector2 position = EnemyPosition(enemy);
                Vector2 offset = center - position;
                float distance = offset.magnitude;
                if (Reach(caught, center, distance) <= Consts.Values.Storage.BlackHoleCoreRadius)
                {
                    _caught.RemoveAt(i);
                    SwallowedTarget.BeginEnemy(enemy, _body, _casterController, Consts.Values.Storage.BlackHoleTakeTime);
                    if (SwallowedTarget.IsSwallowed(enemy.gameObject))
                    {
                        if (_achievementMeal.Eat(enemy.name))
                            AchievementTracker.Run("Light Snack", () => SurgeAchievements.Unlock(SurgeAchievements.LightSnack, _casterController));
                    }
                }
                else if (distance <= _range)
                {
                    Vector2 pull = offset / distance * _enemyPull;
                    caught.Drift += pull * Time.fixedDeltaTime;
                    foreach (Rigidbody2D body in caught.Bodies)
                    {
                        if (body == null) continue;
                        if (body.bodyType == RigidbodyType2D.Dynamic) body.AddForce(pull * body.mass);
                        else if (body.bodyType == RigidbodyType2D.Kinematic) Slide(body, ref caught.Drift);
                    }
                }
            }
        }

        // A kinematic body, like the Wasp Cocoon's, ignores forces, so the pull moves it at the same speed itself, and a wall or another kinematic body stops it
        private static void Slide(Rigidbody2D body, ref Vector2 drift)
        {
            Vector2 step = drift * Time.fixedDeltaTime;
            float length = step.magnitude;
            if (length < 0.0001f) return;

            Vector2 direction = step / length;
            ContactFilter2D filter = new ContactFilter2D { useTriggers = false };
            filter.SetLayerMask(Physics2D.GetLayerCollisionMask(body.gameObject.layer));
            int hits = body.Cast(direction, filter, WallHits, length);
            for (int i = 0; i < hits; i++)
            {
                Rigidbody2D other = WallHits[i].rigidbody;
                if (other != null && other.bodyType == RigidbodyType2D.Dynamic) continue;
                length = Mathf.Min(length, Mathf.Max(0f, WallHits[i].distance - 0.05f));
                drift = Vector2.zero;
            }
            body.MovePosition(body.position + direction * length);
        }

        // The grenade's pull, web cut included, fading to nothing at the edge, so a spider far out can walk away and one close in can't
        private void PullSpiders(Vector2 center)
        {
            List<SpiderHealthSystem> spiders = SpiderHealthSystem.s_activeSpiders;
            if (spiders == null) return;

            int suspension = ModifierManager.instance != null ? ModifierManager.instance.GetModLevel(Consts.ModifierNames.KnockbackSuspension) : 0;
            for (int i = 0; i < spiders.Count; i++)
            {
                SpiderHealthSystem spider = spiders[i];
                if (spider == null || spider == _caster) continue;

                Rigidbody2D body = spider.GetComponent<Rigidbody2D>();
                if (body == null) continue;
                Vector2 offset = center - body.position;
                float distance = offset.magnitude;
                if (distance > _range) continue;

                if (distance <= Consts.Values.Storage.BlackHoleCoreRadius && _ability.CanSwallow(spider))
                {
                    SwallowedTarget.BeginSpider(spider, _body, _casterController);
                    continue;
                }
                if (distance < 0.01f || !_ability.CanPull(spider)) continue;

                if (spider.webMaker != null && spider.webMaker.WebConnected()) spider.webMaker.DisconnectWeb();
                Stabilizer stabilizer = spider.rootObject != null ? spider.rootObject.GetComponent<Stabilizer>() : null;
                float grip = stabilizer != null && stabilizer.grounded && !spider.stunned ? 3.5f : 1f;
                float falloff = 1f - distance / _range;
                float impulse = Consts.Values.Storage.BlackHolePullForce * Consts.Values.Storage.BlackHolePullStrengthMultiplier * falloff * Time.fixedDeltaTime * grip * 13.3f / (suspension + 1);
                body.AddForce(offset / distance * impulse, ForceMode2D.Impulse);
            }
        }

        // What didn't make it in stops where the field leaves it, instead of flying on into the caster
        private void Close()
        {
            foreach (CaughtEnemy caught in _caught)
            {
                Stop(caught.Bodies);
                FrozenEnemy.EndPull(caught.Enemy);
            }
            StorageVfx.BlackHoleClose(_vfx, _lastPosition, _range, CasterAlive ? _body : null);
            _vfx = null;
            Destroy(gameObject);
        }
    }
}
