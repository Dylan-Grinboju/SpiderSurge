using UnityEngine;

namespace SpiderSurge
{
    public class SurgeGameModeManager : MonoBehaviour
    {
        public const string DisplayName = "SURGE SURVIVAL";

        public static SurgeGameModeManager Instance { get; private set; }

        public bool IsActive { get; private set; }

        public bool RunUsedSurge { get; private set; }

        public static bool IsModeEnabled { get; private set; }

        internal static void SelectMode(bool surge)
        {
            IsModeEnabled = surge;
            SurgeNet.MarkDirty();
        }

        public static bool IsSurgeRunActive => Instance != null && Instance.IsActive;

        public static bool WasSurgeRunUsed => Instance != null && Instance.RunUsedSurge;

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
            SurgeNet.MarkDirty();
        }

        internal void ForceActive(bool active)
        {
            IsActive = active;
            SurgeNet.MarkDirty();
        }

        public void ResetRun()
        {
            SetActive(false);
            RunUsedSurge = false;
        }

        internal void ApplyRemote(bool active, bool runUsedSurge)
        {
            IsActive = active;
            RunUsedSurge = runUsedSurge;
        }

        private void LateUpdate()
        {
            SurgeNet.Flush();
        }
    }
}
