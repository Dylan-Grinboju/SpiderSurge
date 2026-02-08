using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using System.Linq;
using Logger = Silk.Logger;

namespace SpiderSurge
{
    public class InputInterceptor : MonoBehaviour
    {
        private static readonly Dictionary<PlayerInput, InputInterceptor> playerInterceptors = new Dictionary<PlayerInput, InputInterceptor>();

        private PlayerInput playerInput;
        private readonly Dictionary<string, System.Action<InputAction.CallbackContext>> overriddenActions = new Dictionary<string, System.Action<InputAction.CallbackContext>>();
        private readonly Dictionary<string, InputAction> customActions = new Dictionary<string, InputAction>();
        private readonly Dictionary<string, BaseAbility> registeredAbilities = new Dictionary<string, BaseAbility>();
        public static IEnumerable<PlayerInput> ActivePlayerInputs => playerInterceptors.Keys.Where(p => p != null);

        // Store original bindings to restore them later
        private struct BindingRestoreInfo
        {
            public InputAction Action;
            public int BindingIndex;
            public string OriginalPath;
        }
        private readonly Dictionary<string, List<BindingRestoreInfo>> restoredBindings = new Dictionary<string, List<BindingRestoreInfo>>();
        private InputActionAsset _originalActions;

        private InputActionAsset _instantiatedActions;

        private void Awake()
        {
            playerInput = GetComponentInParent<PlayerInput>();

            if (playerInput != null)
            {
                playerInterceptors[playerInput] = this;
            }
        }

        private void Start()
        {
            if (playerInput != null && playerInput.actions != null && _instantiatedActions == null)
            {
                // Capture current state before swapping
                var currentScheme = playerInput.currentControlScheme;
                var currentDevices = playerInput.devices.ToArray();

                // Store original actions to restore later
                _originalActions = playerInput.actions;

                // Create a clean instance of the actions for this player
                _instantiatedActions = Instantiate(playerInput.actions);
                playerInput.actions = _instantiatedActions;

                // Restoring the scheme re-binds the devices to the new action asset
                // This is critical because swapping the asset clears the device pairing
                if (!string.IsNullOrEmpty(currentScheme) && currentDevices.Length > 0)
                {
                    try
                    {
                        playerInput.SwitchCurrentControlScheme(currentScheme, currentDevices);
                    }
                    catch (System.Exception ex)
                    {
                        Logger.LogError($"[InputInterceptor] Failed to restore control scheme '{currentScheme}': {ex.Message}");
                    }
                }
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
                OverrideInputBinding(actionName, bindingPath, (context) => OnAbilityButtonPressed(context, ability));
            }
            catch (System.Exception ex)
            {
                Logger.LogError($"Error registering ability: {ex.Message}");
            }
        }

        /// <summary>
        /// Maps a button binding path to the correct action name based on the game's input mapping.
        /// Any button used for ability activation is treated as CustomAbility.
        /// </summary>
        /// <param name="bindingPath">The input binding path (e.g., "<Gamepad>/buttonSouth")</param>
        /// <returns>The corresponding action name, or null if no mapping found</returns>
        private string GetActionNameFromBindingPath(string bindingPath)
        {
            string lowerPath = bindingPath.ToLower();

            // All buttons that can be used as ability buttons are treated as CustomAbility
            // This allows players to remap their ability to any button
            switch (lowerPath)
            {
                // Keyboard ability buttons
                case "<keyboard>/q":
                    return "CustomAbility";

                // All gamepad buttons that can be used for abilities
                case "<gamepad>/leftshoulder":    // L1 (default ability)
                case "<gamepad>/lefttrigger":     // L2
                case "<gamepad>/rightshoulder":   // R1
                case "<gamepad>/righttrigger":    // R2
                case "<gamepad>/buttonnorth":     // Y/Triangle
                case "<gamepad>/buttonsouth":     // A/Cross
                case "<gamepad>/buttoneast":      // B/Circle
                case "<gamepad>/buttonwest":      // X/Square
                case "<gamepad>/dpad/up":
                case "<gamepad>/dpad/down":
                case "<gamepad>/dpad/left":
                case "<gamepad>/dpad/right":
                case "<gamepad>/leftstickpress":  // L3
                case "<gamepad>/rightstickpress": // R3
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

                    // Restore original bindings if any were overridden
                    if (restoredBindings.ContainsKey(bindingPath))
                    {
                        var backups = restoredBindings[bindingPath];
                        foreach (var backup in backups)
                        {
                            try
                            {
                                // Restore the original path
                                backup.Action.ChangeBinding(backup.BindingIndex).WithPath(backup.OriginalPath);
                            }
                            catch (System.Exception restoreEx)
                            {
                                Logger.LogError($"Failed to restore binding for {backup.Action.name}: {restoreEx.Message}");
                            }
                        }
                        restoredBindings.Remove(bindingPath);
                    }

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

        private void OnAbilityButtonPressed(InputAction.CallbackContext context, BaseAbility ability)
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

                ability?.Activate();
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
                    // Prepare list to store restore info for this binding path
                    if (!restoredBindings.ContainsKey(bindingPath))
                    {
                        restoredBindings[bindingPath] = new List<BindingRestoreInfo>();
                    }

                    foreach (var action in allActions)
                    {
                        var bindings = action.bindings;
                        for (int i = 0; i < bindings.Count; i++)
                        {
                            var binding = bindings[i];
                            string effectivePath = binding.effectivePath ?? "";
                            string path = binding.path ?? "";

                            // Check if this binding matches the key we are overriding
                            if (effectivePath.ToLower().Contains(bindingPath.ToLower()) || path.ToLower().Contains(bindingPath.ToLower()))
                            {
                                // Only backup if we haven't already backed it up (avoid backing up empty string if called twice)
                                if (!string.IsNullOrEmpty(path))
                                {
                                    restoredBindings[bindingPath].Add(new BindingRestoreInfo
                                    {
                                        Action = action,
                                        BindingIndex = i,
                                        OriginalPath = path
                                    });

                                    action.ChangeBinding(i).WithPath("");
                                }
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
            var abilitiesSnapshot = registeredAbilities.ToList();
            foreach (var kvp in abilitiesSnapshot)
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

           restoredBindings.Clear();

            overriddenActions.Clear();
            customActions.Clear();

            if (playerInput != null && playerInterceptors.ContainsKey(playerInput))
            {
                playerInterceptors.Remove(playerInput);
            }

            if (playerInput != null && _originalActions != null)
            {
                var currentScheme = playerInput.currentControlScheme;
                var currentDevices = playerInput.devices.ToArray();

                playerInput.actions = _originalActions;

                if (!string.IsNullOrEmpty(currentScheme) && currentDevices.Length > 0)
                {
                    try
                    {
                        playerInput.SwitchCurrentControlScheme(currentScheme, currentDevices);
                    }
                    catch { }
                }
            }

            if (_instantiatedActions != null)
            {
                Destroy(_instantiatedActions);
                _instantiatedActions = null;
            }
        }

        public static InputInterceptor GetPlayerInterceptor(PlayerInput playerInput)
        {
            playerInterceptors.TryGetValue(playerInput, out InputInterceptor interceptor);
            return interceptor;
        }
    }
}