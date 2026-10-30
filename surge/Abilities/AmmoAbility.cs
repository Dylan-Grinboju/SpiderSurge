using System;
using UnityEngine;
using System.Collections.Generic;
using Unity.Netcode;
using SpiderSurge.Vfx;
using Logger = Silk.Logger;
using UnityEngine.SceneManagement;

namespace SpiderSurge
{
    public class AmmoAbility : BaseAbility
    {
        public override string PerkName => Consts.PerkNames.AmmoAbility;
        public override AbilityKind Kind => AbilityKind.Ammo;

        public override float AbilityBaseDuration => 0f;
        public override float AbilityBaseCooldown => Consts.Values.Ammo.AbilityBaseCooldown
            - ((PerksManager.Instance?.GetPerkLevel(Consts.PerkNames.AbilityDuration) ?? 0) >= 1
                ? Consts.Values.Ammo.RechargeReduction : 0f);
        public override float UltimateBaseCooldown => Consts.Values.Ammo.UltimateBaseCooldown;
        public override float AbilityCooldownPerPerkLevel => Consts.Values.Ammo.AbilityCooldownReductionPerLevel;
        public override float UltimateCooldownPerPerkLevel => Consts.Values.Ammo.UltimateCooldownReductionPerLevel;
        public override float UltimateBaseDuration => Consts.Values.Ammo.UltimateBaseDuration;
        public override bool HasUltimate => true;

        private SpiderWeaponManager weaponManager;
        private float weaponCheckTimer;
        private static WeaponSpawner[] cachedWeaponSpawners;
        private static string cachedSceneName;

        internal static void ForgetWeaponSpawners()
        {
            cachedWeaponSpawners = null;
        }

        protected override void Start()
        {
            base.Start();
            FindWeaponManager();
        }

        private void FindWeaponManager()
        {
            weaponManager = GetComponentInChildren<SpiderWeaponManager>();
            if (weaponManager == null && playerController != null)
                weaponManager = playerController.spiderHealthSystem?.GetComponentInChildren<SpiderWeaponManager>();
        }

        protected void Update()
        {
            if (weaponManager != null) return;
            weaponCheckTimer -= Time.deltaTime;
            if (weaponCheckTimer > 0f) return;
            weaponCheckTimer = Consts.Values.Ammo.CheckInterval;
            FindWeaponManager();
        }

        protected override bool CanActivate(bool ultimate)
        {
            if (ultimate) return true;
            if (weaponManager == null) FindWeaponManager();
            return weaponManager != null && weaponManager.equippedWeapon != null
                && weaponManager.equippedWeapon.maxAmmo > 0f;
        }

        protected override void OnActivate()
        {
            Weapon weapon = weaponManager.equippedWeapon;
            if (IsAuthority)
            {
                int efficiency = ModifierManager.instance != null
                    ? Mathf.Clamp(ModifierManager.instance.GetModLevel(Consts.ModifierNames.Efficiency), 0, 2) : 0;
                float target = weapon.maxAmmo * (1f + 0.25f * efficiency);
                weapon.ammo = Mathf.Max(weapon.ammo, target);
            }

            PlayCasterSound(Consts.SoundNames.AmmoAbility, Consts.SoundVolumes.AmmoAbility);
            ClipVfx.Cast(weapon.transform.position);
            SurgeNet.SendFx(SurgeFx.Clip, PlayerId, weapon.transform.position, 0f, true);
            UnlimitedPowerAchievement.Cast(this);
            isActive = false;
            StartCooldown();
        }

        public static void ShowRemoteDrop(Vector3 position)
        {
            WeaponSpawner nearest = null;
            float nearestDistance = 1f;
            foreach (WeaponSpawner spawner in FindObjectsOfType<WeaponSpawner>())
            {
                float distance = (spawner.transform.position - position).sqrMagnitude;
                if (distance >= nearestDistance) continue;
                nearest = spawner;
                nearestDistance = distance;
            }
            if (nearest != null) ClipVfx.Drop(nearest);
        }

        protected override void OnActivateUltimate()
        {
            PlayCasterSound(Consts.SoundNames.AmmoAbility, Consts.SoundVolumes.AmmoAbility);

            SpawnWeaponsAtSpawnPoints();

            // Ultimate is instant (no duration), start cooldown immediately
            isActive = false;
            isUltimateActive = false;
            StartCooldown(wasUltimate: true);
        }

        private static bool IsServerAuthority()
        {
            if (NetworkManager.Singleton == null)
            {
                return true;
            }
            return NetworkManager.Singleton.IsServer || NetworkManager.Singleton.IsHost;
        }

        private void SpawnWeaponsAtSpawnPoints()
        {
            if (!IsServerAuthority())
            {
                Logger.LogDebug("Care Package: Skipping spawn on non-server client.");
                return;
            }

            try
            {
                string currentScene = SceneManager.GetActiveScene().name;
                // A survival run can load the same map again, which destroys the cached spawners without changing the scene name
                if (cachedWeaponSpawners == null || cachedSceneName != currentScene || Array.Exists(cachedWeaponSpawners, spawner => spawner == null))
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
                List<WeaponSpawner> validSpawners = new List<WeaponSpawner>();

                foreach (WeaponSpawner spawner in weaponSpawners)
                {
                    if (spawner != null) validSpawners.Add(spawner);
                }

                // If not spawning all, shuffle and take half
                if (!spawnAtAll && validSpawners.Count > 1)
                {
                    // Fisher-Yates shuffle
                    System.Random rng = new System.Random();
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
                        if (weaponPrefab == null) continue;

                        GameObject spawnedWeapon = Instantiate(weaponPrefab, spawner.transform.position, spawner.transform.rotation);

                        NetworkObject netObj = spawnedWeapon.GetComponent<NetworkObject>();
                        if (netObj != null)
                        {
                            netObj.Spawn(true);
                            netObj.DestroyWithScene = true;
                        }
                        ClipVfx.Drop(spawner);
                        SurgeNet.SendFx(SurgeFx.CareDrop, PlayerId, spawner.transform.position, 0f, false);

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

        // Survival's weapon list is empty outside survival, so Teams hands Care Package the versus match's pool
        public static Func<GameObject> FallbackWeapon;

        private GameObject GetRandomWeaponPrefab()
        {
            try
            {
                // Try to get weapons from SurvivalMode
                if (SurvivalMode.instance != null && SurvivalMode.instance.GameModeActive())
                {
                    GameObject weapon = SurvivalMode.instance.GetRandomWeapon(false);
                    return weapon != null ? weapon : FallbackWeapon?.Invoke();
                }
            }
            catch (System.Exception ex)
            {
                Logger.LogWarning($"Weapon Arsenal: Failed to get random weapon: {ex.Message}");
            }
            return FallbackWeapon?.Invoke();
        }

    }
}
