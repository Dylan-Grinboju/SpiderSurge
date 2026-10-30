using System;
using UnityEngine;
using System.Collections.Generic;
using Unity.Netcode;
using SpiderSurge.Common;
using SpiderSurge.Vfx;
using Logger = Silk.Logger;
using UnityEngine.SceneManagement;

namespace SpiderSurge
{
    public class AmmoAbility : BaseAbility
    {
        private static readonly Dictionary<Weapon, AmmoAbility> overchargedWeapons = new Dictionary<Weapon, AmmoAbility>();
        private static readonly HashSet<SerializationWeaponName> normalBurnoutWeapons = new HashSet<SerializationWeaponName>
        {
            SerializationWeaponName.Grenade,
            SerializationWeaponName.BigGrenade,
            SerializationWeaponName.PerArmedGrenade,
            SerializationWeaponName.GravityGrenade,
            SerializationWeaponName.Mine,
            SerializationWeaponName.BoomStick,
            SerializationWeaponName.LaserCube,
            SerializationWeaponName.DeathCube
        };

        public override string PerkName => Consts.PerkNames.AmmoAbility;
        public override AbilityKind Kind => AbilityKind.Ammo;
        public override float AbilityBaseDuration => Consts.Values.Ammo.AbilityBaseDuration;
        public override float AbilityDurationPerPerkLevel => Consts.Values.Ammo.AbilityDurationIncreasePerLevel;
        public override float AbilityBaseCooldown => Consts.Values.Ammo.AbilityBaseCooldown;
        public override float UltimateBaseCooldown => Consts.Values.Ammo.UltimateBaseCooldown;
        public override float AbilityCooldownPerPerkLevel => Consts.Values.Ammo.AbilityCooldownReductionPerLevel;
        public override float UltimateCooldownPerPerkLevel => Consts.Values.Ammo.UltimateCooldownReductionPerLevel;
        public override float UltimateBaseDuration => Consts.Values.Ammo.UltimateBaseDuration;
        public override bool HasUltimate => true;

        private SpiderWeaponManager weaponManager;
        private float weaponCheckTimer;
        private Weapon trackedWeapon;
        private float trackedAmmo;
        private bool burstInterrupted;
        private WeaponAura aura;
        private static WeaponSpawner[] cachedWeaponSpawners;
        private static string cachedSceneName;

        internal static void ForgetWeaponSpawners()
        {
            cachedWeaponSpawners = null;
        }

        internal static float AmmoFloorFor(Weapon weapon)
        {
            if (weapon == null || !weapon.equipped) return -1f;
            return overchargedWeapons.TryGetValue(weapon, out AmmoAbility ability) && ability != null && ability.isActive
                ? ability.trackedAmmo : -1f;
        }

        internal static void MarkUnequipped(Weapon weapon)
        {
            if (overchargedWeapons.TryGetValue(weapon, out AmmoAbility ability) && ability != null)
                ability.burstInterrupted = true;
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

        protected void LateUpdate()
        {
            if (!isActive || isUltimateActive) return;
            if (!burstInterrupted && trackedWeapon != null && weaponManager != null && weaponManager.equippedWeapon == trackedWeapon
                && trackedWeapon.equipped && IsPlayerAlive()) return;
            if (durationCoroutine != null)
            {
                StopCoroutine(durationCoroutine);
                durationCoroutine = null;
            }
            Deactivate();
            if (!IsRemote && ShouldPlayAbilityEndedSound(false)) PlayAbilityEndedSound(false);
            StartCooldown();
        }

        protected override bool CanActivate(bool ultimate)
        {
            if (ultimate) return true;
            if (weaponManager == null) FindWeaponManager();
            return weaponManager != null && weaponManager.equippedWeapon != null
                && weaponManager.equippedWeapon.maxAmmo > 0f && weaponManager.equippedWeapon.ammo > 0f;
        }

        protected override void OnActivate()
        {
            trackedWeapon = weaponManager.equippedWeapon;
            trackedAmmo = trackedWeapon.ammo;
            burstInterrupted = false;
            overchargedWeapons[trackedWeapon] = this;
            PlayCasterSound(Consts.SoundNames.AmmoAbility, Consts.SoundVolumes.AmmoAbility);
            ClipVfx.Cast(trackedWeapon.transform.position);
            ShowAura(trackedWeapon.transform);
            SurgeNet.SendFx(SurgeFx.Clip, PlayerId, trackedWeapon.transform.position, 0f, true);
            UnlimitedPowerAchievement.Cast(this);
        }

        private void ShowAura(Transform weapon)
        {
            StopAura();
            aura = WeaponAura.Attach(weapon);
        }

        private void StopAura()
        {
            if (aura == null) return;
            aura.Stop();
            aura = null;
        }

        public static void ShowRemoteClip(int playerId, Vector3 position, bool on)
        {
            AmmoAbility ability = PlayerAbilityHandler.FindAbility(playerId, AbilityKind.Ammo) as AmmoAbility;
            if (ability == null) return;
            ability.StopAura();
            if (!on) return;
            ClipVfx.Cast(position);
            Weapon weapon = ability.weaponManager != null ? ability.weaponManager.equippedWeapon : null;
            if (weapon != null) ability.ShowAura(weapon.transform);
        }

        internal static bool CanSalvage(int efficiencyLevel, float roll)
        {
            return efficiencyLevel > 0 && roll < Mathf.Min(efficiencyLevel, 2) * Consts.Values.Ammo.SalvageChancePerLevel;
        }

        protected override void OnDeactivate()
        {
            StopAura();
            SurgeNet.SendFx(SurgeFx.Clip, PlayerId, Vector3.zero, 0f, false);
            Weapon weapon = trackedWeapon;
            float originalAmmo = trackedAmmo;
            trackedWeapon = null;
            trackedAmmo = 0f;
            if (!ReferenceEquals(weapon, null)) overchargedWeapons.Remove(weapon);
            if (!IsAuthority || weapon == null || normalBurnoutWeapons.Contains(weapon.serializationWeaponName)) return;

            int efficiency = ModifierManager.instance != null
                ? ModifierManager.instance.GetModLevel(Consts.ModifierNames.Efficiency) : 0;
            if (efficiency <= 0 || !CanSalvage(efficiency, UnityEngine.Random.value))
            {
                weapon.Disintegrate();
                return;
            }

            float reserve = weapon is ParticleBlade ? weapon.maxAmmo * Consts.Values.Ammo.SalvageBladeFraction : 1f;
            weapon.ammo = Mathf.Min(originalAmmo, reserve);
            ClipVfx.Salvage(weapon.transform.position);
            SurgeNet.SendFx(SurgeFx.Salvage, PlayerId, weapon.transform.position, 0f, true);
        }

        protected override void OnDestroy()
        {
            StopAura();
            if (!ReferenceEquals(trackedWeapon, null)) overchargedWeapons.Remove(trackedWeapon);
            trackedWeapon = null;
            base.OnDestroy();
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
