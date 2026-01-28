using UnityEngine;
using UnityEngine.InputSystem;
using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using Logger = Silk.Logger;

namespace SpiderSurge
{
    public class PerksManager : MonoBehaviour
    {
        public static PerksManager Instance { get; private set; }

        public bool IsFirstNormalPerkSelection { get; set; } = true;

        public bool IsPost30WavePerkSelection { get; set; } = false;

        public bool IsPost60WavePerkSelection { get; set; } = false;

        // Ability perks - shown in special ability selection screen
        private HashSet<string> abilityPerks = new HashSet<string> { "shieldAbility", "infiniteAmmoAbility", "explosionAbility" };

        // Upgrade perks - shown in normal perk selection
        private HashSet<string> upgradePerks = new HashSet<string> { "abilityCooldown", "abilityDuration", "shortTermInvestment", "longTermInvestment", "perkLuck" };

        // Ability Ultimate perks - Ultimate versions of abilities (requires base ability)
        private HashSet<string> abilityUltimatePerks = new HashSet<string> { "shieldAbilityUltimate", "infiniteAmmoAbilityUltimate", "explosionAbilityUltimate" };

        private Dictionary<string, string> displayNames = new Dictionary<string, string>
        {
            ["shieldAbility"] = "Shield Ability",
            ["infiniteAmmoAbility"] = "Infinite Ammo",
            ["explosionAbility"] = "Explosion Ability",
            ["abilityCooldown"] = "Ability Cooldown",
            ["abilityDuration"] = "Ability Duration",
            ["shortTermInvestment"] = "Short Term Investment",
            ["longTermInvestment"] = "Long Term Investment",
            ["perkLuck"] = "Perk Luck",
            // Ultimate perks - dynamic names based on which ability is active
            ["shieldAbilityUltimate"] = "Shield Immunity",
            ["infiniteAmmoAbilityUltimate"] = "Weapon Arsenal",
            ["explosionAbilityUltimate"] = "Deadly Explosion"
        };

        private Dictionary<string, string> descriptions = new Dictionary<string, string>
        {
            ["shieldAbility"] = "Unlocks the shield ability.",
            ["infiniteAmmoAbility"] = "Unlocks the infinite ammo ability.",
            ["explosionAbility"] = "Unlocks the explosion ability.",
            ["abilityCooldown"] = "Reduces ability cooldown.",
            ["abilityDuration"] = "Increases ability duration.",
            ["shortTermInvestment"] = "Increases ability duration by 2 levels, but increases cooldown by 1 level.",
            ["longTermInvestment"] = "Decreases cooldown by 2 levels, but decreases ability duration by 1 level.",
            ["perkLuck"] = "Chance to see level 2 perks even without level 1.",
            // Ultimate perks
            ["shieldAbilityUltimate"] = "Grants complete damage immunity (3x cooldown).",
            ["infiniteAmmoAbilityUltimate"] = "Spawns weapons at all spawn points (3x cooldown).",
            ["explosionAbilityUltimate"] = "Explosion deals lethal damage (3x cooldown)."
        };

        private Dictionary<string, string> upgradeDescriptions = new Dictionary<string, string>
        {
            ["shieldAbility"] = "",
            ["infiniteAmmoAbility"] = "",
            ["explosionAbility"] = "",
            ["abilityCooldown"] = "Further reduces ability cooldown.",
            ["abilityDuration"] = "Further increases ability duration.",
            ["shortTermInvestment"] = "",
            ["longTermInvestment"] = "",
            ["perkLuck"] = "Increases chance to see level 2 perks.",
            // Ultimate perks don't have upgrade descriptions (max level 1)
            ["shieldAbilityUltimate"] = "",
            ["infiniteAmmoAbilityUltimate"] = "",
            ["explosionAbilityUltimate"] = ""
        };

        // Descriptions when explosion ability is unlocked (duration also affects explosion size)
        private const string DURATION_DESC_WITH_EXPLOSION = "Increases explosion size.";
        private const string DURATION_UPGRADE_DESC_WITH_EXPLOSION = "Further increases explosion size.";

        private Dictionary<string, int> maxLevels = new Dictionary<string, int>
        {
            ["shieldAbility"] = 1,
            ["infiniteAmmoAbility"] = 1,
            ["explosionAbility"] = 1,
            ["abilityCooldown"] = 2,
            ["abilityDuration"] = 2,
            ["shortTermInvestment"] = 1,
            ["longTermInvestment"] = 1,
            ["perkLuck"] = 2,
            // Ultimate perks are level 1 only
            ["shieldAbilityUltimate"] = 1,
            ["infiniteAmmoAbilityUltimate"] = 1,
            ["explosionAbilityUltimate"] = 1
        };

        private Dictionary<string, List<string>> dependencies = new Dictionary<string, List<string>>
        {
            ["shieldAbility"] = new List<string>(),
            ["infiniteAmmoAbility"] = new List<string>(),
            ["explosionAbility"] = new List<string>(),
            ["abilityCooldown"] = new List<string>(),
            ["abilityDuration"] = new List<string>(),
            ["shortTermInvestment"] = new List<string>(),
            ["longTermInvestment"] = new List<string>(),
            ["perkLuck"] = new List<string>(),
            // Ultimate perks require the base ability to be unlocked
            ["shieldAbilityUltimate"] = new List<string> { "shieldAbility" },
            ["infiniteAmmoAbilityUltimate"] = new List<string> { "infiniteAmmoAbility" },
            ["explosionAbilityUltimate"] = new List<string> { "explosionAbility" }
        };

        private Dictionary<string, int> perkLevels = new Dictionary<string, int>();

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

        public static void InitializePlayerAbilities(GameObject playerObject)
        {
            if (playerObject == null) return;

            try
            {
                // Check if surge mode is active
                if (!SurgeGameModeManager.Instance.IsActive)
                {
                    Logger.LogWarning("Surge mode not active - skipping ability initialization");
                    return;
                }

                PlayerInput playerInput = playerObject.GetComponentInParent<PlayerInput>();
                if (playerInput == null)
                {
                    Logger.LogWarning("Could not find PlayerInput component on player object");
                    return;
                }

                SpiderController spiderController = playerObject.GetComponent<SpiderController>();
                if (spiderController == null)
                {
                    Logger.LogWarning("Could not find SpiderController component on player object");
                    return;
                }

                if (spiderController.GetComponent<InputInterceptor>() == null)
                {
                    spiderController.gameObject.AddComponent<InputInterceptor>();
                }

                if (spiderController.GetComponent<ShieldAbility>() == null)
                {
                    spiderController.gameObject.AddComponent<ShieldAbility>();
                }

                if (spiderController.GetComponent<InfiniteAmmoAbility>() == null)
                {
                    spiderController.gameObject.AddComponent<InfiniteAmmoAbility>();
                }

                if (spiderController.GetComponent<ExplosionAbility>() == null)
                {
                    spiderController.gameObject.AddComponent<ExplosionAbility>();
                }


            }
            catch (System.Exception ex)
            {
                Logger.LogError($"Error initializing player abilities: {ex.Message}");
            }
        }

        public static void EnableShieldAbility()
        {
            EnableAbility<ShieldAbility>("shieldAbility");
        }

        public static void EnableInfiniteAmmoAbility()
        {
            EnableAbility<InfiniteAmmoAbility>("infiniteAmmoAbility");
        }

        public static void EnableExplosionAbility()
        {
            EnableAbility<ExplosionAbility>("explosionAbility");
        }

        public static void EnableShieldUltimate()
        {
            EnableUltimate<ShieldAbility>("shieldAbilityUltimate");
        }

        public static void EnableInfiniteAmmoUltimate()
        {
            EnableUltimate<InfiniteAmmoAbility>("infiniteAmmoAbilityUltimate");
        }

        public static void EnableExplosionUltimate()
        {
            EnableUltimate<ExplosionAbility>("explosionAbilityUltimate");
        }

        private static void EnableAbility<T>(string perkName) where T : BaseAbility
        {
            try
            {
                Instance.SetPerkLevel(perkName, 1);

                // Register abilities with input interceptor now that they're unlocked
                SpiderController[] players = FindObjectsOfType<SpiderController>();
                foreach (SpiderController player in players)
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
                SpiderController[] players = FindObjectsOfType<SpiderController>();
                foreach (SpiderController player in players)
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
            return GetPerkLevel("shieldAbility") > 0 || GetPerkLevel("infiniteAmmoAbility") > 0 || GetPerkLevel("explosionAbility") > 0;
        }

        public string GetChosenAbilityUltimate()
        {
            if (GetPerkLevel("shieldAbility") > 0) return "shieldAbilityUltimate";
            if (GetPerkLevel("infiniteAmmoAbility") > 0) return "infiniteAmmoAbilityUltimate";
            if (GetPerkLevel("explosionAbility") > 0) return "explosionAbilityUltimate";
            return null;
        }

        public IEnumerable<string> GetAllPerkNames() => maxLevels.Keys;

        public string GetDisplayName(string name) => displayNames.ContainsKey(name) ? displayNames[name] : name;
        public string GetDescription(string name)
        {
            if (name == "abilityDuration" && GetPerkLevel("explosionAbility") > 0)
            {
                return DURATION_DESC_WITH_EXPLOSION;
            }
            return descriptions.ContainsKey(name) ? descriptions[name] : "";
        }

        public string GetUpgradeDescription(string name)
        {
            if (name == "abilityDuration" && GetPerkLevel("explosionAbility") > 0)
            {
                return DURATION_UPGRADE_DESC_WITH_EXPLOSION;
            }
            return upgradeDescriptions.ContainsKey(name) ? upgradeDescriptions[name] : "";
        }
        public int GetMaxLevel(string name) => maxLevels.ContainsKey(name) ? maxLevels[name] : 1;

        public void SetPerkLevel(string perkKey, int level)
        {
            perkLevels[perkKey] = level;
        }

        public void OnSelected(string name)
        {
            if (name == "shieldAbility")
            {
                EnableShieldAbility();
            }
            else if (name == "infiniteAmmoAbility")
            {
                EnableInfiniteAmmoAbility();
            }
            else if (name == "explosionAbility")
            {
                EnableExplosionAbility();
            }
            else if (name == "shieldAbilityUltimate")
            {
                EnableShieldUltimate();
            }
            else if (name == "infiniteAmmoAbilityUltimate")
            {
                EnableInfiniteAmmoUltimate();
            }
            else if (name == "explosionAbilityUltimate")
            {
                EnableExplosionUltimate();
            }
        }

        public void ResetPerks()
        {
            perkLevels.Clear();
            IsFirstNormalPerkSelection = true;
            IsPost30WavePerkSelection = false;
            IsPost60WavePerkSelection = false;
        }

        public float GetPerkLuckChance()
        {
            int level = GetPerkLevel("perkLuck");
            if (level == 1) return 0.1f;
            if (level == 2) return 1f;
            return 0f;
        }
    }
}