using System;
using System.Collections.Generic;
using System.Globalization;
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

    // Achievements belong to this game install and live in the game's save file, next to the Surge highscores
    public static class SurgeAchievements
    {
        private const string SaveKey = "SpiderSurge-Achievements";
        private const char EntrySeparator = '\n';
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

        private static readonly Dictionary<string, AchievementUnlock> NoneYet = new Dictionary<string, AchievementUnlock>();
        private static Dictionary<string, AchievementUnlock> _unlocked;

        public static event Action<Achievement> Unlocked;

        private static Dictionary<string, AchievementUnlock> Data
        {
            get
            {
                if (_unlocked == null && GameSaveWrapper.Instance != null) Load();
                return _unlocked ?? NoneYet;
            }
        }

        public static int UnlockedCount => Array.FindAll(All, achievement => Data.ContainsKey(achievement.Id)).Length;

        public static bool IsUnlocked(string id) => Data.ContainsKey(id);

        public static AchievementUnlock UnlockOf(string id) => Data.TryGetValue(id, out AchievementUnlock unlock) ? unlock : null;

        // Only the machine that runs the game logic decides; a remote client never unlocks anything
        public static void Unlock(string id, PlayerController player)
        {
            if (!Net.IsAuthority || IsUnlocked(id) || _unlocked == null) return;
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
            if (Data == NoneYet) return;
            _unlocked.Clear();
            Save();
            Logger.LogInfo("Achievements reset");
        }

        private static void Load()
        {
            _unlocked = new Dictionary<string, AchievementUnlock>();
            string saved = GameSaveWrapper.Instance.Load(SaveKey, "") ?? "";
            foreach (string entry in saved.Split(EntrySeparator))
            {
                string[] fields = entry.Split(FieldSeparator);
                if (fields.Length < 2 || fields[0].Length == 0) continue;
                DateTime.TryParse(fields[1], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime date);
                _unlocked[fields[0]] = new AchievementUnlock { Date = date, Player = fields.Length > 2 ? fields[2] : "" };
            }
        }

        private static void Save()
        {
            var text = new StringBuilder();
            foreach (KeyValuePair<string, AchievementUnlock> entry in _unlocked)
            {
                string player = (entry.Value.Player ?? "").Replace(FieldSeparator, ' ').Replace(EntrySeparator, ' ');
                text.Append(entry.Key).Append(FieldSeparator)
                    .Append(entry.Value.Date.ToString("o", CultureInfo.InvariantCulture)).Append(FieldSeparator)
                    .Append(player).Append(EntrySeparator);
            }
            GameSaveWrapper.Instance.Save(SaveKey, text.ToString());
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
