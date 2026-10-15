using System;
using System.Reflection;
using HarmonyLib;
using Logger = Silk.Logger;

namespace SpiderTeams
{
    // Reflection bridge to SpiderSurge. Resolved on first use, so it works whatever order Silk loads the mods in.
    internal static class SurgeBridge
    {
        private static bool _resolved;
        private static bool _broken;
        private static bool _loggedMissing;
        private static bool _patched;

        private static PropertyInfo _modeInstance;
        private static MethodInfo _setModeActive;
        private static PropertyInfo _perksInstance;
        private static MethodInfo _setPerkLevel;
        private static MethodInfo _resetPerks;
        private static MethodInfo _resetSpawnTracking;
        private static MethodInfo _clearStoredWeapons;
        private static FieldInfo _weaponSpawners;

        public static MethodInfo PulsePhysics { get; private set; }
        public static MethodInfo ImmuneRevive { get; private set; }
        public static MethodInfo CarePackageWeapon { get; private set; }
        public static string Version { get; private set; }

        public static bool Installed => FindAssembly() != null;

        public static bool Broken => _broken;

        public static bool Available
        {
            get
            {
                if (!_resolved && !_broken)
                {
                    Resolve();
                }
                return _resolved;
            }
        }

        public static void Reset()
        {
            _resolved = false;
            _broken = false;
            _patched = false;
            _modeInstance = null;
            _setModeActive = null;
            _perksInstance = null;
            _setPerkLevel = null;
            _resetPerks = null;
            _resetSpawnTracking = null;
            _clearStoredWeapons = null;
            _weaponSpawners = null;
            PulsePhysics = null;
            ImmuneRevive = null;
            CarePackageWeapon = null;
        }

        // Flips SpiderSurge's run flag directly so versus doesn't depend on its survival-only Surge Mode toggle.
        public static bool SetRunActive(bool active)
        {
            if (!Available)
            {
                return false;
            }

            object manager = _modeInstance.GetValue(null, null);
            if (manager == null)
            {
                return false;
            }
            _setModeActive.Invoke(manager, new object[] { active });
            return true;
        }

        public static bool ResetPerks()
        {
            object perks = Perks();
            if (perks == null)
            {
                return false;
            }
            _resetPerks.Invoke(perks, null);
            return true;
        }

        public static bool SetPerkLevel(string perk, int level)
        {
            object perks = Perks();
            if (perks == null)
            {
                return false;
            }
            _setPerkLevel.Invoke(perks, new object[] { perk, level });
            return true;
        }

        public static void ResetSpawnTracking()
        {
            if (Available)
            {
                _resetSpawnTracking.Invoke(null, null);
            }
        }

        public static void ClearStoredWeapons()
        {
            if (Available)
            {
                _clearStoredWeapons.Invoke(null, null);
            }
        }

        // Care Package caches the spawners per scene name, which goes stale when a map repeats or on custom maps.
        public static void ForgetWeaponSpawners()
        {
            if (Available && _weaponSpawners != null)
            {
                _weaponSpawners.SetValue(null, null);
            }
        }

        private static object Perks()
        {
            return Available ? _perksInstance.GetValue(null, null) : null;
        }

        private static Assembly FindAssembly()
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                try
                {
                    if (assembly.GetType("SpiderSurge.PerksManager", false) != null)
                    {
                        return assembly;
                    }
                }
                catch
                {
                }
            }
            return null;
        }

        private static void Resolve()
        {
            Assembly assembly = FindAssembly();
            if (assembly == null)
            {
                if (!_loggedMissing)
                {
                    _loggedMissing = true;
                    Logger.LogInfo("SpiderTeams: SpiderSurge is not installed, so Surge abilities are off in versus");
                }
                return;
            }

            try
            {
                Type mode = assembly.GetType("SpiderSurge.SurgeGameModeManager");
                Type perks = assembly.GetType("SpiderSurge.PerksManager");
                Type handler = assembly.GetType("SpiderSurge.PlayerAbilityHandler");
                Type storage = assembly.GetType("SpiderSurge.StoragePersistenceManager");
                Type pulse = assembly.GetType("SpiderSurge.PulseAbility");
                Type immune = assembly.GetType("SpiderSurge.ImmuneAbility");
                Type ammo = assembly.GetType("SpiderSurge.AmmoAbility");
                Type main = assembly.GetType("SpiderSurge.SpiderSurgeMod");

                _modeInstance = mode != null ? AccessTools.Property(mode, "Instance") : null;
                PropertyInfo isActive = mode != null ? AccessTools.Property(mode, "IsActive") : null;
                _setModeActive = isActive != null ? isActive.GetSetMethod(true) : null;
                _perksInstance = perks != null ? AccessTools.Property(perks, "Instance") : null;
                _setPerkLevel = perks != null ? AccessTools.Method(perks, "SetPerkLevel", new[] { typeof(string), typeof(int) }) : null;
                _resetPerks = perks != null ? AccessTools.Method(perks, "ResetPerks", Type.EmptyTypes) : null;
                _resetSpawnTracking = handler != null ? AccessTools.Method(handler, "ResetSpawnTracking", Type.EmptyTypes) : null;
                _clearStoredWeapons = storage != null ? AccessTools.Method(storage, "ClearAllStoredWeapons", Type.EmptyTypes) : null;
                PulsePhysics = pulse != null ? AccessTools.Method(pulse, "ApplyPulsePhysics") : null;
                ImmuneRevive = immune != null ? AccessTools.Method(immune, "TryRespawnDeadPlayer") : null;
                CarePackageWeapon = ammo != null ? AccessTools.Method(ammo, "GetRandomWeaponPrefab", Type.EmptyTypes) : null;
                _weaponSpawners = ammo != null ? AccessTools.Field(ammo, "cachedWeaponSpawners") : null;

                PropertyInfo version = main != null ? AccessTools.Property(main, "Version") : null;
                Version = version != null ? version.GetValue(null, null) as string : null;
            }
            catch (Exception ex)
            {
                ReportBroken(ex.Message);
                return;
            }

            if (_modeInstance == null || _setModeActive == null || _perksInstance == null || _setPerkLevel == null ||
                _resetPerks == null || _resetSpawnTracking == null || _clearStoredWeapons == null)
            {
                ReportBroken("its API has changed");
                return;
            }

            _resolved = true;
            Logger.LogInfo($"SpiderTeams found SpiderSurge {Version ?? "(unknown version)"}");
            ApplyPatches();
        }

        // The patches target SpiderSurge's own methods, so they can only be applied once its assembly is known.
        private static void ApplyPatches()
        {
            Harmony harmony = SpiderTeamsMod.Harmony;
            if (_patched || harmony == null)
            {
                return;
            }
            _patched = true;

            foreach (Type patch in SurgePatches.Classes)
            {
                try
                {
                    harmony.CreateClassProcessor(patch).Patch();
                }
                catch (Exception ex)
                {
                    Logger.LogError($"SpiderTeams could not apply {patch.Name}: {ex.GetBaseException().Message}");
                }
            }
        }

        private static void ReportBroken(string reason)
        {
            _broken = true;
            Logger.LogWarning($"SpiderTeams found SpiderSurge but cannot use it ({reason}), so Surge abilities are off in versus");
        }
    }
}
