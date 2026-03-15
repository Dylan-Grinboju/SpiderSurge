using UnityEngine;

namespace SpiderSurge
{
    public class SurgeGameModeManager : MonoBehaviour
    {
        public static SurgeGameModeManager Instance { get; private set; }

        public bool IsActive { get; private set; }
        
        public bool RunUsedSurge { get; private set; }

        public static bool IsModeEnabled => ModConfig.enableSurgeMode;

        public static bool IsSurgeRunActive => IsModeEnabled && Instance != null && Instance.IsActive;
        
        public static bool WasSurgeRunUsed => IsModeEnabled && Instance != null && Instance.RunUsedSurge;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        public void SetActive(bool active)
        {
            IsActive = IsModeEnabled && active;
            if (active)
            {
                RunUsedSurge = true;
            }
        }

        public void ResetRun()
        {
            RunUsedSurge = false;
        }

    }
}