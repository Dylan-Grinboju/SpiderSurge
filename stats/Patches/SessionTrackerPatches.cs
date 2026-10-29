using System;
using HarmonyLib;
using Silk;
using Logger = Silk.Logger;

namespace StatsMod
{
    [HarmonyPatch(typeof(LevelController), "LoadLevelWithTransition")]
    public class LevelControllerLoadLevelPatch
    {
        static void Postfix(LevelController __instance, GameLevel level)
        {
            try
            {
                if (GameSessionManager.Instance.IsActive && level != null)
                {
                    // Use the localized label if available, otherwise the name
                    string mapName = level.label;
                    if (string.IsNullOrEmpty(mapName))
                    {
                        mapName = level.name;
                    }
                    GameSessionManager.Instance.RecordMap(mapName);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error tracking map load: {ex.Message}");
            }
        }
    }
}
