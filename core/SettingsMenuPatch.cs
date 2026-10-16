using Doozy.Engine.UI.Input;
using HarmonyLib;
using UnityEngine.UI;

namespace SpiderSurge.Common
{
    [HarmonyPatch(typeof(ModsMenu), nameof(ModsMenu.CreateButton))]
    public static class ModsMenu_SettingsButton_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(ModsMenu __instance, string buttonName, Button __result)
        {
            if (__instance is ModsMenuPopup || buttonName != SpiderSurgeMod.ModName || __result == null)
            {
                return;
            }
            __result.onClick.RemoveAllListeners();
            __result.onClick.AddListener(() =>
            {
                var page = __instance.GetComponent<SettingsPage>() ?? __instance.gameObject.AddComponent<SettingsPage>();
                page.Build(__instance, __result);
            });
        }
    }

    // The Back button, and cancel on any UIButton, both call BackButton.Execute
    [HarmonyPatch(typeof(BackButton), nameof(BackButton.Execute))]
    public static class Settings_BackInput_Patch
    {
        [HarmonyPrefix]
        public static bool Prefix() => !SettingsPage.HandleBack();
    }

    [HarmonyPatch(typeof(HudController), nameof(HudController.Pause))]
    public static class Settings_PauseInput_Patch
    {
        [HarmonyPrefix]
        public static bool Prefix() => !SettingsPage.HandleBack();
    }
}
