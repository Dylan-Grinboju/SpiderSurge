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

        // Ability perks - shown in special ability selection screen
        private HashSet<string> abilityPerks = new HashSet<string> { "shieldAbility", "infiniteAmmoAbility", "explosionAbility" };

        // Upgrade perks - shown in normal perk selection
        private HashSet<string> upgradePerks = new HashSet<string> { "abilityCooldown", "abilityDuration" };

        private Dictionary<string, string> displayNames = new Dictionary<string, string>
        {
            ["shieldAbility"] = "Shield Ability",
            ["infiniteAmmoAbility"] = "Infinite Ammo",
            ["explosionAbility"] = "Explosion Ability",
            ["abilityCooldown"] = "Ability Cooldown",
            ["abilityDuration"] = "Ability Duration"
        };

        private Dictionary<string, string> descriptions = new Dictionary<string, string>
        {
            ["shieldAbility"] = "Unlocks the shield ability.",
            ["infiniteAmmoAbility"] = "Unlocks the infinite ammo ability.",
            ["explosionAbility"] = "Unlocks the explosion ability.",
            ["abilityCooldown"] = "Reduces ability cooldown.",
            ["abilityDuration"] = "Increases ability duration."
        };

        private Dictionary<string, string> upgradeDescriptions = new Dictionary<string, string>
        {
            ["shieldAbility"] = "",
            ["infiniteAmmoAbility"] = "",
            ["explosionAbility"] = "",
            ["abilityCooldown"] = "Further reduces ability cooldown.",
            ["abilityDuration"] = "Further increases ability duration."
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
            ["abilityDuration"] = 2
        };

        private Dictionary<string, List<string>> dependencies = new Dictionary<string, List<string>>
        {
            ["shieldAbility"] = new List<string>(),
            ["infiniteAmmoAbility"] = new List<string>(),
            ["explosionAbility"] = new List<string>(),
            ["abilityCooldown"] = new List<string>(),
            ["abilityDuration"] = new List<string>()
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
        }

        public void ResetPerks()
        {
            perkLevels.Clear();
            IsFirstNormalPerkSelection = true;
        }
    }
}