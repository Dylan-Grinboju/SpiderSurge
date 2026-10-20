using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using SpiderSurge.Common;
using Logger = Silk.Logger;

namespace StatsMod;

public sealed class UnlockedTitle
{
    public string Name { get; set; }
    public int TimesEarned { get; set; }
    public DateTime FirstEarnedUtc { get; set; }
    public DateTime LastEarnedUtc { get; set; }
}

// The titles earned on this PC, kept in the mod's progress file
public static class TitleCollection
{
    private const string Section = "titles";
    private const string LegacyHeader = "SpiderSurgeTitles 1";
    private const string LegacyChecksumPrefix = "checksum ";
    private const string LegacyFileName = "titles.dat";
    private const int LegacyMaxFileBytes = 512 * 1024;
    private const int MaxTitles = 2000;
    private const int MaxNameLength = 100;

    private enum ReadResult { Ok, Missing, Damaged, Unreadable }

    private static readonly Dictionary<string, UnlockedTitle> _unlocked = new Dictionary<string, UnlockedTitle>(StringComparer.Ordinal);
    // Runs that ended while the file couldn't be read, added once it can be
    private static readonly List<(List<string> names, DateTime time)> _pending = new List<(List<string>, DateTime)>();
    private static bool _loaded;

    public static event Action Changed;

    public static string FilePath => ModData.FilePath;

    private static string LegacyPath => Path.Combine(ModData.Folder, LegacyFileName);

    public static IReadOnlyDictionary<string, UnlockedTitle> Unlocked
    {
        get
        {
            EnsureLoaded();
            return _unlocked;
        }
    }

    public static bool Readable => EnsureLoaded();

    // Each title counts once per run, however many players earned it; returns how many were new, or null while the file can't be read
    public static int? Record(IEnumerable<string> names)
    {
        var run = names.ToList();
        if (!EnsureLoaded())
        {
            _pending.Add((run, DateTime.UtcNow));
            return null;
        }
        int added = Add(run, DateTime.UtcNow);
        Save();
        Changed?.Invoke();
        return added;
    }

    private static int Add(List<string> names, DateTime now)
    {
        var newlyUnlocked = new List<string>();
        foreach (string raw in names.Distinct(StringComparer.Ordinal))
        {
            string name = Clean(raw);
            if (name == null) continue;
            if (_unlocked.TryGetValue(name, out var title))
            {
                if (title.TimesEarned < int.MaxValue) title.TimesEarned++;
                title.LastEarnedUtc = now;
            }
            else if (_unlocked.Count < MaxTitles)
            {
                _unlocked[name] = new UnlockedTitle { Name = name, TimesEarned = 1, FirstEarnedUtc = now, LastEarnedUtc = now };
                newlyUnlocked.Add(name);
            }
        }
        if (newlyUnlocked.Count > 0) Logger.LogInfo($"New titles unlocked: {string.Join(", ", newlyUnlocked)}");
        return newlyUnlocked.Count;
    }

    public static void Reset()
    {
        if (!EnsureLoaded())
        {
            Logger.LogWarning($"Couldn't reset the title collection: {FilePath} can't be read right now");
            return;
        }
        _unlocked.Clear();
        Save();
        Logger.LogInfo("Title collection reset");
        Changed?.Invoke();
    }

    private static bool EnsureLoaded()
    {
        if (_loaded) return true;
        if (!ModData.Readable) return false;

        _loaded = true;
        _unlocked.Clear();
        foreach (string line in ModData.Lines(Section))
        {
            if (TryParse(line, out var title) && (_unlocked.ContainsKey(title.Name) || _unlocked.Count < MaxTitles)) _unlocked[title.Name] = title;
        }
        bool changed = _pending.Count > 0;
        foreach (var (names, time) in _pending) Add(names, time);
        _pending.Clear();

        List<UnlockedTitle> legacy = ReadLegacy();
        if (legacy != null) changed |= MergeLegacy(legacy);
        if (changed && !Save()) return true;
        if (legacy != null)
        {
            ModData.TryDelete(LegacyPath);
            ModData.TryDelete(LegacyPath + ".bak");
            Logger.LogInfo($"Moved {legacy.Count} titles from {LegacyFileName} to {FilePath}");
        }
        return true;
    }

    // Versions before the progress file kept the titles in titles.dat; a file another program holds open is moved on a later launch
    private static List<UnlockedTitle> ReadLegacy()
    {
        foreach (string path in new[] { LegacyPath, LegacyPath + ".bak" })
        {
            ReadResult result = TryReadLegacy(path, out var titles);
            if (result == ReadResult.Ok) return titles;
            if (result == ReadResult.Unreadable) return null;
            if (result == ReadResult.Damaged) Logger.LogWarning($"{path} is damaged, so its titles weren't moved to {FilePath}");
        }
        return null;
    }

    // Keeps the most runs and the earliest and latest dates, so moving the same file twice changes nothing
    private static bool MergeLegacy(List<UnlockedTitle> legacy)
    {
        bool changed = false;
        foreach (var old in legacy)
        {
            if (!_unlocked.TryGetValue(old.Name, out var title))
            {
                if (_unlocked.Count >= MaxTitles) continue;
                _unlocked[old.Name] = old;
                changed = true;
                continue;
            }
            if (old.TimesEarned <= title.TimesEarned && old.FirstEarnedUtc >= title.FirstEarnedUtc && old.LastEarnedUtc <= title.LastEarnedUtc) continue;
            title.TimesEarned = Math.Max(title.TimesEarned, old.TimesEarned);
            if (old.FirstEarnedUtc < title.FirstEarnedUtc) title.FirstEarnedUtc = old.FirstEarnedUtc;
            if (old.LastEarnedUtc > title.LastEarnedUtc) title.LastEarnedUtc = old.LastEarnedUtc;
            changed = true;
        }
        return changed;
    }

    private static ReadResult TryReadLegacy(string path, out List<UnlockedTitle> titles)
    {
        titles = null;
        string text;
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists) return ReadResult.Missing;
            if (info.Length > LegacyMaxFileBytes) return ReadResult.Damaged;
            text = File.ReadAllText(path, Encoding.UTF8);
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
        {
            Logger.LogWarning($"Couldn't read {path}: {ex.Message}");
            return ReadResult.Unreadable;
        }
        return ParseLegacy(text, out titles) ? ReadResult.Ok : ReadResult.Damaged;
    }

    private static bool ParseLegacy(string text, out List<UnlockedTitle> titles)
    {
        titles = null;
        try
        {
            int checksumAt = text.LastIndexOf(LegacyChecksumPrefix, StringComparison.Ordinal);
            if (checksumAt <= 0 || text[checksumAt - 1] != '\n') return false;
            string body = text.Substring(0, checksumAt);
            if (!string.Equals(text.Substring(checksumAt + LegacyChecksumPrefix.Length).Trim(), ModData.Checksum(LegacyHeader, body), StringComparison.Ordinal)) return false;

            string[] lines = body.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length == 0 || lines[0] != LegacyHeader || lines.Length - 1 > MaxTitles) return false;

            var read = new List<UnlockedTitle>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 1; i < lines.Length; i++)
            {
                if (!TryParse(lines[i], out var title) || !seen.Add(title.Name)) return false;
                read.Add(title);
            }
            titles = read;
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    private static bool TryParse(string line, out UnlockedTitle title)
    {
        title = null;
        string[] fields = line.Split('\t');
        if (fields.Length != 4) return false;
        string name;
        try
        {
            name = Clean(Encoding.UTF8.GetString(Convert.FromBase64String(fields[0])));
        }
        catch (FormatException)
        {
            return false;
        }
        if (name == null
            || !int.TryParse(fields[1], NumberStyles.None, CultureInfo.InvariantCulture, out int times) || times < 1
            || !TryParseDate(fields[2], out DateTime first)
            || !TryParseDate(fields[3], out DateTime last))
        {
            return false;
        }
        title = new UnlockedTitle { Name = name, TimesEarned = times, FirstEarnedUtc = first, LastEarnedUtc = last };
        return true;
    }

    private static bool TryParseDate(string field, out DateTime date)
    {
        date = default;
        if (!long.TryParse(field, NumberStyles.None, CultureInfo.InvariantCulture, out long ticks) || ticks < DateTime.MinValue.Ticks || ticks > DateTime.MaxValue.Ticks) return false;
        date = new DateTime(ticks, DateTimeKind.Utc);
        return true;
    }

    private static bool Save()
    {
        var lines = _unlocked.Values.OrderBy(t => t.FirstEarnedUtc).ThenBy(t => t.Name, StringComparer.Ordinal).Select(title =>
            Convert.ToBase64String(Encoding.UTF8.GetBytes(title.Name)) + "\t"
            + title.TimesEarned.ToString(CultureInfo.InvariantCulture) + "\t"
            + title.FirstEarnedUtc.Ticks.ToString(CultureInfo.InvariantCulture) + "\t"
            + title.LastEarnedUtc.Ticks.ToString(CultureInfo.InvariantCulture));
        return ModData.Write(Section, lines);
    }

    private static string Clean(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        var cleaned = new string(name.Where(c => !char.IsControl(c)).ToArray()).Trim();
        if (cleaned.Length == 0) return null;
        return cleaned.Length > MaxNameLength ? cleaned.Substring(0, MaxNameLength) : cleaned;
    }
}
