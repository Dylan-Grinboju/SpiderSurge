using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using Logger = Silk.Logger;

namespace SpiderSurge;

public class StorageAbility : BaseAbility
{
    public override string PerkName => Consts.PerkNames.StorageAbility;

    public override float AbilityBaseDuration => Consts.Values.Storage.AbilityBaseDuration;
    public override float AbilityBaseCooldown => Consts.Values.Storage.AbilityBaseCooldown;
    public override float UltimateBaseDuration => Consts.Values.Storage.UltimateBaseDuration;
    public override float UltimateBaseCooldown => Consts.Values.Storage.UltimateBaseCooldown;
    public override float AbilityCooldownPerPerkLevel => Consts.Values.Storage.AbilityCooldownReductionPerLevel;
    public override float UltimateCooldownPerPerkLevel => Consts.Values.Storage.UltimateCooldownReductionPerLevel;

    public override bool HasUltimate => true;
    public override string UltimatePerkName => Consts.PerkNames.StorageAbilityUltimate;
    public override string UltimatePerkDisplayName => "Pocket Dimension^2";
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
        if (_weaponManager is null && playerController is not null)
        {
            // Try from the player controller's health system
            _weaponManager = playerController.spiderHealthSystem?.GetComponentInChildren<SpiderWeaponManager>();
        }

        if (_weaponManager is null)
        {
            Logger.LogError($"StorageAbility: SpiderWeaponManager not found on {name}");
        }

        // Restore weapons on start (local only)
        RestoreWeapons();
        UpdateCachedModifierLevels();
    }

    protected override void OnActivate() => StartCoroutine(SwapRoutine(false));

    protected override void OnActivateUltimate() => StartCoroutine(SwapRoutine(true));

    private IEnumerator SwapRoutine(bool useUltimateStorage)
    {
        float duration = useUltimateStorage ? UltimateDuration : AbilityDuration;

        if (_weaponManager is null) yield break;

        RuntimeStoredWeapon currentSlotData = useUltimateStorage ? _ultimateStoredWeaponData : _storedWeaponData;
        GameObject storedWeaponObj = currentSlotData?.WeaponRef is not null ? currentSlotData.WeaponRef.gameObject : null;
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
        if (heldWeaponObj is null) return null;

        Weapon val = heldWeaponObj.GetComponent<Weapon>();
        if (val is null) return null;

        _weaponManager.UnEquipWeapon();

        heldWeaponObj.transform.SetParent(transform);
        heldWeaponObj.transform.localPosition = Vector3.zero;
        heldWeaponObj.SetActive(false);

        SoundManager.Instance?.PlaySound(
                Consts.SoundNames.StorageSend,
                Consts.SoundVolumes.StorageSend * Consts.SoundVolumes.MasterVolume
            );

        return new RuntimeStoredWeapon
        {
            WeaponRef = val,
            Name = val.serializationWeaponName,
            Ammo = val.ammo,
            Types = [.. val.type]
        };
    }

    private void RetrieveWeapon(GameObject storedWeaponObj)
    {
        if (storedWeaponObj is null) return;

        // Drop any weapon the player picked up during the delay
        if (_weaponManager is not null && _weaponManager.equippedWeapon is not null)
        {
            _weaponManager.UnEquipWeapon();
        }

        // Spawn at a random position around the player for cool effect
        float angle = Random.Range(0f, Mathf.PI * 2);
        Vector3 offset = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * Consts.Values.Storage.SpawnDistance;
        storedWeaponObj.transform.position = _weaponManager.transform.position + offset;

        storedWeaponObj.SetActive(true);
        storedWeaponObj.transform.SetParent(null); // Detach from player so it can move freely

        Weapon weapon = storedWeaponObj.GetComponent<Weapon>();
        if (weapon is not null)
        {
            if (weapon.rb2D is not null)
            {
                weapon.rb2D.velocity = Vector2.zero;
                weapon.rb2D.angularVelocity = 0f;
            }

            weapon.Equip(_weaponManager);
            _weaponManager.OnEquipWeapon(weapon);
        }

        SoundManager.Instance?.PlaySound(
                Consts.SoundNames.StorageRetrieve,
                Consts.SoundVolumes.StorageRetrieve * Consts.SoundVolumes.MasterVolume
            );
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
        if (PerksManager.Instance is null) return;

        int playerId = playerInput is not null ? playerInput.playerIndex : -1;
        if (playerId == -1) return;

        List<StoragePersistenceManager.SavedWeaponData> dataList = [];

        SaveSlot(_storedWeaponData, false, isDeath, dataList);

        if (HasUltimate)
        {
            SaveSlot(_ultimateStoredWeaponData, true, isDeath, dataList);
        }
        StoragePersistenceManager.SaveStoredWeapons(playerId, dataList);
    }

    private void SaveSlot(RuntimeStoredWeapon weaponData, bool isUltimateSlot, bool isDeath, List<StoragePersistenceManager.SavedWeaponData> dataList)
    {
        if (weaponData is null) return;

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
        if (ModifierManager.instance is not null)
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

        if (effectiveTypes is null || effectiveTypes.Count == 0) return false;
        int requiredModLevel = GetRequiredModLevel(effectiveTypes);

        return isDeath ? requiredModLevel >= 2 : requiredModLevel >= 1;
    }

    private int GetRequiredModLevel(List<Weapon.WeaponType> types)
    {
        bool isBoom = false;
        bool isParticles = false;
        bool isGuns = false;

        foreach (var wType in types)
        {
            if (wType is Weapon.WeaponType.Explosive or Weapon.WeaponType.Throwable or Weapon.WeaponType.Mine)
                isBoom = true;
            else if (wType is Weapon.WeaponType.Particle or Weapon.WeaponType.Melee)
                isParticles = true;
            else if (wType == Weapon.WeaponType.Gun)
                isGuns = true;
        }

        if (isBoom) return _cachedMoreBoomLevel;
        return isParticles ? _cachedMoreParticlesLevel : isGuns ? _cachedMoreGunsLevel : 0;
    }

    private void RestoreWeapons()
    {
        if (PerksManager.Instance is null) return;

        int playerId = playerInput is not null ? playerInput.playerIndex : -1;
        List<StoragePersistenceManager.SavedWeaponData> dataList = StoragePersistenceManager.GetStoredWeapons(playerId);

        if (dataList is null || dataList.Count == 0) return;

        Dictionary<string, GameObject> weaponPrefabMap = [];

        void AddWeaponsToMap(List<SpawnableWeapon> weapons)
        {
            if (weapons is null) return;
            foreach (var sw in weapons)
            {
                if (sw is null || sw.weaponObject is null) continue;
                Weapon w = sw.weaponObject.GetComponent<Weapon>();
                if (w is not null && !weaponPrefabMap.ContainsKey(w.serializationWeaponName.ToString()))
                {
                    weaponPrefabMap[w.serializationWeaponName.ToString()] = sw.weaponObject;
                }
            }
        }

        if (VersusMode.instance is not null)
        {
            AddWeaponsToMap(VersusMode.instance.weapons);
        }

        if (SurvivalMode.instance is not null)
        {
            // Use ReflectionHelper to get private fields safely
            var survivalWeapons = ReflectionHelper.GetPrivateField<List<SpawnableWeapon>>(SurvivalMode.instance, "_weapons");
            if (survivalWeapons is not null)
            {
                AddWeaponsToMap(survivalWeapons);
            }
            else
            {
                Logger.LogWarning("[StorageAbility] Could not retrieve _weapons from SurvivalMode instance.");
            }
        }

        foreach (var data in dataList)
        {
            if (weaponPrefabMap.TryGetValue(data.WeaponName.ToString(), out GameObject prefab))
            {
                GameObject newWeaponObj = Instantiate(prefab, Vector3.zero, Quaternion.identity);

                if (NetworkManager.Singleton is not null && (NetworkManager.Singleton.IsServer || NetworkManager.Singleton.IsHost))
                {
                    var no = newWeaponObj.GetComponent<NetworkObject>();
                    no?.Spawn(true);
                }

                Weapon newWeapon = newWeaponObj.GetComponent<Weapon>();
                newWeapon?.ammo = data.Ammo;

                newWeaponObj.transform.SetParent(transform);
                newWeaponObj.transform.localPosition = Vector3.zero;
                newWeaponObj.SetActive(false);

                if (data.IsUltimateSlot)
                    _ultimateStoredWeaponData = new RuntimeStoredWeapon
                    {
                        WeaponRef = newWeapon,
                        Name = newWeapon.serializationWeaponName,
                        Ammo = newWeapon.ammo,
                        Types = [.. newWeapon.type]
                    };
                else
                    _storedWeaponData = new RuntimeStoredWeapon
                    {
                        WeaponRef = newWeapon,
                        Name = newWeapon.serializationWeaponName,
                        Ammo = newWeapon.ammo,
                        Types = [.. newWeapon.type]
                    };
            }
            else
            {
                Logger.LogWarning($"[Storage] Could not find prefab for saved weapon {data.WeaponName}");
            }
        }

        StoragePersistenceManager.ClearStoredWeapons(playerId);
    }

    public static StorageAbility GetByHealthSystem(SpiderHealthSystem healthSystem) => healthSystem?.GetComponentInParent<StorageAbility>();

    private List<Weapon.WeaponType> GetEffectiveWeaponTypes(SerializationWeaponName name)
    {
        string nameStr = name.ToString();
        List<Weapon.WeaponType> types = [];
        // These weapons are the only ones with no WeaponType, all the missing ones are also a Gun
        if (nameStr is "HeckSaw" or "ParticleBladeLauncher" or "GravitySaw")
        {
            types.Add(Weapon.WeaponType.Melee);
            return types;
        }
        types.Add(Weapon.WeaponType.Gun);
        return types;
    }
}
