using System;
using HarmonyLib;
using Logger = Silk.Logger;

namespace SpiderTeams
{
    [HarmonyPatch(typeof(Announcer), nameof(Announcer.ModsPopup))]
    public static class Announcer_ModsPopup_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(string title, ModsMenuPopup __result)
        {
            try
            {
                if (title != SpiderTeamsMod.ModName || __result == null)
                {
                    return;
                }

                SpiderTeamsMenuPage page = __result.GetComponent<SpiderTeamsMenuPage>();
                if (page == null)
                {
                    page = __result.gameObject.AddComponent<SpiderTeamsMenuPage>();
                }
                page.Build(__result);
            }
            catch (Exception ex)
            {
                Logger.LogError($"SpiderTeams could not build its Mods page: {ex}");
            }
        }
    }
}
