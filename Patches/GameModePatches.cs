using HarmonyLib;
using Logger = Silk.Logger;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;

namespace SpiderSurge
{
    [HarmonyPatch(typeof(SurvivalMode), "StopGameMode")]
    public class SurvivalMode_StopGameMode_Patch
    {
        [HarmonyPostfix]
        public static void Postfix()
        {
            if (SurgeGameModeManager.Instance != null && SurgeGameModeManager.Instance.IsActive)
            {
                SurgeGameModeManager.Instance.SetActive(false);

                if (PerksManager.Instance != null)
                {
                    PerksManager.Instance.ResetPerks();
                }
            }
        }
    }

    [HarmonyPatch(typeof(LobbyController), "OnSceneLoaded")]
    public class LobbyController_OnSceneLoaded_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(Scene scene)
        {
            if (scene.name == "Lobby")
            {
                if (SurgeGameModeManager.Instance != null && SurgeGameModeManager.Instance.IsActive)
                {
                    SurgeGameModeManager.Instance.SetActive(false);
                    PerksManager.Instance.ResetPerks();
                }

                GameModePatches.UpdateSurgeSurvivalText();
            }
        }
    }

    [HarmonyPatch(typeof(LobbyController), "Start")]
    public class LobbyController_Start_Patch
    {
        [HarmonyPostfix]
        public static void Postfix()
        {
            GameModePatches.UpdateSurgeSurvivalText();
        }
    }

    public static class GameModePatches
    {
        public static void UpdateSurgeSurvivalText()
        {
            if (!ModConfig.enableSurgeMode) return;

            const string path = "Level/SurvivalStartPlatform/Text/Survival Mode Text/ModeText";
            var modeTextObj = GameObject.Find(path) ?? GameObject.Find("ModeText");
            if (modeTextObj != null)
            {
                var tmp = modeTextObj.GetComponent<TMP_Text>();
                if (tmp != null)
                {
                    tmp.text = "Surge Survival";
                }
            }
        }
    }
}