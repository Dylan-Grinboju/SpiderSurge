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
            menu.CreateParagraph("<size=200%>\n<b><color=#FFD700>Welcome to SpiderSurge!</color></b></size>");
            menu.CreateParagraph("<size=130%>\nThis mod expands your toolkit with powerful new abilities and mechanics:\n</size>");

            menu.CreateParagraph(" • <b>Shield:</b> Block incoming damage and push enemies back.\n");
            menu.CreateParagraph(" • <b>Explosion:</b> Release a devastating blast around you.\n");
            menu.CreateParagraph(" • <b>Storage:</b> Store and recall weapons at will.\n");
            menu.CreateParagraph(" • <b>Infinite Ammo:</b> Keep firing without depletion for a duration.\n</size>");

            // Custom thin divider with more space
            menu.CreateParagraph("<size=100%>\n </size>");

            menu.CreateParagraph("<size=140%><b>Core Features:</b>\n</size>");
            menu.CreateParagraph("<size=120%> • Ultimate Upgrades at Waves 30 & 60\n");
            menu.CreateParagraph(" • Custom Synergies between abilities\n");
            menu.CreateParagraph(" • New custom enemy variants\n</size>");

            menu.CreateParagraph("<size=100%>\n </size>");

            menu.CreateParagraph("<size=110%><color=#AAAAAA>Check the config file for detailed settings!</color></size>");
        }
    }
}
