using System.Collections;
using SpiderSurge.Common;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Logger = Silk.Logger;

namespace SpiderSurge
{
    // A thin meter bar under the lives counter of the survival HUD, with the team's chaos point count next to it; it ignores the HUD's row layout
    public class MeterHud : MonoBehaviour
    {
        private const string ObjectName = "SurgeMeter";
        private const float BarWidth = 120f;
        private const float BarHeight = 8f;
        private const float Left = 35f;
        private const float Top = -62f;

        private static MeterHud _instance;
        private static bool _failed;

        private RectTransform _fill;
        private Image _fillImage;
        private TMP_Text _points;
        private Color _pointsColor = Color.white;
        private int _shownPoints = -1;
        private float _shownFill = -1f;
        private Coroutine _blink;

        // Called once per frame from SurgeGameModeManager.LateUpdate
        public static void Tick()
        {
            bool show = SurgeMeter.Enabled;
            if (_instance == null)
            {
                if (!show || _failed) return;
                var hud = SurvivalModeHud.instance;
                if (hud == null || hud.survivalHud == null || hud.livesCounter == null) return;
                Create(hud);
                if (_instance == null) return;
            }
            _instance.gameObject.SetActive(show);
            if (show) _instance.Redraw();
        }

        private static void Create(SurvivalModeHud hud)
        {
            try
            {
                Transform parent = hud.survivalHud.transform;
                Transform existing = parent.Find(ObjectName);
                if (existing != null) Destroy(existing.gameObject);

                var root = new GameObject(ObjectName, typeof(RectTransform), typeof(LayoutElement));
                root.GetComponent<LayoutElement>().ignoreLayout = true;
                var rect = (RectTransform)root.transform;
                rect.SetParent(parent, false);
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(0f, 1f);
                rect.pivot = new Vector2(0f, 1f);
                rect.anchoredPosition = new Vector2(Left, Top);
                rect.sizeDelta = new Vector2(BarWidth + 60f, BarHeight);

                var back = new GameObject("Back", typeof(RectTransform), typeof(Image));
                var backRect = (RectTransform)back.transform;
                backRect.SetParent(rect, false);
                backRect.anchorMin = Vector2.zero;
                backRect.anchorMax = new Vector2(0f, 1f);
                backRect.pivot = new Vector2(0f, 0.5f);
                backRect.anchoredPosition = Vector2.zero;
                backRect.sizeDelta = new Vector2(BarWidth, 0f);
                back.GetComponent<Image>().color = Hud.Card;

                var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
                var fillRect = (RectTransform)fill.transform;
                fillRect.SetParent(backRect, false);
                fillRect.anchorMin = Vector2.zero;
                fillRect.anchorMax = new Vector2(0f, 1f);
                fillRect.pivot = new Vector2(0f, 0.5f);
                fillRect.anchoredPosition = Vector2.zero;
                fillRect.sizeDelta = Vector2.zero;
                var fillImage = fill.GetComponent<Image>();
                fillImage.color = Hud.Pink;

                var points = Instantiate(hud.livesCounter.gameObject, rect).GetComponent<TMP_Text>();
                points.gameObject.name = "Points";
                SettingsPage.SetText(points, "");
                var pointsRect = points.rectTransform;
                pointsRect.anchorMin = new Vector2(0f, 0.5f);
                pointsRect.anchorMax = new Vector2(0f, 0.5f);
                pointsRect.pivot = new Vector2(0f, 0.5f);
                pointsRect.anchoredPosition = new Vector2(BarWidth + 6f, 0f);
                pointsRect.sizeDelta = new Vector2(240f, 40f);
                pointsRect.localScale = Vector3.one * 0.5f;
                points.alignment = TextAlignmentOptions.MidlineLeft;
                points.enableWordWrapping = false;

                var meter = root.AddComponent<MeterHud>();
                meter._fill = fillRect;
                meter._fillImage = fillImage;
                meter._points = points;
                meter._pointsColor = points.color;
                _instance = meter;
            }
            catch (System.Exception ex)
            {
                _failed = true;
                Logger.LogError($"[Surge meter] could not build the HUD bar: {ex}");
            }
        }

        private void OnEnable()
        {
            SurgeMeter.PointEarned += OnPointEarned;
        }

        private void OnDisable()
        {
            SurgeMeter.PointEarned -= OnPointEarned;
            _blink = null;
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        private void Redraw()
        {
            float fill = SurgeMeter.Fill;
            if (!Mathf.Approximately(fill, _shownFill))
            {
                _shownFill = fill;
                _fill.sizeDelta = new Vector2(BarWidth * fill, 0f);
            }
            int points = SurgeMeter.Points;
            if (points != _shownPoints)
            {
                _shownPoints = points;
                _points.text = points == 1 ? "1 chaos point" : $"{points} chaos points";
            }
        }

        private void OnPointEarned()
        {
            if (!isActiveAndEnabled) return;
            SoundManager.Instance?.PlaySound(Consts.SoundNames.LuckyUpgrade, Consts.SoundVolumes.LuckyUpgrade * Consts.SoundVolumes.MasterVolume);
            if (_blink != null) StopCoroutine(_blink);
            _blink = StartCoroutine(Blink());
        }

        // Runs on real time, so the flash shows during the slow motion at a wave's end too
        private IEnumerator Blink()
        {
            float start = Time.unscaledTime;
            while (Time.unscaledTime - start < Consts.Values.Meter.HudBlinkSeconds)
            {
                float t = (Time.unscaledTime - start) / Consts.Values.Meter.HudBlinkSeconds;
                _fillImage.color = Color.Lerp(Color.white, Hud.Pink, Hud.Ease(t));
                _points.color = Color.Lerp(Hud.Warm, _pointsColor, Hud.Ease(t));
                yield return null;
            }
            _fillImage.color = Hud.Pink;
            _points.color = _pointsColor;
            _blink = null;
        }
    }
}
