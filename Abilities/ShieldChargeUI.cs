using UnityEngine;
using UnityEngine.InputSystem;
using System.Linq;
using Logger = Silk.Logger;

namespace SpiderSurge
{
    public class ShieldChargeUI : MonoBehaviour
    {
        private PlayerInput localPlayerInput;
        private bool shouldShow = false;

        private void Start()
        {
            // Find the local player
            FindLocalPlayer();
        }

        private void Update()
        {
            // Check if we need to find the local player again (in case of respawn or scene change)
            if (localPlayerInput == null)
            {
                FindLocalPlayer();
            }

            // Update visibility
            UpdateVisibility();
        }

        private void UpdateVisibility()
        {
            bool newShouldShow = SurgeGameModeManager.Instance != null &&
                                 SurgeGameModeManager.Instance.IsActive &&
                                 SurgeGameModeManager.Instance.IsShieldAbilityUnlocked &&
                                 localPlayerInput != null;

            if (newShouldShow != shouldShow)
            {
                shouldShow = newShouldShow;
                Logger.LogInfo($"ShieldChargeUI visibility: {shouldShow}");
            }
        }

        private void FindLocalPlayer()
        {
            try
            {
                var controllers = LobbyController.instance?.GetPlayerControllers();
                if (controllers != null)
                {
                    var localController = controllers.FirstOrDefault(c => c != null && c.isLocalPlayer);
                    if (localController != null)
                    {
                        // Get PlayerInput using the playerInputIndex
                        var playerInputIndex = localController.playerInputIndex.Value;
                        localPlayerInput = PlayerInput.GetPlayerByIndex(playerInputIndex);
                        Logger.LogInfo($"ShieldChargeUI found local player: {localPlayerInput?.playerIndex ?? -1}");
                    }
                }
            }
            catch (System.Exception ex)
            {
                Logger.LogError($"Error finding local player in ShieldChargeUI: {ex.Message}");
            }
        }

        private void OnGUI()
        {
            if (!shouldShow) return;

            int currentCharges = SurgeGameModeManager.Instance.GetShieldCharges(localPlayerInput);

            // Position in bottom right corner
            float width = 100f;
            float height = 50f;
            float x = Screen.width - width - 20;
            float y = Screen.height - height - 20;

            GUI.color = Color.white;
            GUI.Label(new Rect(x, y, width, height), $"Shield: {currentCharges}",
                new GUIStyle(GUI.skin.label) { fontSize = 20, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold });
        }
    }
}