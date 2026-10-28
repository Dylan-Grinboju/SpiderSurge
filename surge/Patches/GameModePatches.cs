using HarmonyLib;
using I2.Loc;
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
            SurgeGameModeManager.EndRun(RunEnd.Stopped);
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
                SurgeGameModeManager.EndRun(RunEnd.Lobby);
                SurgeLobbyPlatform.EnsureCreated();
            }
        }
    }

    [HarmonyPatch(typeof(LobbyController), "Start")]
    public class LobbyController_Start_Patch
    {
        [HarmonyPostfix]
        public static void Postfix()
        {
            SurgeLobbyPlatform.EnsureCreated();
        }
    }

    [HarmonyPatch(typeof(LobbyController), "OnPlayerLeft", new[] { typeof(UnityEngine.InputSystem.PlayerInput) })]
    public class LobbyController_OnPlayerLeft_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(UnityEngine.InputSystem.PlayerInput playerInput)
        {
            if (playerInput != null)
            {
                PlayerControlSettings.ForgetPlayer(playerInput.playerIndex);
            }
        }
    }

    [HarmonyPatch(typeof(PainLevelsScreen), "RefreshScreen")]
    public class PainLevelsScreen_RefreshScreen_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(PainLevelsScreen __instance)
        {
            if (__instance != null)
            {
                GameModePatches.UpdateSurgeSurvivalText(__instance.gameObject);
            }
        }
    }

    [HarmonyPatch(typeof(HudController), "ShowSurvivalStartPrompt")]
    public class HudController_ShowSurvivalStartPrompt_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(HudController __instance)
        {
            if (__instance != null)
            {
                GameModePatches.UpdateSurgeSurvivalText(__instance.gameObject);
            }
        }
    }

    public static class GameModePatches
    {
        public static void UpdateSurgeSurvivalText(GameObject root)
        {
            if (root == null)
            {
                return;
            }

            foreach (var localize in root.GetComponentsInChildren<Localize>(true))
            {
                if (localize.mTerm != "Wave/wave_survival_mode")
                {
                    continue;
                }
                var tmp = localize.GetComponent<TMP_Text>();
                if (tmp != null && SurgeGameModeManager.IsModeEnabled)
                {
                    tmp.text = SurgeGameModeManager.DisplayName;
                }
                else
                {
                    localize.OnLocalize(true);
                }
            }
        }
    }
}
