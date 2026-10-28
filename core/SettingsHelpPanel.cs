using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SpiderSurge.Common
{
    internal sealed class SettingsHelpPanel
    {
        private readonly RectTransform _list;
        private readonly Vector2 _offsetMin;
        private readonly Vector2 _offsetMax;
        private readonly RectTransform _panel;
        private readonly RectTransform _listScrollbar;
        private readonly Vector2 _scrollbarMin;
        private readonly Vector2 _scrollbarMax;
        private readonly TMP_Text _heading;
        private readonly TMP_Text _body;
        private readonly ScrollRect _scroll;
        private string _text;
        private bool _visible;
        private float _availableWidth = -1f;

        public SettingsHelpPanel(ScrollRect list, TMP_Text font)
        {
            _list = (RectTransform)list.transform;
            _offsetMin = _list.offsetMin;
            _offsetMax = _list.offsetMax;
            _listScrollbar = list.verticalScrollbar != null ? (RectTransform)list.verticalScrollbar.transform : null;
            if (_listScrollbar != null)
            {
                _scrollbarMin = _listScrollbar.offsetMin;
                _scrollbarMax = _listScrollbar.offsetMax;
            }
            _panel = new GameObject("Setting explanation", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            _panel.SetParent(_list.parent, false);
            _panel.anchorMin = _list.anchorMin;
            _panel.anchorMax = _list.anchorMax;
            _panel.pivot = _list.pivot;
            _panel.offsetMin = _offsetMin + new Vector2(_list.rect.width * 0.66f, 0f);
            _panel.offsetMax = _offsetMax;
            _panel.GetComponent<Image>().color = new Color(0.035f, 0.035f, 0.055f, 0.95f);
            _heading = Text("Help title", _panel, font, 18f);
            SettingsPage.Stretch(_heading.rectTransform, new Vector2(18f, -66f), new Vector2(-18f, -16f), new Vector2(0f, 1f), Vector2.one);

            var viewport = new GameObject("Help viewport", typeof(RectTransform), typeof(Image), typeof(RectMask2D)).GetComponent<RectTransform>();
            viewport.SetParent(_panel, false);
            SettingsPage.Stretch(viewport, new Vector2(18f, 44f), new Vector2(-18f, -76f));
            viewport.GetComponent<Image>().color = Color.clear;
            _scroll = viewport.gameObject.AddComponent<ScrollRect>();
            _scroll.horizontal = false;
            _scroll.movementType = ScrollRect.MovementType.Clamped;
            _scroll.scrollSensitivity = 15f;
            _scroll.viewport = viewport;
            _body = Text("Help text", viewport, font, 16f);
            SettingsPage.Stretch(_body.rectTransform, Vector2.zero, Vector2.zero, new Vector2(0f, 1f), Vector2.one);
            _body.rectTransform.pivot = new Vector2(0.5f, 1f);
            _body.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            _scroll.content = _body.rectTransform;

            var hint = Text("Help hint", _panel, font, 12f);
            hint.text = "SCROLL HELP: RIGHT STICK / WHEEL";
            hint.color = new Color(0.65f, 0.65f, 0.7f);
            SettingsPage.Stretch(hint.rectTransform, new Vector2(18f, 12f), new Vector2(-18f, 32f), Vector2.zero, new Vector2(1f, 0f));
        }

        public void SetVisible(bool visible)
        {
            _visible = visible;
            _availableWidth = -1f;
            RefreshLayout();
            _panel.gameObject.SetActive(visible);
        }

        public void RefreshLayout()
        {
            float width = ((RectTransform)_list.parent).rect.width * (_list.anchorMax.x - _list.anchorMin.x) + _offsetMax.x - _offsetMin.x;
            if (Mathf.Abs(width - _availableWidth) < 0.1f) return;
            _availableWidth = width;
            _list.offsetMin = _offsetMin;
            _list.offsetMax = _offsetMax;
            float helpWidth = Mathf.Clamp(width * 0.34f, 240f, 320f);
            if (_visible) _list.offsetMax -= new Vector2(helpWidth + 18f, 0f);
            _panel.offsetMin = _offsetMin + new Vector2(width - helpWidth, 0f);
            _panel.offsetMax = _offsetMax;
            if (_listScrollbar != null)
            {
                _listScrollbar.offsetMin = _visible ? new Vector2(-10f, _scrollbarMin.y) : _scrollbarMin;
                _listScrollbar.offsetMax = _visible ? new Vector2(-5f, _scrollbarMax.y) : _scrollbarMax;
            }
        }

        public void Show(string title, string description)
        {
            string text = title + "\n" + description;
            if (_text == text) return;
            _text = text;
            _heading.text = title;
            _body.text = description;
            _scroll.StopMovement();
            _body.rectTransform.anchoredPosition = Vector2.zero;
        }

        public void Destroy()
        {
            SetVisible(false);
            _panel.gameObject.SetActive(false);
            Object.Destroy(_panel.gameObject);
        }

        public void Scroll(float direction)
        {
            float overflow = _body.rectTransform.rect.height - _scroll.viewport.rect.height;
            if (Mathf.Abs(direction) > 0.2f && overflow > 0f)
                _scroll.verticalNormalizedPosition = Mathf.Clamp01(_scroll.verticalNormalizedPosition + direction * Time.unscaledDeltaTime * 240f / overflow);
        }

        private static TMP_Text Text(string name, Transform parent, TMP_Text source, float size)
        {
            var text = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI)).GetComponent<TextMeshProUGUI>();
            text.transform.SetParent(parent, false);
            text.font = source.font;
            text.fontSize = size;
            text.color = Color.white;
            text.enableWordWrapping = true;
            text.raycastTarget = false;
            text.alignment = TextAlignmentOptions.TopLeft;
            return text;
        }

    }

    public sealed class SettingsRowHover : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        public SettingsControl Control;
        public void OnPointerEnter(PointerEventData eventData) => Control.OnPointerEnter(eventData);
        public void OnPointerExit(PointerEventData eventData) => Control.OnPointerExit(eventData);
    }
}
