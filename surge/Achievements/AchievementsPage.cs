using SpiderSurge.Common;

namespace SpiderSurge
{
    public static class AchievementsPage
    {
        public const string Page = "surge.achievements";

        public static void Build(SettingsPage page)
        {
            page.Title("Achievements");
            page.Note($"{SurgeAchievements.UnlockedCount} of {SurgeAchievements.All.Length} unlocked. They are saved with this game's save file.");

            foreach (Achievement achievement in SurgeAchievements.All)
            {
                AchievementUnlock unlock = SurgeAchievements.UnlockOf(achievement.Id);
                string status = unlock != null
                    ? $"<color={SettingsPage.Green}>UNLOCKED</color> <color={SettingsPage.Gray}>{unlock.Date:yyyy-MM-dd}{(string.IsNullOrEmpty(unlock.Player) ? "" : " by <noparse>" + unlock.Player + "</noparse>")}</color>"
                    : $"<color={SettingsPage.Gray}>LOCKED</color>";
                string name = unlock != null ? $"<color={Hud.PinkHex}>{achievement.Name}</color>" : achievement.Name;
                page.Line($"<b>{name}</b>\n<size=90%>{achievement.Description}</size>\n<size=85%>{status}</size>");
            }

            page.Spacer();
            page.ConfirmButton("Reset all achievements", SurgeAchievements.ResetAll);
        }
    }
}
