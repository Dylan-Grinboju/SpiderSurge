using UnityEngine;
using UnityEngine.InputSystem;
using HarmonyLib;
using System.Collections.Generic;
using System.Linq;
using SpiderSurge.Abilities;
using Logger = Silk.Logger;

namespace SpiderSurge
{
    public class InputInterceptor : MonoBehaviour
    {
        public static Dictionary<PlayerInput, InputInterceptor> playerInterceptors = new Dictionary<PlayerInput, InputInterceptor>();

        private PlayerInput playerInput;
        private InputAction customSouthButtonAction;
        private Dictionary<string, System.Action<InputAction.CallbackContext>> overriddenActions = new Dictionary<string, System.Action<InputAction.CallbackContext>>();
        private Dictionary<string, InputAction> customActions = new Dictionary<string, InputAction>();

        private void Awake()
        {
            playerInput = GetComponentInParent<PlayerInput>();

            if (playerInput != null)
            {
                playerInterceptors[playerInput] = this;
                Logger.LogInfo($"InputInterceptor initialized for player {playerInput.playerIndex}");

                // Disable original south button jump binding and set up custom action
                Invoke(nameof(SetupInputOverrides), 0.1f);
            }
        }

        private void SetupInputOverrides()
        {
            try
            {
                // Disable the south button binding for the Jump action
                DisableOriginalJumpBinding();

                // Create a new input action specifically for the south button
                customSouthButtonAction = new InputAction(
                    name: "CustomSouthButton",
                    type: InputActionType.Button,
                    binding: "<Gamepad>/buttonSouth"
                );

                // Subscribe to the action
                customSouthButtonAction.performed += OnCustomSouthButtonPressed;
                customSouthButtonAction.Enable();

                Logger.LogInfo($"Input overrides setup for player {playerInput.playerIndex} - South button now activates shield");
            }
            catch (System.Exception ex)
            {
                Logger.LogError($"Error setting up input overrides: {ex.Message}");
            }
        }

        private void DisableOriginalJumpBinding()
        {
            try
            {
                // Find the Jump action in the player's input actions
                var jumpAction = playerInput.actions?.FindAction("Jump");
                if (jumpAction == null)
                {
                    Logger.LogWarning($"Could not find Jump action for player {playerInput.playerIndex}");
                    return;
                }

                // Get all bindings for the jump action
                var bindings = jumpAction.bindings.ToList();

                // Find and disable the south button binding
                for (int i = 0; i < bindings.Count; i++)
                {
                    var binding = bindings[i];
                    if (binding.effectivePath.Contains("buttonSouth") || binding.path.Contains("buttonSouth"))
                    {
                        // Disable this binding by setting it to an empty path
                        jumpAction.ChangeBinding(i).WithPath("");
                        Logger.LogInfo($"Disabled south button binding for Jump action on player {playerInput.playerIndex}");
                        break;
                    }
                }
            }
            catch (System.Exception ex)
            {
                Logger.LogError($"Error disabling original jump binding: {ex.Message}");
            }
        }

        private void OnCustomSouthButtonPressed(InputAction.CallbackContext context)
        {
            try
            {
                Logger.LogInfo($"South button (A) pressed by player {playerInput.playerIndex} - activating TempShield");

                TempShield shield = TempShield.GetPlayerShield(playerInput);
                if (shield != null)
                {
                    shield.ActivateShield();
                }
                else
                {
                    Logger.LogWarning($"Could not find TempShield for player {playerInput.playerIndex}");
                }
            }
            catch (System.Exception ex)
            {
                Logger.LogError($"Error handling custom south button press: {ex.Message}");
            }
        }

        /// <summary>
        /// Override any input binding with a custom action. This completely replaces the original functionality.
        /// </summary>
        /// <param name="actionName">Name of the action to override (e.g., "Jump")</param>
        /// <param name="bindingPath">Input binding path (e.g., "<Gamepad>/buttonSouth")</param>
        /// <param name="callback">Function to call when the input is pressed</param>
        public void OverrideInputBinding(string actionName, string bindingPath, System.Action<InputAction.CallbackContext> callback)
        {
            try
            {
                // Disable the original binding
                var originalAction = playerInput.actions?.FindAction(actionName);
                if (originalAction != null)
                {
                    var bindings = originalAction.bindings.ToList();
                    for (int i = 0; i < bindings.Count; i++)
                    {
                        var binding = bindings[i];
                        if (binding.effectivePath.Contains(bindingPath) || binding.path.Contains(bindingPath))
                        {
                            originalAction.ChangeBinding(i).WithPath("");
                            Logger.LogInfo($"Disabled original {actionName} binding for {bindingPath} on player {playerInput.playerIndex}");
                            break;
                        }
                    }
                }

                // Create new custom action
                var customAction = new InputAction(
                    name: $"Custom{actionName}",
                    type: InputActionType.Button,
                    binding: bindingPath
                );

                customAction.performed += callback;
                customAction.Enable();

                // Store the override and action for cleanup
                string key = actionName + bindingPath;
                overriddenActions[key] = callback;
                customActions[key] = customAction;

                Logger.LogInfo($"Override setup for {actionName} on {bindingPath} for player {playerInput.playerIndex}");
            }
            catch (System.Exception ex)
            {
                Logger.LogError($"Error overriding input binding: {ex.Message}");
            }
        }

        /// <summary>
        /// Example method showing how to override other buttons.
        /// Call this from other ability scripts to set up custom button mappings.
        /// </summary>
        public void SetupExampleOverrides()
        {
            // Example 1: Override East button (B/Circle) to do something else
            // OverrideInputBinding("Jump", "<Gamepad>/buttonEast", OnCustomEastButtonPressed);

            // Example 2: Override West button (X/Square) for another ability
            // OverrideInputBinding("Jump", "<Gamepad>/buttonWest", OnCustomWestButtonPressed);

            // Example 3: Override North button (Y/Triangle) for yet another ability
            // OverrideInputBinding("Jump", "<Gamepad>/buttonNorth", OnCustomNorthButtonPressed);
        }

        // Example callback methods for other buttons:
        /*
        private void OnCustomEastButtonPressed(InputAction.CallbackContext context)
        {
            Logger.LogInfo($"East button (B) pressed by player {playerInput.playerIndex} - custom action");
            // Add your custom functionality here
        }

        private void OnCustomWestButtonPressed(InputAction.CallbackContext context)
        {
            Logger.LogInfo($"West button (X) pressed by player {playerInput.playerIndex} - custom action");
            // Add your custom functionality here
        }

        private void OnCustomNorthButtonPressed(InputAction.CallbackContext context)
        {
            Logger.LogInfo($"North button (Y) pressed by player {playerInput.playerIndex} - custom action");
            // Add your custom functionality here
        }
        */

        private void OnDestroy()
        {
            if (customSouthButtonAction != null)
            {
                customSouthButtonAction.performed -= OnCustomSouthButtonPressed;
                customSouthButtonAction.Disable();
                customSouthButtonAction.Dispose();
            }

            // Clean up any other overridden actions
            foreach (var kvp in customActions)
            {
                if (kvp.Value != null)
                {
                    kvp.Value.performed -= overriddenActions[kvp.Key];
                    kvp.Value.Disable();
                    kvp.Value.Dispose();
                }
            }

            overriddenActions.Clear();
            customActions.Clear();

            if (playerInput != null && playerInterceptors.ContainsKey(playerInput))
            {
                playerInterceptors.Remove(playerInput);
                Logger.LogInfo($"InputInterceptor removed for player {playerInput.playerIndex}");
            }
        }

        public static InputInterceptor GetPlayerInterceptor(PlayerInput playerInput)
        {
            playerInterceptors.TryGetValue(playerInput, out InputInterceptor interceptor);
            return interceptor;
        }
    }
}
