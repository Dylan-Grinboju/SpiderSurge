using HarmonyLib;
using Doozy.Engine.UI;
using Unity.Netcode;
using UnityEngine.UI;

namespace SpiderSurge
{
    [HarmonyPatch(typeof(HudController), "Restart")]
    public class HudController_Restart_Patch
    {
        [HarmonyPrefix]
        public static bool Prefix(HudController __instance)
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsHost)
            {
                return true;
            }

            if (!HudControllerPatches.IsPauseMenuButton(__instance, __instance.restart))
            {
                return true;
            }

            Announcer.ConfirmationPopup("Are you sure?", __instance.restart.GetComponent<UIButton>(), delegate
            {
                __instance.Resume();
                GameController.Restart();
            }, null, null, true, false);

            return false;
        }
    }

    [HarmonyPatch(typeof(HudController), "OpenLobby", new System.Type[] { })]
    public class HudController_OpenLobby_Patch
    {
        [HarmonyPrefix]
        public static bool Prefix(HudController __instance)
        {
            if (!HudControllerPatches.IsPauseMenuButton(__instance, __instance.mainMenuButton))
            {
                return true;
            }

            Announcer.ConfirmationPopup("Are you sure?", __instance.mainMenuButton.GetComponent<UIButton>(), delegate
            {
                __instance.OpenLobby(false);
            }, null, null, true, false);

            return false;
        }
    }

    public static class HudControllerPatches
    {
        // Only the pause menu's buttons ask first; the game-over and versus countdowns and other callers go straight through
        public static bool IsPauseMenuButton(HudController hud, Button button)
        {
            return button != null
                && button.gameObject.activeInHierarchy
                && hud.pauseView != null
                && hud.pauseView.IsVisible;
        }
    }
}
