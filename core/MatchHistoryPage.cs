using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace SpiderSurge.Common
{
    internal static class MatchHistoryPage
    {
        private static readonly Dictionary<string, int> pages = new Dictionary<string, int>();
        private static HistoryMatch selected;
        private static string loading;
        public static string Text(string value) => (value ?? "").Replace("<", "‹").Replace(">", "›");
        public static string Badge(HistorySummary s) => s.Surge ? " <color=#FF58A0>◆ " + (s.Mode == "survival" ? "SURGE" : "SURGE ABILITIES") + "</color>" : "";
        private static string Date(string value) => DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var time) ? time.ToLocalTime().ToString("g") : Text(value);

        public static void Build(SettingsPage page)
        {
            if (page.Current == "history")
            {
                page.Title("Match history");
                page.Button("Survival", () => page.Open("history.survival"));
                page.Button("Versus", () => page.Open("history.versus"));
                if (Parts.Stats.Running) page.Toggle("Save match history", "stats.tracking.saveHistory");
                else page.Note("Enable Stats to record new matches. Saved matches remain available here.");
                page.Note("All recorded matches are kept on this PC. History starts with this update; older reports are available in Support > Open logs.");
                page.Button("Refresh history", () => HistoryStore.Load(true));
                page.ConfirmButton("Clear match history", () => { selected = null; loading = null; HistoryStore.Clear(); }, "Clear history", "Keep history");
                return;
            }
            HistoryStore.Load();
            if (!string.IsNullOrEmpty(HistoryStore.Error)) page.Warning(Text(HistoryStore.Error));
            if (page.Current == "history.survival" || page.Current == "history.versus")
            {
                string mode = page.Current.Substring("history.".Length);
                page.Title(mode == "survival" ? "Survival history" : "Versus history");
                var matches = HistoryStore.Entries.Where(m => m.Mode == mode).ToList();
                int number = pages.TryGetValue(mode, out int n) ? n : 0;
                number = Math.Min(number, Math.Max(0, (matches.Count - 1) / 20));
                pages[mode] = number;
                page.Note($"{matches.Count} matches · Page {number + 1} of {Math.Max(1, (matches.Count + 19) / 20)}");
                if (HistoryStore.Busy) page.Note("Loading history…");
                else if (matches.Count == 0) page.Note("No matches recorded yet.");
                foreach (var match in matches.Skip(number * 20).Take(20))
                {
                    string extra = mode == "survival" ? $" · Wave {match.Wave} · Pain {match.Pain}" : "";
                    page.Line($"<b>{Date(match.DateUtc)}</b>{Badge(match)}\n{Duration(match.Seconds)}{extra} · {Text(match.Outcome)}\n{Text(match.Players)}", () => Open(page, match.Id));
                }
                if (number > 0) page.Button("Previous page", () => { pages[mode] = number - 1; SettingsPage.RefreshOpen(); });
                if ((number + 1) * 20 < matches.Count) page.Button("Next page", () => { pages[mode] = number + 1; SettingsPage.RefreshOpen(); });
                page.Button("Refresh", () => HistoryStore.Load(true));
                return;
            }
            if (selected == null)
            {
                page.Title("Match");
                page.Note(loading != null ? "Loading match…" : "This match could not be loaded. Return to the list to try again.");
                return;
            }
            if (page.Current == "history.detail") { BuildDetail(page); return; }
            if (page.Current == "history.titles")
            {
                page.Title("Earned titles");
                if (selected.Titles.Count == 0) page.Note("No titles for this match.");
                foreach (var title in selected.Titles)
                {
                    string player = selected.Players.LastOrDefault(p => p.Id == title.Player)?.Name ?? "Player";
                    page.Line($"<b>{Text(title.Name)}</b>{(title.New ? " <color=#FFD24A>NEW THIS RUN</color>" : "")}\n{Text(player)} · {(title.Shown ? "Shown on the end screen" : "Earned, not displayed")}\n{Text(title.Description)}");
                }
                return;
            }
            if (page.Current == "history.perks")
            {
                page.Title("Maps and perks");
                page.Header("Maps");
                foreach (string map in selected.Maps) page.Line(Text(map));
                page.Header("Perks");
                foreach (string perk in selected.Perks) page.Line(Text(perk));
                if (selected.SurgeStats != null)
                {
                    page.Header("Perk choices");
                    foreach (var choice in selected.SurgeStats.PerkChoices)
                        page.Line($"Wave {choice.Wave}: {Text(choice.Picked)} (level {choice.Level})\nOffered: {Text(string.Join(", ", choice.Offered))}" + (choice.TimedOut ? " · Timed out" : ""));
                    page.Header("Waves");
                    foreach (var wave in selected.SurgeStats.Waves)
                        page.Line($"Wave {wave.Wave}: {Text(wave.Map)} · {Text(wave.Result)} · {Duration(wave.Seconds)}\nDeaths: {wave.Deaths} · Ability uses: {wave.AbilityUses} · Ultimate uses: {wave.UltimateUses}");
                }
                return;
            }
            if (page.Current.StartsWith("history.player."))
            {
                string[] parts = page.Current.Split('.');
                if (!int.TryParse(parts[2], out int index) || index < 0 || index >= selected.Players.Count) return;
                BuildPlayer(page, selected.Players[index], parts.Length > 3 ? parts[3] : "overview", index);
            }
        }
        private static void Open(SettingsPage page, string id)
        {
            selected = null;
            loading = id;
            page.Open("history.detail");
            HistoryStore.Detail(id, match => { if (loading != id) return; selected = match; loading = null; SettingsPage.RefreshOpen(); });
        }
        private static void BuildDetail(SettingsPage page)
        {
            var s = selected.Summary;
            page.Title(s.Mode == "survival" ? "Survival result" : "Versus result");
            page.Line($"<b>{Date(s.DateUtc)}</b>{Badge(s)}\n{Duration(s.Seconds)} · {Text(s.Outcome)}\nSpiderSurge {Text(s.Version)}");
            if (s.Mode == "survival") page.Line($"Wave reached: {s.Wave} · Pain: {s.Pain} · Enemies killed: {selected.EnemiesKilled}" + (selected.SurgeStats != null ? $"\nWaves survived: {selected.SurgeStats.WavesSurvived}" : ""));
            foreach (var team in selected.Teams) page.Line($"{Text(team.Name)}: {Points(team.Score)}");
            for (int i = 0; i < selected.Players.Count; i++)
            {
                int index = i;
                var player = selected.Players[i];
                page.Button(Text(player.Name) + (player.LeftEarly ? " (left early)" : "") + (s.Mode == "versus" ? $" · {Points(player.Score)}" : ""), () => page.Open("history.player." + index));
            }
            page.Button("Maps and perks", () => page.Open("history.perks"));
            page.Button("Earned titles", () => page.Open("history.titles"));
        }
        private static void BuildPlayer(SettingsPage page, HistoryPlayer player, string category, int index)
        {
            page.Title(Text(player.Name));
            if (category == "overview")
            {
                if (player.Team >= 0) page.Note("Team: " + Text(selected.Teams.FirstOrDefault(t => t.Id == player.Team)?.Name));
                foreach (string group in new[] { "stats", "enemies", "weapons", "accuracy", "maps" })
                {
                    string target = group;
                    page.Button(group == "stats" ? "Player statistics" : group == "maps" ? "Deaths by map" : char.ToUpperInvariant(group[0]) + group.Substring(1), () => page.Open("history.player." + index + "." + target));
                }
                var surge = selected.SurgeStats?.PlayerStats.FirstOrDefault(p => p.PlayerId == player.Id);
                if (surge != null) page.Line($"Ability uses: {surge.AbilityActivationCount}\nUltimate uses: {surge.UltimateActivationCount}\nNull Field saves: {surge.Saves}");
                return;
            }
            if (category == "accuracy")
            {
                foreach (var accuracy in player.Accuracy)
                    page.Line($"{Text(accuracy.Weapon)}: {(accuracy.Fired > 0 ? (100.0 * accuracy.Hit / accuracy.Fired).ToString("0.#") + "%" : "--")} ({accuracy.Hit}/{accuracy.Fired})");
                return;
            }
            var rows = category == "enemies" ? player.Enemies : category == "weapons" ? player.Weapons : category == "maps" ? player.DeathsPerMap : player.Stats;
            if (rows.Count == 0) page.Note("None recorded.");
            foreach (var row in rows) page.Line(Text(Words(row.Name)) + ": " + row.Value.ToString("0.##"));
        }
        private static string Points(int count) => count + (count == 1 ? " point" : " points");
        private static string Words(string value) => System.Text.RegularExpressions.Regex.Replace(value ?? "", "([a-z])([A-Z])", "$1 $2");
        private static string Duration(double seconds) => StatsMod.TimeFormatUtils.FormatTimeSpan(TimeSpan.FromSeconds(Math.Max(0, seconds)));
    }
}
