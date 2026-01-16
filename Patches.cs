using HarmonyLib;
using Silk;
using Logger = Silk.Logger;
using UnityEngine.SceneManagement;

namespace SpiderSurge
{
    [HarmonyPatch(typeof(SurvivalMode), "StartGame")]
    public class SurvivalMode_StartGame_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(bool __result, SurvivalConfig survivalConfig)
        {
            if (__result && ModConfig.enableSurgeMode)
            {
                SurgeModeManager.Instance.SetActive(true);
                Logger.LogInfo("Surge mode started");
            }
        }
    }

    [HarmonyPatch(typeof(SurvivalMode), "StopGameMode")]
    public class SurvivalMode_StopGameMode_Patch
    {
        [HarmonyPostfix]
        public static void Postfix()
        {
            if (SurgeModeManager.Instance.IsActive)
            {
                Logger.LogInfo("Surge mode ended");
                SurgeModeManager.Instance.SetActive(false);
            }
        }
    }

    [HarmonyPatch(typeof(LobbyController), "OnSceneLoaded")]
    public class LobbyController_OnSceneLoaded_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(Scene scene)
        {
            if (scene.name == "Lobby" && SurgeModeManager.Instance.IsActive)
            {
                Logger.LogInfo("Surge mode ended (lobby entered)");
                SurgeModeManager.Instance.SetActive(false);
            }
        }
    }
}