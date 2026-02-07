using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using Logger = Silk.Logger;

namespace SpiderSurge
{
    /// <summary>
    /// Manages per-player control settings for ability activation.
    /// Allows each player to customize which button activates their ability.
    /// 
    /// Controls:
    /// - Hold Menu button + Double-tap any button = Set that button as ability activation
    /// - Hold Menu button for 3 seconds = Reset to default (L1)
    /// </summary>
    public class PlayerControlSettings : MonoBehaviour
    {
        public static PlayerControlSettings Instance { get; private set; }

        // Default ability button path
        public const string DefaultGamepadButton = "<Gamepad>/leftshoulder";

        // Per-player button mapping (playerIndex -> button path)
        private static readonly Dictionary<int, string> playerAbilityButtons = new Dictionary<int, string>();

        // Track menu button hold state per player
        private readonly Dictionary<int, float> menuButtonHoldStartTime = new Dictionary<int, float>();
        private readonly Dictionary<int, bool> menuButtonHeld = new Dictionary<int, bool>();

        // Track button presses for double-tap detection
        private readonly Dictionary<int, string> lastButtonPressed = new Dictionary<int, string>();
        private readonly Dictionary<int, float> lastButtonPressTime = new Dictionary<int, float>();

        // Visual feedback tracking
        private readonly Dictionary<int, string> feedbackMessages = new Dictionary<int, string>();
        private readonly Dictionary<int, float> feedbackEndTimes = new Dictionary<int, float>();

        private const float DoubleTapWindow = 0.4f;
        private const float ResetHoldDuration = 3f;
        private const float FeedbackDisplayDuration = 2f;

        // All supported gamepad buttons for ability mapping
        private static readonly string[] SupportedButtons = new string[]
        {
            "<Gamepad>/leftshoulder",
            "<Gamepad>/lefttrigger",
            "<Gamepad>/rightshoulder",
            "<Gamepad>/righttrigger",
            "<Gamepad>/buttonNorth",
            "<Gamepad>/buttonSouth",
            "<Gamepad>/buttonEast",
            "<Gamepad>/buttonWest",
            "<Gamepad>/dpad/up",
            "<Gamepad>/dpad/down",
            "<Gamepad>/dpad/left",
            "<Gamepad>/dpad/right",
            "<Gamepad>/leftStickPress",
            "<Gamepad>/rightStickPress"
        };

        // Input actions for detection
        private InputAction menuButtonAction;
        private readonly Dictionary<string, InputAction> buttonActions = new Dictionary<string, InputAction>();

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                SetupInputActions();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void SetupInputActions()
        {
            Logger.LogInfo("[PlayerControlSettings] Setting up input actions...");

            // Menu/Start button for holding
            menuButtonAction = new InputAction(
                name: "MenuButton",
                type: InputActionType.Button
            );
            menuButtonAction.AddBinding("<Gamepad>/start");
            menuButtonAction.AddBinding("<Gamepad>/select");
            menuButtonAction.performed += OnMenuButtonPressed;
            menuButtonAction.canceled += OnMenuButtonReleased;
            menuButtonAction.Enable();

            Logger.LogInfo("[PlayerControlSettings] Menu button action enabled");

            // Create actions for all supported buttons
            foreach (var buttonPath in SupportedButtons)
            {
                string actionName = $"ControlSwitch_{buttonPath.Replace("/", "_").Replace("<", "").Replace(">", "")}";
                var action = new InputAction(
                    name: actionName,
                    type: InputActionType.Button,
                    binding: buttonPath
                );
                action.performed += ctx => OnButtonPressed(ctx, buttonPath);
                action.Enable();
                buttonActions[buttonPath] = action;
            }

        }

        private void OnMenuButtonPressed(InputAction.CallbackContext context)
        {

            int playerIndex = GetPlayerIndexFromDevice(context.control.device);
            if (playerIndex < 0)
            {
                Logger.LogWarning($"[PlayerControlSettings] Could not find player for device: {context.control.device.name}");
                return;
            }

            menuButtonHeld[playerIndex] = true;
            menuButtonHoldStartTime[playerIndex] = Time.time;
        }

        private void OnMenuButtonReleased(InputAction.CallbackContext context)
        {
            int playerIndex = GetPlayerIndexFromDevice(context.control.device);
            if (playerIndex < 0) return;

            menuButtonHeld[playerIndex] = false;
            menuButtonHoldStartTime.Remove(playerIndex);
        }

        private void OnButtonPressed(InputAction.CallbackContext context, string buttonPath)
        {
            int playerIndex = GetPlayerIndexFromDevice(context.control.device);
            if (playerIndex < 0) return;

            // Only process if menu button is held
            if (!menuButtonHeld.TryGetValue(playerIndex, out bool held) || !held)
            {
                return;
            }

            float currentTime = Time.time;

            // Check for double-tap
            if (lastButtonPressed.TryGetValue(playerIndex, out string lastButton) &&
                lastButton == buttonPath &&
                lastButtonPressTime.TryGetValue(playerIndex, out float lastTime) &&
                (currentTime - lastTime) <= DoubleTapWindow)
            {
                // Double-tap detected! Set this as the new ability button
                SetPlayerAbilityButton(playerIndex, buttonPath);
                lastButtonPressed.Remove(playerIndex);
                lastButtonPressTime.Remove(playerIndex);
            }
            else
            {
                // First tap, record it
                lastButtonPressed[playerIndex] = buttonPath;
                lastButtonPressTime[playerIndex] = currentTime;
            }
        }

        private void Update()
        {
            // Check for 3-second hold to reset
            foreach (var kvp in new Dictionary<int, float>(menuButtonHoldStartTime))
            {
                int playerIndex = kvp.Key;
                float holdStart = kvp.Value;

                if (menuButtonHeld.TryGetValue(playerIndex, out bool held) && held)
                {
                    if (Time.time - holdStart >= ResetHoldDuration)
                    {
                        // Reset to default
                        ResetPlayerAbilityButton(playerIndex);
                        menuButtonHoldStartTime[playerIndex] = float.MaxValue; // Prevent repeated resets
                    }
                }
            }
        }

        public static string GetPlayerAbilityButton(int playerIndex)
        {
            if (playerAbilityButtons.TryGetValue(playerIndex, out string button))
            {
                return button;
            }
            return DefaultGamepadButton;
        }

        public void SetPlayerAbilityButton(int playerIndex, string buttonPath)
        {
            playerAbilityButtons[playerIndex] = buttonPath;

            string buttonName = GetButtonDisplayName(buttonPath);
            ShowFeedback(playerIndex, $"Ability button set to {buttonName}");

            // Notify abilities to re-register with new button
            NotifyAbilitiesOfButtonChange(playerIndex);
        }

        public void ResetPlayerAbilityButton(int playerIndex)
        {
            if (playerAbilityButtons.ContainsKey(playerIndex))
            {
                playerAbilityButtons.Remove(playerIndex);
                ShowFeedback(playerIndex, "Ability button reset to L1");

                // Notify abilities to re-register with default button
                NotifyAbilitiesOfButtonChange(playerIndex);
            }
        }

        private void NotifyAbilitiesOfButtonChange(int playerIndex)
        {
            // Find all abilities for this player and re-register them
            // Use PlayerInput to be consistent with detection logic
            var playerInputs = FindObjectsOfType<PlayerInput>();
            bool matchedPlayer = false;

            foreach (var input in playerInputs)
            {
                if (input.playerIndex == playerIndex)
                {
                    matchedPlayer = true;
                    var abilities = input.GetComponentsInChildren<BaseAbility>();

                    foreach (var ability in abilities)
                    {
                        ability.RefreshInputBindings();
                    }
                }
            }

            if (!matchedPlayer)
            {
                Logger.LogWarning($"[PlayerControlSettings] Could not find active PlayerInput for player {playerIndex} to refresh bindings!");
            }
        }

        private void ShowFeedback(int playerIndex, string message)
        {
            feedbackMessages[playerIndex] = message;
            feedbackEndTimes[playerIndex] = Time.time + FeedbackDisplayDuration;

            // Also play a sound if available
            SoundManager.Instance?.PlaySound(
                Consts.SoundNames.PowerUp,
                Consts.SoundVolumes.PowerUp * Consts.SoundVolumes.MasterVolume
            );
        }

        private int GetPlayerIndexFromDevice(InputDevice device)
        {
            // Find which player this device belongs to by searching PlayerInput directly
            // This is safer than searching for PlayerController as PlayerInput exists in Lobby too
            var playerInputs = FindObjectsOfType<PlayerInput>();

            foreach (var playerInput in playerInputs)
            {
                if (playerInput == null) continue;

                foreach (var assignedDevice in playerInput.devices)
                {
                    if (assignedDevice == device || assignedDevice.deviceId == device.deviceId)
                    {
                        return playerInput.playerIndex;
                    }
                }
            }
            return -1;
        }

        private static string GetButtonDisplayName(string buttonPath)
        {
            switch (buttonPath)
            {
                case "<Gamepad>/leftshoulder": return "L1";
                case "<Gamepad>/lefttrigger": return "L2";
                case "<Gamepad>/rightshoulder": return "R1";
                case "<Gamepad>/righttrigger": return "R2";
                case "<Gamepad>/buttonNorth": return "Y/△";
                case "<Gamepad>/buttonSouth": return "A/X";
                case "<Gamepad>/buttonEast": return "B/○";
                case "<Gamepad>/buttonWest": return "X/□";
                case "<Gamepad>/dpad/up": return "D-Pad Up";
                case "<Gamepad>/dpad/down": return "D-Pad Down";
                case "<Gamepad>/dpad/left": return "D-Pad Left";
                case "<Gamepad>/dpad/right": return "D-Pad Right";
                case "<Gamepad>/leftStickPress": return "L3";
                case "<Gamepad>/rightStickPress": return "R3";
                default: return buttonPath;
            }
        }

        private void OnGUI()
        {
            // Display feedback messages for each player
            float yOffset = 100f;
            foreach (var kvp in feedbackMessages)
            {
                int playerIndex = kvp.Key;
                string message = kvp.Value;

                if (feedbackEndTimes.TryGetValue(playerIndex, out float endTime) && Time.time < endTime)
                {
                    float alpha = Mathf.Clamp01((endTime - Time.time) / 0.5f); // Fade out in last 0.5s

                    GUIStyle style = new GUIStyle(GUI.skin.label)
                    {
                        fontSize = 24,
                        alignment = TextAnchor.MiddleCenter,
                        fontStyle = FontStyle.Bold
                    };

                    // Background
                    GUI.color = new Color(0, 0, 0, 0.7f * alpha);
                    float width = 400f;
                    float height = 40f;
                    Rect bgRect = new Rect(Screen.width / 2 - width / 2, yOffset + (playerIndex * 50), width, height);
                    GUI.DrawTexture(bgRect, Texture2D.whiteTexture);

                    // Text
                    GUI.color = new Color(0.5f, 1f, 0.5f, alpha);
                    GUI.Label(bgRect, $"P{playerIndex + 1}: {message}", style);

                    GUI.color = Color.white;
                }
            }
        }

        private void OnDestroy()
        {
            // Clean up input actions
            menuButtonAction?.Disable();
            menuButtonAction?.Dispose();

            foreach (var action in buttonActions.Values)
            {
                action?.Disable();
                action?.Dispose();
            }
            buttonActions.Clear();

            if (Instance == this)
            {
                Instance = null;
            }
        }

        public static void Initialize()
        {
            if (Instance != null) return;

            var go = new GameObject("PlayerControlSettings");
            go.AddComponent<PlayerControlSettings>();
        }
    }
}
