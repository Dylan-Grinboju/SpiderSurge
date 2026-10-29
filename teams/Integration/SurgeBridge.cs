using SpiderSurge;
using SpiderSurge.Common;

namespace SpiderTeams
{
    internal static class SurgeBridge
    {
        public static bool Available => Parts.Surge.Running;

        // Versus activates abilities independently of the selected survival platform.
        public static bool SetRunActive(bool active)
        {
            SurgeGameModeManager manager = Available ? SurgeGameModeManager.Instance : null;
            if (manager == null)
            {
                return false;
            }
            manager.ForceActive(active);
            return true;
        }

        public static void EndRun()
        {
            if (Available)
            {
                SurgeGameModeManager.EndRun(RunEnd.Versus);
            }
        }

        public static bool ResetPerks()
        {
            PerksManager perks = Perks();
            if (perks == null)
            {
                return false;
            }
            perks.ResetPerks();
            perks.IsFirstNormalPerkSelection = false;
            return true;
        }

        public static bool SetPerkLevel(string perk, int level)
        {
            PerksManager perks = Perks();
            if (perks == null)
            {
                return false;
            }
            perks.SetPerkLevel(perk, level);
            return true;
        }

        public static void ResetSpawnTracking()
        {
            if (Available)
            {
                PlayerAbilityHandler.ResetSpawnTracking();
            }
        }

        public static void ClearStoredWeapons()
        {
            if (Available)
            {
                StoragePersistenceManager.ClearAllStoredWeapons();
            }
        }

        // Care Package caches the spawners per scene name, which goes stale when a map repeats or on custom maps.
        public static void ForgetWeaponSpawners()
        {
            if (Available)
            {
                AmmoAbility.ForgetWeaponSpawners();
            }
        }

        private static PerksManager Perks()
        {
            return Available ? PerksManager.Instance : null;
        }
    }
}
