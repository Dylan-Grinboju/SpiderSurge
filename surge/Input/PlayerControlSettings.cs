using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using System.Linq;
using SpiderSurge.Common;
using Logger = Silk.Logger;

namespace SpiderSurge
{
    /// <summary>
    /// Manages per-player control settings for ability activation.
    /// Allows each player to customize which button activates their ability.
    ///
    /// Controls:
    /// - Hold Menu button + Double-tap any button = Set that button as ability activation
    /// - Hold Menu button for 3 seconds = Reset to the controller ability button of the Controls page
    /// </summary>
    public class PlayerControlSettings : MonoBehaviour
    {
        public static PlayerControlSettings Instance { get; private set; }

        public static string DefaultGamepadButton => Controls.Get(Controls.AbilityController);

        // Per-player button mapping (playerIndex -> button path)
        private static readonly Dictionary<int, string> playerAbilityButtons = new Dictionary<int, string>();

        // Track menu button hold state per player
        private readonly Dictionary<int, float> menuButtonHoldStartTime = new Dictionary<int, float>();
        private readonly Dictionary<int, bool> menuButtonHeld = new Dictionary<int, bool>();
        private readonly List<int> playersToReset = new List<int>();

        // Track button presses for double-tap detection
        private readonly Dictionary<int, string> lastButtonPressed = new Dictionary<int, string>();
        private readonly Dictionary<int, float> lastButtonPressTime = new Dictionary<int, float>();
        private readonly Dictionary<int, string> pendingBindings = new Dictionary<int, string>();

        // Visual feedback tracking
        private readonly Dictionary<int, string> feedbackMessages = new Dictionary<int, string>();
        private readonly Dictionary<int, float> feedbackEndTimes = new Dictionary<int, float>();
        private GUIStyle feedbackStyle;

        private const float DoubleTapWindow = 0.4f;
        private const float ResetHoldDuration = 3f;
        private const float FeedbackDisplayDuration = 2f;
        private const float ReferenceScreenHeight = 1080f;

        private static readonly string[] SupportedButtons = Controls.ControllerButtons.Select(button => button.path).ToArray();

        // Input actions for detection
        private InputAction menuButtonAction;
        private readonly Dictionary<string, InputAction> buttonActions = new Dictionary<string, InputAction>();
        private readonly HashSet<InputControl> heldMenuControls = new HashSet<InputControl>();
        private readonly HashSet<InputControl> heldButtonControls = new HashSet<InputControl>();

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                SetupInputActions();
                Settings.Changed += OnSettingsChanged;
            }
            else
            {
                Destroy(gameObject);
            }
        }

        // The actions are pass-through: a Button action shared by every pad ignores one pad's press while another pad holds the same button.
        private void SetupInputActions()
        {
            Logger.LogInfo("[PlayerControlSettings] Setting up input actions...");

            // Select only, since Start also opens the pause menu
            menuButtonAction = new InputAction(
                name: "MenuButton",
                type: InputActionType.PassThrough
            );
            menuButtonAction.AddBinding("<Gamepad>/select");
            menuButtonAction.performed += OnMenuButtonChanged;
            menuButtonAction.canceled += OnMenuButtonChanged;
            menuButtonAction.Enable();

            Logger.LogInfo("[PlayerControlSettings] Menu button action enabled");

            // Create actions for all supported buttons
            foreach (var buttonPath in SupportedButtons)
            {
                string actionName = $"ControlSwitch_{buttonPath.Replace("/", "_").Replace("<", "").Replace(">", "")}";
                var action = new InputAction(
                    name: actionName,
                    type: InputActionType.PassThrough,
                    binding: buttonPath
                );
                action.performed += ctx => OnButtonChanged(ctx, buttonPath);
                action.canceled += ctx => OnButtonChanged(ctx, buttonPath);
                action.Enable();
                buttonActions[buttonPath] = action;
            }

        }

        // A player holds the menu button while Select is down on their pad.
        private void OnMenuButtonChanged(InputAction.CallbackContext context)
        {
            InputControl control = context.control;
            if (control == null) return;

            bool pressed = context.ReadValueAsButton();
            int playerIndex = GetPlayerIndexFromDevice(control.device);
            if (playerIndex < 0) return;

            if (pressed)
            {
                heldMenuControls.Add(control);
            }
            else
            {
                heldMenuControls.Remove(control);
            }

            bool wasHeld = menuButtonHeld.TryGetValue(playerIndex, out bool held) && held;
            bool nowHeld = IsMenuHeldOn(control.device);
            if (nowHeld == wasHeld) return;

            menuButtonHeld[playerIndex] = nowHeld;
            lastButtonPressed.Remove(playerIndex);
            lastButtonPressTime.Remove(playerIndex);
            pendingBindings.Remove(playerIndex);
            if (nowHeld)
            {
                menuButtonHoldStartTime[playerIndex] = Time.unscaledTime;
            }
            else
            {
                menuButtonHoldStartTime.Remove(playerIndex);
            }
        }

        // While a pad holds Select, its button presses belong to the remap gesture, not to abilities
        public static bool IsMenuHeld(InputDevice device)
        {
            return Instance != null && device != null && Instance.IsMenuHeldOn(device);
        }

        private bool IsMenuHeldOn(InputDevice device)
        {
            foreach (var control in heldMenuControls)
            {
                if (control.device == device)
                {
                    return true;
                }
            }
            return false;
        }

        private void OnButtonChanged(InputAction.CallbackContext context, string buttonPath)
        {
            InputControl control = context.control;
            if (control == null) return;

            if (!context.ReadValueAsButton())
            {
                heldButtonControls.Remove(control);
                return;
            }

            if (heldButtonControls.Add(control))
            {
                menuButtonHoldStartTime.Remove(GetPlayerIndexFromDevice(control.device));
            }
        }

        private void RecordBinding(int playerIndex, string buttonPath)
        {
            if (playerIndex < 0) return;

            // Only process if menu button is held
            if (!menuButtonHeld.TryGetValue(playerIndex, out bool held) || !held)
            {
                return;
            }

            float currentTime = Time.unscaledTime;

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
            foreach (var pad in Gamepad.all)
            {
                int playerIndex = GetPlayerIndexFromDevice(pad);
                if (playerIndex < 0 || !IsMenuHeldOn(pad)) continue;
                var pressed = Controls.ControllerButtons.Where(button => button.button(pad).isPressed).Select(button => button.path).ToArray();
                if (pressed.Length > 0 && pressed.Length <= 2)
                {
                    string binding = string.Join(Controls.ComboSeparator.ToString(), pressed);
                    if (!pendingBindings.TryGetValue(playerIndex, out string pending) || binding.Length > pending.Length)
                        pendingBindings[playerIndex] = binding;
                }
                else if (pressed.Length == 0 && pendingBindings.TryGetValue(playerIndex, out string pending))
                {
                    pendingBindings.Remove(playerIndex);
                    RecordBinding(playerIndex, pending);
                }
            }
            if (menuButtonHoldStartTime.Count == 0) return;

            // Check for 3-second hold to reset
            playersToReset.Clear();
            foreach (var kvp in menuButtonHoldStartTime)
            {
                if (menuButtonHeld.TryGetValue(kvp.Key, out bool held) && held && Time.unscaledTime - kvp.Value >= ResetHoldDuration)
                {
                    playersToReset.Add(kvp.Key);
                }
            }

            foreach (int playerIndex in playersToReset)
            {
                ResetPlayerAbilityButton(playerIndex);
                menuButtonHoldStartTime[playerIndex] = float.MaxValue; // Prevent repeated resets
            }
        }

        public static string GetPlayerAbilityButton(int playerIndex)
        {
            if (playerAbilityButtons.TryGetValue(playerIndex, out string button))
            {
                if (MatchesUltimateChord(button)) return DefaultGamepadButton;
                return button;
            }
            return DefaultGamepadButton;
        }

        public void SetPlayerAbilityButton(int playerIndex, string buttonPath)
        {
            if (MatchesUltimateChord(buttonPath))
            {
                ShowFeedback(playerIndex, "This combination is the ultimate. Change it in Controls first.");
                return;
            }
            playerAbilityButtons[playerIndex] = buttonPath;

            string buttonName = GetButtonDisplayName(buttonPath);
            ShowFeedback(playerIndex, $"Ability button set to {buttonName}");

            // Notify abilities to re-register with new button
            NotifyAbilitiesOfButtonChange(playerIndex);
        }

        private static bool MatchesUltimateChord(string binding)
        {
            if (!InputChord.Split(binding, out string first, out string second)) return false;
            return Controls.Parse(Controls.Get(Controls.UltimateController)).Any(combo => combo.Length == 2 &&
                combo.Contains(first, System.StringComparer.OrdinalIgnoreCase) && combo.Contains(second, System.StringComparer.OrdinalIgnoreCase));
        }

        public void ResetPlayerAbilityButton(int playerIndex)
        {
            if (playerAbilityButtons.ContainsKey(playerIndex))
            {
                playerAbilityButtons.Remove(playerIndex);
                ShowFeedback(playerIndex, $"Ability button reset to {Controls.Label(DefaultGamepadButton)}");

                // Notify abilities to re-register with default button
                NotifyAbilitiesOfButtonChange(playerIndex);
            }
        }

        // A spider that has abilities rebinds now; in the lobby or while dead, the next spider's abilities read the new button when they are added
        private void NotifyAbilitiesOfButtonChange(int playerIndex)
        {
            foreach (var input in PlayerInput.all)
            {
                if (input == null || input.playerIndex != playerIndex) continue;

                foreach (var ability in input.GetComponentsInChildren<BaseAbility>())
                {
                    ability.RefreshInputBindings();
                }
            }
        }

        // A remap belongs to the player who made it, so whoever joins next in that slot starts on the default button
        public static void ForgetPlayer(int playerIndex)
        {
            playerAbilityButtons.Remove(playerIndex);
            if (Instance == null) return;

            Instance.menuButtonHeld.Remove(playerIndex);
            Instance.menuButtonHoldStartTime.Remove(playerIndex);
            Instance.lastButtonPressed.Remove(playerIndex);
            Instance.lastButtonPressTime.Remove(playerIndex);
            Instance.pendingBindings.Remove(playerIndex);
            Instance.feedbackMessages.Remove(playerIndex);
            Instance.feedbackEndTimes.Remove(playerIndex);
        }

        private void ShowFeedback(int playerIndex, string message)
        {
            feedbackMessages[playerIndex] = message;
            feedbackEndTimes[playerIndex] = Time.unscaledTime + FeedbackDisplayDuration;

            // Also play a sound if available
            SoundManager.Instance?.PlaySound(
                Consts.SoundNames.PowerUp,
                Consts.SoundVolumes.PowerUp * Consts.SoundVolumes.MasterVolume
            );
        }

        // Every joined player has a PlayerInput, in the lobby and while their spider is dead too, so the gesture works there and in any mode
        private int GetPlayerIndexFromDevice(InputDevice device)
        {
            foreach (var playerInput in PlayerInput.all)
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

        private static string GetButtonDisplayName(string buttonPath) => Controls.Label(buttonPath);

        private void OnSettingsChanged(string key)
        {
            if (!Controls.Affects(key, Controls.AbilityKeyboard, Controls.AbilityController, Controls.UltimateKeyboard, Controls.UltimateController)) return;

            foreach (var input in PlayerInput.all)
            {
                if (input == null) continue;
                foreach (var ability in input.GetComponentsInChildren<BaseAbility>())
                {
                    ability.RefreshControls();
                }
            }
        }

        // One row per player, scaled to the screen height and wrapped into centred columns when they run off the bottom
        private void OnGUI()
        {
            float now = Time.unscaledTime;
            int highestIndex = -1;
            foreach (var kvp in feedbackEndTimes)
            {
                if (now < kvp.Value && kvp.Key > highestIndex)
                {
                    highestIndex = kvp.Key;
                }
            }
            if (highestIndex < 0) return;

            float scale = Screen.height / ReferenceScreenHeight;
            float top = 100f * scale;
            float rowStep = 50f * scale;
            float height = 40f * scale;
            float gap = 10f * scale;
            int rows = Mathf.Max(1, Mathf.FloorToInt((Screen.height - top - gap) / rowStep));
            int columns = highestIndex / rows + 1;
            float width = Mathf.Min(400f * scale, (Screen.width - gap * (columns + 1)) / columns);
            float left = (Screen.width - (columns * width + (columns - 1) * gap)) / 2f;

            if (feedbackStyle == null)
            {
                feedbackStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = FontStyle.Bold
                };
            }
            feedbackStyle.fontSize = Mathf.Max(10, Mathf.RoundToInt(24f * scale));

            foreach (var kvp in feedbackMessages)
            {
                int playerIndex = kvp.Key;
                string message = kvp.Value;

                if (feedbackEndTimes.TryGetValue(playerIndex, out float endTime) && now < endTime)
                {
                    float alpha = Mathf.Clamp01((endTime - now) / 0.5f); // Fade out in last 0.5s

                    // Background
                    GUI.color = new Color(0, 0, 0, 0.7f * alpha);
                    int column = playerIndex / rows;
                    int row = playerIndex % rows;
                    Rect bgRect = new Rect(left + column * (width + gap), top + row * rowStep, width, height);
                    GUI.DrawTexture(bgRect, Texture2D.whiteTexture);

                    // Text
                    GUI.color = new Color(0.5f, 1f, 0.5f, alpha);
                    GUI.Label(bgRect, $"P{playerIndex + 1}: {message}", feedbackStyle);

                    GUI.color = Color.white;
                }
            }
        }

        private void OnDestroy()
        {
            if (Instance != this) return;
            Settings.Changed -= OnSettingsChanged;
            // Clean up input actions
            menuButtonAction?.Disable();
            menuButtonAction?.Dispose();

            foreach (var action in buttonActions.Values)
            {
                action?.Disable();
                action?.Dispose();
            }
            buttonActions.Clear();
            playerAbilityButtons.Clear();

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
