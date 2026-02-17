using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using System.Reflection;
using System.Linq;
using Unity.Netcode;
using Logger = Silk.Logger;

namespace SpiderSurge
{
    public class ShieldAbility : BaseAbility
    {
        private static Dictionary<SpiderHealthSystem, ShieldAbility> shieldsByHealth = new Dictionary<SpiderHealthSystem, ShieldAbility>();

        public override string PerkName => Consts.PerkNames.ShieldAbility;

        public override float AbilityBaseDuration => Consts.Values.Shield.AbilityBaseDuration;
        public override float AbilityBaseCooldown => Consts.Values.Shield.AbilityBaseCooldown;
        public override float UltimateBaseDuration => Consts.Values.Shield.UltimateBaseDuration;
        public override float UltimateBaseCooldown => Consts.Values.Shield.UltimateBaseCooldown;
        public override float AbilityDurationPerPerkLevel => Consts.Values.Shield.AbilityDurationIncreasePerLevel;
        public override float AbilityCooldownPerPerkLevel => Consts.Values.Shield.AbilityCooldownReductionPerLevel;
        public override float UltimateDurationPerPerkLevel => Consts.Values.Shield.UltimateDurationIncreasePerLevel;
        public override float UltimateCooldownPerPerkLevel => Consts.Values.Shield.UltimateCooldownReductionPerLevel;

        public override float UltimateDuration
        {
            get
            {
                int durationLevel = PerksManager.Instance?.GetPerkLevel(Consts.PerkNames.AbilityDuration) ?? 0;
                int shortTermLevel = PerksManager.Instance?.GetPerkLevel(Consts.PerkNames.ShortTermInvestment) ?? 0;
                int longTermLevel = PerksManager.Instance?.GetPerkLevel(Consts.PerkNames.LongTermInvestment) ?? 0;

                float duration = UltimateBaseDuration;
                if (durationLevel == 2) duration -= UltimateDurationPerPerkLevel;
                if (shortTermLevel > 0) duration += UltimateDurationPerPerkLevel;
                if (longTermLevel > 0) duration -= UltimateDurationPerPerkLevel;

                return Mathf.Max(0.1f, duration);
            }
        }

        public override bool HasUltimate => true;
        public override string UltimatePerkDisplayName => "Resurgence";
        public override string UltimatePerkDescription => "After a cast time, revive one dead player or summon 3 friendly wasps if nobody is dead.";

        private static FieldInfo immuneTimeField;
        private static MethodInfo _breakShieldMethod;

        private bool hadShieldOnSessionStart = false;
        private bool wasHitDuringSession = false;
        private bool isUltimateCastPending = false;
        private bool hadShieldOnUltimateCastStart = false;

        public bool IsImmune { get; private set; }

        protected override void Awake()
        {
            base.Awake();

            if (immuneTimeField == null)
            {
                immuneTimeField = typeof(SpiderHealthSystem).GetField("_immuneTill",
                    BindingFlags.NonPublic | BindingFlags.Instance);

                if (immuneTimeField == null)
                {
                    Logger.LogError("ShieldAbility: Checked for _immuneTill field but it was null! Immunity will not work.");
                }
            }

            if (_breakShieldMethod == null)
            {
                _breakShieldMethod = typeof(SpiderHealthSystem).GetMethod("BreakShieldClientRpc",
                    BindingFlags.NonPublic | BindingFlags.Instance);
            }
        }

        protected override void Start()
        {
            base.Start();
            StartCoroutine(RegisterWithHealthSystem());
        }

        private IEnumerator RegisterWithHealthSystem()
        {
            while (spiderHealthSystem == null)
            {
                yield return null;
            }
            shieldsByHealth[spiderHealthSystem] = this;
        }

        public static ShieldAbility GetByHealthSystem(SpiderHealthSystem healthSystem)
        {
            if (healthSystem == null) return null;
            shieldsByHealth.TryGetValue(healthSystem, out var ability);
            return ability;
        }

        public void RegisterHit()
        {
            if (isActive && IsImmune)
            {
                wasHitDuringSession = true;
            }
        }

        protected override void OnActivate()
        {
            wasHitDuringSession = false;
            hadShieldOnSessionStart = spiderHealthSystem != null && spiderHealthSystem.HasShield();

            ApplyImmunity(true);

            if (SoundManager.Instance != null)
            {
                SoundManager.Instance.PlaySound(
                    Consts.SoundNames.ShieldAbility,
                    Consts.SoundVolumes.ShieldAbility * Consts.SoundVolumes.MasterVolume
                );
            }
        }

        protected override void OnDeactivate()
        {
            if (spiderHealthSystem != null)
            {
                bool shouldKeepShield = hadShieldOnSessionStart && !wasHitDuringSession;
                if (hadShieldOnSessionStart && !shouldKeepShield)
                {
                    DestroyShield();
                    spiderHealthSystem.DisableShield();
                }
            }

            if (IsImmune)
            {
                ApplyImmunity(false);
            }

            wasHitDuringSession = false;
            hadShieldOnSessionStart = false;
        }

        protected override void OnActivateUltimate()
        {
            isUltimateCastPending = true;
            hadShieldOnUltimateCastStart = spiderHealthSystem != null && spiderHealthSystem.HasShield();

            if (SoundManager.Instance != null)
            {
                SoundManager.Instance.PlaySound(
                    Consts.SoundNames.ShieldUlt,
                    Consts.SoundVolumes.ShieldUlt * Consts.SoundVolumes.MasterVolume
                );
            }
        }

        protected override void OnDeactivateUltimate()
        {
            if (!isUltimateCastPending)
            {
                return;
            }

            isUltimateCastPending = false;
            ExecuteUltimateEffect();
            hadShieldOnUltimateCastStart = false;
        }


        private Dictionary<SpriteRenderer, Color> _originalColors = new Dictionary<SpriteRenderer, Color>();
        private Color? _originalLightColor;
        private float? _originalLightIntensity;

        private void ApplyRadiance(bool enable)
        {
            if (spiderHealthSystem == null) return;

            if (enable)
            {
                // -- 1. Apply to Sprites --
                if (spiderHealthSystem.spritesRoot != null)
                {
                    var renderers = spiderHealthSystem.spritesRoot.GetComponentsInChildren<SpriteRenderer>(true);
                    foreach (var sr in renderers)
                    {
                        if (sr == null) continue;
                        if (!_originalColors.ContainsKey(sr))
                        {
                            _originalColors[sr] = sr.color;
                        }
                        sr.color = new Color(1f, 0.84f, 0.0f, 1f); // Gold
                    }
                }

                // Also the head
                if (spiderHealthSystem.head != null)
                {
                    if (!_originalColors.ContainsKey(spiderHealthSystem.head))
                    {
                        _originalColors[spiderHealthSystem.head] = spiderHealthSystem.head.color;
                    }
                    spiderHealthSystem.head.color = new Color(1f, 0.84f, 0.0f, 1f);
                }

                // -- 2. Apply to Light (Via Reflection) --
                ApplyLightRadiance(true);
            }
            else
            {
                // -- Restore Sprites --
                foreach (var kvp in _originalColors)
                {
                    if (kvp.Key != null)
                    {
                        kvp.Key.color = kvp.Value;
                    }
                }
                _originalColors.Clear();

                // -- Restore Light --
                ApplyLightRadiance(false);
            }
        }

        private void ApplyLightRadiance(bool enable)
        {
            try
            {
                var lightField = typeof(SpiderHealthSystem).GetField("spiderLight");
                if (lightField == null) return;

                var lightObj = lightField.GetValue(spiderHealthSystem);
                if (lightObj == null) return;

                var lightType = lightObj.GetType();
                var colorProp = lightType.GetProperty("color");
                var intensityProp = lightType.GetProperty("intensity");

                if (enable)
                {
                    if (colorProp != null && _originalLightColor == null)
                        _originalLightColor = (Color)colorProp.GetValue(lightObj, null);

                    if (intensityProp != null && _originalLightIntensity == null)
                        _originalLightIntensity = (float)intensityProp.GetValue(lightObj, null);

                    if (colorProp != null) colorProp.SetValue(lightObj, new Color(1f, 0.6f, 0.0f), null);
                    if (intensityProp != null) intensityProp.SetValue(lightObj, 4.0f, null);
                }
                else
                {
                    if (colorProp != null && _originalLightColor != null)
                    {
                        colorProp.SetValue(lightObj, _originalLightColor.Value, null);
                        _originalLightColor = null;
                    }

                    if (intensityProp != null && _originalLightIntensity != null)
                    {
                        intensityProp.SetValue(lightObj, _originalLightIntensity.Value, null);
                        _originalLightIntensity = null;
                    }
                }
            }
            catch (System.Exception ex)
            {
                Logger.LogWarning($"[SpiderSurge] Failed to apply light radiance: {ex.Message}");
            }
        }

        private void ApplyImmunity(bool enable)
        {
            if (spiderHealthSystem == null) return;
            IsImmune = enable;

            ApplyRadiance(enable);

            if (enable)
            {
                try
                {
                    if (immuneTimeField != null)
                    {
                        immuneTimeField.SetValue(spiderHealthSystem, float.MaxValue);
                    }
                }
                catch (System.Exception ex)
                {
                    Logger.LogError($"Failed to set immune time: {ex.Message}");
                }
            }
            else
            {
                try
                {
                    if (immuneTimeField != null)
                    {
                        // Reset immunity to current time (expired)
                        immuneTimeField.SetValue(spiderHealthSystem, Time.time);
                    }
                }
                catch (System.Exception ex)
                {
                    Logger.LogError($"Failed to reset immune time: {ex.Message}");
                }
            }
        }

        private void ExecuteUltimateEffect()
        {
            if (!IsServerAuthority())
            {
                return;
            }

            if (TryRespawnDeadPlayer(out var revivedPlayer))
            {
                if (hadShieldOnUltimateCastStart && Random.value <= 0.5f)
                {
                    StartCoroutine(ApplyShieldToRevivedPlayer(revivedPlayer));
                }
                return;
            }

            SpawnFriendlyWasps();
        }

        private bool TryRespawnDeadPlayer(out PlayerController revivedPlayer)
        {
            revivedPlayer = null;

            if (LobbyController.instance == null)
            {
                return false;
            }

            var playerControllers = LobbyController.instance.GetPlayerControllers();
            if (playerControllers == null || !playerControllers.Any())
            {
                return false;
            }

            var spawnPoints = LobbyController.instance.GetSpawnPoints();
            if (spawnPoints == null || spawnPoints.Length == 0)
            {
                return false;
            }

            var deadPlayers = playerControllers.Where(pc => pc != null && !pc.isAlive).ToList();
            if (deadPlayers.Count == 0)
            {
                return false;
            }

            revivedPlayer = deadPlayers[Random.Range(0, deadPlayers.Count)];
            var spawnPoint = spawnPoints[Random.Range(0, spawnPoints.Length)];
            revivedPlayer.SpawnCharacter(spawnPoint.position, spawnPoint.rotation);
            return true;
        }

        private IEnumerator ApplyShieldToRevivedPlayer(PlayerController revivedPlayer)
        {
            float elapsed = 0f;
            const float timeout = 2f;

            while (elapsed < timeout)
            {
                if (revivedPlayer == null)
                {
                    yield break;
                }

                var revivedHealth = revivedPlayer.spiderHealthSystem;
                if (revivedHealth != null)
                {
                    revivedHealth.EnableShield();
                    yield break;
                }

                elapsed += Time.deltaTime;
                yield return null;
            }
        }

        private void SpawnFriendlyWasps()
        {
            var friendlyWaspPrefab = SurvivalMode.instance != null ? SurvivalMode.instance.friendlyWasp : null;
            if (friendlyWaspPrefab == null)
            {
                return;
            }

            var spawnPoints = LobbyController.instance != null ? LobbyController.instance.GetSpawnPoints() : null;
            if (spawnPoints == null || spawnPoints.Length == 0)
            {
                spawnPoints = GameObject.FindGameObjectsWithTag("EnemySpawn").Select(go => go.transform).ToArray();
            }

            if (spawnPoints == null || spawnPoints.Length == 0)
            {
                return;
            }

            var selectedSpawns = GetDistinctSpawnPoints(spawnPoints, 3);
            foreach (var spawn in selectedSpawns)
            {
                if (spawn == null)
                {
                    continue;
                }

                var spawnedObj = Instantiate(friendlyWaspPrefab, spawn.position, spawn.rotation);
                spawnedObj.SetActive(true);

                var netObj = spawnedObj.GetComponent<NetworkObject>();
                if (netObj != null)
                {
                    netObj.Spawn(true);
                    netObj.DestroyWithScene = true;
                }

                if (EnemySpawner.instance != null && !EnemySpawner.instance.spawnedEnemies.Contains(spawnedObj))
                {
                    EnemySpawner.instance.spawnedEnemies.Add(spawnedObj);
                }

                if (Random.value <= 0.5f)
                {
                    TryGiveEnemyShield(spawnedObj);
                }
            }
        }

        private List<Transform> GetDistinctSpawnPoints(Transform[] spawnPoints, int count)
        {
            var selectedSpawns = new List<Transform>();
            var availableIndices = Enumerable.Range(0, spawnPoints.Length).ToList();

            int spawnCount = Mathf.Min(count, spawnPoints.Length);
            for (int i = 0; i < spawnCount; i++)
            {
                int randomListIndex = Random.Range(0, availableIndices.Count);
                int spawnIndex = availableIndices[randomListIndex];
                availableIndices.RemoveAt(randomListIndex);
                selectedSpawns.Add(spawnPoints[spawnIndex]);
            }

            return selectedSpawns;
        }

        private void TryGiveEnemyShield(GameObject enemyObject)
        {
            if (enemyObject == null)
            {
                return;
            }

            var enemyHealth = enemyObject.GetComponent<EnemyHealthSystem>();
            if (enemyHealth == null || enemyHealth.shield == null)
            {
                return;
            }

            enemyHealth.shield.SetActive(true);

            try
            {
                var enableShieldMethod = typeof(EnemyHealthSystem).GetMethod("EnableShield", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                enableShieldMethod?.Invoke(enemyHealth, null);
            }
            catch
            {
            }
        }

        private bool IsServerAuthority()
        {
            if (NetworkManager.Singleton == null)
            {
                return true;
            }

            return NetworkManager.Singleton.IsServer || NetworkManager.Singleton.IsHost;
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();

            if (spiderHealthSystem != null && shieldsByHealth.ContainsKey(spiderHealthSystem))
            {
                shieldsByHealth.Remove(spiderHealthSystem);
            }

            isUltimateCastPending = false;
            hadShieldOnUltimateCastStart = false;
        }

        private void DestroyShield()
        {
            try
            {
                if (_breakShieldMethod != null)
                {
                    _breakShieldMethod.Invoke(spiderHealthSystem, null);
                }
                else
                {
                    Logger.LogWarning($"Could not find BreakShieldClientRpc method for player {playerInput.playerIndex}");
                }
            }
            catch (System.Exception e)
            {
                Logger.LogError($"Failed to trigger shield explosion for player {playerInput.playerIndex}: {e.Message}");
            }
        }
    }
}