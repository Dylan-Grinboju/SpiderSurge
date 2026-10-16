using HarmonyLib;
using I2.Loc;
using TMPro;
using Unity.Netcode;
using UnityEngine;

namespace SpiderSurge
{
    public sealed class SurgeLobbyPlatform : MonoBehaviour
    {
        private static GameObject _platform;
        private TMP_Text _label;

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

            _platform = Instantiate(survival, survival.transform.parent);
            _platform.name = "SurgeStartPlatform";
            _platform.transform.position = survival.transform.position + new Vector3(-110f, 85f, 0f);
            var marker = _platform.AddComponent<SurgeLobbyPlatform>();
            marker._label = _platform.transform.Find("Text/Survival Mode Text/ModeText").GetComponent<TMP_Text>();
            Localize localize = marker._label.GetComponent<Localize>();
            if (localize != null)
            {
                localize.enabled = false;
            }
            marker.LateUpdate();
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

        private void LateUpdate()
        {
            if (_label != null && _label.text != "SURGE SURVIVAL")
            {
                _label.text = "SURGE SURVIVAL";
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
                || (NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsHost))
            {
                return;
            }

            SurgeGameModeManager.SelectMode(__instance.targetMode == GameModeStartButton.Mode.Survival
                && __instance.GetComponentInParent<SurgeLobbyPlatform>() != null);
        }
    }
}
