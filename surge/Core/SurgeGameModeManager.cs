using UnityEngine;

namespace SpiderSurge
{
    public class SurgeGameModeManager : MonoBehaviour
    {
        public const string DisplayName = "SURGE SURVIVAL";

        public static SurgeGameModeManager Instance { get; private set; }

        public bool IsActive { get; private set; }

        public bool RunUsedSurge { get; private set; }

        public bool NoAbilitiesThisRun { get; private set; }

        public static bool IsModeEnabled { get; private set; }

        internal static void SelectMode(bool surge)
        {
            IsModeEnabled = surge;
            SurgeNet.MarkDirty();
        }

        public static bool IsSurgeRunActive => Instance != null && Instance.IsActive;

        public static bool WasSurgeRunUsed => Instance != null && Instance.RunUsedSurge;

        // Read once when a Surge Survival run starts, so changing the setting mid-run waits for the next run
        public static bool AbilitiesDisabled => IsSurgeRunActive && Instance.NoAbilitiesThisRun;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                gameObject.AddComponent<EncounterHints>();
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        public void SetActive(bool active)
        {
            if (!active) AbilitySelection.Reset();
            IsActive = IsModeEnabled && active;
            if (active)
            {
                RunUsedSurge = true;
                NoAbilitiesThisRun = IsActive && ModConfig.NoAbilities;
            }
            SurgeNet.MarkDirty();
        }

        internal void ForceActive(bool active)
        {
            IsActive = active;
            NoAbilitiesThisRun = false;
            SurgeNet.MarkDirty();
        }

        public void ResetRun()
        {
            SetActive(false);
            RunUsedSurge = false;
        }

        internal void ApplyRemote(bool active, bool runUsedSurge, bool noAbilities)
        {
            IsActive = active;
            RunUsedSurge = runUsedSurge;
            NoAbilitiesThisRun = noAbilities;
        }

        private void LateUpdate()
        {
            SurgeNet.Flush();
            ChaosNet.Flush();
            ChallengeController.Tick();
            ChaosHud.Tick();
        }
    }
}
