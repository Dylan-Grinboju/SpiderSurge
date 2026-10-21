using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using SpiderSurge.Common;

namespace SpiderSurge
{
    internal static class AbilitySelection
    {
        private static readonly AccessTools.FieldRef<SurvivalMode, SurvivalConfig> ActiveConfig = AccessTools.FieldRefAccess<SurvivalMode, SurvivalConfig>("_config");

        private static bool pending;
        public static bool IsChoosing { get; private set; }

        // Only the host runs the starting choice; a client knows it is on screen from the synced first-selection flag
        public static bool StartingChoiceOpen => Net.IsRemoteClient
            ? PerksManager.Instance != null && PerksManager.Instance.IsFirstNormalPerkSelection
            : IsChoosing;

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
            foreach (string key in PerksManager.Instance.GetAbilityPerkNames())
            {
                int id = manager.GetModId(key);
                if (id >= 0) abilities.Add(manager.GetModById(id));
            }
            if (abilities.Count == 0)
            {
                Silk.Logger.LogError("Surge could not find any abilities for the starting choice");
                pending = false;
                PerksManager.Instance.IsFirstNormalPerkSelection = false;
                return true;
            }
            abilities = abilities.OrderBy(_ => UnityEngine.Random.value).Take(ChoiceCount(SurvivalMode.instance)).ToList();
            var maps = abilities.Select(_ => SurvivalMode.instance._defaultConfig.maps[0]).ToList();

            IsChoosing = true;
            TimeScaleController.instance.Pause();
            ModifierManager_SetModLevel_Patch.PlayPowerUpSound();
            hud.ShowPerkChoices(abilities, maps);
            hud.perkChoiceView.InstantShow();
            return false;
        }

        // As many cards as a normal perk choice: the pain level's base count (2 from pain level 3) plus Picky
        private static int ChoiceCount(SurvivalMode mode)
        {
            SurvivalConfig config = ActiveConfig(mode);
            if (config == null) config = mode._defaultConfig;
            return Math.Max(1, config.basePerkChoiceCount + ModifierManager.instance.GetModLevel("picky"));
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
