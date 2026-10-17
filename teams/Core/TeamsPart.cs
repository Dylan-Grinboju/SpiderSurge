using System;
using SpiderSurge.Common;
using UnityEngine;
using Logger = Silk.Logger;

namespace SpiderTeams
{
    public class TeamsPart : Part
    {
        public override string Key => "teams";
        public override string Title => "Teams";
        public override string Description => "Team versus: pick teams in the lobby, friendly fire rules, team colors, and Surge abilities in versus.";
        public override string Namespace => "SpiderTeams";
        public override bool OnByDefault => false;

        protected internal override void OnStart()
        {
            ModConfig.Initialize();
            CreateManager();
            ReflectionCache.ReportMissing();
            TeamNet.Initialize();
        }

        protected internal override void OnStop()
        {
            try
            {
                if (TeamVisuals.Instance != null)
                {
                    TeamVisuals.Instance.RestoreAll();
                }
                LobbyLabel.Restore();
                SurgeVersus.Stop();
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"Teams could not restore the game's visuals: {ex.Message}");
            }

            GameObject manager = TeamManager.Instance != null ? TeamManager.Instance.gameObject : GameObject.Find(Consts.ManagerObjectName);
            if (manager != null)
            {
                UnityEngine.Object.Destroy(manager);
            }

            VersusTeamsPage.RemoveAll();
            TeamNet.Shutdown();
            ModConfig.Shutdown();
            OwnerRegistry.Clear();
            ExplosionOwners.Clear();
            DamageContext.Clear();
            SpawnScope.Clear();
            Announcements.Clear();
            MatchSnapshot.Clear();
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
    }
}
