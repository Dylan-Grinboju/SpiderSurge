using P = SpiderSurge.Consts.PerkNames;
using M = SpiderSurge.Consts.ModifierNames;

namespace SpiderSurge
{
    public static class PerkSynergy
    {
        public static bool Improves(string key, int currentLevel, int offeredLevel)
        {
            var perks = PerksManager.Instance;
            if (key != M.BiggerBoom || perks == null) return Describe(key, offeredLevel) != null;
            return perks.GetPerkLevel(P.PulseAbility) > 0 && currentLevel < 1 && offeredLevel >= 1
                || perks.GetPerkLevel(P.PulseAbilityUltimate) > 0 && currentLevel < 2 && offeredLevel >= 2;
        }

        public static string Describe(string key, int offeredLevel)
        {
            var perks = PerksManager.Instance;
            if (perks == null || key == null) return null;
            if (key == M.StartShields || key == M.PositiveEncouragement || key == M.SafetyNet)
            {
                bool field = perks.GetPerkLevel(P.ImmuneAbility) > 0;
                bool backup = perks.GetPerkLevel(P.ImmuneAbilityUltimate) > 0;
                string shield = field ? "Null Field keeps a shield held when casting if it blocks no hits." : "";
                string revive = backup ? "Starting Neural Backup shielded can shield the revived player or summon an extra Wasp." : "";
                if (field || backup) return field && backup ? shield + " " + revive : field ? shield : revive;
            }
            if (perks.GetPerkLevel(P.AmmoAbility) > 0 && key == M.Efficiency)
                return offeredLevel >= 2
                    ? "50% chance to salvage an Overcharged weapon when the burst ends or it is dropped. Salvage keeps up to one shot, or 10% blade durability."
                    : "25% chance to salvage an Overcharged weapon when the burst ends or it is dropped. Level 2 raises this to 50%. Salvage keeps a small ammo reserve.";
            bool pulse = perks.GetPerkLevel(P.PulseAbility) > 0;
            bool detonation = perks.GetPerkLevel(P.PulseAbilityUltimate) > 0;
            if (key == M.BiggerBoom && (pulse || detonation))
            {
                if (!pulse) return offeredLevel >= 2
                    ? "Stronger Thermal Detonation knockback."
                    : "Level 2 strengthens Thermal Detonation knockback; level 1 does not change it.";
                if (!detonation) return offeredLevel >= 2
                    ? "Level 1 strengthens Kinetic Pulse knockback; level 2 adds no further Pulse knockback."
                    : "Stronger Kinetic Pulse knockback.";
                return offeredLevel >= 2
                    ? "Stronger knockback for Kinetic Pulse at level 1 and Thermal Detonation at level 2."
                    : "Stronger Kinetic Pulse knockback. Level 2 also strengthens Thermal Detonation.";
            }
            if (detonation && key == M.TooCool) return "Thermal Detonation kills enemies in a wider area.";
            if (perks.GetPerkLevel(P.StorageAbility) > 0)
            {
                string weapons = key == M.MoreGuns ? "guns" : key == M.MoreBoom ? "explosives, throwables and mines"
                    : key == M.MoreParticles ? "particle and melee weapons" : null;
                if (weapons != null) return $"Pocket Dimension keeps stored {weapons} across map changes" + (offeredLevel >= 2 ? " and death." : ". Level 2 also keeps them after death.");
            }
            bool freeze = perks.GetPerkLevel(P.FreezeAbility) > 0;
            bool rewind = perks.GetPerkLevel(P.FreezeAbilityUltimate) > 0;
            if ((freeze || rewind) && IsSlowMotionPerk(key))
            {
                string actions = freeze && rewind ? "Freeze Time or Rewind" : freeze ? "Freeze Time" : "Rewind";
                return $"Perk slow motion shortens cooldowns started by {actions}. Other actions' cooldowns are unchanged; overlaps do not stack.";
            }
            return null;
        }

        public static bool IsSlowMotionPerk(string key) => key == M.Adrenaline || key == M.Blademaster || key == M.CoolExplosions;
    }
}
