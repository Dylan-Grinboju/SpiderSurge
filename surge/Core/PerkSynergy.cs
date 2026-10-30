using System.Collections.Generic;
using SpiderSurge.Common;
using P = SpiderSurge.Consts.PerkNames;
using M = SpiderSurge.Consts.ModifierNames;

namespace SpiderSurge
{
    public static class PerkSynergy
    {
        [System.Flags]
        private enum Actions { None = 0, Field = 1, Overcharge = 2, Pulse = 4, Storage = 8, Freeze = 16, Backup = 32, Detonation = 64, Rewind = 128 }

        private static readonly (string key, Actions flag)[] ActionKeys =
        {
            (P.ImmuneAbility, Actions.Field), (P.AmmoAbility, Actions.Overcharge),
            (P.PulseAbility, Actions.Pulse), (P.StorageAbility, Actions.Storage), (P.FreezeAbility, Actions.Freeze),
            (P.ImmuneAbilityUltimate, Actions.Backup), (P.PulseAbilityUltimate, Actions.Detonation), (P.FreezeAbilityUltimate, Actions.Rewind)
        };

        internal static int SelectionStamp => (int)Selected;

        private static Actions Selected
        {
            get
            {
                var perks = PerksManager.Instance;
                var players = Players.All;
                if (perks == null || players == null) return Actions.None;
                Actions selected = Actions.None;
                foreach (var player in players)
                {
                    if (player == null) continue;
                    if (!perks.HasPersonalLoadouts)
                    {
                        foreach (var action in ActionKeys)
                            if (perks.GetPerkLevel(action.key) > 0) selected |= action.flag;
                        break;
                    }
                    int id = player.playerID.Value;
                    string regular = perks.Loadouts.Get(id, false), ultimate = perks.Loadouts.Get(id, true);
                    foreach (var action in ActionKeys)
                        if (action.key == regular || action.key == ultimate) selected |= action.flag;
                }
                return selected;
            }
        }

        private static Actions Affected(string key, int currentLevel, int offeredLevel)
        {
            Actions selected = Selected;
            if (key == M.StartShields || key == M.PositiveEncouragement || key == M.SafetyNet)
                return selected & (Actions.Field | Actions.Backup);
            if (key == M.Efficiency) return selected & Actions.Overcharge;
            if (key == M.BiggerBoom)
            {
                Actions affected = Actions.None;
                if (currentLevel < 1 && offeredLevel >= 1) affected |= selected & Actions.Pulse;
                if (currentLevel < 2 && offeredLevel >= 2) affected |= selected & Actions.Detonation;
                return affected;
            }
            if (key == M.TooCool) return selected & Actions.Detonation;
            if (key == M.MoreGuns || key == M.MoreBoom || key == M.MoreParticles) return selected & Actions.Storage;
            return IsSlowMotionPerk(key) ? selected & (Actions.Freeze | Actions.Rewind) : Actions.None;
        }

        public static bool Improves(string key, int currentLevel, int offeredLevel) => Affected(key, currentLevel, offeredLevel) != Actions.None;

        private static string Names(Actions actions)
        {
            var names = new List<string>();
            foreach (var action in ActionKeys)
                if ((actions & action.flag) != 0) names.Add(Consts.Descriptions.GetDisplayName(action.key));
            return string.Join(" / ", names);
        }

        public static string Label(string key, int currentLevel, int offeredLevel)
        {
            Actions actions = Affected(key, currentLevel, offeredLevel);
            return actions == Actions.None ? null : $"<color={Consts.Formatting.ColorCyan}>Synergized: {Names(actions)}</color>";
        }

        public static string Decorate(string description, string key, int currentLevel, int offeredLevel, bool lucky)
        {
            string text = description ?? "";
            int previous = text.IndexOf("\n<color=" + Consts.Formatting.ColorCyan + ">Synergized", System.StringComparison.Ordinal);
            if (previous >= 0) text = text.Substring(0, previous);
            text = text.Replace("\n" + Consts.Formatting.TextLuckyUpgrade, "");
            if (lucky) text += "\n" + Consts.Formatting.TextLuckyUpgrade;
            string label = Label(key, currentLevel, offeredLevel);
            return label == null ? text : text + "\n" + label;
        }

        public static string Describe(string key, int offeredLevel, int currentLevel = -1)
        {
            if (currentLevel < 0) currentLevel = System.Math.Max(0, offeredLevel - 1);
            Actions actions = Affected(key, currentLevel, offeredLevel);
            if (actions == Actions.None) return null;
            if (key == M.StartShields || key == M.PositiveEncouragement || key == M.SafetyNet)
            {
                bool field = (actions & Actions.Field) != 0, backup = (actions & Actions.Backup) != 0;
                string shield = "Null Field keeps a shield held when casting if it blocks no hits.";
                string revive = "Starting Neural Backup shielded can shield the revived player or summon an extra Wasp.";
                return field && backup ? shield + " " + revive : field ? shield : revive;
            }
            if (key == M.Efficiency)
                return offeredLevel >= 2
                    ? "Overcharge: 50% chance to salvage the weapon when the burst ends or it is dropped. Salvage keeps up to one shot, or 10% blade durability."
                    : "Overcharge: 25% chance to salvage the weapon when the burst ends or it is dropped. Salvage keeps a small ammo reserve.";
            if (key == M.BiggerBoom) return "Stronger knockback for " + Names(actions) + ".";
            if (key == M.TooCool) return "Thermal Detonation kills enemies in a wider area.";
            if (key == M.MoreGuns || key == M.MoreBoom || key == M.MoreParticles)
            {
                string weapons = key == M.MoreGuns ? "guns" : key == M.MoreBoom ? "explosives, throwables and mines" : "particle and melee weapons";
                return $"Pocket Dimension keeps stored {weapons} across map changes" + (offeredLevel >= 2 ? " and death." : ". Level 2 also keeps them after death.");
            }
            return $"Perk slow motion shortens cooldowns started by {Names(actions)}. Other actions' cooldowns are unchanged; overlaps do not stack.";
        }

        public static bool IsSlowMotionPerk(string key) => key == M.Adrenaline || key == M.Blademaster || key == M.CoolExplosions;
    }
}
