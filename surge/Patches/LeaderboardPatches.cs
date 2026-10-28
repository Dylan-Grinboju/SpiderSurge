using HarmonyLib;
using System;

namespace SpiderSurge
{
    [HarmonyPatch(typeof(EOSLeaderboards), "SetRecord", new Type[] { typeof(uint), typeof(int) })]
    public class EOSLeaderboards_SetRecord_Patch
    {
        [HarmonyPrefix]
        public static bool Prefix()
        {
            if (SurgeGameModeManager.IsSurgeRunActive || SurgeGameModeManager.WasSurgeRunUsed)
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
        public static bool Prefix(ref string __result) => LeaderboardPatches.HideRecords(ref __result);
    }

    [HarmonyPatch(typeof(SonyLeaderboards), "GetRecordsText")]
    public class SonyLeaderboards_GetRecordsText_Patch
    {
        [HarmonyPrefix]
        public static bool Prefix(ref string __result) => LeaderboardPatches.HideRecords(ref __result);
    }

    public static class LeaderboardPatches
    {
        public static bool HideRecords(ref string records)
        {
            if (SurgeGameModeManager.IsModeEnabled || SurgeGameModeManager.IsSurgeRunActive)
            {
                records = "";
                return false;
            }
            return true;
        }
    }
}
