using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using SpiderSurge.Common;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace SpiderSurge
{
    internal sealed class PersonalChoicePanel : MonoBehaviour
    {
        private static readonly AccessTools.FieldRef<SurvivalModeHud, List<SurvivalModifierChoiceCard>> CardList = AccessTools.FieldRefAccess<SurvivalModeHud, List<SurvivalModifierChoiceCard>>("survivalModifierChoiceCards");
        private static PersonalChoicePanel current;
        private int epoch;
        private SurvivalModeHud hud;
        private TMP_Text readyCount;
        private readonly List<Transform> markerRoots = new List<Transform>();
        private readonly Dictionary<int, PlayerMarker> markers = new Dictionary<int, PlayerMarker>();
        private readonly HashSet<int> released = new HashSet<int>();
        private readonly Dictionary<int, float> nextMove = new Dictionary<int, float>();
        private readonly List<GameObject> hidden = new List<GameObject>();
        private float createdAt;
        private EventSystem inputEvents;
        private bool navigationEnabled;
        private bool suppressingNavigation;

        private sealed class PlayerMarker
        {
            public Image Image;
            public TMP_Text Label;
            public Outline Outline;
        }

        internal static void Show()
        {
            if (!AbilitySelection.IsChoosing || LevelController.IsLoading) return;
            var level = LevelController.instance;
            string scene = SceneManager.GetActiveScene().name;
            if (level != null && (scene == level.lobby.name || scene == level.waitingRoom.name)) return;
            if (HudController.instance != null && HudController.instance.paused) return;
            if (current != null && current.epoch == AbilitySelection.Round.Epoch)
            {
                if (!current.hud.perkChoiceView.IsVisible || current.hud.perkChoiceView.IsHiding)
                    current.hud.perkChoiceView.InstantShow();
                return;
            }
            var hud = SurvivalModeHud.instance;
            if (hud == null || hud.perkChoiceView == null || hud.perkCardPrefab == null || SurvivalMode.instance == null) return;
            Hide();
            current = hud.gameObject.AddComponent<PersonalChoicePanel>();
            current.hud = hud;
            current.epoch = AbilitySelection.Round.Epoch;
            current.createdAt = Time.unscaledTime;
            current.Build();
        }

        internal static void Hide()
        {
            if (current == null) return;
            current.RestoreNavigation();
            foreach (var item in current.hidden) if (item != null) item.SetActive(true);
            foreach (var root in current.markerRoots) if (root != null) Destroy(root.gameObject);
            if (current.readyCount != null) Destroy(current.readyCount.gameObject);
            current.hud?.ClosePerkChoiseView();
            current.enabled = false;
            Destroy(current);
            current = null;
        }

        // Reuse the native view, card prefab and layout on every peer; the host's choice state supplies their contents.
        private void Build()
        {
            PerkConfirmButton.Clear();
            PerkShopPanel.Clear();
            PerkHint.Clear();
            foreach (Transform child in hud.perkCardsRoot)
            {
                child.gameObject.SetActive(false);
                Destroy(child.gameObject);
            }
            var cards = CardList(hud);
            cards.Clear();
            PerkChoiceWidget.ResetChoice();
            int count = AbilitySelection.Round.Offers.Length;
            float scale = Mathf.Clamp(1.45f - count * .15f, .55f, 1f);
            for (int i = 0; i < count; i++)
            {
                var card = Instantiate(hud.perkCardPrefab, hud.perkCardsRoot).GetComponent<SurvivalModifierChoiceCard>();
                card.transform.localScale = Vector3.one * scale;
                string key = AbilitySelection.Round.Offers[i];
                card.SetupCard(ModifierManager.instance.GetModById(ModifierManager.instance.GetModId(key)), SurvivalMode.instance._defaultConfig.maps[0], i, false);
                var description = card.perkDescriptionText;
                description.text = System.Text.RegularExpressions.Regex.Replace(description.text, @"</?color[^>]*>", "");
                description.alignment = TextAlignmentOptions.Top;
                description.enableAutoSizing = true;
                description.fontSizeMin = 10;
                description.fontSizeMax = 13;
                description.rectTransform.anchorMin = new Vector2(0, .5f);
                description.rectTransform.anchorMax = new Vector2(1, .5f);
                description.rectTransform.pivot = new Vector2(.5f, 1);
                description.rectTransform.sizeDelta = new Vector2(-28, 100);
                description.rectTransform.anchoredPosition = new Vector2(-2, 20);
                card.mapImage.gameObject.SetActive(false);
                card.mapNameText.gameObject.SetActive(false);
                foreach (var vote in card.voteImages) if (vote != null) vote.gameObject.SetActive(false);
                var root = new GameObject("Personal players", typeof(RectTransform)).GetComponent<RectTransform>();
                root.SetParent(card.transform, false);
                root.anchorMin = root.anchorMax = new Vector2(.5f, 0);
                root.sizeDelta = new Vector2(220, 72);
                root.anchoredPosition = new Vector2(0, 45);
                markerRoots.Add(root);
                int index = i;
                card.gameObject.AddComponent<PersonalChoicePointer>().Click = () => PointerPick(index);
                cards.Add(card);
            }
            PerkChoiceMenu.Attach(hud);
            PerkChoiceHud.Attach(hud);
            foreach (var card in cards) card.cardButton.navigation = new Navigation { mode = Navigation.Mode.None };
            var heading = hud.perkChoiceView.transform.Find("Heading")?.GetComponent<TMP_Text>();
            if (heading != null)
            {
                var localize = heading.GetComponent<I2.Loc.Localize>();
                if (localize != null) localize.enabled = false;
                heading.text = AbilitySelection.Round.Ultimate ? "CHOOSE YOUR ULTIMATE" : "CHOOSE YOUR ABILITY";
            }
            foreach (var timer in hud.perkChoiceView.GetComponentsInChildren<PerkChoiseTimer>(true)) HidePart(timer.gameObject);
            foreach (var tag in hud.perkChoiceView.GetComponentsInChildren<PlayerGameTag>(true)) HidePart(tag.gameObject);
            HidePart(hud.perkSelectTwitch.gameObject);
            readyCount = Text(cards[0].perkDescriptionText, hud.perkChoiceView.transform, "Personal choice controls", new Vector2(900, 70), 14);
            readyCount.rectTransform.anchorMin = readyCount.rectTransform.anchorMax = new Vector2(.5f, 0);
            readyCount.rectTransform.anchoredPosition = new Vector2(0, 65);
            hud.perkChoiceView.SelectedButton = null;
            hud.perkChoiceView.InstantShow();
            SetNavigation(false);
            EventSystem.current?.SetSelectedGameObject(null);
            Refresh();
        }

        private void HidePart(GameObject item)
        {
            if (!item.activeSelf) return;
            hidden.Add(item);
            item.SetActive(false);
        }

        private void SetNavigation(bool enabled)
        {
            if (enabled) { RestoreNavigation(); return; }
            if (suppressingNavigation) return;
            inputEvents = EventSystem.current;
            if (inputEvents == null) return;
            navigationEnabled = inputEvents.sendNavigationEvents;
            inputEvents.sendNavigationEvents = false;
            suppressingNavigation = true;
        }

        private void RestoreNavigation()
        {
            if (suppressingNavigation && inputEvents != null) inputEvents.sendNavigationEvents = navigationEnabled;
            suppressingNavigation = false;
        }

        private static TMP_Text Text(TMP_Text template, Transform parent, string name, Vector2 size, float fontSize)
        {
            var text = Instantiate(template, parent);
            text.gameObject.name = name;
            text.gameObject.SetActive(true);
            SettingsPage.SetText(text, "");
            text.rectTransform.localScale = Vector3.one;
            text.rectTransform.anchorMin = text.rectTransform.anchorMax = new Vector2(.5f, .5f);
            text.rectTransform.pivot = new Vector2(.5f, .5f);
            text.rectTransform.sizeDelta = size;
            text.rectTransform.anchoredPosition = Vector2.zero;
            text.fontSize = fontSize;
            text.enableAutoSizing = false;
            text.enableWordWrapping = true;
            text.alignment = TextAlignmentOptions.Center;
            text.color = Color.white;
            text.raycastTarget = false;
            return text;
        }

        private void Update()
        {
            bool visible = !(HudController.instance != null && HudController.instance.paused) && !LevelController.IsLoading;
            SetNavigation(!visible);
            if (!visible) { released.Clear(); return; }
            Refresh();
            if (Time.unscaledTime - createdAt < .25f) return;
            foreach (var player in Players.All ?? new List<PlayerController>())
            {
                if (!Players.IsLocal(player)) continue;
                int id = player.playerID.Value;
                if (!AbilitySelection.Round.Picks.TryGetValue(id, out var pick)) continue;
                var input = PlayerInput.GetPlayerByIndex(player.playerInputIndex.Value);
                if (input == null) continue;
                foreach (var device in input.devices)
                {
                    if (Controls.Ignored(device)) continue;
                    float direction = 0;
                    bool submit = false, cancel = false, held = false;
                    if (device is Gamepad pad)
                    {
                        direction = Mathf.Abs(pad.dpad.x.ReadValue()) > .5f ? pad.dpad.x.ReadValue() : pad.leftStick.x.ReadValue();
                        submit = pad.buttonSouth.wasPressedThisFrame; cancel = pad.buttonEast.wasPressedThisFrame;
                        held = pad.buttonSouth.isPressed || pad.buttonEast.isPressed;
                    }
                    else if (device is Keyboard keyboard)
                    {
                        direction = keyboard.leftArrowKey.isPressed ? -1 : keyboard.rightArrowKey.isPressed ? 1 : 0;
                        submit = keyboard.enterKey.wasPressedThisFrame; cancel = keyboard.backspaceKey.wasPressedThisFrame;
                        held = keyboard.enterKey.isPressed || keyboard.backspaceKey.isPressed;
                    }
                    else continue;
                    if (!released.Contains(id)) { if (!held && Mathf.Abs(direction) < .4f) released.Add(id); continue; }
                    if (cancel) AbilitySelection.Choose(id, pick.Index, false);
                    else if (submit) AbilitySelection.Choose(id, pick.Index, true);
                    else if (!pick.Ready && Mathf.Abs(direction) > .6f && (!nextMove.TryGetValue(id, out float next) || Time.unscaledTime >= next))
                    {
                        int index = (pick.Index + (direction > 0 ? 1 : -1) + markerRoots.Count) % markerRoots.Count;
                        AbilitySelection.Choose(id, index, false);
                        nextMove[id] = Time.unscaledTime + .23f;
                    }
                    else if (Mathf.Abs(direction) < .4f) nextMove.Remove(id);
                }
            }
        }

        private static int PointerPlayer()
        {
            foreach (var player in Players.All ?? new List<PlayerController>())
            {
                if (!Players.IsLocal(player)) continue;
                var input = PlayerInput.GetPlayerByIndex(player.playerInputIndex.Value);
                if (input != null && input.devices.Any(device => device is Keyboard || device is Mouse)) return player.playerID.Value;
            }
            var local = (Players.All ?? new List<PlayerController>()).FirstOrDefault(Players.IsLocal);
            return local != null ? local.playerID.Value : -1;
        }

        private void PointerPick(int index)
        {
            int id = PointerPlayer();
            if (!AbilitySelection.Round.Picks.TryGetValue(id, out var pick)) return;
            AbilitySelection.Choose(id, index, pick.Index != index || !pick.Ready);
        }

        private void Refresh()
        {
            var round = AbilitySelection.Round;
            readyCount.text = $"{round.Picks.Values.Count(pick => pick.Ready)} / {round.Picks.Count} LOCKED IN";
            foreach (int id in markers.Keys.Except(round.Picks.Keys).ToArray())
            {
                Destroy(markers[id].Image.gameObject);
                markers.Remove(id);
            }
            for (int i = 0; i < markerRoots.Count; i++)
            {
                var picks = round.Picks.Where(pair => pair.Value.Index == i).OrderBy(pair => pair.Key).ToArray();
                int columns = Mathf.Min(4, Mathf.Max(1, picks.Length));
                int rows = Mathf.Max(1, Mathf.CeilToInt(picks.Length / (float)columns));
                float size = Mathf.Min(40, 72f / rows);
                for (int n = 0; n < picks.Length; n++)
                {
                    var pair = picks[n];
                    if (!markers.TryGetValue(pair.Key, out var marker))
                    {
                        var icon = new GameObject("Player " + (pair.Key + 1), typeof(RectTransform), typeof(Image), typeof(Outline)).GetComponent<Image>();
                        var template = hud.perkCardPrefab.GetComponent<SurvivalModifierChoiceCard>();
                        icon.sprite = template.voteImages.FirstOrDefault(vote => vote != null)?.sprite;
                        icon.raycastTarget = false;
                        marker = new PlayerMarker { Image = icon, Outline = icon.GetComponent<Outline>(), Label = Text(template.perkNameText, icon.transform, "Player label", new Vector2(48, 22), 12) };
                        markers.Add(pair.Key, marker);
                    }
                    var controller = Players.ById(pair.Key);
                    Color color = controller != null ? Hud.Readable(controller.playerColor.Value) : Color.white;
                    marker.Image.transform.SetParent(markerRoots[i], false);
                    marker.Image.rectTransform.sizeDelta = new Vector2(size * .8f, size * .8f);
                    int rowCount = Mathf.Min(columns, picks.Length - n / columns * columns);
                    marker.Image.rectTransform.anchoredPosition = new Vector2((n % columns - (rowCount - 1) / 2f) * 52, (rows - 1) * size / 2 - n / columns * size);
                    marker.Image.color = new Color(color.r, color.g, color.b, pair.Value.Ready ? 1f : .5f);
                    marker.Outline.effectColor = pair.Value.Ready ? Color.white : color;
                    marker.Outline.effectDistance = pair.Value.Ready ? new Vector2(2, 2) : Vector2.one;
                    marker.Label.text = $"P{pair.Key + 1}" + (pair.Value.Ready ? " \u2713" : "");
                    marker.Label.fontSize = Mathf.Min(12, size * .45f);
                    marker.Label.rectTransform.anchoredPosition = new Vector2(0, -size * .55f);
                    marker.Label.color = Color.white;
                }
            }
        }

        private void OnDestroy() => RestoreNavigation();
    }

    internal sealed class PersonalChoicePointer : MonoBehaviour, IPointerClickHandler
    {
        public Action Click;
        public void OnPointerClick(PointerEventData data)
        {
            if (data.button == PointerEventData.InputButton.Left) Click?.Invoke();
        }
    }
}
