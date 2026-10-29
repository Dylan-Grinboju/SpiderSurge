using System.Collections;
using System.Collections.Generic;
using SpiderSurge.Common;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Logger = Silk.Logger;
using System.Linq;
using M = SpiderSurge.Consts.Values.Chaos;

namespace SpiderSurge
{
    // Remembers the card root's vanilla layout so it can be put back when the shop isn't there
    public class ShopCardLayout : MonoBehaviour
    {
        public Vector2 OriginalPosition;
        public Vector2 OriginalSize;
    }

    // The shop column uses the game's cards and keeps the team wallet visible.
    public class PerkShopPanel : MonoBehaviour
    {
        private const string PanelName = "SurgeShop";
        private const float HeaderHeight = 24f;
        private const float EntryInset = 14f;
        private const float TitleTop = 10f;
        private const float TitleHeight = 22f;
        private const float IconGap = 10f;

        private class Entry
        {
            public ShopItem Item;
            public Button Button;
            public Image Background;
            public Image Icon;
            public TMP_Text Title;
            public ChaosIcon Currency;
            public TMP_Text Description;
        }

        private readonly List<Entry> _entries = new List<Entry>();
        private TMP_Text _header;
        private ChaosIcon _headerIcon;
        private SurvivalModeHud _hud;
        private Vector2 _viewSize;
        private Entry _selected;
        private PerkActionButton _buy;
        private int _lives = -1;
        private int _maxLives = -1;
        private ModifierManager _modifiers;
        private Modifier _maxLivesModifier;

        public static void Build(SurvivalModeHud hud)
        {
            if (hud == null || hud.perkChoiceView == null || hud.perkCardsRoot == null) return;

            Remove(hud);
            var prefabCard = hud.perkCardPrefab != null ? hud.perkCardPrefab.GetComponent<SurvivalModifierChoiceCard>() : null;
            if (!ChaosWallet.Enabled || AbilitySelection.StartingChoiceOpen || prefabCard == null || prefabCard.perkNameText == null || prefabCard.perkDescriptionText == null)
            {
                RestoreCards(hud);
                PerkConfirmButton.Current?.Reposition();
                PerkHint.Current?.Reposition();
                return;
            }

            try
            {
                Vector2 column = LayoutCards(hud);
                var panelObject = new GameObject(PanelName, typeof(RectTransform));
                var rect = (RectTransform)panelObject.transform;
                rect.SetParent(hud.perkChoiceView.transform, false);
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0f, 1f);
                rect.anchoredPosition = new Vector2(column.x, CardsTop(hud));

                var panel = panelObject.AddComponent<PerkShopPanel>();
                panel._hud = hud;
                panel._viewSize = ((RectTransform)hud.perkChoiceView.transform).rect.size;
                int count = PerkShop.Offer.Length;
                float minimum = M.WalletBlockHeight + count * (M.MinEntryHeight + M.EntrySpacing) + M.BuyHeight;
                float height = Mathf.Max(column.y, minimum);
                rect.sizeDelta = new Vector2(M.PanelWidth, height);

                panel.BuildWallet(prefabCard);
                float entryHeight = count > 0 ? Mathf.Clamp((height - M.WalletBlockHeight - M.BuyHeight - count * M.EntrySpacing) / count, M.MinEntryHeight, M.MaxEntryHeight) : 0f;
                panel.Populate(entryHeight);
                panel.LinkNavigation();
                PerkChoiceMenu.LinkNavigation(hud);
                panel.Refresh();
                if (panel.isActiveAndEnabled) panel.StartCoroutine(panel.ReselectNextFrame());
                PerkConfirmButton.Current?.Reposition();
                PerkHint.Current?.Reposition();
            }
            catch (System.Exception ex)
            {
                Logger.LogError($"[Surge shop] could not build the panel: {ex}");
            }
        }

        // Called when a run ends, so a stale panel never waits for the next run's first perk screen; the card root outlives the run, so a plain Survival run after it gets the vanilla layout back
        public static void Clear()
        {
            var hud = SurvivalModeHud.instance;
            if (hud == null || hud.perkChoiceView == null) return;
            Remove(hud);
            RestoreCards(hud);
        }

        public static void Remove(SurvivalModeHud hud)
        {
            PerkChoiceWidget.Remove(hud, PanelName);
        }

        // Centers the cards and the column together; the cards keep their vanilla size unless they don't fit across the view or above Confirm and the hint line. Returns the column's left edge and the cards' height
        private static Vector2 LayoutCards(SurvivalModeHud hud)
        {
            var root = (RectTransform)hud.perkCardsRoot;
            var layout = root.GetComponent<ShopCardLayout>();
            if (layout == null)
            {
                layout = root.gameObject.AddComponent<ShopCardLayout>();
                layout.OriginalPosition = root.anchoredPosition;
                layout.OriginalSize = root.sizeDelta;
            }
            var view = root.parent as RectTransform;
            float viewWidth = view != null ? view.rect.width : 1066f;
            float viewHeight = view != null ? view.rect.height : 600f;
            var group = root.GetComponent<HorizontalLayoutGroup>();
            var cards = PerkChoiceWidget.Cards(hud);
            float spaces = (group != null ? group.spacing : 5f) * Mathf.Max(0, cards.Count - 1);

            float cardsWidth = 0f;
            float cardsHeight = 0f;
            foreach (var card in cards)
            {
                var cardRect = (RectTransform)card.transform;
                Vector3 scale = PerkCardScale.Of(card).Base;
                cardsWidth += cardRect.sizeDelta.x * scale.x;
                cardsHeight = Mathf.Max(cardsHeight, cardRect.sizeDelta.y * scale.y);
            }
            float roomX = viewWidth - 2f * M.SideMargin - M.PanelGap - M.PanelWidth - spaces;
            float below = (PerkConfirmButton.Current != null ? M.ConfirmGap + M.ConfirmHeight : 0f) + PerkHint.Reserve;
            float roomY = CardsTop(hud) + viewHeight / 2f - M.BottomMargin - below;
            float factor = 1f;
            if (cardsWidth > 0f) factor = Mathf.Min(factor, roomX / cardsWidth);
            if (cardsHeight > 0f) factor = Mathf.Min(factor, roomY / cardsHeight);
            factor = Mathf.Max(factor, M.MinCardFactor);
            foreach (var card in cards)
            {
                PerkCardScale.Of(card).SetFactor(factor);
            }

            float width = cardsWidth * factor + spaces;
            float left = layout.OriginalPosition.x - (width + M.PanelGap + M.PanelWidth) / 2f;
            root.sizeDelta = new Vector2(width - viewWidth, layout.OriginalSize.y);
            root.anchoredPosition = new Vector2(left + width / 2f, layout.OriginalPosition.y);
            return new Vector2(left + width + M.PanelGap, cardsHeight * factor);
        }

        private static void RestoreCards(SurvivalModeHud hud)
        {
            foreach (var card in PerkChoiceWidget.Cards(hud))
            {
                PerkCardScale.Of(card).SetFactor(1f);
            }
            var root = hud.perkCardsRoot as RectTransform;
            var layout = root != null ? root.GetComponent<ShopCardLayout>() : null;
            if (layout != null)
            {
                root.anchoredPosition = layout.OriginalPosition;
                root.sizeDelta = layout.OriginalSize;
            }
            if (!ChaosWallet.Enabled || !AbilitySelection.StartingChoiceOpen) return;
            var view = hud.perkChoiceView.transform as RectTransform;
            if (view == null) return;
            float room = CardsTop(hud) + view.rect.height / 2f - M.BottomMargin - M.ConfirmGap - M.ConfirmHeight - PerkHint.Reserve;
            foreach (var card in PerkChoiceWidget.Cards(hud))
            {
                float height = ((RectTransform)card.transform).rect.height * PerkCardScale.Of(card).Base.y;
                if (height > 0f) PerkCardScale.Of(card).SetFactor(Mathf.Clamp(room / height, M.MinCardFactor, 1f));
            }
        }

        // The cards sit at the top of their root, so the panel's top lines up with theirs
        private static float CardsTop(SurvivalModeHud hud)
        {
            var root = hud.perkCardsRoot as RectTransform;
            if (root == null) return 106f;
            return root.anchoredPosition.y + root.sizeDelta.y * (1f - root.pivot.y);
        }

        private TMP_Text CloneText(TMP_Text source, string name)
        {
            var text = Hud.CloneText(source, transform, name, "");
            text.enableAutoSizing = false;
            return text;
        }

        private static void PlaceTop(RectTransform rect, float top, float height, float inset)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -top);
            rect.sizeDelta = new Vector2(-2f * inset, height);
        }

        // The team's points above the shop items
        private void BuildWallet(SurvivalModifierChoiceCard prefabCard)
        {
            _header = CloneText(prefabCard.perkNameText, "Points");
            PlaceTop(_header.rectTransform, 0f, HeaderHeight, 0f);
            _header.fontSize = 14f;
            _header.alignment = TextAlignmentOptions.Center;
            _headerIcon = ChaosIcon.Add(_header.transform, Vector2.zero, 18, Hud.Warm);
            _header.enableWordWrapping = false;
            _header.overflowMode = TextOverflowModes.Overflow;
            var note = CloneText(prefabCard.perkDescriptionText, "Shared wallet explanation");
            PlaceTop(note.rectTransform, 25f, 28f, 0f);
            note.fontSize = 10f;
            note.alignment = TextAlignmentOptions.Center;
            note.enableWordWrapping = true;
            note.text = "Shared by the team\nEach item once per visit";
        }

        private void Populate(float entryHeight)
        {
            float y = M.WalletBlockHeight;
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
                    Destroy(card);
                }

                var rect = (RectTransform)entryObject.transform;
                rect.localScale = Vector3.one;
                PlaceTop(rect, y, entryHeight, 0f);
                y += entryHeight + M.EntrySpacing;

                if (entry.Icon != null)
                {
                    var iconRect = entry.Icon.rectTransform;
                    iconRect.anchorMin = new Vector2(0f, 0.5f);
                    iconRect.anchorMax = new Vector2(0f, 0.5f);
                    iconRect.pivot = new Vector2(0f, 0.5f);
                    entry.Icon.sprite = IconLoader.GetIcon(PerkShop.IconOf(item));
                    entry.Icon.color = Color.white;
                    entry.Icon.preserveAspect = true;
                }
                if (entry.Title != null)
                {
                    SettingsPage.SetText(entry.Title, "");
                    var titleRect = entry.Title.rectTransform;
                    titleRect.pivot = new Vector2(0.5f, 0.5f);
                    SettingsPage.Stretch(titleRect, Vector2.zero, Vector2.zero);
                    entry.Currency = ChaosIcon.Add(entry.Title.transform, new Vector2(7, 0), 13, Hud.Warm, new Vector2(0, .5f));
                    entry.Title.alignment = TextAlignmentOptions.MidlineLeft;
                    entry.Title.enableWordWrapping = false;
                    entry.Title.overflowMode = TextOverflowModes.Ellipsis;
                    entry.Title.enableAutoSizing = true;
                    entry.Title.fontSizeMin = M.MinLabelFontSize;
                    entry.Title.fontSizeMax = 15f;
                }
                if (entry.Description != null)
                {
                    SettingsPage.SetText(entry.Description, "");
                    var descriptionRect = entry.Description.rectTransform;
                    descriptionRect.pivot = new Vector2(0.5f, 0.5f);
                    SettingsPage.Stretch(descriptionRect, Vector2.zero, Vector2.zero);
                    entry.Description.enableAutoSizing = true;
                    entry.Description.fontSizeMin = M.MinLabelFontSize;
                    entry.Description.fontSizeMax = 12f;
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
        }

        private void BuildBuyButton(float y)
        {
            _buy = PerkActionButton.Create(_hud.perkCardPrefab, transform, "Shop Buy", OnBuy, 6f);
            PlaceTop((RectTransform)_buy.transform, y, M.BuyHeight, 0f);
            TMP_Text label = _buy.Label;
            if (label != null)
            {
                label.enableAutoSizing = true;
                label.fontSizeMin = M.MinLabelFontSize;
                label.fontSizeMax = 13f;
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
            Button buy = _buy != null ? _buy.Button : null;
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
                nav.selectOnDown = i < _entries.Count - 1 ? _entries[i + 1].Button : buy;
                nav.selectOnLeft = last;
                nav.selectOnRight = null;
                button.navigation = nav;
            }
            if (buy != null)
            {
                Navigation nav = buy.navigation;
                nav.mode = Navigation.Mode.Explicit;
                nav.selectOnUp = _entries.Count > 0 ? _entries[_entries.Count - 1].Button : null;
                nav.selectOnDown = null;
                nav.selectOnLeft = confirm != null ? confirm : last;
                nav.selectOnRight = null;
                buy.navigation = nav;
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
            ChaosWallet.Changed += Refresh;
            PerkShop.OfferChanged += Rebuild;
        }

        private void OnDisable()
        {
            ChaosWallet.Changed -= Refresh;
            PerkShop.OfferChanged -= Rebuild;
        }

        // What the shop can sell depends on the lives, which a client gets after the points, so the panel redraws when they change
        private void Update()
        {
            if (_hud != null && _viewSize != ((RectTransform)_hud.perkChoiceView.transform).rect.size)
            {
                Build(_hud);
                return;
            }
            SurvivalMode mode = SurvivalMode.instance;
            if (mode == null) return;
            int maxLives = MaxLives(mode);
            if (mode.Lives == _lives && maxLives == _maxLives) return;
            _lives = mode.Lives;
            _maxLives = maxLives;
            Refresh();
        }

        // SurvivalMode.MaxLives finds the modifier with a LINQ lookup, which makes garbage every frame
        private int MaxLives(SurvivalMode mode)
        {
            var manager = ModifierManager.instance;
            if (manager != _modifiers)
            {
                _modifiers = manager;
                int id = manager != null ? manager.GetModId("maxLives") : -1;
                _maxLivesModifier = id >= 0 ? manager.GetModById(id) : null;
            }
            return mode._maxlives.Value + (_maxLivesModifier != null ? _maxLivesModifier.levelInSurvival : 0);
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
            int points = ChaosWallet.Points;
            if (_header != null)
            {
                _header.text = $"TEAM CHAOS <color={Consts.Formatting.ColorGold}>{points}</color>";
                ChaosIcon.CenterBefore(_headerIcon, _header);
            }
            bool selectedCan = false;
            foreach (Entry entry in _entries)
            {
                int cost = PerkShop.CostOf(entry.Item);
                string unavailable = PerkShop.Unavailable(entry.Item);
                bool can = points >= cost && unavailable == null;
                Modifier purchased = entry.Item == ShopItem.Perk ? PerkShop.PurchasedPerk : null;
                bool revealed = purchased?.data != null;
                LayoutEntry(entry, revealed);
                if (entry == _selected) selectedCan = can;
                float alpha = can || revealed ? 1f : 0.45f;
                if (entry.Title != null)
                {
                    bool showCurrency = !revealed && entry.Item != ShopItem.Sacrifice;
                    entry.Currency.gameObject.SetActive(showCurrency);
                    entry.Currency.color = PerkChoiceWidget.Faded(Hud.Warm, alpha);
                    entry.Title.margin = new Vector4(showCurrency ? 18 : 0, 0, 0, 0);
                    entry.Title.text = revealed ? PerkShop.DisplayNameOfPerk(purchased.data.key)
                        : $"<color={Consts.Formatting.ColorGold}>{PerkShop.PriceOf(entry.Item)}</color>  {PerkShop.NameOf(entry.Item)}";
                    entry.Title.color = PerkChoiceWidget.Faded(entry.Title.color, alpha);
                }
                if (entry.Description != null)
                {
                    entry.Description.text = revealed ? PurchasedDescription(purchased)
                        : unavailable ?? (points < cost ? $"Need {cost - points} more point{(cost - points == 1 ? "" : "s")}" : PerkShop.DescriptionOf(entry.Item));
                    entry.Description.color = PerkChoiceWidget.Faded(entry.Description.color, alpha);
                }
                if (entry.Icon != null)
                {
                    entry.Icon.sprite = revealed ? purchased.data.icon : IconLoader.GetIcon(PerkShop.IconOf(entry.Item));
                    entry.Icon.color = PerkChoiceWidget.Faded(Color.white, alpha);
                }
                if (entry.Background != null)
                {
                    Color tint = entry == _selected ? M.LockedCardTint : Color.white;
                    entry.Background.color = PerkChoiceWidget.Faded(tint, can || revealed ? 1f : 0.6f);
                }
            }
            RefreshBuy(selectedCan);
        }

        // Centers the title and description around the middle and keeps them inside the card's frame; a bought perk shows a small icon beside its name and the description across the card
        private static void LayoutEntry(Entry entry, bool revealed)
        {
            if (entry.Button == null) return;
            float height = ((RectTransform)entry.Button.transform).rect.height;
            float titleBottom = Mathf.Min(height / 2f + 1f, height - TitleTop - TitleHeight);
            float iconSize = revealed ? TitleHeight : Mathf.Min(height - 2f * EntryInset, 52f);
            float textLeft = EntryInset + iconSize + IconGap;
            if (entry.Icon != null)
            {
                entry.Icon.rectTransform.sizeDelta = new Vector2(iconSize, iconSize);
                entry.Icon.rectTransform.anchoredPosition = new Vector2(EntryInset, revealed ? titleBottom + TitleHeight / 2f - height / 2f : 0f);
            }
            if (entry.Title != null)
            {
                entry.Title.rectTransform.offsetMin = new Vector2(textLeft, titleBottom);
                entry.Title.rectTransform.offsetMax = new Vector2(-EntryInset, titleBottom + TitleHeight - height);
            }
            if (entry.Description != null)
            {
                entry.Description.rectTransform.offsetMin = new Vector2(revealed ? EntryInset : textLeft, EntryInset - 4f);
                entry.Description.rectTransform.offsetMax = new Vector2(-EntryInset, titleBottom - 2f - height);
            }
        }

        private static string PurchasedDescription(Modifier perk)
        {
            var perks = PerksManager.Instance;
            return perks != null && perks.GetAllPerkNames().Contains(perk.data.key)
                ? perks.GetDescription(perk.data.key) : (string)perk.data.description;
        }

        private void RefreshBuy(bool selectedCan)
        {
            if (_buy == null || _buy.Button == null) return;
            bool can = _selected != null && selectedCan;
            _buy.Show(can, _selected != null && _selected.Button != null ? _selected.Button : null, _hud);
            if (_buy.Label != null)
            {
                _buy.Label.text = _selected == null ? "Pick an item to buy"
                    : _selected.Item == ShopItem.Perk && PerkShop.PurchasedPerk != null ? "Purchased"
                    : $"Buy {PerkShop.NameOf(_selected.Item)} for <color={Consts.Formatting.ColorGold}>{PerkShop.PriceOf(_selected.Item)}</color>";
            }
        }

        // After a reroll the selected card is gone with the old cards, so the view's first card is selected again; a view that is still hidden selects its own when it opens
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
