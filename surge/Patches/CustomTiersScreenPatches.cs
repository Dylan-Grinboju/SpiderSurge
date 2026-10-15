using HarmonyLib;

namespace SpiderSurge.Patches
{
    [HarmonyPatch(typeof(CustomTiersScreen), nameof(CustomTiersScreen.Start))]
    public static class CustomTiersScreen_Start_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(CustomTiersScreen __instance)
        {
            if (__instance.allElements != null)
            {
                Enemies.CustomEnemies.InitializeFromLists(__instance.allElements);
            }
        }
    }
}
