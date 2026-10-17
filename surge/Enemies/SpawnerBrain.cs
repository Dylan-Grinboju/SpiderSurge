using HarmonyLib;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Unity.Netcode;
using UnityEngine;

namespace SpiderSurge.Enemies
{
    public class SpawnerBrain : MonoBehaviour
    {
        [SerializeField] private GameObject spawnPrefab;
        [SerializeField] private GameObject shieldedSpawnPrefab;
        [SerializeField] private float interval;
        [SerializeField] private int maxHatchlings;

        private EnemyHealthSystem health;
        private Collider2D[] colliders;
        private readonly HashSet<GameObject> ignored = new HashSet<GameObject>();
        private readonly List<EnemyHealthSystem> hatchlings = new List<EnemyHealthSystem>();
        private bool hatching;
        private float timer;

        private static readonly List<EnemyHealthSystem> allHatchlings = new List<EnemyHealthSystem>();
        private static readonly MethodInfo spawnEffectRpc = AccessTools.Method(typeof(EnemySpawner), "InstantiateEnemySpawnEffectClientRpc");
        private static readonly MethodInfo spawnCosmeticRpc = AccessTools.Method(typeof(EnemySpawner), "SpawnObjectCosmeticClientRpc");

        public void Initialize(GameObject prefab, GameObject shieldedPrefab, float spawnInterval, int maxAlive)
        {
            spawnPrefab = prefab;
            shieldedSpawnPrefab = shieldedPrefab;
            interval = spawnInterval;
            maxHatchlings = maxAlive;
        }

        // Live cocoon wasps, which the wave's live enemy cap leaves out
        public static int CountHatchlingsAlive()
        {
            allHatchlings.RemoveAll(h => h == null);
            int count = 0;
            foreach (EnemyHealthSystem hatchling in allHatchlings)
            {
                if (hatchling.isActiveAndEnabled && hatchling.needToKillToCompleteLevel) count++;
            }
            return count;
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

            hatchlings.RemoveAll(h => h == null || h.IsDead());
            if (hatching || hatchlings.Count >= maxHatchlings) return;
            timer += Time.fixedDeltaTime;
            if (timer < interval) return;
            timer = 0f;

            GameObject prefab = health.shield != null && health.shield.activeInHierarchy && shieldedSpawnPrefab != null
                ? shieldedSpawnPrefab
                : spawnPrefab;
            if (prefab != null)
            {
                StartCoroutine(Hatch(prefab));
            }
        }

        // EnemySpawner.SpawnObject without keeping the spawned enemy, so this follows it to count its own wasps
        private IEnumerator Hatch(GameObject prefab)
        {
            hatching = true;
            EnemySpawner spawner = EnemySpawner.instance;
            float delay = 2.5f + ModifierManager.instance.GetModLevel("interference") * 1.5f;
            spawnEffectRpc.Invoke(spawner, new object[] { transform.position, delay, false });
            yield return new WaitForSeconds(delay);
            if (health.IsDead())
            {
                hatching = false;
                yield break;
            }

            GameObject hatchling = Instantiate(prefab, transform.position, transform.rotation);
            EnemyHealthSystem hatchlingHealth = hatchling.GetComponent<EnemyHealthSystem>();
            spawner.spawnedEnemies.Add(hatchling);
            hatchlings.Add(hatchlingHealth);
            allHatchlings.Add(hatchlingHealth);
            hatchling.GetComponent<NetworkObject>().Spawn(destroyWithScene: true);
            spawnCosmeticRpc.Invoke(spawner, null);
            hatching = false;
        }

        private void OnCharacterDied()
        {
            StopAllCoroutines();
            hatching = false;
        }

        // Hatchlings and enemies from a shared spawn point appear inside the cocoon, which would otherwise push them out
        private void IgnoreEnemiesInside()
        {
            foreach (GameObject enemy in EnemySpawner.instance.spawnedEnemies)
            {
                if (enemy == null || enemy == gameObject || ignored.Contains(enemy)) continue;
                if (((Vector2)(enemy.transform.position - transform.position)).sqrMagnitude > 0.01f) continue;

                ignored.Add(enemy);
                EnemyHealthSystem enemyHealth = enemy.GetComponent<EnemyHealthSystem>();
                if (enemyHealth != null && enemyHealth.serializationEnemyName == SerializationEnemyName.FriendlyWasp) continue;
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
