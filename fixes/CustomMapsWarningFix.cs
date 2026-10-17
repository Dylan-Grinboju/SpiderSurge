using HarmonyLib;
using TMPro;

namespace SpiderSurge.Fixes
{
    [HarmonyPatch(typeof(WarningCastomMaps), "OnEnable")]
    internal static class CustomMapsWarningEnablePatch
    {
        static bool Prefix(TextMeshProUGUI ___warningCastomMapsText)
        {
            if (VersusMapSettings.instance != null)
            {
                return true;
            }
            if (___warningCastomMapsText != null)
            {
                ___warningCastomMapsText.text = string.Empty;
            }
            return false;
        }
    }
}
