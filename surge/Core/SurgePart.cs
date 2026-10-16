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

            Settings.Lock(ModConfig.SurgeModeKey, () => SurgeSettingsPage.RunActive, "Surge Mode can't change while a Survival run is active, so it kept its value.");
            Settings.Changed += OnSettingsChanged;
        }

        protected internal override void OnStop()
        {
            Settings.Changed -= OnSettingsChanged;
            Settings.Unlock(ModConfig.SurgeModeKey);

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

        private static void OnSettingsChanged(string key)
        {
            if (key == null)
            {
                ModConfig.ForgetColors();
            }
            if (Settings.Affects(key, ModConfig.SurgeModeKey))
            {
                GameModePatches.UpdateSurgeSurvivalText();
            }
        }
    }
}
