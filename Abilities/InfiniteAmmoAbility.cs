using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using System.Reflection;
using Unity.Netcode;
using Logger = Silk.Logger;
using UnityEngine.SceneManagement;


namespace SpiderSurge
{
    public class InfiniteAmmoAbility : BaseAbility
    {
        public static Dictionary<PlayerInput, InfiniteAmmoAbility> playerInfiniteAmmo = new Dictionary<PlayerInput, InfiniteAmmoAbility>();

        public override string PerkName => Consts.PerkNames.InfiniteAmmoAbility;

        public override float BaseDuration => Consts.Values.InfiniteAmmo.BaseDuration;
        public override float DurationPerPerkLevel => Consts.Values.InfiniteAmmo.DurationIncreasePerLevel;

        public override float BaseCooldown => Consts.Values.InfiniteAmmo.BaseCooldown;
        public override float CooldownPerPerkLevel => Consts.Values.InfiniteAmmo.CooldownReductionPerLevel;
        public override float UltimateCooldownMultiplier => Consts.Values.InfiniteAmmo.UltimateCooldownMultiplier;

        // Ultimate: Weapon Arsenal
        public override bool HasUltimate => true;
        public override string UltimatePerkDisplayName => "Ammo Ultimate";
        public override string UltimatePerkDescription => "Spawns weapons at all weapon spawn points on the map.";

        private SpiderWeaponManager weaponManager;
        private float storedMaxAmmo = 0f;
        private float weaponCheckTimer = 0f;
        private static FieldInfo networkAmmoField;
        private Weapon lastWeapon;
        private static WeaponSpawner[] cachedWeaponSpawners;
        private static string cachedSceneName;

        protected override void Awake()
        {
            base.Awake();
            if (playerInput != null)
            {
                playerInfiniteAmmo[playerInput] = this;
            }

            if (networkAmmoField == null)
            {
                networkAmmoField = typeof(Weapon).GetField("networkAmmo",
                    BindingFlags.NonPublic | BindingFlags.Instance);

                if (networkAmmoField == null)
                {
                    Logger.LogError("InfiniteAmmoAbility: Could not find networkAmmo field!");
                }
            }
        }

        protected override void Start()
        {
            base.Start();
            weaponManager = GetComponentInChildren<SpiderWeaponManager>();
            if (weaponManager == null && playerController != null)
            {
                weaponManager = playerController.spiderHealthSystem?.GetComponentInChildren<SpiderWeaponManager>();
            }
        }

        protected void Update()
        {
            if (weaponManager == null)
            {
                weaponCheckTimer -= Time.deltaTime;
                if (weaponCheckTimer <= 0)
                {
                    weaponCheckTimer = Consts.Values.InfiniteAmmo.CheckInterval;
                    weaponManager = GetComponentInChildren<SpiderWeaponManager>();
                    if (weaponManager == null && playerController != null)
                    {
                        weaponManager = playerController.spiderHealthSystem?.GetComponentInChildren<SpiderWeaponManager>();
                    }
                }
            }

            if (isActive && weaponManager != null && weaponManager.equippedWeapon != null)
            {
                var weapon = weaponManager.equippedWeapon;

                if (weapon != lastWeapon)
                {
                    lastWeapon = weapon;
                    storedMaxAmmo = GetTargetAmmoCount(weapon);
                }

                if (weapon.ammo < storedMaxAmmo)
                {
                    SetWeaponAmmo(weapon, storedMaxAmmo);
                }
            }
        }

        private float GetTargetAmmoCount(Weapon weapon)
        {
            if (weapon == null) return 0f;

            // Default: Lock at current ammo (no refill)
            float floor = weapon.ammo;

            // Synergy Logic
            if (PerksManager.Instance != null && PerksManager.Instance.GetPerkLevel(Consts.PerkNames.Synergy) > 0 && ModifierManager.instance != null)
            {
                int efficiencyLevel = ModifierManager.instance.GetModLevel(Consts.ModifierNames.Efficiency);

                if (efficiencyLevel == 1)
                {
                    // Level 1: Ensure at least 50% ammo
                    float halfMax = Mathf.Ceil(weapon.maxAmmo * 0.5f);
                    if (halfMax > floor) floor = halfMax;
                }
                else if (efficiencyLevel >= 2)
                {
                    // Level 2: Fill to max
                    floor = weapon.maxAmmo;
                }
            }

            return floor;
        }

        private void SetWeaponAmmo(Weapon weapon, float value)
        {
            if (weapon == null) return;

            weapon.ammo = value;

            if (weapon.ammo < value && networkAmmoField != null)
            {
                try
                {
                    var networkAmmo = networkAmmoField.GetValue(weapon) as NetworkVariable<float>;
                    if (networkAmmo != null)
                    {
                        networkAmmo.Value = value;
                    }
                }
                catch (System.Exception ex)
                {
                    Logger.LogError($"InfiniteAmmoAbility: Failed to set ammo via reflection: {ex.Message}");
                }
            }
        }

        protected override void OnActivate()
        {
            storedMaxAmmo = 0f;

            if (weaponManager != null && weaponManager.equippedWeapon != null)
            {
                lastWeapon = weaponManager.equippedWeapon;
                storedMaxAmmo = GetTargetAmmoCount(lastWeapon);
                SetWeaponAmmo(lastWeapon, storedMaxAmmo);
            }
            else
            {
                Logger.LogWarning($"Infinite Ammo ACTIVATED for player {playerInput.playerIndex} - no weapon equipped");
            }
        }

        protected override void OnDeactivate()
        {
            storedMaxAmmo = 0f;
        }

        protected override void OnActivateUltimate()
        {
            OnActivate();
            SpawnWeaponsAtAllSpawnPoints();
        }

        private void SpawnWeaponsAtAllSpawnPoints()
        {
            try
            {
                string currentScene = SceneManager.GetActiveScene().name;
                if (cachedWeaponSpawners == null || cachedSceneName != currentScene)
                {
                    cachedWeaponSpawners = FindObjectsOfType<WeaponSpawner>();
                    cachedSceneName = currentScene;
                }
                WeaponSpawner[] weaponSpawners = cachedWeaponSpawners;

                if (weaponSpawners.Length == 0)
                {
                    Logger.LogWarning("Weapon Arsenal: No weapon spawners found");
                    return;
                }

                int spawnedCount = 0;

                foreach (WeaponSpawner spawner in weaponSpawners)
                {
                    if (spawner == null) continue;

                    try
                    {
                        GameObject weaponPrefab = GetRandomWeaponPrefab();
                        if (weaponPrefab == null) continue;

                        GameObject spawnedWeapon = Instantiate(weaponPrefab, spawner.transform.position, spawner.transform.rotation);

                        NetworkObject netObj = spawnedWeapon.GetComponent<NetworkObject>();
                        if (netObj != null)
                        {
                            netObj.Spawn(true);
                            netObj.DestroyWithScene = true;
                        }

                        spawnedCount++;
                    }
                    catch (System.Exception ex)
                    {
                        Logger.LogWarning($"Weapon Arsenal: Failed to spawn at {spawner.name}: {ex.Message}");
                    }
                }

                Logger.LogInfo($"Weapon Arsenal: Spawned {spawnedCount} weapons at {weaponSpawners.Length} spawn points");
            }
            catch (System.Exception ex)
            {
                Logger.LogError($"Weapon Arsenal: Error spawning weapons: {ex.Message}");
            }
        }

        private GameObject GetRandomWeaponPrefab()
        {
            try
            {
                // Try to get weapons from VersusMode or SurvivalMode
                if (SurvivalMode.instance != null && SurvivalMode.instance.GameModeActive())
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

            if (playerInput != null && playerInfiniteAmmo.ContainsKey(playerInput))
            {
                playerInfiniteAmmo.Remove(playerInput);
            }
        }
    }
}
