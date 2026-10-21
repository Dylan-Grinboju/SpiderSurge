using System.Collections.Generic;
using SpiderSurge.Common;
using UnityEngine;

namespace SpiderSurge
{
    public class SurgePart : Part
    {
        private static readonly string[] Objects =
        {
            "SurgeGameModeManager", "PerksManager", "SoundManager", "SoundTester", "PlayerControlSettings", "TelemetryConsentUI", "SurgeGuide",
        };

        public override string Key => "surge";
        public override string Title => "Surge";
        public override string Description => "Play Surge from its lobby platform above and left of Wave Survival.";
        public override string Namespace => "SpiderSurge";
        public override bool OnByDefault => true;
        public override bool CanDisable => false;
        public override bool HasSettings => true;

        public override IEnumerable<string> SharedSettings => new[] { ModConfig.UnlimitedPerkTimeKey, ModConfig.NoAbilitiesKey, ModConfig.MeterChargePerWaveKey, ModConfig.MeterWavesPerPointKey };

        protected internal override void OnStart()
        {
            Logging.SpiderSurgeTelemetryUploader.Instance.FlushQueuedPayloads();
            SurgeNet.Initialize();

            new GameObject("SurgeGameModeManager").AddComponent<SurgeGameModeManager>();
            new GameObject("PerksManager").AddComponent<PerksManager>();
            new GameObject("SoundManager").AddComponent<SoundManager>();
            SurgeGuide.Initialize();
            TelemetryConsentUI.Initialize();
            PlayerControlSettings.Initialize();

            SurgeLobbyPlatform.EnsureCreated();
            Integration.StatsModBridge.DescribeSurgeTitles();
            StatsMod.HitLogic.EnemyKilled += MeterKills.OnEnemyKilled;
            if (!Parts.Stats.Enabled || Parts.Stats.BlockedBy != null) Silk.Logger.LogWarning("Stats is off, so kills don't charge the Surge meter for Bottomless Clip, Null Field and Care Package");
            Settings.Changed += OnSettingsChanged;
        }

        protected internal override void OnStop()
        {
            Settings.Changed -= OnSettingsChanged;
            StatsMod.HitLogic.EnemyKilled -= MeterKills.OnEnemyKilled;
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
            SurgeGuide.ResetInstance();
            InputInterceptor.ResetStaticState();
        }

        public override void BuildSettings(SettingsPage page)
        {
            SurgeSettingsPage.Build(page);
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
