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
        private Vector3 telegraphPosition;

        private static readonly List<EnemyHealthSystem> allHatchlings = new List<EnemyHealthSystem>();
        private static readonly MethodInfo spawnEffectRpc = AccessTools.Method(typeof(EnemySpawner), "InstantiateEnemySpawnEffectClientRpc");
        private static readonly MethodInfo spawnCosmeticRpc = AccessTools.Method(typeof(EnemySpawner), "SpawnObjectCosmeticClientRpc");
        private static readonly AccessTools.FieldRef<SurvivalMode, bool> WaveInProgress = AccessTools.FieldRefAccess<SurvivalMode, bool>("_waveInProgress");

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
            if (health == null || NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer || health.IsDead() || EnemySpawner.instance == null) return;

            IgnoreEnemiesInside();

            if (SpawningStopped)
            {
                if (hatching) CancelHatch();
                return;
            }

            hatchlings.RemoveAll(h => h == null || h.IsDead());
            if (hatching || hatchlings.Count >= maxHatchlings || Held) return;
            timer += Time.fixedDeltaTime;
            if (timer < interval) return;
            timer = 0f;

            if (spawnPrefab != null || shieldedSpawnPrefab != null)
            {
                StartCoroutine(Hatch());
            }
        }

        // EnemySpawner.SpawnObject without keeping the spawned enemy, so this follows it to count its own wasps
        private IEnumerator Hatch()
        {
            hatching = true;
            EnemySpawner spawner = EnemySpawner.instance;
            // A hold that outlasts the telegraph starts it again, so a wasp never appears without one
            do
            {
                float delay = 2.5f + ModifierManager.instance.GetModLevel("interference") * 1.5f;
                telegraphPosition = transform.position;
                spawnEffectRpc.Invoke(spawner, new object[] { telegraphPosition, delay, false });
                yield return new WaitForSeconds(delay);
                if (!Held) break;
                // A banished cocoon is removed at the end of its pull, so it waits like a frozen one and never hatches
                while (Held && !health.IsDead()) yield return null;
            }
            while (!health.IsDead());

            // The cocoon's shield when the wasp appears decides its kind, so breaking it during the telegraph counts
            GameObject prefab = health.shield != null && health.shield.activeInHierarchy && shieldedSpawnPrefab != null
                ? shieldedSpawnPrefab
                : spawnPrefab;
            if (health.IsDead() || prefab == null)
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

        // Once a survival wave is lost or over, the game's own pending spawns are stopped too
        private static bool SpawningStopped => SurvivalMode.instance != null && SurvivalMode.instance.GameModeActive() && !WaveInProgress(SurvivalMode.instance);

        private void CancelHatch()
        {
            StopAllCoroutines();
            hatching = false;
            GameObject effect = EnemySpawner.instance.spawnEffect;
            if (effect == null) return;
            string effectName = effect.name + "(Clone)";
            foreach (ParticleSystem particles in FindObjectsOfType<ParticleSystem>())
            {
                if (particles.name == effectName && ((Vector2)(particles.transform.position - telegraphPosition)).sqrMagnitude < 0.01f)
                {
                    Destroy(particles.gameObject);
                }
            }
        }

        private bool Held => FrozenEnemy.IsFrozen(health) || BanishedTarget.IsBanished(gameObject);

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
