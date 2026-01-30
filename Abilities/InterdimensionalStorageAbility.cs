using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using Interfaces;
using Logger = Silk.Logger;

namespace SpiderSurge
{
    public class InterdimensionalStorageAbility : BaseAbility
    {
        public override string PerkName => "interdimensionalStorageAbility";

        public override float BaseDuration => 1f;
        public override float BaseCooldown => 11f;
        public override float DurationPerPerkLevel => 0f; // Handled custom logic in GetSwapDuration
        public override float CooldownPerPerkLevel => 5f;

        public override bool HasUltimate => true;
        public override string UltimatePerkName => "interdimensionalStorageAbilityUltimate";
        public override string UltimatePerkDisplayName => "Storage Ultimate";
        public override string UltimatePerkDescription => "Adds a second storage slot (3x cooldown).";
        public override float UltimateCooldownMultiplier => 3f;

        private SpiderWeaponManager _weaponManager;

        // Local storage - no network objects
        private Weapon _storedWeapon;
        private Weapon _ultimateStoredWeapon;

        public override float Duration
        {
            get
            {
                return GetSwapDuration(isUltimateActive);
            }
        }

        protected override void Start()
        {
            base.Start();
            _weaponManager = GetComponentInChildren<SpiderWeaponManager>();
            if (_weaponManager == null && playerController != null)
            {
                // Try from the player controller's health system
                _weaponManager = playerController.spiderHealthSystem?.GetComponentInChildren<SpiderWeaponManager>();
            }

            if (_weaponManager == null)
            {
                Logger.LogError($"InterdimensionalStorageAbility: SpiderWeaponManager not found on {name}");
            }

            // Restore weapons on start (local only)
            RestoreWeapons();
        }

        protected override void OnActivate()
        {
            // Direct execution since we are local
            StartCoroutine(SwapRoutine(false));
        }

        protected override void OnActivateUltimate()
        {
            StartCoroutine(SwapRoutine(true));
        }

        private IEnumerator SwapRoutine(bool useUltimateStorage)
        {
            float duration = GetSwapDuration(useUltimateStorage);

            Logger.LogInfo($"InterdimensionalStorageAbility: Swapping weapon (Ultimate: {useUltimateStorage}). Duration: {duration}s");

            yield return new WaitForSeconds(duration);

            PerformSwap(useUltimateStorage);
        }

        private void PerformSwap(bool useUltimateStorage)
        {
            if (_weaponManager == null) return;

            // Determine which storage slot to use
            ref Weapon targetStorage = ref _storedWeapon;
            if (useUltimateStorage)
            {
                targetStorage = ref _ultimateStoredWeapon;
            }

            GameObject heldWeaponObj = _weaponManager.equippedWeapon ? _weaponManager.equippedWeapon.gameObject : null;
            GameObject storedWeaponObj = targetStorage ? targetStorage.gameObject : null;

            // 1. Handle current weapon (Store it)
            if (heldWeaponObj != null)
            {
                _weaponManager.UnEquipWeapon();
                // Disable and parent to us
                heldWeaponObj.SetActive(false);
                heldWeaponObj.transform.SetParent(transform);
                heldWeaponObj.transform.localPosition = Vector3.zero;
            }

            // 2. Handle stored weapon (Retrieve it)
            if (storedWeaponObj != null)
            {
                // Force position to player to prevent it from flying across the map
                storedWeaponObj.transform.position = _weaponManager.transform.position;

                storedWeaponObj.SetActive(true);
                storedWeaponObj.transform.SetParent(null); // Detach from player so it can move freely

                Weapon weapon = storedWeaponObj.GetComponent<Weapon>();
                if (weapon != null)
                {
                    if (weapon.rb2D != null)
                    {
                        weapon.rb2D.velocity = Vector2.zero;
                        weapon.rb2D.angularVelocity = 0f;
                    }

                    // Use Weapon.Equip to set owner/layer
                    weapon.Equip(_weaponManager);
                    // Manually call OnEquipWeapon to setup joints and manager state (since EquipWeapon is protected)
                    _weaponManager.OnEquipWeapon(weapon);
                }
            }

            // 3. Update storage reference
            // If we held something, it goes into storage. If not, storage becomes empty (or stays empty)
            if (heldWeaponObj != null)
            {
                targetStorage = heldWeaponObj.GetComponent<Weapon>();
            }
            else
            {
                targetStorage = null;
            }
        }

        public void OnCharacterDied()
        {
            if (_playerDied) return;
            // Local check
            _playerDied = true;
            SaveWeapons(isDeath: true);
        }

        protected override void OnDestroy()
        {
            if (!_playerDied)
            {
                // If we haven't died, assume map change or forced cleanup
                SaveWeapons(isDeath: false);
            }
            base.OnDestroy();
        }

        private bool _playerDied = false;

        // Local storage moved to PerksManager


        private void SaveWeapons(bool isDeath)
        {
            // Only save if we have the synergy perk
            if (PerksManager.Instance == null || ModifierManager.instance == null || PerksManager.Instance.GetPerkLevel("synergy") <= 0) return;

            // Use PlayerIndex or something local for ID
            int playerId = playerInput != null ? playerInput.playerIndex : -1;
            if (playerId == -1) return;

            List<PerksManager.SavedWeaponData> dataList = new List<PerksManager.SavedWeaponData>();

            // Check Slot 1
            SaveSlot(_storedWeapon, false, isDeath, dataList);

            // Check Slot 2
            if (HasUltimate)
            {
                SaveSlot(_ultimateStoredWeapon, true, isDeath, dataList);
            }

            // Save to PerksManager (singleton)
            PerksManager.Instance.SaveStoredWeapons(playerId, dataList);
        }

        private void SaveSlot(Weapon weapon, bool isUltimateSlot, bool isDeath, List<PerksManager.SavedWeaponData> dataList)
        {
            if (weapon == null) return;

            if (ShouldKeepWeapon(weapon, isDeath))
            {
                dataList.Add(new PerksManager.SavedWeaponData
                {
                    WeaponName = weapon.serializationWeaponName,
                    Ammo = weapon.ammo,
                    IsUltimateSlot = isUltimateSlot
                });
            }
        }

        private bool ShouldKeepWeapon(Weapon weapon, bool isDeath)
        {
            if (weapon.type == null || weapon.type.Count == 0) return false;
            if (ModifierManager.instance == null) return false;

            Weapon.WeaponType wType = weapon.type[0];
            int modLevel = 0;

            if (wType == Weapon.WeaponType.Gun)
                modLevel = ModifierManager.instance.GetModLevel("moreGuns");
            else if (wType == Weapon.WeaponType.Explosive)
                modLevel = ModifierManager.instance.GetModLevel("moreBoom");
            else if (wType == Weapon.WeaponType.Particle)
                modLevel = ModifierManager.instance.GetModLevel("moreParticles");

            if (isDeath)
                return modLevel >= 2;
            else
                return modLevel >= 1;
        }

        private void RestoreWeapons()
        {
            if (PerksManager.Instance == null) return;

            int playerId = playerInput != null ? playerInput.playerIndex : -1;
            List<PerksManager.SavedWeaponData> dataList = PerksManager.Instance.GetStoredWeapons(playerId);

            if (dataList == null || dataList.Count == 0) return;

            Logger.LogInfo($"[Storage] Restoring {dataList.Count} weapons for player {playerId}");

            // Gather all spawnable weapons to find prefabs
            List<SpawnableWeapon> availableWeapons = new List<SpawnableWeapon>();
            if (VersusMode.instance != null) availableWeapons.AddRange(VersusMode.instance.weapons);
            if (SurvivalMode.instance != null)
            {
                var field = typeof(SurvivalMode).GetField("_weapons", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                if (field != null)
                {
                    var survivalWeapons = (List<SpawnableWeapon>)field.GetValue(SurvivalMode.instance);
                    if (survivalWeapons != null)
                    {
                        availableWeapons.AddRange(survivalWeapons);
                    }
                }
            }

            foreach (var data in dataList)
            {
                GameObject prefab = null;
                foreach (var sw in availableWeapons)
                {
                    if (sw.weaponObject == null) continue;
                    Weapon w = sw.weaponObject.GetComponent<Weapon>();
                    if (w != null && w.serializationWeaponName == data.WeaponName)
                    {
                        prefab = sw.weaponObject;
                        break;
                    }
                }

                if (prefab != null)
                {
                    GameObject newWeaponObj = Instantiate(prefab, Vector3.zero, Quaternion.identity);

                    if (NetworkManager.Singleton != null && (NetworkManager.Singleton.IsServer || NetworkManager.Singleton.IsHost))
                    {
                        var no = newWeaponObj.GetComponent<NetworkObject>();
                        if (no != null) no.Spawn(true);
                    }

                    Weapon newWeapon = newWeaponObj.GetComponent<Weapon>();
                    if (newWeapon != null)
                    {
                        newWeapon.ammo = data.Ammo;
                    }

                    // Put into storage
                    newWeaponObj.SetActive(false);
                    newWeaponObj.transform.SetParent(transform);
                    newWeaponObj.transform.localPosition = Vector3.zero;

                    if (data.IsUltimateSlot)
                        _ultimateStoredWeapon = newWeapon;
                    else
                        _storedWeapon = newWeapon;
                }
                else
                {
                    Logger.LogWarning($"[Storage] Could not find prefab for saved weapon {data.WeaponName}");
                }
            }

            // Clear after restoring
            PerksManager.Instance.ClearStoredWeapons(playerId);
        }

        private float GetSwapDuration(bool useUltimate)
        {
            float duration = BaseDuration;

            // Apply "Ability Duration" perk (reduction)
            int durationLevel = PerksManager.Instance?.GetPerkLevel("abilityDuration") ?? 0;
            if (durationLevel > 0)
            {
                duration *= Mathf.Pow(0.8f, durationLevel);
            }

            // Synergy Logic
            Weapon interestingWeapon = GetRelevantWeaponForSynergy(useUltimate);

            if (interestingWeapon != null && interestingWeapon.type.Count > 0)
            {
                Weapon.WeaponType wType = interestingWeapon.type[0];

                if (wType == Weapon.WeaponType.Gun)
                {
                    int gunsLevel = ModifierManager.instance?.GetModLevel("moreGuns") ?? 0;
                    if (gunsLevel > 0) duration *= Mathf.Pow(0.9f, gunsLevel);
                }
                else if (wType == Weapon.WeaponType.Explosive)
                {
                    int boomLevel = ModifierManager.instance?.GetModLevel("moreBoom") ?? 0;
                    if (boomLevel > 0) duration *= Mathf.Pow(0.9f, boomLevel);
                }
                else if (wType == Weapon.WeaponType.Particle)
                {
                    int particleLevel = ModifierManager.instance?.GetModLevel("moreParticles") ?? 0;
                    if (particleLevel > 0) duration *= Mathf.Pow(0.9f, particleLevel);
                }
            }
            return Mathf.Max(0.1f, duration);
        }

        private Weapon GetRelevantWeaponForSynergy(bool ultimate)
        {
            // If we have a stored weapon, that's the one we are "retrieving", so its speed should benefit.
            Weapon targetStorage = ultimate ? _ultimateStoredWeapon : _storedWeapon;

            if (targetStorage != null)
            {
                return targetStorage;
            }

            // If nothing stored, we are storing the held weapon
            return _weaponManager != null ? _weaponManager.equippedWeapon : null;
        }
        public static InterdimensionalStorageAbility GetByHealthSystem(SpiderHealthSystem healthSystem)
        {
            if (healthSystem == null) return null;
            return healthSystem.GetComponentInParent<InterdimensionalStorageAbility>();
        }
    }
}
