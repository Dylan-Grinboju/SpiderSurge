using UnityEngine;
using System.Collections.Generic;
using System.Collections;
using System.Reflection;
using System.Linq;
using Unity.Netcode;
using Logger = Silk.Logger;

namespace SpiderSurge;

public class ImmuneAbility : BaseAbility
{
    private static Dictionary<SpiderHealthSystem, ImmuneAbility> immuneByHealthSystem = [];

    public override string PerkName => Consts.PerkNames.ImmuneAbility;

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

    private static FieldInfo immuneTimeField;
    private static MethodInfo _breakShieldMethod;
    private static FieldInfo _spiderLightField;

    private bool hadBarrierOnSessionStart = false;
    private bool wasHitDuringSession = false;
    private bool isUltimateCastPending = false;
    private bool hadBarrierOnUltimateCastStart = false;

    public bool IsImmune { get; private set; }

    protected override void Awake()
    {
        base.Awake();

        if (immuneTimeField is null)
        {
            immuneTimeField = typeof(SpiderHealthSystem).GetField("_immuneTill",
                BindingFlags.NonPublic | BindingFlags.Instance);

            if (immuneTimeField is null)
            {
                Logger.LogError("ImmuneAbility: Checked for _immuneTill field but it was null! Immunity will not work.");
            }
        }

        _breakShieldMethod ??= typeof(SpiderHealthSystem).GetMethod("BreakShieldClientRpc",
            BindingFlags.NonPublic | BindingFlags.Instance);

        _spiderLightField ??= typeof(SpiderHealthSystem).GetField("spiderLight",
            BindingFlags.NonPublic | BindingFlags.Instance);
    }

    protected override void Start()
    {
        base.Start();
        StartCoroutine(RegisterWithHealthSystem());
    }

    private IEnumerator RegisterWithHealthSystem()
    {
        while (spiderHealthSystem is null)
        {
            yield return null;
        }
        immuneByHealthSystem[spiderHealthSystem] = this;
    }

    public static ImmuneAbility GetByHealthSystem(SpiderHealthSystem healthSystem)
    {
        if (healthSystem is null) return null;
        immuneByHealthSystem.TryGetValue(healthSystem, out var ability);
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
        hadBarrierOnSessionStart = spiderHealthSystem is not null && spiderHealthSystem.HasShield();

        ApplyImmunity(true);

        SoundManager.Instance?.PlaySound(
                Consts.SoundNames.ImmuneAbility,
                Consts.SoundVolumes.ImmuneAbility * Consts.SoundVolumes.MasterVolume
            );
    }

    protected override void OnDeactivate()
    {
        if (spiderHealthSystem is not null)
        {
            if (hadBarrierOnSessionStart && wasHitDuringSession)
            {
                DestroyBarrier();
                spiderHealthSystem.DisableShield();
            }
        }

        if (IsImmune)
        {
            ApplyImmunity(false);
        }

        wasHitDuringSession = false;
        hadBarrierOnSessionStart = false;
    }

    protected override void OnActivateUltimate()
    {
        isUltimateCastPending = true;
        hadBarrierOnUltimateCastStart = spiderHealthSystem is not null && spiderHealthSystem.HasShield();

        SoundManager.Instance?.PlaySound(
                Consts.SoundNames.ImmuneUlt,
                Consts.SoundVolumes.ImmuneUlt * Consts.SoundVolumes.MasterVolume
            );
    }

    protected override void OnDeactivateUltimate()
    {
        if (!isUltimateCastPending)
        {
            return;
        }

        isUltimateCastPending = false;
        ExecuteUltimateEffect();
        hadBarrierOnUltimateCastStart = false;
    }

    protected override bool ShouldPlayAbilityEndedSound(bool wasUltimate) => !wasUltimate;


    private Dictionary<SpriteRenderer, Color> _originalColors = [];
    private Color? _originalLightColor;
    private float? _originalLightIntensity;

    private void ApplyRadiance(bool enable)
    {
        if (spiderHealthSystem is null) return;

        if (enable)
        {
            // -- 1. Apply to Sprites --
            if (spiderHealthSystem.spritesRoot is not null)
            {
                var renderers = spiderHealthSystem.spritesRoot.GetComponentsInChildren<SpriteRenderer>(true);
                foreach (var sr in renderers)
                {
                    if (sr is null) continue;
                    if (!_originalColors.ContainsKey(sr))
                    {
                        _originalColors[sr] = sr.color;
                    }
                    sr.color = new Color(1f, 0.84f, 0.0f, 1f); // Gold
                }
            }

            // Also the head
            if (spiderHealthSystem.head is not null)
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
                kvp.Key?.color = kvp.Value;
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
            if (_spiderLightField is null) return;

            var lightObj = _spiderLightField.GetValue(spiderHealthSystem);
            if (lightObj is null) return;

            var lightType = lightObj.GetType();
            var colorProp = lightType.GetProperty("color");
            var intensityProp = lightType.GetProperty("intensity");

            if (enable)
            {
                if (colorProp is not null && _originalLightColor is null)
                    _originalLightColor = (Color)colorProp.GetValue(lightObj, null);

                if (intensityProp is not null && _originalLightIntensity is null)
                    _originalLightIntensity = (float)intensityProp.GetValue(lightObj, null);

                colorProp?.SetValue(lightObj, new Color(1f, 0.6f, 0.0f), null);
                intensityProp?.SetValue(lightObj, 4.0f, null);
            }
            else
            {
                if (colorProp is not null && _originalLightColor is not null)
                {
                    colorProp.SetValue(lightObj, _originalLightColor.Value, null);
                    _originalLightColor = null;
                }

                if (intensityProp is not null && _originalLightIntensity is not null)
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
        if (spiderHealthSystem is null) return;
        IsImmune = enable;

        ApplyRadiance(enable);

        if (enable)
        {
            try
            {
                immuneTimeField?.SetValue(spiderHealthSystem, float.MaxValue);
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
                // Reset immunity to current time (expired)
                immuneTimeField?.SetValue(spiderHealthSystem, Time.time);
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
            Logger.LogDebug("[ImmuneAbility] Ultimate effect skipped: not server authority.");
            return;
        }

        if (TryRespawnDeadPlayer(out var revivedPlayer))
        {
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

        if (LobbyController.instance is null)
        {
            return false;
        }

        var playerControllers = LobbyController.instance.GetPlayerControllers();
        if (playerControllers is null || !playerControllers.Any())
        {
            return false;
        }

        var spawnPoints = LobbyController.instance.GetSpawnPoints();
        if (spawnPoints is null || spawnPoints.Length == 0)
        {
            return false;
        }

        var deadPlayers = playerControllers.Where(pc => pc is not null && !pc.isAlive).ToList();
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
            if (revivedPlayer is null)
            {
                Logger.LogWarning("[ImmuneAbility] Revived player reference became null before shield application.");
                yield break;
            }

            var revivedHealth = revivedPlayer.spiderHealthSystem;
            if (revivedHealth is not null)
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
        var friendlyWaspPrefab = SurvivalMode.instance?.friendlyWasp;
        if (friendlyWaspPrefab is null)
        {
            Logger.LogWarning("[ImmuneAbility] Friendly wasp prefab is missing; cannot spawn friendly wasps.");
            return;
        }

        var spawnPoints = LobbyController.instance?.GetSpawnPoints();
        if (spawnPoints is null || spawnPoints.Length == 0)
        {
            spawnPoints = GameObject.FindGameObjectsWithTag("EnemySpawn").Select(go => go.transform).ToArray();
        }

        if (spawnPoints is null || spawnPoints.Length == 0)
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
        if (friendlyWaspPrefab is null)
        {
            return false;
        }

        if (spawn is null)
        {
            Logger.LogWarning("[ImmuneAbility] Selected friendly wasp spawn point was null.");
            return false;
        }

        var spawnedObj = Instantiate(friendlyWaspPrefab, spawn.position, spawn.rotation);
        spawnedObj.SetActive(true);

        var netObj = spawnedObj.GetComponent<NetworkObject>();
        if (netObj is not null)
        {
            netObj.Spawn(true);
            netObj.DestroyWithScene = true;
        }
        else
        {
            Logger.LogWarning("[ImmuneAbility] Friendly wasp has no NetworkObject component.");
        }

        if (EnemySpawner.instance is not null && !EnemySpawner.instance.spawnedEnemies.Contains(spawnedObj))
        {
            EnemySpawner.instance.spawnedEnemies.Add(spawnedObj);
        }

        return true;
    }

    private List<Transform> GetDistinctSpawnPoints(Transform[] spawnPoints, int count)
    {
        List<Transform> selectedSpawns = [];
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

    private bool IsServerAuthority() => NetworkManager.Singleton is null ? true : NetworkManager.Singleton.IsServer || NetworkManager.Singleton.IsHost;

    protected override void OnDestroy()
    {
        base.OnDestroy();

        if (spiderHealthSystem is not null && immuneByHealthSystem.ContainsKey(spiderHealthSystem))
        {
            ApplyImmunity(false);
            immuneByHealthSystem.Remove(spiderHealthSystem);
        }

        isUltimateCastPending = false;
        hadBarrierOnUltimateCastStart = false;
    }

    private void DestroyBarrier()
    {
        try
        {
            if (_breakShieldMethod is not null)
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
            Logger.LogError($"Failed to trigger immune barrier burst for player {playerInput.playerIndex}: {e.Message}");
        }
    }
}