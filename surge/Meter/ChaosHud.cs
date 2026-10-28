using SpiderSurge.Common;
using TMPro;
using UnityEngine;
using Logger = Silk.Logger;

namespace SpiderSurge
{
    public class ChaosHud : MonoBehaviour
    {
        private static ChaosHud _instance;
        private static bool _failed;
        private TMP_Text _points;
        private TMP_Text _challenge;
        private float _pulseRemaining;
        private int _shownPoints = -1;
        private string _shownStatus;
        private bool _wasPulsing;

        // Called once per frame from SurgeGameModeManager.LateUpdate
        public static void Tick()
        {
            bool show = ChaosWallet.Enabled;
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
            else _instance.ResetFeedback();
        }

        private static void Create(SurvivalModeHud hud)
        {
            GameObject root = null;
            try
            {
                root = new GameObject("ChaosHud", typeof(RectTransform));
                var rect = (RectTransform)root.transform;
                rect.SetParent(hud.survivalHud.transform, false);
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
                rect.sizeDelta = new Vector2(470, 54);
                const float sectionInset = 60f;
                var chaos = root.AddComponent<ChaosHud>();
                chaos._points = chaos.Label(hud.livesCounter, "Points", 0, 15);
                chaos._points.rectTransform.pivot = new Vector2(0, 0.5f);
                chaos._points.rectTransform.sizeDelta = new Vector2(430 - sectionInset, 28);
                chaos._points.rectTransform.anchoredPosition = new Vector2(sectionInset + 28, -25);
                ChaosIcon.Add(root.transform, new Vector2(sectionInset + 12, -25), 14, Hud.Warm, new Vector2(0, 1));
                chaos._challenge = chaos.Label(hud.livesCounter, "Challenge", -32, 12);
                chaos._challenge.rectTransform.anchoredPosition = new Vector2(sectionInset, -32);
                chaos._challenge.rectTransform.sizeDelta = new Vector2(470 - sectionInset, 25);
                _instance = chaos;
            }
            catch (System.Exception ex)
            {
                _failed = true;
                if (root != null) Destroy(root);
                Logger.LogError($"[Chaos] could not build the HUD: {ex}");
            }
        }

        private void Redraw()
        {
            if (ChaosWallet.Points != _shownPoints)
            {
                _shownPoints = ChaosWallet.Points;
                _points.text = $"{_shownPoints}";
            }
            AnimatePoints();
            string status = ChallengeController.Status;
            if (status != _shownStatus)
            {
                _shownStatus = status;
                _challenge.text = status;
                _challenge.color = ChallengeController.Failed ? Hud.Pink : Hud.Muted;
            }
        }

        private void AnimatePoints()
        {
            if (_pulseRemaining <= 0f && !_wasPulsing) return;
            float pulse = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_pulseRemaining / Consts.Values.Chaos.HudGainSeconds));
            _points.rectTransform.localScale = Vector3.one * Mathf.Lerp(1f, Consts.Values.Chaos.HudGainScale, pulse);
            _points.color = Color.Lerp(Color.white, Hud.Warm, pulse);
            _wasPulsing = _pulseRemaining > 0f;
            if (_points.isActiveAndEnabled && SurvivalModeHud.instance.survivalHud.IsVisible)
                _pulseRemaining = Mathf.Max(0f, _pulseRemaining - Time.unscaledDeltaTime);
        }

        private void ResetFeedback()
        {
            _pulseRemaining = 0f;
            _wasPulsing = false;
            _points.rectTransform.localScale = Vector3.one;
            _points.color = Color.white;
        }

        private TMP_Text Label(TMP_Text template, string name, float y, float size)
        {
            var text = Instantiate(template.gameObject, transform).GetComponent<TMP_Text>();
            text.name = name;
            SettingsPage.SetText(text, "");
            text.fontSize = size;
            text.enableAutoSizing = false;
            text.enableWordWrapping = false;
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.raycastTarget = false;
            text.color = Color.white;
            var rect = text.rectTransform;
            rect.localScale = Vector3.one;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(0, y);
            rect.sizeDelta = new Vector2(470, 25);
            return text;
        }

        private void Awake()
        {
            ChaosWallet.PointEarned += Earned;
            ChaosWallet.Changed += WalletChanged;
        }
        private void OnDestroy()
        {
            ChaosWallet.PointEarned -= Earned;
            ChaosWallet.Changed -= WalletChanged;
            if (_instance == this) _instance = null;
        }
        private void WalletChanged()
        {
            if (_points != null && ChaosWallet.PointsEarned == 0) ResetFeedback();
        }
        private void Earned()
        {
            if (!ChaosWallet.Enabled) return;
            _pulseRemaining = Consts.Values.Chaos.HudGainSeconds;
            SoundManager.Instance?.PlaySound(Consts.SoundNames.LuckyUpgrade, Consts.SoundVolumes.LuckyUpgrade * Consts.SoundVolumes.MasterVolume);
        }
    }
}
