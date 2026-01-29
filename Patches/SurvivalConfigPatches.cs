using HarmonyLib;

namespace SpiderSurge
{
    [HarmonyPatch(typeof(SurvivalConfig), "GetHighScore")]
    public class SurvivalConfig_GetHighScore_Patch
    {
        [HarmonyPrefix]
        public static bool Prefix(SurvivalConfig __instance, ref float __result)
        {
            if (ModConfig.enableSurgeMode || __instance.name.Contains("_Surge"))
            {
                string baseName = __instance.name.Replace("_Surge", "");
                __result = GameSaveWrapper.Instance.Load(baseName + "-SurgeHS", 0f);
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
            if (ModConfig.enableSurgeMode || __instance.name.Contains("_Surge"))
            {
                string baseName = __instance.name.Replace("_Surge", "");
                GameSaveWrapper.Instance.Save(baseName + "-SurgeHS", score);
                return false; // Prevent original save
            }
            return true;
        }
    }
}