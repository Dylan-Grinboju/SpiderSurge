using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace SpiderSurge.Common
{
    // Every key and button the mod reacts to; only the controller ability button can be changed, per player, with the Select gesture
    public static class Controls
    {
        public const string None = "none";
        public const string Dpad = "<Gamepad>/dpad";
        public const char ComboSeparator = '+';
        public const char AlternativeSeparator = '|';

        public const string AbilityKeyboard = "<Keyboard>/q";
        public const string AbilityController = "<Gamepad>/leftTrigger";
        public const string UltimateKeyboard = "<Keyboard>/c";
        public const string UltimateController = "<Gamepad>/leftStickPress+<Gamepad>/rightStickPress";
        public const string SmallWindow = "<Keyboard>/f1";
        public const string BigWindow = "<Keyboard>/f2";
        public const string TitlesWindow = "<Keyboard>/f3";
        public const string SwitchWindows = Dpad;
        public const string PreviousPresetKeyboard = "<Keyboard>/pageUp";
        public const string NextPresetKeyboard = "<Keyboard>/pageDown";
        public const string PreviousPresetController = "<Gamepad>/dpad/left";
        public const string NextPresetController = "<Gamepad>/dpad/right";
        public const string TeamKeyboard = "<Keyboard>/t";
        public const string TeamController = "<Gamepad>/buttonNorth";

        public static readonly (string path, string label, Func<Gamepad, ButtonControl> button)[] ControllerButtons =
        {
            ("<Gamepad>/buttonSouth", "A", pad => pad.buttonSouth),
            ("<Gamepad>/buttonEast", "B", pad => pad.buttonEast),
            ("<Gamepad>/buttonWest", "X", pad => pad.buttonWest),
            ("<Gamepad>/buttonNorth", "Y", pad => pad.buttonNorth),
            ("<Gamepad>/leftShoulder", "L1", pad => pad.leftShoulder),
            ("<Gamepad>/rightShoulder", "R1", pad => pad.rightShoulder),
            ("<Gamepad>/leftTrigger", "L2", pad => pad.leftTrigger),
            ("<Gamepad>/rightTrigger", "R2", pad => pad.rightTrigger),
            ("<Gamepad>/leftStickPress", "L3", pad => pad.leftStickButton),
            ("<Gamepad>/rightStickPress", "R3", pad => pad.rightStickButton),
        };

        public static readonly string[] DpadDirections = { "<Gamepad>/dpad/up", "<Gamepad>/dpad/down", "<Gamepad>/dpad/left", "<Gamepad>/dpad/right" };

        private static readonly Dictionary<string, string> GamepadLabels = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { Dpad, "D-pad" },
            { "<Gamepad>/dpad/up", "D-pad up" },
            { "<Gamepad>/dpad/down", "D-pad down" },
            { "<Gamepad>/dpad/left", "D-pad left" },
            { "<Gamepad>/dpad/right", "D-pad right" },
            { "<Gamepad>/select", "Select" },
            { "<Gamepad>/start", "Start" },
            { "<Gamepad>/leftStick", "Left stick" },
            { "<Gamepad>/rightStick", "Right stick" },
        };

        private static readonly Dictionary<string, string[][]> _parsed = new Dictionary<string, string[][]>();
        private static readonly Dictionary<string, string> _labels = new Dictionary<string, string>();

        static Controls()
        {
            foreach (var button in ControllerButtons)
            {
                GamepadLabels[button.path] = button.label;
            }
        }

        public static bool IsDpad(string path) => string.Equals(path, Dpad, StringComparison.OrdinalIgnoreCase);

        public static string[][] Parse(string binding)
        {
            if (string.IsNullOrEmpty(binding) || binding == None)
            {
                return Array.Empty<string[]>();
            }
            if (_parsed.TryGetValue(binding, out string[][] cached))
            {
                return cached;
            }

            string[][] parsed = binding.Split(AlternativeSeparator)
                .Select(alternative => alternative.Split(ComboSeparator).Select(path => path.Trim()).Where(path => path.Length > 0).ToArray())
                .Where(combo => combo.Length > 0)
                .ToArray();
            _parsed[binding] = parsed;
            return parsed;
        }

        public static string[] Combo(string binding)
        {
            string[][] alternatives = Parse(binding);
            return alternatives.Length == 1 && alternatives[0].Length == 2 ? alternatives[0] : null;
        }

        // Every control path that fires the binding on its own, with the D-pad as its four directions
        public static IEnumerable<string> PressPaths(string binding)
        {
            foreach (string[] combo in Parse(binding))
            {
                if (combo.Length != 1)
                {
                    continue;
                }
                if (IsDpad(combo[0]))
                {
                    foreach (string direction in DpadDirections) yield return direction;
                }
                else
                {
                    yield return combo[0];
                }
            }
        }

        // While a pad holds Select for the ability remap, no mod button reacts to it
        public static bool Ignored(InputDevice device) => SpiderSurge.PlayerControlSettings.IsMenuHeld(device);

        public static string Display(string binding) => $"<noparse>{Label(binding)}</noparse>";

        public static string Label(string binding)
        {
            if (string.IsNullOrEmpty(binding) || binding == None)
            {
                return "Off";
            }
            if (_labels.TryGetValue(binding, out string cached))
            {
                return cached;
            }

            string label = string.Join(" or ", Parse(binding).Select(combo => string.Join(" + ", combo.Select(PathLabel))));
            _labels[binding] = label;
            return label;
        }

        public static string PathLabel(string path)
        {
            if (GamepadLabels.TryGetValue(path, out string label))
            {
                return label;
            }
            try
            {
                string readable = InputControlPath.ToHumanReadableString(path, InputControlPath.HumanReadableStringOptions.OmitDevice);
                if (!string.IsNullOrEmpty(readable))
                {
                    return readable.Length == 1 ? readable.ToUpperInvariant() : readable;
                }
            }
            catch (Exception)
            {
            }
            int slash = path.LastIndexOf('/');
            return slash >= 0 ? path.Substring(slash + 1) : path;
        }
    }

    public sealed class ControlPoller
    {
        private string _binding;
        private InputDevice _device;
        private ButtonControl[][] _alternatives = Array.Empty<ButtonControl[]>();

        // True on the frame a button of the binding goes down while the rest of its combo is held
        public bool WasPressed(string binding, InputDevice device)
        {
            if (device == null || string.IsNullOrEmpty(binding) || binding == Controls.None || Controls.Ignored(device))
            {
                return false;
            }
            if (!string.Equals(binding, _binding, StringComparison.Ordinal) || device != _device)
            {
                Resolve(binding, device);
            }

            for (int i = 0; i < _alternatives.Length; i++)
            {
                ButtonControl[] combo = _alternatives[i];
                bool held = true;
                bool pressed = false;
                for (int j = 0; j < combo.Length; j++)
                {
                    held &= combo[j].isPressed;
                    pressed |= combo[j].wasPressedThisFrame;
                }
                if (held && pressed)
                {
                    return true;
                }
            }
            return false;
        }

        private void Resolve(string binding, InputDevice device)
        {
            _binding = binding;
            _device = device;
            var alternatives = new List<ButtonControl[]>();
            foreach (string[] combo in Controls.Parse(binding))
            {
                if (combo.Length == 1 && Controls.IsDpad(combo[0]))
                {
                    foreach (string direction in Controls.DpadDirections)
                    {
                        AddResolved(alternatives, device, direction);
                    }
                    continue;
                }
                AddResolved(alternatives, device, combo);
            }
            _alternatives = alternatives.ToArray();
        }

        private static void AddResolved(List<ButtonControl[]> alternatives, InputDevice device, params string[] paths)
        {
            var buttons = new ButtonControl[paths.Length];
            for (int i = 0; i < paths.Length; i++)
            {
                buttons[i] = InputControlPath.TryFindControl(device, paths[i]) as ButtonControl;
                if (buttons[i] == null)
                {
                    return;
                }
            }
            alternatives.Add(buttons);
        }
    }

    // A press on any gamepad, not only the one Unity calls current, which is whichever pad last sent input
    public sealed class AnyPadPoller
    {
        private readonly List<ControlPoller> _pollers = new List<ControlPoller>();

        public bool WasPressed(string binding)
        {
            var pads = Gamepad.all;
            while (_pollers.Count < pads.Count)
            {
                _pollers.Add(new ControlPoller());
            }
            for (int i = 0; i < pads.Count; i++)
            {
                if (_pollers[i].WasPressed(binding, pads[i]))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
