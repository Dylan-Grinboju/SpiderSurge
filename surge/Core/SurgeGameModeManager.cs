using UnityEngine;

namespace SpiderSurge
{
    internal enum RunEnd
    {
        Stopped,
        Lobby,
        Starting,
        LeftHost,
        Versus,
    }

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
            if (IsModeEnabled != surge)
            {
                IsModeEnabled = surge;
                SurgePainLevels.Refresh(PainLevelsScreen.instance);
            }
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
            if (!active)
            {
                AbilitySelection.Reset();
                EncounterHints.ResetRun();
            }
            IsActive = IsModeEnabled && active;
            if (active)
            {
                RunUsedSurge = true;
                NoAbilitiesThisRun = IsActive && ModConfig.NoAbilities && !SurgePainLevels.SelectedEight;
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

        // Every way a run ends or a new one begins resets the run here. Stopping and the lobby keep RunUsedSurge and reset
        // perks only after an active run; a new run and leaving a host start clean; versus switches its forced run off.
        internal static void EndRun(RunEnd reason)
        {
            bool wasActive = IsSurgeRunActive;
            bool clean = reason == RunEnd.Starting || reason == RunEnd.LeftHost;
            if (reason == RunEnd.Versus) Instance?.ForceActive(false);
            else if (clean) Instance?.ResetRun();
            else if (wasActive) Instance.SetActive(false);

            var perks = PerksManager.Instance;
            if (perks != null && (wasActive || clean || reason == RunEnd.Versus))
            {
                perks.ResetPerks();
                if (reason == RunEnd.Versus) perks.IsFirstNormalPerkSelection = false;
            }
            ModifierManager_GetNonMaxedSurvivalMods_Patch.LuckyPerkKeys.Clear();
            if (reason == RunEnd.Starting) SurgeSpawnPressure.Reset();

            StoragePersistenceManager.ClearAllStoredWeapons();
            AbilityAchievementTracker.ClearWave();
            if (reason != RunEnd.Versus) ChaosWallet.ResetRun();
            if (reason == RunEnd.Stopped || reason == RunEnd.Lobby)
            {
                PerkShopPanel.Clear();
                PerkConfirmButton.Clear();
                PerkHint.Clear();
            }

            if (reason == RunEnd.Lobby || reason == RunEnd.LeftHost)
            {
                SelectMode(false);
            }
        }

        internal void ApplyRemote(bool active, bool runUsedSurge, bool noAbilities)
        {
            IsActive = active;
            RunUsedSurge = runUsedSurge;
            NoAbilitiesThisRun = noAbilities;
        }

        private void LateUpdate()
        {
            AbilitySelection.Tick();
            if (AbilitySelection.IsChoosing) PersonalChoicePanel.Show();
            SurgeNet.Flush();
            ChaosNet.Flush();
            ChallengeController.Tick();
            ChaosHud.Tick();
        }
    }
}
