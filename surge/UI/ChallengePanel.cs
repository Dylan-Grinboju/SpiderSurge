using System.Collections;
using SpiderSurge.Common;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SpiderSurge
{
    public sealed class ChallengePanel : MonoBehaviour
    {
        private static ChallengePanel _current;
        private Button _accept;
        private Button _skip;
        private Canvas _canvas;
        private GraphicRaycaster _raycaster;

        public static bool IsShown => _current != null;

        // Hidden while the pause menu is open, so the menu keeps the focus, the clicks and the A presses
        public static void Show(bool visible)
        {
            if (_current != null)
            {
                _current.SetVisible(visible);
                if (visible) _current.Refresh();
                return;
            }
            var hud = SurvivalModeHud.instance;
            if (hud == null || hud.perkChoiceView == null || hud.perkCardPrefab == null) return;
            var template = hud.perkCardPrefab.GetComponent<SurvivalModifierChoiceCard>();
            var root = new GameObject("SurgeChallenge", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster), typeof(Image));
            var rect = (RectTransform)root.transform;
            rect.SetParent(hud.perkChoiceView.transform.parent, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            var canvas = root.GetComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = 210;
            root.GetComponent<Image>().color = Color.black;
            _current = root.AddComponent<ChallengePanel>();
            _current._canvas = canvas;
            _current._raycaster = root.GetComponent<GraphicRaycaster>();
            float width = Mathf.Min(700f, ((RectTransform)hud.perkChoiceView.transform).rect.width - 48f);
            _current.Text(template.perkDescriptionText, "Description", ChaosRules.Description(ChallengeController.Offered), new Vector2(0, 70), new Vector2(width - 40, 90), 27);
            _current.Text(template.perkNameText, "Reward", $"Complete for {ChaosRules.Points(ChaosRules.Reward(ChallengeController.Offered), signed: true)}", Vector2.zero, new Vector2(width, 40), 21);
            // Only the host answers, so a client only sees that it is waiting
            if (Net.IsRemoteClient)
            {
                _current.Text(template.perkNameText, "Waiting", "Waiting for the host to accept or skip", new Vector2(0, -80), new Vector2(width, 40), 19);
            }
            else
            {
                float buttonWidth = Mathf.Min(250f, (width - 30f) / 2f);
                _current._accept = _current.Button(hud, "Accept", new Vector2(-(buttonWidth + 16f) / 2, -80), buttonWidth, true);
                _current._skip = _current.Button(hud, "Skip", new Vector2((buttonWidth + 16f) / 2, -80), buttonWidth, false);
                var nav = new Navigation { mode = Navigation.Mode.Explicit, selectOnRight = _current._skip };
                _current._accept.navigation = nav;
                nav.selectOnRight = null;
                nav.selectOnLeft = _current._accept;
                _current._skip.navigation = nav;
            }
            _current.SetVisible(visible);
            if (visible) _current.Refresh();
        }

        // A hidden button still takes Submit while it is selected, and the menu's navigation can reach it, so it stops being interactable too
        private void SetVisible(bool visible)
        {
            if (_canvas.enabled == visible) return;
            _canvas.enabled = visible;
            _raycaster.enabled = visible;
            if (_accept == null || _skip == null) return;
            _accept.interactable = _skip.interactable = visible;
            if (!visible)
            {
                _accept.GetComponent<ChallengeHoldButton>().Cancel();
                _skip.GetComponent<ChallengeHoldButton>().Cancel();
            }
            var events = EventSystem.current;
            if (!visible && events != null && events.currentSelectedGameObject != null && events.currentSelectedGameObject.transform.IsChildOf(transform)) events.SetSelectedGameObject(null);
        }

        private TMP_Text Text(TMP_Text template, string name, string value, Vector2 position, Vector2 size, float fontSize)
        {
            var text = Instantiate(template.gameObject, transform).GetComponent<TMP_Text>();
            text.name = name;
            SettingsPage.SetText(text, value);
            text.gameObject.SetActive(true);
            text.raycastTarget = false;
            text.alignment = TextAlignmentOptions.Center;
            text.enableAutoSizing = false;
            text.fontSize = fontSize;
            text.color = Color.white;
            text.enableWordWrapping = true;
            text.overflowMode = TextOverflowModes.Overflow;
            var rect = text.rectTransform;
            rect.localScale = Vector3.one;
            rect.localRotation = Quaternion.identity;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return text;
        }

        // Host only: the hold on the button sends the answer
        private Button Button(SurvivalModeHud hud, string title, Vector2 position, float width, bool accept)
        {
            var obj = Instantiate(hud.perkCardPrefab, transform);
            obj.name = "Challenge " + title;
            var card = obj.GetComponent<SurvivalModifierChoiceCard>();
            PerkChoiceWidget.HideCardParts(card, false, false);
            var text = card.perkNameText;
            SettingsPage.SetText(text, title);
            text.fontSize = 21;
            text.enableAutoSizing = false;
            text.alignment = TextAlignmentOptions.Center;
            text.rectTransform.anchorMin = Vector2.zero;
            text.rectTransform.anchorMax = Vector2.one;
            text.rectTransform.offsetMin = text.rectTransform.offsetMax = Vector2.zero;
            Destroy(card);
            var rect = (RectTransform)obj.transform;
            rect.localScale = Vector3.one;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(width, 48);
            var button = obj.GetComponent<Button>();
            button.onClick.RemoveAllListeners();
            obj.AddComponent<ChallengeHoldButton>().Initialize(button, text, title, accept);
            return button;
        }

        private void Refresh()
        {
            if (EventSystem.current != null && _skip != null)
            {
                var selected = EventSystem.current.currentSelectedGameObject;
                if (selected == null || !selected.transform.IsChildOf(transform)) EventSystem.current.SetSelectedGameObject(_skip.gameObject);
            }
        }

        public static void Hide()
        {
            if (_current == null) return;
            GameObject old = _current.gameObject;
            var canvas = old.GetComponent<Canvas>();
            if (canvas != null) canvas.enabled = false;
            old.GetComponent<GraphicRaycaster>().enabled = false;
            _current = null;
            SettingsPage.StopClickAnimations(old);
            var hud = SurvivalModeHud.instance;
            if (hud != null && hud.isActiveAndEnabled) hud.StartCoroutine(DestroyAfterClick(old));
            else Destroy(old);
        }

        private static IEnumerator DestroyAfterClick(GameObject old)
        {
            yield return new WaitForEndOfFrame();
            if (old == null) yield break;
            SettingsPage.StopClickAnimations(old);
            Destroy(old);
        }

        private void OnDestroy()
        {
            if (_current == this) _current = null;
        }
    }

    // The "+1 chaos point" line on the marked card. Runs when the cards are shown and when a client learns the marked slot, not every frame
    public static class MapBonusLabels
    {
        private const string ObjectName = "ChaosMapBonus";

        public static void Refresh()
        {
            var hud = SurvivalModeHud.instance;
            if (hud == null) return;
            int slot = ChaosWallet.Enabled && !AbilitySelection.StartingChoiceOpen ? PerkShop.BonusSlot : -1;
            var cards = PerkChoiceWidget.Cards(hud);
            for (int i = 0; i < cards.Count; i++)
            {
                var card = cards[i];
                var existing = card.transform.Find(ObjectName);
                bool show = i == slot;
                if (!show && existing == null) continue;
                if (existing == null)
                {
                    if (card.mapNameText == null) continue;
                    var text = UnityEngine.Object.Instantiate(card.mapNameText.gameObject, card.transform).GetComponent<TMP_Text>();
                    text.name = ObjectName;
                    SettingsPage.SetText(text, $"<color={Consts.Formatting.ColorGold}>{ChaosRules.Points(1, signed: true)}</color>");
                    text.raycastTarget = false;
                    text.color = Color.white;
                    text.fontSize = 14;
                    text.enableAutoSizing = false;
                    text.alignment = TextAlignmentOptions.Center;
                    text.enableWordWrapping = false;
                    var rect = text.rectTransform;
                    rect.localScale = Vector3.one;
                    rect.anchorMin = new Vector2(0, 0);
                    rect.anchorMax = new Vector2(1, 0);
                    rect.pivot = new Vector2(.5f, 0);
                    rect.anchoredPosition = new Vector2(0, 8);
                    rect.sizeDelta = new Vector2(-8, 24);
                    existing = rect;
                }
                existing.gameObject.SetActive(show);
            }
        }
    }
}
