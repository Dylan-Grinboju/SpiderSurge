using HarmonyLib;

namespace SpiderSurge.Patches
{
    [HarmonyPatch(typeof(Announcer), nameof(Announcer.ModsPopup))]
    public static class SilkMenuPatches
    {
        public static void Postfix(string title, ref ModsMenuPopup __result)
        {
            if (title == "SpiderSurge" && __result != null)
            {
                AddSpiderSurgeInfo(__result);
            }
        }

        private static void AddSpiderSurgeInfo(ModsMenuPopup menu)
        {
            bool canToggle = !IsAnySurvivalSessionActive();

            menu.CreateParagraph("<size=200%>\n<b><color=#FFD700>Welcome to SpiderSurge!</color></b></size>");
            menu.CreateParagraph($"<size=115%><b>Surge Mode Status:</b> {(ModConfig.enableSurgeMode ? "<color=#00FF00>Enabled</color>" : "<color=#FF5555>Disabled</color>")}</size>");
            menu.CreateParagraph("<size=95%><color=#AAAAAA>Changes apply to the next run.</color></size>");
            if (canToggle)
            {
                int childCountBefore = menu.objectParent != null ? menu.objectParent.childCount : 0;
                menu.CreateButton(ModConfig.enableSurgeMode ? "Disable Surge Mode" : "Enable Surge Mode", () => ToggleSurgeMode(menu));
                ResizeLastCreatedButton(menu, childCountBefore);
            }
            else
            {
                menu.CreateParagraph("<size=95%><color=#FFAA66>Toggle locked while a session is active.</color></size>");
            }

            // Custom thin divider with more space
            menu.CreateParagraph("<size=100%>\n </size>");

            menu.CreateParagraph("<size=130%><color=#AAAAAA>Check the config file for detailed settings!</color></size>");
            menu.CreateParagraph("<size=100%>\n </size>");
            menu.CreateParagraph("<size=120%><b>Credits:</b>\n</size>");
            menu.CreateParagraph("<size=120%>Icons by these artists from game-icons.net:\n</size>");
            menu.CreateParagraph(" • delapouite\n");
            menu.CreateParagraph(" • lorc\n");
            menu.CreateParagraph(" • pierre-leducq\n");
            menu.CreateParagraph("Licence CC BY 3.0 for the icons can be found here: https://creativecommons.org/licenses/by/3.0/");
        }

        private static void ToggleSurgeMode(ModsMenuPopup menu)
        {
            if (IsAnySurvivalSessionActive())
            {
                Announcer.InformationPopup("Cannot change Surge Mode while a Survival run is active.");
                RefreshMenu(menu);
                return;
            }

            bool newValue = !ModConfig.enableSurgeMode;
            ModConfig.SetEnableSurgeMode(newValue);
            GameModePatches.UpdateSurgeSurvivalText();
            RefreshMenu(menu);
        }

        private static bool IsAnySurvivalSessionActive()
        {
            return SurvivalMode.instance != null && SurvivalMode.instance.GameModeActive();
        }

        private static void RefreshMenu(ModsMenuPopup menu)
        {
            if (menu == null || menu.objectParent == null)
            {
                return;
            }

            for (int i = menu.objectParent.childCount - 1; i >= 0; i--)
            {
                var child = menu.objectParent.GetChild(i);
                if (child != null)
                {
                    UnityEngine.Object.Destroy(child.gameObject);
                }
            }

            AddSpiderSurgeInfo(menu);
        }

        private static void ResizeLastCreatedButton(ModsMenuPopup menu, int childCountBefore)
        {
            if (menu == null || menu.objectParent == null)
            {
                return;
            }

            if (menu.objectParent.childCount <= childCountBefore)
            {
                return;
            }

            var buttonTransform = menu.objectParent.GetChild(menu.objectParent.childCount - 1);
            if (buttonTransform == null)
            {
                return;
            }

            var button = buttonTransform.GetComponent<UnityEngine.UI.Button>();
            if (button == null)
            {
                return;
            }

            var rect = button.GetComponent<UnityEngine.RectTransform>();
            if (rect != null)
            {
                rect.sizeDelta = new UnityEngine.Vector2(rect.sizeDelta.x * 1.1f, rect.sizeDelta.y * 1.45f);
            }

            var label = button.GetComponentInChildren<TMPro.TMP_Text>();
            if (label != null)
            {
                label.fontSize *= 1.2f;
            }
        }
    }
}
