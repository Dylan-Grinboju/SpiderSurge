using System;
using HarmonyLib;
using Logger = Silk.Logger;

namespace SpiderSurge.Common
{
    [HarmonyPatch(typeof(Announcer), nameof(Announcer.ModsPopup))]
    public static class Announcer_ModsPopup_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(string title, ModsMenuPopup __result)
        {
            try
            {
                if (__result == null)
                {
                    return;
                }

                SettingsPage page = __result.GetComponent<SettingsPage>();
                if (title != SpiderSurgeMod.ModName)
                {
                    if (page != null)
                    {
                        page.Clear();
                    }
                    return;
                }

                if (page == null)
                {
                    page = __result.gameObject.AddComponent<SettingsPage>();
                }
                page.Build(__result);
            }
            catch (Exception ex)
            {
                Logger.LogError($"SpiderSurge could not build its Mods page: {ex}");
            }
        }
    }
}
