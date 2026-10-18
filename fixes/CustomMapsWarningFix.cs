using HarmonyLib;
using TMPro;

namespace SpiderSurge.Fixes
{
    [HarmonyPatch(typeof(WarningCastomMaps), "OnEnable")]
    internal static class CustomMapsWarningEnablePatch
    {
        // The Xbox build has no custom maps, and its warning hides itself in Awake without an OnEnable to patch
        static bool Prepare() => AccessTools.DeclaredMethod(typeof(WarningCastomMaps), "OnEnable") != null;

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
