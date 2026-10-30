using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using SpiderSurge.Common;
using UnityEngine;

namespace SpiderSurge.Fixes
{
    [HarmonyPatch]
    internal static class AstralWeaponDropFix
    {
        private static MethodBase TargetMethod() =>
            AccessTools.EnumeratorMoveNext(AccessTools.Method(typeof(SpiderHealthSystem), "DisintegrateLegsAndDestroy"));

        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var code = new List<CodeInstruction>(instructions);
            var setActive = AccessTools.Method(typeof(GameObject), nameof(GameObject.SetActive));
            int deactivations = 0;
            for (int i = 1; i < code.Count; i++)
            {
                if (!code[i].Calls(setActive) || !code[i - 1].LoadsConstant(0)) continue;
                if (++deactivations != 1) continue;
                code[i].opcode = OpCodes.Call;
                code[i].operand = AccessTools.Method(typeof(AstralWeaponDropFix), nameof(SetClientActive));
            }
            if (deactivations != 2)
                throw new InvalidOperationException($"Expected two Astral spider deactivations, found {deactivations}");
            return code;
        }

        // The host hides the root again after OnCharacterDied has dropped and synchronized its weapon.
        private static void SetClientActive(GameObject spider, bool active)
        {
            if (!Net.IsAuthority) spider.SetActive(active);
        }
    }
}
