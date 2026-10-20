using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SpiderSurge.Common
{
    internal static class CollectionSeen
    {
        private static readonly Dictionary<string, HashSet<string>> sets = new Dictionary<string, HashSet<string>>();
        private static readonly HashSet<string> dirty = new HashSet<string>();
        private static DateTime retryAt;
        private static string Encode(string id) => Convert.ToBase64String(Encoding.UTF8.GetBytes(id));
        public static bool Ensure(string kind, IEnumerable<string> existing)
        {
            if (!ModData.Readable) return false;
            if (sets.ContainsKey(kind)) return true;
            var lines = ModData.Lines("seen." + kind);
            var seen = new HashSet<string>(lines);
            if (!seen.Contains("initialized"))
            {
                seen.Add("initialized");
                foreach (string id in existing) seen.Add(Encode(id));
                dirty.Add(kind);
            }
            sets[kind] = seen;
            if (dirty.Contains(kind)) Save(kind);
            return true;
        }
        public static bool IsNew(string kind, string id) => sets.TryGetValue(kind, out var seen) && !seen.Contains(Encode(id));
        public static void Mark(string kind, IEnumerable<string> ids)
        {
            if (!sets.TryGetValue(kind, out var seen)) return;
            foreach (string id in ids) seen.Add(Encode(id));
            Save(kind);
            SettingsPage.RefreshOpen();
        }
        public static void Reset(string kind)
        {
            sets[kind] = new HashSet<string> { "initialized" };
            Save(kind);
        }
        private static void Save(string kind)
        {
            if (ModData.Write("seen." + kind, sets[kind].OrderBy(x => x))) dirty.Remove(kind);
            else dirty.Add(kind);
        }
        public static void Tick()
        {
            if (dirty.Count == 0 || DateTime.UtcNow < retryAt) return;
            retryAt = DateTime.UtcNow.AddSeconds(5);
            Flush();
        }
        public static void Flush()
        {
            foreach (string kind in dirty.ToArray()) Save(kind);
        }

    }

    internal static class CollectionFilters
    {
        public static readonly string[] Labels = { "All", "Locked", "Unlocked", "Recently earned" };
        private static readonly Dictionary<string, int> filters = new Dictionary<string, int>();
        public static int Add(SettingsPage page, string kind)
        {
            int current = filters.TryGetValue(kind, out int value) ? value : 0;
            page.Button("Show: " + Labels[current], () => { filters[kind] = (current + 1) % Labels.Length; SettingsPage.RefreshOpen(); });
            return current;
        }
        public static string Badge(string kind, string id) => CollectionSeen.IsNew(kind, id) ? " <color=#FFD24A>NEW</color>" : "";
    }
}
