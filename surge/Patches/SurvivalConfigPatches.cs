using HarmonyLib;

namespace SpiderSurge
{
    [HarmonyPatch(typeof(SurvivalConfig), "GetHighScore")]
    public class SurvivalConfig_GetHighScore_Patch
    {
        internal static bool UseSurgeHighScore(SurvivalConfig config)
        {
            return config != null
                && (config.name.Contains("_Surge")
                    || (SurgeGameModeManager.IsModeEnabled && config.type == SurvivalConfig.Type.EndlessSurvival));
        }

        [HarmonyPrefix]
        public static bool Prefix(SurvivalConfig __instance, ref float __result)
        {
            if (UseSurgeHighScore(__instance))
            {
                string baseName = __instance.name.Replace("_Surge", "");
                __result = SurgeHighScores.Get(baseName);
                return false;
            }
            return true;
        }
    }

    [HarmonyPatch(typeof(SurvivalConfig), "SetHighScore")]
    public class SurvivalConfig_SetHighScore_Patch
    {
        [HarmonyPrefix]
        public static bool Prefix(SurvivalConfig __instance, float score)
        {
            if (SurvivalConfig_GetHighScore_Patch.UseSurgeHighScore(__instance))
            {
                string baseName = __instance.name.Replace("_Surge", "");
                SurgeHighScores.Set(baseName, score);
                return false; // Prevent original save
            }
            return true;
        }
    }
}
