using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using Logger = Silk.Logger;

namespace SpiderSurge.Common
{
    // The mod's progress on this PC in one file in the game's user data folder, so reinstalling the mod or the game keeps it; each part owns a section
    public static class ModData
    {
        private const string Header = "SpiderSurgeData 1";
        private const string ChecksumPrefix = "checksum ";
        private const string FileName = "progress.dat";
        private const int MaxFileBytes = 1024 * 1024;
        private const double RetrySeconds = 2;

        private enum ReadResult { Ok, Missing, Damaged, Unreadable }

        private static readonly SortedDictionary<string, List<string>> _sections = new SortedDictionary<string, List<string>>(StringComparer.Ordinal);
        private static string _path;
        private static bool _loaded;
        private static DateTime _retryAt;

        public static string FilePath => _path ??= Path.GetFullPath(Path.Combine(Application.persistentDataPath, "SpiderSurge", FileName));

        public static string Folder => Path.GetDirectoryName(FilePath);

        private static string BackupPath => FilePath + ".bak";

        public static bool Readable => EnsureLoaded();

        public static IReadOnlyList<string> Lines(string section)
        {
            if (EnsureLoaded() && _sections.TryGetValue(section, out var lines)) return lines;
            return Array.Empty<string>();
        }

        // Replaces one section and keeps every other one, including those of parts that are turned off; false when the file couldn't be saved
        public static bool Write(string section, IEnumerable<string> lines)
        {
            if (!IsSectionName(section)) throw new ArgumentException($"Bad section name: {section}");
            var list = lines.ToList();
            if (list.Any(line => !IsDataLine(line))) throw new ArgumentException($"Bad line in section {section}");
            if (!EnsureLoaded()) return false;
            if (list.Count > 0) _sections[section] = list;
            else _sections.Remove(section);
            return Save();
        }

        // A file another program holds open is tried again a little later rather than treated as damaged and overwritten
        private static bool EnsureLoaded()
        {
            if (_loaded) return true;
            if (DateTime.UtcNow < _retryAt) return false;
            ReadResult main = TryRead(FilePath, out var sections);
            if (main == ReadResult.Damaged) KeepBroken();
            if (main != ReadResult.Ok && main != ReadResult.Unreadable)
            {
                ReadResult backup = TryRead(BackupPath, out sections);
                if (backup == ReadResult.Ok) Logger.LogWarning($"{FilePath} was {(main == ReadResult.Damaged ? "damaged" : "missing")}, so the mod's progress was restored from its backup");
                else if (backup != ReadResult.Unreadable && main == ReadResult.Damaged) Logger.LogWarning($"{FilePath} was damaged and had no usable backup, so achievements, titles and highscores start empty. The damaged file was kept as {FileName}.broken");
                if (backup == ReadResult.Unreadable) main = ReadResult.Unreadable;
            }
            if (main == ReadResult.Unreadable)
            {
                _retryAt = DateTime.UtcNow.AddSeconds(RetrySeconds);
                return false;
            }

            _loaded = true;
            _sections.Clear();
            if (sections != null)
            {
                foreach (var section in sections) _sections[section.Key] = section.Value;
            }
            return true;
        }

        // A file that is too big, edited by hand or cut short fails here and is never partly loaded
        private static ReadResult TryRead(string path, out SortedDictionary<string, List<string>> sections)
        {
            sections = null;
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
            return Parse(text, out sections) ? ReadResult.Ok : ReadResult.Damaged;
        }

        private static bool Parse(string text, out SortedDictionary<string, List<string>> sections)
        {
            sections = null;
            try
            {
                int checksumAt = text.LastIndexOf(ChecksumPrefix, StringComparison.Ordinal);
                if (checksumAt <= 0 || text[checksumAt - 1] != '\n') return false;
                string body = text.Substring(0, checksumAt);
                if (!string.Equals(text.Substring(checksumAt + ChecksumPrefix.Length).Trim(), Checksum(body), StringComparison.Ordinal)) return false;

                string[] lines = body.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
                if (lines.Length == 0 || lines[0] != Header) return false;

                var read = new SortedDictionary<string, List<string>>(StringComparer.Ordinal);
                List<string> current = null;
                for (int i = 1; i < lines.Length; i++)
                {
                    string line = lines[i];
                    if (line[0] == '[')
                    {
                        string name = line.Length > 2 && line[line.Length - 1] == ']' ? line.Substring(1, line.Length - 2) : null;
                        if (!IsSectionName(name) || read.ContainsKey(name)) return false;
                        read[name] = current = new List<string>();
                    }
                    else
                    {
                        if (current == null) return false;
                        current.Add(line);
                    }
                }
                sections = read;
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        // The new file is fully written and flushed before it replaces the old one, which stays as the backup, so a crash or power cut mid-save loses nothing
        private static bool Save()
        {
            string temp = FilePath + ".tmp";
            try
            {
                var body = new StringBuilder(Header).Append('\n');
                foreach (var section in _sections)
                {
                    body.Append('[').Append(section.Key).Append("]\n");
                    foreach (string line in section.Value) body.Append(line).Append('\n');
                }
                string content = body.ToString();
                byte[] bytes = new UTF8Encoding(false).GetBytes(content + ChecksumPrefix + Checksum(content) + "\n");
                if (bytes.Length > MaxFileBytes)
                {
                    Logger.LogError($"Didn't save {FilePath}: it would be bigger than {MaxFileBytes / 1024} KB");
                    return false;
                }

                Directory.CreateDirectory(Folder);
                using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }
                Replace(temp);
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Couldn't save the mod's progress to {FilePath}: {ex.Message}");
                TryDelete(temp);
                return false;
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
                Logger.LogWarning($"Couldn't keep a copy of the damaged progress file: {ex.Message}");
            }
        }

        internal static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch (Exception) { }
        }

        private static bool IsSectionName(string name) => !string.IsNullOrEmpty(name) && name.All(c => (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '.');

        private static bool IsDataLine(string line) => !string.IsNullOrEmpty(line) && line[0] != '[' && line.IndexOf('\n') < 0 && line.IndexOf('\r') < 0;

        // Catches damage and hand edits; the file holds no secrets, so it isn't encrypted
        internal static string Checksum(string header, string body)
        {
            using (var sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(header + "|" + body));
                var hex = new StringBuilder(hash.Length * 2);
                foreach (byte b in hash) hex.Append(b.ToString("x2", CultureInfo.InvariantCulture));
                return hex.ToString();
            }
        }

        private static string Checksum(string body) => Checksum(Header, body);
    }
}
