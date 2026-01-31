using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using System.Linq;
using Logger = Silk.Logger;

namespace SpiderSurge
{
    public class InputInterceptor : MonoBehaviour
    {
        private static Dictionary<PlayerInput, InputInterceptor> playerInterceptors = new Dictionary<PlayerInput, InputInterceptor>();

        private PlayerInput playerInput;
        private Dictionary<string, System.Action<InputAction.CallbackContext>> overriddenActions = new Dictionary<string, System.Action<InputAction.CallbackContext>>();
        private Dictionary<string, InputAction> customActions = new Dictionary<string, InputAction>();
        private Dictionary<string, BaseAbility> registeredAbilities = new Dictionary<string, BaseAbility>();

        private void Awake()
        {
            playerInput = GetComponentInParent<PlayerInput>();

            if (playerInput != null)
            {
                playerInterceptors[playerInput] = this;
            }
        }

        /// <summary>
        /// Register an ability with its activation button. This will override the original input binding.
        /// </summary>
        /// <param name="ability">The ability to register</param>
        /// <param name="bindingPath">The input binding path for this ability</param>
        public void RegisterAbility(BaseAbility ability, string bindingPath)
        {
            if (ability == null) return;

            try
            {
                string abilityName = ability.GetType().Name;

                if (string.IsNullOrEmpty(bindingPath))
                {
                    Logger.LogWarning($"No activation button defined for {abilityName}");
                    return;
                }

                // Store the ability for later reference
                registeredAbilities[bindingPath] = ability;

                // Map button to correct action name based on game's input mapping
                string actionName = GetActionNameFromBindingPath(bindingPath);
                if (string.IsNullOrEmpty(actionName))
                {
                    Logger.LogWarning($"No action mapping found for button {bindingPath} - ability {abilityName} will not be registered");
                    return;
                }

                // Override the input binding for this button
                OverrideInputBinding(actionName, bindingPath, (context) => OnAbilityButtonPressed(context, ability, bindingPath));
            }
            catch (System.Exception ex)
            {
                Logger.LogError($"Error registering ability: {ex.Message}");
            }
        }

        /// <summary>
        /// Maps a button binding path to the correct action name based on the game's input mapping
        /// </summary>
        /// <param name="bindingPath">The input binding path (e.g., "<Gamepad>/buttonSouth")</param>
        /// <returns>The corresponding action name, or null if no mapping found</returns>
        private string GetActionNameFromBindingPath(string bindingPath)
        {
            // Based on the game's SpiderInput.cs mapping

            switch (bindingPath.ToLower())
            {
                case "<gamepad>/buttonsouth":
                case "<keyboard>/space":
                case "<gamepad>/lefttrigger":
                case "<keyboard>/ctrl":
                    return "Jump";

                case "<gamepad>/buttonwest":
                case "<gamepad>/righttrigger":
                case "<mouse>/leftbutton":
                case "<keyboard>/f":
                    return "Fire";

                case "<gamepad>/buttoneast":
                case "<gamepad>/rightshoulder":
                case "<mouse>/rightbutton":
                    return "Equip";

                case "<gamepad>/leftshoulder":
                case "<keyboard>/q":
                    return "CustomAbility";

                default:
                    Logger.LogWarning($"Unknown binding path: {bindingPath}. No action mapping available.");
                    return null;
            }
        }

        /// <summary>
        /// Unregister an ability and restore original input binding if needed
        /// </summary>
        /// <param name="ability">The ability to unregister</param>
        /// <param name="bindingPath">The binding path to unregister</param>
        public void UnregisterAbility(BaseAbility ability, string bindingPath)
        {
            if (ability == null) return;

            try
            {
                string abilityName = ability.GetType().Name;

                if (registeredAbilities.ContainsKey(bindingPath))
                {
                    registeredAbilities.Remove(bindingPath);

                    // Get the correct action name for this binding
                    string actionName = GetActionNameFromBindingPath(bindingPath);
                    if (!string.IsNullOrEmpty(actionName))
                    {
                        // Clean up the custom action
                        string key = actionName + bindingPath;
                        if (customActions.ContainsKey(key))
                        {
                            var customAction = customActions[key];
                            if (customAction != null)
                            {
                                customAction.performed -= overriddenActions[key];
                                customAction.Disable();
                                customAction.Dispose();
                            }
                            customActions.Remove(key);
                            overriddenActions.Remove(key);
                        }
                    }

                }
            }
            catch (System.Exception ex)
            {
                Logger.LogError($"Error unregistering ability: {ex.Message}");
            }
        }

        private void OnAbilityButtonPressed(InputAction.CallbackContext context, BaseAbility ability, string bindingPath)
        {
            try
            {
                // Ensure this input comes from a device assigned to this player
                var device = context.control.device;
                bool deviceAssigned = false;
                foreach (var assignedDevice in playerInput.devices)
                {
                    if (assignedDevice == device)
                    {
                        deviceAssigned = true;
                        break;
                    }
                }
                if (!deviceAssigned)
                {
                    return;
                }

                if (ability != null)
                {
                    ability.Activate();
                }
            }
            catch (System.Exception ex)
            {
                Logger.LogError($"Error handling ability button press: {ex.Message}");
            }
        }


        /// <param name="actionName">Name of the action to override (e.g., "Jump") or "CustomAbility" for unmapped buttons</param>
        /// <param name="bindingPath">Input binding path (e.g., "<Gamepad>/buttonSouth")</param>
        /// <param name="callback">Function to call when the input is pressed</param>
        public void OverrideInputBinding(string actionName, string bindingPath, System.Action<InputAction.CallbackContext> callback)
        {
            try
            {
                // Always disable any original binding that matches this bindingPath, regardless of actionName
                var allActions = playerInput.actions;
                if (allActions != null)
                {
                    foreach (var action in allActions)
                    {
                        var bindings = action.bindings.ToList();
                        for (int i = 0; i < bindings.Count; i++)
                        {
                            var binding = bindings[i];
                            if (binding.effectivePath.ToLower().Contains(bindingPath.ToLower()) || binding.path.ToLower().Contains(bindingPath.ToLower()))
                            {
                                action.ChangeBinding(i).WithPath("");
                                break;
                            }
                        }
                    }
                }

                // Create new custom action
                var customAction = new InputAction(
                    name: actionName == "CustomAbility" ? $"CustomAbility{bindingPath.Replace("/", "").Replace("<", "").Replace(">", "")}" : $"Custom{actionName}",
                    type: InputActionType.Button,
                    binding: bindingPath
                );

                customAction.performed += callback;
                customAction.Enable();

                // Store the override and action for cleanup
                string key = actionName + bindingPath;
                overriddenActions[key] = callback;
                customActions[key] = customAction;

            }
            catch (System.Exception ex)
            {
                Logger.LogError($"Error overriding input binding: {ex.Message}");
            }
        }

        private void OnDestroy()
        {
            // Clean up any registered abilities
            foreach (var kvp in registeredAbilities)
            {
                if (kvp.Value != null)
                {
                    UnregisterAbility(kvp.Value, kvp.Key);
                }
            }
            registeredAbilities.Clear();

            // Clean up any other overridden actions
            foreach (var kvp in customActions)
            {
                if (kvp.Value != null)
                {
                    if (overriddenActions.ContainsKey(kvp.Key))
                    {
                        kvp.Value.performed -= overriddenActions[kvp.Key];
                    }
                    kvp.Value.Disable();
                    kvp.Value.Dispose();
                }
            }

            overriddenActions.Clear();
            customActions.Clear();

            if (playerInput != null && playerInterceptors.ContainsKey(playerInput))
            {
                playerInterceptors.Remove(playerInput);
            }
        }

        public static InputInterceptor GetPlayerInterceptor(PlayerInput playerInput)
        {
            playerInterceptors.TryGetValue(playerInput, out InputInterceptor interceptor);
            return interceptor;
        }
    }
}