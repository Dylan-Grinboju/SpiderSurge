using System;
using System.Collections.Generic;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine.InputSystem;

namespace SpiderSurge.Fixes
{
    [HarmonyPatch(typeof(HudController), "Update")]
    internal static class ControllerPauseFix
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var code = new List<CodeInstruction>(instructions);
            var current = AccessTools.PropertyGetter(typeof(Gamepad), nameof(Gamepad.current));
            var pressed = AccessTools.Method(typeof(ControllerPauseFix), nameof(GetPauseGamepad));
            int replaced = 0;
            foreach (var instruction in code)
            {
                if (!instruction.Calls(current))
                {
                    continue;
                }
                instruction.opcode = OpCodes.Call;
                instruction.operand = pressed;
                replaced++;
            }
            if (replaced != 2)
            {
                throw new InvalidOperationException($"Expected two gamepad reads in HudController.Update, found {replaced}");
            }
            return code;
        }

        // Keep the game's pause gates and single toggle, but accept Start from any gamepad.
        private static Gamepad GetPauseGamepad()
        {
            foreach (var gamepad in Gamepad.all)
            {
                if (gamepad.startButton.wasPressedThisFrame)
                {
                    return gamepad;
                }
            }
            return null;
        }
    }
}
