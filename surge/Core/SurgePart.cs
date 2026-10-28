using System.Collections.Generic;
using SpiderSurge.Common;
using UnityEngine;

namespace SpiderSurge
{
    public class SurgePart : Part
    {
        private static readonly string[] Objects =
        {
            "SurgeGameModeManager", "PerksManager", "SoundManager", "PlayerControlSettings", "TelemetryConsentUI",
        };

        public override string Key => "surge";
        public override string Title => "Surge";
        public override string Description => "Play Surge from its lobby platform above and left of Wave Survival.";
        public override string Namespace => "SpiderSurge";
        public override bool OnByDefault => true;
        public override bool CanDisable => false;
        public override bool HasSettings => true;
        public override bool HasCollection => true;
        public override IEnumerable<KeyValuePair<string, object>> SettingDefaults => ModConfig.Defaults;

        public override IEnumerable<string> SharedSettings => new[] { ModConfig.UnlimitedPerkTimeKey, ModConfig.NoAbilitiesKey };

        protected internal override void OnStart()
        {
            Logging.SpiderSurgeTelemetryUploader.Instance.FlushQueuedPayloads();
            SurgeNet.Initialize();

            new GameObject("SurgeGameModeManager").AddComponent<SurgeGameModeManager>();
            new GameObject("PerksManager").AddComponent<PerksManager>();
            new GameObject("SoundManager").AddComponent<SoundManager>();
            TelemetryConsentUI.Initialize();
            PlayerControlSettings.Initialize();

            SurgeLobbyPlatform.EnsureCreated();
            StatsMod.RunReportSources.Register(Integration.SurgeReportSource.Instance);
            Settings.Changed += OnSettingsChanged;
        }

        protected internal override void OnStop()
        {
            Settings.Changed -= OnSettingsChanged;
            StatsMod.RunReportSources.Unregister(Integration.SurgeReportSource.Instance);
            SurgeNet.Shutdown();
            SurgeLobbyPlatform.Remove();
            SurgeGameModeManager.SelectMode(false);

            foreach (string name in Objects)
            {
                GameObject found = GameObject.Find(name);
                if (found != null)
                {
                    Object.Destroy(found);
                }
            }

            TelemetryConsentUI.ResetInstance();
        }

        public override void BuildSettings(SettingsPage page)
        {
            SurgeSettingsPage.Build(page);
        }

        public override void BuildHub(SettingsPage page)
        {
            page.Button("How to play", () => page.Open(SurgeGuide.Page));
            page.Note("A guide to starting a run, abilities and ultimates, perk synergies, chaos points, challenges, enemies, controls and multiplayer.");
        }

        public override void BuildRecords(SettingsPage page)
        {
            page.Spacer();
            SurgeSettingsPage.BuildTelemetry(page);
        }

        public override void BuildCollection(SettingsPage page)
        {
            page.Button($"Surge achievements ({SurgeAchievements.UnlockedCount} of {SurgeAchievements.All.Length})", () => page.Open(AchievementsPage.Page));
        }

        public override void BuildSupport(SettingsPage page)
        {
            SurgeSettingsPage.BuildTelemetrySupport(page);
        }

        private static void OnSettingsChanged(string key)
        {
            if (key == null)
            {
                ModConfig.ForgetColors();
            }
        }
    }
}
