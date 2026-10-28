using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Threading.Tasks;
using UnityEngine;

namespace SpiderSurge.Common
{
    public sealed class HistoryIndex
    {
        public int Schema = 1;
        public List<HistorySummary> Matches = new List<HistorySummary>();
    }
    internal static class HistoryStore
    {
        private static readonly object gate = new object();
        private sealed class PendingWrite
        {
            public HistorySummary Summary;
            public byte[] Bytes;
        }
        private static readonly Dictionary<string, PendingWrite> pending = new Dictionary<string, PendingWrite>();
        private static List<HistorySummary> index;
        private static string skippedWarning;
        private static Task worker = Task.CompletedTask;
        private static string folder;
        private static bool loaded;
        private static List<HistorySummary> entries = new List<HistorySummary>();
        private static float retryAt;
        public static string Error { get; private set; }
        public static bool Busy => !worker.IsCompleted;
        public static IReadOnlyList<HistorySummary> Entries => entries;
        private static string Folder => folder ??= Path.Combine(ModData.Folder, "History");

        private static void Schedule(Action action)
        {
            worker = worker.ContinueWith(_ => { lock (gate) action(); }, TaskScheduler.Default);
        }
        public static void Report(string message)
        {
            MainThread.Run(() => { Error = message; ThreadSafeLog.LogWarning(message); SettingsPage.RefreshOpen(); });
        }
        public static void Save(HistoryMatch match)
        {
            string path = Folder;
            byte[] bytes = Serialize(match);
            Schedule(() => { pending[match.Summary.Id] = new PendingWrite { Summary = match.Summary, Bytes = bytes }; Flush(path); });
        }
        public static void ResolveTitleUnlocks(Dictionary<string, long> firstEarned, Action<bool> complete)
        {
            string path = Folder;
            var snapshot = new Dictionary<string, long>(firstEarned, StringComparer.Ordinal);
            Schedule(() =>
            {
                bool saved = false;
                try
                {
                    foreach (var item in pending.Values)
                    {
                        var match = Read<HistoryMatch>(item.Bytes);
                        if (ResolveTitles(match, snapshot)) item.Bytes = Serialize(match);
                    }
                    if (Directory.Exists(path)) foreach (string file in Directory.EnumerateFiles(path, "*.json"))
                    {
                        string id = Path.GetFileNameWithoutExtension(file);
                        if (!Guid.TryParseExact(id, "N", out _) || pending.ContainsKey(id)) continue;
                        HistoryMatch match;
                        try { match = Read<HistoryMatch>(file); }
                        catch (SerializationException) { continue; }
                        if (!ValidMatch(match) || match.Summary.Id != id) continue;
                        if (ResolveTitles(match, snapshot)) Write(file, match);
                    }
                    saved = pending.Count == 0 || Flush(path);
                }
                catch (Exception ex) { Report("Could not preserve new-title markers in match history. " + ex.Message); }
                MainThread.Enqueue(() => complete?.Invoke(saved));
            });
        }
        private static bool ResolveTitles(HistoryMatch match, Dictionary<string, long> firstEarned)
        {
            if (firstEarned == null) return false;
            bool changed = false;
            foreach (var title in match.Titles)
            {
                if (title.PendingUnlockTicks <= 0) continue;
                title.New = firstEarned.TryGetValue(title.Name, out long earned) && earned == title.PendingUnlockTicks;
                title.PendingUnlockTicks = 0;
                changed = true;
            }
            return changed;
        }
        public static void Tick()
        {
            if (Busy || Time.unscaledTime < retryAt) return;
            retryAt = Time.unscaledTime + 10f;
            lock (gate)
            {
                if (pending.Count == 0) return;
            }
            string path = Folder;
            Schedule(() => Flush(path));
        }
        public static void FinishWrites()
        {
            string path = Folder;
            Schedule(() => { if (pending.Count > 0) Flush(path); });
            try { worker.Wait(3000); }
            catch (Exception ex) { ThreadSafeLog.LogWarning("Match history shutdown: " + ex.Message); }
        }
        private static bool Flush(string path)
        {
            try
            {
                Directory.CreateDirectory(path);
                EnsureIndex(path, false);
                foreach (var item in pending.Values)
                {
                    WriteBytes(Path.Combine(path, item.Summary.Id + ".json"), item.Bytes);
                    index.RemoveAll(s => s.Id == item.Summary.Id);
                    index.Add(item.Summary);
                }
                index = Sort(index);
                Write(Path.Combine(path, "index.json"), new HistoryIndex { Matches = index });
                pending.Clear();
                Publish(index.ToList(), skippedWarning);
                return true;
            }
            catch (Exception ex) { Report("Match history could not be saved; retrying during this session. " + ex.Message); return false; }
        }
        public static void Load(bool refresh = false)
        {
            if (loaded && !refresh || Busy) return;
            loaded = true;
            string path = Folder;
            Schedule(() =>
            {
                try { EnsureIndex(path, refresh); Publish(index.ToList(), skippedWarning); }
                catch (Exception ex) { Report("Could not read match history. " + ex.Message); }
            });
        }
        private static void EnsureIndex(string path, bool refresh)
        {
            if (index != null && !refresh) return;
            string indexPath = Path.Combine(path, "index.json");
            if (!refresh && File.Exists(indexPath))
            {
                try
                {
                    var saved = Read<HistoryIndex>(indexPath);
                    var files = new HashSet<string>(Directory.EnumerateFiles(path, "*.json")
                        .Select(Path.GetFileNameWithoutExtension).Where(id => Guid.TryParseExact(id, "N", out _)));
                    if (saved.Schema == 1 && saved.Matches != null && saved.Matches.All(Valid)
                        && saved.Matches.Count == files.Count && files.SetEquals(saved.Matches.Select(s => s.Id)))
                    { index = Sort(saved.Matches); skippedWarning = null; return; }
                }
                catch { }
            }
            Rebuild(path);
        }
        private static List<HistorySummary> Sort(List<HistorySummary> list) => list.OrderByDescending(m => m.DateUtc, StringComparer.Ordinal).ThenBy(m => m.Id).ToList();
        private static bool DurationValid(double seconds) => !double.IsNaN(seconds) && seconds >= 0 && seconds < TimeSpan.MaxValue.TotalSeconds - 1;
        private static bool Valid(HistorySummary s) => s != null && Guid.TryParseExact(s.Id, "N", out _)
            && (s.Mode == "survival" || s.Mode == "versus") && DurationValid(s.Seconds) && DateTime.TryParse(s.DateUtc, out _);
        private static bool ValuesValid(List<HistoryValue> values) => values != null && values.All(v => v != null && !double.IsNaN(v.Value) && !double.IsInfinity(v.Value));
        private static bool ValidMatch(HistoryMatch m)
        {
            if (m == null || m.Schema != 1 || !Valid(m.Summary) || m.Maps == null || m.Perks == null
                || m.Titles == null || m.Titles.Any(t => t == null) || m.Teams == null || m.Teams.Any(t => t == null)
                || m.Players == null || m.Players.Any(p => p == null || !ValuesValid(p.Stats) || !ValuesValid(p.Enemies)
                    || !ValuesValid(p.Weapons) || !ValuesValid(p.DeathsPerMap) || p.Accuracy == null || p.Accuracy.Any(a => a == null))) return false;
            var surge = m.SurgeStats;
            return surge == null || (surge.PlayerStats != null && surge.PlayerStats.All(p => p != null)
                && surge.PerkChoices != null && surge.PerkChoices.All(p => p != null && p.Offered != null)
                && surge.Waves != null && surge.Waves.All(w => w != null && DurationValid(w.Seconds)));
        }
        private static void Rebuild(string path)
        {
            var list = new List<HistorySummary>();
            int damaged = 0;
            if (Directory.Exists(path)) foreach (string file in Directory.EnumerateFiles(path, "*.json"))
            {
                if (!Guid.TryParseExact(Path.GetFileNameWithoutExtension(file), "N", out _)) continue;
                try
                {
                    var match = Read<HistoryMatch>(file);
                    if (!ValidMatch(match) || match.Summary.Id != Path.GetFileNameWithoutExtension(file)) { damaged++; continue; }
                    list.Add(match.Summary);
                }
                catch { damaged++; }
            }
            index = Sort(list);
            if (Directory.Exists(path)) Write(Path.Combine(path, "index.json"), new HistoryIndex { Matches = index });
            skippedWarning = damaged > 0 ? $"{damaged} unreadable match file(s) were skipped and kept on disk." : null;
        }
        private static void Publish(List<HistorySummary> list, string problem)
        {
            MainThread.Enqueue(() => { entries = list; Error = problem; SettingsPage.RefreshOpen(); });
        }
        public static void Detail(string id, Action<HistoryMatch> complete)
        {
            if (!Guid.TryParseExact(id, "N", out _)) return;
            string path = Path.Combine(Folder, id + ".json");
            var firstEarned = StatsMod.TitleCollection.FirstEarnedSnapshot();
            Schedule(() =>
            {
                try
                {
                    var match = pending.TryGetValue(id, out var item) ? Read<HistoryMatch>(item.Bytes) : Read<HistoryMatch>(path);
                    if (!ValidMatch(match) || match.Summary.Id != id) throw new InvalidDataException("Unsupported or damaged match.");
                    if (ResolveTitles(match, firstEarned))
                    {
                        pending[id] = new PendingWrite { Summary = match.Summary, Bytes = Serialize(match) };
                        Flush(Folder);
                    }
                    MainThread.Enqueue(() => complete(match));
                }
                catch (Exception ex) { Report("Could not open this match. " + ex.Message); MainThread.Enqueue(() => complete(null)); }
            });
        }
        public static void Clear()
        {
            string path = Folder;
            Schedule(() =>
            {
                try
                {
                    pending.Clear();
                    if (Directory.Exists(path)) foreach (string file in Directory.EnumerateFiles(path))
                    {
                        string name = Path.GetFileName(file);
                        if (name == "index.json" || name == "index.json.bak" || name == "index.json.tmp"
                            || (Guid.TryParseExact(name.Split('.')[0], "N", out _) && (name.EndsWith(".json") || name.EndsWith(".json.bak") || name.EndsWith(".json.tmp")))) File.Delete(file);
                    }
                    index = new List<HistorySummary>();
                    skippedWarning = null;
                    Publish(index.ToList(), null);
                }
                catch (Exception ex) { Report("Could not clear all match history. " + ex.Message); index = null; loaded = false; }
            });
        }
        private static T Read<T>(string path)
        {
            using (var stream = File.OpenRead(path)) return (T)new DataContractJsonSerializer(typeof(T)).ReadObject(stream);
        }
        private static T Read<T>(byte[] bytes)
        {
            using (var stream = new MemoryStream(bytes)) return (T)new DataContractJsonSerializer(typeof(T)).ReadObject(stream);
        }
        private static byte[] Serialize<T>(T value)
        {
            using (var stream = new MemoryStream())
            {
                new DataContractJsonSerializer(typeof(T)).WriteObject(stream, value);
                return stream.ToArray();
            }
        }
        private static void Write<T>(string path, T value) => WriteBytes(path, Serialize(value));
        private static void WriteBytes(string path, byte[] bytes)
        {
            string temp = path + ".tmp";
            using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(true);
            }
            if (File.Exists(path)) File.Replace(temp, path, path + ".bak");
            else File.Move(temp, path);
        }
    }
}
