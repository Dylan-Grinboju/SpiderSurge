using Silk;
using Logger = Silk.Logger;

namespace SpiderSurge
{
    public class SurgeModeManager
    {
        public static SurgeModeManager Instance { get; private set; }

        public bool IsActive { get; private set; }
        public static bool AbilitiesEnabled => Instance != null && Instance.IsActive;

        public void SetActive(bool active)
        {
            IsActive = active;
        }

        public SurgeModeManager()
        {
            Instance = this;
        }
    }
}