using SpiderSurge.Common;
using System.Linq;
using System;

namespace SpiderSurge
{
    public static class AchievementsPage
    {
        public const string Page = "surge.achievements";

        public static void Build(SettingsPage page)
        {
            page.Title("Surge achievements");
            page.Note($"{SurgeAchievements.UnlockedCount} of {SurgeAchievements.All.Length} unlocked. Click a locked achievement to see how to get it.");

            if (!ModData.Readable) { page.Warning("Progress cannot be read right now. Try again shortly."); return; }
            var unlocked = SurgeAchievements.All.Where(a => SurgeAchievements.IsUnlocked(a.Id)).Select(a => a.Id).ToArray();
            CollectionSeen.Ensure("achievements", unlocked);
            int filter = CollectionFilters.Add(page, "achievements");
            var rows = filter == 3 ? SurgeAchievements.All.OrderByDescending(a => SurgeAchievements.UnlockOf(a.Id)?.Date ?? DateTime.MinValue).ToArray() : SurgeAchievements.All;
            int shown = 0;
            foreach (Achievement achievement in rows)
            {
                AchievementUnlock unlock = SurgeAchievements.UnlockOf(achievement.Id);
                if ((filter == 1 && unlock != null) || (filter >= 2 && unlock == null)) continue;
                shown++;
                if (unlock != null)
                {
                    string by = string.IsNullOrEmpty(unlock.Player) ? "" : " by <noparse>" + unlock.Player + "</noparse>";
                    page.Line($"<b><color={Hud.PinkHex}>{achievement.Name}</color></b>\n<size=90%>{achievement.Description}</size>\n<size=85%><color={SettingsPage.Green}>UNLOCKED</color> <color={SettingsPage.Gray}>{unlock.Date:yyyy-MM-dd}{by}</color></size>" + CollectionFilters.Badge("achievements", achievement.Id), () => CollectionSeen.Mark("achievements", new[] { achievement.Id }));
                    continue;
                }

                string id = achievement.Id;
                string howTo = page.Revealed(id) ? $"\n<size=90%>{achievement.Description}</size>" : "";
                page.Line($"<b>{achievement.Name}</b>{howTo}\n<size=85%><color={SettingsPage.Gray}>LOCKED</color></size>", () => page.ToggleRevealed(id));
            }

            if (shown == 0) page.Note("No achievements in this view.");
            page.Button("Mark all seen", () => CollectionSeen.Mark("achievements", unlocked));
            page.Spacer();
            page.ConfirmButton("Reset all achievements", SurgeAchievements.ResetAll);
            page.Note($"Saved in <noparse>{ModData.FilePath}</noparse>");
        }
    }
}
