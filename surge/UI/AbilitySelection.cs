using System.Collections.Generic;
using HarmonyLib;

namespace SpiderSurge
{
    internal static class AbilitySelection
    {
        private static bool pending;
        public static bool IsChoosing { get; private set; }

        public static void Arm()
        {
            Reset();
            pending = SurgeGameModeManager.IsSurgeRunActive && !SurgeGameModeManager.AbilitiesDisabled;
        }

        public static void Reset(bool instant = true)
        {
            if (IsChoosing)
            {
                var hud = SurvivalModeHud.instance;
                if (hud != null)
                {
                    if (instant) hud.ClosePerkChoiseView();
                    else hud.perkChoiceView.Hide();
                }
                TimeScaleController.instance?.Reset();
                GameController.instance?.EnableInputs();
            }
            pending = false;
            IsChoosing = false;
        }

        // Hold the first wave on its loaded map until the shared ability has been chosen.
        public static bool BeforeWave()
        {
            if (!pending) return true;
            if (IsChoosing) return false;
            var manager = ModifierManager.instance;
            var hud = SurvivalModeHud.instance;
            if (manager == null || hud == null || PerksManager.Instance == null) return false;

            var abilities = new List<Modifier>();
            var maps = new List<GameLevel>();
            foreach (string key in PerksManager.Instance.GetAbilityPerkNames())
            {
                int id = manager.GetModId(key);
                if (id < 0) continue;
                abilities.Add(manager.GetModById(id));
                maps.Add(SurvivalMode.instance._defaultConfig.maps[0]);
            }
            if (abilities.Count == 0)
            {
                Silk.Logger.LogError("Surge could not find any abilities for the starting choice");
                pending = false;
                PerksManager.Instance.IsFirstNormalPerkSelection = false;
                return true;
            }

            IsChoosing = true;
            TimeScaleController.instance.Pause();
            ModifierManager_SetModLevel_Patch.PlayPowerUpSound();
            hud.ShowPerkChoices(abilities, maps);
            hud.perkChoiceView.InstantShow();
            return false;
        }

        public static void Choose(Modifier modifier)
        {
            if (!IsChoosing || modifier?.data == null || !PerksManager.Instance.IsAbilityPerk(modifier.data.key)) return;
            ModifierManager.instance.SetModLevel(modifier, GameMode.Wave, 1);
            PerksManager.Instance.IsFirstNormalPerkSelection = false;
            Logging.SpiderSurgeStatsManager.RunSafely("StartingAbility", stats => stats.OnPerkChosen(modifier));
            if (StatsMod.GameSessionManager.Instance?.IsActive == true)
                StatsMod.GameSessionManager.Instance.RecordPerk(PerksManager.Instance.GetDisplayName(modifier.data.key));
            Reset(instant: false);
            PlayerAbilityHandler.AttachToExistingSpiders();
            PerksManager.Instance.RefreshAbilityRegistrations();
            SurgeNet.SendStateNow();
        }
    }

    [HarmonyPatch(typeof(SurvivalMode), "StartWave")]
    internal static class StartingAbilityWavePatch
    {
        static bool Prefix() => AbilitySelection.BeforeWave();
    }

    [HarmonyPatch(typeof(SurvivalModeHud), "PerkChoiceSubmit")]
    internal static class StartingAbilitySubmitPatch
    {
        static bool Prefix(Modifier modifier)
        {
            if (!AbilitySelection.IsChoosing) return true;
            AbilitySelection.Choose(modifier);
            return false;
        }
    }
}
