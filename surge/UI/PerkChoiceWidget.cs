using System.Collections;
using System.Collections.Generic;
using Doozy.Engine.UI;
using HarmonyLib;
using SpiderSurge.Common;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SpiderSurge
{
    // Shared handling for the objects Surge adds to the perk choice view: the shop panel, the confirm button and the meter hint
    public static class PerkChoiceWidget
    {
        private static readonly AccessTools.FieldRef<SurvivalModeHud, List<SurvivalModifierChoiceCard>> CardList = AccessTools.FieldRefAccess<SurvivalModeHud, List<SurvivalModifierChoiceCard>>("survivalModifierChoiceCards");

        public static bool ChoiceMade { get; private set; }

        // The cards on screen, in order; the perk card root also still holds the previous cards until the end of the frame they are destroyed in
        public static List<SurvivalModifierChoiceCard> Cards(SurvivalModeHud hud)
        {
            var cards = new List<SurvivalModifierChoiceCard>();
            var list = hud != null ? CardList(hud) : null;
            if (list == null) return cards;
            foreach (var card in list)
            {
                if (card != null) cards.Add(card);
            }
            return cards;
        }

        // The middle of the card root and the lowest card bottom, in the perk choice view's space; the root stretches across the view and moves left when the shop is beside it
        public static Vector2 BelowCards(SurvivalModeHud hud)
        {
            var root = hud != null ? hud.perkCardsRoot as RectTransform : null;
            if (root == null) return Vector2.zero;
            LayoutRebuilder.ForceRebuildLayoutImmediate(root);
            Vector3 local = root.localPosition;
            float bottom = local.y + root.rect.yMin;
            bool any = false;
            foreach (var choice in Cards(hud))
            {
                var card = (RectTransform)choice.transform;
                float cardBottom = local.y + card.localPosition.y + card.rect.yMin * card.localScale.y;
                bottom = any ? Mathf.Min(bottom, cardBottom) : cardBottom;
                any = true;
            }
            return new Vector2(local.x + root.rect.center.x, bottom);
        }

        public static Selectable FirstCard(SurvivalModeHud hud)
        {
            var cards = Cards(hud);
            return cards.Count > 0 ? cards[0].cardButton : null;
        }

        // Every way the game settles a pick (a click, the votes, the timer) ends in PerkChoiceSubmitClientRpc; nothing is bought or confirmed after it
        public static void MarkChoiceMade()
        {
            ChoiceMade = true;
            PerkConfirmButton.Current?.Refresh();
        }

        public static void ResetChoice()
        {
            ChoiceMade = false;
        }

        public static Color Faded(Color color, float alpha) => new Color(color.r, color.g, color.b, alpha);

        // Unity drops the selection when the selected control stops being interactable, which leaves a controller nothing to move from
        public static void SetInteractable(Selectable control, bool interactable, Selectable fallback)
        {
            if (control == null || control.interactable == interactable) return;
            EventSystem eventSystem = EventSystem.current;
            bool wasSelected = eventSystem != null && eventSystem.currentSelectedGameObject == control.gameObject;
            control.interactable = interactable;
            if (wasSelected && !interactable && fallback != null && fallback.isActiveAndEnabled) eventSystem.SetSelectedGameObject(fallback.gameObject);
        }

        // A rebuild can find more than one old copy, so every one goes, renamed so the next pass can't find it again
        public static void Remove(SurvivalModeHud hud, string name)
        {
            if (hud == null || hud.perkChoiceView == null) return;
            Transform parent = hud.perkChoiceView.transform;
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                Transform child = parent.GetChild(i);
                if (child.name != name) continue;
                child.name = name + " (old)";
                child.SetParent(null, false);
                foreach (MonoBehaviour behaviour in child.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (behaviour != null && behaviour.GetType().Assembly == typeof(PerkChoiceWidget).Assembly) behaviour.enabled = false;
                }
                SettingsPage.StopClickAnimations(child.gameObject);
                if (hud.isActiveAndEnabled) hud.StartCoroutine(DestroyAfterFrame(child.gameObject));
                else Object.Destroy(child.gameObject);
            }
        }

        // Out of the canvas the old object neither draws nor takes input, but the button Doozy is still clicking stays alive for the rest of the frame; its tweens are killed again once the frame is over
        private static IEnumerator DestroyAfterFrame(GameObject widget)
        {
            yield return new WaitForEndOfFrame();
            if (widget == null) yield break;
            SettingsPage.StopClickAnimations(widget);
            Object.Destroy(widget);
        }

        // Hides the card parts a widget made from the perk card prefab has no use for; the card component itself goes afterwards
        public static void HideCardParts(SurvivalModifierChoiceCard card, bool keepIcon, bool keepDescription)
        {
            if (card == null) return;
            if (!keepIcon && card.modifierIconImage != null) card.modifierIconImage.gameObject.SetActive(false);
            if (!keepDescription && card.perkDescriptionText != null) card.perkDescriptionText.gameObject.SetActive(false);
            if (card.mapImage != null) card.mapImage.gameObject.SetActive(false);
            if (card.mapNameText != null) card.mapNameText.gameObject.SetActive(false);
            if (card.twitchVotes != null) card.twitchVotes.SetActive(false);
            if (card.voteImages != null)
            {
                foreach (Image vote in card.voteImages)
                {
                    if (vote == null) continue;
                    if (vote.transform.parent != null && vote.transform.parent != card.transform) vote.transform.parent.gameObject.SetActive(false);
                    else vote.gameObject.SetActive(false);
                }
            }
        }
    }

    // A perk card's scale built from its vanilla scale, the shop layout's factor and the lock highlight, so repeated layouts never compound and Doozy's punches return to it
    public class PerkCardScale : MonoBehaviour
    {
        private Vector3 _base = Vector3.one;
        private float _factor = 1f;
        private bool _locked;

        public Vector3 Base => _base;

        public static PerkCardScale Of(SurvivalModifierChoiceCard card)
        {
            var scale = card.GetComponent<PerkCardScale>();
            if (scale != null) return scale;
            scale = card.gameObject.AddComponent<PerkCardScale>();
            scale._base = card.transform.localScale;
            return scale;
        }

        public void SetFactor(float factor)
        {
            _factor = factor;
            Apply();
        }

        public void SetLocked(bool locked)
        {
            _locked = locked;
            Apply();
        }

        private void Apply()
        {
            transform.localScale = _base * _factor * (_locked ? Consts.Values.Meter.LockedCardScale : 1f);
            var button = GetComponent<UIButton>();
            if (button != null) button.UpdateStartScale();
        }
    }
}
