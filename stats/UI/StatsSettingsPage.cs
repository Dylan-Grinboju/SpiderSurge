using System.Linq;
using System.Text;
using SpiderSurge.Common;

namespace StatsMod
{
    internal static class StatsSettingsPage
    {
        private const string ColumnsPage = "stats.columns";
        private static readonly string[] CycleInputNames = { "D-pad", "Stick press (L3 or R3)", "D-pad or stick press", "Nothing" };

        private static readonly (string title, string prefix)[] ColumnGroups =
        {
            ("Totals", "total"),
            ("Enemy kills", "enemyKills."),
            ("Weapon kills", "weaponKills."),
            ("Shooting accuracy", "accuracy."),
            ("Player stats", ""),
        };

        public static void Build(SettingsPage page)
        {
            if (page.Current == ColumnsPage)
            {
                BuildColumns(page);
                return;
            }

            page.Title("Stats");
            page.Header("Windows");
            page.Toggle("Show the stats windows", Key("display.showStatsWindow"));
            page.Note("F1 shows the small window, F2 the big one and F3 the titles. On a controller, the button below switches between them.");
            page.Cycle("Switch windows with", Key("input.cycleInput"), ModConfig.CycleInputs, CycleInputNames);
            if (ModConfig.DpadTakenBySurge && ModConfig.ConfiguredCycleInput != ModConfig.CycleInput)
            {
                page.Warning("Surge's ultimate is on the D-pad, so the D-pad doesn't switch windows.");
            }
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
            page.Button("Reset Stats settings to defaults", () => Settings.Reset("stats", Key("display.position.x"), Key("display.position.y")));
        }

        private static void BuildColumns(SettingsPage page)
        {
            page.Title("Player table columns");
            page.Note("The player's name is always shown.");
            page.Note("Accuracy is the percentage of shots that hit an enemy or a versus opponent. Each shot counts once; shields, armor and knockback count. Friendly fire does not.");

            var columns = Settings.Keys.Where(key => key.StartsWith(ModConfig.ColumnsPrefix)).Select(key => key.Substring(ModConfig.ColumnsPrefix.Length)).ToList();
            foreach (var (title, prefix) in ColumnGroups)
            {
                var group = columns.Where(column => column.StartsWith(prefix)).ToList();
                columns.RemoveAll(group.Contains);

                page.Spacer();
                page.Header(title);
                foreach (string column in group)
                {
                    string name = column.Substring(column.LastIndexOf('.') + 1);
                    string label = prefix == "accuracy."
                        ? (name == "overall" ? "Overall accuracy" : WeaponAccuracy.Weapons.FirstOrDefault(weapon => weapon.Key == name).Label)
                        : null;
                    page.Toggle(label ?? Words(name), ModConfig.ColumnsPrefix + column);
                }
            }

            page.Spacer();
            page.Button("Reset the columns to defaults", () => Settings.Reset(ModConfig.ColumnsPrefix.TrimEnd('.')));
        }

        private static string Key(string key) => ModConfig.Prefix + key;

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
