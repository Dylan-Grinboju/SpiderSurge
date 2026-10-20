using System.Collections;
using System.Collections.Generic;
using SpiderSurge.Common;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Logger = Silk.Logger;
using System.Linq;

namespace SpiderSurge
{
    // Remembers the card root's vanilla layout so it can be put back when the shop isn't there
    public class ShopCardLayout : MonoBehaviour
    {
        public Vector2 OriginalPosition;
        public Vector2 OriginalSize;
    }

    // The shop column beside the perk cards, built from the game's card prefab: a row click selects an item and Buy spends the chaos points
    public class PerkShopPanel : MonoBehaviour
    {
        private const string PanelName = "SurgeShop";
        private const float Gap = 12f;

        private class Entry
        {
            public ShopItem Item;
            public Button Button;
            public Image Background;
            public Image Icon;
            public TMP_Text Title;
            public TMP_Text Description;
        }

        private readonly List<Entry> _entries = new List<Entry>();
        private TMP_Text _header;
        private SurvivalModeHud _hud;
        private Entry _selected;
        private Button _buy;
        private Image _buyBackground;
        private TMP_Text _buyLabel;

        public static void Build(SurvivalModeHud hud)
        {
            if (hud == null || hud.perkChoiceView == null || hud.perkCardsRoot == null) return;

            Remove(hud);
            if (!SurgeMeter.Enabled || AbilitySelection.StartingChoiceOpen || hud.perkCardPrefab == null)
            {
                RestoreCards(hud);
                PerkConfirmButton.Current?.Reposition();
                return;
            }

            try
            {
                LayoutCards(hud);
                var panelObject = new GameObject(PanelName, typeof(RectTransform));
                var rect = (RectTransform)panelObject.transform;
                rect.SetParent(hud.perkChoiceView.transform, false);
                rect.anchorMin = new Vector2(1f, 0.5f);
                rect.anchorMax = new Vector2(1f, 0.5f);
                rect.pivot = new Vector2(1f, 1f);
                rect.anchoredPosition = new Vector2(-27f, CardsTop(hud));
                int count = PerkShop.Offer.Length;
                rect.sizeDelta = new Vector2(Consts.Values.Meter.PanelWidth, Consts.Values.Meter.HeaderHeight + count * (Consts.Values.Meter.EntryHeight + Consts.Values.Meter.EntrySpacing) + Consts.Values.Meter.BuyHeight);

                var panel = panelObject.AddComponent<PerkShopPanel>();
                panel._hud = hud;
                panel.Populate();
                panel.LinkNavigation();
                panel.Refresh();
                panel.StartCoroutine(panel.ReselectNextFrame());
                PerkConfirmButton.Current?.Reposition();
            }
            catch (System.Exception ex)
            {
                Logger.LogError($"[Surge shop] could not build the panel: {ex}");
            }
        }

        // Called when a run ends, so a stale panel never waits for the next run's first perk screen
        public static void Clear()
        {
            var hud = SurvivalModeHud.instance;
            if (hud != null && hud.perkChoiceView != null) Remove(hud);
        }

        public static void Remove(SurvivalModeHud hud)
        {
            PerkChoiceWidget.Remove(hud, PanelName);
        }

        // Shrinks the card root to the space left of the shop; the layout group centers the cards in it and scales them to fit
        private static void LayoutCards(SurvivalModeHud hud)
        {
            var root = hud.perkCardsRoot as RectTransform;
            if (root == null) return;
            var layout = root.GetComponent<ShopCardLayout>();
            if (layout == null)
            {
                layout = root.gameObject.AddComponent<ShopCardLayout>();
                layout.OriginalPosition = root.anchoredPosition;
                layout.OriginalSize = root.sizeDelta;
            }
            float taken = Consts.Values.Meter.PanelWidth + Gap;
            root.sizeDelta = new Vector2(layout.OriginalSize.x - taken, layout.OriginalSize.y);
            root.anchoredPosition = new Vector2(layout.OriginalPosition.x - taken / 2f, layout.OriginalPosition.y);

            var parent = root.parent as RectTransform;
            float width = (parent != null ? parent.rect.width : 800f) + root.sizeDelta.x;
            var group = root.GetComponent<HorizontalLayoutGroup>();
            float spacing = group != null ? group.spacing : 5f;
            var cards = PerkChoiceWidget.Cards(hud);
            if (cards.Count == 0) return;
            float cardsWidth = 0f;
            foreach (var card in cards)
            {
                cardsWidth += ((RectTransform)card.transform).sizeDelta.x * PerkCardScale.Of(card).Base.x;
            }
            float room = width - spacing * (cards.Count - 1);
            float factor = cardsWidth > 0f ? Mathf.Min(1f, room / cardsWidth) : 1f;
            factor = Mathf.Min(factor, Consts.Values.Meter.CardScale);
            foreach (var card in cards)
            {
                PerkCardScale.Of(card).SetFactor(factor);
            }
        }

        private static void RestoreCards(SurvivalModeHud hud)
        {
            foreach (var card in PerkChoiceWidget.Cards(hud))
            {
                PerkCardScale.Of(card).SetFactor(1f);
            }
            var root = hud.perkCardsRoot as RectTransform;
            var layout = root != null ? root.GetComponent<ShopCardLayout>() : null;
            if (layout == null) return;
            root.anchoredPosition = layout.OriginalPosition;
            root.sizeDelta = layout.OriginalSize;
        }

        // The cards sit at the top of their root, so the panel's top lines up with theirs
        private static float CardsTop(SurvivalModeHud hud)
        {
            var root = hud.perkCardsRoot as RectTransform;
            if (root == null) return 106f;
            return root.anchoredPosition.y + root.sizeDelta.y * (1f - root.pivot.y);
        }

        private void Populate()
        {
            float y = 0f;
            foreach (ShopItem item in PerkShop.Offer)
            {
                GameObject entryObject = Instantiate(_hud.perkCardPrefab, transform);
                entryObject.name = $"Shop {item}";
                var card = entryObject.GetComponent<SurvivalModifierChoiceCard>();
                var entry = new Entry
                {
                    Item = item,
                    Button = entryObject.GetComponent<Button>(),
                    Background = entryObject.GetComponent<Image>(),
                };
                if (card != null)
                {
                    entry.Icon = card.modifierIconImage;
                    entry.Title = card.perkNameText;
                    entry.Description = card.perkDescriptionText;
                    PerkChoiceWidget.HideCardParts(card, true, true);
                    if (_header == null && card.perkNameText != null)
                    {
                        _header = Instantiate(card.perkNameText.gameObject, transform).GetComponent<TMP_Text>();
                        _header.gameObject.name = "Points";
                    }
                    Destroy(card);
                }

                var rect = (RectTransform)entryObject.transform;
                rect.localScale = Vector3.one;
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(1f, 1f);
                rect.pivot = new Vector2(0.5f, 1f);
                rect.anchoredPosition = new Vector2(0f, -(Consts.Values.Meter.HeaderHeight + y));
                rect.sizeDelta = new Vector2(0f, Consts.Values.Meter.EntryHeight);
                y += Consts.Values.Meter.EntryHeight + Consts.Values.Meter.EntrySpacing;

                if (entry.Icon != null)
                {
                    var iconRect = entry.Icon.rectTransform;
                    iconRect.anchorMin = new Vector2(0f, 0.5f);
                    iconRect.anchorMax = new Vector2(0f, 0.5f);
                    iconRect.pivot = new Vector2(0f, 0.5f);
                    iconRect.anchoredPosition = new Vector2(8f, 0f);
                    iconRect.sizeDelta = new Vector2(42f, 42f);
                    entry.Icon.sprite = IconLoader.GetIcon(PerkShop.IconOf(item));
                    entry.Icon.color = Color.white;
                    entry.Icon.preserveAspect = true;
                }
                if (entry.Title != null)
                {
                    SettingsPage.SetText(entry.Title, "");
                    var titleRect = entry.Title.rectTransform;
                    titleRect.anchorMin = Vector2.zero;
                    titleRect.anchorMax = Vector2.one;
                    titleRect.pivot = new Vector2(0.5f, 0.5f);
                    titleRect.offsetMin = new Vector2(56f, Consts.Values.Meter.EntryHeight - 28f);
                    titleRect.offsetMax = new Vector2(-6f, -3f);
                    entry.Title.fontSize = 12f;
                    entry.Title.alignment = TextAlignmentOptions.MidlineLeft;
                    entry.Title.enableWordWrapping = false;
                    entry.Title.overflowMode = TextOverflowModes.Ellipsis;
                    entry.Title.enableAutoSizing = true;
                    entry.Title.fontSizeMin = Consts.Values.Meter.MinLabelFontSize;
                    entry.Title.fontSizeMax = 12f;
                }
                if (entry.Description != null)
                {
                    SettingsPage.SetText(entry.Description, "");
                    var descriptionRect = entry.Description.rectTransform;
                    descriptionRect.anchorMin = Vector2.zero;
                    descriptionRect.anchorMax = Vector2.one;
                    descriptionRect.pivot = new Vector2(0.5f, 0.5f);
                    descriptionRect.offsetMin = new Vector2(56f, 4f);
                    descriptionRect.offsetMax = new Vector2(-6f, -28f);
                    entry.Description.fontSize = 9f;
                    entry.Description.alignment = TextAlignmentOptions.TopLeft;
                    entry.Description.enableWordWrapping = true;
                    entry.Description.overflowMode = TextOverflowModes.Truncate;
                }
                if (entry.Button != null)
                {
                    entry.Button.onClick.RemoveAllListeners();
                    Entry captured = entry;
                    entry.Button.onClick.AddListener(() => Select(captured));
                }
                _entries.Add(entry);
            }
            BuildBuyButton(y);

            if (_header != null)
            {
                SettingsPage.SetText(_header, "");
                var headerRect = _header.rectTransform;
                headerRect.anchorMin = new Vector2(0f, 1f);
                headerRect.anchorMax = new Vector2(1f, 1f);
                headerRect.pivot = new Vector2(0.5f, 1f);
                headerRect.anchoredPosition = Vector2.zero;
                headerRect.sizeDelta = new Vector2(0f, Consts.Values.Meter.HeaderHeight);
                headerRect.localScale = Vector3.one;
                _header.fontSize = 14f;
                _header.alignment = TextAlignmentOptions.Center;
                _header.enableWordWrapping = false;
            }
        }

        private void BuildBuyButton(float y)
        {
            GameObject buyObject = Instantiate(_hud.perkCardPrefab, transform);
            buyObject.name = "Shop Buy";
            var card = buyObject.GetComponent<SurvivalModifierChoiceCard>();
            _buy = buyObject.GetComponent<Button>();
            _buyBackground = buyObject.GetComponent<Image>();
            if (card != null)
            {
                _buyLabel = card.perkNameText;
                PerkChoiceWidget.HideCardParts(card, false, false);
                Destroy(card);
            }
            var rect = (RectTransform)buyObject.transform;
            rect.localScale = Vector3.one;
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -(Consts.Values.Meter.HeaderHeight + y));
            rect.sizeDelta = new Vector2(0f, Consts.Values.Meter.BuyHeight);
            if (_buyLabel != null)
            {
                SettingsPage.SetText(_buyLabel, "");
                var labelRect = _buyLabel.rectTransform;
                labelRect.anchorMin = Vector2.zero;
                labelRect.anchorMax = Vector2.one;
                labelRect.pivot = new Vector2(0.5f, 0.5f);
                labelRect.offsetMin = new Vector2(6f, 2f);
                labelRect.offsetMax = new Vector2(-6f, -2f);
                _buyLabel.fontSize = 12f;
                _buyLabel.alignment = TextAlignmentOptions.Center;
                _buyLabel.enableWordWrapping = false;
                _buyLabel.overflowMode = TextOverflowModes.Ellipsis;
                _buyLabel.enableAutoSizing = true;
                _buyLabel.fontSizeMin = Consts.Values.Meter.MinLabelFontSize;
                _buyLabel.fontSizeMax = 12f;
            }
            if (_buy != null)
            {
                _buy.onClick.RemoveAllListeners();
                _buy.onClick.AddListener(OnBuy);
                ColorBlock colors = _buy.colors;
                colors.disabledColor = Consts.Values.Meter.ConfirmDisabledTint;
                _buy.colors = colors;
            }
        }

        private void Select(Entry entry)
        {
            _selected = entry;
            Refresh();
        }

        private void OnBuy()
        {
            if (_selected == null) return;
            PerkShop.Request(_selected.Item, LocalBuyer());
        }

        // Right from the last card enters the shop, up and down reach Buy, left goes back; Buy and the confirm button lead to each other
        private void LinkNavigation()
        {
            var cards = new List<Button>();
            foreach (var card in PerkChoiceWidget.Cards(_hud))
            {
                var button = card.GetComponent<Button>();
                if (button != null) cards.Add(button);
            }
            Button last = cards.Count > 0 ? cards[cards.Count - 1] : null;
            Button beforeLast = cards.Count > 1 ? cards[cards.Count - 2] : null;
            Button confirm = PerkConfirmButton.Current != null ? PerkConfirmButton.Current.Button : null;
            Button first = _entries.Count > 0 ? _entries[0].Button : null;
            if (last != null && first != null)
            {
                Navigation nav = last.navigation;
                nav.mode = Navigation.Mode.Explicit;
                nav.selectOnLeft = beforeLast;
                nav.selectOnRight = first;
                nav.selectOnUp = null;
                nav.selectOnDown = confirm;
                last.navigation = nav;
            }
            for (int i = 0; i < _entries.Count; i++)
            {
                Button button = _entries[i].Button;
                if (button == null) continue;
                Navigation nav = button.navigation;
                nav.mode = Navigation.Mode.Explicit;
                nav.selectOnUp = i > 0 ? _entries[i - 1].Button : null;
                nav.selectOnDown = i < _entries.Count - 1 ? _entries[i + 1].Button : _buy;
                nav.selectOnLeft = last;
                nav.selectOnRight = null;
                button.navigation = nav;
            }
            if (_buy != null)
            {
                Navigation nav = _buy.navigation;
                nav.mode = Navigation.Mode.Explicit;
                nav.selectOnUp = _entries.Count > 0 ? _entries[_entries.Count - 1].Button : null;
                nav.selectOnDown = null;
                nav.selectOnLeft = confirm != null ? confirm : last;
                nav.selectOnRight = null;
                _buy.navigation = nav;
            }
            if (confirm != null && first != null)
            {
                Navigation nav = confirm.navigation;
                nav.selectOnRight = first;
                confirm.navigation = nav;
            }
        }

        private void OnEnable()
        {
            SurgeMeter.Changed += Refresh;
            PerkShop.OfferChanged += Rebuild;
        }

        private void OnDisable()
        {
            SurgeMeter.Changed -= Refresh;
            PerkShop.OfferChanged -= Rebuild;
        }

        // A client gets the cards and the host's offer in separate messages, so the panel is redrawn when the offer arrives later
        private void Rebuild()
        {
            if (this == null || _hud == null) return;
            if (!_entries.Select(entry => entry.Item).SequenceEqual(PerkShop.Offer)) Build(_hud);
        }

        public void Refresh()
        {
            if (this == null) return;
            int points = SurgeMeter.Points;
            if (_header != null)
            {
                _header.text = $"Chaos points: <color={Consts.Formatting.ColorGold}>{points}</color>";
            }
            bool selectedCan = false;
            foreach (Entry entry in _entries)
            {
                int cost = PerkShop.CostOf(entry.Item);
                string unavailable = PerkShop.Unavailable(entry.Item);
                bool can = points >= cost && unavailable == null;
                if (entry == _selected) selectedCan = can;
                float alpha = can ? 1f : 0.45f;
                if (entry.Title != null)
                {
                    entry.Title.text = $"<color={Consts.Formatting.ColorGold}>{PerkShop.PriceOf(entry.Item)}</color>  {PerkShop.NameOf(entry.Item)}";
                    entry.Title.color = PerkChoiceWidget.Faded(entry.Title.color, alpha);
                }
                if (entry.Description != null)
                {
                    entry.Description.text = unavailable ?? PerkShop.DescriptionOf(entry.Item);
                    entry.Description.color = PerkChoiceWidget.Faded(entry.Description.color, alpha);
                }
                if (entry.Icon != null) entry.Icon.color = PerkChoiceWidget.Faded(Color.white, alpha);
                if (entry.Background != null)
                {
                    Color tint = entry == _selected ? Consts.Values.Meter.LockedCardTint : Color.white;
                    entry.Background.color = PerkChoiceWidget.Faded(tint, can ? 1f : 0.6f);
                }
            }
            RefreshBuy(selectedCan);
        }

        private void RefreshBuy(bool selectedCan)
        {
            if (_buy == null) return;
            bool can = _selected != null && selectedCan;
            _buy.interactable = can;
            if (_selected != null && _selected.Button != null)
            {
                Navigation nav = _buy.navigation;
                nav.selectOnUp = _selected.Button;
                _buy.navigation = nav;
            }
            if (_buyLabel != null)
            {
                _buyLabel.text = _selected == null ? "Pick an item to buy"
                    : $"Buy {PerkShop.NameOf(_selected.Item)} for <color={Consts.Formatting.ColorGold}>{PerkShop.PriceOf(_selected.Item)}</color>";
                _buyLabel.color = PerkChoiceWidget.Faded(_buyLabel.color, can ? 1f : 0.5f);
            }
            if (_buyBackground != null) _buyBackground.color = PerkChoiceWidget.Faded(Color.white, can ? 1f : 0.6f);
        }

        // After a reroll the selected card is gone with the old cards, so the view's first card is selected again
        private IEnumerator ReselectNextFrame()
        {
            yield return null;
            var eventSystem = EventSystem.current;
            if (eventSystem == null || _hud == null || _hud.perkChoiceView == null) yield break;
            GameObject selected = eventSystem.currentSelectedGameObject;
            if (selected == null || !selected.activeInHierarchy || !selected.transform.IsChildOf(_hud.perkChoiceView.transform))
            {
                eventSystem.SetSelectedGameObject(_hud.perkChoiceView.SelectedButton);
            }
        }

        // The purchase is credited to this machine's first player; the shop is the team's, so it only names the buyer
        private static int LocalBuyer()
        {
            foreach (PlayerController player in Players.All)
            {
                if (player != null && Players.IsLocal(player)) return player.playerID.Value;
            }
            return -1;
        }
    }
}
