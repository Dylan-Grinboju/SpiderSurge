using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using SpiderSurge.Common;

namespace StatsMod
{
    internal static class StatsSettingsPage
    {
        private const string ColumnsPage = "stats.columns";
        internal const string TitlesPage = "stats.titles";
        private const string AccuracyPrefix = "accuracy.";
        private const string OverallAccuracy = AccuracyPrefix + "overall";

        private static readonly (string title, string prefix)[] ColumnGroups =
        {
            ("Totals", "total"),
            ("Enemy kills", "enemyKills."),
            ("Weapon kills", "weaponKills."),
            ("Shooting accuracy", AccuracyPrefix),
            ("Player stats", ""),
        };

        public static void Build(SettingsPage page)
        {
            if (page.Current == ColumnsPage || page.Current.StartsWith(ColumnsPage + ".", System.StringComparison.Ordinal))
            {
                BuildColumns(page);
                return;
            }
            if (page.Current == TitlesPage)
            {
                BuildTitles(page);
                return;
            }

            page.Title("Stats");
            page.Header("Windows");
            page.Toggle("Show the stats windows", Key("display.showStatsWindow"));
            page.Note($"Small window: {KeyLabel(Controls.SmallWindow)}. Big window: {KeyLabel(Controls.BigWindow)}. Titles: {KeyLabel(Controls.TitlesWindow)}. Switch windows on a controller: <noparse>{Controls.Label(ModConfig.SwitchWindowsBinding)}</noparse>.");
            page.Button("Change these keys", () => page.Open(ControlsPage.Page));
            page.Toggle("Small window background", Key("display.smallUIShowBackground"));
            page.Toggle("Scale with the screen size", ModConfig.AutoScaleKey);
            page.Slider("Size", ModConfig.UIScaleKey, ModConfig.MinScale, ModConfig.MaxScale, 0.1f, "x");
            page.Slider("Big window opacity", Key("display.bigUIOpacity"), 0f, 100f, 5f, "%");

            page.Spacer();
            page.Header("Big window");
            page.Toggle("Run time", Key("display.showPlayTime"));
            page.Toggle("Enemies killed", Key("display.showEnemyDeaths"));
            page.Toggle("Player table", Key("display.showPlayers"));
            page.Button("Player table columns", () => page.Open(ColumnsPage));

            page.Spacer();
            page.Header("End of run");
            page.Toggle("Titles", Key("titles.enabled"));
            page.Slider("Title reveal delay", Key("titles.revealDelaySeconds"), 0f, ModConfig.MaxRevealDelay, 0.5f, " s");
            page.Toggle("Save each run's stats to a file", Key("tracking.saveStatsToFile"));

            page.Spacer();
            page.ConfirmButton("Reset Stats settings to defaults", () => Settings.Reset("stats", ModConfig.PositionXKey, ModConfig.PositionYKey));
        }

        private static void BuildColumns(SettingsPage page)
        {
            page.Title("Player table columns");
            if (page.Current == ColumnsPage)
            {
                page.Note("Choose a group of columns. The player's name is always shown.");
                for (int i = 0; i < ColumnGroups.Length; i++)
                {
                    string destination = ColumnsPage + "." + i;
                    page.Button(ColumnGroups[i].title, () => page.Open(destination));
                }
                page.Spacer();
                page.ConfirmButton("Reset the columns to defaults", () => Settings.Reset(ModConfig.ColumnsPrefix.TrimEnd('.')));
                return;
            }
            page.Note("The player's name is always shown.");

            var columns = Settings.Keys.Where(key => key.StartsWith(ModConfig.ColumnsPrefix)).Select(key => key.Substring(ModConfig.ColumnsPrefix.Length)).ToList();
            for (int i = 0; i < ColumnGroups.Length; i++)
            {
                var (title, prefix) = ColumnGroups[i];
                var group = columns.Where(column => column.StartsWith(prefix))
                    .OrderBy(column => column != OverallAccuracy)
                    .ThenBy(ColumnLabel, System.StringComparer.OrdinalIgnoreCase).ToList();
                columns.RemoveAll(group.Contains);
                if (page.Current != ColumnsPage + "." + i) continue;

                page.Title(title + " columns");
                if (prefix == AccuracyPrefix)
                {
                    page.Note("Accuracy is the percentage of shots that hit an enemy or a versus opponent. Each shot counts once; shields, armor and knockback count. Friendly fire does not.");
                }
                foreach (string column in group)
                {
                    page.Toggle(ColumnLabel(column), ModConfig.ColumnsPrefix + column);
                }
            }
        }

        private static void BuildTitles(SettingsPage page)
        {
            page.Title("Titles");
            if (!TitleCollection.Readable)
            {
                page.Warning($"The title collection can't be read right now. Close any program that has <noparse>{TitleCollection.FilePath}</noparse> open, then open this page again.");
                return;
            }

            var unlocked = TitleCollection.Unlocked;
            var catalog = TitleCatalog.All;
            var locked = catalog.Where(title => !unlocked.ContainsKey(title.Name)).ToList();
            var others = unlocked.Values.Where(title => !catalog.Any(known => known.Name == title.Name)).OrderBy(title => title.FirstEarnedUtc).ToList();
            page.Note($"{catalog.Count - locked.Count} of {catalog.Count} unlocked. A title unlocks when it shows on the end screen of a Survival run with two or more players.");

            page.Spacer();
            page.Header("Unlocked");
            if (unlocked.Count == 0) page.Note("None yet.");
            foreach (var title in catalog.Where(title => unlocked.ContainsKey(title.Name)))
            {
                page.Line(UnlockedLine(title.Name, title.HowToEarn, unlocked[title.Name]));
            }
            foreach (var title in others)
            {
                page.Line(UnlockedLine(title.Name, "From another mod or an older version", title));
            }

            page.Spacer();
            page.Header("Locked");
            if (locked.Count == 0) page.Note("Every title is unlocked.");
            else page.Note("Click a locked title to see how to earn it.");
            foreach (var title in locked)
            {
                string name = title.Name;
                string requirements = page.Revealed(name) ? $"\n<size=90%><noparse>{title.HowToEarn}</noparse></size>" : "";
                page.Line($"<color={SettingsPage.Gray}><b><noparse>{name}</noparse></b>{requirements}</color>", () => page.ToggleRevealed(name));
            }

            page.Spacer();
            page.ConfirmButton("Reset the title collection", TitleCollection.Reset);
            page.Note($"Saved in <noparse>{TitleCollection.FilePath}</noparse>");
        }

        internal static string TitlesButtonLabel => TitleCollection.Readable
            ? $"Titles ({TitleCatalog.All.Count(title => TitleCollection.Unlocked.ContainsKey(title.Name))} of {TitleCatalog.All.Count})"
            : "Titles";

        private static string UnlockedLine(string name, string howToEarn, UnlockedTitle title)
        {
            string times = title.TimesEarned == 1 ? "Earned once" : $"Earned in {title.TimesEarned} runs";
            string first = title.FirstEarnedUtc.ToLocalTime().ToString("d MMM yyyy", CultureInfo.InvariantCulture);
            return $"<b><noparse>{name}</noparse></b>\n<size=90%><color={SettingsPage.Gray}><noparse>{howToEarn}</noparse></color>\n<color={SettingsPage.Green}>{times}, first on {first}</color></size>";
        }

        private static string Key(string key) => ModConfig.Prefix + key;

        private static string KeyLabel(Control control) => $"<noparse>{Controls.Label(control)}</noparse>";

        private static string ColumnLabel(string column)
        {
            string name = column.Substring(column.LastIndexOf('.') + 1);
            if (column == OverallAccuracy) return "Overall accuracy";
            if (column.StartsWith(AccuracyPrefix))
            {
                string label = WeaponAccuracy.Weapons.FirstOrDefault(weapon => weapon.Key == name).Label;
                if (label != null) return label;
            }
            return Words(name);
        }

        // "maxSoloKillStreak" -> "Max solo kill streak"
        private static string Words(string camel)
        {
            var text = new StringBuilder();
            foreach (char c in camel)
            {
                if (char.IsUpper(c) && text.Length > 0)
                {
                    text.Append(' ').Append(char.ToLowerInvariant(c));
                }
                else
                {
                    text.Append(text.Length == 0 ? char.ToUpperInvariant(c) : c);
                }
            }
            return text.ToString();
        }
    }
}
