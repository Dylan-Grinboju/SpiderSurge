using HarmonyLib;
using Doozy.Engine.UI;
using Unity.Netcode;

namespace SpiderSurge
{
    [HarmonyPatch(typeof(HudController), "Restart")]
    public class HudController_Restart_Patch
    {
        [HarmonyPrefix]
        public static bool Prefix(HudController __instance)
        {
            if (!NetworkManager.Singleton.IsHost)
            {
                return true;
            }

            UIButton component = null;
            if (__instance.restart != null && __instance.restart.gameObject.activeInHierarchy)
            {
                component = __instance.restart.GetComponent<UIButton>();
            }

            Announcer.ConfirmationPopup("Are you sure?", component, delegate
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
            UIButton component = null;
            if (__instance.mainMenuButton != null && __instance.mainMenuButton.gameObject.activeInHierarchy)
            {
                component = __instance.mainMenuButton.GetComponent<UIButton>();
            }

            Announcer.ConfirmationPopup("Are you sure?", component, delegate
            {
                __instance.OpenLobby(false);
            }, null, null, true, false);

            return false;
        }
    }
}
