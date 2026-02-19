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
        private readonly HashSet<string> abilityPerks = new HashSet<string> { Consts.PerkNames.ImmuneAbility, Consts.PerkNames.AmmoAbility, Consts.PerkNames.PulseAbility, Consts.PerkNames.StorageAbility };

        // Upgrade perks - shown in normal perk selection
        private readonly HashSet<string> upgradePerks = new HashSet<string> { Consts.PerkNames.AbilityCooldown, Consts.PerkNames.AbilityDuration, Consts.PerkNames.ShortTermInvestment, Consts.PerkNames.LongTermInvestment, Consts.PerkNames.PerkLuck };

        // Ability Ultimate perks - Ultimate versions of abilities (requires base ability)
        private readonly HashSet<string> abilityUltimatePerks = new HashSet<string> { Consts.PerkNames.ImmuneAbilityUltimate, Consts.PerkNames.AmmoAbilityUltimate, Consts.PerkNames.PulseAbilityUltimate, Consts.PerkNames.StorageAbilityUltimate };



        private readonly Dictionary<string, int> maxLevels = new Dictionary<string, int>
        {
            [Consts.PerkNames.ImmuneAbility] = 1,
            [Consts.PerkNames.AmmoAbility] = 1,
            [Consts.PerkNames.PulseAbility] = 1,
            [Consts.PerkNames.StorageAbility] = 1,
            [Consts.PerkNames.AbilityCooldown] = 2,
            [Consts.PerkNames.AbilityDuration] = 2,
            [Consts.PerkNames.ShortTermInvestment] = 1,
            [Consts.PerkNames.LongTermInvestment] = 1,
            [Consts.PerkNames.PerkLuck] = 2,
            // Ultimate perks are level 1 only
            [Consts.PerkNames.ImmuneAbilityUltimate] = 1,
            [Consts.PerkNames.AmmoAbilityUltimate] = 1,
            [Consts.PerkNames.PulseAbilityUltimate] = 1,
            [Consts.PerkNames.StorageAbilityUltimate] = 1
        };

        private readonly Dictionary<string, List<string>> dependencies = new Dictionary<string, List<string>>
        {
            [Consts.PerkNames.ImmuneAbility] = new List<string>(),
            [Consts.PerkNames.AmmoAbility] = new List<string>(),
            [Consts.PerkNames.PulseAbility] = new List<string>(),
            [Consts.PerkNames.StorageAbility] = new List<string>(),
            [Consts.PerkNames.AbilityCooldown] = new List<string>(),
            [Consts.PerkNames.AbilityDuration] = new List<string>(),
            [Consts.PerkNames.ShortTermInvestment] = new List<string>(),
            [Consts.PerkNames.LongTermInvestment] = new List<string>(),
            [Consts.PerkNames.PerkLuck] = new List<string>(),
            // Ultimate perks require the base ability to be unlocked
            [Consts.PerkNames.ImmuneAbilityUltimate] = new List<string> { Consts.PerkNames.ImmuneAbility },
            [Consts.PerkNames.AmmoAbilityUltimate] = new List<string> { Consts.PerkNames.AmmoAbility },
            [Consts.PerkNames.PulseAbilityUltimate] = new List<string> { Consts.PerkNames.PulseAbility },
            [Consts.PerkNames.StorageAbilityUltimate] = new List<string> { Consts.PerkNames.StorageAbility }
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



        public static void EnableImmuneAbility()
        {
            EnableAbility<ImmuneAbility>(Consts.PerkNames.ImmuneAbility);
        }

        public static void EnableAmmoAbility()
        {
            EnableAbility<AmmoAbility>(Consts.PerkNames.AmmoAbility);
        }

        public static void EnablePulseAbility()
        {
            EnableAbility<PulseAbility>(Consts.PerkNames.PulseAbility);
        }

        public static void EnableStorageAbility()
        {
            EnableAbility<StorageAbility>(Consts.PerkNames.StorageAbility);
        }

        public static void EnableImmuneUltimate()
        {
            EnableUltimate<ImmuneAbility>(Consts.PerkNames.ImmuneAbilityUltimate);
        }

        public static void EnableAmmoUltimate()
        {
            EnableUltimate<AmmoAbility>(Consts.PerkNames.AmmoAbilityUltimate);
        }

        public static void EnablePulseUltimate()
        {
            EnableUltimate<PulseAbility>(Consts.PerkNames.PulseAbilityUltimate);
        }

        public static void EnableStorageUltimate()
        {
            EnableUltimate<StorageAbility>(Consts.PerkNames.StorageAbilityUltimate);
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
                Instance.UpdatePerkIcons();
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
                Instance.UpdatePerkIcons();
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

            if ((perkName == Consts.PerkNames.AbilityCooldown || perkName == Consts.PerkNames.AbilityDuration) && level == 1)
            {
                bool hasAnyUltimate = GetPerkLevel(Consts.PerkNames.ImmuneAbilityUltimate) > 0 ||
                                      GetPerkLevel(Consts.PerkNames.AmmoAbilityUltimate) > 0 ||
                                      GetPerkLevel(Consts.PerkNames.PulseAbilityUltimate) > 0 ||
                                      GetPerkLevel(Consts.PerkNames.StorageAbilityUltimate) > 0;
                if (!hasAnyUltimate) return false;
            }

            if (perkName == Consts.PerkNames.ShortTermInvestment || perkName == Consts.PerkNames.LongTermInvestment)
            {
                bool hasAnyUltimate = GetPerkLevel(Consts.PerkNames.ImmuneAbilityUltimate) > 0 ||
                                      GetPerkLevel(Consts.PerkNames.AmmoAbilityUltimate) > 0 ||
                                      GetPerkLevel(Consts.PerkNames.PulseAbilityUltimate) > 0 ||
                                      GetPerkLevel(Consts.PerkNames.StorageAbilityUltimate) > 0;
                if (!hasAnyUltimate) return false;
            }

            return true;
        }

        public bool HasAnyAbilityUnlocked()
        {
            return GetPerkLevel(Consts.PerkNames.ImmuneAbility) > 0 || GetPerkLevel(Consts.PerkNames.AmmoAbility) > 0 || GetPerkLevel(Consts.PerkNames.PulseAbility) > 0 || GetPerkLevel(Consts.PerkNames.StorageAbility) > 0;
        }

        public string GetChosenAbilityUltimate()
        {
            if (GetPerkLevel(Consts.PerkNames.ImmuneAbility) > 0) return Consts.PerkNames.ImmuneAbilityUltimate;
            if (GetPerkLevel(Consts.PerkNames.AmmoAbility) > 0) return Consts.PerkNames.AmmoAbilityUltimate;
            if (GetPerkLevel(Consts.PerkNames.PulseAbility) > 0) return Consts.PerkNames.PulseAbilityUltimate;
            if (GetPerkLevel(Consts.PerkNames.StorageAbility) > 0) return Consts.PerkNames.StorageAbilityUltimate;
            return null;
        }

        public IEnumerable<string> GetAllPerkNames() => maxLevels.Keys;

        public string GetDisplayName(string name) => Consts.Descriptions.GetDisplayName(name, this);
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
                [Consts.PerkNames.ImmuneAbility] = EnableImmuneAbility,
                [Consts.PerkNames.AmmoAbility] = EnableAmmoAbility,
                [Consts.PerkNames.PulseAbility] = EnablePulseAbility,
                [Consts.PerkNames.StorageAbility] = EnableStorageAbility,
                [Consts.PerkNames.ImmuneAbilityUltimate] = () => HandleUltimateSelection(Consts.PerkNames.ImmuneAbilityUltimate, EnableImmuneUltimate),
                [Consts.PerkNames.AmmoAbilityUltimate] = () => HandleUltimateSelection(Consts.PerkNames.AmmoAbilityUltimate, EnableAmmoUltimate),
                [Consts.PerkNames.PulseAbilityUltimate] = () => HandleUltimateSelection(Consts.PerkNames.PulseAbilityUltimate, EnablePulseUltimate),
                [Consts.PerkNames.StorageAbilityUltimate] = () => HandleUltimateSelection(Consts.PerkNames.StorageAbilityUltimate, EnableStorageUltimate)
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
            UpdatePerkIcons();
        }

        public float GetPerkLuckChance()
        {
            int level = GetPerkLevel(Consts.PerkNames.PerkLuck);
            if (level == 1) return Consts.Values.Luck.Level1Chance;
            if (level == 2) return Consts.Values.Luck.Level2Chance;
            return 0f;
        }

        private void UpdatePerkIcons()
        {
            if (ModifierManager.instance == null) return;

            int durationId = ModifierManager.instance.GetModId(Consts.PerkNames.AbilityDuration);
            if (durationId == -1) return;

            Modifier durationMod = ModifierManager.instance.GetModById(durationId);
            if (durationMod == null || durationMod.data == null) return;

            Sprite newIcon;
            if (GetPerkLevel(Consts.PerkNames.PulseAbility) > 0)
            {
                newIcon = IconLoader.GetIcon("pulse_duration_perk");
            }
            else
            {
                newIcon = IconLoader.GetIcon("duration_perk");
            }

            if (newIcon != null)
            {
                durationMod.data.icon = newIcon;
            }
        }


    }
}