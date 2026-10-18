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
        [HarmonyPrefix]
        public static void Prefix(NetworkManager __instance)
        {
            Net.PrepareConnect(__instance);
        }

        [HarmonyPostfix]
        public static void Postfix()
        {
            Net.Bind();
        }
    }

    // Netcode 1.0.0-pre makes a joining client an observer of every object and syncs them all, whatever CheckObjectVisibility says
    [HarmonyPatch(typeof(NetworkSpawnManager), "UpdateObservedNetworkObjects")]
    public static class NetworkSpawnManager_UpdateObservedNetworkObjects_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(NetworkSpawnManager __instance, ulong clientId)
        {
            try
            {
                foreach (NetworkObject networkObject in __instance.SpawnedObjectsList)
                {
                    if (networkObject.CheckObjectVisibility != null && !networkObject.CheckObjectVisibility(clientId))
                    {
                        networkObject.Observers.Remove(clientId);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"SpiderSurge could not apply object visibility for client {clientId}: {ex.Message}");
            }
        }
    }

    [HarmonyPatch(typeof(SceneEventData), "AddSpawnedNetworkObjects")]
    public static class SceneEventData_AddSpawnedNetworkObjects_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(SceneEventData __instance)
        {
            try
            {
                ulong target = __instance.TargetClientId;
                __instance.m_NetworkObjectsSync.RemoveAll(networkObject => networkObject != null && !networkObject.Observers.Contains(target));
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"SpiderSurge could not filter the join sync: {ex.Message}");
            }
        }
    }

    [HarmonyPatch(typeof(LobbyController), "ConnectionApprovalCallback")]
    public static class LobbyController_ConnectionApprovalCallback_Patch
    {
        [HarmonyPrefix]
        public static void Prefix(byte[] connectionData, ulong clientId)
        {
            try
            {
                Net.OnApproval(connectionData, clientId);
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"SpiderSurge could not read the connection data: {ex.Message}");
            }
        }
    }

    // Public and friends-only lobbies default to a dedicated server, which runs no mods; only a P2P host can run SpiderSurge online
    [HarmonyPatch(typeof(HudController), "StartMultiplayerMatch")]
    public static class HudController_StartMultiplayerMatch_Patch
    {
        private static bool _warned;

        [HarmonyPrefix]
        public static void Prefix(LobbyType lobby)
        {
            try
            {
                if (_warned || lobby == LobbyType.Private || !GameSettings.LoadUseServer())
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
