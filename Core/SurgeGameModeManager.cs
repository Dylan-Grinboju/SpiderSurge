using Silk;
using Logger = Silk.Logger;

namespace SpiderSurge
{
    public class SurgeGameModeManager
    {
        public static SurgeGameModeManager Instance { get; private set; }

        public bool IsActive { get; private set; }
        public bool IsShieldAbilityUnlocked { get; private set; }
        public static bool AbilitiesEnabled => Instance != null && Instance.IsActive;

        public void SetActive(bool active)
        {
            IsActive = active;
        }

        public void UnlockShieldAbility()
        {
            IsShieldAbilityUnlocked = true;
            Logger.LogInfo("Shield ability unlocked");
        }

        public SurgeGameModeManager()
        {
            Instance = this;
        }
    }
}