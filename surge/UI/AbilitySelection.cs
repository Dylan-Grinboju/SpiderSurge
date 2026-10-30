using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using SpiderSurge.Common;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SpiderSurge
{
    internal static class AbilitySelection
    {
        private const string ChoiceChannel = "surge.personal-choice";
        private static readonly MethodInfo ReEnableInputs = AccessTools.Method(typeof(LevelController), "ReEnableInputs");
        internal static readonly PersonalChoiceRound Round = new PersonalChoiceRound();
        private static readonly Dictionary<int, PlayerController> Owners = new Dictionary<int, PlayerController>();
        private static bool pending;
        private static bool ultimateEarned;
        private static bool initialized;
        private static float completeAt = -1f;
        private static float offeredAt;
        private static int offeredWave;
        private static Coroutine loading;
        private static LevelController loadingController;
        private static List<Modifier> deferredModifiers;
        private static List<GameLevel> deferredMaps;
        private static string[] abilityOffers;
        private static string[] ultimateOffers;

        public static bool IsChoosing => Round.Open;
        public static bool StartingChoiceOpen => IsChoosing || (PerksManager.Instance?.IsFirstNormalPerkSelection ?? false);

        public static void Initialize()
        {
            if (initialized) return;
            initialized = true;
            Net.RegisterFromClients(ChoiceChannel, OnChoice, offline: true);
        }

        public static void Shutdown()
        {
            Net.Unregister(ChoiceChannel);
            initialized = false;
            Reset();
        }

        public static void Arm()
        {
            Reset();
            pending = SurgeGameModeManager.IsSurgeRunActive && !SurgeGameModeManager.AbilitiesDisabled;
        }

        public static void Reset(bool instant = true)
        {
            if (loading != null && loadingController != null) loadingController.StopCoroutine(loading);
            loading = null;
            loadingController = null;
            if (IsChoosing) Release();
            Round.Clear();
            PersonalChoicePanel.Hide();
            pending = ultimateEarned = false;
            abilityOffers = ultimateOffers = null;
            deferredModifiers = null;
            deferredMaps = null;
            completeAt = -1f;
            Owners.Clear();
        }

        public static bool HoldLoading(LevelController controller)
        {
            if (!SurgeGameModeManager.IsSurgeRunActive || SurgeGameModeManager.AbilitiesDisabled
                || !(Net.IsAuthority ? pending : StartingChoiceOpen)) return false;
            string scene = SceneManager.GetActiveScene().name;
            if (scene == controller.lobby.name || scene == controller.waitingRoom.name) return false;
            if (loading != null) return true;
            controller.CancelInvoke("ReEnableInputs");
            loadingController = controller;
            loading = controller.StartCoroutine(FinishLoading(controller, SceneManager.GetActiveScene().handle));
            return true;
        }

        private static IEnumerator FinishLoading(LevelController controller, int scene)
        {
            try
            {
                yield return new WaitForSecondsRealtime(1f);
                while (Net.IsRemoteClient && StartingChoiceOpen && !IsChoosing)
                {
                    if (controller == null || SceneManager.GetActiveScene().handle != scene) yield break;
                    yield return null;
                }
                if (controller != null && SceneManager.GetActiveScene().handle == scene)
                {
                    ReEnableInputs.Invoke(controller, null);
                    if (Net.IsAuthority) BeforeWave();
                    if (IsChoosing) GameController.instance?.DisableInputs();
                }
            }
            finally
            {
                loading = null;
                loadingController = null;
            }
        }

        // New arrivals choose at the next wave boundary; existing players keep their loadouts.
        public static bool BeforeWave()
        {
            if (!Net.IsAuthority || !SurgeGameModeManager.IsSurgeRunActive || SurgeGameModeManager.AbilitiesDisabled) return true;
            if (IsChoosing) return false;
            if (LevelController.IsLoading) return !pending;
            if (PerksManager.Instance == null || ModifierManager.instance == null) return false;
            ReconcileOwners();
            if (Missing(false).Any()) { Begin(false); return false; }
            if ((ultimateEarned || SurgePainLevels.ActiveEight) && Missing(true).Any()) { Begin(true); return false; }
            pending = false;
            return true;
        }

        private static IEnumerable<int> Missing(bool ultimate) => (Players.All ?? new List<PlayerController>())
            .Where(player => player != null && PerksManager.Instance.Loadouts.Get(player.playerID.Value, ultimate) == null)
            .Select(player => player.playerID.Value);

        private static bool ReconcileOwners()
        {
            var players = Players.All;
            if (players == null) return false;
            bool changed = false;
            foreach (int id in Owners.Keys.ToArray())
                if (Owners[id] == null || Players.ById(id) != Owners[id]) { PerksManager.Instance.Loadouts.Remove(id); Round.Picks.Remove(id); Owners.Remove(id); changed = true; }
            foreach (var player in players)
                if (player != null) Owners[player.playerID.Value] = player;
            if (changed && IsChoosing) Round.InvalidateRequests();
            return changed;
        }

        private static void Begin(bool ultimate)
        {
            var perks = PerksManager.Instance;
            string[] offers = ultimate ? ultimateOffers : abilityOffers;
            if (offers == null)
            {
                var keys = ultimate ? perks.GetAbilityUltimatePerkNames() : perks.GetAbilityPerkNames();
                offers = keys.Where(key => ModifierManager.instance.GetModId(key) >= 0)
                    .OrderBy(_ => UnityEngine.Random.value).Take(ChoiceCount()).ToArray();
                if (ultimate) ultimateOffers = offers;
                else abilityOffers = offers;
            }
            if (offers.Length == 0) throw new InvalidOperationException("No registered Surge choices");
            Round.Begin(offers, Missing(ultimate), ultimate);
            offeredAt = Time.realtimeSinceStartup;
            offeredWave = SurvivalMode.instance != null ? SurvivalMode.instance.CurrentWave : 1;
            completeAt = -1f;
            TimeScaleController.instance?.Pause();
            GameController.instance?.DisableInputs();
            PersonalChoicePanel.Show();
            ModifierManager_SetModLevel_Patch.PlayPowerUpSound();
            SurgeNet.SendStateNow();
        }

        private static int ChoiceCount()
        {
            var mode = SurvivalMode.instance;
            var config = mode != null ? GameFields.ActiveConfig(mode) ?? mode._defaultConfig : null;
            return Math.Max(1, (config != null ? config.basePerkChoiceCount : 3) + ModifierManager.instance.GetModLevel("picky"));
        }

        internal static bool DeferPerkScreen(List<Modifier> modifiers, List<GameLevel> maps)
        {
            if (!Net.IsAuthority || !SurgeGameModeManager.IsSurgeRunActive || SurgeGameModeManager.AbilitiesDisabled
                || !(PerksManager.Instance?.IsUltUpgradePerkSelection ?? false)) return false;
            PerksManager.Instance.IsUltUpgradePerkSelection = false;
            ultimateEarned = true;
            deferredModifiers = modifiers;
            deferredMaps = maps;
            ReconcileOwners();
            Begin(true);
            return true;
        }

        internal static void Tick()
        {
            if (!IsChoosing) return;
            TimeScaleController.instance?.Pause();
            GameController.instance?.DisableInputs();
            if (!Net.IsAuthority) return;
            bool ownersChanged = ReconcileOwners();
            bool picksChanged = Round.Reconcile(Missing(Round.Ultimate));
            if (ownersChanged || picksChanged) { completeAt = -1f; SurgeNet.SendStateNow(); }
            if (!Round.Complete || (HudController.instance != null && HudController.instance.paused)) { completeAt = -1f; return; }
            if (completeAt < 0f) completeAt = Time.unscaledTime + 0.5f;
            if (Time.unscaledTime >= completeAt) Finish();
        }

        internal static void Choose(int player, int index, bool ready)
        {
            if (!IsChoosing || !Players.IsLocal(player) || !Round.Picks.TryGetValue(player, out var pick)) return;
            int revision = pick.Revision + 1;
            if (Net.IsRemoteClient) Round.Choose(Round.Epoch, player, index, ready, revision);
            Net.SendToServer(ChoiceChannel, writer => { writer.Write(Round.Epoch); writer.Write(player); writer.Write(index); writer.Write(ready); writer.Write(revision); writer.Write(Players.ById(player).NetworkObjectId); });
        }

        private static void OnChoice(ulong sender, BinaryReader reader)
        {
            int epoch = reader.ReadInt32();
            int player = reader.ReadInt32();
            int index = reader.ReadInt32();
            bool ready = reader.ReadBoolean();
            int revision = reader.ReadInt32();
            ulong identity = reader.ReadUInt64();
            var owner = Players.ById(player);
            if (owner == null || owner.NetworkObjectId != identity) return;
            if (ReconcileOwners()) SurgeNet.SendStateNow();
            if (!Players.MayActFor(player, sender) || !Round.Choose(epoch, player, index, ready, revision)) return;
            completeAt = -1f;
            SurgeNet.SendStateNow();
        }

        private static void Finish()
        {
            var perks = PerksManager.Instance;
            foreach (var pair in Round.Picks)
            {
                string key = Round.Offers[pair.Value.Index];
                perks.ChooseForPlayer(pair.Key, key, Round.Ultimate);
                Logging.SpiderSurgeStatsManager.RunSafely("PersonalChoice", stats => stats.OnPersonalChoice(pair.Key, offeredWave, Round.Ultimate, Round.Offers, key, Time.realtimeSinceStartup - offeredAt));
                var session = StatsMod.GameSessionManager.Instance;
                if (session?.IsActive == true) session.RecordPerk($"{Players.NameOf(pair.Key)}: {perks.GetDisplayName(key)}");
            }
            bool wasUltimate = Round.Ultimate;
            Round.Clear();
            PersonalChoicePanel.Hide();
            perks.IsFirstNormalPerkSelection = false;
            pending = false;
            PlayerAbilityHandler.AttachToExistingSpiders();
            perks.RefreshAbilityRegistrations();
            if (!wasUltimate && (ultimateEarned || SurgePainLevels.ActiveEight) && Missing(true).Any()) { Begin(true); return; }
            Release();
            SurgeNet.SendStateNow();
            if (deferredModifiers != null)
            {
                var modifiers = deferredModifiers;
                var maps = deferredMaps;
                deferredModifiers = null;
                deferredMaps = null;
                SurvivalModeHud.instance.ShowPerkChoices(modifiers, maps);
            }
        }

        private static void Release()
        {
            TimeScaleController.instance?.Reset();
            if (!GameMenus.PerkChoiceOpen && !(HudController.instance != null && HudController.instance.paused))
                GameController.instance?.EnableInputs();
        }

        internal static void Write(BinaryWriter writer)
        {
            (PerksManager.Instance?.Loadouts ?? new PersonalLoadouts()).Write(writer);
            Round.Write(writer);
        }

        internal static void Read(BinaryReader reader)
        {
            bool wasOpen = IsChoosing;
            int oldEpoch = Round.Epoch;
            var localPicks = Round.Picks.Where(pair => Players.IsLocal(pair.Key)).ToArray();
            (PerksManager.Instance?.Loadouts ?? new PersonalLoadouts()).Read(reader);
            Round.Read(reader);
            if (Round.Epoch == oldEpoch)
                foreach (var pair in localPicks)
                    if (Round.Picks.TryGetValue(pair.Key, out var pick) && pair.Value.Revision > pick.Revision) Round.Picks[pair.Key] = pair.Value;
            if (IsChoosing)
            {
                TimeScaleController.instance?.Pause();
                GameController.instance?.DisableInputs();
                PersonalChoicePanel.Show();
            }
            else if (wasOpen) { PersonalChoicePanel.Hide(); Release(); }
            PerksManager.Instance?.RefreshAbilityRegistrations();
        }
    }

    [HarmonyPatch(typeof(LevelController), nameof(LevelController.HideLoadingScene))]
    internal static class StartingAbilityLoadingPatch
    {
        static bool Prefix(LevelController __instance) => !AbilitySelection.HoldLoading(__instance);
    }

    [HarmonyPatch(typeof(SurvivalMode), "StartWave")]
    internal static class StartingAbilityWavePatch
    {
        static bool Prefix() => AbilitySelection.BeforeWave();
    }

    [HarmonyPatch(typeof(SurvivalModeHud), nameof(SurvivalModeHud.ShowPerkChoices))]
    internal static class PersonalUltimateScreenPatch
    {
        [HarmonyPriority(Priority.First)]
        static bool Prefix(List<Modifier> modifiers, List<GameLevel> gameLevels) => !AbilitySelection.DeferPerkScreen(modifiers, gameLevels);
    }

    [HarmonyPatch(typeof(SurvivalModeHud), nameof(SurvivalModeHud.ShowPerkChoicesServerRpc))]
    internal static class PersonalChoiceJoinPatch
    {
        static bool Prefix() => !AbilitySelection.IsChoosing;
    }
}
