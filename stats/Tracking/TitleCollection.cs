using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using Logger = Silk.Logger;

namespace StatsMod;

public sealed class UnlockedTitle
{
    public string Name { get; set; }
    public int TimesEarned { get; set; }
    public DateTime FirstEarnedUtc { get; set; }
    public DateTime LastEarnedUtc { get; set; }
}

// The titles earned on this PC, kept in the game's user data folder so reinstalling the mod or the game keeps them
public static class TitleCollection
{
    private const string Header = "SpiderSurgeTitles 1";
    private const string ChecksumPrefix = "checksum ";
    private const string FileName = "titles.dat";
    private const int MaxFileBytes = 512 * 1024;
    private const int MaxTitles = 2000;
    private const int MaxNameLength = 100;

    private enum ReadResult { Ok, Missing, Damaged, Unreadable }

    private static readonly Dictionary<string, UnlockedTitle> _unlocked = new Dictionary<string, UnlockedTitle>(StringComparer.Ordinal);
    // Runs that ended while the file couldn't be read, added once it can be
    private static readonly List<(List<string> names, DateTime time)> _pending = new List<(List<string>, DateTime)>();
    private static string _path;
    private static bool _loaded;

    public static event Action Changed;

    public static string FilePath => _path ??= Path.GetFullPath(Path.Combine(Application.persistentDataPath, "SpiderSurge", FileName));

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

    // A file another program holds open is tried again on the next access rather than treated as damaged and overwritten
    private static bool EnsureLoaded()
    {
        if (_loaded) return true;
        ReadResult main = TryRead(FilePath, out var titles);
        if (main == ReadResult.Unreadable) return false;
        if (main == ReadResult.Damaged) KeepBroken();
        if (main != ReadResult.Ok)
        {
            ReadResult backup = TryRead(BackupPath, out titles);
            if (backup == ReadResult.Unreadable) return false;
            if (backup == ReadResult.Ok) Logger.LogWarning($"{FilePath} was {(main == ReadResult.Damaged ? "damaged" : "missing")}, so the title collection was restored from its backup");
            else if (main == ReadResult.Damaged) Logger.LogWarning($"{FilePath} was damaged and had no usable backup, so the title collection starts empty. The damaged file was kept as {FileName}.broken");
        }

        _loaded = true;
        _unlocked.Clear();
        foreach (var title in titles ?? new List<UnlockedTitle>()) _unlocked[title.Name] = title;
        if (_pending.Count > 0)
        {
            foreach (var (names, time) in _pending) Add(names, time);
            _pending.Clear();
            Save();
        }
        return true;
    }

    private static string BackupPath => FilePath + ".bak";

    // A file that is too big, edited by hand or cut short fails here and is never partly loaded
    private static ReadResult TryRead(string path, out List<UnlockedTitle> titles)
    {
        titles = null;
        string text;
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists) return ReadResult.Missing;
            if (info.Length > MaxFileBytes) return ReadResult.Damaged;
            text = File.ReadAllText(path, Encoding.UTF8);
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
        {
            Logger.LogWarning($"Couldn't read {path}: {ex.Message}");
            return ReadResult.Unreadable;
        }
        return Parse(text, out titles) ? ReadResult.Ok : ReadResult.Damaged;
    }

    private static bool Parse(string text, out List<UnlockedTitle> titles)
    {
        titles = null;
        try
        {
            int checksumAt = text.LastIndexOf(ChecksumPrefix, StringComparison.Ordinal);
            if (checksumAt <= 0 || text[checksumAt - 1] != '\n') return false;
            string body = text.Substring(0, checksumAt);
            if (!string.Equals(text.Substring(checksumAt + ChecksumPrefix.Length).Trim(), Checksum(body), StringComparison.Ordinal)) return false;

            string[] lines = body.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length == 0 || lines[0] != Header || lines.Length - 1 > MaxTitles) return false;

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

    // The new file is fully written and flushed before it replaces the old one, which stays as the backup, so a crash or power cut mid-save loses nothing
    private static void Save()
    {
        string temp = FilePath + ".tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath));
            var body = new StringBuilder(Header).Append('\n');
            foreach (var title in _unlocked.Values.OrderBy(t => t.FirstEarnedUtc).ThenBy(t => t.Name, StringComparer.Ordinal))
            {
                body.Append(Convert.ToBase64String(Encoding.UTF8.GetBytes(title.Name))).Append('\t')
                    .Append(title.TimesEarned.ToString(CultureInfo.InvariantCulture)).Append('\t')
                    .Append(title.FirstEarnedUtc.Ticks.ToString(CultureInfo.InvariantCulture)).Append('\t')
                    .Append(title.LastEarnedUtc.Ticks.ToString(CultureInfo.InvariantCulture)).Append('\n');
            }
            string content = body.ToString();
            byte[] bytes = new UTF8Encoding(false).GetBytes(content + ChecksumPrefix + Checksum(content) + "\n");
            using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                stream.Write(bytes, 0, bytes.Length);
                stream.Flush(true);
            }
            Replace(temp);
        }
        catch (Exception ex)
        {
            Logger.LogError($"Couldn't save the title collection to {FilePath}: {ex.Message}");
            TryDelete(temp);
        }
    }

    private static void Replace(string temp)
    {
        if (!File.Exists(FilePath))
        {
            File.Move(temp, FilePath);
            return;
        }
        try
        {
            File.Replace(temp, FilePath, BackupPath, true);
        }
        catch (Exception ex) when (ex is PlatformNotSupportedException || ex is NotImplementedException || ex is IOException)
        {
            File.Copy(FilePath, BackupPath, true);
            File.Copy(temp, FilePath, true);
            File.Delete(temp);
        }
    }

    private static void KeepBroken()
    {
        try
        {
            File.Copy(FilePath, FilePath + ".broken", true);
        }
        catch (Exception ex)
        {
            Logger.LogWarning($"Couldn't keep a copy of the damaged title collection: {ex.Message}");
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch (Exception) { }
    }

    // Catches damage and hand edits; the file holds no secrets, so it isn't encrypted
    private static string Checksum(string body)
    {
        using (var sha = SHA256.Create())
        {
            byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(Header + "|" + body));
            var hex = new StringBuilder(hash.Length * 2);
            foreach (byte b in hash) hex.Append(b.ToString("x2", CultureInfo.InvariantCulture));
            return hex.ToString();
        }
    }

    private static string Clean(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        var cleaned = new string(name.Where(c => !char.IsControl(c)).ToArray()).Trim();
        if (cleaned.Length == 0) return null;
        return cleaned.Length > MaxNameLength ? cleaned.Substring(0, MaxNameLength) : cleaned;
    }
}
