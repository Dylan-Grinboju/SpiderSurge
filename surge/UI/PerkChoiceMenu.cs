using Doozy.Engine.UI;
using HarmonyLib;
using SpiderSurge.Common;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SpiderSurge
{
    public sealed class PerkChoiceMenu : MonoBehaviour
    {
        private SurvivalModeHud _hud;
        private Button _button;
        private CanvasGroup _group;
        private GameObject _focus;
        private GameObject _menuFocus;
        private bool _suspended;
        private bool _restoreFocus;
        private float _alpha;
        private bool _interactable;
        private bool _blocksRaycasts;

        public static bool Choosing
        {
            get
            {
                var hud = SurvivalModeHud.instance;
                var view = hud != null ? hud.perkChoiceView : null;
                return view != null && view.gameObject.activeInHierarchy && !view.IsHiding
                    && (view.IsVisible || view.IsShowing) && !LevelController.IsLoading
                    && (!SurgeGameModeManager.IsSurgeRunActive || !PerkChoiceWidget.ChoiceMade);
            }
        }

        public static void Attach(SurvivalModeHud hud)
        {
            if (hud == null || hud.perkChoiceView == null || hud.perkCardPrefab == null) return;
            var menu = hud.GetComponent<PerkChoiceMenu>() ?? hud.gameObject.AddComponent<PerkChoiceMenu>();
            menu._hud = hud;
            if (menu._button == null) menu.Build();
            menu.Relink();
        }

        private void Build()
        {
            var card = _hud.perkCardPrefab.GetComponent<SurvivalModifierChoiceCard>();
            if (card == null || card.perkNameText == null) return;
            var target = new GameObject("SurgePerkMenu", typeof(RectTransform), typeof(LayoutElement), typeof(Image), typeof(Button));
            target.transform.SetParent(_hud.perkChoiceView.transform, false);
            target.GetComponent<LayoutElement>().ignoreLayout = true;
            var rect = (RectTransform)target.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one;
            rect.anchoredPosition = new Vector2(-27f, -22f);
            rect.sizeDelta = new Vector2(110f, 34f);
            var image = target.GetComponent<Image>();
            var template = _hud.perkCardPrefab.GetComponent<Image>();
            if (template != null) image.sprite = template.sprite;
            image.type = Image.Type.Sliced;
            _button = target.GetComponent<Button>();
            _button.targetGraphic = image;
            _button.onClick.AddListener(Open);
            if (HudController.instance != null && HudController.instance.resume != null) _button.colors = HudController.instance.resume.colors;
            var label = Instantiate(card.perkNameText.gameObject, target.transform).GetComponent<TMP_Text>();
            SettingsPage.SetText(label, "Menu");
            label.fontSize = 14f;
            label.enableAutoSizing = false;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;
            label.raycastTarget = false;
            var labelRect = label.rectTransform;
            labelRect.localScale = Vector3.one;
            SettingsPage.Stretch(labelRect, new Vector2(4f, 2f), new Vector2(-4f, -2f));
            _group = _hud.perkChoiceView.GetComponent<CanvasGroup>() ?? _hud.perkChoiceView.gameObject.AddComponent<CanvasGroup>();
        }

        private void Open()
        {
            var hud = HudController.instance;
            if (Choosing && hud != null && !hud.paused) hud.Pause();
        }

        public static void LinkNavigation(SurvivalModeHud hud) => hud?.GetComponent<PerkChoiceMenu>()?.Relink();

        private void Relink()
        {
            if (_button == null || _hud == null) return;
            var cards = PerkChoiceWidget.Cards(_hud);
            for (int i = 0; i < cards.Count; i++)
            {
                var button = cards[i].cardButton;
                if (button == null) continue;
                Navigation nav = button.navigation;
                if (nav.mode != Navigation.Mode.Explicit)
                {
                    nav.selectOnLeft = i > 0 ? cards[i - 1].cardButton : null;
                    nav.selectOnRight = i + 1 < cards.Count ? cards[i + 1].cardButton : null;
                }
                nav.mode = Navigation.Mode.Explicit;
                nav.selectOnUp = _button;
                button.navigation = nav;
            }
            Navigation menuNav = new Navigation { mode = Navigation.Mode.Explicit };
            menuNav.selectOnDown = PerkConfirm.Locked != null ? PerkConfirm.Locked.cardButton : PerkChoiceWidget.FirstCard(_hud);
            _button.navigation = menuNav;
        }

        public static void RememberFocus()
        {
            var hud = SurvivalModeHud.instance;
            var menu = hud != null ? hud.GetComponent<PerkChoiceMenu>() : null;
            if (menu == null || !Choosing) return;
            var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            menu._focus = selected != null && selected.transform.IsChildOf(hud.perkChoiceView.transform) ? selected : null;
        }

        private void Awake() => PerkConfirm.LockedChanged += Relink;
        private void OnDestroy() => PerkConfirm.LockedChanged -= Relink;
        private void LateUpdate() => UpdateVisibility();

        // Hide the choice without disabling its widgets or rebuilding cards, so the locked choice and shop state survive the menu.
        public void UpdateVisibility()
        {
            if (_group == null || _hud == null || HudController.instance == null) return;
            var hud = HudController.instance;
            var view = _hud.perkChoiceView;
            bool visible = view != null && !view.IsHiding && (view.IsVisible || view.IsShowing);
            if (!visible)
            {
                _restoreFocus = false;
                var inactiveSelection = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
                if (!hud.paused && inactiveSelection != null && view != null && inactiveSelection.transform.IsChildOf(view.transform))
                    EventSystem.current.SetSelectedGameObject(null);
            }
            if (hud.paused && visible)
            {
                if (!_suspended)
                {
                    _alpha = _group.alpha;
                    _interactable = _group.interactable;
                    _blocksRaycasts = _group.blocksRaycasts;
                    _suspended = true;
                }
                _group.alpha = 0f;
                _group.interactable = _group.blocksRaycasts = false;
                var eventSystem = EventSystem.current;
                if (eventSystem != null)
                {
                    var current = eventSystem.currentSelectedGameObject;
                    if (current != null && !current.transform.IsChildOf(view.transform)) _menuFocus = current;
                    else
                    {
                        var menuView = _menuFocus != null ? _menuFocus.GetComponentInParent<UIView>() : null;
                        var control = _menuFocus != null ? _menuFocus.GetComponent<Selectable>() : null;
                        if (menuView != null && (menuView.IsVisible || menuView.IsShowing) && !menuView.IsHiding
                            && control != null && control.isActiveAndEnabled && control.IsInteractable())
                            eventSystem.SetSelectedGameObject(_menuFocus);
                        else if (hud.pauseView != null && hud.pauseView.IsVisible && hud.resume != null)
                            eventSystem.SetSelectedGameObject(hud.resume.gameObject);
                    }
                }
                return;
            }
            if (_suspended)
            {
                _suspended = false;
                if (visible)
                {
                    _group.alpha = _alpha;
                    _group.interactable = _interactable;
                    _group.blocksRaycasts = _blocksRaycasts;
                    _restoreFocus = true;
                }
            }
            if (!_restoreFocus || hud.paused || !visible || (hud.pauseView != null && !hud.pauseView.IsHidden)) return;
            _restoreFocus = false;
            if (EventSystem.current == null || !Choosing) return;
            var selected = _focus != null ? _focus.GetComponent<Selectable>() : null;
            if (selected == null || !selected.isActiveAndEnabled || !selected.IsInteractable()) selected = PerkChoiceWidget.FirstCard(_hud);
            if (selected != null) EventSystem.current.SetSelectedGameObject(selected.gameObject);
        }
    }

    [HarmonyPatch(typeof(HudController), nameof(HudController.HidePauseView))]
    internal static class PerkChoiceMenuKeepPausePatch
    {
        static bool Prefix(HudController __instance) => !__instance.paused;
    }

    [HarmonyPatch(typeof(HudController), nameof(HudController.Pause))]
    internal static class PerkChoiceMenuPausePatch
    {
        static void Prefix(HudController __instance)
        {
            if (!__instance.paused) PerkChoiceMenu.RememberFocus();
        }

        static void Postfix()
        {
            var hud = SurvivalModeHud.instance;
            if (hud != null) hud.GetComponent<PerkChoiceMenu>()?.UpdateVisibility();
        }
    }
}
