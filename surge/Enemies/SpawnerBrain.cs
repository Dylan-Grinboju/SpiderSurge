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
        private float timer;

        private static readonly AccessTools.FieldRef<SurvivalMode, SurvivalConfig> survivalConfig = AccessTools.FieldRefAccess<SurvivalMode, SurvivalConfig>("_config");
        private static readonly HashSet<SpawnerBrain> hatching = new HashSet<SpawnerBrain>();
        private static readonly MethodInfo spawnEffectRpc = AccessTools.Method(typeof(EnemySpawner), "InstantiateEnemySpawnEffectClientRpc");
        private static readonly MethodInfo spawnCosmeticRpc = AccessTools.Method(typeof(EnemySpawner), "SpawnObjectCosmeticClientRpc");

        public void Initialize(GameObject prefab, GameObject shieldedPrefab, float spawnInterval, int maxAlive)
        {
            spawnPrefab = prefab;
            shieldedSpawnPrefab = shieldedPrefab;
            interval = spawnInterval;
            maxHatchlings = maxAlive;
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
            hatchlings.RemoveAll(h => h == null || h.IsDead());
            if (timer < interval || hatching.Contains(this) || hatchlings.Count >= maxHatchlings || !RoomForEnemy()) return;
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
            hatching.Add(this);
            EnemySpawner spawner = EnemySpawner.instance;
            float delay = 2.5f + ModifierManager.instance.GetModLevel("interference") * 1.5f;
            spawnEffectRpc.Invoke(spawner, new object[] { transform.position, delay, false });
            yield return new WaitForSeconds(delay);

            GameObject hatchling = Instantiate(prefab, transform.position, transform.rotation);
            spawner.spawnedEnemies.Add(hatchling);
            hatchlings.Add(hatchling.GetComponent<EnemyHealthSystem>());
            hatchling.GetComponent<NetworkObject>().Spawn(destroyWithScene: true);
            spawnCosmeticRpc.Invoke(spawner, null);
            hatching.Remove(this);
        }

        // The same live enemy cap that holds back the wave's own spawns, counting wasps still hatching
        private static bool RoomForEnemy()
        {
            SurvivalMode survival = SurvivalMode.instance;
            SurvivalConfig config = survival != null ? survivalConfig(survival) : null;
            if (config == null || config.difficultyIncreaseThreshold <= 0) return true;

            int alive = hatching.Count;
            foreach (EnemyHealthSystem enemy in EnemyHealthSystem.s_activeEnemies)
            {
                if (enemy.needToKillToCompleteLevel) alive++;
            }
            return alive < survival.CurrentWave / config.difficultyIncreaseThreshold + 2;
        }

        private void OnCharacterDied()
        {
            StopAllCoroutines();
            hatching.Remove(this);
        }

        private void OnDestroy()
        {
            hatching.Remove(this);
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
