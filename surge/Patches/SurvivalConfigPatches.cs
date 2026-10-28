using System.Collections.Generic;
using HarmonyLib;

namespace SpiderSurge
{
    [HarmonyPatch(typeof(SurvivalConfig), "GetHighScore")]
    public class SurvivalConfig_GetHighScore_Patch
    {
        private static readonly Dictionary<int, string> SurgeConfigs = new Dictionary<int, string>();

        // Surge's copies of a config are known by instance id, so the per-frame highscore reads don't allocate the config name
        internal static void RegisterSurgeConfig(SurvivalConfig surgeConfig)
        {
            SurgeConfigs[surgeConfig.GetInstanceID()] = surgeConfig.name.Replace("_Surge", "");
        }

        // The name the Surge highscore is kept under, or null when the game's own highscore is used; a remote tier's name comes from the server, so it is still checked
        internal static string SurgeHighScoreName(SurvivalConfig config)
        {
            if (config == null) return null;
            if (SurgeConfigs.TryGetValue(config.GetInstanceID(), out string baseName)) return baseName;
            if ((SurgeGameModeManager.IsModeEnabled && config.type == SurvivalConfig.Type.EndlessSurvival)
                || (config.remoteConfig && config.name.Contains("_Surge")))
                return config.name.Replace("_Surge", "");
            return null;
        }

        [HarmonyPrefix]
        public static bool Prefix(SurvivalConfig __instance, ref float __result)
        {
            string baseName = SurgeHighScoreName(__instance);
            if (baseName != null)
            {
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
            string baseName = SurvivalConfig_GetHighScore_Patch.SurgeHighScoreName(__instance);
            if (baseName != null)
            {
                SurgeHighScores.Set(baseName, score);
                return false; // Prevent original save
            }
            return true;
        }
    }
}
