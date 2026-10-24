using SpiderSurge.Common;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SpiderSurge
{
    public class ChaosHud : MonoBehaviour
    {
        private static ChaosHud _instance;
        private TMP_Text _points;
        private TMP_Text _challenge;
        private float _flashUntil;

        public static void Tick()
        {
            var hud = SurvivalModeHud.instance;
            if (_instance == null && ChaosWallet.Enabled && hud != null && hud.survivalHud != null && hud.livesCounter != null)
            {
                var root = new GameObject("ChaosHud", typeof(RectTransform), typeof(LayoutElement));
                root.GetComponent<LayoutElement>().ignoreLayout = true;
                var rect = (RectTransform)root.transform;
                rect.SetParent(hud.survivalHud.transform, false);
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
                rect.anchoredPosition = new Vector2(35, -62);
                rect.sizeDelta = new Vector2(470, 60);
                _instance = root.AddComponent<ChaosHud>();
                _instance._points = _instance.Label(hud.livesCounter, "Points", 0, 17);
                _instance._challenge = _instance.Label(hud.livesCounter, "Challenge", -25, 14);
            }
            if (_instance == null) return;
            _instance.gameObject.SetActive(ChaosWallet.Enabled);
            if (!ChaosWallet.Enabled) return;
            _instance._points.text = $"Team chaos points: {ChaosWallet.Points}";
            _instance._points.color = Time.unscaledTime < _instance._flashUntil ? Hud.Warm : Color.white;
            _instance._challenge.text = ChallengeController.Status;
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
            _flashUntil = Time.unscaledTime + .8f;
            SoundManager.Instance?.PlaySound(Consts.SoundNames.LuckyUpgrade, Consts.SoundVolumes.LuckyUpgrade * Consts.SoundVolumes.MasterVolume);
        }
    }
}
