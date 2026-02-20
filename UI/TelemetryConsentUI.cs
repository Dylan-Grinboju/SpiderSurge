using System.Collections;
using UnityEngine;

namespace SpiderSurge
{
    public class TelemetryConsentUI : MonoBehaviour
    {
        private static TelemetryConsentUI _instance;

        public static void ResetInstance()
        {
            _instance = null;
        }

        public static void Initialize()
        {
            if (_instance != null)
            {
                return;
            }

            var obj = new GameObject("TelemetryConsentUI");
            _instance = obj.AddComponent<TelemetryConsentUI>();
            DontDestroyOnLoad(obj);
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
        }

        private void Start()
        {
            if (ModConfig.ShowTelemetryConsentPrompt)
            {
                StartCoroutine(ShowConsentPopupWhenReady());
            }
        }

        private IEnumerator ShowConsentPopupWhenReady()
        {
            yield return new WaitForSeconds(5f);

            if (!ModConfig.ShowTelemetryConsentPrompt)
            {
                yield break;
            }

            Announcer.TwoOptionsPopup(
                "Would you like to send anonymized match data for SpiderSurge?\nThis helps improve balancing and stability. No personal identifiers are sent.",
                "Yes",
                "No",
                () => AcceptConsent(true),
                () => AcceptConsent(false),
                null
            );
        }

        private void AcceptConsent(bool enabled)
        {
            ModConfig.SetTelemetryEnabled(enabled);
            ModConfig.SetShowTelemetryConsentPrompt(false);
        }
    }
}