using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace SpiderSurge.Common
{
    // Waits for the next key or button for a row of the Controls page; menu navigation is off meanwhile so the press only sets the control
    public static class ControlCapture
    {
        private const float Timeout = 8f;
        private const float ReleaseTimeout = 3f;

        private static int _startFrame;
        private static int _endFrame = -10;
        private static float _until;
        private static bool _restoreNavigation;
        private static bool _navigation;
        private static Gamepad _comboPad;
        private static (string path, string label, System.Func<Gamepad, ButtonControl> button) _first;

        public static Control Current { get; private set; }

        public static bool WaitingForSecond => _comboPad != null;

        // Also true on the two frames after a capture ends, so the press that set it doesn't reach a hotkey or the Back button
        public static bool Busy => Current != null || Time.frameCount - _endFrame <= 1;

        public static string Prompt
        {
            get
            {
                if (Current == null) return "";
                if (WaitingForSecond) return $"{_first.label} + press a second button for a combo, or let go";
                return Current.Device == ControlDevice.Keyboard
                    ? "press a key (Escape or Start cancels, Backspace turns it off)"
                    : "press a button (Start or Escape cancels, Select turns it off)";
            }
        }

        public static void Toggle(Control control)
        {
            if (Current == control)
            {
                End();
                return;
            }
            Start(control);
        }

        public static void Start(Control control)
        {
            if (Current == null)
            {
                EventSystem events = EventSystem.current;
                if (!_restoreNavigation && events != null)
                {
                    _navigation = events.sendNavigationEvents;
                    _restoreNavigation = true;
                }
                if (events != null) events.sendNavigationEvents = false;
            }
            Current = control;
            _comboPad = null;
            _startFrame = Time.frameCount;
            _until = Time.unscaledTime + Timeout;
            SettingsPage.RefreshOpen();
        }

        public static void Cancel()
        {
            if (Current != null) End();
            RestoreNavigation();
        }

        public static void Tick()
        {
            if (Current == null)
            {
                if (_restoreNavigation && Time.frameCount > _endFrame && (!AnyHeld() || Time.unscaledTime > _until)) RestoreNavigation();
                return;
            }
            if (Time.frameCount <= _startFrame)
            {
                return;
            }
            if (CancelPressed()) End();
            else if (Current.Device == ControlDevice.Keyboard) TickKeyboard();
            else TickController();
            if (Current != null && Time.unscaledTime > _until)
            {
                if (WaitingForSecond) Set(_first.path);
                else End();
            }
        }

        // Escape and Start cancel from either device, and so does a controller's B while a keyboard row waits, so a player on the other device isn't stuck until the timeout
        private static bool CancelPressed()
        {
            if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) return true;
            foreach (Gamepad pad in Gamepad.all)
            {
                if (pad.startButton.wasPressedThisFrame || (Current.Device == ControlDevice.Keyboard && pad.buttonEast.wasPressedThisFrame)) return true;
            }
            return false;
        }

        private static void TickKeyboard()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;
            var keys = keyboard.allKeys;
            for (int i = 0; i < keys.Count; i++)
            {
                KeyControl key = keys[i];
                if (key == null || !key.wasPressedThisFrame) continue;
                if (key.keyCode == Key.Backspace) Set(Controls.None);
                else Set("<Keyboard>/" + key.name);
                return;
            }
        }

        private static void TickController()
        {
            if (WaitingForSecond)
            {
                TickSecondButton();
                return;
            }
            foreach (Gamepad pad in Gamepad.all)
            {
                if (pad.selectButton.wasPressedThisFrame)
                {
                    Set(Controls.None);
                    return;
                }
                if (Current.Allows(ControlExtras.Dpad) && (pad.dpad.up.wasPressedThisFrame || pad.dpad.down.wasPressedThisFrame || pad.dpad.left.wasPressedThisFrame || pad.dpad.right.wasPressedThisFrame))
                {
                    Set(Controls.Dpad);
                    return;
                }
                int first = PressedAfter(pad, -1);
                if (first < 0) continue;
                var button = Controls.ControllerButtons[first];
                int second = Current.Allows(ControlExtras.Combo) ? PressedAfter(pad, first) : -1;
                if (second >= 0)
                {
                    Set(button.path + Controls.ComboSeparator + Controls.ControllerButtons[second].path);
                }
                else if (Current.Allows(ControlExtras.Combo))
                {
                    _first = button;
                    _comboPad = pad;
                    SettingsPage.RefreshOpen();
                }
                else
                {
                    Set(button.path);
                }
                return;
            }
        }

        // Two buttons that go down on the same frame, like both sticks clicked together, make a combo straight away
        private static int PressedAfter(Gamepad pad, int after)
        {
            var buttons = Controls.ControllerButtons;
            for (int i = after + 1; i < buttons.Length; i++)
            {
                if (buttons[i].button(pad).wasPressedThisFrame) return i;
            }
            return -1;
        }

        // A second button pressed while the first is held makes a combo; letting go of the first keeps it alone
        private static void TickSecondButton()
        {
            if (_comboPad == null || !_comboPad.added || !_first.button(_comboPad).isPressed)
            {
                Set(_first.path);
                return;
            }
            foreach (var button in Controls.ControllerButtons)
            {
                if (button.path != _first.path && button.button(_comboPad).wasPressedThisFrame)
                {
                    Set(_first.path + Controls.ComboSeparator + button.path);
                    return;
                }
            }
        }

        private static void Set(string binding)
        {
            Control control = Current;
            End();
            Settings.Set(control.Key, binding);
        }

        private static void End()
        {
            Current = null;
            _comboPad = null;
            _endFrame = Time.frameCount;
            _until = Time.unscaledTime + ReleaseTimeout;
            SettingsPage.RefreshOpen();
        }

        // Navigation comes back once the press is let go, so a held D-pad or Enter doesn't also move or submit in the menu
        private static bool AnyHeld()
        {
            if (Keyboard.current != null && Keyboard.current.anyKey.isPressed) return true;
            foreach (Gamepad pad in Gamepad.all)
            {
                if (pad.dpad.up.isPressed || pad.dpad.down.isPressed || pad.dpad.left.isPressed || pad.dpad.right.isPressed || pad.startButton.isPressed || pad.selectButton.isPressed) return true;
                foreach (var button in Controls.ControllerButtons)
                {
                    if (button.button(pad).isPressed) return true;
                }
            }
            return false;
        }

        private static void RestoreNavigation()
        {
            if (!_restoreNavigation) return;
            _restoreNavigation = false;
            EventSystem events = EventSystem.current;
            if (events != null) events.sendNavigationEvents = _navigation;
        }
    }
}
