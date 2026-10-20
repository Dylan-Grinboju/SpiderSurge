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
    private const string LegacyHeader = "SpiderSurgeTitles 1";
    private const string LegacyFileName = "titles.dat";
    private const string LegacySource = "titles.dat";
    private const int LegacyMaxFileBytes = 512 * 1024;
    private const int MaxTitles = 2000;
    private const int MaxNameLength = 100;

    private static readonly ProgressSection<UnlockedTitle> _titles = new ProgressSection<UnlockedTitle>("titles", TryParse, Format, Sum, OnWaitedAdded);
    private static bool _legacyChecked;

    public static event Action Changed;

    public static string FilePath => ModData.FilePath;

    private static string LegacyPath => Path.Combine(ModData.Folder, LegacyFileName);

    public static IReadOnlyDictionary<string, UnlockedTitle> Unlocked
    {
        get
        {
            EnsureLoaded();
            return _titles.Entries;
        }
    }

    public static bool Readable => EnsureLoaded();

    // Each title counts once per run, however many players earned it; returns how many were new, or null while the file can't be read and the run waits
    public static int? Record(IEnumerable<string> names)
    {
        DateTime now = DateTime.UtcNow;
        var run = names.Select(Clean).Where(name => name != null).Distinct(StringComparer.Ordinal).ToList();
        if (!EnsureLoaded())
        {
            _titles.Add(run.Select(name => Earned(name, now)));
            return null;
        }

        int room = MaxTitles - _titles.Entries.Count;
        var newlyUnlocked = new List<string>();
        var earned = new List<KeyValuePair<string, UnlockedTitle>>();
        foreach (string name in run)
        {
            if (!_titles.Contains(name))
            {
                if (room <= 0) continue;
                room--;
                newlyUnlocked.Add(name);
            }
            earned.Add(Earned(name, now));
        }
        _titles.Add(earned);
        if (newlyUnlocked.Count > 0) Logger.LogInfo($"New titles unlocked: {string.Join(", ", newlyUnlocked)}");
        Changed?.Invoke();
        return newlyUnlocked.Count;
    }

    public static void Reset()
    {
        if (!EnsureLoaded() || !_titles.Clear())
        {
            Logger.LogWarning($"Couldn't reset the title collection: {FilePath} can't be read right now");
            return;
        }
        Logger.LogInfo("Title collection reset");
        Changed?.Invoke();
    }

    private static bool EnsureLoaded()
    {
        if (!_titles.Loaded) return false;
        if (!_legacyChecked)
        {
            _legacyChecked = true;
            ImportLegacy();
        }
        return true;
    }

    // titles.dat is moved first, so a title it already had isn't reported as new
    private static void OnWaitedAdded(List<KeyValuePair<string, UnlockedTitle>> waited)
    {
        EnsureLoaded();
        var names = waited.Where(entry => _titles.TryGet(entry.Key, out UnlockedTitle title) && title.FirstEarnedUtc == entry.Value.FirstEarnedUtc).Select(entry => entry.Key).ToList();
        if (names.Count > 0) Logger.LogInfo($"New titles unlocked: {string.Join(", ", names)}");
        Changed?.Invoke();
    }

    // Versions before the progress file kept the titles in titles.dat; they move once, and a file another program holds open is moved on a later launch
    private static void ImportLegacy()
    {
        string[] paths = { LegacyPath, LegacyPath + ".bak" };
        if (!paths.Any(File.Exists)) return;
        if (!ModData.Imported(LegacySource))
        {
            List<UnlockedTitle> legacy = ReadLegacy(paths);
            if (legacy == null || !_titles.Import(LegacySource, legacy.Select(title => new KeyValuePair<string, UnlockedTitle>(title.Name, title)))) return;
            Logger.LogInfo($"Moved {legacy.Count} titles from {LegacyFileName} to {FilePath}");
        }
        foreach (string path in paths) ModData.TryDelete(path);
    }

    private static List<UnlockedTitle> ReadLegacy(string[] paths)
    {
        foreach (string path in paths)
        {
            ReadResult result = ModData.TryRead(path, LegacyHeader, LegacyMaxFileBytes, out string[] lines, out string error);
            if (result == ReadResult.Unreadable)
            {
                Logger.LogWarning($"Couldn't read {path}: {error}");
                return null;
            }
            if (result != ReadResult.Ok) continue;
            var titles = ParseLegacy(lines);
            if (titles != null) return titles;
            Logger.LogWarning($"{path} is damaged, so its titles weren't moved to {FilePath}");
        }
        return null;
    }

    private static List<UnlockedTitle> ParseLegacy(string[] lines)
    {
        if (lines.Length > MaxTitles) return null;
        var read = new List<UnlockedTitle>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (string line in lines)
        {
            if (!TryParse(line, out string name, out UnlockedTitle title) || !seen.Add(name)) return null;
            read.Add(title);
        }
        return read;
    }

    private static KeyValuePair<string, UnlockedTitle> Earned(string name, DateTime now)
    {
        return new KeyValuePair<string, UnlockedTitle>(name, new UnlockedTitle { Name = name, TimesEarned = 1, FirstEarnedUtc = now, LastEarnedUtc = now });
    }

    // Runs that waited and titles moved from titles.dat both add to what the file has
    private static UnlockedTitle Sum(UnlockedTitle saved, UnlockedTitle other)
    {
        return new UnlockedTitle
        {
            Name = saved.Name,
            TimesEarned = (int)Math.Min(int.MaxValue, (long)saved.TimesEarned + other.TimesEarned),
            FirstEarnedUtc = other.FirstEarnedUtc < saved.FirstEarnedUtc ? other.FirstEarnedUtc : saved.FirstEarnedUtc,
            LastEarnedUtc = other.LastEarnedUtc > saved.LastEarnedUtc ? other.LastEarnedUtc : saved.LastEarnedUtc,
        };
    }

    private static bool TryParse(string line, out string name, out UnlockedTitle title)
    {
        title = null;
        string[] fields = line.Split('\t');
        name = null;
        if (fields.Length != 4) return false;
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

    private static string Format(string name, UnlockedTitle title)
    {
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(name)) + "\t"
            + title.TimesEarned.ToString(CultureInfo.InvariantCulture) + "\t"
            + title.FirstEarnedUtc.Ticks.ToString(CultureInfo.InvariantCulture) + "\t"
            + title.LastEarnedUtc.Ticks.ToString(CultureInfo.InvariantCulture);
    }

    private static string Clean(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        var cleaned = new string(name.Where(c => !char.IsControl(c)).ToArray()).Trim();
        if (cleaned.Length == 0) return null;
        return cleaned.Length > MaxNameLength ? cleaned.Substring(0, MaxNameLength) : cleaned;
    }
}
