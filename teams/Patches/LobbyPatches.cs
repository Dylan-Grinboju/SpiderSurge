using System;
using HarmonyLib;
using UnityEngine.SceneManagement;
using Logger = Silk.Logger;

namespace SpiderTeams
{
    [HarmonyPatch(typeof(LobbyController), "OnSceneLoaded")]
    public static class LobbyController_OnSceneLoaded_Patch
    {
        // Runs before the new lobby's LobbyScreen.Start, so the win snapshot is left for it to consume.
        [HarmonyPostfix]
        public static void Postfix(Scene scene)
        {
            try
            {
                OwnerRegistry.Clear();
                ExplosionOwners.Clear();
                DamageContext.Clear();
                SpawnScope.Clear();

                if (scene.name != "Lobby")
                {
                    return;
                }

                SurgeVersus.Stop();
                var manager = TeamManager.Instance;
                if (manager != null)
                {
                    manager.OnLobbyLoaded();
                }
                if (TeamVisuals.Instance != null)
                {
                    TeamVisuals.Instance.RestoreAll();
                }
                LobbyLabel.Refresh();
            }
            catch (Exception ex)
            {
                Logger.LogError($"SpiderTeams scene-load patch failed: {ex}");
            }
        }
    }

    [HarmonyPatch(typeof(LobbyController), nameof(LobbyController.RemovePlayer))]
    public static class LobbyController_RemovePlayer_Patch
    {
        [HarmonyPrefix]
        public static void Prefix(PlayerController player)
        {
            try
            {
                if (player == null)
                {
                    return;
                }

                if (TeamManager.Instance != null)
                {
                    TeamManager.Instance.Forget(player);
                }
                if (TeamVisuals.Instance != null)
                {
                    TeamVisuals.Instance.Forget(player);
                }
                if (TeamMarkers.Instance != null)
                {
                    TeamMarkers.Instance.Forget(player);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"SpiderTeams RemovePlayer patch failed: {ex}");
            }
        }
    }

    [HarmonyPatch(typeof(LobbyController), nameof(LobbyController.AddPlayer))]
    public static class LobbyController_AddPlayer_Patch
    {
        [HarmonyPostfix]
        public static void Postfix()
        {
            if (TeamManager.Instance != null)
            {
                TeamManager.Instance.MarkRosterChanged();
            }
        }
    }

    [HarmonyPatch(typeof(LobbyController), nameof(LobbyController.SpawnPlayers))]
    public static class LobbyController_SpawnPlayers_Patch
    {
        [HarmonyPrefix]
        public static bool Prefix(LobbyController __instance)
        {
            try
            {
                if (!ModConfig.GroupTeammates || !TeamManager.TeamMatchActive)
                {
                    return true;
                }
                return !SpawnGrouping.TrySpawn(__instance);
            }
            catch (Exception ex)
            {
                Logger.LogError($"SpiderTeams could not group spawns, using vanilla spawns: {ex}");
                return true;
            }
        }
    }
}
