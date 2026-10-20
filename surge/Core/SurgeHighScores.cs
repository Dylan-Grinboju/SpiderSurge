using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using SpiderSurge.Common;
using Logger = Silk.Logger;

namespace SpiderSurge
{
    // Surge Survival's own highscores, one per survival config, in the mod's progress file
    public static class SurgeHighScores
    {
        private const string Section = "highscores";
        private const string LegacySuffix = "-SurgeHS";
        private const char FieldSeparator = '\t';

        private static readonly Dictionary<string, float> _scores = new Dictionary<string, float>(StringComparer.Ordinal);
        private static bool _loaded;
        private static bool _legacyChecked;

        public static float Get(string config)
        {
            EnsureLoaded();
            return _scores.TryGetValue(config, out float score) ? score : 0f;
        }

        // Overwrites, the way the game's own highscores do
        public static void Set(string config, float score)
        {
            EnsureLoaded();
            _scores[config] = score;
            Save();
        }

        // A score set while the file can't be read is kept and saved once it can be
        private static void EnsureLoaded()
        {
            if (!_loaded)
            {
                if (!ModData.Readable) return;
                var pending = new Dictionary<string, float>(_scores);
                _scores.Clear();
                foreach (string line in ModData.Lines(Section))
                {
                    if (TryParse(line, out string config, out float score)) _scores[config] = score;
                }
                _loaded = true;
                foreach (KeyValuePair<string, float> entry in pending) _scores[entry.Key] = entry.Value;
                if (pending.Count > 0) Save();
            }
            if (!_legacyChecked && GameSave.Ready)
            {
                _legacyChecked = true;
                ImportLegacy();
            }
        }

        // Older versions kept these in the game's save as "<config>-SurgeHS"; they move once, keeping the higher score, and every old entry goes only after that is saved
        private static void ImportLegacy()
        {
            var keys = GameSave.Keys().Where(key => key.EndsWith(LegacySuffix, StringComparison.Ordinal)).ToList();
            if (keys.Count == 0) return;
            bool changed = false;
            foreach (string key in keys)
            {
                string config = key.Substring(0, key.Length - LegacySuffix.Length);
                float score = GameSave.Load(key, 0f);
                if (config.Length == 0 || score <= 0f || (_scores.TryGetValue(config, out float mine) && mine >= score)) continue;
                _scores[config] = score;
                changed = true;
            }
            if (changed && !Save()) return;
            GameSave.Delete(keys);
            Logger.LogInfo($"Moved the Surge highscores from the game's save to {ModData.FilePath}");
        }

        private static bool TryParse(string line, out string config, out float score)
        {
            config = null;
            score = 0f;
            string[] fields = line.Split(FieldSeparator);
            if (fields.Length != 2 || !float.TryParse(fields[1], NumberStyles.Float, CultureInfo.InvariantCulture, out score)) return false;
            try
            {
                config = Encoding.UTF8.GetString(Convert.FromBase64String(fields[0]));
            }
            catch (FormatException)
            {
                return false;
            }
            return config.Length > 0;
        }

        private static bool Save()
        {
            if (!_loaded) return false;
            var lines = _scores.OrderBy(entry => entry.Key, StringComparer.Ordinal)
                .Select(entry => Convert.ToBase64String(Encoding.UTF8.GetBytes(entry.Key)) + FieldSeparator + entry.Value.ToString("R", CultureInfo.InvariantCulture));
            return ModData.Write(Section, lines);
        }
    }
}
