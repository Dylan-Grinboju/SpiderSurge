using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.Layouts;
using Logger = Silk.Logger;

namespace SpiderSurge.Common
{
    public enum ControlDevice
    {
        Keyboard,
        Controller
    }

    [Flags]
    public enum ControlExtras
    {
        None = 0,
        Dpad = 1,
        Combo = 2,
        Alternatives = 4
    }

    public sealed class Control
    {
        public string Key { get; }
        public string Name { get; }
        public ControlDevice Device { get; }
        public string Default { get; }
        public ControlExtras Extras { get; }
        public Part Owner { get; }

        internal string Raw;
        internal string Value;

        public Control(string key, string name, ControlDevice device, string value, ControlExtras extras, Part owner)
        {
            Key = key;
            Name = name;
            Device = device;
            Default = value;
            Extras = extras;
            Owner = owner;
        }

        public bool Allows(ControlExtras extra) => (Extras & extra) == extra;

        public bool Shown => Owner == null || Owner.Running;
    }

    // Every key and button the mod reacts to, set from one page of the Mods menu
    public static class Controls
    {
        public const string Prefix = "controls";
        public const string None = "none";
        public const string Dpad = "<Gamepad>/dpad";
        public const char ComboSeparator = '+';
        public const char AlternativeSeparator = '|';
        private const string KeyboardDevice = "<Keyboard>/";
        private const string GamepadDevice = "<Gamepad>/";

        public static readonly Control AbilityKeyboard = new Control("controls.ability.keyboard", "Ability", ControlDevice.Keyboard, "<Keyboard>/q", ControlExtras.None, Parts.Surge);
        public static readonly Control AbilityController = new Control("controls.ability.controller", "Ability", ControlDevice.Controller, "<Gamepad>/leftShoulder", ControlExtras.None, Parts.Surge);
        public static readonly Control UltimateKeyboard = new Control("controls.ultimate.keyboard", "Ultimate", ControlDevice.Keyboard, "<Keyboard>/c", ControlExtras.Alternatives, Parts.Surge);
        public static readonly Control UltimateController = new Control("controls.ultimate.controller", "Ultimate", ControlDevice.Controller, "<Gamepad>/leftStickPress+<Gamepad>/rightStickPress", ControlExtras.Dpad | ControlExtras.Combo | ControlExtras.Alternatives, Parts.Surge);
        public static readonly Control SurgeGuide = new Control("controls.surgeGuide.keyboard", "Surge guide", ControlDevice.Keyboard, "<Keyboard>/backquote", ControlExtras.Alternatives, Parts.Surge);
        public static readonly Control SmallWindow = new Control("controls.stats.smallWindow", "Small window", ControlDevice.Keyboard, "<Keyboard>/f1", ControlExtras.Alternatives, Parts.Stats);
        public static readonly Control BigWindow = new Control("controls.stats.bigWindow", "Big window", ControlDevice.Keyboard, "<Keyboard>/f2", ControlExtras.Alternatives, Parts.Stats);
        public static readonly Control TitlesWindow = new Control("controls.stats.titlesWindow", "Titles", ControlDevice.Keyboard, "<Keyboard>/f3", ControlExtras.Alternatives, Parts.Stats);
        public static readonly Control SwitchWindows = new Control("controls.stats.switchWindows", "Switch windows", ControlDevice.Controller, Dpad, ControlExtras.Dpad | ControlExtras.Combo | ControlExtras.Alternatives, Parts.Stats);
        public static readonly Control TeamKeyboard = new Control("controls.teams.keyboard", "Switch team", ControlDevice.Keyboard, "<Keyboard>/t", ControlExtras.Alternatives, Parts.Teams);
        public static readonly Control TeamController = new Control("controls.teams.controller", "Switch team", ControlDevice.Controller, "<Gamepad>/buttonNorth", ControlExtras.Alternatives, Parts.Teams);

        public static readonly Control[] All =
        {
            AbilityKeyboard, AbilityController, UltimateKeyboard, UltimateController, SurgeGuide,
            SmallWindow, BigWindow, TitlesWindow, SwitchWindows,
            TeamKeyboard, TeamController
        };

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
        private static readonly Dictionary<(string, string), string> _without = new Dictionary<(string, string), string>();
        private static HashSet<string> _keyNames;
        private static readonly HashSet<string> _warned = new HashSet<string>();

        static Controls()
        {
            foreach (var button in ControllerButtons)
            {
                GamepadLabels[button.path] = button.label;
            }
        }

        public static IEnumerable<KeyValuePair<string, object>> Defaults => All.Select(control => new KeyValuePair<string, object>(control.Key, control.Default));

        public static bool Affects(string changedKey, params Control[] controls)
        {
            return controls.Any(control => Settings.Affects(changedKey, control.Key));
        }

        // The binding in use: the file's value when it fits the row, otherwise the default; None when turned off
        public static string Get(Control control)
        {
            string raw = Settings.GetString(control.Key);
            if (control.Raw == raw && control.Value != null)
            {
                return control.Value;
            }

            string value = Normalize(control, raw);
            if (value == null)
            {
                if (_warned.Add(control.Key + "=" + raw))
                {
                    Logger.LogWarning($"Setting {control.Key} '{raw}' is not a binding this control can use, using {control.Default}");
                }
                value = control.Default;
            }
            control.Raw = raw;
            control.Value = value;
            return value;
        }

        public static bool Accepts(Control control, string raw) => Normalize(control, raw) != null;

        private static string Normalize(Control control, string raw)
        {
            string text = (raw ?? "").Trim();
            if (text.Length == 0 || text.Equals(None, StringComparison.OrdinalIgnoreCase))
            {
                return None;
            }

            string[][] alternatives = Parse(text);
            if (alternatives.Length == 0 || (alternatives.Length > 1 && !control.Allows(ControlExtras.Alternatives)))
            {
                return null;
            }
            foreach (string[] combo in alternatives)
            {
                if (combo.Length > 2 || (combo.Length == 2 && (!control.Allows(ControlExtras.Combo) || alternatives.Length > 1)))
                {
                    return null;
                }
                foreach (string path in combo)
                {
                    if (!Fits(control, path) || (IsDpad(path) && (!control.Allows(ControlExtras.Dpad) || combo.Length > 1)))
                    {
                        return null;
                    }
                }
            }
            return text;
        }

        // A controller row takes the buttons of ControllerButtons and the D-pad, and a keyboard row any key but Escape; Start, Select and Escape stay with the game's pause menu and the ability remap gesture
        private static bool Fits(Control control, string path)
        {
            if (control.Device == ControlDevice.Controller)
            {
                return IsDpad(path) || ControllerButtons.Any(button => string.Equals(button.path, path, StringComparison.OrdinalIgnoreCase));
            }
            if (!path.StartsWith(KeyboardDevice, StringComparison.OrdinalIgnoreCase)) return false;
            string key = path.Substring(KeyboardDevice.Length);
            if (key.Equals("escape", StringComparison.OrdinalIgnoreCase)) return false;
            HashSet<string> keys = KeyNames();
            return keys.Count > 0 ? keys.Contains(key) : key.Length > 0 && key.All(char.IsLetterOrDigit);
        }

        private static HashSet<string> KeyNames()
        {
            if (_keyNames != null) return _keyNames;
            _keyNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            try
            {
                foreach (InputControlLayout.ControlItem item in InputSystem.LoadLayout("Keyboard").controls)
                {
                    if (item.layout == "Key") _keyNames.Add(item.name);
                }
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Couldn't list the keyboard's keys: {ex.Message}");
            }
            return _keyNames;
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

        public static IEnumerable<string> AllPaths(string binding)
        {
            foreach (string[] combo in Parse(binding))
            {
                foreach (string path in combo)
                {
                    if (IsDpad(path))
                    {
                        foreach (string direction in DpadDirections) yield return direction;
                    }
                    else
                    {
                        yield return path;
                    }
                }
            }
        }

        public static bool Overlap(string binding, string other)
        {
            var paths = new HashSet<string>(AllPaths(binding), StringComparer.OrdinalIgnoreCase);
            return AllPaths(other).Any(paths.Contains);
        }

        // The binding without the alternatives that share a button with what fires other on its own
        public static string Without(string binding, string other)
        {
            if (_without.TryGetValue((binding, other), out string cached))
            {
                return cached;
            }
            string result = binding;
            var taken = new HashSet<string>(PressPaths(other), StringComparer.OrdinalIgnoreCase);
            if (taken.Count > 0)
            {
                var kept = Parse(binding).Where(combo => combo.Length > 1 || !AllPaths(combo[0]).Any(taken.Contains)).Select(combo => string.Join(ComboSeparator.ToString(), combo)).ToArray();
                result = kept.Length == 0 ? None : string.Join(AlternativeSeparator.ToString(), kept);
            }
            _without[(binding, other)] = result;
            return result;
        }

        // While a control is being set, or a pad holds Start or Select for the ability remap, no mod button reacts
        public static bool Ignored(InputDevice device) => ControlCapture.Busy || SpiderSurge.PlayerControlSettings.IsMenuHeld(device);

        public static string Label(Control control) => Label(Get(control));

        public static string Display(Control control) => Display(Get(control));

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

        public static IEnumerable<Control> ClashesOf(Control control)
        {
            string binding = Get(control);
            if (binding == None)
            {
                yield break;
            }
            foreach (Control other in All)
            {
                if (other != control && other.Device == control.Device && other.Shown && Overlap(binding, Get(other)))
                {
                    yield return other;
                }
            }
        }

        public static void ResetAll()
        {
            Settings.Reset(Prefix);
            Logger.LogInfo("Controls reset to defaults");
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
}
