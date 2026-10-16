using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
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
        private const float CheckboxLabelWidth = 330f;
        private const float ButtonWidth = 300f;
        private const float MaxButtonWidth = 700f;
        private const float ButtonPadding = 60f;
        private const float SliderLabelWidth = 200f;
        private const float SliderBarWidth = 150f;
        private static readonly string[] DoozyAnimations = { "Punch-Move", "Punch-Rotate", "Punch-Scale", "Punch-Fade", "State-Move", "State-Rotate", "State-Scale", "State-Fade" };

        private static readonly Part[] MenuOrder = { Parts.Stats, Parts.Surge, Parts.Teams, Parts.Fixes };
        private static SettingsPage _open;

        private ModsMenuPopup _menu;
        private readonly List<GameObject> _created = new List<GameObject>();
        private readonly Stack<string> _history = new Stack<string>();
        private int _baseIndex;
        private bool _subscribed;
        private bool _quiet;
        private Coroutine _pending;

        public string Current { get; private set; } = MainPage;

        public static void RefreshOpen()
        {
            if (_open != null)
            {
                _open.RequestRebuild(null);
            }
        }

        public void Build(ModsMenuPopup menu)
        {
            DestroyCreated();
            _menu = menu;
            _open = this;
            _baseIndex = menu.objectParent != null ? menu.objectParent.childCount : 0;
            _history.Clear();
            Current = MainPage;
            if (!_subscribed)
            {
                Settings.Changed += RequestRebuild;
                _subscribed = true;
            }
            Rebuild(-1);
        }

        public void Clear()
        {
            DestroyCreated();
            _menu = null;
            if (_open == this)
            {
                _open = null;
            }
        }

        public void Open(string page)
        {
            _history.Push(Current);
            Show(page);
        }

        public void Back()
        {
            Show(_history.Count > 0 ? _history.Pop() : MainPage);
        }

        // The clicked button is still handling its click, so the page changes on the next frame
        private void Show(string page)
        {
            Current = page;
            if (_pending != null)
            {
                StopCoroutine(_pending);
            }
            _pending = StartCoroutine(ShowNextFrame());
        }

        private IEnumerator ShowNextFrame()
        {
            yield return null;
            _pending = null;
            Rebuild(0, true);
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

        private void Rebuild(int selectIndex, bool toTop = false)
        {
            try
            {
                DestroyCreated();
                if (_menu == null || _menu.objectParent == null)
                {
                    return;
                }

                AddContent();
                for (int i = 0; i < _created.Count; i++)
                {
                    if (_created[i] != null)
                    {
                        _created[i].transform.SetSiblingIndex(_baseIndex + i);
                    }
                }
                Select(selectIndex);
                if (toTop)
                {
                    ScrollToTop();
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"SpiderSurge settings page failed: {ex}");
            }
        }

        private void AddContent()
        {
            if (Current == MainPage)
            {
                AddMain();
                return;
            }

            Part part = Array.Find(Parts.All, candidate => Current == candidate.Key || Current.StartsWith(candidate.Key + ".", StringComparison.Ordinal));
            Button("Return", Back);
            if (part == null || !part.Running)
            {
                Warning("This part isn't running.");
                return;
            }
            part.BuildSettings(this);
            Spacer();
            Button("Return", Back);
            Spacer();
        }

        private void AddMain()
        {
            Paragraph($"<size=200%>\n<b>{Hud.Logo}</b></size>");
            Paragraph($"<size=115%>Version {SpiderSurgeMod.Version}</size>");
            Note("Turning a part on or off takes effect after restarting the game. Its settings below change right away.");

            foreach (Part part in MenuOrder)
            {
                Spacer();
                Checkbox($"<b>{part.Title}</b>", part.Enabled, value => Settings.Set(part.SettingKey, value));
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
                if (part.Running && part.HasSettings)
                {
                    Button($"{part.Title} settings", () => Open(part.Key));
                }
            }

            Spacer();
            Header("General");
            Toggle("Check for updates", ModUpdater.CheckForUpdatesKey);
            Button("Reload the settings file", ReloadFile);
            Note($"Settings file: <noparse>{Settings.FilePath}</noparse>");

            Spacer();
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
            Paragraph($"<size=170%>\n<b><color={Hud.PinkHex}>{text.ToUpperInvariant()}</color></b></size>");
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
            Paragraph("<size=100%>\n </size>");
        }

        public void Paragraph(string text)
        {
            TMP_Text paragraph = _menu.CreateParagraph(text);
            if (paragraph != null)
            {
                _created.Add(paragraph.gameObject);
            }
        }

        public void Checkbox(string label, bool value, Action<bool> onToggle)
        {
            CheckboxScript checkbox = _menu.CreateCheckbox(changed => Run(() => onToggle(changed)), label, value);
            if (checkbox != null)
            {
                _created.Add(checkbox.gameObject);
                SetText(checkbox.label, label);
                Widen(checkbox.label != null ? checkbox.label.rectTransform : null, CheckboxLabelWidth);
            }
        }

        public void Toggle(string label, string key)
        {
            Checkbox(label, Settings.GetBool(key), value => Settings.Set(key, value));
        }

        public void Button(string label, Action onClick)
        {
            UnityEngine.UI.Button button = ((ModsMenu)_menu).CreateButton(label, () => Run(onClick));
            if (button == null)
            {
                return;
            }

            _created.Add(button.gameObject);
            var text = button.GetComponentInChildren<TMP_Text>();
            float textWidth = 0f;
            if (text != null)
            {
                text.fontSize *= 1.2f;
                textWidth = text.GetPreferredValues(text.text, float.PositiveInfinity, float.PositiveInfinity).x + ButtonPadding;
            }

            var rect = button.GetComponent<RectTransform>();
            if (rect != null)
            {
                float width = Mathf.Clamp(Mathf.Max(rect.sizeDelta.x * 1.1f, textWidth), ButtonWidth, MaxButtonWidth);
                rect.sizeDelta = new Vector2(width, rect.sizeDelta.y * 1.45f);
            }
        }

        // A button that steps through fixed choices of a text setting; a value typed into the file shows as is until changed
        public void Cycle(string label, string key, string[] values, string[] names)
        {
            string current = Settings.GetString(key);
            int index = Array.FindIndex(values, value => string.Equals(value, current, StringComparison.OrdinalIgnoreCase));
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
            Widen(slider.sliderDescription != null ? slider.sliderDescription.rectTransform : null, SliderLabelWidth);
            var bar = slider.slider.GetComponent<RectTransform>();
            if (bar != null)
            {
                bar.sizeDelta = new Vector2(SliderBarWidth, bar.sizeDelta.y);
            }
            _created.Add(slider.gameObject);
        }

        private static void Widen(RectTransform rect, float width)
        {
            if (rect != null && rect.sizeDelta.x < width)
            {
                rect.sizeDelta = new Vector2(width, rect.sizeDelta.y);
            }
        }

        // The menu prefabs carry I2 Localize components that would put their placeholder text back
        private static void SetText(TMP_Text text, string value)
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
            if (_pending != null)
            {
                StopCoroutine(_pending);
                _pending = null;
            }
            for (int i = 0; i < _created.Count; i++)
            {
                if (_created[i] != null)
                {
                    StopClickAnimations(_created[i]);
                    DestroyImmediate(_created[i]);
                }
            }
            _created.Clear();
        }

        // Doozy's click animations are DOTween sequences named after the button's RectTransform; left running, they fail once it is gone
        private static void StopClickAnimations(GameObject created)
        {
            foreach (RectTransform rect in created.GetComponentsInChildren<RectTransform>(true))
            {
                foreach (string animation in DoozyAnimations)
                {
                    DOTween.Kill($"{rect.GetInstanceID()}-{animation}");
                }
            }
        }

        private void ScrollToTop()
        {
            ScrollRect scroll = _menu != null && _menu.objectParent != null ? _menu.objectParent.GetComponentInParent<ScrollRect>() : null;
            if (scroll != null)
            {
                scroll.verticalNormalizedPosition = 1f;
                StartCoroutine(ScrollToTopAfterLayout(scroll));
            }
        }

        private static IEnumerator ScrollToTopAfterLayout(ScrollRect scroll)
        {
            yield return null;
            if (scroll != null)
            {
                scroll.verticalNormalizedPosition = 1f;
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
            if (index < 0 || events == null || _created.Count == 0)
            {
                return;
            }

            int start = Mathf.Min(index, _created.Count - 1);
            for (int distance = 0; distance < _created.Count; distance++)
            {
                if (TrySelect(events, start - distance) || TrySelect(events, start + distance))
                {
                    return;
                }
            }
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
}
