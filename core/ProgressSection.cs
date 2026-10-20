using System;
using System.Collections.Generic;
using System.Linq;

namespace SpiderSurge.Common
{
    // One section of the progress file as entries by key, combined with the section's own rule. Entries added while the file can't be read wait and are combined in once it can; an older version's data is imported once, recorded in the same save, so a later reset isn't undone
    public sealed class ProgressSection<T>
    {
        public delegate bool LineParser(string line, out string key, out T value);

        private readonly string _name;
        private readonly LineParser _parse;
        private readonly Func<string, T, string> _format;
        private readonly Func<T, T, T> _combine;
        private readonly Action<List<KeyValuePair<string, T>>> _pendingAdded;
        private readonly Dictionary<string, T> _entries = new Dictionary<string, T>(StringComparer.Ordinal);
        private readonly Dictionary<string, T> _pending = new Dictionary<string, T>(StringComparer.Ordinal);
        private bool _loaded;

        public ProgressSection(string name, LineParser parse, Func<string, T, string> format, Func<T, T, T> combine, Action<List<KeyValuePair<string, T>>> pendingAdded = null)
        {
            _name = name;
            _parse = parse;
            _format = format;
            _combine = combine;
            _pendingAdded = pendingAdded;
        }

        public bool Loaded => Load();

        public IReadOnlyDictionary<string, T> Entries
        {
            get
            {
                Load();
                return _entries;
            }
        }

        // While the file can't be read, only what is waiting
        public bool TryGet(string key, out T value) => (Load() ? _entries : _pending).TryGetValue(key, out value);

        public bool Contains(string key) => TryGet(key, out _);

        public void Add(string key, T value) => Add(new[] { new KeyValuePair<string, T>(key, value) });

        public void Add(IEnumerable<KeyValuePair<string, T>> entries)
        {
            if (!Load())
            {
                Combine(_pending, entries);
                return;
            }
            if (Combine(_entries, entries)) Save();
        }

        // True once the data is in the file, now or by an earlier import, so the old copy can go
        public bool Import(string source, IEnumerable<KeyValuePair<string, T>> entries)
        {
            if (!Load()) return false;
            if (ModData.Imported(source)) return true;
            Combine(_entries, entries);
            return Save(source);
        }

        public bool Clear()
        {
            if (!Load()) return false;
            _entries.Clear();
            Save();
            return true;
        }

        private bool Load()
        {
            if (_loaded) return true;
            if (!ModData.Readable) return false;

            _loaded = true;
            foreach (string line in ModData.Lines(_name))
            {
                if (_parse(line, out string key, out T value)) _entries[key] = value;
            }
            if (_pending.Count == 0) return true;

            var added = _pending.Where(entry => !_entries.ContainsKey(entry.Key)).ToList();
            Combine(_entries, _pending);
            _pending.Clear();
            Save();
            if (added.Count > 0) _pendingAdded?.Invoke(added);
            return true;
        }

        private bool Combine(Dictionary<string, T> into, IEnumerable<KeyValuePair<string, T>> entries)
        {
            bool changed = false;
            foreach (var entry in entries)
            {
                T value = into.TryGetValue(entry.Key, out T current) ? _combine(current, entry.Value) : entry.Value;
                if (into.ContainsKey(entry.Key) && EqualityComparer<T>.Default.Equals(value, current)) continue;
                into[entry.Key] = value;
                changed = true;
            }
            return changed;
        }

        private bool Save(string imported = null)
        {
            return ModData.Write(_name, _entries.OrderBy(entry => entry.Key, StringComparer.Ordinal).Select(entry => _format(entry.Key, entry.Value)), imported);
        }
    }
}
