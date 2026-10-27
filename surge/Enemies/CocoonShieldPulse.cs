using System.Collections.Generic;
using Interfaces;
using SpiderSurge.Common;
using SpiderSurge.Vfx;
using Unity.Netcode;
using UnityEngine;

namespace SpiderSurge.Enemies
{
    public sealed class CocoonShieldPulse : MonoBehaviour
    {
        private EnemyHealthSystem health;
        private NetworkObject networkObject;
        private Collider2D[] hits = new Collider2D[32];
        private readonly HashSet<IDamageable> affected = new HashSet<IDamageable>();
        private int layers;
        private float timer;
        private Shockwave wave;

        private bool ShieldActive => health != null && !health.IsDead() && health.shield != null && health.shield.activeInHierarchy;

        private void Awake()
        {
            health = GetComponent<EnemyHealthSystem>();
            networkObject = GetComponent<NetworkObject>();
            layers = LayerMask.GetMask("Player", "Item", "Weapon", "EnemyWeapon", "DynamicWorld");
        }

        private void OnEnable()
        {
            timer = 0f;
        }

        private void Update()
        {
            if (!ShieldActive) ClearWave();
        }

        private void FixedUpdate()
        {
            if (!Net.IsAuthority || networkObject == null || !networkObject.IsSpawned || !ShieldActive) return;
            if (FrozenEnemy.IsHeld(health) || SwallowedTarget.IsSwallowed(gameObject)) return;

            timer += Time.fixedDeltaTime;
            if (timer < Consts.Values.Enemies.CocoonPulseInterval) return;
            timer = 0f;

            ShowPulse();
            SurgeNet.SendEnemyFx(EnemyFx.CocoonPulse, networkObject, 0f);
            Repel();
        }

        public void ShowPulse()
        {
            if (!ShieldActive) return;
            ClearWave();
            wave = SurgeVfx.Safely(() => Shockwave.Expand(transform.position,
                Consts.Values.Enemies.CocoonPulseRadius, Consts.Values.Enemies.CocoonPulseColor,
                Consts.Values.Enemies.CocoonPulseSpeed, 1.5f));
        }

        // Apply one gentle impulse per target without stunning players, removing their shields or pushing the cocoon's allies.
        private void Repel()
        {
            Vector2 origin = transform.position;
            int count;
            while ((count = Physics2D.OverlapCircleNonAlloc(origin, Consts.Values.Enemies.CocoonPulseRadius, hits, layers)) == hits.Length)
            {
                System.Array.Resize(ref hits, hits.Length * 2);
            }
            affected.Clear();
            for (int i = 0; i < count; i++)
            {
                Collider2D hit = hits[i];
                if (hit == null || hit.GetComponentInParent<EnemyHealthSystem>() != null) continue;
                Weapon weapon = hit.GetComponentInParent<Weapon>();
                if (weapon != null && weapon.equipped) continue;
                IDamageable target = hit.GetComponentInParent<IDamageable>();
                if (target == null || !affected.Add(target)) continue;

                Vector2 point = hit.ClosestPoint(origin);
                Vector2 direction = point - origin;
                if (direction.sqrMagnitude < 0.0001f && hit.attachedRigidbody != null)
                    direction = hit.attachedRigidbody.position - origin;
                if (direction.sqrMagnitude < 0.0001f) direction = Vector2.up;
                target.Impact(direction.normalized * Consts.Values.Enemies.CocoonPulseForce, point, false, false);
            }
            affected.Clear();
        }

        private void ClearWave()
        {
            if (wave != null) wave.Close();
            wave = null;
        }

        private void OnDisable()
        {
            ClearWave();
        }
    }
}
