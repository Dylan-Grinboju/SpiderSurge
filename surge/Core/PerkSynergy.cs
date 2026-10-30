using P = SpiderSurge.Consts.PerkNames;
using M = SpiderSurge.Consts.ModifierNames;

namespace SpiderSurge
{
    public static class PerkSynergy
    {
        public static string Describe(string key, int offeredLevel)
        {
            var perks = PerksManager.Instance;
            if (perks == null || key == null) return null;
            if ((perks.GetPerkLevel(P.ImmuneAbility) > 0 || perks.GetPerkLevel(P.ImmuneAbilityUltimate) > 0)
                && (key == M.StartShields || key == M.PositiveEncouragement || key == M.SafetyNet))
                return "Null Field keeps your shield if it blocks no hits. Starting Neural Backup shielded can shield the revived player or summon an extra Wasp.";
            if (perks.GetPerkLevel(P.AmmoAbility) > 0 && key == M.Efficiency)
                return offeredLevel >= 2
                    ? "Bottomless Clip keeps dropped weapons with their original ammo and refills your held weapon to at least half capacity when it ends."
                    : "Bottomless Clip keeps weapons with their original ammo when it ends or you drop them.";
            if (perks.GetPerkLevel(P.PulseAbility) > 0 || perks.GetPerkLevel(P.PulseAbilityUltimate) > 0)
            {
                if (key == M.BiggerBoom) return offeredLevel >= 2
                    ? "Stronger knockback for both Kinetic Pulse and Thermal Detonation."
                    : "Stronger Kinetic Pulse knockback. Level 2 also strengthens Thermal Detonation.";
                if (key == M.TooCool) return "Thermal Detonation kills enemies in a wider area.";
            }
            if (perks.GetPerkLevel(P.StorageAbility) > 0)
            {
                string weapons = key == M.MoreGuns ? "guns" : key == M.MoreBoom ? "explosives, throwables and mines"
                    : key == M.MoreParticles ? "particle and melee weapons" : null;
                if (weapons != null) return $"Pocket Dimension keeps stored {weapons} across map changes" + (offeredLevel >= 2 ? " and death." : ". Level 2 also keeps them after death.");
            }
            if ((perks.GetPerkLevel(P.FreezeAbility) > 0 || perks.GetPerkLevel(P.FreezeAbilityUltimate) > 0) && IsSlowMotionPerk(key))
                return "Freeze Time and Rewind recharge faster during perk slow motion. Overlapping slow motion does not strengthen this effect.";
            return null;
        }

        public static bool IsSlowMotionPerk(string key) => key == M.Adrenaline || key == M.Blademaster || key == M.CoolExplosions;
    }
}
