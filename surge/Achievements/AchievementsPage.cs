using SpiderSurge.Common;

namespace SpiderSurge
{
    public static class AchievementsPage
    {
        public const string Page = "surge.achievements";

        public static void Build(SettingsPage page)
        {
            page.Title("Surge achievements");
            page.Note($"{SurgeAchievements.UnlockedCount} of {SurgeAchievements.All.Length} unlocked. Click a locked achievement to see how to get it.");

            foreach (Achievement achievement in SurgeAchievements.All)
            {
                AchievementUnlock unlock = SurgeAchievements.UnlockOf(achievement.Id);
                if (unlock != null)
                {
                    string by = string.IsNullOrEmpty(unlock.Player) ? "" : " by <noparse>" + unlock.Player + "</noparse>";
                    page.Line($"<b><color={Hud.PinkHex}>{achievement.Name}</color></b>\n<size=90%>{achievement.Description}</size>\n<size=85%><color={SettingsPage.Green}>UNLOCKED</color> <color={SettingsPage.Gray}>{unlock.Date:yyyy-MM-dd}{by}</color></size>");
                    continue;
                }

                string id = achievement.Id;
                string howTo = page.Revealed(id) ? $"\n<size=90%>{achievement.Description}</size>" : "";
                page.Line($"<b>{achievement.Name}</b>{howTo}\n<size=85%><color={SettingsPage.Gray}>LOCKED</color></size>", () => page.ToggleRevealed(id));
            }

            page.Spacer();
            page.ConfirmButton("Reset all achievements", SurgeAchievements.ResetAll);
            page.Note($"Saved in <noparse>{ModData.FilePath}</noparse>");
        }
    }
}
