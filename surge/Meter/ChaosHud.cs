using SpiderSurge.Common;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Logger = Silk.Logger;

namespace SpiderSurge
{
    public class ChaosHud : MonoBehaviour
    {
        private static ChaosHud _instance;
        private static bool _failed;
        private TMP_Text _points;
        private TMP_Text _challenge;
        private float _flashUntil;
        private int _shownPoints = -1;
        private string _shownStatus;
        private bool _shownFlash;

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
        }

        private static void Create(SurvivalModeHud hud)
        {
            GameObject root = null;
            try
            {
                root = new GameObject("ChaosHud", typeof(RectTransform), typeof(LayoutElement));
                root.GetComponent<LayoutElement>().ignoreLayout = true;
                var rect = (RectTransform)root.transform;
                rect.SetParent(hud.survivalHud.transform, false);
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
                rect.anchoredPosition = new Vector2(35, -62);
                rect.sizeDelta = new Vector2(470, 60);
                var chaos = root.AddComponent<ChaosHud>();
                chaos._points = chaos.Label(hud.livesCounter, "Points", 0, 17);
                chaos._challenge = chaos.Label(hud.livesCounter, "Challenge", -25, 14);
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
                _points.text = $"Team chaos points: {_shownPoints}";
            }
            bool flash = Time.unscaledTime < _flashUntil;
            if (flash != _shownFlash)
            {
                _shownFlash = flash;
                _points.color = flash ? Hud.Warm : Color.white;
            }
            string status = ChallengeController.Status;
            if (status != _shownStatus)
            {
                _shownStatus = status;
                _challenge.text = status;
            }
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

        private void OnEnable() => ChaosWallet.PointEarned += Earned;
        private void OnDisable() => ChaosWallet.PointEarned -= Earned;
        private void OnDestroy() { if (_instance == this) _instance = null; }
        private void Earned()
        {
            _flashUntil = Time.unscaledTime + Consts.Values.Chaos.HudBlinkSeconds;
            SoundManager.Instance?.PlaySound(Consts.SoundNames.LuckyUpgrade, Consts.SoundVolumes.LuckyUpgrade * Consts.SoundVolumes.MasterVolume);
        }
    }
}
