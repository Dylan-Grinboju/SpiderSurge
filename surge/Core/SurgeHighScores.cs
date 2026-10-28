using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using SpiderSurge.Common;
using Logger = Silk.Logger;

namespace SpiderSurge
{
    // Surge Survival's own highscores, one per survival config, in the mod's progress file
    public static class SurgeHighScores
    {
        private const string LegacySuffix = "-SurgeHS";
        private const string LegacySource = "gamesave.highscores";
        private const char FieldSeparator = '\t';

        private static readonly ProgressSection<float> _scores = new ProgressSection<float>("highscores", TryParse, Format, Math.Max);
        private static bool _legacyChecked;

        public static float Get(string config)
        {
            ImportLegacy();
            return _scores.TryGet(config, out float score) ? score : 0f;
        }

        // Surge runs only on endless survival, where a higher wave is better, so only a higher score is kept; one set while the file couldn't be read never lowers a saved one
        public static void Set(string config, float score)
        {
            ImportLegacy();
            _scores.Add(config, score);
        }

        // Older versions kept these in the game's save as "<config>-SurgeHS"; they move once, keeping the higher score, and the old entries go once that is saved
        private static void ImportLegacy()
        {
            if (_legacyChecked || !GameSave.Ready || !_scores.Loaded) return;
            _legacyChecked = true;
            var keys = GameSave.Keys().Where(key => key.EndsWith(LegacySuffix, StringComparison.Ordinal)).ToList();
            if (keys.Count == 0) return;
            if (!ModData.Imported(LegacySource))
            {
                var old = new List<KeyValuePair<string, float>>();
                foreach (string key in keys)
                {
                    string config = key.Substring(0, key.Length - LegacySuffix.Length);
                    float score = GameSave.Load(key, 0f);
                    if (config.Length > 0 && score > 0f) old.Add(new KeyValuePair<string, float>(config, score));
                }
                if (!_scores.Import(LegacySource, old)) return;
                Logger.LogInfo($"Moved the Surge highscores from the game's save to {ModData.FilePath}");
            }
            GameSave.Delete(keys);
        }

        private static bool TryParse(string line, out string config, out float score)
        {
            config = null;
            score = 0f;
            string[] fields = line.Split(FieldSeparator);
            if (fields.Length != 2 || !float.TryParse(fields[1], NumberStyles.Float, CultureInfo.InvariantCulture, out score)) return false;
            try
            {
                config = ModData.Decode(fields[0]);
            }
            catch (FormatException)
            {
                return false;
            }
            return config.Length > 0;
        }

        private static string Format(string config, float score)
        {
            return ModData.Encode(config) + FieldSeparator + score.ToString("R", CultureInfo.InvariantCulture);
        }
    }
}
