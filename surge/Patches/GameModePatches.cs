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

            StoragePersistenceManager.ClearAllStoredWeapons();
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

                    PerksManager.Instance?.ResetPerks();
                }

                StoragePersistenceManager.ClearAllStoredWeapons();

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
        private static string GetSurvivalModeDisplayName()
        {
            return ModConfig.enableSurgeMode ? "Surge Survival" : "Wave Survival";
        }

        public static void UpdateSurgeSurvivalText()
        {
            const string path = "Level/SurvivalStartPlatform/Text/Survival Mode Text/ModeText";
            var modeTextObj = GameObject.Find(path) ?? GameObject.Find("ModeText");
            if (modeTextObj != null)
            {
                UpdateSurgeSurvivalText(modeTextObj);
            }
        }

        public static void UpdateSurgeSurvivalText(GameObject root)
        {
            if (root == null)
            {
                return;
            }

            var textElements = root.GetComponentsInChildren<TMP_Text>(true);
            foreach (var tmp in textElements)
            {
                if (tmp == null || string.IsNullOrWhiteSpace(tmp.text))
                {
                    continue;
                }

                if (string.Equals(tmp.text, "Wave Survival", System.StringComparison.OrdinalIgnoreCase)
                    || string.Equals(tmp.text, "Waves Survival", System.StringComparison.OrdinalIgnoreCase)
                    || string.Equals(tmp.text, "Surge Survival", System.StringComparison.OrdinalIgnoreCase))
                {
                    tmp.text = GetSurvivalModeDisplayName();
                }
            }
        }
    }
}