using System;
using System.Collections.Generic;
using Doozy.Engine.UI;
using HarmonyLib;
using SpiderSurge.Common;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Logger = Silk.Logger;
using Object = UnityEngine.Object;

namespace SpiderTeams
{
    internal sealed class VersusTeamsPage : MonoBehaviour
    {
        private float RowWidth => _viewport != null ? Mathf.Clamp(_viewport.rect.width - 8f, 480f, 720f) : 720f;
        private const float RowHeight = 30f;
        private const float ArrowWidth = 40f;
        private const float ValueWidth = 224f;
        private const float ColumnSpacing = 8f;
        private float LabelWidth => RowWidth - ValueWidth - 2f * ArrowWidth - 3f * ColumnSpacing;
        private const float FontSize = 19f;
        private const float MinFontSize = 12f;

        private enum Kind { Spacer, Text, Choice, Button }

        private sealed class Entry
        {
            public Kind Kind;
            public string Label;
            public string Value;
            public Action Previous;
            public Action Next;
        }

        private readonly List<Transform> _originals = new List<Transform>();
        private readonly List<GameObject> _created = new List<GameObject>();
        private readonly Dictionary<Selectable, Navigation> _navigation = new Dictionary<Selectable, Navigation>();
        private readonly List<Action> _undo = new List<Action>();
        private List<Entry> _entries = new List<Entry>();
        private List<Entry> _building;
        private int _primaryCount;
        private int _builtPrimaryCount;
        private UIView _view;
        private VerticalLayoutGroup _layout;
        private RectTransform _content;
        private ScrollRect _scroll;
        private RectTransform _viewport;
        private Transform _rowTemplate;
        private Button _back;
        private RectTransform _title;
        private Vector2 _titlePosition, _backPosition;
        private Vector2 _titleAnchorMin, _titleAnchorMax, _backAnchorMin, _backAnchorMax;
        private bool _captured;
        private GameObject _selected;
        private bool _dirty;
        private float _builtViewportWidth;
        internal bool AdvancedAbilities { get; private set; }
        internal void ToggleAdvancedAbilities() { AdvancedAbilities = !AdvancedAbilities; Refresh(); }
        private bool _showing;
        private string _confirming;

        public static void Install(VersusSettingsUi ui)
        {
            foreach (string name in new[] { "View - StartVersus", "View - StartCustomVersus" })
            {
                Transform view = ui.transform.Find(name);
                if (view == null || view.GetComponent<VersusTeamsPage>() != null)
                {
                    continue;
                }

                VersusTeamsPage page = view.gameObject.AddComponent<VersusTeamsPage>();
                try
                {
                    page.Build();
                }
                catch (Exception ex)
                {
                    Logger.LogError($"SpiderTeams could not add its settings to {name}: {ex}");
                    page.Restore();
                    Object.DestroyImmediate(page);
                }
            }
        }

        // Unity skips OnDestroy for a component whose object was never active, so restore explicitly before destroying.
        public static void RemoveAll()
        {
            foreach (VersusTeamsPage page in Resources.FindObjectsOfTypeAll<VersusTeamsPage>())
            {
                if (page.gameObject.scene.IsValid())
                {
                    page.Restore();
                    Object.DestroyImmediate(page);
                }
            }
        }

        public static void RefreshNavigation(VersusSettingsUi ui)
        {
            foreach (VersusTeamsPage page in ui.GetComponentsInChildren<VersusTeamsPage>())
            {
                if (page._content != null) page.ConfigureNavigation();
            }
        }

        // Keep the native title and Back button fixed while the original options and Teams share a scroll area.
        private void Build()
        {
            _view = GetComponent<UIView>();
            _layout = GetComponent<VerticalLayoutGroup>();
            _rowTemplate = transform.Find("WinningScore");
            Transform back = transform.Find("Button - Cancel");
            _back = back != null ? back.GetComponent<Button>() : null;
            _title = (RectTransform)(transform.Find("Title") ?? transform.Find("Text (TMP)"));
            if (_view == null || _layout == null || _rowTemplate == null || _back == null || _title == null)
            {
                throw new InvalidOperationException("the view's UIView, layout, WinningScore row, title or Back button is missing");
            }

            foreach (Transform child in transform) _originals.Add(child);
            foreach (Selectable control in GetComponentsInChildren<Selectable>(true)) _navigation[control] = control.navigation;
            RectTransform backRect = (RectTransform)_back.transform;
            _titlePosition = _title.anchoredPosition;
            _backPosition = backRect.anchoredPosition;
            _titleAnchorMin = _title.anchorMin;
            _titleAnchorMax = _title.anchorMax;
            _backAnchorMin = backRect.anchorMin;
            _backAnchorMax = backRect.anchorMax;
            _captured = true;

            _layout.enabled = false;
            _title.anchorMin = _title.anchorMax = new Vector2(0.5f, 1f);
            _title.anchoredPosition = new Vector2(0f, -32f);
            backRect.anchorMin = backRect.anchorMax = new Vector2(0.5f, 0f);
            backRect.anchoredPosition = new Vector2(0f, 30f);

            _scroll = new GameObject("Versus settings scroll", typeof(RectTransform), typeof(ScrollRect)).GetComponent<ScrollRect>();
            _scroll.transform.SetParent(transform, false);
            Stretch((RectTransform)_scroll.transform, new Vector2(65f, 65f), new Vector2(-65f, -65f));
            _viewport = new GameObject("Viewport", typeof(RectTransform), typeof(RectMask2D), typeof(Image)).GetComponent<RectTransform>();
            _viewport.SetParent(_scroll.transform, false);
            Stretch(_viewport, Vector2.zero, Vector2.zero);
            _viewport.GetComponent<Image>().color = Color.clear;
            var content = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter)).GetComponent<RectTransform>();
            content.SetParent(_viewport, false);
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = Vector2.one;
            content.pivot = new Vector2(0.5f, 1f);
            content.sizeDelta = Vector2.zero;
            var contentLayout = content.GetComponent<VerticalLayoutGroup>();
            contentLayout.childAlignment = TextAnchor.UpperCenter;
            contentLayout.childControlWidth = false;
            contentLayout.childControlHeight = false;
            contentLayout.childForceExpandWidth = false;
            contentLayout.childForceExpandHeight = false;
            contentLayout.spacing = 10f;
            contentLayout.padding = new RectOffset(0, 0, 8, 16);
            content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            _scroll.viewport = _viewport;
            _scroll.content = content;
            _scroll.horizontal = false;
            _scroll.movementType = ScrollRect.MovementType.Clamped;
            _scroll.scrollSensitivity = 35f;
            AddScrollbar();
            foreach (Transform child in _originals)
            {
                if (child == _title || child == _back.transform) continue;
                if (child.name == "VersusTypes")
                {
                    Vector3 scale = child.localScale;
                    _undo.Add(() => { if (child != null) child.localScale = scale; });
                }
                child.SetParent(content, false);
                if (IsChoiceRow(child))
                {
                    Remember(child);
                    StyleRow(child);
                }
            }
            _content = content;
            if (gameObject.activeInHierarchy) Show();
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
            ModConfig.Changed += Refresh;
            TeamManager.RosterChanged += Refresh;
            if (_content != null) Show();
        }

        private void OnDisable()
        {
            ModConfig.Changed -= Refresh;
            TeamManager.RosterChanged -= Refresh;
        }

        private void Show()
        {
            _confirming = null;
            Rebuild(true);
            SetOffset(0f);
            _showing = true;
        }

        // Rects are only reliable once the show animation ends, so the page starts at the top and keeps the view's own selection.
        private void LateUpdate()
        {
            if (_content == null) return;
            if (_view.IsVisible && !Mathf.Approximately(_builtViewportWidth, _viewport.rect.width)) Rebuild(true);
            if (_dirty) Rebuild(false);
            GameObject selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            if (_showing)
            {
                if (!_view.IsVisible) return;
                _showing = false;
                LayoutRebuilder.ForceRebuildLayoutImmediate(_content);
                SetOffset(0f);
                _selected = selected;
                return;
            }
            if (_selected == selected) return;
            _selected = selected;
            if (selected != null && selected.transform.IsChildOf(_content)) Reveal((RectTransform)selected.transform);
        }

        // Rows are only recreated when their text changes; otherwise the new actions replace the old ones in place.
        private void Rebuild(bool force)
        {
            _dirty = false;
            _builtViewportWidth = _viewport.rect.width;
            foreach (Transform original in _originals)
            {
                if (IsChoiceRow(original)) StyleRow(original);
                else if (original.name == "VersusTypes")
                {
                    float factor = Mathf.Min(1f, Mathf.Max(0f, _viewport.rect.width - 8f) / ((RectTransform)original).rect.width);
                    original.localScale = new Vector3(factor, factor, 1f);
                }
            }
            var entries = new List<Entry>();
            _building = entries;
            _primaryCount = 0;
            try
            {
                TeamsSettingsPage.Build(this);
            }
            catch (Exception ex)
            {
                Logger.LogError($"SpiderTeams versus settings failed: {ex}");
            }
            _building = null;
            bool same = !force && SameRows(entries);
            _entries = entries;
            if (!same) Recreate();
            ConfigureNavigation();
        }

        private bool SameRows(List<Entry> entries)
        {
            if (entries.Count != _entries.Count || _primaryCount != _builtPrimaryCount || _created.Count != _entries.Count) return false;
            for (int i = 0; i < entries.Count; i++)
            {
                Entry a = entries[i], b = _entries[i];
                if (a.Kind != b.Kind || a.Label != b.Label || a.Value != b.Value) return false;
            }
            return true;
        }

        private void Recreate()
        {
            GameObject selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            if (selected != null && !selected.transform.IsChildOf(_content)) selected = null;
            float focusTop = selected != null ? TopInViewport(selected.transform) : 0f;
            int selection = selected != null ? _created.FindIndex(item => selected.transform.IsChildOf(item.transform)) : -1;
            int column = selection >= 0 ? Array.FindIndex(_created[selection].GetComponentsInChildren<Selectable>(), control => control.gameObject == selected) : 0;
            string selectedRow = selection >= 0 ? _created[selection].name : null;
            float offset = _content.anchoredPosition.y;

            foreach (GameObject item in _created)
            {
                item.SetActive(false);
                Object.Destroy(item);
            }
            _created.Clear();
            Transform anchor = _content.Find(name == "View - StartVersus" ? "VersusTypes" : "DeathZone");
            for (int i = 0; i < _entries.Count; i++)
            {
                GameObject item = Create(_entries[i], i);
                if (i < _primaryCount && anchor != null) item.transform.SetSiblingIndex(anchor.GetSiblingIndex());
                _created.Add(item);
            }
            _builtPrimaryCount = _primaryCount;

            LayoutRebuilder.ForceRebuildLayoutImmediate(_content);
            SetOffset(offset);
            if (selection >= 0) Select(Follow(selectedRow, selection), column);
            GameObject now = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            if (selected != null && now != null && now.transform.IsChildOf(_content))
            {
                SetOffset(_content.anchoredPosition.y + focusTop - TopInViewport(now.transform));
            }
        }

        private int Follow(string rowName, int fallback)
        {
            int first = _created.FindIndex(item => item.name == rowName);
            return first >= 0 && first == _created.FindLastIndex(item => item.name == rowName) ? first : fallback;
        }

        private void Select(int index, int column)
        {
            int start = Mathf.Min(index, _created.Count - 1);
            for (int distance = 0; distance < _created.Count; distance++)
            {
                if (TrySelect(start - distance, column) || TrySelect(start + distance, column)) return;
            }
            EventSystem.current.SetSelectedGameObject(_back.gameObject);
        }

        private bool TrySelect(int index, int column)
        {
            if (index < 0 || index >= _created.Count) return false;
            Selectable[] controls = Array.FindAll(_created[index].GetComponentsInChildren<Selectable>(), Usable);
            if (controls.Length == 0) return false;
            EventSystem.current.SetSelectedGameObject(controls[Mathf.Clamp(column, 0, controls.Length - 1)].gameObject);
            return true;
        }

        private static bool Usable(Selectable control) => control.enabled && control.interactable && control.gameObject.activeInHierarchy;

        private float TopInViewport(Transform target)
        {
            var corners = new Vector3[4];
            ((RectTransform)target).GetWorldCorners(corners);
            return _viewport.InverseTransformPoint(corners[1]).y;
        }

        private void SetOffset(float y)
        {
            float max = Mathf.Max(0f, _content.rect.height - _viewport.rect.height);
            _scroll.StopMovement();
            _content.anchoredPosition = new Vector2(_content.anchoredPosition.x, Mathf.Clamp(y, 0f, max));
        }

        public void Header(string text) => Paragraph("<b>" + text.ToUpperInvariant() + "</b>");
        public void Note(string text) => Paragraph($"<color={SettingsPage.Gray}>{text}</color>");

        public void PlacePrimaryControls() => _primaryCount = _building.Count;

        public void Spacer() => _building.Add(new Entry { Kind = Kind.Spacer });

        public void Paragraph(string value) => _building.Add(new Entry { Kind = Kind.Text, Label = value });

        public void Checkbox(string label, Func<bool> value, Action<bool> set)
        {
            Action toggle = () => set(!value());
            Choice(label, value() ? "On" : "Off", toggle, toggle);
        }

        public void Choice(string label, string value, Action previous, Action next)
        {
            _building.Add(new Entry { Kind = Kind.Choice, Label = label, Value = value, Previous = previous, Next = next });
        }

        public void Button(string label, Action action) => _building.Add(new Entry { Kind = Kind.Button, Label = label, Next = action });

        // Pressing it swaps the button for a question with Keep and Reset, like the Mods menu's confirmation page.
        public void ConfirmButton(string label, Action onConfirm)
        {
            if (_confirming != label)
            {
                Button(label, () => _confirming = label);
                return;
            }
            Paragraph(label + "?");
            Note("This cannot be undone.");
            Button("Keep current settings", () => _confirming = null);
            Button("Reset", () =>
            {
                _confirming = null;
                onConfirm();
            });
        }

        private GameObject Create(Entry entry, int index)
        {
            switch (entry.Kind)
            {
                case Kind.Spacer:
                    return CreateSpacer();
                case Kind.Text:
                    return CreateParagraph(entry.Label);
                case Kind.Choice:
                    return CreateChoice(entry, index);
                default:
                    return CreateButton(entry, index);
            }
        }

        private GameObject CreateSpacer()
        {
            var spacer = new GameObject("Teams spacing", typeof(RectTransform));
            spacer.transform.SetParent(_content, false);
            ((RectTransform)spacer.transform).sizeDelta = new Vector2(RowWidth, 8f);
            return spacer;
        }

        private GameObject CreateParagraph(string value)
        {
            TMP_Text text = Object.Instantiate(_rowTemplate.Find("Label").GetComponent<TMP_Text>(), _content);
            SettingsPage.SetText(text, value);
            text.name = "Teams text";
            text.fontSize = 18f;
            text.enableAutoSizing = false;
            text.enableWordWrapping = true;
            text.alignment = TextAlignmentOptions.TopLeft;
            text.raycastTarget = false;
            text.transform.localScale = Vector3.one;
            text.rectTransform.sizeDelta = new Vector2(RowWidth, text.GetPreferredValues(value, RowWidth, float.PositiveInfinity).y + 4f);
            return text.gameObject;
        }

        private GameObject CreateChoice(Entry entry, int index)
        {
            Transform row = Object.Instantiate(_rowTemplate, _content);
            row.name = "Teams - " + entry.Label;
            row.gameObject.SetActive(true);
            row.localScale = Vector3.one;
            StyleRow(row);
            SettingsPage.SetText(row.Find("Label").GetComponent<TMP_Text>(), entry.Label);
            SettingsPage.SetText(row.Find("Setting").GetComponent<TMP_Text>(), entry.Value);
            PrepareButton(row.Find("Button - Back").gameObject, index, true);
            PrepareButton(row.Find("Button - Forward").gameObject, index, false);
            return row.gameObject;
        }

        private GameObject CreateButton(Entry entry, int index)
        {
            GameObject target = Object.Instantiate(_back.gameObject, _content);
            target.name = "Teams - " + entry.Label;
            target.transform.localScale = Vector3.one;
            ((RectTransform)target.transform).sizeDelta = new Vector2(RowWidth, RowHeight);
            TMP_Text text = target.GetComponentInChildren<TMP_Text>();
            SettingsPage.SetText(text, entry.Label);
            text.fontSize = 20f;
            text.enableAutoSizing = false;
            PrepareButton(target, index, false);
            return target;
        }

        private void PrepareButton(GameObject target, int index, bool previous)
        {
            UIButton doozy = target.GetComponent<UIButton>();
            if (doozy != null) Object.DestroyImmediate(doozy);
            Button button = target.GetComponent<Button>();
            button.onClick = new Button.ButtonClickedEvent();
            button.onClick.AddListener(() => Run(index, previous));
            button.interactable = true;
            foreach (TMP_Text text in target.GetComponentsInChildren<TMP_Text>()) SettingsPage.SetText(text, text.text);
        }

        private void Run(int index, bool previous)
        {
            if (index >= _entries.Count) return;
            Action action = previous ? _entries[index].Previous : _entries[index].Next;
            try
            {
                action?.Invoke();
            }
            catch (Exception ex)
            {
                Logger.LogError($"SpiderTeams versus setting failed: {ex}");
            }
            Refresh();
        }

        private static bool IsChoiceRow(Transform row)
        {
            return row.GetComponent<HorizontalLayoutGroup>() != null && row.Find("Label") != null && row.Find("Setting") != null;
        }

        // The game's rows and the Teams rows share one set of columns, so their edges line up.
        private void StyleRow(Transform row)
        {
            ((RectTransform)row).sizeDelta = new Vector2(RowWidth, RowHeight);
            var layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(0, 0, 0, 0);
            layout.spacing = ColumnSpacing;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            foreach (Transform child in row)
            {
                TMP_Text text = child.GetComponent<TMP_Text>();
                if (child.name == "Label" && text != null) StyleText(text, LabelWidth, TextAlignmentOptions.MidlineLeft);
                else if (child.name == "Setting" && text != null) StyleText(text, ValueWidth, TextAlignmentOptions.Midline);
                else if (child.GetComponent<Selectable>() != null) SetSize(child.gameObject, ArrowWidth);
            }
        }

        private static void StyleText(TMP_Text text, float width, TextAlignmentOptions alignment)
        {
            text.fontSize = FontSize;
            text.fontSizeMax = FontSize;
            text.fontSizeMin = MinFontSize;
            text.enableAutoSizing = true;
            text.enableWordWrapping = false;
            text.alignment = alignment;
            SetSize(text.gameObject, width);
        }

        private static void SetSize(GameObject target, float width)
        {
            var size = target.GetComponent<LayoutElement>() ?? target.AddComponent<LayoutElement>();
            size.minWidth = size.preferredWidth = width;
            size.minHeight = size.preferredHeight = RowHeight;
            size.flexibleWidth = size.flexibleHeight = 0f;
        }

        // Records a game row's layout before StyleRow changes it, so Restore can put it back.
        private void Remember(Transform row)
        {
            var rect = (RectTransform)row;
            var layout = row.GetComponent<HorizontalLayoutGroup>();
            Vector2 rowSize = rect.sizeDelta;
            RectOffset padding = layout.padding;
            float spacing = layout.spacing;
            TextAnchor alignment = layout.childAlignment;
            bool controlWidth = layout.childControlWidth, controlHeight = layout.childControlHeight;
            bool expandWidth = layout.childForceExpandWidth, expandHeight = layout.childForceExpandHeight;
            _undo.Add(() =>
            {
                if (layout == null) return;
                layout.padding = padding;
                layout.spacing = spacing;
                layout.childAlignment = alignment;
                layout.childControlWidth = controlWidth;
                layout.childControlHeight = controlHeight;
                layout.childForceExpandWidth = expandWidth;
                layout.childForceExpandHeight = expandHeight;
                rect.sizeDelta = rowSize;
            });

            foreach (Transform child in row)
            {
                var childRect = (RectTransform)child;
                Vector2 childSize = childRect.sizeDelta;
                LayoutElement element = child.GetComponent<LayoutElement>();
                float[] sizes = element != null ? new[] { element.minWidth, element.preferredWidth, element.minHeight, element.preferredHeight, element.flexibleWidth, element.flexibleHeight } : null;
                _undo.Add(() =>
                {
                    if (childRect == null) return;
                    childRect.sizeDelta = childSize;
                    LayoutElement now = childRect.GetComponent<LayoutElement>();
                    if (now == null) return;
                    if (sizes == null)
                    {
                        Object.Destroy(now);
                        return;
                    }
                    now.minWidth = sizes[0];
                    now.preferredWidth = sizes[1];
                    now.minHeight = sizes[2];
                    now.preferredHeight = sizes[3];
                    now.flexibleWidth = sizes[4];
                    now.flexibleHeight = sizes[5];
                });

                TMP_Text text = child.GetComponent<TMP_Text>();
                if (text == null) continue;
                float fontSize = text.fontSize, fontMin = text.fontSizeMin, fontMax = text.fontSizeMax;
                bool autoSize = text.enableAutoSizing, wrap = text.enableWordWrapping;
                TextAlignmentOptions textAlignment = text.alignment;
                _undo.Add(() =>
                {
                    if (text == null) return;
                    text.enableAutoSizing = autoSize;
                    text.fontSizeMin = fontMin;
                    text.fontSizeMax = fontMax;
                    text.fontSize = fontSize;
                    text.enableWordWrapping = wrap;
                    text.alignment = textAlignment;
                });
            }
        }

        // Explicit links keep Up and Down on the visible rows; rerun whenever a native control turns on or off.
        private void ConfigureNavigation()
        {
            var rows = new List<Selectable[]>();
            foreach (Transform child in _content)
            {
                if (!child.gameObject.activeSelf) continue;
                Selectable[] controls = Array.FindAll(child.GetComponentsInChildren<Selectable>(), Usable);
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

        private void OnDestroy() => Restore();

        private void Restore()
        {
            ModConfig.Changed -= Refresh;
            TeamManager.RosterChanged -= Refresh;
            if (!_captured) return;
            _captured = false;
            foreach (Transform child in _originals)
            {
                if (child == null) continue;
                child.SetParent(transform, false);
                child.SetAsLastSibling();
            }
            foreach (var entry in _navigation) if (entry.Key != null) entry.Key.navigation = entry.Value;
            foreach (Action undo in _undo) undo();
            _undo.Clear();
            if (_title != null)
            {
                _title.anchorMin = _titleAnchorMin;
                _title.anchorMax = _titleAnchorMax;
                _title.anchoredPosition = _titlePosition;
            }
            if (_back != null)
            {
                RectTransform back = (RectTransform)_back.transform;
                back.anchorMin = _backAnchorMin;
                back.anchorMax = _backAnchorMax;
                back.anchoredPosition = _backPosition;
            }
            if (_layout != null) _layout.enabled = true;
            if (_scroll != null) Object.Destroy(_scroll.gameObject);
            _content = null;
        }
    }

    [HarmonyPatch(typeof(VersusSettingsUi), nameof(VersusSettingsUi.SetupVersusSettings))]
    internal static class VersusSettingsUi_Teams_Patch
    {
        [HarmonyPostfix]
        private static void Postfix(VersusSettingsUi __instance)
        {
            try
            {
                VersusTeamsPage.Install(__instance);
            }
            catch (Exception ex)
            {
                Logger.LogError($"SpiderTeams versus settings patch failed: {ex}");
            }
        }
    }

    [HarmonyPatch(typeof(VersusSettingsUi), nameof(VersusSettingsUi.CheckToggleType))]
    internal static class VersusSettingsUi_CheckToggleType_Patch
    {
        [HarmonyPostfix]
        private static void Postfix(VersusSettingsUi __instance)
        {
            try
            {
                VersusTeamsPage.RefreshNavigation(__instance);
            }
            catch (Exception ex)
            {
                Logger.LogError($"SpiderTeams versus navigation patch failed: {ex}");
            }
        }
    }
}
