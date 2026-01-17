using HarmonyLib;
using Silk;
using Logger = Silk.Logger;

namespace SpiderSurge
{
    // Harmony patch to initialize abilities when players spawn
    [HarmonyPatch(typeof(SpiderController), "Start")]
    public class SpiderController_Start_Patch
    {
        static void Postfix(SpiderController __instance)
        {
            try
            {
                AbilityManager.InitializePlayerAbilities(__instance.gameObject);
            }
            catch (System.Exception ex)
            {
                Logger.LogError($"Error in SpiderController Start patch: {ex.Message}");
            }
        }
    }
}