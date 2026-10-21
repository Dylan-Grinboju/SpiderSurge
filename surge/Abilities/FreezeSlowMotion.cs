using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace SpiderSurge
{
    public static class FreezeSlowMotion
    {
        [ThreadStatic] private static int _perkScope;
        private static float _until;
        private static float _from;

        public static bool Active => Time.realtimeSinceStartup < _until && Time.timeScale > 0f && Time.timeScale < 1f
            && TimeScaleController.instance != null && TimeScaleController.instance.SlowMoActive();

        internal static void Enter() => _perkScope++;
        internal static void Exit() => _perkScope--;
        internal static void Observe(float duration)
        {
            if (_perkScope <= 0 || duration <= 0f) return;
            float now = Time.realtimeSinceStartup;
            if (now >= _until) _from = now;
            _until = Mathf.Max(_until, now + duration);
        }

        internal static float ElapsedSince(float previous) => Mathf.Max(0f,
            Mathf.Min(Time.realtimeSinceStartup, _until) - Mathf.Max(previous, _from));

        internal static void Reset() => _until = 0f;
    }

    // Only slow motion requested by these three perk effects qualifies; wave-end and death slow motion do not.
    [HarmonyPatch]
    internal static class FreezeSlowMotion_SourcePatch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(EnemyHealthSystem), "ExplodeCosmeticsClientRpc");
            yield return AccessTools.Method(typeof(ForceField), "DamageClientRpc");
            yield return AccessTools.Method(typeof(Explosion), "Start");
        }

        [HarmonyPrefix] private static void Prefix() => FreezeSlowMotion.Enter();
        [HarmonyFinalizer] private static void Finalizer() => FreezeSlowMotion.Exit();
    }

    [HarmonyPatch(typeof(TimeScaleController), nameof(TimeScaleController.DoSlowMotion))]
    internal static class FreezeSlowMotion_ObservePatch
    {
        [HarmonyPostfix] private static void Postfix(float time) => FreezeSlowMotion.Observe(time);
    }

    [HarmonyPatch(typeof(TimeScaleController), nameof(TimeScaleController.Reset))]
    internal static class FreezeSlowMotion_ResetPatch
    {
        [HarmonyPostfix] private static void Postfix() => FreezeSlowMotion.Reset();
    }
}
