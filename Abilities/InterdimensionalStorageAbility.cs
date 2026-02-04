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

        public override float AbilityBaseDuration => Consts.Values.Storage.AbilityBaseDuration;
        public override float AbilityBaseCooldown => Consts.Values.Storage.AbilityBaseCooldown;
        public override float UltimateBaseDuration => Consts.Values.Storage.UltimateBaseDuration;
        public override float UltimateBaseCooldown => Consts.Values.Storage.UltimateBaseCooldown;
        public override float AbilityCooldownPerPerkLevel => Consts.Values.Storage.AbilityCooldownReductionPerLevel;
        public override float UltimateCooldownPerPerkLevel => Consts.Values.Storage.UltimateCooldownReductionPerLevel;

        public override bool HasUltimate => true;
        public override string UltimatePerkName => Consts.PerkNames.InterdimensionalStorageAbilityUltimate;
        public override string UltimatePerkDisplayName => "Storage Ultimate";
        public override string UltimatePerkDescription => "Adds a second storage slot (3x cooldown).";

        public override float AbilityDuration
        {
            get
            {
                int durationLevel = PerksManager.Instance?.GetPerkLevel(Consts.PerkNames.AbilityDuration) ?? 0;
                int shortTermLevel = PerksManager.Instance?.GetPerkLevel(Consts.PerkNames.ShortTermInvestment) ?? 0;
                int longTermLevel = PerksManager.Instance?.GetPerkLevel(Consts.PerkNames.LongTermInvestment) ?? 0;

                float duration = AbilityBaseDuration;
                if (durationLevel >= 1) duration -= Consts.Values.Storage.AbilityDurationReductionPerLevel;
                if (shortTermLevel > 0) duration -= Consts.Values.Storage.AbilityDurationReductionPerLevel;
                if (longTermLevel > 0) duration += Consts.Values.Storage.AbilityDurationReductionPerLevel;

                return duration;
            }
        }

        public override float UltimateDuration
        {
            get
            {
                int durationLevel = PerksManager.Instance?.GetPerkLevel(Consts.PerkNames.AbilityDuration) ?? 0;
                int shortTermLevel = PerksManager.Instance?.GetPerkLevel(Consts.PerkNames.ShortTermInvestment) ?? 0;
                int longTermLevel = PerksManager.Instance?.GetPerkLevel(Consts.PerkNames.LongTermInvestment) ?? 0;

                float duration = UltimateBaseDuration;
                if (durationLevel == 2) duration -= Consts.Values.Storage.UltimateDurationReductionPerLevel;
                if (shortTermLevel > 0) duration += Consts.Values.Storage.UltimateDurationReductionPerLevel;
                if (longTermLevel > 0) duration -= Consts.Values.Storage.UltimateDurationReductionPerLevel;

                return duration;
            }
        }
        private int _cachedMoreGunsLevel = 0;
        private int _cachedMoreBoomLevel = 0;
        private int _cachedMoreParticlesLevel = 0;
        private SpiderWeaponManager _weaponManager;

        private class RuntimeStoredWeapon
        {
            public Weapon WeaponRef; // Live reference (can be null/destroyed)
            public SerializationWeaponName Name;
            public float Ammo;
            public List<Weapon.WeaponType> Types;
        }

        private RuntimeStoredWeapon _storedWeaponData;
        private RuntimeStoredWeapon _ultimateStoredWeaponData;

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
            StartCoroutine(SwapRoutine(false));
        }

        protected override void OnActivateUltimate()
        {
            StartCoroutine(SwapRoutine(true));
        }

        private IEnumerator SwapRoutine(bool useUltimateStorage)
        {
            float duration = useUltimateStorage ? UltimateDuration : AbilityDuration;

            if (_weaponManager == null) yield break;

            RuntimeStoredWeapon currentSlotData = useUltimateStorage ? _ultimateStoredWeaponData : _storedWeaponData;
            GameObject storedWeaponObj = currentSlotData?.WeaponRef != null ? currentSlotData.WeaponRef.gameObject : null;
            GameObject heldWeaponObj = _weaponManager.equippedWeapon ? _weaponManager.equippedWeapon.gameObject : null;

            RuntimeStoredWeapon newStoredData = StoreWeapon(heldWeaponObj);

            if (useUltimateStorage)
                _ultimateStoredWeaponData = newStoredData;
            else
                _storedWeaponData = newStoredData;

            yield return new WaitForSeconds(duration);

            RetrieveWeapon(storedWeaponObj);
        }

        private RuntimeStoredWeapon StoreWeapon(GameObject heldWeaponObj)
        {
            if (heldWeaponObj == null) return null;

            Weapon val = heldWeaponObj.GetComponent<Weapon>();
            if (val == null) return null;

            _weaponManager.UnEquipWeapon();

            heldWeaponObj.transform.SetParent(transform);
            heldWeaponObj.transform.localPosition = Vector3.zero;
            heldWeaponObj.SetActive(false);

            if (SoundManager.Instance != null)
            {
                SoundManager.Instance.PlaySound(
                    Consts.SoundNames.StorageSend,
                    Consts.SoundVolumes.StorageSend * Consts.SoundVolumes.MasterVolume
                );
            }

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

            // Spawn at a random position around the player for cool effect
            float angle = Random.Range(0f, Mathf.PI * 2);
            Vector3 offset = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * Consts.Values.Storage.SpawnDistance;
            storedWeaponObj.transform.position = _weaponManager.transform.position + offset;

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

                weapon.Equip(_weaponManager);
                _weaponManager.OnEquipWeapon(weapon);
            }

            if (SoundManager.Instance != null)
            {
                SoundManager.Instance.PlaySound(
                    Consts.SoundNames.StorageRetrieve,
                    Consts.SoundVolumes.StorageRetrieve * Consts.SoundVolumes.MasterVolume
                );
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

        private void SaveWeapons(bool isDeath)
        {
            if (PerksManager.Instance == null) return;

            int playerId = playerInput != null ? playerInput.playerIndex : -1;
            if (playerId == -1) return;

            List<StoragePersistenceManager.SavedWeaponData> dataList = new List<StoragePersistenceManager.SavedWeaponData>();

            SaveSlot(_storedWeaponData, false, isDeath, dataList);

            if (HasUltimate)
            {
                SaveSlot(_ultimateStoredWeaponData, true, isDeath, dataList);
            }
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
            UpdateCachedModifierLevels();
            List<Weapon.WeaponType> effectiveTypes = weapon.Types.Count != 0 ? weapon.Types : GetEffectiveWeaponTypes(weapon.Name);

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

            Dictionary<string, GameObject> weaponPrefabMap = new Dictionary<string, GameObject>();

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

                    newWeaponObj.transform.SetParent(transform);
                    newWeaponObj.transform.localPosition = Vector3.zero;
                    newWeaponObj.SetActive(false);

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

            StoragePersistenceManager.ClearStoredWeapons(playerId);
        }

        public static InterdimensionalStorageAbility GetByHealthSystem(SpiderHealthSystem healthSystem)
        {
            if (healthSystem == null) return null;
            return healthSystem.GetComponentInParent<InterdimensionalStorageAbility>();
        }

        private List<Weapon.WeaponType> GetEffectiveWeaponTypes(SerializationWeaponName name)
        {
            string nameStr = name.ToString();
            List<Weapon.WeaponType> types = new List<Weapon.WeaponType>();
            // These weapons are the only ones with no WeaponType, all the missing ones are also a Gun
            if (nameStr == "HeckSaw" || nameStr == "ParticleBladeLauncher" || nameStr == "GravitySaw")
            {
                types.Add(Weapon.WeaponType.Melee);
                return types;
            }
            types.Add(Weapon.WeaponType.Gun);
            return types;
        }
    }
}
