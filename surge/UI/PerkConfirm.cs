using System;
using System.Collections.Generic;
using HarmonyLib;
using SpiderSurge.Common;
using UnityEngine;
using UnityEngine.UI;
using Logger = Silk.Logger;

namespace SpiderSurge
{
    // With unlimited choosing time a card click only locks the card; the Confirm button under the cards submits it through the game's own path
    public static class PerkConfirm
    {
        private static readonly AccessTools.FieldRef<SurvivalModifierChoiceCard, int> CardIndex = AccessTools.FieldRefAccess<SurvivalModifierChoiceCard, int>("cardIndex");
        private static readonly AccessTools.FieldRef<SurvivalModifierChoiceCard, bool> Picked = AccessTools.FieldRefAccess<SurvivalModifierChoiceCard, bool>("_picked");

        public static SurvivalModifierChoiceCard Locked { get; private set; }
        public static event Action LockedChanged;

        // The unlimited-time setting is shared, so a client reads the host's; the starting ability choice has no timer either
        public static bool Active => SurgeGameModeManager.IsSurgeRunActive && (ModConfig.UnlimitedPerkChoosingTime || AbilitySelection.StartingChoiceOpen);

        public static void Lock(SurvivalModifierChoiceCard card)
        {
            if (card == null || card == Locked) return;
            Unlock();
            Locked = card;
            PerkCardScale.Of(card).SetLocked(true);
            var background = card.GetComponent<Image>();
            if (background != null) background.color = Consts.Values.Chaos.LockedCardTint;
            LockedChanged?.Invoke();
        }

        public static void Reset()
        {
            Unlock();
            LockedChanged?.Invoke();
        }

        private static void Unlock()
        {
            if (Locked != null)
            {
                PerkCardScale.Of(Locked).SetLocked(false);
                var background = Locked.GetComponent<Image>();
                if (background != null) background.color = Color.white;
            }
            Locked = null;
        }

        // The same call a card click makes in the normal game, so the host picks and a client votes
        public static void Submit()
        {
            var card = Locked;
            var hud = SurvivalModeHud.instance;
            if (card == null || card.modifier == null || hud == null) return;
            Picked(card) = true;
            hud.PerkChoiceSubmit(card.modifier, card.level, CardIndex(card));
        }
    }

    [HarmonyPatch(typeof(SurvivalModifierChoiceCard), "PickModifier")]
    public class SurvivalModifierChoiceCard_PickModifier_Patch
    {
        [HarmonyPrefix]
        public static bool Prefix(SurvivalModifierChoiceCard __instance)
        {
            if (!PerkConfirm.Active) return true;
            PerkConfirm.Lock(__instance);
            return false;
        }
    }

    // The button under the cards, built from the card prefab so it looks like them and takes the same controller input
    public class PerkConfirmButton : MonoBehaviour
    {
        public const string ObjectName = "SurgeConfirm";

        private PerkActionButton _action;
        private SurvivalModeHud _hud;
        private SurvivalModifierChoiceCard _submitted;

        public static PerkConfirmButton Current { get; private set; }

        public Button Button => _action != null ? _action.Button : null;

        // Called when a run ends, so a stale button never waits for the next run's first perk screen
        public static void Clear()
        {
            PerkChoiceWidget.Remove(SurvivalModeHud.instance, ObjectName);
            Current = null;
            PerkConfirm.Reset();
        }

        public static void Build(SurvivalModeHud hud)
        {
            if (hud == null || hud.perkChoiceView == null || hud.perkCardsRoot == null) return;
            PerkChoiceWidget.Remove(hud, ObjectName);
            Current = null;
            PerkConfirm.Reset();
            if (!PerkConfirm.Active || hud.perkCardPrefab == null) return;

            try
            {
                PerkConfirmButton confirm = null;
                var action = PerkActionButton.Create(hud.perkCardPrefab, hud.perkChoiceView.transform, ObjectName, () => confirm.OnClick(), 8f);
                confirm = action.gameObject.AddComponent<PerkConfirmButton>();
                confirm._hud = hud;
                confirm._action = action;

                var rect = (RectTransform)action.transform;
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 1f);
                rect.sizeDelta = new Vector2(Consts.Values.Chaos.ConfirmWidth, Consts.Values.Chaos.ConfirmHeight);
                confirm.Reposition();
                if (action.Label != null) action.Label.fontSize = 13f;
                confirm.LinkNavigation();
                confirm.Refresh();
                Current = confirm;
            }
            catch (Exception ex)
            {
                Logger.LogError($"[Surge] could not build the perk confirm button: {ex}");
            }
        }

        public void Reposition()
        {
            if (_hud == null || _hud.perkCardsRoot == null) return;
            Vector2 below = PerkChoiceWidget.BelowCards(_hud);
            transform.localPosition = new Vector3(below.x, below.y - Consts.Values.Chaos.ConfirmGap, 0f);
        }

        // The host's pick is final; a client's is a vote, so it can confirm another card the way a vanilla click moves the vote
        private bool CanConfirm(SurvivalModifierChoiceCard locked)
        {
            if (locked == null || locked == _submitted || PerkChoiceWidget.ChoiceMade) return false;
            return _submitted == null || Net.IsRemoteClient;
        }

        private void OnClick()
        {
            var locked = PerkConfirm.Locked;
            if (!CanConfirm(locked)) return;
            _submitted = locked;
            PerkConfirm.Submit();
            Refresh();
        }

        // Down from any card reaches the button; up goes back to the locked card, or the first one
        private void LinkNavigation()
        {
            Button confirm = Button;
            var cards = new List<Button>();
            foreach (var card in PerkChoiceWidget.Cards(_hud))
            {
                var button = card.GetComponent<Button>();
                if (button != null) cards.Add(button);
            }
            for (int i = 0; i < cards.Count; i++)
            {
                Navigation nav = cards[i].navigation;
                nav.mode = Navigation.Mode.Explicit;
                nav.selectOnLeft = i > 0 ? cards[i - 1] : null;
                nav.selectOnRight = i < cards.Count - 1 ? cards[i + 1] : null;
                nav.selectOnUp = null;
                nav.selectOnDown = confirm;
                cards[i].navigation = nav;
            }
            if (confirm != null)
            {
                Navigation nav = confirm.navigation;
                nav.mode = Navigation.Mode.Explicit;
                nav.selectOnUp = cards.Count > 0 ? cards[0] : null;
                nav.selectOnDown = null;
                nav.selectOnLeft = null;
                nav.selectOnRight = null;
                confirm.navigation = nav;
            }
        }

        private void OnEnable()
        {
            PerkConfirm.LockedChanged += Refresh;
        }

        private void OnDisable()
        {
            PerkConfirm.LockedChanged -= Refresh;
            if (Current == this) Current = null;
        }

        public void Refresh()
        {
            if (this == null || Button == null) return;
            var locked = PerkConfirm.Locked;
            bool can = CanConfirm(locked);
            bool confirmed = _submitted != null && (locked == _submitted || !Net.IsRemoteClient);
            _action.Show(can, locked != null && locked.cardButton != null ? locked.cardButton : null, _hud);
            if (_action.Label != null)
            {
                string name = locked != null && locked.perkNameText != null ? locked.perkNameText.text : null;
                _action.Label.text = confirmed ? "Confirmed" : name != null ? $"Confirm <color={Consts.Formatting.ColorGold}>{name}</color>" : "Pick a card to confirm";
            }
        }
    }
}
