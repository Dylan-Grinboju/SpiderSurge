using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using SpiderSurge.Common;
using Unity.Netcode;
using UnityEngine.SceneManagement;

namespace SpiderSurge
{
    internal static class AbilitySelection
    {
        private static readonly AccessTools.FieldRef<SurvivalMode, SurvivalConfig> ActiveConfig = AccessTools.FieldRefAccess<SurvivalMode, SurvivalConfig>("_config");
        private static readonly MethodInfo ShowCards = AccessTools.Method(typeof(SurvivalModeHud), "ShowPerkChoicesClientRpc");
        private static readonly MethodInfo SubmitCard = AccessTools.Method(typeof(SurvivalModeHud), "PerkChoiceSubmitClientRpc");

        private static bool pending;
        private static NetworkSceneManager _scenes;
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
            Listen(false);
            pending = false;
            IsChoosing = false;
        }

        // Hold the first wave on its loaded map until the shared ability has been chosen.
        public static bool BeforeWave()
        {
            if (!pending) return true;
            if (IsChoosing) return false;
            // The game holds joiners until the loading screen has finished, which takes game time a paused choice never gives it
            if (LevelController.IsLoading) return false;
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
            int[] ids = abilities.Select(ability => manager.GetModId(ability.data.key)).ToArray();

            IsChoosing = true;
            TimeScaleController.instance.Pause();
            ModifierManager_SetModLevel_Patch.PlayPowerUpSound();
            // Vanilla clients have no Surge abilities to build cards from, so only the host and modded clients get them
            ShowCards.Invoke(hud, new object[] { ids, new int[ids.Length], Net.ServerId, false });
            hud.perkChoiceView.InstantShow();
            Logging.SpiderSurgeStatsManager.RunSafely("StartingAbility", stats => stats.OnPerkChoicesShown(abilities));
            Listen(true);
            foreach (ulong client in Net.ModdedClients) SendTo(client);
            return false;
        }

        // As many cards as a normal perk choice: the pain level's base count (2 from pain level 3) plus Picky
        private static int ChoiceCount(SurvivalMode mode)
        {
            SurvivalConfig config = ActiveConfig(mode);
            if (config == null) config = mode._defaultConfig;
            return Math.Max(1, config.basePerkChoiceCount + ModifierManager.instance.GetModLevel("picky"));
        }

        // A client still loading the level closes the cards when its scene loads, so it gets them again once it is done, as does a client that joins now
        private static void Listen(bool on)
        {
            if (_scenes != null) _scenes.OnLoadComplete -= OnClientLoaded;
            Net.PeerReady -= SendTo;
            _scenes = on && Net.IsHost ? Net.Manager.SceneManager : null;
            if (_scenes != null) _scenes.OnLoadComplete += OnClientLoaded;
            if (on) Net.PeerReady += SendTo;
        }

        private static void OnClientLoaded(ulong clientId, string sceneName, LoadSceneMode mode) => SendTo(clientId);

        // The game's own late-join path: the host's cards and the current votes, sent to one client
        private static void SendTo(ulong clientId)
        {
            var hud = SurvivalModeHud.instance;
            if (!IsChoosing || hud == null || clientId == Net.ServerId || !Net.ClientHasMod(clientId)) return;
            hud.ShowPerkChoicesServerRpc(clientId);
        }

        public static void Choose(Modifier modifier, int index)
        {
            if (!IsChoosing || modifier?.data == null || !PerksManager.Instance.IsAbilityPerk(modifier.data.key)) return;
            ModifierManager.instance.SetModLevel(modifier, GameMode.Wave, 1);
            PerksManager.Instance.IsFirstNormalPerkSelection = false;
            Logging.SpiderSurgeStatsManager.RunSafely("StartingAbility", stats => stats.OnPerkChosen(modifier));
            if (StatsMod.GameSessionManager.Instance?.IsActive == true)
                StatsMod.GameSessionManager.Instance.RecordPerk(PerksManager.Instance.GetDisplayName(modifier.data.key));
            // The game's submit message closes the cards on every client and greys out the ones not taken
            SubmitCard.Invoke(SurvivalModeHud.instance, new object[] { index });
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
        static bool Prefix(Modifier modifier, int index)
        {
            if (!AbilitySelection.IsChoosing) return true;
            AbilitySelection.Choose(modifier, index);
            return false;
        }
    }
}
