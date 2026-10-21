using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using System.Reflection;
using System.Linq;
using HarmonyLib;
using SpiderSurge.Common;
using Unity.Netcode;
using SpiderSurge.Vfx;
using Logger = Silk.Logger;

namespace SpiderSurge
{
    public class ImmuneAbility : BaseAbility
    {
        private static Dictionary<SpiderHealthSystem, ImmuneAbility> immuneByHealthSystem = new Dictionary<SpiderHealthSystem, ImmuneAbility>();

        public override string PerkName => Consts.PerkNames.ImmuneAbility;
        public override AbilityKind Kind => AbilityKind.Immune;

        public override float AbilityBaseDuration => Consts.Values.Immune.AbilityBaseDuration;
        public override float AbilityBaseCooldown => Consts.Values.Immune.AbilityBaseCooldown;
        public override float UltimateBaseDuration => Consts.Values.Immune.UltimateBaseDuration;
        public override float UltimateBaseCooldown => Consts.Values.Immune.UltimateBaseCooldown;
        public override float AbilityDurationPerPerkLevel => Consts.Values.Immune.AbilityDurationIncreasePerLevel;
        public override float AbilityCooldownPerPerkLevel => Consts.Values.Immune.AbilityCooldownReductionPerLevel;
        public override float UltimateDurationPerPerkLevel => Consts.Values.Immune.UltimateDurationIncreasePerLevel;
        public override float UltimateCooldownPerPerkLevel => Consts.Values.Immune.UltimateCooldownReductionPerLevel;

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
        public override string UltimatePerkDisplayName => "Neural Backup";
        public override string UltimatePerkDescription => "After a cast time, revive one dead player or summon 3 friendly wasps if nobody is dead.";

        private static readonly AccessTools.FieldRef<SpiderHealthSystem, bool> SpiderDying = AccessTools.FieldRefAccess<SpiderHealthSystem, bool>("_dying");
        private static FieldInfo immuneTimeField;
        private static MethodInfo _breakShieldMethod;
        private static FieldInfo _spiderLightField;

        private bool hadBarrierOnSessionStart = false;
        private bool wasHitDuringSession = false;
        private bool isUltimateCastPending = false;
        private bool hadBarrierOnUltimateCastStart = false;
        private Logging.WaveRecord hitWave;

        public bool IsImmune { get; private set; }

        public static bool AnyImmune
        {
            get
            {
                foreach (var ability in immuneByHealthSystem.Values)
                {
                    if (ability != null && ability.IsImmune) return true;
                }
                return false;
            }
        }

        protected override void Awake()
        {
            base.Awake();

            if (immuneTimeField == null)
            {
                immuneTimeField = typeof(SpiderHealthSystem).GetField("_immuneTill",
                    BindingFlags.NonPublic | BindingFlags.Instance);

                if (immuneTimeField == null)
                {
                    Logger.LogError("ImmuneAbility: Checked for _immuneTill field but it was null! Immunity will not work.");
                }
            }

            if (_breakShieldMethod == null)
            {
                _breakShieldMethod = typeof(SpiderHealthSystem).GetMethod("BreakShieldClientRpc",
                    BindingFlags.NonPublic | BindingFlags.Instance);
            }

            if (_spiderLightField == null)
            {
                _spiderLightField = typeof(SpiderHealthSystem).GetField("spiderLight",
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
            immuneByHealthSystem[spiderHealthSystem] = this;
        }

        public static ImmuneAbility GetByHealthSystem(SpiderHealthSystem healthSystem)
        {
            if (healthSystem == null) return null;
            immuneByHealthSystem.TryGetValue(healthSystem, out var ability);
            return ability;
        }

        private float _lastChargedHit = -10f;
        private int _chargedHits;

        // Lava calls this every frame, so a blocked hit charges the meter at most twice a second and only a few times per cast
        public void RegisterHit()
        {
            if (isActive && IsImmune)
            {
                if (!wasHitDuringSession) hitWave = Logging.SpiderSurgeStatsManager.Instance.CurrentWaveRecord;
                wasHitDuringSession = true;
                if (bubble != null) bubble.Hit();
                if (Time.time - _lastChargedHit >= Consts.Values.Meter.NullFieldHitInterval && _chargedHits < Consts.Values.Meter.NullFieldChargedHitsPerCast)
                {
                    _lastChargedHit = Time.time;
                    _chargedHits++;
                    SurgeMeter.Add(MeterSource.NullField, PlayerId, Consts.Values.Meter.NullFieldChargePerHit);
                }
            }
        }

        private NullFieldBubble bubble;
        private ChannelEffect channel;

        private void ShowBubble(bool enable)
        {
            if (enable)
            {
                if (bubble == null && spiderHealthSystem != null) bubble = NullFieldBubble.Attach(spiderHealthSystem.transform);
            }
            else if (bubble != null)
            {
                bubble.End();
                bubble = null;
            }
        }

        private void EndChannel(bool completed)
        {
            if (channel == null) return;
            channel.Finish(completed);
            channel = null;
        }

        protected override void OnActivate()
        {
            wasHitDuringSession = false;
            _lastChargedHit = -10f;
            _chargedHits = 0;
            hadBarrierOnSessionStart = spiderHealthSystem != null && spiderHealthSystem.HasShield();

            ApplyImmunity(true);

            if (SoundManager.Instance != null && !IsRemote)
            {
                SoundManager.Instance.PlaySound(
                    Consts.SoundNames.ImmuneAbility,
                    Consts.SoundVolumes.ImmuneAbility * Consts.SoundVolumes.MasterVolume
                );
            }
        }

        protected override void OnDeactivate()
        {
            if (spiderHealthSystem != null && IsAuthority)
            {
                if (hadBarrierOnSessionStart && wasHitDuringSession)
                {
                    DestroyBarrier();
                    spiderHealthSystem.DisableShield();
                }
                else if (wasHitDuringSession && IsPlayerAlive() && isActiveAndEnabled)
                {
                    StartCoroutine(LogSaveIfSurvived(hitWave));
                }
            }

            if (IsImmune)
            {
                ApplyImmunity(false);
            }

            wasHitDuringSession = false;
            hadBarrierOnSessionStart = false;
            hitWave = null;
        }

        // Lava or a blast still hitting when Null Field ends kills a moment later, so the save counts once the spider outlives it
        private IEnumerator LogSaveIfSurvived(Logging.WaveRecord wave)
        {
            yield return new WaitForSeconds(Consts.Values.Immune.SaveConfirmSeconds);
            if (IsPlayerAlive())
            {
                Logging.SpiderSurgeStatsManager.Instance.LogAbilitySave(PlayerId, wave);
            }
        }

        protected override void OnActivateUltimate()
        {
            isUltimateCastPending = true;
            hadBarrierOnUltimateCastStart = spiderHealthSystem != null && spiderHealthSystem.HasShield();
            if (spiderHealthSystem != null) channel = ChannelEffect.Attach(spiderHealthSystem.transform);

            if (SoundManager.Instance != null && !IsRemote)
            {
                SoundManager.Instance.PlaySound(
                    Consts.SoundNames.ImmuneUlt,
                    Consts.SoundVolumes.ImmuneUlt * Consts.SoundVolumes.MasterVolume
                );
            }
        }

        protected override void OnDeactivateUltimate()
        {
            if (!isUltimateCastPending)
            {
                EndChannel(false);
                return;
            }

            isUltimateCastPending = false;
            EndChannel(true);
            ExecuteUltimateEffect();
            hadBarrierOnUltimateCastStart = false;
        }

        protected override bool ShouldPlayAbilityEndedSound(bool wasUltimate)
        {
            return !wasUltimate;
        }


        private Dictionary<SpriteRenderer, Color> _originalColors = new Dictionary<SpriteRenderer, Color>();
        private Color? _originalLightColor;
        private float? _originalLightIntensity;

        private void ApplyRadiance(bool enable)
        {
            if (spiderHealthSystem == null) return;
            ShowBubble(enable);

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
                if (_spiderLightField == null) return;

                var lightObj = _spiderLightField.GetValue(spiderHealthSystem);
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

        // Clients other than the caster only hear about the glow; the host tells them when it starts and ends
        public static void ShowRemoteImmunity(int playerId, bool on)
        {
            foreach (var ability in immuneByHealthSystem.Values)
            {
                if (ability != null && ability.PlayerId == playerId)
                {
                    ability.ApplyRadiance(on);
                    return;
                }
            }
        }

        private void ApplyImmunity(bool enable)
        {
            if (spiderHealthSystem == null) return;
            IsImmune = enable;

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

            SurgeVfx.Safely(() => ApplyRadiance(enable));
            if (Net.IsHost)
            {
                SurgeNet.SendFx(SurgeFx.Immune, PlayerId, Vector3.zero, 0f, enable);
            }
        }

        private void ExecuteUltimateEffect()
        {
            if (!IsServerAuthority())
            {
                Logger.LogDebug("[ImmuneAbility] Ultimate effect skipped: not server authority.");
                return;
            }

            if (TryRespawnDeadPlayer(out var revivedPlayer))
            {
                SurgeMeter.Add(MeterSource.NeuralBackup, PlayerId, Consts.Values.Meter.NeuralBackupChargePerRevive);
                if (hadBarrierOnUltimateCastStart && Random.value <= Consts.Values.Immune.UltimateShieldSynergyChance)
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

            // A caster who dies late in the cast still finishes it but can't bring themselves back; a caster still floating as an astral ghost can
            var deadPlayers = playerControllers.Where(pc => pc != null && (IsGhost(pc) || (!pc.isAlive && pc != playerController))).ToList();
            if (deadPlayers.Count == 0)
            {
                return false;
            }

            revivedPlayer = deadPlayers[Random.Range(0, deadPlayers.Count)];
            // An Astral Heckhound ghost comes back where it floats, the way the game revives ghosts at the end of a wave
            if (IsGhost(revivedPlayer))
            {
                revivedPlayer.spiderHealthSystem.DisableDeathEffect();
                NullFieldVfx.Revive(revivedPlayer.spiderHealthSystem.transform.position);
                return true;
            }
            var spawnPoint = spawnPoints[Random.Range(0, spawnPoints.Length)];
            revivedPlayer.SpawnCharacter(spawnPoint.position, spawnPoint.rotation);
            NullFieldVfx.Revive(spawnPoint.position);
            return true;
        }

        // A ghost already dissolving in lava or below the map is gone for good, so it isn't one to revive
        public static bool IsGhost(PlayerController player)
        {
            SpiderHealthSystem health = player != null ? player.spiderHealthSystem : null;
            return health != null && health.astralDead && !health.dead && !SpiderDying(health) && !SwallowedTarget.IsSwallowed(health.rootObject);
        }

        private IEnumerator ApplyShieldToRevivedPlayer(PlayerController revivedPlayer)
        {
            float elapsed = 0f;
            const float timeout = 2f;

            while (elapsed < timeout)
            {
                if (revivedPlayer == null)
                {
                    Logger.LogWarning("[ImmuneAbility] Revived player reference became null before shield application.");
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

            Logger.LogWarning("[ImmuneAbility] Timed out waiting to apply shield to revived player.");
        }

        private void SpawnFriendlyWasps()
        {
            var friendlyWaspPrefab = SurvivalMode.instance != null ? SurvivalMode.instance.friendlyWasp : null;
            if (friendlyWaspPrefab == null)
            {
                Logger.LogWarning("[ImmuneAbility] Friendly wasp prefab is missing; cannot spawn friendly wasps.");
                return;
            }

            var spawnPoints = LobbyController.instance != null ? LobbyController.instance.GetSpawnPoints() : null;
            if (spawnPoints == null || spawnPoints.Length == 0)
            {
                spawnPoints = GameObject.FindGameObjectsWithTag("EnemySpawn").Select(go => go.transform).ToArray();
            }

            if (spawnPoints == null || spawnPoints.Length == 0)
            {
                Logger.LogWarning("[ImmuneAbility] No spawn points found for friendly wasp spawn.");
                return;
            }

            int baseSpawnCount = Consts.Values.Immune.UltimateFriendlyWaspSpawnCount;
            int shieldBonusCount = hadBarrierOnUltimateCastStart ? Consts.Values.Immune.UltimateFriendlyWaspShieldBonusCount : 0;
            var selectedSpawns = GetDistinctSpawnPoints(spawnPoints, baseSpawnCount);

            foreach (var spawn in selectedSpawns)
            {
                if (!TrySpawnFriendlyWaspAtTransform(friendlyWaspPrefab, spawn))
                {
                    continue;
                }
            }

            for (int i = 0; i < shieldBonusCount; i++)
            {
                if (Random.value > Consts.Values.Immune.UltimateShieldSynergyChance)
                {
                    continue;
                }

                var bonusSpawn = spawnPoints[Random.Range(0, spawnPoints.Length)];
                TrySpawnFriendlyWaspAtTransform(friendlyWaspPrefab, bonusSpawn);
            }

        }

        private bool TrySpawnFriendlyWaspAtTransform(GameObject friendlyWaspPrefab, Transform spawn)
        {
            if (friendlyWaspPrefab == null)
            {
                return false;
            }

            if (spawn == null)
            {
                Logger.LogWarning("[ImmuneAbility] Selected friendly wasp spawn point was null.");
                return false;
            }

            var spawnedObj = Instantiate(friendlyWaspPrefab, spawn.position, spawn.rotation);
            spawnedObj.AddComponent<FriendlyWaspOwner>().PlayerId = PlayerId;
            spawnedObj.SetActive(true);
            NullFieldVfx.Summon(spawn.position);

            var netObj = spawnedObj.GetComponent<NetworkObject>();
            if (netObj != null)
            {
                if (NetworkManager.Singleton != null && (NetworkManager.Singleton.IsServer || NetworkManager.Singleton.IsHost))
                {
                    netObj.Spawn(true);
                }
                netObj.DestroyWithScene = true;
            }
            else
            {
                Logger.LogWarning("[ImmuneAbility] Friendly wasp has no NetworkObject component.");
            }

            if (EnemySpawner.instance != null && !EnemySpawner.instance.spawnedEnemies.Contains(spawnedObj))
            {
                EnemySpawner.instance.spawnedEnemies.Add(spawnedObj);
            }

            return true;
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

            if ((object)spiderHealthSystem != null && immuneByHealthSystem.ContainsKey(spiderHealthSystem))
            {
                ApplyImmunity(false);
                immuneByHealthSystem.Remove(spiderHealthSystem);
            }

            isUltimateCastPending = false;
            hadBarrierOnUltimateCastStart = false;
            EndChannel(false);
            ShowBubble(false);
        }

        private void DestroyBarrier()
        {
            try
            {
                if (_breakShieldMethod != null)
                {
                    _breakShieldMethod.Invoke(spiderHealthSystem, null);
                }
                else
                {
                    Logger.LogWarning($"Could not find BreakShieldClientRpc method for player {PlayerId}");
                }
            }
            catch (System.Exception e)
            {
                Logger.LogError($"Failed to trigger immune barrier burst for player {PlayerId}: {e.Message}");
            }
        }
    }
}
