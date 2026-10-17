using System.Collections.Generic;
using UnityEngine;

namespace SpiderSurge.Enemies
{
    public class SpawnerBrain : MonoBehaviour
    {
        [SerializeField] private GameObject spawnPrefab;
        [SerializeField] private GameObject shieldedSpawnPrefab;
        [SerializeField] private float interval;

        private EnemyHealthSystem health;
        private Collider2D[] colliders;
        private readonly HashSet<GameObject> ignored = new HashSet<GameObject>();
        private float timer;

        public void Initialize(GameObject prefab, GameObject shieldedPrefab, float spawnInterval)
        {
            spawnPrefab = prefab;
            shieldedSpawnPrefab = shieldedPrefab;
            interval = spawnInterval;
        }

        private void Start()
        {
            health = GetComponent<EnemyHealthSystem>();
            colliders = GetComponentsInChildren<Collider2D>(true);
        }

        private void FixedUpdate()
        {
            if (health == null || !health.IsHost || health.IsDead() || EnemySpawner.instance == null) return;

            IgnoreEnemiesInside();

            timer += Time.fixedDeltaTime;
            if (timer < interval) return;
            timer = 0f;

            GameObject prefab = health.shield != null && health.shield.activeInHierarchy && shieldedSpawnPrefab != null
                ? shieldedSpawnPrefab
                : spawnPrefab;
            if (prefab != null)
            {
                StartCoroutine(EnemySpawner.instance.SpawnObject(prefab, transform));
            }
        }

        private void OnCharacterDied()
        {
            StopAllCoroutines();
        }

        // Hatchlings and enemies from a shared spawn point appear inside the cocoon, which would otherwise push them out
        private void IgnoreEnemiesInside()
        {
            foreach (GameObject enemy in EnemySpawner.instance.spawnedEnemies)
            {
                if (enemy == null || enemy == gameObject || ignored.Contains(enemy)) continue;
                if (((Vector2)(enemy.transform.position - transform.position)).sqrMagnitude > 0.01f) continue;

                ignored.Add(enemy);
                foreach (Collider2D other in enemy.GetComponentsInChildren<Collider2D>(true))
                {
                    foreach (Collider2D own in colliders)
                    {
                        Physics2D.IgnoreCollision(own, other);
                    }
                }
            }
            ignored.RemoveWhere(e => e == null);
        }
    }
}
