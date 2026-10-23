using SpiderSurge.Common;
using UnityEngine;

namespace SpiderSurge
{
    public sealed class CocoonPulseKnockback : MonoBehaviour
    {
        private RaycastHit2D[] _hits = new RaycastHit2D[16];
        private EnemyHealthSystem _enemy;
        private Rigidbody2D _body;
        private Vector2 _velocity;
        private float _deceleration;

        public static void Begin(EnemyHealthSystem enemy, Rigidbody2D body, Vector2 velocity)
        {
            var push = enemy.GetComponent<CocoonPulseKnockback>() ?? enemy.gameObject.AddComponent<CocoonPulseKnockback>();
            push._enemy = enemy;
            push._body = body;
            push._velocity = velocity;
            push._deceleration = velocity.magnitude / PulseKnockback.UncappedDuration;
            push.enabled = true;
        }

        // Sweep each step so the cocoon stops at solid terrain, then slow it to rest without changing its body type
        private void FixedUpdate()
        {
            if (!Net.IsAuthority || _enemy == null || _enemy.dead || _body == null || !_body.simulated ||
                _body.bodyType != RigidbodyType2D.Kinematic || FrozenEnemy.IsHeld(_enemy) || SwallowedTarget.IsSwallowed(gameObject))
            {
                enabled = false;
                return;
            }

            float length = _velocity.magnitude * Time.fixedDeltaTime;
            if (length < 0.0001f)
            {
                enabled = false;
                return;
            }

            Vector2 direction = _velocity.normalized;
            var filter = new ContactFilter2D { useTriggers = false };
            filter.SetLayerMask(Physics2D.GetLayerCollisionMask(_body.gameObject.layer));
            int count;
            while ((count = _body.Cast(direction, filter, _hits, length)) == _hits.Length)
            {
                System.Array.Resize(ref _hits, _hits.Length * 2);
            }
            bool blocked = false;
            for (int i = 0; i < count; i++)
            {
                Rigidbody2D other = _hits[i].rigidbody;
                if (other != null && other.bodyType == RigidbodyType2D.Dynamic) continue;
                length = Mathf.Min(length, Mathf.Max(0f, _hits[i].distance - 0.05f));
                blocked = true;
            }

            _body.MovePosition(_body.position + direction * length);
            _velocity = blocked ? Vector2.zero : Vector2.MoveTowards(_velocity, Vector2.zero, _deceleration * Time.fixedDeltaTime);
        }

        private void OnDisable()
        {
            _velocity = Vector2.zero;
            if (_body != null && _body.bodyType == RigidbodyType2D.Kinematic) _body.velocity = Vector2.zero;
        }
    }
}
