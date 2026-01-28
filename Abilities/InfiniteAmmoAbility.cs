using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using System.Reflection;
using Unity.Netcode;
using Logger = Silk.Logger;

namespace SpiderSurge
{
    public class InfiniteAmmoAbility : BaseAbility
    {
        public static Dictionary<PlayerInput, InfiniteAmmoAbility> playerInfiniteAmmo = new Dictionary<PlayerInput, InfiniteAmmoAbility>();

        public override string PerkName => "infiniteAmmoAbility";

        public override float BaseDuration => 5f;
        public override float DurationPerPerkLevel => 5f;

        public override float BaseCooldown => 11f;
        public override float CooldownPerPerkLevel => 5f;

        // Ultimate: Weapon Arsenal
        public override bool HasUltimate => true;
        public override string UltimatePerkDisplayName => "Weapon Arsenal";
        public override string UltimatePerkDescription => "Spawns weapons at all weapon spawn points on the map.";

        private SpiderWeaponManager weaponManager;
        private float storedMaxAmmo = 0f;
        private static FieldInfo networkAmmoField;

        protected override void Awake()
        {
            base.Awake();
            if (playerInput != null)
            {
                playerInfiniteAmmo[playerInput] = this;
            }

            // Cache reflection field for networkAmmo
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
            // SpiderWeaponManager is a child component, not a parent
            weaponManager = GetComponentInChildren<SpiderWeaponManager>();
            if (weaponManager == null && playerController != null)
            {
                // Try from the player controller's health system
                weaponManager = playerController.spiderHealthSystem?.GetComponentInChildren<SpiderWeaponManager>();
            }
        }

        protected override void Update()
        {
            base.Update();

            if (weaponManager == null)
            {
                weaponManager = GetComponentInChildren<SpiderWeaponManager>();
                if (weaponManager == null && playerController != null)
                {
                    weaponManager = playerController.spiderHealthSystem?.GetComponentInChildren<SpiderWeaponManager>();
                }
            }

            if (isActive && weaponManager != null && weaponManager.equippedWeapon != null)
            {
                var weapon = weaponManager.equippedWeapon;

                if (storedMaxAmmo <= 0f || weapon.maxAmmo > storedMaxAmmo)
                {
                    storedMaxAmmo = weapon.maxAmmo;
                }

                if (weapon.ammo < storedMaxAmmo)
                {
                    SetWeaponAmmo(weapon, storedMaxAmmo);
                }
            }
        }

        private void SetWeaponAmmo(Weapon weapon, float value)
        {
            if (weapon == null) return;

            // First try using the normal setter (works if we're host)
            weapon.ammo = value;

            // If that didn't work (we're not host), use reflection
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
                storedMaxAmmo = weaponManager.equippedWeapon.maxAmmo;
                float previousAmmo = weaponManager.equippedWeapon.ammo;

                SetWeaponAmmo(weaponManager.equippedWeapon, storedMaxAmmo);
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
            // First, activate normal infinite ammo effect
            OnActivate();

            // Then spawn weapons at all weapon spawn points
            SpawnWeaponsAtAllSpawnPoints();
        }

        private void SpawnWeaponsAtAllSpawnPoints()
        {
            // // Only spawn on host/server
            // if (!NetworkManager.Singleton.IsHost && !NetworkManager.Singleton.IsServer)
            // {
            //     Logger.LogInfo("Weapon Arsenal: Client-side only, skipping weapon spawn");
            //     return;
            // }

            try
            {
                // Find all weapon spawners in the scene
                WeaponSpawner[] weaponSpawners = FindObjectsOfType<WeaponSpawner>();

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
                        // Get a random weapon and spawn it at this spawner's position
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
