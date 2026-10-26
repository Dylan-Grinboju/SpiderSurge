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

            if (page.Current == "stats.presets")
            {
                page.Title("Scoreboard presets");
                page.Note("Default is recommended and always available. Only Default uses your customized columns.");
                page.Button("Customize Default columns", () => page.Open(ColumnsPage));
                for (int i = 1; i < BigUI.PresetKeys.Length; i++)
                    page.Toggle(BigUI.PresetNames[i], "stats.display.presets." + BigUI.PresetKeys[i]);
                return;
            }
            page.Title("Stats");
            page.Header("Windows");
            page.Toggle("Show stats and titles", Key("display.showStatsWindow"));
            page.Note("Hiding Stats prevents its windows and title cards from appearing. Tracking stays on for Surge and run results.");
            page.Note($"Small window: {Controls.Display(Controls.SmallWindow)}. Big window: {Controls.Display(Controls.BigWindow)}. Titles: {Controls.Display(Controls.TitlesWindow)}. Switch windows on a controller: {Controls.Display(ModConfig.SwitchWindowsBinding)}.");
            page.Button("Change these keys", () => page.Open(ControlsPage.Page));
            page.Note("Choose keys for the three stats windows and controller buttons for switching windows and scoreboard presets.");
            page.Toggle("Small window background", Key("display.smallUIShowBackground"));
            page.Note("Adds a background behind the small stats window to make its text easier to read over the arena.");
            page.Toggle("Scale with the screen size", ModConfig.AutoScaleKey);
            page.Note("Keeps stats windows proportional to the screen resolution. Size applies an additional scale multiplier.");
            page.Slider("Size", ModConfig.UIScaleKey, ModConfig.MinScale, ModConfig.MaxScale, 0.1f, "x", ModConfig.UIScale);
            page.Note("Scales the stats windows. This does not change the size of the settings menu.");
            page.Slider("Big window opacity", Key("display.bigUIOpacity"), 0f, 100f, 5f, "%", ModConfig.BigUIOpacity);
            page.Note("Adjusts the full scoreboard's background opacity. A higher value makes the statistics easier to read over gameplay.");

            page.Spacer();
            page.Header("Big window");
            page.Toggle("Run time", Key("display.showPlayTime"));
            page.Note("Shows elapsed run time in the full scoreboard.");
            page.Toggle("Enemies killed", Key("display.showEnemyDeaths"));
            page.Note("Shows the enemy kill total in the full scoreboard.");
            page.Toggle("Player table", Key("display.showPlayers"));
            page.Note("Shows each player's statistics in the full scoreboard.");
            page.Button("Scoreboard presets", () => page.Open("stats.presets"));
            page.Note("Choose which fixed scoreboard presets are available. Default always stays available and uses your customized columns.");
            page.Button("Player table columns", () => page.Open(ColumnsPage));
            page.Note("Choose the columns in the Default scoreboard directly. Other presets keep their fixed columns.");

            page.Spacer();
            page.Header("End of run");
            page.Toggle("Titles", Key("titles.enabled"));
            page.Note("Shows performance titles after a Survival run with at least two players. Titles enter the collection when they appear on the results screen.");
            page.Slider("Title reveal delay", Key("titles.revealDelaySeconds"), 0f, ModConfig.MaxRevealDelay, 0.5f, " s", ModConfig.TitlesRevealDelaySeconds);
            page.Note("Time between title reveals at the end of a run. The titles key skips the reveal sequence.");
            page.Toggle("Save each run's stats to a file", Key("tracking.saveStatsToFile"));
            page.Note("Writes a local text report of the run's statistics. This is separate from match history and sharing anonymous reports.");
            page.Toggle("Save match history", Key("tracking.saveHistory"));
            page.Note("Keeps Survival and Versus results for Match history. Existing records remain available when this is off. No upload is required.");

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
            CollectionSeen.Ensure("titles", unlocked.Keys);
            int filter = CollectionFilters.Add(page, "titles");
            page.Note($"{catalog.Count(title => unlocked.ContainsKey(title.Name))} of {catalog.Count} unlocked. Titles unlock when shown after a Survival run with two or more players.");
            var rows = catalog.Select(title => (Name: title.Name, How: title.HowToEarn))
                .Concat(unlocked.Keys.Where(name => !catalog.Any(title => title.Name == name)).Select(name => (Name: name, How: "From another mod or an older version")));
            if (filter == 3) rows = rows.OrderByDescending(row => unlocked.TryGetValue(row.Name, out var title) ? title.LastEarnedUtc : System.DateTime.MinValue);
            int shown = 0;
            foreach (var row in rows)
            {
                bool earned = unlocked.TryGetValue(row.Name, out var title);
                if ((filter == 1 && earned) || (filter >= 2 && !earned)) continue;
                shown++;
                string name = row.Name;
                if (earned)
                    page.Line(UnlockedLine(name, row.How, title) + CollectionFilters.Badge("titles", name), () => CollectionSeen.Mark("titles", new[] { name }));
                else
                {
                    string how = page.Revealed(name) ? $"\n<size=90%><noparse>{row.How}</noparse></size>" : "";
                    page.Line($"<color={SettingsPage.Gray}><b><noparse>{name}</noparse></b>{how}</color>", () => page.ToggleRevealed(name));
                }
            }
            if (shown == 0) page.Note("No titles in this view.");
            page.Spacer();
            page.Button("Mark all seen", () => CollectionSeen.Mark("titles", unlocked.Keys));
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
