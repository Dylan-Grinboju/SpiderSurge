using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using Logger = Silk.Logger;

namespace SpiderSurge
{
    public class InterdimensionalStorageAbility : BaseAbility
    {
        public override string PerkName => Consts.PerkNames.InterdimensionalStorageAbility;

        public override float BaseDuration => Consts.Values.Storage.BaseDuration;
        public override float BaseCooldown => Consts.Values.Storage.BaseCooldown;
        public override float DurationPerPerkLevel => 0f; // Handled custom logic in GetSwapDuration
        public override float CooldownPerPerkLevel => Consts.Values.Storage.CooldownReductionPerLevel;

        public override bool HasUltimate => true;
        public override string UltimatePerkName => Consts.PerkNames.InterdimensionalStorageAbilityUltimate;
        public override string UltimatePerkDisplayName => "Storage Ultimate";
        public override string UltimatePerkDescription => "Adds a second storage slot (3x cooldown).";
        public override float UltimateCooldownMultiplier => Consts.Values.Storage.UltimateCooldownMultiplier;

        private SpiderWeaponManager _weaponManager;

        // Local storage - no network objects
        // Runtime storage data - safe from scene destruction race conditions
        private class RuntimeStoredWeapon
        {
            public Weapon WeaponRef; // Live reference (can be null/destroyed)
            public SerializationWeaponName Name;
            public float Ammo;
            public List<Weapon.WeaponType> Types;
        }

        private RuntimeStoredWeapon _storedWeaponData;
        private RuntimeStoredWeapon _ultimateStoredWeaponData;

        public override float Duration
        {
            get
            {
                return GetSwapDuration();
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
            UpdateCachedModifierLevels();
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
            float duration = GetSwapDuration();

            yield return new WaitForSeconds(duration);

            PerformSwap(useUltimateStorage);
        }

        private void PerformSwap(bool useUltimateStorage)
        {
            if (_weaponManager == null) return;

            // Determine which storage slot to use
            RuntimeStoredWeapon currentSlotData = useUltimateStorage ? _ultimateStoredWeaponData : _storedWeaponData;

            GameObject heldWeaponObj = _weaponManager.equippedWeapon ? _weaponManager.equippedWeapon.gameObject : null;
            GameObject storedWeaponObj = currentSlotData?.WeaponRef != null ? currentSlotData.WeaponRef.gameObject : null;

            // 1. Handle current weapon (Store it)
            RuntimeStoredWeapon newStoredData = StoreWeapon(heldWeaponObj);

            // 2. Handle stored weapon (Retrieve it)
            RetrieveWeapon(storedWeaponObj);

            // 3. Update storage reference
            if (useUltimateStorage)
                _ultimateStoredWeaponData = newStoredData;
            else
                _storedWeaponData = newStoredData;
        }

        private RuntimeStoredWeapon StoreWeapon(GameObject heldWeaponObj)
        {
            if (heldWeaponObj == null) return null;

            Weapon val = heldWeaponObj.GetComponent<Weapon>();
            if (val == null) return null;

            _weaponManager.UnEquipWeapon();

            // Disable and parent to us
            heldWeaponObj.SetActive(false);
            heldWeaponObj.transform.SetParent(transform);
            heldWeaponObj.transform.localPosition = Vector3.zero;

            // Create data object
            return new RuntimeStoredWeapon
            {
                WeaponRef = val,
                Name = val.serializationWeaponName,
                Ammo = val.ammo,
                Types = new List<Weapon.WeaponType>(val.type)
            };
        }

        private void RetrieveWeapon(GameObject storedWeaponObj)
        {
            if (storedWeaponObj == null) return;

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
            // We don't check ModifierManager.instance here because we might be relying on cached values during scene transition
            if (PerksManager.Instance == null || PerksManager.Instance.GetPerkLevel(Consts.PerkNames.Synergy) <= 0) return;

            // Use PlayerIndex or something local for ID
            int playerId = playerInput != null ? playerInput.playerIndex : -1;
            if (playerId == -1) return;

            List<StoragePersistenceManager.SavedWeaponData> dataList = new List<StoragePersistenceManager.SavedWeaponData>();

            // Check Slot 1
            SaveSlot(_storedWeaponData, false, isDeath, dataList);

            // Check Slot 2
            if (HasUltimate)
            {
                SaveSlot(_ultimateStoredWeaponData, true, isDeath, dataList);
            }

            // Save to StoragePersistenceManager
            StoragePersistenceManager.SaveStoredWeapons(playerId, dataList);
        }

        private void SaveSlot(RuntimeStoredWeapon weaponData, bool isUltimateSlot, bool isDeath, List<StoragePersistenceManager.SavedWeaponData> dataList)
        {
            if (weaponData == null) return;

            if (ShouldKeepWeapon(weaponData, isDeath))
            {
                dataList.Add(new StoragePersistenceManager.SavedWeaponData
                {
                    WeaponName = weaponData.Name,
                    Ammo = weaponData.Ammo,
                    IsUltimateSlot = isUltimateSlot
                });
            }
        }


        // Cached modifier levels for safety during destruction/scene load
        private int _cachedMoreGunsLevel = 0;
        private int _cachedMoreBoomLevel = 0;
        private int _cachedMoreParticlesLevel = 0;


        public void UpdateCachedModifierLevels()
        {
            if (ModifierManager.instance != null)
            {
                _cachedMoreGunsLevel = ModifierManager.instance.GetModLevel(Consts.ModifierNames.MoreGuns);
                _cachedMoreBoomLevel = ModifierManager.instance.GetModLevel(Consts.ModifierNames.MoreBoom);
                _cachedMoreParticlesLevel = ModifierManager.instance.GetModLevel(Consts.ModifierNames.MoreParticles);
            }
        }

        private bool ShouldKeepWeapon(RuntimeStoredWeapon weapon, bool isDeath)
        {
            List<Weapon.WeaponType> effectiveTypes = GetEffectiveWeaponTypes(weapon.Name, weapon.Types);

            if (effectiveTypes == null || effectiveTypes.Count == 0) return false;

            int maxModLevel = 0;

            foreach (var wType in effectiveTypes)
            {
                if (wType == Weapon.WeaponType.Gun)
                    maxModLevel = Mathf.Max(maxModLevel, _cachedMoreGunsLevel);
                else if (wType == Weapon.WeaponType.Explosive || wType == Weapon.WeaponType.Throwable || wType == Weapon.WeaponType.Mine)
                    maxModLevel = Mathf.Max(maxModLevel, _cachedMoreBoomLevel);
                else if (wType == Weapon.WeaponType.Particle || wType == Weapon.WeaponType.Melee)
                    maxModLevel = Mathf.Max(maxModLevel, _cachedMoreParticlesLevel);
            }

            if (isDeath)
                return maxModLevel >= 2;
            else
                return maxModLevel >= 1;
        }

        private void RestoreWeapons()
        {
            if (PerksManager.Instance == null) return;

            int playerId = playerInput != null ? playerInput.playerIndex : -1;
            List<StoragePersistenceManager.SavedWeaponData> dataList = StoragePersistenceManager.GetStoredWeapons(playerId);

            if (dataList == null || dataList.Count == 0) return;

            // Gather all spawnable weapons into a dictionary for O(1) lookup
            Dictionary<string, GameObject> weaponPrefabMap = new Dictionary<string, GameObject>();

            // Helper to populate map
            void AddWeaponsToMap(List<SpawnableWeapon> weapons)
            {
                if (weapons == null) return;
                foreach (var sw in weapons)
                {
                    if (sw == null || sw.weaponObject == null) continue;
                    Weapon w = sw.weaponObject.GetComponent<Weapon>();
                    if (w != null && !weaponPrefabMap.ContainsKey(w.serializationWeaponName.ToString()))
                    {
                        weaponPrefabMap[w.serializationWeaponName.ToString()] = sw.weaponObject;
                    }
                }
            }

            if (VersusMode.instance != null)
            {
                AddWeaponsToMap(VersusMode.instance.weapons);
            }

            if (SurvivalMode.instance != null)
            {
                // Use ReflectionHelper to get private fields safely
                var survivalWeapons = ReflectionHelper.GetPrivateField<List<SpawnableWeapon>>(SurvivalMode.instance, "_weapons");
                if (survivalWeapons != null)
                {
                    AddWeaponsToMap(survivalWeapons);
                }
                else
                {
                    Logger.LogWarning("[InterdimensionalStorageAbility] Could not retrieve _weapons from SurvivalMode instance.");
                }
            }

            foreach (var data in dataList)
            {
                if (weaponPrefabMap.TryGetValue(data.WeaponName.ToString(), out GameObject prefab))
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
                        _ultimateStoredWeaponData = new RuntimeStoredWeapon
                        {
                            WeaponRef = newWeapon,
                            Name = newWeapon.serializationWeaponName,
                            Ammo = newWeapon.ammo,
                            Types = newWeapon.type
                        };
                    else
                        _storedWeaponData = new RuntimeStoredWeapon
                        {
                            WeaponRef = newWeapon,
                            Name = newWeapon.serializationWeaponName,
                            Ammo = newWeapon.ammo,
                            Types = newWeapon.type
                        };
                }
                else
                {
                    Logger.LogWarning($"[Storage] Could not find prefab for saved weapon {data.WeaponName}");
                }
            }

            // Clear after restoring
            StoragePersistenceManager.ClearStoredWeapons(playerId);
        }

        private float GetSwapDuration()
        {
            float duration = BaseDuration;

            // Apply "Ability Duration" perk (reduction)
            int durationLevel = PerksManager.Instance?.GetPerkLevel(Consts.PerkNames.AbilityDuration) ?? 0;
            if (durationLevel > 0)
            {
                duration *= Mathf.Pow(Consts.Values.Storage.PerkDurationMultiplier, durationLevel);
            }

            return Mathf.Max(0.1f, duration);
        }
        public static InterdimensionalStorageAbility GetByHealthSystem(SpiderHealthSystem healthSystem)
        {
            if (healthSystem == null) return null;
            return healthSystem.GetComponentInParent<InterdimensionalStorageAbility>();
        }

        private List<Weapon.WeaponType> GetEffectiveWeaponTypes(SerializationWeaponName name, List<Weapon.WeaponType> existingTypes)
        {
            string nameStr = name.ToString();
            List<Weapon.WeaponType> types = new List<Weapon.WeaponType>();

            switch (nameStr)
            {
                case "HeckSaw":
                case "ParticleBladeLauncher":
                case "GravitySaw":
                    types.Add(Weapon.WeaponType.Gun);
                    types.Add(Weapon.WeaponType.Melee);
                    return types;
                case "LaserCannon":
                case "DeathRay":
                    types.Add(Weapon.WeaponType.Gun);
                    return types;
            }

            if (existingTypes != null) return existingTypes;
            return types;
        }
    }
}
