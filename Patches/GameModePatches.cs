using HarmonyLib;
using Logger = Silk.Logger;
using UnityEngine.SceneManagement;

namespace SpiderSurge
{
    [HarmonyPatch(typeof(SurvivalMode), "StopGameMode")]
    public class SurvivalMode_StopGameMode_Patch
    {
        [HarmonyPostfix]
        public static void Postfix()
        {
            if (SurgeGameModeManager.Instance.IsActive)
            {
                Logger.LogInfo("Surge mode ended");
                SurgeGameModeManager.Instance.SetActive(false);
                PerksManager.Instance.ResetPerks();
            }
        }
    }

    [HarmonyPatch(typeof(LobbyController), "OnSceneLoaded")]
    public class LobbyController_OnSceneLoaded_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(Scene scene)
        {
            if (scene.name == "Lobby" && SurgeGameModeManager.Instance.IsActive)
            {
                Logger.LogInfo("Surge mode ended (lobby entered)");
                SurgeGameModeManager.Instance.SetActive(false);
                PerksManager.Instance.ResetPerks();
            }
        }
    }
}