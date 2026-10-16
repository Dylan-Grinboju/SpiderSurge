using System;
using System.Collections.Generic;
using Doozy.Engine.UI;
using HarmonyLib;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace SpiderTeams
{
    internal sealed class VersusTeamsPage : MonoBehaviour
    {
        private readonly List<Transform> _originals = new List<Transform>();
        private readonly List<GameObject> _created = new List<GameObject>();
        private readonly Dictionary<Selectable, Navigation> _navigation = new Dictionary<Selectable, Navigation>();
        private UIView _view;
        private VerticalLayoutGroup _layout;
        private RectTransform _content;
        private ScrollRect _scroll;
        private RectTransform _viewport;
        private Transform _rowTemplate;
        private Button _back;
        private Transform _title;
        private Vector2 _titlePosition;
        private Vector2 _backPosition;
        private Vector2 _titleAnchorMin, _titleAnchorMax, _backAnchorMin, _backAnchorMax;
        private Navigation _backNavigation;
        private GameObject _selected;
        private bool _dirty;
        private bool _remoteClient;
        private bool _toTop = true;
        private int _rosterSignature;

        public static void Install(VersusSettingsUi ui)
        {
            foreach (string name in new[] { "View - StartVersus", "View - StartCustomVersus" })
            {
                Transform view = ui.transform.Find(name);
                if (view != null && view.GetComponent<VersusTeamsPage>() == null)
                {
                    view.gameObject.AddComponent<VersusTeamsPage>().Build();
                }
            }
        }

        public static void RemoveAll()
        {
            foreach (VersusTeamsPage page in Resources.FindObjectsOfTypeAll<VersusTeamsPage>())
            {
                if (page.gameObject.scene.IsValid()) Object.DestroyImmediate(page);
            }
        }

        // Keep the native title and Back button fixed while the original options and Teams share a scroll area.
        private void Build()
        {
            _view = GetComponent<UIView>();
            _layout = GetComponent<VerticalLayoutGroup>();
            _rowTemplate = transform.Find("WinningScore");
            _back = transform.Find("Button - Cancel").GetComponent<Button>();
            _backNavigation = _back.navigation;
            _title = transform.Find("Title") ?? transform.Find("Text (TMP)");
            foreach (Transform child in transform) _originals.Add(child);
            foreach (Selectable control in GetComponentsInChildren<Selectable>(true)) _navigation[control] = control.navigation;
            RectTransform title = (RectTransform)_title;
            RectTransform back = (RectTransform)_back.transform;
            _titlePosition = title.anchoredPosition;
            _backPosition = back.anchoredPosition;
            _titleAnchorMin = title.anchorMin;
            _titleAnchorMax = title.anchorMax;
            _backAnchorMin = back.anchorMin;
            _backAnchorMax = back.anchorMax;
            _layout.enabled = false;
            title.anchorMin = title.anchorMax = new Vector2(0.5f, 1f);
            title.anchoredPosition = new Vector2(0f, -32f);
            back.anchorMin = back.anchorMax = new Vector2(0.5f, 0f);
            back.anchoredPosition = new Vector2(0f, 30f);

            _scroll = new GameObject("Versus settings scroll", typeof(RectTransform), typeof(ScrollRect)).GetComponent<ScrollRect>();
            _scroll.transform.SetParent(transform, false);
            Stretch((RectTransform)_scroll.transform, new Vector2(65f, 65f), new Vector2(-65f, -65f));
            _viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D), typeof(Image)).GetComponent<RectTransform>();
            _viewport.SetParent(_scroll.transform, false);
            Stretch(_viewport, Vector2.zero, Vector2.zero);
            _viewport.GetComponent<Image>().color = Color.clear;
            _content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter)).GetComponent<RectTransform>();
            _content.SetParent(_viewport, false);
            _content.anchorMin = new Vector2(0f, 1f);
            _content.anchorMax = Vector2.one;
            _content.pivot = new Vector2(0.5f, 1f);
            _content.sizeDelta = Vector2.zero;
            var contentLayout = _content.GetComponent<VerticalLayoutGroup>();
            contentLayout.childAlignment = TextAnchor.UpperCenter;
            contentLayout.childControlWidth = false;
            contentLayout.childControlHeight = false;
            contentLayout.childForceExpandWidth = false;
            contentLayout.childForceExpandHeight = false;
            contentLayout.spacing = 12f;
            contentLayout.padding = new RectOffset(0, 0, 8, 16);
            _content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            _scroll.viewport = _viewport;
            _scroll.content = _content;
            _scroll.horizontal = false;
            _scroll.movementType = ScrollRect.MovementType.Clamped;
            _scroll.scrollSensitivity = 35f;
            AddScrollbar();
            foreach (Transform child in _originals)
            {
                if (child != _title && child != _back.transform) child.SetParent(_content, false);
            }
            ModConfig.Changed += Refresh;
            TeamManager.RosterChanged += Refresh;
            Rebuild();
            _scroll.verticalNormalizedPosition = 1f;
        }

        private static void Stretch(RectTransform rect, Vector2 min, Vector2 max)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = min;
            rect.offsetMax = max;
        }

        private void AddScrollbar()
        {
            var bar = new GameObject("Scrollbar", typeof(RectTransform), typeof(Image), typeof(Scrollbar)).GetComponent<Scrollbar>();
            bar.transform.SetParent(_scroll.transform, false);
            RectTransform rect = (RectTransform)bar.transform;
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0f, 0.5f);
            rect.sizeDelta = new Vector2(8f, 0f);
            rect.anchoredPosition = new Vector2(12f, 0f);
            bar.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.1f);
            var handle = new GameObject("Handle", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            handle.transform.SetParent(bar.transform, false);
            Stretch((RectTransform)handle.transform, Vector2.zero, Vector2.zero);
            handle.color = new Color(1f, 1f, 1f, 0.65f);
            bar.handleRect = (RectTransform)handle.transform;
            bar.targetGraphic = handle;
            bar.direction = Scrollbar.Direction.BottomToTop;
            bar.navigation = new Navigation { mode = Navigation.Mode.None };
            _scroll.verticalScrollbar = bar;
        }

        private void Refresh() => _dirty = true;

        private void OnEnable()
        {
            _dirty = true;
            _selected = null;
            _toTop = true;
            if (_scroll != null) _scroll.verticalNormalizedPosition = 1f;
        }

        private void LateUpdate()
        {
            if (_content == null || !_view.IsVisible) return;
            int signature = TeamManager.IsHost ? 1 : 0;
            var players = TeamManager.Players;
            if (players != null)
            {
                foreach (PlayerController player in players) signature = unchecked(signature * 31 + (player != null ? player.GetInstanceID() : 0));
            }
            if (signature != _rosterSignature)
            {
                _rosterSignature = signature;
                _dirty = true;
            }
            if (_dirty) Rebuild();
            GameObject selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            if (_toTop)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(_content);
                _scroll.verticalNormalizedPosition = 1f;
                _selected = selected;
                _toTop = false;
            }
            if (_selected != selected)
            {
                _selected = selected;
                if (selected != null && selected.transform.IsChildOf(_content)) Reveal((RectTransform)selected.transform);
            }
        }

        private void Rebuild()
        {
            _dirty = false;
            float scroll = _scroll.verticalNormalizedPosition;
            GameObject selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            int selection = _created.FindIndex(item => selected != null && selected.transform.IsChildOf(item.transform));
            int column = selection >= 0 ? Array.FindIndex(_created[selection].GetComponentsInChildren<Selectable>(), control => control.gameObject == selected) : 0;
            foreach (GameObject item in _created)
            {
                item.SetActive(false);
                Object.Destroy(item);
            }
            _created.Clear();
            NetworkManager network = NetworkManager.Singleton;
            _remoteClient = network != null && network.IsListening && !network.IsHost;
            TeamsSettingsPage.Build(this);
            ConfigureNavigation();
            LayoutRebuilder.ForceRebuildLayoutImmediate(_content);
            _scroll.verticalNormalizedPosition = scroll;
            if (selection >= 0 && EventSystem.current != null)
            {
                for (int i = Mathf.Min(selection, _created.Count - 1); i >= 0; i--)
                {
                    Selectable[] controls = _created[i].GetComponentsInChildren<Selectable>();
                    Selectable control = controls.Length > 0 ? controls[Mathf.Clamp(column, 0, controls.Length - 1)] : null;
                    if (control != null && control.IsInteractable())
                    {
                        control.Select();
                        break;
                    }
                }
            }
        }

        public void Header(string text) => Paragraph("<b>" + text.ToUpperInvariant() + "</b>");
        public void Note(string text) => Paragraph("<color=#AAAAAA>" + text + "</color>");
        public void Warning(string text) => Paragraph("<color=#FFAA66>" + text + "</color>");

        public void PlacePrimaryControls()
        {
            Transform before = _content.Find(name == "View - StartVersus" ? "VersusTypes" : "DeathZone");
            if (before == null) return;
            foreach (GameObject item in _created) item.transform.SetSiblingIndex(before.GetSiblingIndex());
        }

        public void Spacer()
        {
            var spacer = new GameObject("Teams spacing", typeof(RectTransform));
            spacer.transform.SetParent(_content, false);
            ((RectTransform)spacer.transform).sizeDelta = new Vector2(720f, 8f);
            _created.Add(spacer);
        }

        public void Paragraph(string value)
        {
            TMP_Text text = Object.Instantiate(_rowTemplate.Find("Label").GetComponent<TMP_Text>(), _content);
            SetText(text, value);
            text.name = "Teams text";
            text.fontSize = 18f;
            text.enableAutoSizing = false;
            text.enableWordWrapping = true;
            text.alignment = TextAlignmentOptions.TopLeft;
            text.raycastTarget = false;
            text.transform.localScale = Vector3.one;
            text.rectTransform.sizeDelta = new Vector2(720f, text.GetPreferredValues(value, 720f, float.PositiveInfinity).y + 4f);
            _created.Add(text.gameObject);
        }

        public void Checkbox(string label, bool value, Action<bool> toggle)
        {
            Choice(label, value ? "On" : "Off", () => toggle(!value), () => toggle(!value));
        }

        public void Choice(string label, string value, Action previous, Action next)
        {
            Transform row = Object.Instantiate(_rowTemplate, _content);
            row.name = "Teams - " + label;
            row.gameObject.SetActive(true);
            row.localScale = Vector3.one;
            ((RectTransform)row).sizeDelta = new Vector2(720f, 36f);
            var layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.spacing = 8f;
            TMP_Text title = row.Find("Label").GetComponent<TMP_Text>();
            TMP_Text setting = row.Find("Setting").GetComponent<TMP_Text>();
            SetText(title, label);
            SetText(setting, value);
            StyleText(title, 360f);
            StyleText(setting, 220f);
            PrepareButton(row.Find("Button - Back").gameObject, previous);
            PrepareButton(row.Find("Button - Forward").gameObject, next);
            _created.Add(row.gameObject);
        }

        public void Button(string label, Action action)
        {
            GameObject target = Object.Instantiate(_back.gameObject, _content);
            target.name = "Teams - " + label;
            target.transform.localScale = Vector3.one;
            ((RectTransform)target.transform).sizeDelta = new Vector2(720f, 36f);
            TMP_Text text = target.GetComponentInChildren<TMP_Text>();
            SetText(text, label);
            text.fontSize = 20f;
            text.enableAutoSizing = false;
            PrepareButton(target, action);
            _created.Add(target);
        }

        private Button PrepareButton(GameObject target, Action action)
        {
            UIButton doozy = target.GetComponent<UIButton>();
            if (doozy != null) Object.DestroyImmediate(doozy);
            Button button = target.GetComponent<Button>();
            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener(() =>
            {
                NetworkManager network = NetworkManager.Singleton;
                if (network != null && network.IsListening && !network.IsHost) return;
                action();
                Refresh();
            });
            button.interactable = !_remoteClient;
            var size = target.GetComponent<LayoutElement>() ?? target.AddComponent<LayoutElement>();
            size.minWidth = size.preferredWidth = target.name.StartsWith("Button -", StringComparison.Ordinal) ? 40f : 720f;
            size.minHeight = size.preferredHeight = 36f;
            size.flexibleWidth = size.flexibleHeight = 0f;
            foreach (TMP_Text text in target.GetComponentsInChildren<TMP_Text>()) SetText(text, text.text);
            return button;
        }

        private static void StyleText(TMP_Text text, float width)
        {
            text.fontSize = 19f;
            text.enableAutoSizing = false;
            text.enableWordWrapping = false;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            var layout = text.GetComponent<LayoutElement>() ?? text.gameObject.AddComponent<LayoutElement>();
            layout.minWidth = layout.preferredWidth = width;
            layout.flexibleWidth = 0f;
        }

        private static void SetText(TMP_Text text, string value)
        {
            foreach (I2.Loc.Localize localize in text.GetComponents<I2.Loc.Localize>()) Object.DestroyImmediate(localize);
            text.text = value;
        }

        private void ConfigureNavigation()
        {
            var rows = new List<Selectable[]>();
            foreach (Transform child in _content)
            {
                if (!child.gameObject.activeSelf) continue;
                Selectable[] controls = Array.FindAll(child.GetComponentsInChildren<Selectable>(), control => control.gameObject.activeInHierarchy && control.IsInteractable());
                if (controls.Length > 0) rows.Add(controls);
            }
            rows.Add(new Selectable[] { _back });
            for (int i = 0; i < rows.Count; i++)
            {
                for (int j = 0; j < rows[i].Length; j++)
                {
                    Selectable[] above = rows[(i + rows.Count - 1) % rows.Count];
                    Selectable[] below = rows[(i + 1) % rows.Count];
                    rows[i][j].navigation = new Navigation
                    {
                        mode = Navigation.Mode.Explicit,
                        selectOnUp = above[Mathf.Min(j, above.Length - 1)],
                        selectOnDown = below[Mathf.Min(j, below.Length - 1)],
                        selectOnLeft = rows[i][Mathf.Max(0, j - 1)],
                        selectOnRight = rows[i][Mathf.Min(rows[i].Length - 1, j + 1)]
                    };
                }
            }
        }

        private void Reveal(RectTransform selected)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(_content);
            var corners = new Vector3[4];
            selected.GetWorldCorners(corners);
            float bottom = _viewport.InverseTransformPoint(corners[0]).y;
            float top = _viewport.InverseTransformPoint(corners[1]).y;
            float delta = top > _viewport.rect.yMax ? _viewport.rect.yMax - top :
                bottom < _viewport.rect.yMin ? _viewport.rect.yMin - bottom : 0f;
            if (delta != 0f)
            {
                _scroll.StopMovement();
                _content.anchoredPosition += new Vector2(0f, delta);
            }
        }

        private void OnDestroy()
        {
            ModConfig.Changed -= Refresh;
            TeamManager.RosterChanged -= Refresh;
            if (_content == null) return;
            foreach (Transform child in _originals)
            {
                if (child == null) continue;
                child.SetParent(transform, false);
                child.SetAsLastSibling();
            }
            foreach (var entry in _navigation) if (entry.Key != null) entry.Key.navigation = entry.Value;
            if (_title == null || _back == null || _layout == null) return;
            RectTransform title = (RectTransform)_title;
            title.anchorMin = _titleAnchorMin;
            title.anchorMax = _titleAnchorMax;
            title.anchoredPosition = _titlePosition;
            RectTransform back = (RectTransform)_back.transform;
            back.anchorMin = _backAnchorMin;
            back.anchorMax = _backAnchorMax;
            back.anchoredPosition = _backPosition;
            _back.navigation = _backNavigation;
            _layout.enabled = true;
            Object.Destroy(_scroll.gameObject);
        }
    }

    [HarmonyPatch(typeof(VersusSettingsUi), nameof(VersusSettingsUi.SetupVersusSettings))]
    internal static class VersusSettingsUi_Teams_Patch
    {
        [HarmonyPostfix]
        private static void Postfix(VersusSettingsUi __instance) => VersusTeamsPage.Install(__instance);
    }
}
