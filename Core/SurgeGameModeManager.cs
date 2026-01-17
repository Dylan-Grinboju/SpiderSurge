using Silk;
using Logger = Silk.Logger;
using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

namespace SpiderSurge
{
    public class SurgeGameModeManager : MonoBehaviour
    {
        public static SurgeGameModeManager Instance { get; private set; }

        public bool IsActive { get; private set; }
        public bool IsShieldAbilityUnlocked { get; private set; }

        private Dictionary<string, int> perkLevels = new Dictionary<string, int>();
        private Dictionary<PlayerInput, int> shieldCharges = new Dictionary<PlayerInput, int>();

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                Logger.LogInfo("SurgeGameModeManager initialized and set to persist across scenes");
            }
            else
            {
                Destroy(gameObject);
            }
        }

        public void SetActive(bool active)
        {
            IsActive = active;
        }

        public void UnlockShieldAbility()
        {
            IsShieldAbilityUnlocked = true;
            Logger.LogInfo("Shield ability unlocked");
        }

        public void SetPerkLevel(string perkKey, int level)
        {
            perkLevels[perkKey] = level;
            Logger.LogInfo($"Perk {perkKey} set to level {level}");
        }

        public int GetPerkLevel(string perkKey)
        {
            return perkLevels.ContainsKey(perkKey) ? perkLevels[perkKey] : 0;
        }

        public int GetShieldChargeCap()
        {
            int cap = 1; // Base
            if (GetPerkLevel("shieldCap2") > 0) cap = 2;
            if (GetPerkLevel("shieldCap3") > 0) cap = 3;
            return cap;
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

        public void ResetShieldCharges()
        {
            shieldCharges.Clear();
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
    }
}