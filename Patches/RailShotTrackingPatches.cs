using HarmonyLib;
using System.Collections.Generic;

namespace SpiderSurge;

public static class RailShotTracker
{
    private static readonly List<RailShot> _all = [];

    public static IReadOnlyList<RailShot> All => _all;

    public static void Register(RailShot railShot)
    {
        if (railShot == null)
        {
            return;
        }

        if (!_all.Contains(railShot))
        {
            _all.Add(railShot);
        }
    }

    public static void Unregister(RailShot railShot)
    {
        if (railShot == null)
        {
            return;
        }

        _all.Remove(railShot);
    }
}

[HarmonyPatch(typeof(RailShot), "Start")]
internal static class RailShot_Start_TrackingPatch
{
    [HarmonyPostfix]
    private static void Postfix(RailShot __instance) => RailShotTracker.Register(__instance);
}

[HarmonyPatch(typeof(RailShot), "StopProjectile")]
internal static class RailShot_StopProjectile_TrackingPatch
{
    [HarmonyPostfix]
    private static void Postfix(RailShot __instance) => RailShotTracker.Unregister(__instance);
}

[HarmonyPatch(typeof(Unity.Netcode.NetworkBehaviour), "OnDestroy")]
internal static class RailShot_OnDestroy_TrackingPatch
{
    [HarmonyPostfix]
    private static void Postfix(Unity.Netcode.NetworkBehaviour __instance)
    {
        if (__instance is RailShot railShot)
        {
            RailShotTracker.Unregister(railShot);
        }
    }
}