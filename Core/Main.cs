using System;
using System.Reflection;
using HarmonyLib;
using Silk;
using UnityEngine;
using Logger = Silk.Logger;

namespace SpiderTeams
{
    [SilkMod(SpiderTeamsMod.ModName, new string[] { "Dylan" }, "0.1.0", "0.7.0", SpiderTeamsMod.ModId, 1)]
    public class SpiderTeamsMod : SilkMod
    {
        public const string ModName = "SpiderTeams";
        public const string ModId = "SpiderTeams";

        public static SpiderTeamsMod Instance { get; private set; }

        private static string _version;
        public static string Version
        {
            get
            {
                if (_version == null)
                {
                    var version = Assembly.GetExecutingAssembly().GetName().Version;
                    _version = $"{version.Major}.{version.Minor}.{version.Build}";
                }
                return _version;
            }
        }

        private Harmony _harmony;

        public override void Initialize()
        {
            Instance = this;
            Logger.LogInfo($"Initializing SpiderTeams {Version}...");

            ModConfig.Load();
            CreateManager();
            ReflectionCache.ReportMissing();
            ApplyPatches();
            ModUpdater.ScheduleCheck();
        }

        public override void Unload()
        {
            Logger.LogInfo("Unloading SpiderTeams...");

            try
            {
                if (TeamVisuals.Instance != null)
                {
                    TeamVisuals.Instance.RestoreAll();
                }
                LobbyLabel.Restore();
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"SpiderTeams could not restore the game's visuals: {ex.Message}");
            }

            (_harmony ?? new Harmony(Consts.HarmonyId)).UnpatchSelf();
            _harmony = null;

            GameObject manager = TeamManager.Instance != null ? TeamManager.Instance.gameObject : GameObject.Find(Consts.ManagerObjectName);
            if (manager != null)
            {
                Destroy(manager);
            }

            OwnerRegistry.Clear();
            ExplosionOwners.Clear();
            DamageContext.Clear();
            SpawnScope.Clear();
            Announcements.Clear();
            MatchSnapshot.Clear();
            Instance = null;
        }

        private static void CreateManager()
        {
            if (TeamManager.Instance != null || GameObject.Find(Consts.ManagerObjectName) != null)
            {
                return;
            }

            var manager = new GameObject(Consts.ManagerObjectName);
            manager.AddComponent<TeamManager>();
            manager.AddComponent<TeamVisuals>();
            manager.AddComponent<TeamMarkers>();
            manager.AddComponent<LobbyTeamPicker>();
        }

        // Patches class by class, so one missing game method only disables the feature that needs it.
        private void ApplyPatches()
        {
            _harmony = new Harmony(Consts.HarmonyId);
            foreach (Type type in AccessTools.GetTypesFromAssembly(Assembly.GetExecutingAssembly()))
            {
                try
                {
                    _harmony.CreateClassProcessor(type).Patch();
                }
                catch (Exception ex)
                {
                    Logger.LogError($"SpiderTeams could not apply {type.Name}: {ex.GetBaseException().Message}");
                }
            }

            int count = 0;
            foreach (MethodBase method in _harmony.GetPatchedMethods())
            {
                count++;
                Logger.LogInfo($"SpiderTeams patched {(method.DeclaringType != null ? method.DeclaringType.Name : "?")}.{method.Name}");
            }
            Logger.LogInfo($"SpiderTeams applied patches to {count} methods");
        }
    }
}
