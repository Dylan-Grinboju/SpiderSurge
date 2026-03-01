using HarmonyLib;
using System;

namespace SpiderSurge;

[HarmonyPatch(typeof(EOSLeaderboards), "SetRecord", [typeof(uint), typeof(int)])]
public class EOSLeaderboards_SetRecord_Patch
{
    [HarmonyPrefix]
    public static bool Prefix()
    {
        if (SurgeGameModeManager.Instance != null && SurgeGameModeManager.Instance.IsActive)
        {
            return false; // Skip leaderboard submission for Surge mode
        }
        return true;
    }
}

[HarmonyPatch(typeof(EOSLeaderboards), "GetRecordsText")]
public class EOSLeaderboards_GetRecordsText_Patch
{
    [HarmonyPrefix]
    public static bool Prefix(ref string __result)
    {
        if (ModConfig.enableSurgeMode)
        {
            __result = "";
            return false;
        }
        return true;
    }
}

[HarmonyPatch(typeof(SonyLeaderboards), "GetRecordsText")]
public class SonyLeaderboards_GetRecordsText_Patch
{
    [HarmonyPrefix]
    public static bool Prefix(ref string __result)
    {
        if (ModConfig.enableSurgeMode)
        {
            __result = "";
            return false;
        }
        return true;
    }
}
