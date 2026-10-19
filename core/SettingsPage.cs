using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using DG.Tweening;
using Doozy.Engine.UI;
using Doozy.Engine.UI.Input;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Logger = Silk.Logger;

namespace SpiderSurge.Common
{
    public class SettingsPage : MonoBehaviour
    {
        public const string MainPage = "main";
        public const string Green = "#00FF00";
        public const string Red = "#FF5555";
        public const string Gray = "#AAAAAA";
        public const string Orange = "#FFAA66";
        private const float FontSize = 19f;
        private const float RowHeight = 38f;
        private static readonly string[] DoozyAnimations = { "Punch-Move", "Punch-Rotate", "Punch-Scale", "Punch-Fade", "State-Move", "State-Rotate", "State-Scale", "State-Fade" };

        private static readonly Part[] MenuOrder = { Parts.Stats, Parts.Surge, Parts.Teams, Parts.Fixes };
        private static readonly AccessTools.FieldRef<ModsMenu, UIButton> BackButtonOf = AccessTools.FieldRefAccess<ModsMenu, UIButton>("backButton");
        private static SettingsPage _open;
        private static int _backFrame = -1;
        private static ModsMenu _entryMenu;
        private static Button _entryButton;

        private ModsMenu _menu;
        private UIView _view;
        private ScrollRect _scroll;
        private RectTransform _originalContent;
        private RectTransform _content;
        private Button _entry;
        private Selectable _back;
        private Navigation _backNavigation;
        private GameObject _originalSelection;
        private float _originalScroll;
        private TMP_Text _title;
        private string _originalTitle;
        private I2.Loc.Localize _titleLocalization;
        private bool _localizationEnabled;
        private GameObject _lastSelected;
        private Action _confirmation;
        private string _confirmationLabel;
        private readonly List<Selectable> _controls = new List<Selectable>();
        private readonly List<GameObject> _created = new List<GameObject>();
        private readonly Stack<(string page, int selection, float scroll)> _history = new Stack<(string, int, float)>();
        private bool _subscribed;
        private bool _quiet;
        private Coroutine _pending;
        private bool _pagePending;

        public string Current { get; private set; } = MainPage;

        public static void RefreshOpen()
        {
            if (_open != null)
            {
                _open.RequestRebuild(null);
            }
        }

        public static bool IsShowing(string page) => _open != null && _open._menu != null && _open._view.IsVisible && _open.Current == page;

        // Silk adds the SpiderSurge button to the Mods menu only once its update check is done
        public static bool CanShow => (_open != null && _open._menu != null) || (_entryMenu != null && _entryButton != null);

        internal static void SetEntry(ModsMenu menu, Button entry)
        {
            _entryMenu = menu;
            _entryButton = entry;
        }

        // Opens the SpiderSurge menu in the Mods menu at a page, with the pages before it to go back through
        public static bool Show(params string[] pages)
        {
            if (_open != null && _open._menu != null)
            {
                if (_open.Current != pages[pages.Length - 1]) _open.Open(pages[pages.Length - 1]);
                return true;
            }
            if (!CanShow) return false;
            Show(_entryMenu, _entryButton, pages);
            return true;
        }

        internal static void Show(ModsMenu menu, Button entry, params string[] pages)
        {
            var page = menu.GetComponent<SettingsPage>() ?? menu.gameObject.AddComponent<SettingsPage>();
            page.StartCoroutine(page.BuildNextFrame(menu, entry, pages));
        }

        private IEnumerator BuildNextFrame(ModsMenu menu, Button entry, string[] pages)
        {
            yield return null;
            BuildMenu(menu, entry, pages);
        }

        private void BuildMenu(ModsMenu menu, Button entry, string[] pages)
        {
            if (_menu != null) return;
            var view = menu.GetComponent<UIView>();
            var original = menu.objectParent as RectTransform;
            var scroll = original != null ? original.GetComponentInParent<ScrollRect>() : null;
            UIButton backButton = BackButtonOf(menu);
            var back = backButton != null ? backButton.GetComponent<Selectable>() : null;
            if (view == null || scroll == null || back == null)
            {
                Logger.LogError("SpiderSurge settings page: the Mods menu has no view, scroll list or Back button.");
                return;
            }

            try
            {
                _menu = menu;
                _entry = entry;
                _view = view;
                _originalContent = original;
                _scroll = scroll;
                _back = back;
                _originalScroll = scroll.verticalNormalizedPosition;
                _originalSelection = view.SelectedButton;
                _backNavigation = back.navigation;
                Transform title = menu.transform.Find("Text (TMP)");
                _title = title != null ? title.GetComponent<TMP_Text>() : null;
                if (_title != null)
                {
                    _originalTitle = _title.text;
                    _titleLocalization = _title.GetComponent<I2.Loc.Localize>();
                    if (_titleLocalization != null)
                    {
                        _localizationEnabled = _titleLocalization.enabled;
                        _titleLocalization.enabled = false;
                    }
                }
                view.HideBehavior.OnStart.Event.AddListener(Clear);
                _content = new GameObject("SpiderSurge settings", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter)).GetComponent<RectTransform>();
                _content.SetParent(_originalContent.parent, false);
                _content.anchorMin = new Vector2(0f, 1f);
                _content.anchorMax = Vector2.one;
                _content.pivot = new Vector2(0.5f, 1f);
                _content.sizeDelta = Vector2.zero;
                var layout = _content.GetComponent<VerticalLayoutGroup>();
                layout.childAlignment = TextAnchor.UpperCenter;
                layout.childControlWidth = true;
                layout.childControlHeight = true;
                layout.childForceExpandWidth = true;
                layout.childForceExpandHeight = false;
                layout.spacing = 8f;
                layout.padding = new RectOffset(18, 32, 8, 16);
                _content.GetComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
                _originalContent.gameObject.SetActive(false);
                _scroll.content = _content;
                _scroll.StopMovement();
                menu.objectParent = _content;
                _open = this;
                _history.Clear();
                Current = MainPage;
                foreach (string page in pages)
                {
                    _history.Push((Current, 0, 1f));
                    Current = page;
                }
                if (!_subscribed)
                {
                    Settings.Changed += RequestRebuild;
                    _subscribed = true;
                }
                Rebuild(0, 1f);
            }
            catch (Exception ex)
            {
                Logger.LogError($"SpiderSurge settings page failed to open: {ex}");
                Clear();
            }
        }

        // Also runs while the menu is being turned off, where Unity refuses DestroyImmediate, so the page goes at the end of the frame
        public void Clear()
        {
            StopAllCoroutines();
            _pending = null;
            _pagePending = false;
            foreach (GameObject created in _created)
            {
                if (created != null) StopClickAnimations(created);
            }
            _created.Clear();
            _controls.Clear();
            _lastSelected = null;
            if (_menu != null)
            {
                _view.HideBehavior.OnStart.Event.RemoveListener(Clear);
                _menu.objectParent = _originalContent;
                _scroll.content = _originalContent;
                _scroll.StopMovement();
                _originalContent.gameObject.SetActive(true);
                _scroll.verticalNormalizedPosition = _originalScroll;
                _back.navigation = _backNavigation;
                _view.SelectedButton = _originalSelection;
                if (_title != null) _title.text = _originalTitle;
                if (_titleLocalization != null) _titleLocalization.enabled = _localizationEnabled;
            }
            if (_content != null) Destroy(_content.gameObject);
            _content = null;
            _title = null;
            _titleLocalization = null;
            _history.Clear();
            Settings.Flush();
            _menu = null;
            if (_open == this)
            {
                _open = null;
            }
        }

        public void Open(string page)
        {
            if (_menu == null || _pagePending) return;
            _history.Push((Current, SelectedIndex(), _scroll.verticalNormalizedPosition));
            Show(page, 0, 1f);
        }

        public void Back()
        {
            if (_menu == null || _backFrame == Time.frameCount) return;
            _backFrame = Time.frameCount;
            if (_history.Count == 0)
            {
                Clear();
                if (_entry != null) _entry.Select();
                return;
            }
            var previous = _history.Pop();
            Show(previous.page, previous.selection, previous.scroll);
        }

        // Every back input ends here; one key can reach several of them in the same frame, so only the first counts
        public static bool HandleBack()
        {
            if (_backFrame == Time.frameCount) return true;
            if (_open == null || _open._menu == null || !_open._view.IsVisible || UIPopupManager.CurrentVisibleQueuePopup != null)
                return false;
            _open.Back();
            return true;
        }

        // The clicked button is still handling its click, so the page changes on the next frame
        private void Show(string page, int selection, float scroll)
        {
            Current = page;
            if (_pending != null)
            {
                StopCoroutine(_pending);
            }
            _pagePending = true;
            _pending = StartCoroutine(ShowNextFrame(selection, scroll));
        }

        private IEnumerator ShowNextFrame(int selection, float scroll)
        {
            yield return null;
            _pending = null;
            _pagePending = false;
            Rebuild(selection, scroll);
        }

        private void OnDisable()
        {
            if (_menu != null) Clear();
        }

        private void OnDestroy()
        {
            if (_subscribed)
            {
                Settings.Changed -= RequestRebuild;
                _subscribed = false;
            }
            if (_open == this)
            {
                _open = null;
            }
        }

        private void RequestRebuild(string key)
        {
            if (_quiet || this == null || !isActiveAndEnabled || _menu == null || _pending != null)
            {
                return;
            }
            _pending = StartCoroutine(RebuildNextFrame());
        }

        private IEnumerator RebuildNextFrame()
        {
            yield return null;
            _pending = null;
            Rebuild(SelectedIndex());
        }

        private void Rebuild(int selectIndex, float? scroll = null)
        {
            try
            {
                DestroyCreated();
                if (_menu == null || _menu.objectParent == null)
                {
                    return;
                }

                AddContent();
                ConfigureNavigation();
                LayoutRebuilder.ForceRebuildLayoutImmediate(_content);
                Select(selectIndex);
                if (scroll.HasValue)
                {
                    _scroll.StopMovement();
                    _scroll.verticalNormalizedPosition = scroll.Value;
                }
                KeepSelectionVisible();
                _lastSelected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            }
            catch (Exception ex)
            {
                Logger.LogError($"SpiderSurge settings page failed: {ex}");
            }
        }

        private void AddContent()
        {
            SetTitle("SPIDERSURGE");
            if (Current == MainPage)
            {
                AddMain();
                return;
            }
            if (Current == "general") { AddGeneral(); return; }
            if (Current == "parts") { AddParts(); return; }
            if (Current == "about") { AddAbout(); return; }
            if (Current == "confirm")
            {
                Title("Confirm reset");
                Paragraph(_confirmationLabel + "?");
                Note("This cannot be undone.");
                Button("Keep current settings", Back);
                Button("Reset", () =>
                {
                    Action action = _confirmation;
                    Back();
                    Run(action);
                });
                return;
            }

            Part part = Array.Find(Parts.All, candidate => Current == candidate.Key || Current.StartsWith(candidate.Key + ".", StringComparison.Ordinal));
            if (part == null || !part.Running)
            {
                Warning("This part isn't running.");
                return;
            }
            part.BuildSettings(this);
        }

        private void AddMain()
        {
            Note("Choose a settings category. Changes are saved automatically.");
            foreach (Part part in MenuOrder)
            {
                if (part.Running && part.HasSettings) Button(part.Title, () => Open(part.Key));
            }
            Button("Enable or disable parts", () => Open("parts"));
            Button("General", () => Open("general"));
            Button("About SpiderSurge", () => Open("about"));
        }

        private void AddParts()
        {
            Title("Mod parts");
            Note("Turning a part on or off takes effect after restarting the game.");
            foreach (Part part in MenuOrder)
            {
                Spacer();
                if (part.CanDisable)
                {
                    Checkbox($"<b>{part.Title}</b>", part.Enabled, value => Settings.Set(part.SettingKey, value));
                }
                else
                {
                    Header(part.Title);
                }
                Note(part.Description);
                if (part.BlockedBy != null)
                {
                    Warning($"Off because the old {part.BlockedBy} is still in the Mods folder. Delete it and restart the game.");
                }
                else if (part.Failed)
                {
                    Warning("Failed to start. The Silk log has the error.");
                }
                else if (part.Enabled != part.Running)
                {
                    Warning(part.Enabled ? "Restart the game to turn it on." : "Restart the game to turn it off.");
                }
            }
        }

        private void AddGeneral()
        {
            Title("General");
            Toggle("Check for updates", ModUpdater.CheckForUpdatesKey);
            Button("Reload the settings file", ReloadFile);
            Note($"Settings file: <noparse>{Settings.FilePath}</noparse>");

        }

        private void AddAbout()
        {
            Title("About SpiderSurge");
            Paragraph($"Version {SpiderSurgeMod.Version}");
            Header("Credits");
            Paragraph("Icons by these artists from game-icons.net: delapouite, lorc, pierre-leducq");
            Note("Icons licensed under CC BY 3.0: https://creativecommons.org/licenses/by/3.0/");
            Paragraph("Special thanks to everyone who helped test the mod a thousand times: Adam, Eitan, Guy, Tal, Yoav and everyone else");
            Spacer();
        }

        private static void ReloadFile()
        {
            string problem = Settings.Reload();
            if (problem != null)
            {
                Announcer.InformationPopup(problem);
            }
        }

        public void Header(string text)
        {
            Paragraph($"<size=130%><b>{text.ToUpperInvariant()}</b></size>");
        }

        public void Title(string text)
        {
            SetTitle("SPIDERSURGE / " + text.ToUpperInvariant());
        }

        private void SetTitle(string text)
        {
            if (_title != null) _title.text = text;
        }

        public void Note(string text)
        {
            Paragraph($"<size=95%><color={Gray}>{text}</color></size>");
        }

        public void Warning(string text)
        {
            Paragraph($"<size=95%><color={Orange}>{text}</color></size>");
        }

        public void Spacer()
        {
            var spacer = new GameObject("Section spacing", typeof(RectTransform), typeof(LayoutElement));
            spacer.transform.SetParent(_menu.objectParent, false);
            spacer.GetComponent<LayoutElement>().preferredHeight = 8f;
            _created.Add(spacer);
        }

        public void Paragraph(string text)
        {
            TMP_Text paragraph = _menu.CreateParagraph(text);
            if (paragraph != null)
            {
                SetText(paragraph, text);
                paragraph.fontSize = FontSize;
                paragraph.enableAutoSizing = false;
                paragraph.enableWordWrapping = true;
                paragraph.alignment = TextAlignmentOptions.TopLeft;
                paragraph.margin = Vector4.zero;
                paragraph.raycastTarget = false;
                var fitter = paragraph.GetComponent<ContentSizeFitter>();
                if (fitter != null) DestroyImmediate(fitter);
                _created.Add(paragraph.gameObject);
            }
        }

        // Text the controller can select, so a page of reading scrolls with the D-pad and sticks like the other pages
        public void Line(string text)
        {
            TMP_Text paragraph = _menu.CreateParagraph(text);
            if (paragraph == null)
            {
                return;
            }

            var row = new GameObject("Line", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            row.transform.SetParent(_menu.objectParent, false);
            var layout = row.GetComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(16, 16, 6, 6);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            paragraph.transform.SetParent(row.transform, false);
            SetText(paragraph, text);
            paragraph.fontSize = FontSize;
            paragraph.enableAutoSizing = false;
            paragraph.enableWordWrapping = true;
            paragraph.alignment = TextAlignmentOptions.TopLeft;
            paragraph.margin = Vector4.zero;
            paragraph.raycastTarget = false;
            var fitter = paragraph.GetComponent<ContentSizeFitter>();
            if (fitter != null) DestroyImmediate(fitter);
            var selectable = paragraph.gameObject.AddComponent<Selectable>();
            selectable.transition = Selectable.Transition.None;
            _created.Add(row);
            Register(selectable);
        }

        public void Checkbox(string label, bool value, Action<bool> onToggle)
        {
            CheckboxScript checkbox = _menu.CreateCheckbox(changed => Run(() => onToggle(changed)), label, value);
            if (checkbox != null)
            {
                _created.Add(checkbox.gameObject);
                SetText(checkbox.label, label);
                StyleRow(checkbox.gameObject);
                StyleLabel(checkbox.label, -1f, 1f);
                SetLayout(checkbox.toggle.gameObject, 30f, RowHeight, 0f, RowHeight);
                Register(checkbox.toggle);
            }
        }

        public void Toggle(string label, string key)
        {
            Checkbox(Settings.IsRemote(key) ? label + " (set by host)" : label, Settings.GetBool(key), value => Settings.Set(key, value));
        }

        public void Button(string label, Action onClick)
        {
            UnityEngine.UI.Button button = null;
            button = _menu.CreateButton(label, () =>
            {
                button.Select();
                Run(onClick);
            });
            if (button == null)
            {
                return;
            }

            _created.Add(button.gameObject);
            StyleRow(button.gameObject);
            var padding = button.gameObject.AddComponent<HorizontalLayoutGroup>();
            padding.padding = new RectOffset(16, 16, 4, 4);
            padding.childControlWidth = true;
            padding.childControlHeight = true;
            padding.childForceExpandWidth = true;
            padding.childForceExpandHeight = true;
            var text = button.GetComponentInChildren<TMP_Text>();
            SetText(text, label);
            text.fontSize = FontSize;
            text.enableAutoSizing = false;
            text.enableWordWrapping = true;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.margin = Vector4.zero;
            Register(button);
        }

        public void ConfirmButton(string label, Action onConfirm)
        {
            Button(label, () =>
            {
                _confirmation = onConfirm;
                _confirmationLabel = label;
                Open("confirm");
            });
        }

        // A button that steps through fixed choices of a text setting; a value typed into the file shows as is until changed
        public void Cycle(string label, string key, string[] values, string[] names, Func<string, bool> isCurrent = null)
        {
            string current = Settings.GetString(key);
            int index = Array.FindIndex(values, value => isCurrent != null ? isCurrent(value) : string.Equals(value, current, StringComparison.OrdinalIgnoreCase));
            string shown = index >= 0 ? names[index] : $"<noparse>{current}</noparse>";
            Button($"{label}: {shown}", () => Settings.Set(key, values[(index + 1) % values.Length]));
        }

        // Sliders step in whole numbers, so a value with decimals is stored as min + steps * step
        public void Slider(string label, string key, float min, float max, float step, string unit = "")
        {
            bool whole = Settings.Default(key) is int;
            float value = whole ? Settings.GetInt(key) : Settings.GetFloat(key);
            int steps = Mathf.Max(1, Mathf.RoundToInt((max - min) / step));
            int start = Mathf.Clamp(Mathf.RoundToInt((value - min) / step), 0, steps);

            SliderScript slider = null;
            slider = _menu.CreateSlider(position =>
            {
                float chosen = (float)Math.Round(min + Mathf.Round(position) * step, 4);
                slider.sliderValueText.text = Format(chosen, step, unit);
                _quiet = true;
                try
                {
                    Settings.SetLater(key, whole ? (object)Mathf.RoundToInt(chosen) : chosen);
                }
                finally
                {
                    _quiet = false;
                }
            }, label, start, 0f, steps);
            if (slider == null)
            {
                return;
            }

            slider.slider.wholeNumbers = true;
            slider.slider.value = start;
            SetText(slider.sliderDescription, label);
            SetText(slider.sliderValueText, Format(value, step, unit));
            StyleRow(slider.gameObject);
            StyleLabel(slider.sliderDescription, -1f, 1f);
            StyleLabel(slider.sliderValueText, 78f, 0f);
            SetLayout(slider.slider.gameObject, 180f, RowHeight, 0f, RowHeight);
            Register(slider.slider);
            _created.Add(slider.gameObject);
        }

        // A preferred height of -1 lets the row's layout group or the label's text decide, so wrapped labels get taller rows
        private static void SetLayout(GameObject target, float width, float minHeight, float flexibleWidth = 0f, float preferredHeight = -1f)
        {
            var layout = target.GetComponent<LayoutElement>() ?? target.AddComponent<LayoutElement>();
            layout.minWidth = width;
            layout.preferredWidth = width;
            layout.minHeight = minHeight;
            layout.preferredHeight = preferredHeight;
            layout.flexibleWidth = flexibleWidth;
        }

        private static void StyleRow(GameObject row)
        {
            SetLayout(row, -1f, RowHeight, 1f);
            var layout = row.GetComponent<HorizontalLayoutGroup>();
            if (layout != null)
            {
                layout.childControlWidth = true;
                layout.childControlHeight = true;
                layout.childForceExpandWidth = false;
                layout.childForceExpandHeight = false;
                layout.childAlignment = TextAnchor.MiddleLeft;
                layout.spacing = 16f;
            }
        }

        private static void StyleLabel(TMP_Text label, float width, float flexibleWidth)
        {
            label.fontSize = FontSize;
            label.enableAutoSizing = false;
            label.enableWordWrapping = true;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.margin = Vector4.zero;
            SetLayout(label.gameObject, width, RowHeight, flexibleWidth);
        }

        private void Register(Selectable control)
        {
            _controls.Add(control);
            var settingsControl = control.gameObject.AddComponent<SettingsControl>();
            if (!(control is UnityEngine.UI.Button))
            {
                var row = control.transform.parent.gameObject;
                settingsControl.Highlight = row.GetComponent<Image>() ?? row.AddComponent<Image>();
                settingsControl.Highlight.raycastTarget = false;
                settingsControl.Highlight.color = Color.clear;
            }
            var button = control.GetComponent<UIButton>();
            if (button != null) button.DeselectButtonAfterClick = false;
            settingsControl.ForwardCancel = button == null;
        }

        private void ConfigureNavigation()
        {
            for (int i = 0; i < _controls.Count; i++)
            {
                _controls[i].navigation = new Navigation
                {
                    mode = Navigation.Mode.Explicit,
                    selectOnUp = i == 0 ? _back : _controls[i - 1],
                    selectOnDown = i == _controls.Count - 1 ? _back : _controls[i + 1]
                };
            }
            _back.navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnUp = _controls.Count > 0 ? _controls[_controls.Count - 1] : null,
                selectOnDown = _controls.Count > 0 ? _controls[0] : null
            };
            _view.SelectedButton = _controls.Count > 0 ? _controls[0].gameObject : _back.gameObject;
        }

        // Keeps a control focused; clicks on empty space and the game's select-on-hover buttons must not scroll the list
        private void LateUpdate()
        {
            if (_menu == null || !_view.IsVisible || UIPopupManager.CurrentVisibleQueuePopup != null) return;
            foreach (var pad in Gamepad.all)
            {
                if (pad.buttonEast.wasPressedThisFrame)
                {
                    HandleBack();
                    return;
                }
            }
            var events = EventSystem.current;
            if (events == null) return;
            var selected = events.currentSelectedGameObject;
            if (selected == null)
            {
                if (_lastSelected != null && _lastSelected.activeInHierarchy) events.SetSelectedGameObject(_lastSelected);
                else Select(0);
                selected = events.currentSelectedGameObject;
            }
            if (selected == _lastSelected) return;
            _lastSelected = selected;
            if (selected != SettingsControl.Hovered) KeepSelectionVisible();
        }

        // Scroll only when focus moves, so the mouse wheel remains independent of selection.
        private void KeepSelectionVisible()
        {
            var selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            if (selected == null || !selected.transform.IsChildOf(_content)) return;
            Transform row = selected.transform;
            while (row.parent != _content) row = row.parent;
            var viewport = _scroll.viewport;
            var corners = new Vector3[4];
            ((RectTransform)row).GetWorldCorners(corners);
            var bounds = new Bounds(viewport.InverseTransformPoint(corners[0]), Vector3.zero);
            for (int i = 1; i < corners.Length; i++) bounds.Encapsulate(viewport.InverseTransformPoint(corners[i]));
            float delta = bounds.max.y > viewport.rect.yMax ? viewport.rect.yMax - bounds.max.y :
                bounds.min.y < viewport.rect.yMin ? viewport.rect.yMin - bounds.min.y : 0f;
            if (Mathf.Abs(delta) > 0.1f)
            {
                _scroll.StopMovement();
                _content.anchoredPosition += new Vector2(0f, delta);
            }
        }

        // The menu prefabs carry I2 Localize components that would put their placeholder text back
        internal static void SetText(TMP_Text text, string value)
        {
            if (text == null)
            {
                return;
            }
            foreach (I2.Loc.Localize localize in text.GetComponents<I2.Loc.Localize>())
            {
                DestroyImmediate(localize);
            }
            text.text = value;
        }

        private static string Format(float value, float step, string unit)
        {
            string format = step >= 1f ? "0" : step >= 0.1f ? "0.0" : "0.00";
            return value.ToString(format, CultureInfo.InvariantCulture) + unit;
        }

        private static void Run(Action action)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                Logger.LogError($"SpiderSurge settings action failed: {ex}");
            }
        }

        private void DestroyCreated()
        {
            for (int i = 0; i < _created.Count; i++)
            {
                if (_created[i] != null)
                {
                    StopClickAnimations(_created[i]);
                    DestroyImmediate(_created[i]);
                }
            }
            _created.Clear();
            _controls.Clear();
            _lastSelected = null;
        }

        // Doozy's click animations are DOTween sequences named after the button's RectTransform, and a finished punch starts an unnamed tween on it; left running, they fail once it is gone
        private static void StopClickAnimations(GameObject created)
        {
            foreach (RectTransform rect in created.GetComponentsInChildren<RectTransform>(true))
            {
                foreach (string animation in DoozyAnimations)
                {
                    DOTween.Kill($"{rect.GetInstanceID()}-{animation}");
                }
                DOTween.Kill(rect);
            }
        }

        private int SelectedIndex()
        {
            var events = EventSystem.current;
            GameObject selected = events != null ? events.currentSelectedGameObject : null;
            if (selected == null)
            {
                return -1;
            }

            for (int i = 0; i < _created.Count; i++)
            {
                if (_created[i] != null && selected.transform.IsChildOf(_created[i].transform))
                {
                    return i;
                }
            }
            return -1;
        }

        // Keeps controller navigation alive: reselect the nearest control at or above the old position
        private void Select(int index)
        {
            var events = EventSystem.current;
            if (events == null)
            {
                return;
            }
            if (index < 0) { _back.Select(); return; }

            int start = Mathf.Min(index, _created.Count - 1);
            for (int distance = 0; distance < _created.Count; distance++)
            {
                if (TrySelect(events, start - distance) || TrySelect(events, start + distance))
                {
                    return;
                }
            }
            _back.Select();
        }

        private bool TrySelect(EventSystem events, int index)
        {
            if (index < 0 || index >= _created.Count || _created[index] == null)
            {
                return false;
            }

            Selectable selectable = _created[index].GetComponentInChildren<Selectable>();
            if (selectable == null || !selectable.interactable || !selectable.gameObject.activeInHierarchy)
            {
                return false;
            }

            events.SetSelectedGameObject(selectable.gameObject);
            return true;
        }
    }

    public class SettingsControl : MonoBehaviour, ICancelHandler, ISelectHandler, IDeselectHandler, IPointerEnterHandler, IPointerExitHandler
    {
        public static GameObject Hovered;
        public Image Highlight;
        public bool ForwardCancel;

        public void OnPointerEnter(PointerEventData eventData)
        {
            Hovered = gameObject;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (Hovered == gameObject) Hovered = null;
        }

        private void OnDestroy()
        {
            if (Hovered == gameObject) Hovered = null;
        }

        public void OnSelect(BaseEventData eventData)
        {
            if (Highlight != null) Highlight.color = new Color(1f, 0f, 0.38f, 0.22f);
        }

        public void OnDeselect(BaseEventData eventData)
        {
            if (Highlight != null) Highlight.color = Color.clear;
        }

        // Doozy's UIButton already sends cancel to the BackButton; toggles and sliders do the same through this
        public void OnCancel(BaseEventData eventData)
        {
            if (ForwardCancel) BackButton.Instance.Execute();
        }
    }
}
