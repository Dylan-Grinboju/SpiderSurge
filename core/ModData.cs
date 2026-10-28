using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Logger = Silk.Logger;

namespace SpiderSurge.Common
{
    internal enum ReadResult { Ok, Missing, Damaged, Unreadable }

    // The mod's progress on this PC in one file in the game's user data folder, so reinstalling the mod or the game keeps it; each part owns a section
    public static class ModData
    {
        private const string Header = "SpiderSurgeData 1";
        private const string ChecksumPrefix = "checksum ";
        private const string FileName = "progress.dat";
        private const string ImportedSection = "imported";
        private const int MaxFileBytes = 1024 * 1024;
        private const double FirstRetrySeconds = 2;
        private const double MaxRetrySeconds = 30;
        private const double SaveRetrySeconds = 5;
        private const int ShutdownWaitMs = 3000;

        private static readonly SortedDictionary<string, List<string>> _sections = new SortedDictionary<string, List<string>>(StringComparer.Ordinal);
        private static readonly object _writeGate = new object();
        private static Task _writer = Task.CompletedTask;
        private static byte[] _queued;
        private static bool _saveFailed;
        private static bool _failing;
        private static DateTime _saveRetryAt;
        private static string _path;
        private static bool _loaded;
        private static bool _mainDamaged;
        private static DateTime _retryAt;
        private static double _retrySeconds;

        public static string FilePath => _path ??= Path.GetFullPath(Path.Combine(Application.persistentDataPath, "SpiderSurge", FileName));

        public static string Folder => Path.GetDirectoryName(FilePath);

        private static string BackupPath => FilePath + ".bak";

        public static bool Readable => EnsureLoaded();

        public static IReadOnlyList<string> Lines(string section)
        {
            if (EnsureLoaded() && _sections.TryGetValue(section, out var lines)) return lines;
            return Array.Empty<string>();
        }

        public static bool Imported(string source) => Lines(ImportedSection).Contains(source);

        // Free text such as a player name is stored as UTF-8 base64, so it can't break the line format
        public static string Encode(string text) => Convert.ToBase64String(Encoding.UTF8.GetBytes(text));

        public static string Decode(string field) => Encoding.UTF8.GetString(Convert.FromBase64String(field));

        // Replaces one section and keeps every other one, including those of parts that are turned off; with imported, the same save records that an older version's data moved in and waits for the disk. False when the file can't be read or that save failed
        public static bool Write(string section, IEnumerable<string> lines, string imported = null)
        {
            if (!IsSectionName(section)) throw new ArgumentException($"Bad section name: {section}");
            var list = lines.ToList();
            if (list.Any(line => !IsDataLine(line)) || (imported != null && !IsDataLine(imported))) throw new ArgumentException($"Bad line in section {section}");
            if (!EnsureLoaded()) return false;
            if (list.Count > 0) _sections[section] = list;
            else _sections.Remove(section);
            if (imported != null && !Imported(imported)) _sections[ImportedSection] = Lines(ImportedSection).Concat(new[] { imported }).ToList();
            return imported != null ? SaveNow() : Save();
        }

        // A file another program holds open is tried again later, less often each time, rather than treated as damaged and overwritten
        private static bool EnsureLoaded()
        {
            if (_loaded) return true;
            if (DateTime.UtcNow < _retryAt) return false;
            ReadResult main = TryReadSections(FilePath, out var sections, out string error);
            if (main == ReadResult.Damaged) KeepBroken();
            if (main != ReadResult.Ok && main != ReadResult.Unreadable)
            {
                ReadResult backup = TryReadSections(BackupPath, out sections, out string backupError);
                if (backup == ReadResult.Ok) Logger.LogWarning($"{FilePath} was {(main == ReadResult.Damaged ? "damaged" : "missing")}, so the mod's progress was restored from its backup");
                else if (backup != ReadResult.Unreadable && main == ReadResult.Damaged) Logger.LogWarning($"{FilePath} was damaged and had no usable backup, so achievements, titles and highscores start empty. The damaged file was kept as {FileName}.broken");
                if (backup == ReadResult.Unreadable)
                {
                    main = ReadResult.Unreadable;
                    error = backupError;
                }
            }
            if (main == ReadResult.Unreadable)
            {
                if (_retrySeconds == 0) Logger.LogWarning($"Couldn't read {FilePath}: {error}. Progress made meanwhile is kept and saved once it can be read");
                _retrySeconds = _retrySeconds == 0 ? FirstRetrySeconds : Math.Min(_retrySeconds * 2, MaxRetrySeconds);
                _retryAt = DateTime.UtcNow.AddSeconds(_retrySeconds);
                return false;
            }

            if (_retrySeconds > 0) Logger.LogInfo($"{FilePath} can be read again");
            _loaded = true;
            _mainDamaged = main == ReadResult.Damaged;
            _sections.Clear();
            if (sections != null)
            {
                foreach (var section in sections) _sections[section.Key] = section.Value;
            }
            return true;
        }

        private static ReadResult TryReadSections(string path, out SortedDictionary<string, List<string>> sections, out string error)
        {
            sections = null;
            ReadResult result = TryRead(path, Header, MaxFileBytes, out string[] lines, out error);
            if (result != ReadResult.Ok) return result;
            sections = ParseSections(lines);
            return sections != null ? ReadResult.Ok : ReadResult.Damaged;
        }

        // A file that is too big, edited by hand or cut short fails here and is never partly loaded; lines are those after the header
        internal static ReadResult TryRead(string path, string header, int maxBytes, out string[] lines, out string error)
        {
            lines = null;
            error = null;
            string text;
            try
            {
                var info = new FileInfo(path);
                if (!info.Exists) return ReadResult.Missing;
                if (info.Length > maxBytes) return ReadResult.Damaged;
                text = File.ReadAllText(path, Encoding.UTF8);
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                error = ex.Message;
                return ReadResult.Unreadable;
            }
            return Verify(text, header, out lines) ? ReadResult.Ok : ReadResult.Damaged;
        }

        private static bool Verify(string text, string header, out string[] lines)
        {
            lines = null;
            int checksumAt = text.LastIndexOf(ChecksumPrefix, StringComparison.Ordinal);
            if (checksumAt <= 0 || text[checksumAt - 1] != '\n') return false;
            string body = text.Substring(0, checksumAt);
            if (!string.Equals(text.Substring(checksumAt + ChecksumPrefix.Length).Trim(), Checksum(header, body), StringComparison.Ordinal)) return false;

            string[] all = body.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            if (all.Length == 0 || all[0] != header) return false;
            lines = all.Skip(1).ToArray();
            return true;
        }

        private static SortedDictionary<string, List<string>> ParseSections(string[] lines)
        {
            var read = new SortedDictionary<string, List<string>>(StringComparer.Ordinal);
            List<string> current = null;
            foreach (string line in lines)
            {
                if (line[0] == '[')
                {
                    string name = line.Length > 2 && line[line.Length - 1] == ']' ? line.Substring(1, line.Length - 2) : null;
                    if (!IsSectionName(name) || read.ContainsKey(name)) return null;
                    read[name] = current = new List<string>();
                }
                else
                {
                    if (current == null) return null;
                    current.Add(line);
                }
            }
            return read;
        }

        // The file is built here from the in-memory sections, which stay authoritative; one background writer puts the newest copy on disk
        private static bool Save()
        {
            var body = new StringBuilder(Header).Append('\n');
            foreach (var section in _sections)
            {
                body.Append('[').Append(section.Key).Append("]\n");
                foreach (string line in section.Value) body.Append(line).Append('\n');
            }
            string content = body.ToString();
            byte[] bytes = new UTF8Encoding(false).GetBytes(content + ChecksumPrefix + Checksum(Header, content) + "\n");
            if (bytes.Length > MaxFileBytes)
            {
                Logger.LogError($"Didn't save {FilePath}: it would be bigger than {MaxFileBytes / 1024} KB");
                return false;
            }

            string path = FilePath;
            lock (_writeGate)
            {
                _saveFailed = false;
                bool scheduled = _queued != null;
                _queued = bytes;
                if (!scheduled) _writer = _writer.ContinueWith(_ => WriteQueued(path), TaskScheduler.Default);
            }
            return true;
        }

        // An older version's data is deleted once its import is saved, so that save waits for the disk
        private static bool SaveNow()
        {
            if (!Save()) return false;
            WaitForWriter(Timeout.Infinite);
            lock (_writeGate) return !_saveFailed;
        }

        // The new file is fully written and flushed before it replaces the old one, which stays as the backup, so a crash or power cut mid-save loses nothing
        private static void WriteQueued(string path)
        {
            byte[] bytes;
            lock (_writeGate)
            {
                bytes = _queued;
                _queued = null;
            }
            if (bytes == null) return;

            string temp = path + ".tmp";
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }
                Replace(path, temp);
                lock (_writeGate)
                {
                    if (_failing) ThreadSafeLog.LogInfo($"{path} could be saved again");
                    _failing = false;
                }
            }
            catch (Exception ex)
            {
                TryDelete(temp);
                lock (_writeGate)
                {
                    if (_queued != null) return;
                    if (!_failing) ThreadSafeLog.LogError($"Couldn't save the mod's progress to {path} ({ex.Message}); trying again every {SaveRetrySeconds:0} s");
                    _failing = true;
                    _saveFailed = true;
                    _saveRetryAt = DateTime.UtcNow.AddSeconds(SaveRetrySeconds);
                }
            }
        }

        // Called every frame; a save that failed is tried again with the newest data
        public static void Tick()
        {
            lock (_writeGate)
            {
                if (!_saveFailed || DateTime.UtcNow < _saveRetryAt) return;
            }
            Save();
        }

        // Called on quit and unload so the newest progress reaches the disk
        public static void FinishWrites()
        {
            bool retry;
            lock (_writeGate) retry = _saveFailed;
            if (retry) Save();
            WaitForWriter(ShutdownWaitMs);
        }

        private static void WaitForWriter(int milliseconds)
        {
            Task writer;
            lock (_writeGate) writer = _writer;
            try
            {
                writer.Wait(milliseconds);
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Saving the mod's progress didn't finish: {ex.Message}");
            }
        }

        // A damaged file is overwritten rather than kept as the backup, so the backup it was restored from survives
        private static void Replace(string path, string temp)
        {
            if (!File.Exists(path))
            {
                File.Move(temp, path);
                return;
            }
            if (_mainDamaged)
            {
                File.Copy(temp, path, true);
                File.Delete(temp);
                _mainDamaged = false;
                return;
            }
            string backup = path + ".bak";
            try
            {
                File.Replace(temp, path, backup, true);
            }
            catch (Exception ex) when (ex is PlatformNotSupportedException || ex is NotImplementedException || ex is IOException)
            {
                File.Copy(path, backup, true);
                File.Copy(temp, path, true);
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
                Logger.LogWarning($"Couldn't keep a copy of the damaged progress file: {ex.Message}");
            }
        }

        // False, with a warning, when the file is still there afterwards
        internal static bool TryDelete(string path)
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
                return true;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                ThreadSafeLog.LogWarning($"Couldn't delete {path}: {ex.Message}");
                return false;
            }
        }

        private static bool IsSectionName(string name) => !string.IsNullOrEmpty(name) && name.All(c => (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '.');

        private static bool IsDataLine(string line) => !string.IsNullOrEmpty(line) && line[0] != '[' && line.IndexOf('\n') < 0 && line.IndexOf('\r') < 0;

        // Catches damage and hand edits; the file holds no secrets, so it isn't encrypted
        private static string Checksum(string header, string body)
        {
            using (var sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(header + "|" + body));
                var hex = new StringBuilder(hash.Length * 2);
                foreach (byte b in hash) hex.Append(b.ToString("x2", CultureInfo.InvariantCulture));
                return hex.ToString();
            }
        }
    }
}
