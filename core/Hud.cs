using TMPro;
using UnityEngine;

namespace SpiderSurge.Common
{
    // The game's look for the IMGUI windows: its Spartan font, hot pink and dark purple panels.
    public static class Hud
    {
        public const string PinkHex = "#FF0061";

        public static readonly Color Pink = Rgb(255, 0, 97);
        public static readonly Color Ink = new Color(0.035f, 0.027f, 0.063f, 0.96f);
        public static readonly Color Card = new Color(0.106f, 0.086f, 0.161f, 0.94f);
        public static readonly Color Stripe = new Color(1f, 1f, 1f, 0.045f);
        public static readonly Color Line = new Color(0.67f, 0.63f, 0.87f, 0.28f);
        public static readonly Color Text = Rgb(246, 242, 255);
        public static readonly Color Muted = Rgb(167, 157, 196);
        public static readonly Color Dim = Rgb(98, 91, 122);
        public static readonly Color Good = Rgb(92, 242, 160);
        public static readonly Color Warm = Rgb(255, 182, 39);
        public static readonly Color Dimmer = new Color(0.012f, 0.008f, 0.024f, 0.93f);

        private const float FontRetryInterval = 3f;
        private static readonly string[] _numbers = new string[1000];
        private static Texture2D _white;
        private static Font _regular;
        private static Font _bold;
        private static float _nextFontSearch;

        public static int FontVersion { get; private set; }

        public static Color Rgb(int r, int g, int b) => new Color(r / 255f, g / 255f, b / 255f, 1f);

        public static Color WithAlpha(Color color, float alpha) => new Color(color.r, color.g, color.b, color.a * alpha);

        // Dark spider colors get lightened so names stay readable on the dark panels.
        public static Color Readable(Color color)
        {
            float luminance = 0.2126f * color.r + 0.7152f * color.g + 0.0722f * color.b;
            return luminance < 0.3f ? Color.Lerp(color, Color.white, 0.45f) : new Color(color.r, color.g, color.b, 1f);
        }

        public static void EnsureFonts()
        {
            if ((_regular != null && _bold != null) || Time.unscaledTime < _nextFontSearch)
            {
                return;
            }
            _nextFontSearch = Time.unscaledTime + FontRetryInterval;

            bool found = false;
            foreach (Font font in Resources.FindObjectsOfTypeAll<Font>())
            {
                if (_regular == null && font.name == "Spartan-Regular")
                {
                    _regular = font;
                    found = true;
                }
                else if (_bold == null && font.name == "Spartan-SemiBold")
                {
                    _bold = font;
                    found = true;
                }
            }
            if (found)
            {
                FontVersion++;
            }
        }

        public static GUIStyle Style(int size, bool bold = false, TextAnchor anchor = TextAnchor.MiddleLeft, bool wrap = false)
        {
            EnsureFonts();
            Font font = bold && _bold != null ? _bold : _regular;
            var style = new GUIStyle(GUI.skin.label)
            {
                font = font,
                fontSize = size,
                fontStyle = bold && (_bold == null || font == null) ? FontStyle.Bold : FontStyle.Normal,
                alignment = anchor,
                wordWrap = wrap,
                richText = true,
                clipping = wrap ? TextClipping.Overflow : TextClipping.Clip,
                padding = new RectOffset(0, 0, 0, 0),
                margin = new RectOffset(0, 0, 0, 0),
            };
            style.normal.textColor = Text;
            return style;
        }

        public static Texture2D White
        {
            get
            {
                if (_white == null)
                {
                    _white = MakeTexture(Color.white);
                }
                return _white;
            }
        }


        public static void Fill(Rect rect, Color color)
        {
            Color old = GUI.color;
            GUI.color = new Color(color.r, color.g, color.b, color.a * old.a);
            GUI.DrawTexture(rect, White);
            GUI.color = old;
        }

        public static void Label(Rect rect, string text, GUIStyle style, Color color, bool shadow = false)
        {
            if (shadow)
            {
                float offset = Mathf.Max(1f, Mathf.Round(style.fontSize / 14f));
                style.normal.textColor = new Color(0f, 0f, 0f, 0.7f * color.a);
                GUI.Label(new Rect(rect.x + offset, rect.y + offset, rect.width, rect.height), text, style);
            }
            style.normal.textColor = color;
            GUI.Label(rect, text, style);
        }

        public static void Resize(GUIStyle style, int baseSize, float scale)
        {
            style.fontSize = Mathf.Max(1, Mathf.RoundToInt(baseSize * scale));
        }

        // Shrinks the UI scale so a panel of the given base size fits in that share of the screen.
        public static float FitScale(float scale, float baseWidth, float baseHeight, float maxWidthPercent, float maxHeightPercent)
        {
            return Mathf.Min(scale, Screen.width * maxWidthPercent / baseWidth, Screen.height * maxHeightPercent / baseHeight);
        }

        // Stat numbers are drawn every frame, so each one is turned into text once
        public static string Number(int value)
        {
            if (value < 0 || value >= _numbers.Length)
            {
                return value.ToString();
            }
            return _numbers[value] ??= value.ToString();
        }

        // A copy of one of the game's texts that keeps the given text instead of its I2 term; size, alignment and autosize are the caller's
        public static TMP_Text CloneText(TMP_Text template, Transform parent, string name, string text)
        {
            var clone = Object.Instantiate(template.gameObject, parent).GetComponent<TMP_Text>();
            clone.name = name;
            clone.gameObject.SetActive(true);
            SettingsPage.SetText(clone, text);
            clone.raycastTarget = false;
            clone.rectTransform.localScale = Vector3.one;
            clone.rectTransform.localRotation = Quaternion.identity;
            return clone;
        }

        public static float Width(GUIStyle style, string text) => style.CalcSize(new GUIContent(text)).x;

        public static float Ease(float t)
        {
            t = Mathf.Clamp01(t);
            return 1f - (1f - t) * (1f - t) * (1f - t);
        }

        private static Texture2D MakeTexture(Color color)
        {
            var texture = new Texture2D(1, 1) { hideFlags = HideFlags.HideAndDontSave };
            texture.SetPixel(0, 0, color);
            texture.Apply();
            return texture;
        }
    }
}
