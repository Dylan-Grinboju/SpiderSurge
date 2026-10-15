using SpiderSurge.Common;
using UnityEngine;

namespace SpiderSurge
{
    public class SurgePart : Part
    {
        private static readonly string[] Objects =
        {
            "SurgeGameModeManager", "PerksManager", "SoundManager", "SoundTester", "PlayerControlSettings", "TelemetryConsentUI", "TutorialUI",
        };

        public override string Key => "surge";
        public override string Title => "Surge";
        public override string Description => "Abilities, ultimates and perks for Survival.";
        public override string Namespace => "SpiderSurge";
        public override bool OnByDefault => false;
        public override bool HasSettings => true;

        protected internal override void OnStart()
        {
            Logging.SpiderSurgeTelemetryUploader.Instance.FlushQueuedPayloads();

            new GameObject("SurgeGameModeManager").AddComponent<SurgeGameModeManager>();
            new GameObject("PerksManager").AddComponent<PerksManager>();
            new GameObject("SoundManager").AddComponent<SoundManager>();
            TutorialUI.Initialize();
            TelemetryConsentUI.Initialize();
            PlayerControlSettings.Initialize();
        }

        protected internal override void OnStop()
        {
            foreach (string name in Objects)
            {
                GameObject found = GameObject.Find(name);
                if (found != null)
                {
                    Object.Destroy(found);
                }
            }

            TelemetryConsentUI.ResetInstance();
            TutorialUI.ResetInstance();
            InputInterceptor.ResetStaticState();
        }

        public override void BuildSettings(SettingsPage page)
        {
            SurgeSettingsPage.Build(page);
        }
    }
}
