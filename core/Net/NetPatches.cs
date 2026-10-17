using System;
using HarmonyLib;
using Unity.Netcode;
using Logger = Silk.Logger;

namespace SpiderSurge.Common
{
    [HarmonyPatch(typeof(NetworkManager), nameof(NetworkManager.StartHost))]
    public static class NetworkManager_StartHost_Patch
    {
        [HarmonyPostfix]
        public static void Postfix()
        {
            Net.Bind();
        }
    }

    [HarmonyPatch(typeof(NetworkManager), nameof(NetworkManager.StartClient))]
    public static class NetworkManager_StartClient_Patch
    {
        [HarmonyPostfix]
        public static void Postfix()
        {
            Net.Bind();
        }
    }

    // Public and friends-only lobbies default to a dedicated server, which runs no mods; only a P2P host can run SpiderSurge online
    [HarmonyPatch(typeof(HudController), "StartMultiplayerMatch")]
    public static class HudController_StartMultiplayerMatch_Patch
    {
        private static bool _warned;

        [HarmonyPrefix]
        public static void Prefix()
        {
            try
            {
                if (_warned || !GameSettings.LoadUseServer())
                {
                    return;
                }
                _warned = true;
                Logger.LogWarning("The lobby uses a dedicated server, so SpiderSurge's online features are off. Choose P2P in the lobby settings to play with them.");
                Announcer.InformationPopup("This lobby runs on a dedicated server, so SpiderSurge's Surge, Stats and Teams stay off online.\nChoose P2P in the lobby settings to play with them.");
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"SpiderSurge could not check the lobby type: {ex.Message}");
            }
        }
    }
}
