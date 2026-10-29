using HarmonyLib;
using SpiderSurge.Common;

namespace SpiderSurge.Fixes
{
    // The game keeps the last Survival run's pain level through Tiers of Heck and Parkour, whose waves then counted for that level's records
    internal static class StalePainLevelFix
    {
        internal static bool OutsideSurvival(SurvivalMode mode)
        {
            SurvivalConfig config = GameFields.ActiveConfig(mode);
            return config != null && config.type != SurvivalConfig.Type.EndlessSurvival;
        }
    }

    // The wave-start high score update asks for the pain level's config; outside Survival it gets the running mode's own, which keeps no high score
    [HarmonyPatch(typeof(SurvivalMode), nameof(SurvivalMode.GetSurvivalConfig))]
    internal static class StalePainLevelConfigPatch
    {
        static bool Prefix(SurvivalMode __instance, int painLevel, ref SurvivalConfig __result)
        {
            if (painLevel > 0 || !StalePainLevelFix.OutsideSurvival(__instance)) return true;
            __result = GameFields.ActiveConfig(__instance);
            return false;
        }
    }

    // Losing the last life sends the wave to the pain level's online leaderboard
    [HarmonyPatch(typeof(SurvivalMode), "SetRecordClientRpc")]
    internal static class StalePainLevelRecordPatch
    {
        static bool Prefix(SurvivalMode __instance) => !StalePainLevelFix.OutsideSurvival(__instance);
    }
}
