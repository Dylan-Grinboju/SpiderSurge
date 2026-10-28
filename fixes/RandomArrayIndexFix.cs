using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace SpiderSurge.Fixes
{
    [HarmonyPatch]
    internal static class RandomArrayIndexFix
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.EnumeratorMoveNext(AccessTools.Method(typeof(SurvivalModeHud), "ActivateChoiseViewTimer"));
            yield return AccessTools.Method(typeof(Shotgun), "ShootClientRpc");
            yield return AccessTools.Method(typeof(DamageOnParticle), "OnParticleCollision");
        }

        // Integer Random.Range excludes its upper bound, so the array length must not be reduced.
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase __originalMethod)
        {
            var code = new List<CodeInstruction>(instructions);
            var range = AccessTools.Method(typeof(UnityEngine.Random), nameof(UnityEngine.Random.Range), new[] { typeof(int), typeof(int) });
            var array = __originalMethod.DeclaringType == typeof(Shotgun)
                ? AccessTools.Field(typeof(Shotgun), "shotSounds")
                : __originalMethod.DeclaringType == typeof(DamageOnParticle)
                    ? AccessTools.Field(typeof(DamageOnParticle), "hitSounds")
                    : AccessTools.Field(typeof(SurvivalModeHud), "availableModifiers");
            int replaced = 0;
            for (int i = 5; i < code.Count; i++)
            {
                if (!code[i].Calls(range) || !code[i - 5].LoadsField(array) ||
                    code[i - 4].opcode != OpCodes.Ldlen || code[i - 3].opcode != OpCodes.Conv_I4 ||
                    !code[i - 2].LoadsConstant(1) || code[i - 1].opcode != OpCodes.Sub)
                {
                    continue;
                }
                code[i - 2].opcode = OpCodes.Nop;
                code[i - 2].operand = null;
                code[i - 1].opcode = OpCodes.Nop;
                code[i - 1].operand = null;
                replaced++;
            }
            if (replaced != 1)
            {
                throw new InvalidOperationException($"Expected one array random bound in {__originalMethod}, found {replaced}");
            }
            return code;
        }
    }
}
