using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using System.Reflection;
using Unity.Netcode;
using Logger = Silk.Logger;
using UnityEngine.SceneManagement;


namespace SpiderSurge;

public class AmmoAbility : BaseAbility
{
    public static Dictionary<PlayerInput, AmmoAbility> playerAmmoAbilities = [];

    public override string PerkName => Consts.PerkNames.AmmoAbility;

    public override float AbilityBaseDuration => Consts.Values.Ammo.AbilityBaseDuration;
    public override float AbilityBaseCooldown => Consts.Values.Ammo.AbilityBaseCooldown;
    public override float UltimateBaseCooldown => Consts.Values.Ammo.UltimateBaseCooldown;
    public override float AbilityDurationPerPerkLevel => Consts.Values.Ammo.AbilityDurationIncreasePerLevel;
    public override float AbilityCooldownPerPerkLevel => Consts.Values.Ammo.AbilityCooldownReductionPerLevel;
    public override float UltimateCooldownPerPerkLevel => Consts.Values.Ammo.UltimateCooldownReductionPerLevel;
    public override float UltimateBaseDuration => Consts.Values.Ammo.UltimateBaseDuration;

    // Ultimate: Care Package
    public override bool HasUltimate => true;
    public override string UltimatePerkDisplayName => "Care Package";
    public override string UltimatePerkDescription => "Spawns weapons at half the weapon spawn points on the map.";

    private SpiderWeaponManager weaponManager;
    private float weaponCheckTimer = 0f;
    private static FieldInfo networkAmmoField;
    private Weapon trackedWeapon;
    private float trackedOriginalAmmo = 0f;
    private Weapon lastResolvedWeapon;
    private int lastResolvedFrame = -1;
    private static WeaponSpawner[] cachedWeaponSpawners;
    private static string cachedSceneName;

    protected override void Awake()
    {
        base.Awake();
        if (playerInput is not null)
        {
            playerAmmoAbilities[playerInput] = this;
        }

        if (networkAmmoField is null)
        {
            networkAmmoField = typeof(Weapon).GetField("networkAmmo",
                BindingFlags.NonPublic | BindingFlags.Instance);

            if (networkAmmoField is null)
            {
                Logger.LogError("AmmoAbility: Could not find networkAmmo field!");
            }
        }
    }

    protected override void Start()
    {
        base.Start();
        weaponManager = GetComponentInChildren<SpiderWeaponManager>();
        if (weaponManager is null && playerController is not null)
        {
            weaponManager = playerController.spiderHealthSystem?.GetComponentInChildren<SpiderWeaponManager>();
        }
    }

    protected void Update()
    {
        if (weaponManager is null)
        {
            weaponCheckTimer -= Time.deltaTime;
            if (weaponCheckTimer <= 0)
            {
                weaponCheckTimer = Consts.Values.Ammo.CheckInterval;
                weaponManager = GetComponentInChildren<SpiderWeaponManager>();
                if (weaponManager is null && playerController is not null)
                {
                    weaponManager = playerController.spiderHealthSystem?.GetComponentInChildren<SpiderWeaponManager>();
                }
            }
        }

        if (isActive && weaponManager is not null && weaponManager.equippedWeapon is not null)
        {
            var weapon = weaponManager.equippedWeapon;
            float currentAmmoFloor = GetHeldWeaponFloorAmmo(weapon);

            if (weapon.ammo < currentAmmoFloor)
            {
                SetWeaponAmmo(weapon, currentAmmoFloor);
            }
        }
    }

    private float GetHeldWeaponFloorAmmo(Weapon weapon)
    {
        if (weapon is null) return 0f;

        float originalAmmo = GetTrackedOriginalAmmo(weapon);
        int efficiencyLevel = GetEfficiencyLevel();

        if (efficiencyLevel >= 2)
        {
            float halfMax = Mathf.Ceil(weapon.maxAmmo * 0.5f);
            return Mathf.Max(originalAmmo, halfMax);
        }

        return originalAmmo;
    }

    private int GetEfficiencyLevel() => ModifierManager.instance is null ? 0 : ModifierManager.instance.GetModLevel(Consts.ModifierNames.Efficiency);

    private void EnsureTrackedWeapon(Weapon weapon)
    {
        if (weapon is null)
        {
            return;
        }

        if (trackedWeapon != weapon)
        {
            trackedWeapon = weapon;
            trackedOriginalAmmo = weapon.ammo;
        }
    }

    private float GetTrackedOriginalAmmo(Weapon weapon)
    {
        if (weapon is null) return 0f;
        EnsureTrackedWeapon(weapon);
        return weapon == trackedWeapon ? trackedOriginalAmmo : 0f;
    }

    private float GetRemovalAmmoTarget(Weapon weapon, bool duringActiveUnequip, int efficiencyLevel)
    {
        if (weapon is null) return 0f;

        float originalAmmo = GetTrackedOriginalAmmo(weapon);

        if (efficiencyLevel == 1)
        {
            return originalAmmo;
        }

        if (duringActiveUnequip)
        {
            return originalAmmo;
        }

        //efficiency is 2
        float halfMax = Mathf.Ceil(weapon.maxAmmo * 0.5f);
        return Mathf.Max(originalAmmo, halfMax);
    }

    private bool ShouldDisintegrateOnRemoval(Weapon weapon)
    {
        return weapon is null
            ? false
            : weapon.type is null
            ? true
            : !weapon.type.Contains(Weapon.WeaponType.Explosive)
            && !weapon.type.Contains(Weapon.WeaponType.Mine);
    }

    private void SetWeaponAmmo(Weapon weapon, float value)
    {
        if (weapon is null) return;

        weapon.ammo = value;

        if (!Mathf.Approximately(weapon.ammo, value) && networkAmmoField is not null)
        {
            try
            {
                if (networkAmmoField.GetValue(weapon) is NetworkVariable<float> networkAmmo)
                {
                    networkAmmo.Value = value;
                }
            }
            catch (System.Exception ex)
            {
                Logger.LogError($"AmmoAbility: Failed to set ammo via reflection: {ex.Message}");
            }
        }
    }

    protected override void OnActivate()
    {
        // Play ammo ability sound
        SoundManager.Instance?.PlaySound(
                Consts.SoundNames.AmmoAbility,
                Consts.SoundVolumes.AmmoAbility * Consts.SoundVolumes.MasterVolume
            );

        trackedWeapon = null;
        trackedOriginalAmmo = 0f;
        lastResolvedWeapon = null;
        lastResolvedFrame = -1;

        if (weaponManager is not null && weaponManager.equippedWeapon is not null)
        {
            trackedWeapon = weaponManager.equippedWeapon;
            trackedOriginalAmmo = trackedWeapon.ammo;
            float currentAmmoFloor = GetHeldWeaponFloorAmmo(trackedWeapon);
            SetWeaponAmmo(trackedWeapon, currentAmmoFloor);
        }
    }

    protected override void OnDeactivate()
    {
        if (weaponManager is not null && weaponManager.equippedWeapon is not null)
        {
            ResolveWeaponAmmoOnAbilityRemoval(weaponManager.equippedWeapon, false);
        }

        trackedWeapon = null;
        trackedOriginalAmmo = 0f;
        lastResolvedWeapon = null;
        lastResolvedFrame = -1;
    }

    public static void HandleWeaponRemoved(SpiderWeaponManager manager, Weapon weapon)
    {
        if (manager is null || weapon is null) return;

        PlayerInput playerInput = manager.GetComponentInParent<PlayerInput>();
        if (playerInput is null) return;

        if (!playerAmmoAbilities.TryGetValue(playerInput, out AmmoAbility ability) || ability is null)
        {
            return;
        }

        if (!ability.isActive)
        {
            return;
        }

        ability.ResolveWeaponAmmoOnAbilityRemoval(weapon, true);
    }

    private void ResolveWeaponAmmoOnAbilityRemoval(Weapon weapon, bool duringActiveUnequip)
    {
        if (weapon is null) return;
        if (weapon == lastResolvedWeapon && Time.frameCount == lastResolvedFrame) return;

        int efficiencyLevel = GetEfficiencyLevel();
        if (efficiencyLevel <= 0)
        {
            lastResolvedWeapon = weapon;
            lastResolvedFrame = Time.frameCount;

            if (ShouldDisintegrateOnRemoval(weapon))
            {
                weapon.Disintegrate();
            }
            else
            {
                //for explosives, nothing
            }

            if (weapon == trackedWeapon)
            {
                trackedWeapon = null;
                trackedOriginalAmmo = 0f;
            }

            return;
        }

        float targetAmmo = GetRemovalAmmoTarget(weapon, duringActiveUnequip, efficiencyLevel);
        SetWeaponAmmo(weapon, targetAmmo);
        lastResolvedWeapon = weapon;
        lastResolvedFrame = Time.frameCount;

        if (weapon == trackedWeapon)
        {
            trackedWeapon = null;
            trackedOriginalAmmo = 0f;
        }
    }

    protected override void OnActivateUltimate()
    {
        // Ultimate does not grant ammo locking - it only spawns weapons
        SoundManager.Instance?.PlaySound(
                Consts.SoundNames.AmmoAbility,
                Consts.SoundVolumes.AmmoAbility * Consts.SoundVolumes.MasterVolume
            );

        SpawnWeaponsAtSpawnPoints();

        // Ultimate is instant (no duration), start cooldown immediately
        isActive = false;
        isUltimateActive = false;
        StartCooldown(wasUltimate: true);
    }

    private void SpawnWeaponsAtSpawnPoints()
    {
        try
        {
            string currentScene = SceneManager.GetActiveScene().name;
            if (cachedWeaponSpawners is null || cachedSceneName != currentScene)
            {
                cachedWeaponSpawners = FindObjectsOfType<WeaponSpawner>();
                cachedSceneName = currentScene;
            }
            WeaponSpawner[] weaponSpawners = cachedWeaponSpawners;

            if (weaponSpawners.Length == 0)
            {
                Logger.LogWarning("Care Package: No weapon spawners found");
                return;
            }

            int durationLevel = PerksManager.Instance?.GetPerkLevel(Consts.PerkNames.AbilityDuration) ?? 0;
            bool spawnAtAll = durationLevel >= 2;

            int spawnedCount = 0;
            List<WeaponSpawner> validSpawners = [];

            foreach (WeaponSpawner spawner in weaponSpawners)
            {
                if (spawner is not null) validSpawners.Add(spawner);
            }

            // If not spawning all, shuffle and take half
            if (!spawnAtAll && validSpawners.Count > 1)
            {
                // Fisher-Yates shuffle
                System.Random rng = new();
                int n = validSpawners.Count;
                while (n > 1)
                {
                    n--;
                    int k = rng.Next(n + 1);
                    var temp = validSpawners[k];
                    validSpawners[k] = validSpawners[n];
                    validSpawners[n] = temp;
                }
                // Take only half (rounded up)
                int halfCount = (validSpawners.Count + 1) / 2;
                validSpawners = validSpawners.GetRange(0, halfCount);
            }

            foreach (WeaponSpawner spawner in validSpawners)
            {
                try
                {
                    GameObject weaponPrefab = GetRandomWeaponPrefab();
                    if (weaponPrefab is null) continue;

                    GameObject spawnedWeapon = Instantiate(weaponPrefab, spawner.transform.position, spawner.transform.rotation);

                    NetworkObject netObj = spawnedWeapon.GetComponent<NetworkObject>();
                    if (netObj is not null)
                    {
                        netObj.Spawn(true);
                        netObj.DestroyWithScene = true;
                    }

                    spawnedCount++;
                }
                catch (System.Exception ex)
                {
                    Logger.LogWarning($"Care Package: Failed to spawn at {spawner.name}: {ex.Message}");
                }
            }

            Logger.LogInfo($"Care Package: Spawned weapons at {spawnedCount}/{weaponSpawners.Length} spawn points");
        }
        catch (System.Exception ex)
        {
            Logger.LogError($"Care Package: Error spawning weapons: {ex.Message}");
        }
    }

    private GameObject GetRandomWeaponPrefab()
    {
        try
        {
            // Try to get weapons from SurvivalMode
            if (SurvivalMode.instance is not null && SurvivalMode.instance.GameModeActive())
            {
                return SurvivalMode.instance.GetRandomWeapon(false);
            }
        }
        catch (System.Exception ex)
        {
            Logger.LogWarning($"Weapon Arsenal: Failed to get random weapon: {ex.Message}");
        }
        return null;
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();

        if (playerInput is not null && playerAmmoAbilities.ContainsKey(playerInput))
        {
            playerAmmoAbilities.Remove(playerInput);
        }
    }
}
