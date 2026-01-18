using UnityEngine;
using UnityEngine.InputSystem;
using HarmonyLib;
using System.Collections.Generic;
using Logger = Silk.Logger;

namespace SpiderSurge
{
    public class PerksManager : MonoBehaviour
    {
        public static PerksManager Instance { get; private set; }

        private Dictionary<PlayerInput, int> shieldCharges = new Dictionary<PlayerInput, int>();
        public bool IsShieldAbilityUnlocked { get; private set; }

        private Dictionary<string, string> displayNames = new Dictionary<string, string>
        {
            ["shieldAbility"] = "Shield Ability",
            ["capacity"] = "Shield Capacity",
            ["stillness"] = "Stillness Charge",
            ["airborne"] = "Airborne Charge",
            ["shieldCooldown"] = "Shield Cooldown",
            ["shieldDuration"] = "Shield Duration"
        };

        private Dictionary<string, string> descriptions = new Dictionary<string, string>
        {
            ["shieldAbility"] = "Unlocks the shield ability.",
            ["capacity"] = "Increases shield charge capacity to 2.",
            ["stillness"] = "Gain shield charge after 10s of stillness.",
            ["airborne"] = "Gain shield charge after 10s airborne.",
            ["shieldCooldown"] = "Reduces shield cooldown from 30s to 20s.",
            ["shieldDuration"] = "Increases shield duration to 2s."
        };

        private Dictionary<string, string> upgradeDescriptions = new Dictionary<string, string>
        {
            ["shieldAbility"] = "",
            ["capacity"] = "Increases shield charge capacity to 3.",
            ["stillness"] = "Gain shield charge after 5s of stillness.",
            ["airborne"] = "Gain shield charge after 5s airborne.",
            ["shieldCooldown"] = "Reduces shield cooldown from 20s to 10s.",
            ["shieldDuration"] = "Increases shield duration to 3s."
        };

        private Dictionary<string, int> maxLevels = new Dictionary<string, int>
        {
            ["shieldAbility"] = 1,
            ["capacity"] = 2,
            ["stillness"] = 2,
            ["airborne"] = 2,
            ["shieldCooldown"] = 2,
            ["shieldDuration"] = 2
        };

        private Dictionary<string, List<string>> dependencies = new Dictionary<string, List<string>>
        {
            ["shieldAbility"] = new List<string>(),
            ["capacity"] = new List<string> { "shieldAbility" },
            ["stillness"] = new List<string> { "shieldAbility" },
            ["airborne"] = new List<string> { "shieldAbility" },
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
                if (PlayerStateTracker.Instance == null)
                {
                    GameObject tracker = new GameObject("PlayerStateTracker");
                    tracker.AddComponent<PlayerStateTracker>();
                }

                // Create Shield Charge UI
                GameObject uiObj = new GameObject("ShieldChargeUI");
                uiObj.AddComponent<ShieldChargeUI>();
                DontDestroyOnLoad(uiObj);
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

                // Register with managers
                PerksManager.Instance.RegisterPlayer(playerInput);
                if (PlayerStateTracker.Instance != null)
                {
                    PlayerStateTracker.Instance.RegisterPlayer(playerInput);
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

        public int GetShieldChargeCap()
        {
            return GetShieldCapacity();
        }

        public int GetShieldCharges(PlayerInput playerInput)
        {
            return shieldCharges.ContainsKey(playerInput) ? shieldCharges[playerInput] : 0;
        }

        public void SetShieldCharges(PlayerInput playerInput, int charges)
        {
            int cap = GetShieldChargeCap();
            shieldCharges[playerInput] = Mathf.Min(charges, cap);
        }

        public void AddShieldCharge(PlayerInput playerInput)
        {
            int current = GetShieldCharges(playerInput);
            SetShieldCharges(playerInput, current + 1);
        }

        public void ConsumeShieldCharge(PlayerInput playerInput)
        {
            int current = GetShieldCharges(playerInput);
            if (current > 0)
            {
                SetShieldCharges(playerInput, current - 1);
            }
        }

        public void RegisterPlayer(PlayerInput playerInput)
        {
            if (!shieldCharges.ContainsKey(playerInput))
            {
                shieldCharges[playerInput] = 0;
            }
        }

        public void UnregisterPlayer(PlayerInput playerInput)
        {
            if (shieldCharges.ContainsKey(playerInput))
            {
                shieldCharges.Remove(playerInput);
            }
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

        public int GetShieldCapacity()
        {
            int baseCap = 1;
            int level = GetPerkLevel("capacity");
            return baseCap + level; // level 1: +1, level 2: +2
        }

        public float GetStillnessDuration()
        {
            int level = GetPerkLevel("stillness");
            return level == 1 ? 15f : level == 2 ? 10f : 0f;
        }

        public float GetAirborneDuration()
        {
            int level = GetPerkLevel("airborne");
            return level == 1 ? 10f : level == 2 ? 5f : 0f;
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

        public string GetDisplayName(string name) => displayNames[name];
        public string GetDescription(string name) => descriptions[name];
        public string GetUpgradeDescription(string name) => upgradeDescriptions[name];
        public int GetMaxLevel(string name) => maxLevels[name];

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