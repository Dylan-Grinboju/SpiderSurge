using HarmonyLib;

namespace SpiderSurge.Fixes
{
    [HarmonyPatch(typeof(HudController), nameof(HudController.OpenLobby), new[] { typeof(bool) })]
    internal static class ReturnToLobbyFix
    {
        // Doozy keeps hidden views active, so IsActive alone can block returning to the lobby forever.
        private static void Prefix(ref bool ignorUiConnectingView)
        {
            var level = LevelController.instance;
            var view = level != null ? level.connectingView : null;
            if (view != null && !view.IsVisible && !view.IsShowing && !view.IsHiding)
            {
                ignorUiConnectingView = true;
            }
        }
    }
}
