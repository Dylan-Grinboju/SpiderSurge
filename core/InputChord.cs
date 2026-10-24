using System;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

namespace SpiderSurge.Common
{
    public static class InputChord
    {
        public const string Interaction = "SpiderSurgeChord";
        private static readonly string[] Buttons =
        {
            "leftShoulder", "rightShoulder", "leftTrigger", "rightTrigger",
            "buttonNorth", "buttonSouth", "buttonEast", "buttonWest",
            "leftStickPress", "rightStickPress", "dpad/up", "dpad/down", "dpad/left", "dpad/right",
            "start", "select"
        };

        public static void Initialize() => InputSystem.RegisterInteraction<ChordInteraction>(Interaction);

        public static int Code(InputControl control)
        {
            if (control == null) return 0;
            if (control is KeyControl key) return 1000 + (int)key.keyCode;
            if (!(control.device is Gamepad)) return 0;
            for (int i = 0; i < Buttons.Length; i++)
                if (control == control.device.TryGetChildControl<ButtonControl>(Buttons[i])) return i + 1;
            return 0;
        }

        public static int Code(string path)
        {
            if (string.IsNullOrEmpty(path)) return 0;
            const string keyboardPrefix = "<Keyboard>/";
            if (path.StartsWith(keyboardPrefix, StringComparison.OrdinalIgnoreCase) &&
                Enum.TryParse(path.Substring(keyboardPrefix.Length), true, out Key key)) return 1000 + (int)key;
            for (int i = 0; i < Buttons.Length; i++)
                if (string.Equals(path, "<Gamepad>/" + Buttons[i], StringComparison.OrdinalIgnoreCase)) return i + 1;
            return 0;
        }

        public static ButtonControl Control(InputDevice device, int code)
        {
            if (code >= 1000 && device is Keyboard keyboard) return keyboard[(Key)(code - 1000)];
            return device is Gamepad && code > 0 && code <= Buttons.Length
                ? device.TryGetChildControl<ButtonControl>(Buttons[code - 1]) : null;
        }

        public static string Path(InputControl control)
        {
            int code = Code(control);
            if (code >= 1000) return "<Keyboard>/" + control.name;
            return code > 0 ? "<Gamepad>/" + Buttons[code - 1] : null;
        }

        public static int Modifier(string interactions)
        {
            if (string.IsNullOrEmpty(interactions)) return 0;
            int start = interactions.IndexOf(Interaction + "(modifier=", StringComparison.OrdinalIgnoreCase);
            if (start < 0) return 0;
            start += Interaction.Length + "(modifier=".Length;
            int end = interactions.IndexOf(')', start);
            return end > start && int.TryParse(interactions.Substring(start, end - start), out int code) ? code : 0;
        }

        public static string Interactions(int code) => Interaction + "(modifier=" + code + ")";

        public static string Label(string path, int modifier = 0)
        {
            string label = InputControlPath.ToHumanReadableString(path, InputControlPath.HumanReadableStringOptions.OmitDevice);
            if (modifier == 0 || (modifier < 1000 && (modifier < 1 || modifier > Buttons.Length))) return label;
            string other = modifier >= 1000 ? ((Key)(modifier - 1000)).ToString() : Buttons[modifier - 1];
            if (modifier < 1000) other = InputControlPath.ToHumanReadableString("<Gamepad>/" + other, InputControlPath.HumanReadableStringOptions.OmitDevice);
            return label + " + " + other;
        }

        public static bool Split(string binding, out string primary, out string secondary)
        {
            int separator = binding.IndexOf('+');
            primary = separator < 0 ? binding : binding.Substring(0, separator);
            secondary = separator < 0 ? null : binding.Substring(separator + 1);
            return secondary != null;
        }

        // Poll while the primary is held so either press order works without inventing extra bindings in the saved asset.
        public class ChordInteraction : IInputInteraction
        {
            public int modifier;

            public void Process(ref InputInteractionContext context)
            {
                bool primary = context.control is ButtonControl button && button.isPressed;
                bool secondary = Control(context.control.device, modifier)?.isPressed == true;
                if (!primary)
                {
                    if (context.phase == InputActionPhase.Performed || context.phase == InputActionPhase.Started)
                        context.Canceled();
                    return;
                }
                if (context.phase == InputActionPhase.Waiting) context.Started();
                if (secondary)
                {
                    if (context.phase != InputActionPhase.Performed)
                    {
                        context.PerformedAndStayPerformed();
                    }
                }
                else if (context.phase == InputActionPhase.Performed)
                {
                    context.Canceled();
                    context.Started();
                }
                context.SetTimeout(0.01f);
            }

            public void Reset() { }
        }
    }
}
