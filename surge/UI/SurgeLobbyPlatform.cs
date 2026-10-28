using HarmonyLib;
using I2.Loc;
using TMPro;
using SpiderSurge.Common;
using UnityEngine;
using Logger = Silk.Logger;

namespace SpiderSurge
{
    public sealed class SurgeLobbyPlatform : MonoBehaviour
    {
        private const string LabelPath = "Text/Survival Mode Text/ModeText";

        private static GameObject _platform;

        // A copy of the Survival platform; its label's Localize is removed so I2 never puts "Wave Survival" back
        internal static void EnsureCreated()
        {
            if (_platform != null)
            {
                return;
            }

            GameObject survival = GameObject.Find("Level/SurvivalStartPlatform");
            if (survival == null || survival.scene.name != "Lobby")
            {
                return;
            }

            GameObject platform = Instantiate(survival, survival.transform.parent);
            Transform labelObject = platform.transform.Find(LabelPath);
            TMP_Text label = labelObject != null ? labelObject.GetComponent<TMP_Text>() : null;
            if (label == null)
            {
                Logger.LogWarning($"The Survival platform has no {LabelPath}, so the Surge Survival platform was not added");
                platform.SetActive(false);
                Destroy(platform);
                return;
            }

            Localize localize = label.GetComponent<Localize>();
            if (localize != null)
            {
                Destroy(localize);
            }
            label.text = SurgeGameModeManager.DisplayName;
            platform.name = "SurgeStartPlatform";
            platform.transform.position = survival.transform.position + new Vector3(-110f, 85f, 0f);
            platform.AddComponent<SurgeLobbyPlatform>();
            _platform = platform;
        }

        internal static void Remove()
        {
            if (_platform != null)
            {
                _platform.SetActive(false);
                Destroy(_platform);
                _platform = null;
            }
        }
    }

    [HarmonyPatch(typeof(GameModeStartButton), "ShowGameModePrompt")]
    public static class GameModeStartButton_ShowGameModePrompt_Patch
    {
        [HarmonyPrefix]
        public static void Prefix(GameModeStartButton __instance)
        {
            if (GameController.instance.gamePaused || !LobbyController.inLobby
                || Net.IsRemoteClient)
            {
                return;
            }

            SurgeGameModeManager.SelectMode(__instance.targetMode == GameModeStartButton.Mode.Survival
                && __instance.GetComponentInParent<SurgeLobbyPlatform>() != null);
        }
    }
}
