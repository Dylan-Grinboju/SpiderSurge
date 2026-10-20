using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using Unity.Netcode;
using SpiderSurge.Common;
using SpiderSurge.Vfx;
using Logger = Silk.Logger;

namespace SpiderSurge
{
    public class StorageAbility : BaseAbility
    {
        public override string PerkName => Consts.PerkNames.StorageAbility;
        public override AbilityKind Kind => AbilityKind.Storage;

        public override float AbilityBaseDuration => Consts.Values.Storage.AbilityBaseDuration;
        public override float AbilityBaseCooldown => Consts.Values.Storage.AbilityBaseCooldown;
        public override float UltimateBaseDuration => 0f;
        public override float UltimateBaseCooldown => Consts.Values.Storage.UltimateBaseCooldown;
        public override float AbilityCooldownPerPerkLevel => Consts.Values.Storage.AbilityCooldownReductionPerLevel;
        public override float UltimateCooldownPerPerkLevel => Consts.Values.Storage.UltimateCooldownReductionPerLevel;

        public override bool HasUltimate => true;
        public override string UltimatePerkName => Consts.PerkNames.StorageAbilityUltimate;
        public override string UltimatePerkDisplayName => "Banish";
        public override string UltimatePerkDescription => "Pulls the nearest enemy or spider into the pocket dimension, forever.";

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

        public float BanishRange
        {
            get
            {
                int durationLevel = PerksManager.Instance?.GetPerkLevel(Consts.PerkNames.AbilityDuration) ?? 0;
                int shortTermLevel = PerksManager.Instance?.GetPerkLevel(Consts.PerkNames.ShortTermInvestment) ?? 0;
                int longTermLevel = PerksManager.Instance?.GetPerkLevel(Consts.PerkNames.LongTermInvestment) ?? 0;

                float range = Consts.Values.Storage.BanishBaseRange;
                if (durationLevel == 2) range += Consts.Values.Storage.BanishRangeIncreasePerLevel;
                if (shortTermLevel > 0) range -= Consts.Values.Storage.BanishRangeIncreasePerLevel;
                if (longTermLevel > 0) range += Consts.Values.Storage.BanishRangeIncreasePerLevel;

                return range;
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
        public Sprite StoredWeaponSprite => _storedWeaponData?.WeaponRef != null
            ? (_storedWeaponData.WeaponRef.GetComponent<SpriteRenderer>()?.sprite
                ?? _storedWeaponData.WeaponRef.GetComponentInChildren<SpriteRenderer>(true)?.sprite) : null;

        private static readonly Dictionary<Weapon, float> restoredAmmo = new Dictionary<Weapon, float>();

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
                Logger.LogError($"StorageAbility: SpiderWeaponManager not found on {name}");
            }

            // Restore weapons on start (local only)
            RestoreWeapons();
            UpdateCachedModifierLevels();
        }

        // Weapons are network objects, so only the host moves them; a client plays the sounds for its own press
        protected override void OnActivate()
        {
            if (IsAuthority)
            {
                StartCoroutine(SwapRoutine());
            }
            else if (!IsRemote)
            {
                StartCoroutine(SwapSoundsOnly());
            }
        }

        protected override void OnActivateUltimate()
        {
            bool hit = IsAuthority && TryBanishNearest();
            SpawnBanishRing();
            if (Net.IsHost && spiderHealthSystem != null)
            {
                SurgeNet.SendFx(SurgeFx.Banish, PlayerId, spiderHealthSystem.transform.position, BanishRange, false);
            }
            // Like the other abilities, only the caster's own machine plays the sound
            if (!IsRemote)
            {
                SoundManager.Instance?.PlaySound(
                    Consts.SoundNames.StorageSend,
                    Consts.SoundVolumes.StorageSend * Consts.SoundVolumes.MasterVolume
                );
            }

            isActive = false;
            isUltimateActive = false;
            StartCooldown(wasUltimate: true);
            if (IsAuthority && !hit)
            {
                ReduceCooldown(UltimateCooldownTime * (1f - Consts.Values.Storage.BanishMissCooldownFraction));
            }
        }

        private bool _clientSlotFilled;

        private IEnumerator SwapSoundsOnly()
        {
            bool hadWeapon = _weaponManager != null && _weaponManager.equippedWeapon != null;
            bool hadStored = _clientSlotFilled;
            _clientSlotFilled = hadWeapon;

            if (hadWeapon && SoundManager.Instance != null)
            {
                SoundManager.Instance.PlaySound(Consts.SoundNames.StorageSend, Consts.SoundVolumes.StorageSend * Consts.SoundVolumes.MasterVolume);
            }

            yield return new WaitForSeconds(AbilityDuration);

            if (hadStored && SoundManager.Instance != null)
            {
                SoundManager.Instance.PlaySound(Consts.SoundNames.StorageRetrieve, Consts.SoundVolumes.StorageRetrieve * Consts.SoundVolumes.MasterVolume);
            }
        }

        private void SpawnBanishRing()
        {
            if (spiderHealthSystem == null) return;
            SpawnBanishRing(spiderHealthSystem.transform, BanishRange);
        }

        private static void SpawnBanishRing(Transform body, float range)
        {
            StorageVfx.Banish(body, range);
        }

        // Another player's Banish online, drawn from the host's message
        public static void DrawRemoteBanish(int playerId, float range)
        {
            SpiderHealthSystem health = Players.HealthOf(playerId);
            if (health != null)
            {
                SpawnBanishRing(health.transform, range);
            }
        }

        // Pulls the closest enemy that counts for the wave, or the closest other spider that can die, within banish range
        private bool TryBanishNearest()
        {
            if (spiderHealthSystem == null) return false;

            Transform body = spiderHealthSystem.transform;
            Vector2 origin = body.position;
            float closest = BanishRange;
            EnemyHealthSystem enemyTarget = null;
            SpiderHealthSystem spiderTarget = null;

            if (EnemyHealthSystem.s_activeEnemies != null)
            {
                foreach (EnemyHealthSystem enemy in EnemyHealthSystem.s_activeEnemies)
                {
                    if (enemy == null || enemy.IsDead() || !enemy.needToKillToCompleteLevel || BanishedTarget.IsBanished(enemy.gameObject)) continue;

                    Vector2 position = enemy.rb2D != null ? enemy.rb2D.position : (Vector2)enemy.transform.position;
                    float distance = Vector2.Distance(origin, position);
                    if (distance <= closest)
                    {
                        closest = distance;
                        enemyTarget = enemy;
                    }
                }
            }

            if (SpiderHealthSystem.s_activeSpiders != null)
            {
                foreach (SpiderHealthSystem spider in SpiderHealthSystem.s_activeSpiders)
                {
                    if (!CanBanish(spider)) continue;

                    float distance = Vector2.Distance(origin, spider.transform.position);
                    if (distance <= closest)
                    {
                        closest = distance;
                        spiderTarget = spider;
                        enemyTarget = null;
                    }
                }
            }

            if (spiderTarget != null)
            {
                BanishedTarget.BeginSpider(spiderTarget, body, playerController);
                return true;
            }
            if (enemyTarget != null)
            {
                BanishedTarget.BeginEnemy(enemyTarget, body, playerController);
                return true;
            }
            return false;
        }

        // Teams spares teammates here when friendly fire is off
        public bool CanBanish(SpiderHealthSystem spider)
        {
            return spider != null && spider != spiderHealthSystem && !BanishedTarget.IsBanished(spider.rootObject) && BanishedTarget.CanDie(spider);
        }

        private IEnumerator SwapRoutine()
        {
            if (_weaponManager == null) yield break;

            GameObject storedWeaponObj = _storedWeaponData?.WeaponRef != null ? _storedWeaponData.WeaponRef.gameObject : null;
            GameObject heldWeaponObj = _weaponManager.equippedWeapon ? _weaponManager.equippedWeapon.gameObject : null;

            _storedWeaponData = StoreWeapon(heldWeaponObj);
            _retrieving = storedWeaponObj;

            yield return new WaitForSeconds(AbilityDuration);

            _retrieving = null;
            RetrieveWeapon(storedWeaponObj);
        }

        private RuntimeStoredWeapon StoreWeapon(GameObject heldWeaponObj)
        {
            if (heldWeaponObj == null) return null;

            Weapon val = heldWeaponObj.GetComponent<Weapon>();
            if (val == null) return null;

            _weaponManager.UnEquipWeapon();
            StorageVfx.Send(heldWeaponObj.transform.position);

            HideFromClients(heldWeaponObj);
            heldWeaponObj.transform.SetParent(transform);
            heldWeaponObj.transform.localPosition = Vector3.zero;
            heldWeaponObj.SetActive(false);

            if (SoundManager.Instance != null && !IsRemote)
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
                Types = val.type != null ? new List<Weapon.WeaponType>(val.type) : new List<Weapon.WeaponType>()
            };
        }

        private void RetrieveWeapon(GameObject storedWeaponObj)
        {
            if (storedWeaponObj == null) return;

            // Drop any weapon the player picked up during the delay
            if (_weaponManager != null && _weaponManager.equippedWeapon != null)
            {
                _weaponManager.UnEquipWeapon();
            }

            // Spawn at a random position around the player for cool effect
            float angle = Random.Range(0f, Mathf.PI * 2);
            Vector3 offset = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * Consts.Values.Storage.SpawnDistance;
            storedWeaponObj.transform.position = _weaponManager.transform.position + offset;

            storedWeaponObj.SetActive(true);
            storedWeaponObj.transform.SetParent(null); // Detach from player so it can move freely
            ShowToClients(storedWeaponObj);
            StorageVfx.Retrieve(_weaponManager.transform.position);

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

            if (SoundManager.Instance != null && !IsRemote)
            {
                SoundManager.Instance.PlaySound(
                    Consts.SoundNames.StorageRetrieve,
                    Consts.SoundVolumes.StorageRetrieve * Consts.SoundVolumes.MasterVolume
                );
            }
        }

        // Netcode doesn't sync SetActive, so a stored weapon is despawned on clients until it comes back out
        private static bool HiddenFromClients(ulong clientId) => clientId == Net.ServerId;

        private static void HideFromClients(GameObject weaponObj)
        {
            var no = weaponObj.GetComponent<NetworkObject>();
            if (no == null || !Net.IsHost)
            {
                return;
            }
            no.CheckObjectVisibility = HiddenFromClients;
            if (!no.IsSpawned)
            {
                return;
            }
            foreach (ulong client in new List<ulong>(Net.Manager.ConnectedClientsIds))
            {
                if (client != Net.ServerId && no.IsNetworkVisibleTo(client))
                {
                    no.NetworkHide(client);
                }
            }
            }

        private static void ShowToClients(GameObject weaponObj)
        {
            var no = weaponObj.GetComponent<NetworkObject>();
            if (no == null || !Net.IsHost)
            {
                return;
            }
            no.CheckObjectVisibility = null;
            if (!no.IsSpawned)
            {
                return;
            }
            foreach (ulong client in new List<ulong>(Net.Manager.ConnectedClientsIds))
            {
                if (client != Net.ServerId && !no.IsNetworkVisibleTo(client))
                {
                    no.NetworkShow(client);
                }
            }
        }

        // An Astral Heckhound hit only makes a ghost, which can still be revived, so its death counts once its time runs out or it is disintegrated
        public void OnCharacterDied()
        {
            if (_playerDied || BecomingGhost) return;
            // Local check
            _playerDied = true;
            SaveWeapons(isDeath: true);
            DestroyStoredWeapons();
        }

        private bool BecomingGhost => spiderHealthSystem != null && spiderHealthSystem.astralHeckhoundMod && !spiderHealthSystem.astralDead && !spiderHealthSystem.dead;

        private bool Died => spiderHealthSystem != null && (spiderHealthSystem.dead || (spiderHealthSystem.astralDead && !spiderHealthSystem.astralHeckhoundMod));

        private void LateUpdate()
        {
            if (!_playerDied && Died) OnCharacterDied();
        }

        // A ghost whose time runs out is marked dead and its spider switched off in the same frame
        private void OnDisable()
        {
            if (!_playerDied && Died) OnCharacterDied();
        }

        // Stored weapons are inactive network objects under the spider, which outlive it hidden in the level; a kept one comes back as a new copy
        private void DestroyStoredWeapons()
        {
            if (!IsAuthority) return;
            GameObject stored = _storedWeaponData?.WeaponRef != null ? _storedWeaponData.WeaponRef.gameObject : null;
            GameObject retrieving = _retrieving;
            _storedWeaponData = null;
            _retrieving = null;
            foreach (GameObject weaponObj in new[] { stored, retrieving })
            {
                if (weaponObj == null || weaponObj.activeSelf) continue;
                NetworkObject no = weaponObj.GetComponent<NetworkObject>();
                if (no != null && no.IsSpawned) no.Despawn(true);
                else Destroy(weaponObj);
            }
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
        private GameObject _retrieving;

        private void SaveWeapons(bool isDeath)
        {
            if (PerksManager.Instance == null || !IsAuthority) return;

            int playerId = PlayerId;
            if (playerId == -1) return;

            List<StoragePersistenceManager.SavedWeaponData> dataList = new List<StoragePersistenceManager.SavedWeaponData>();

            SaveSlot(_storedWeaponData, isDeath, dataList);
            StoragePersistenceManager.SaveStoredWeapons(playerId, dataList);
        }

        private void SaveSlot(RuntimeStoredWeapon weaponData, bool isDeath, List<StoragePersistenceManager.SavedWeaponData> dataList)
        {
            if (weaponData == null) return;

            if (ShouldKeepWeapon(weaponData, isDeath))
            {
                dataList.Add(new StoragePersistenceManager.SavedWeaponData
                {
                    WeaponName = weaponData.Name,
                    Ammo = weaponData.Ammo
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
            int requiredModLevel = GetRequiredModLevel(effectiveTypes);

            if (isDeath)
                return requiredModLevel >= 2;
            else
                return requiredModLevel >= 1;
        }

        private int GetRequiredModLevel(List<Weapon.WeaponType> types)
        {
            bool isBoom = false;
            bool isParticles = false;
            bool isGuns = false;

            foreach (var wType in types)
            {
                if (wType == Weapon.WeaponType.Explosive || wType == Weapon.WeaponType.Throwable || wType == Weapon.WeaponType.Mine)
                    isBoom = true;
                else if (wType == Weapon.WeaponType.Particle || wType == Weapon.WeaponType.Melee)
                    isParticles = true;
                else if (wType == Weapon.WeaponType.Gun)
                    isGuns = true;
            }

            if (isBoom) return _cachedMoreBoomLevel;
            if (isParticles) return _cachedMoreParticlesLevel;
            if (isGuns) return _cachedMoreGunsLevel;
            return 0;
        }

        private void RestoreWeapons()
        {
            if (PerksManager.Instance == null || !IsAuthority) return;

            int playerId = PlayerId;
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
                    Logger.LogWarning("[StorageAbility] Could not retrieve _weapons from SurvivalMode instance.");
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
                        if (no != null)
                        {
                            no.CheckObjectVisibility = HiddenFromClients;
                            no.Spawn(true);
                        }
                    }

                    Weapon newWeapon = newWeaponObj.GetComponent<Weapon>();
                    if (newWeapon != null)
                    {
                        newWeapon.ammo = data.Ammo;
                        KeepRestoredAmmo(newWeapon, data.Ammo);
                    }

                    newWeaponObj.transform.SetParent(transform);
                    newWeaponObj.transform.localPosition = Vector3.zero;
                    newWeaponObj.SetActive(false);

                    _storedWeaponData = new RuntimeStoredWeapon
                    {
                        WeaponRef = newWeapon,
                        Name = newWeapon.serializationWeaponName,
                        Ammo = newWeapon.ammo,
                        Types = newWeapon.type != null ? new List<Weapon.WeaponType>(newWeapon.type) : new List<Weapon.WeaponType>()
                    };
                }
                else
                {
                    Logger.LogWarning($"[Storage] Could not find prefab for saved weapon {data.WeaponName}");
                }
            }

            StoragePersistenceManager.ClearStoredWeapons(playerId);
        }

        private static void KeepRestoredAmmo(Weapon weapon, float ammo)
        {
            foreach (Weapon gone in new List<Weapon>(restoredAmmo.Keys))
            {
                if (gone == null) restoredAmmo.Remove(gone);
            }
            restoredAmmo[weapon] = ammo;
        }

        // A hit before Weapon.Start also refills the ammo, and Start refills it again, so the saved ammo is kept until Start is done
        public static void ApplyRestoredAmmo(Weapon weapon, bool started)
        {
            if (restoredAmmo.TryGetValue(weapon, out float ammo))
            {
                if (started) restoredAmmo.Remove(weapon);
                weapon.ammo = Mathf.Min(ammo, weapon.maxAmmo);
            }
        }

        public static StorageAbility GetByHealthSystem(SpiderHealthSystem healthSystem)
        {
            if (healthSystem == null) return null;
            return healthSystem.GetComponentInParent<StorageAbility>();
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

    [HarmonyPatch(typeof(Weapon), "StartActions")]
    public class Weapon_StartActions_StoragePatch
    {
        static void Postfix(Weapon __instance)
        {
            StorageAbility.ApplyRestoredAmmo(__instance, false);
        }
    }

    [HarmonyPatch(typeof(Weapon), nameof(Weapon.Start))]
    public class Weapon_Start_StoragePatch
    {
        static void Postfix(Weapon __instance)
        {
            StorageAbility.ApplyRestoredAmmo(__instance, true);
        }
    }
}
