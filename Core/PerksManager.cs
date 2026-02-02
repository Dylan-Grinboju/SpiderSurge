using UnityEngine;
using System.Collections.Generic;
using Logger = Silk.Logger;

namespace SpiderSurge
{
    public class PerksManager : MonoBehaviour
    {
        public static PerksManager Instance { get; private set; }

        public bool IsFirstNormalPerkSelection { get; set; } = true;

        public bool IsUltUpgradePerkSelection { get; set; } = false;

        public bool IsUltSwapPerkSelection { get; set; } = false;

        // Ability perks - shown in special ability selection screen
        private readonly HashSet<string> abilityPerks = new HashSet<string> { Consts.PerkNames.ShieldAbility, Consts.PerkNames.InfiniteAmmoAbility, Consts.PerkNames.ExplosionAbility, Consts.PerkNames.InterdimensionalStorageAbility };

        // Upgrade perks - shown in normal perk selection
        private readonly HashSet<string> upgradePerks = new HashSet<string> { Consts.PerkNames.AbilityCooldown, Consts.PerkNames.AbilityDuration, Consts.PerkNames.ShortTermInvestment, Consts.PerkNames.LongTermInvestment, Consts.PerkNames.PerkLuck };

        // Ability Ultimate perks - Ultimate versions of abilities (requires base ability)
        private readonly HashSet<string> abilityUltimatePerks = new HashSet<string> { Consts.PerkNames.ShieldAbilityUltimate, Consts.PerkNames.InfiniteAmmoAbilityUltimate, Consts.PerkNames.ExplosionAbilityUltimate, Consts.PerkNames.InterdimensionalStorageAbilityUltimate };



        private readonly Dictionary<string, int> maxLevels = new Dictionary<string, int>
        {
            [Consts.PerkNames.ShieldAbility] = 1,
            [Consts.PerkNames.InfiniteAmmoAbility] = 1,
            [Consts.PerkNames.ExplosionAbility] = 1,
            [Consts.PerkNames.InterdimensionalStorageAbility] = 1,
            [Consts.PerkNames.AbilityCooldown] = 2,
            [Consts.PerkNames.AbilityDuration] = 2,
            [Consts.PerkNames.ShortTermInvestment] = 1,
            [Consts.PerkNames.LongTermInvestment] = 1,
            [Consts.PerkNames.PerkLuck] = 2,
            // Ultimate perks are level 1 only
            [Consts.PerkNames.ShieldAbilityUltimate] = 1,
            [Consts.PerkNames.InfiniteAmmoAbilityUltimate] = 1,
            [Consts.PerkNames.ExplosionAbilityUltimate] = 1,
            [Consts.PerkNames.InterdimensionalStorageAbilityUltimate] = 1
        };

        private readonly Dictionary<string, List<string>> dependencies = new Dictionary<string, List<string>>
        {
            [Consts.PerkNames.ShieldAbility] = new List<string>(),
            [Consts.PerkNames.InfiniteAmmoAbility] = new List<string>(),
            [Consts.PerkNames.ExplosionAbility] = new List<string>(),
            [Consts.PerkNames.InterdimensionalStorageAbility] = new List<string>(),
            [Consts.PerkNames.AbilityCooldown] = new List<string>(),
            [Consts.PerkNames.AbilityDuration] = new List<string>(),
            [Consts.PerkNames.ShortTermInvestment] = new List<string>(),
            [Consts.PerkNames.LongTermInvestment] = new List<string>(),
            [Consts.PerkNames.PerkLuck] = new List<string>(),
            // Ultimate perks require the base ability to be unlocked
            [Consts.PerkNames.ShieldAbilityUltimate] = new List<string> { Consts.PerkNames.ShieldAbility },
            [Consts.PerkNames.InfiniteAmmoAbilityUltimate] = new List<string> { Consts.PerkNames.InfiniteAmmoAbility },
            [Consts.PerkNames.ExplosionAbilityUltimate] = new List<string> { Consts.PerkNames.ExplosionAbility },
            [Consts.PerkNames.InterdimensionalStorageAbilityUltimate] = new List<string> { Consts.PerkNames.InterdimensionalStorageAbility }
        };

        private readonly Dictionary<string, int> perkLevels = new Dictionary<string, int>();

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);

                // Ensure managers exist
                if (SurgeGameModeManager.Instance == null)
                {
                    GameObject surgeManager = new GameObject("SurgeGameModeManager");
                    surgeManager.AddComponent<SurgeGameModeManager>();
                }
            }
            else
            {
                Destroy(gameObject);
            }
        }



        public static void EnableShieldAbility()
        {
            EnableAbility<ShieldAbility>(Consts.PerkNames.ShieldAbility);
        }

        public static void EnableInfiniteAmmoAbility()
        {
            EnableAbility<InfiniteAmmoAbility>(Consts.PerkNames.InfiniteAmmoAbility);
        }

        public static void EnableExplosionAbility()
        {
            EnableAbility<ExplosionAbility>(Consts.PerkNames.ExplosionAbility);
        }

        public static void EnableStorageAbility()
        {
            EnableAbility<InterdimensionalStorageAbility>(Consts.PerkNames.InterdimensionalStorageAbility);
        }

        public static void EnableShieldUltimate()
        {
            EnableUltimate<ShieldAbility>(Consts.PerkNames.ShieldAbilityUltimate);
        }

        public static void EnableInfiniteAmmoUltimate()
        {
            EnableUltimate<InfiniteAmmoAbility>(Consts.PerkNames.InfiniteAmmoAbilityUltimate);
        }

        public static void EnableExplosionUltimate()
        {
            EnableUltimate<ExplosionAbility>(Consts.PerkNames.ExplosionAbilityUltimate);
        }

        public static void EnableStorageUltimate()
        {
            EnableUltimate<InterdimensionalStorageAbility>(Consts.PerkNames.InterdimensionalStorageAbilityUltimate);
        }

        private static void EnableAbility<T>(string perkName) where T : BaseAbility
        {
            try
            {
                Instance.SetPerkLevel(perkName, 1);

                // Register abilities with input interceptor now that they're unlocked
                PlayerAbilityHandler.ActiveSpiderControllers.RemoveAll(sc => sc == null);
                foreach (SpiderController player in PlayerAbilityHandler.ActiveSpiderControllers)
                {
                    var ability = player.GetComponent<T>();
                    if (ability != null)
                    {
                        ability.RegisterWithInputInterceptor();
                    }
                }
            }
            catch (System.Exception ex)
            {
                Logger.LogError($"Error enabling {typeof(T).Name}: {ex.Message}");
            }
        }

        private static void EnableUltimate<T>(string perkName) where T : BaseAbility
        {
            try
            {
                // Also enable the base ability
                string baseAbility = perkName.Replace("Ultimate", "");
                Instance.SetPerkLevel(baseAbility, 1);
                Instance.SetPerkLevel(perkName, 1);

                // Register abilities with input interceptor now that they're unlocked
                PlayerAbilityHandler.ActiveSpiderControllers.RemoveAll(sc => sc == null);
                foreach (SpiderController player in PlayerAbilityHandler.ActiveSpiderControllers)
                {
                    var ability = player.GetComponent<T>();
                    if (ability != null)
                    {
                        ability.RegisterWithInputInterceptor();
                    }
                }
            }
            catch (System.Exception ex)
            {
                Logger.LogError($"Error enabling {typeof(T).Name} ultimate: {ex.Message}");
            }
        }

        public bool IsAbilityPerk(string perkName)
        {
            return abilityPerks.Contains(perkName);
        }

        public bool IsUpgradePerk(string perkName)
        {
            return upgradePerks.Contains(perkName);
        }

        public IEnumerable<string> GetAbilityPerkNames()
        {
            return abilityPerks;
        }

        public IEnumerable<string> GetUpgradePerkNames()
        {
            return upgradePerks;
        }

        public IEnumerable<string> GetAbilityUltimatePerkNames()
        {
            return abilityUltimatePerks;
        }

        public int GetPerkLevel(string perkName)
        {
            return perkLevels.ContainsKey(perkName) ? perkLevels[perkName] : 0;
        }

        public bool IsAvailable(string perkName)
        {
            if (!maxLevels.ContainsKey(perkName)) return false;
            int level = GetPerkLevel(perkName);
            if (level >= maxLevels[perkName]) return false;
            foreach (var dep in dependencies[perkName])
            {
                if (GetPerkLevel(dep) == 0) return false;
            }
            return true;
        }

        public bool HasAnyAbilityUnlocked()
        {
            return GetPerkLevel(Consts.PerkNames.ShieldAbility) > 0 || GetPerkLevel(Consts.PerkNames.InfiniteAmmoAbility) > 0 || GetPerkLevel(Consts.PerkNames.ExplosionAbility) > 0 || GetPerkLevel(Consts.PerkNames.InterdimensionalStorageAbility) > 0;
        }

        public string GetChosenAbilityUltimate()
        {
            if (GetPerkLevel(Consts.PerkNames.ShieldAbility) > 0) return Consts.PerkNames.ShieldAbilityUltimate;
            if (GetPerkLevel(Consts.PerkNames.InfiniteAmmoAbility) > 0) return Consts.PerkNames.InfiniteAmmoAbilityUltimate;
            if (GetPerkLevel(Consts.PerkNames.ExplosionAbility) > 0) return Consts.PerkNames.ExplosionAbilityUltimate;
            if (GetPerkLevel(Consts.PerkNames.InterdimensionalStorageAbility) > 0) return Consts.PerkNames.InterdimensionalStorageAbilityUltimate;
            return null;
        }

        public IEnumerable<string> GetAllPerkNames() => maxLevels.Keys;

        public string GetDisplayName(string name) => Consts.Descriptions.GetDisplayName(name);
        public string GetDescription(string name) => Consts.Descriptions.GetDescription(name, this);

        public string GetUpgradeDescription(string name) => Consts.Descriptions.GetUpgradeDescription(name, this);
        public int GetMaxLevel(string name) => maxLevels.ContainsKey(name) ? maxLevels[name] : 1;

        public void SetPerkLevel(string perkKey, int level)
        {
            perkLevels[perkKey] = level;
        }

        private Dictionary<string, System.Action> _perkActions;

        private void InitializePerkActions()
        {
            _perkActions = new Dictionary<string, System.Action>
            {
                [Consts.PerkNames.ShieldAbility] = EnableShieldAbility,
                [Consts.PerkNames.InfiniteAmmoAbility] = EnableInfiniteAmmoAbility,
                [Consts.PerkNames.ExplosionAbility] = EnableExplosionAbility,
                [Consts.PerkNames.InterdimensionalStorageAbility] = EnableStorageAbility,
                [Consts.PerkNames.ShieldAbilityUltimate] = () => HandleUltimateSelection(Consts.PerkNames.ShieldAbilityUltimate, EnableShieldUltimate),
                [Consts.PerkNames.InfiniteAmmoAbilityUltimate] = () => HandleUltimateSelection(Consts.PerkNames.InfiniteAmmoAbilityUltimate, EnableInfiniteAmmoUltimate),
                [Consts.PerkNames.ExplosionAbilityUltimate] = () => HandleUltimateSelection(Consts.PerkNames.ExplosionAbilityUltimate, EnableExplosionUltimate),
                [Consts.PerkNames.InterdimensionalStorageAbilityUltimate] = () => HandleUltimateSelection(Consts.PerkNames.InterdimensionalStorageAbilityUltimate, EnableStorageUltimate)
            };
        }

        public void OnSelected(string name)
        {
            if (_perkActions == null) InitializePerkActions();

            if (_perkActions.ContainsKey(name))
            {
                _perkActions[name].Invoke();
            }
        }

        private void HandleUltimateSelection(string newUltName, System.Action enableMethod)
        {
            string currentUltPath = GetChosenAbilityUltimate();

            if (currentUltPath != null && currentUltPath != newUltName)
            {
                if (dependencies.ContainsKey(currentUltPath) && dependencies[currentUltPath].Count > 0)
                {
                    string oldAbilityName = dependencies[currentUltPath][0];

                    Logger.LogInfo($"[Perk Swap] Swapping from {oldAbilityName}/{currentUltPath} to {newUltName}");

                    SetPerkLevel(currentUltPath, 0);
                    SetPerkLevel(oldAbilityName, 0);
                }
            }
            enableMethod?.Invoke();
        }

        public void ResetPerks()
        {
            perkLevels.Clear();
            IsFirstNormalPerkSelection = true;
            IsUltUpgradePerkSelection = false;
            IsUltSwapPerkSelection = false;
        }

        public float GetPerkLuckChance()
        {
            int level = GetPerkLevel(Consts.PerkNames.PerkLuck);
            if (level == 1) return Consts.Values.Luck.Level1Chance;
            if (level == 2) return Consts.Values.Luck.Level2Chance;
            return 0f;
        }


    }
}