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

            if (page.Current == "stats.appearance") { BuildAppearance(page); return; }
            if (page.Current == "stats.presets")
            {
                page.Title("Customize scoreboard");
                page.Button("Edit My scoreboard columns", () => page.Open(ColumnsPage));
                page.Note("My scoreboard and your starting scoreboard stay available. These switches hide other fixed presets from the page cycle.");
                for (int i = 1; i < BigUI.PresetKeys.Length; i++)
                    page.Toggle(BigUI.PresetNames[i], "stats.display.presets." + BigUI.PresetKeys[i]);
                return;
            }
            page.Title("Stats");
            page.Toggle("Show stats and results", Key("display.showStatsWindow"));
            page.Note($"Small stats: {Controls.Display(Controls.SmallWindow)}. Scoreboard: {Controls.Display(Controls.BigWindow)}. Results: {Controls.Display(Controls.TitlesWindow)}. Tracking continues when hidden.");
            page.Slider("Size", ModConfig.UIScaleKey, ModConfig.MinScale, ModConfig.MaxScale, 0.1f, "x", ModConfig.UIScale);
            page.Note("Windows scale with the screen. This adjusts their size on your display.");
            page.Cycle("Starting scoreboard", Key("display.startingPreset"), BigUI.PresetKeys, BigUI.PresetNames);
            page.Note("Choose the scoreboard shown when you open it. Switch pages with the arrows, mouse wheel or controller D-pad left / right.");
            page.Button("Customize scoreboard", () => page.Open("stats.presets"));
            page.Toggle("End-of-run titles", Key("titles.enabled"));
            page.Note("Reveal performance titles after Survival with two or more players. The run recap remains available in Results.");
            page.Button("Display and reveal options", () => page.Open("stats.appearance"));
            page.ConfirmButton("Reset Stats settings to defaults", () => Settings.Reset("stats", ModConfig.PositionXKey, ModConfig.PositionYKey));
        }

        private static void BuildAppearance(SettingsPage page)
        {
            page.Title("Stats display options");
            page.Toggle("Small window background", Key("display.smallUIShowBackground"));
            page.Note("Improve contrast over the arena.");
            page.Slider("Scoreboard opacity", Key("display.bigUIOpacity"), 0f, 100f, 5f, "%", ModConfig.BigUIOpacity);
            page.Toggle("Run time", Key("display.showPlayTime"));
            page.Toggle("Enemies killed", Key("display.showEnemyDeaths"));
            page.Toggle("Player table", Key("display.showPlayers"));
            page.Slider("Title reveal delay", Key("titles.revealDelaySeconds"), 0f, ModConfig.MaxRevealDelay, 0.5f, " s", ModConfig.TitlesRevealDelaySeconds);
            page.Note("The Results key skips the reveal sequence.");
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

        // Stats owns the report choice; while Surge runs the same checkbox also sets Surge's report setting
        public static void BuildRecords(SettingsPage page)
        {
            page.Toggle("Keep match history", ModConfig.SaveHistoryKey);
            page.Note("Keep completed match results on this PC. Turning this off keeps your existing history.");
            page.Checkbox("Export detailed reports", Settings.GetBool("stats.tracking.saveStatsToFile") || (Parts.Surge.Running && SpiderSurge.ModConfig.EnableStatsLogging), value =>
            {
                Settings.Set("stats.tracking.saveStatsToFile", value);
                if (Parts.Surge.Running) Settings.Set(SpiderSurge.ModConfig.StatsLoggingKey, value);
            });
            page.Note("Write detailed Stats and Surge text reports on this PC. Match history and anonymous sharing are separate choices.");
        }

        internal static string TitlesButtonLabel =>TitleCollection.Readable
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
