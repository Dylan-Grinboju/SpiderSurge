using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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
        private const string LegacySource = "gamesave.achievements";
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
        public const string MaxCapacity = "maxCapacity";
        public const string FactoryReset = "factoryReset";
        public const string HoldThatThought = "holdThatThought";
        public const string ColdCuts = "coldCuts";
        public const string StillSharp = "stillSharp";
        public const string ReturnToSender = "returnToSender";
        public const string EvictionNotice = "evictionNotice";
        public const string PersonalSpace = "personalSpace";
        public const string FamilyHeirloom = "familyHeirloom";
        public const string LightSnack = "lightSnack";
        public const string Overkill = "overkill";
        public const string WeBroughtOurOwn = "weBroughtOurOwn";

        public static readonly Achievement[] All =
        {
            new Achievement(BlackHole, "Event Horizon", "Feed a Hornet Shaman to your Black Hole."),
            new Achievement(TotalDestruction, "Total Destruction", "Kill 3 teammates with one Thermal Detonation."),
            new Achievement(KeepYouSafe, "I Will Keep You Safe", "Feed a teammate to your Black Hole."),
            new Achievement(UnlimitedPower, "Unlimited Power", "Use Bottomless Clip while holding a Particle Blade made with Size Matters and Thick Blades at level 2."),
            new Achievement(Doomsday, "Doomsday Scenario", "Send an armed Biggest Grenade into the Pocket Dimension."),
            new Achievement(Begone, "Begone", "Knock an enemy into lava with Kinetic Pulse."),
            new Achievement(UnstoppableForce, "Unstoppable Force Against Immovable Object", "Survive a Biggest Grenade blast inside Null Field."),
            new Achievement(Healer, "Healer", "Revive the same teammate twice in one wave or round with Neural Backup."),
            new Achievement(YayIGuess, "Yay I Guess?", "Pick the Nothing perk when Luck made it a Lucky Upgrade."),
            new Achievement(NotDoneYet, "You Are Not Done Yet", "Revive an astral ghost with Neural Backup."),
            new Achievement(KissOfDeath, "Kiss of Death", "Hit a Power Twin Blade Whisp with a Particle Blade below 15% durability."),
            new Achievement(NotOnMyWatch, "Not On My Watch", "Block a laser beam headed for a teammate while Null Field makes you immune."),
            new Achievement(MaxCapacity, "Max Capacity", "Buy a Bigger Heart in the Surge shop."),
            new Achievement(FactoryReset, "Factory Reset", "Turn a Wasp Cocoon into a Wasp with Rewind."),
            new Achievement(HoldThatThought, "Hold That Thought", "Freeze a laser enemy while its laser ring is firing."),
            new Achievement(ColdCuts, "Cold Cuts", "Kill 3 frozen enemies with a Particle Blade during one Freeze Time activation."),
            new Achievement(StillSharp, "Still Sharp", "Die to a frozen enemy."),
            new Achievement(ReturnToSender, "Return to Sender", "Push a Missile Whisp's rocket back with Kinetic Pulse and kill its shooter with the blast."),
            new Achievement(EvictionNotice, "Eviction Notice", "Push a Wasp Cocoon into lava with Kinetic Pulse."),
            new Achievement(PersonalSpace, "Personal Space", "Knock 5 enemies away with one Kinetic Pulse."),
            new Achievement(FamilyHeirloom, "Family Heirloom", "Carry the same stored weapon through 3 map changes."),
            new Achievement(LightSnack, "Light Snack", "Swallow 3 different enemy types with one Black Hole."),
            new Achievement(Overkill, "Overkill", "Use Thermal Detonation to kill the wave's last enemy: a single plain Wasp."),
            new Achievement(WeBroughtOurOwn, "We Brought Our Own", "Have a friendly Wasp summoned by Neural Backup kill a Wasp spawned by a Cocoon."),
        };

        private static readonly ProgressSection<AchievementUnlock> _unlocks = new ProgressSection<AchievementUnlock>(Section, TryParse, Format, Earlier, AnnounceAll);
        private static bool _legacyChecked;

        public static int UnlockedCount => All.Count(achievement => IsUnlocked(achievement.Id));

        public static bool IsUnlocked(string id)
        {
            ImportLegacy();
            return _unlocks.Contains(id);
        }

        public static AchievementUnlock UnlockOf(string id)
        {
            ImportLegacy();
            return _unlocks.TryGet(id, out AchievementUnlock unlock) ? unlock : null;
        }

        // Only the machine that runs the game logic decides; a remote client never unlocks anything. One unlocked while the file can't be read is announced once it can, if the file doesn't already have it
        public static void Unlock(string id, PlayerController player)
        {
            if (!Net.IsAuthority || IsUnlocked(id) || Array.Find(All, candidate => candidate.Id == id) == null) return;
            bool loaded = _unlocks.Loaded;
            if (loaded) CollectionSeen.Ensure("achievements", _unlocks.Entries.Keys);
            _unlocks.Add(id, new AchievementUnlock { Date = DateTime.Now, Player = player != null ? Players.NameOf(player) : "" });
            if (loaded) Announce(id);
        }

        public static void ResetAll()
        {
            if (!_unlocks.Clear())
            {
                Logger.LogWarning($"Couldn't reset the achievements: {ModData.FilePath} can't be read right now");
                return;
            }
            CollectionSeen.Reset("achievements");
            Logger.LogInfo("Achievements reset");
        }

        // The older save is merged first, so an unlock it already had isn't announced again
        private static void AnnounceAll(List<KeyValuePair<string, AchievementUnlock>> waited)
        {
            ImportLegacy();
            CollectionSeen.Ensure("achievements", _unlocks.Entries.Keys.Except(waited.Select(entry => entry.Key)));
            foreach (var entry in waited)
            {
                if (_unlocks.TryGet(entry.Key, out AchievementUnlock unlock) && unlock == entry.Value) Announce(entry.Key);
            }
        }

        private static void Announce(string id)
        {
            Achievement achievement = Array.Find(All, candidate => candidate.Id == id);
            if (achievement == null || !_unlocks.TryGet(id, out AchievementUnlock unlock)) return;
            string name = unlock.Player ?? "";
            Logger.LogInfo($"Achievement unlocked: {achievement.Name}{(name.Length > 0 ? " by " + name : "")}");
            ShowPopup(achievement, name);
            SettingsPage.RefreshOpen();
        }

        // Older versions kept the achievements in the game's save; they move to the progress file once, and the old entry goes once that is saved
        private static void ImportLegacy()
        {
            if (_legacyChecked || !GameSave.Ready || !_unlocks.Loaded) return;
            _legacyChecked = true;
            if (!GameSave.Has(LegacyKey)) return;
            if (!ModData.Imported(LegacySource))
            {
                string saved = GameSave.Load(LegacyKey, "") ?? "";
                var old = new Dictionary<string, AchievementUnlock>();
                foreach (string entry in saved.Split('\n'))
                {
                    string[] fields = entry.Split(FieldSeparator);
                    if (fields.Length < 2 || !IsId(fields[0])) continue;
                    DateTime.TryParse(fields[1], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime date);
                    old[fields[0]] = new AchievementUnlock { Date = date, Player = fields.Length > 2 ? fields[2] : "" };
                }
                if (!_unlocks.Import(LegacySource, old)) return;
                if (old.Count > 0) Logger.LogInfo($"Moved {old.Count} achievements from the game's save to {ModData.FilePath}");
            }
            GameSave.Delete(new[] { LegacyKey });
        }

        private static AchievementUnlock Earlier(AchievementUnlock saved, AchievementUnlock other) => other.Date < saved.Date ? other : saved;

        private static bool TryParse(string line, out string id, out AchievementUnlock unlock)
        {
            id = null;
            unlock = null;
            string[] fields = line.Split(FieldSeparator);
            if (fields.Length != 3 || !IsId(fields[0]) || !DateTime.TryParse(fields[1], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime date)) return false;
            try
            {
                unlock = new AchievementUnlock { Date = date, Player = ModData.Decode(fields[2]) };
            }
            catch (FormatException)
            {
                return false;
            }
            id = fields[0];
            return true;
        }

        private static bool IsId(string id) => id.Length > 0 && id.All(char.IsLetterOrDigit);

        private static string Format(string id, AchievementUnlock unlock)
        {
            return id + FieldSeparator + unlock.Date.ToString("o", CultureInfo.InvariantCulture) + FieldSeparator + ModData.Encode(unlock.Player ?? "");
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
