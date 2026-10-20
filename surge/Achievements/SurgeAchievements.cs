using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using Doozy.Engine.UI;
using SpiderSurge.Common;
using TMPro;
using UnityEngine;
using Logger = Silk.Logger;

namespace SpiderSurge
{
    public sealed class Achievement
    {
        public string Id { get; }
        public string Name { get; }
        public string Description { get; }

        public Achievement(string id, string name, string description)
        {
            Id = id;
            Name = name;
            Description = description;
        }
    }

    public sealed class AchievementUnlock
    {
        public DateTime Date;
        public string Player;
    }

    // Achievements belong to this PC and live in the mod's progress file
    public static class SurgeAchievements
    {
        private const string Section = "achievements";
        private const string LegacyKey = "SpiderSurge-Achievements";
        private const char FieldSeparator = '\t';

        public const string BlackHole = "blackHole";
        public const string TotalDestruction = "totalDestruction";
        public const string KeepYouSafe = "keepYouSafe";
        public const string UnlimitedPower = "unlimitedPower";
        public const string Doomsday = "doomsdayScenario";
        public const string Begone = "begone";
        public const string UnstoppableForce = "unstoppableForce";
        public const string Healer = "healer";
        public const string YayIGuess = "yayIGuess";
        public const string NotDoneYet = "notDoneYet";
        public const string KissOfDeath = "kissOfDeath";
        public const string NotOnMyWatch = "notOnMyWatch";

        public static readonly Achievement[] All =
        {
            new Achievement(BlackHole, "Black Hole", "Banish a Hornet Shaman."),
            new Achievement(TotalDestruction, "Total Destruction", "Kill 3 teammates with one Thermal Detonation."),
            new Achievement(KeepYouSafe, "I Will Keep You Safe", "Banish a teammate."),
            new Achievement(UnlimitedPower, "Unlimited Power", "Use Bottomless Clip while holding a Particle Blade made with Size Matters and Thick Blades at level 2."),
            new Achievement(Doomsday, "Doomsday Scenario", "Send an armed Biggest Grenade into the Pocket Dimension."),
            new Achievement(Begone, "Begone", "Knock an enemy into lava with Kinetic Pulse."),
            new Achievement(UnstoppableForce, "Unstoppable Force Against Immovable Object", "Survive a Biggest Grenade blast inside Null Field."),
            new Achievement(Healer, "Healer", "Revive the same teammate twice in one wave or round with Neural Backup."),
            new Achievement(YayIGuess, "Yay I Guess?", "Pick the Nothing perk when Luck made it a Lucky Upgrade."),
            new Achievement(NotDoneYet, "You Are Not Done Yet", "Revive an astral ghost with Neural Backup."),
            new Achievement(KissOfDeath, "Kiss of Death", "Hit a Power Twin Blade Whisp with a Particle Blade below 15% durability."),
            new Achievement(NotOnMyWatch, "Not On My Watch", "Block a laser beam headed for a teammate while Null Field makes you immune."),
        };

        private static readonly Dictionary<string, AchievementUnlock> _unlocked = new Dictionary<string, AchievementUnlock>();
        private static bool _loaded;
        private static bool _legacyChecked;

        public static event Action<Achievement> Unlocked;

        private static Dictionary<string, AchievementUnlock> Data
        {
            get
            {
                EnsureLoaded();
                return _unlocked;
            }
        }

        public static int UnlockedCount => Array.FindAll(All, achievement => Data.ContainsKey(achievement.Id)).Length;

        public static bool IsUnlocked(string id) => Data.ContainsKey(id);

        public static AchievementUnlock UnlockOf(string id) => Data.TryGetValue(id, out AchievementUnlock unlock) ? unlock : null;

        // Only the machine that runs the game logic decides; a remote client never unlocks anything
        public static void Unlock(string id, PlayerController player)
        {
            if (!Net.IsAuthority || IsUnlocked(id)) return;
            Achievement achievement = Array.Find(All, candidate => candidate.Id == id);
            if (achievement == null) return;

            string name = player != null ? Players.NameOf(player) : "";
            _unlocked[id] = new AchievementUnlock { Date = DateTime.Now, Player = name };
            Save();
            Logger.LogInfo($"Achievement unlocked: {achievement.Name}{(name.Length > 0 ? " by " + name : "")}");
            ShowPopup(achievement, name);
            Unlocked?.Invoke(achievement);
            SettingsPage.RefreshOpen();
        }

        public static void ResetAll()
        {
            if (!EnsureLoaded())
            {
                Logger.LogWarning($"Couldn't reset the achievements: {ModData.FilePath} can't be read right now");
                return;
            }
            _unlocked.Clear();
            Save();
            Logger.LogInfo("Achievements reset");
        }

        // An achievement unlocked while the file can't be read is kept and saved once it can be
        private static bool EnsureLoaded()
        {
            if (!_loaded)
            {
                if (!ModData.Readable) return false;
                var pending = new Dictionary<string, AchievementUnlock>(_unlocked);
                _unlocked.Clear();
                foreach (string line in ModData.Lines(Section))
                {
                    if (TryParse(line, out string id, out AchievementUnlock unlock)) _unlocked[id] = unlock;
                }
                _loaded = true;
                if (Merge(pending)) Save();
            }
            if (!_legacyChecked && GameSave.Ready)
            {
                _legacyChecked = true;
                ImportLegacy();
            }
            return true;
        }

        // Older versions kept the achievements in the game's save; they move to the progress file once, and the old entry goes only after that is saved
        private static void ImportLegacy()
        {
            if (!GameSave.Has(LegacyKey)) return;
            string saved = GameSave.Load(LegacyKey, "") ?? "";
            var old = new Dictionary<string, AchievementUnlock>();
            foreach (string entry in saved.Split('\n'))
            {
                string[] fields = entry.Split(FieldSeparator);
                if (fields.Length < 2 || !IsId(fields[0])) continue;
                DateTime.TryParse(fields[1], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime date);
                old[fields[0]] = new AchievementUnlock { Date = date, Player = fields.Length > 2 ? fields[2] : "" };
            }
            if (Merge(old) && !Save()) return;
            GameSave.Delete(new[] { LegacyKey });
            if (old.Count > 0) Logger.LogInfo($"Moved {old.Count} achievements from the game's save to {ModData.FilePath}");
        }

        private static bool Merge(Dictionary<string, AchievementUnlock> other)
        {
            bool changed = false;
            foreach (KeyValuePair<string, AchievementUnlock> entry in other)
            {
                if (_unlocked.TryGetValue(entry.Key, out AchievementUnlock mine) && mine.Date <= entry.Value.Date) continue;
                _unlocked[entry.Key] = entry.Value;
                changed = true;
            }
            return changed;
        }

        private static bool TryParse(string line, out string id, out AchievementUnlock unlock)
        {
            id = null;
            unlock = null;
            string[] fields = line.Split(FieldSeparator);
            if (fields.Length != 3 || !IsId(fields[0]) || !DateTime.TryParse(fields[1], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime date)) return false;
            try
            {
                unlock = new AchievementUnlock { Date = date, Player = Encoding.UTF8.GetString(Convert.FromBase64String(fields[2])) };
            }
            catch (FormatException)
            {
                return false;
            }
            id = fields[0];
            return true;
        }

        private static bool IsId(string id) => id.Length > 0 && id.All(char.IsLetterOrDigit);

        private static bool Save()
        {
            if (!_loaded) return false;
            var lines = new List<string>();
            foreach (KeyValuePair<string, AchievementUnlock> entry in _unlocked)
            {
                lines.Add(entry.Key + FieldSeparator
                    + entry.Value.Date.ToString("o", CultureInfo.InvariantCulture) + FieldSeparator
                    + Convert.ToBase64String(Encoding.UTF8.GetBytes(entry.Value.Player ?? "")));
            }
            return ModData.Write(Section, lines);
        }

        private static void ShowPopup(Achievement achievement, string player)
        {
            try
            {
                UIPopup popup = UIPopup.GetPopup("TextPopUp");
                if (popup == null) return;

                string by = player.Length > 0 ? $"\n<size=80%>{player}</size>" : "";
                popup.Data.SetLabelsTexts(new[] { $"Achievement unlocked\n<b>{achievement.Name}</b>{by}" });
                if (popup.Data.Labels.Count > 0 && popup.Data.Labels[0] != null)
                {
                    var label = popup.Data.Labels[0].GetComponent<TextMeshProUGUI>();
                    if (label != null) label.color = Hud.Warm;
                }
                UIPopupManager.ShowPopup(popup, true, false);
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Could not show the achievement popup: {ex.Message}");
            }
        }
    }
}
