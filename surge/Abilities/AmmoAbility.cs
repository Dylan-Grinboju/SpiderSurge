using System;
using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using System.Reflection;
using Unity.Netcode;
using SpiderSurge.Common;
using SpiderSurge.Vfx;
using Logger = Silk.Logger;
using UnityEngine.SceneManagement;


namespace SpiderSurge
{
    public class AmmoAbility : BaseAbility
    {
        public static Dictionary<int, AmmoAbility> playerAmmoAbilities = new Dictionary<int, AmmoAbility>();

        // Weapon.ammo is set on every shot, so the holder is only looked up while some Bottomless Clip is running
        internal static AmmoAbility ActiveFor(Weapon weapon)
        {
            if (!AnyActive || weapon == null || !weapon.equipped || weapon.owner == null) return null;

            if (!playerAmmoAbilities.TryGetValue(Players.IdOf(weapon.owner), out AmmoAbility ability) || ability == null || !ability.isActive)
                return null;

            return ability;
        }

        private static bool AnyActive
        {
            get
            {
                foreach (AmmoAbility ability in playerAmmoAbilities.Values)
                {
                    if (ability != null && ability.isActive) return true;
                }
                return false;
            }
        }

        public override string PerkName => Consts.PerkNames.AmmoAbility;
        public override AbilityKind Kind => AbilityKind.Ammo;

        public override float AbilityBaseDuration => Consts.Values.Ammo.AbilityBaseDuration;
        public override float AbilityBaseCooldown => Consts.Values.Ammo.AbilityBaseCooldown;
        public override float UltimateBaseCooldown => Consts.Values.Ammo.UltimateBaseCooldown;
        public override float AbilityDurationPerPerkLevel => Consts.Values.Ammo.AbilityDurationIncreasePerLevel;
        public override float AbilityCooldownPerPerkLevel => Consts.Values.Ammo.AbilityCooldownReductionPerLevel;
        public override float UltimateCooldownPerPerkLevel => Consts.Values.Ammo.UltimateCooldownReductionPerLevel;
        public override float UltimateBaseDuration => Consts.Values.Ammo.UltimateBaseDuration;

        // Ultimate: Care Package
        public override bool HasUltimate => true;

        private SpiderWeaponManager weaponManager;
        private float weaponCheckTimer = 0f;
        private static FieldInfo networkAmmoField;
        private Weapon trackedWeapon;
        private float trackedOriginalAmmo = 0f;
        private Weapon lastResolvedWeapon;
        private int lastResolvedFrame = -1;
        private bool showingRemote;
        private static WeaponSpawner[] cachedWeaponSpawners;
        private static string cachedSceneName;

        internal static void ForgetWeaponSpawners()
        {
            cachedWeaponSpawners = null;
        }

        protected override void Awake()
        {
            base.Awake();

            if (networkAmmoField == null)
            {
                networkAmmoField = typeof(Weapon).GetField("networkAmmo",
                    BindingFlags.NonPublic | BindingFlags.Instance);

                if (networkAmmoField == null)
                {
                    Logger.LogError("AmmoAbility: Could not find networkAmmo field!");
                }
            }
        }

        protected override void Start()
        {
            base.Start();
            if (PlayerId >= 0)
            {
                playerAmmoAbilities[PlayerId] = this;
            }
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
                    weaponCheckTimer = Consts.Values.Ammo.CheckInterval;
                    weaponManager = GetComponentInChildren<SpiderWeaponManager>();
                    if (weaponManager == null && playerController != null)
                    {
                        weaponManager = playerController.spiderHealthSystem?.GetComponentInChildren<SpiderWeaponManager>();
                    }
                }
            }
        }

        internal float GetHeldWeaponFloorAmmo(Weapon weapon)
        {
            if (weapon == null) return 0f;

            float originalAmmo = GetTrackedOriginalAmmo(weapon);
            int efficiencyLevel = GetEfficiencyLevel();

            if (efficiencyLevel >= 2)
            {
                float halfMax = Mathf.Ceil(weapon.maxAmmo * 0.5f);
                return Mathf.Max(originalAmmo, halfMax);
            }

            return originalAmmo;
        }

        private int GetEfficiencyLevel()
        {
            if (ModifierManager.instance == null) return 0;
            return ModifierManager.instance.GetModLevel(Consts.ModifierNames.Efficiency);
        }

        private void EnsureTrackedWeapon(Weapon weapon)
        {
            if (weapon == null)
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
            if (weapon == null) return 0f;
            return weapon == trackedWeapon ? trackedOriginalAmmo : 0f;
        }

        private float GetRemovalAmmoTarget(Weapon weapon, bool duringActiveUnequip, int efficiencyLevel)
        {
            if (weapon == null) return 0f;

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

        private static readonly HashSet<SerializationWeaponName> nonDisintegrateWeapons = new HashSet<SerializationWeaponName>
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

        private bool ShouldDisintegrateOnRemoval(Weapon weapon)
        {
            if (weapon == null) return false;
            return !nonDisintegrateWeapons.Contains(weapon.serializationWeaponName);
        }

        private void SetWeaponAmmo(Weapon weapon, float value)
        {
            if (weapon == null || !IsAuthority) return;

            weapon.ammo = value;

            if (!Mathf.Approximately(weapon.ammo, value) && networkAmmoField != null)
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
            PlayCasterSound(Consts.SoundNames.AmmoAbility, Consts.SoundVolumes.AmmoAbility);

            trackedWeapon = null;
            trackedOriginalAmmo = 0f;
            lastResolvedWeapon = null;
            lastResolvedFrame = -1;

            if (weaponManager != null && weaponManager.equippedWeapon != null)
            {
                trackedWeapon = weaponManager.equippedWeapon;
                trackedOriginalAmmo = trackedWeapon.ammo;
                float currentAmmoFloor = GetHeldWeaponFloorAmmo(trackedWeapon);
                SetWeaponAmmo(trackedWeapon, currentAmmoFloor);
            }

            if (spiderHealthSystem != null)
            {
                ClipVfx.Cast(trackedWeapon != null ? trackedWeapon.transform.position : spiderHealthSystem.transform.position);
                aura = WeaponAura.Attach(spiderHealthSystem.transform);
                ShowAuraOn(trackedWeapon);
                SurgeNet.SendFx(SurgeFx.Clip, PlayerId, spiderHealthSystem.transform.position, 0f, true);
            }
            UnlimitedPowerAchievement.Cast(this);
        }

        // Peers other than the host and the caster only see the aura; the host tells them when it starts and ends
        public static void ShowRemoteClip(int playerId, Vector3 position, bool on)
        {
            if (!playerAmmoAbilities.TryGetValue(playerId, out AmmoAbility clip) || clip == null) return;
            clip.StopAura();
            clip.showingRemote = on && clip.spiderHealthSystem != null;
            if (!clip.showingRemote) return;
            ClipVfx.Cast(clip.weaponManager != null && clip.weaponManager.equippedWeapon != null ? clip.weaponManager.equippedWeapon.transform.position : position);
            clip.aura = WeaponAura.Attach(clip.spiderHealthSystem.transform);
            clip.ShowAuraOn(clip.weaponManager != null ? clip.weaponManager.equippedWeapon : null);
        }

        // Only the host spawns the weapons, so it tells every client where they land
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

        private float nextShotAccent;

        internal void AccentShot(Weapon weapon)
        {
            if (aura == null || Time.time < nextShotAccent) return;
            nextShotAccent = Time.time + 0.08f;
            Vector3 position = weapon.point != null ? weapon.point.position : weapon.transform.position;
            Vector3 direction = weapon.transform.up;
            float angle = Mathf.Atan2(direction.y, direction.x);
            WeaponVfx.Shot(position, angle);
            SurgeNet.SendFx(SurgeFx.ClipShot, PlayerId, position, angle, false);
        }

        private WeaponAura aura;

        private void ShowAuraOn(Weapon weapon)
        {
            if (aura != null) aura.Retarget(weapon != null ? weapon.transform : null);
        }

        private void StopAura()
        {
            if (aura == null) return;
            aura.Stop();
            aura = null;
        }

        protected override void OnDeactivate()
        {
            StopAura();
            SurgeNet.SendFx(SurgeFx.Clip, PlayerId, Vector3.zero, 0f, false);
            Weapon equippedWeapon = weaponManager?.equippedWeapon;

            if (equippedWeapon != null)
            {
                ResolveWeaponAmmoOnAbilityRemoval(equippedWeapon, false);
            }

            if (trackedWeapon != null && trackedWeapon != equippedWeapon)
            {
                ResolveWeaponAmmoOnAbilityRemoval(trackedWeapon, false);
            }

            trackedWeapon = null;
            trackedOriginalAmmo = 0f;
            lastResolvedWeapon = null;
            lastResolvedFrame = -1;
        }

        public static void HandleWeaponEquipped(SpiderWeaponManager manager, Weapon weapon)
        {
            if (manager == null || weapon == null) return;

            int playerId = Players.IdOf(manager);
            if (playerId < 0) return;

            if (!playerAmmoAbilities.TryGetValue(playerId, out AmmoAbility ability) || ability == null)
                return;

            if (!ability.isActive)
            {
                if (ability.showingRemote) ability.ShowAuraOn(weapon);
                return;
            }

            ability.EnsureTrackedWeapon(weapon);
            ability.ShowAuraOn(weapon);
        }

        public static void HandleWeaponRemoved(SpiderWeaponManager manager, Weapon weapon)
        {
            if (manager == null || weapon == null) return;

            int playerId = Players.IdOf(manager);
            if (playerId < 0) return;

            if (!playerAmmoAbilities.TryGetValue(playerId, out AmmoAbility ability) || ability == null)
            {
                return;
            }

            if (!ability.isActive)
            {
                if (ability.showingRemote) ability.ShowAuraOn(null);
                return;
            }

            ability.ShowAuraOn(null);
            ability.ResolveWeaponAmmoOnAbilityRemoval(weapon, true);
        }

        private void ResolveWeaponAmmoOnAbilityRemoval(Weapon weapon, bool duringActiveUnequip)
        {
            if (weapon == null) return;
            if (weapon == lastResolvedWeapon && Time.frameCount == lastResolvedFrame) return;

            int efficiencyLevel = GetEfficiencyLevel();

            if (efficiencyLevel <= 0)
            {
                lastResolvedWeapon = weapon;
                lastResolvedFrame = Time.frameCount;

                if (IsAuthority && ShouldDisintegrateOnRemoval(weapon))
                {
                    weapon.Disintegrate();
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
                    return SurvivalMode.instance.GetRandomWeapon(false) ?? FallbackWeapon?.Invoke();
                }
            }
            catch (System.Exception ex)
            {
                Logger.LogWarning($"Weapon Arsenal: Failed to get random weapon: {ex.Message}");
            }
            return FallbackWeapon?.Invoke();
        }

        protected override void OnDestroy()
        {
            showingRemote = false;
            StopAura();
            base.OnDestroy();

            if (playerAmmoAbilities.TryGetValue(PlayerId, out var registered) && registered == this)
            {
                playerAmmoAbilities.Remove(PlayerId);
            }
        }
    }
}
