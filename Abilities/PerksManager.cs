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

        public bool IsShieldAbilityUnlocked { get; private set; }
        public bool IsFirstNormalPerkSelection { get; set; } = true;

        // Ability perks - shown in special ability selection screen
        private HashSet<string> abilityPerks = new HashSet<string> { "shieldAbility" };

        // Upgrade perks - shown in normal perk selection
        private HashSet<string> upgradePerks = new HashSet<string> { "shieldCooldown", "shieldDuration" };

        private Dictionary<string, string> displayNames = new Dictionary<string, string>
        {
            ["shieldAbility"] = "Shield Ability",
            ["shieldCooldown"] = "Shield Cooldown",
            ["shieldDuration"] = "Shield Duration"
        };

        private Dictionary<string, string> descriptions = new Dictionary<string, string>
        {
            ["shieldAbility"] = "Unlocks the shield ability.",
            ["shieldCooldown"] = "Reduces shield cooldown from 30s to 20s.",
            ["shieldDuration"] = "Increases shield duration to 2s."
        };

        private Dictionary<string, string> upgradeDescriptions = new Dictionary<string, string>
        {
            ["shieldAbility"] = "",
            ["shieldCooldown"] = "Reduces shield cooldown from 20s to 10s.",
            ["shieldDuration"] = "Increases shield duration to 3s."
        };

        private Dictionary<string, int> maxLevels = new Dictionary<string, int>
        {
            ["shieldAbility"] = 1,
            ["shieldCooldown"] = 2,
            ["shieldDuration"] = 2
        };

        private Dictionary<string, List<string>> dependencies = new Dictionary<string, List<string>>
        {
            ["shieldAbility"] = new List<string>(),
            ["shieldCooldown"] = new List<string> { "shieldAbility" },
            ["shieldDuration"] = new List<string> { "shieldAbility" }
        };

        private Dictionary<string, int> perkLevels = new Dictionary<string, int>();

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                Logger.LogInfo("PerksManager initialized");

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
                    Logger.LogInfo("Surge mode not active - skipping ability initialization");
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
                    ShieldAbility shield = spiderController.gameObject.AddComponent<ShieldAbility>();
                }


            }
            catch (System.Exception ex)
            {
                Logger.LogError($"Error initializing player abilities: {ex.Message}");
            }
        }

        public static void EnableShieldAbility()
        {
            try
            {
                Instance.IsShieldAbilityUnlocked = true;
                // Register shield abilities with input interceptor now that they're unlocked
                SpiderController[] players = FindObjectsOfType<SpiderController>();
                foreach (SpiderController player in players)
                {
                    var shieldAbility = player.GetComponent<ShieldAbility>();
                    if (shieldAbility != null)
                    {
                        shieldAbility.RegisterWithInputInterceptor();
                    }
                }
                Logger.LogInfo("Shield ability unlocked and registered for all players");
            }
            catch (System.Exception ex)
            {
                Logger.LogError($"Error enabling shield ability: {ex.Message}");
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

        public float GetShieldCooldown()
        {
            int level = GetPerkLevel("shieldCooldown");
            return level == 1 ? 20f : level == 2 ? 10f : 30f;
        }

        public float GetShieldDuration()
        {
            int level = GetPerkLevel("shieldDuration");
            return level == 1 ? 2f : level == 2 ? 3f : 1f;
        }

        public IEnumerable<string> GetAllPerkNames() => maxLevels.Keys;

        public string GetDisplayName(string name) => displayNames.ContainsKey(name) ? displayNames[name] : name;
        public string GetDescription(string name) => descriptions.ContainsKey(name) ? descriptions[name] : "";
        public string GetUpgradeDescription(string name) => upgradeDescriptions.ContainsKey(name) ? upgradeDescriptions[name] : "";
        public int GetMaxLevel(string name) => maxLevels.ContainsKey(name) ? maxLevels[name] : 1;

        public void SetPerkLevel(string perkKey, int level)
        {
            perkLevels[perkKey] = level;
            Logger.LogInfo($"Perk {perkKey} set to level {level}");
        }

        public void OnSelected(string name)
        {
            if (name == "shieldAbility")
            {
                EnableShieldAbility();
            }
        }
    }
}