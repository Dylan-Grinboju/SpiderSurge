using System.Collections.Generic;
using SpiderSurge.Common;
using UnityEngine;
using Logger = Silk.Logger;

namespace StatsMod
{
    public class StatsPart : Part
    {
        private const string PauseHandlerName = "PauseHandler";

        public override string Key => "stats";
        public override string Title => "Stats";
        public override string Description => "Tracks kills, deaths, streaks and more in Survival and Versus, with live stats windows and Survival titles.";
        public override string Namespace => "StatsMod";
        public override bool OnByDefault => true;
        public override bool CanDisable => false;
        public override bool HasSettings => true;
        public override bool HasCollection => true;
        public override IEnumerable<KeyValuePair<string, object>> SettingDefaults => ModConfig.Defaults;

        protected internal override void OnStart()
        {
            UIManager.Initialize();
            Logger.LogInfo("UI Manager initialized");
            StatsNet.Initialize();
            RunReportSources.Register(StatsModApi.Source);

            GameObject pauseHandlerObj = new GameObject(PauseHandlerName);
            pauseHandlerObj.AddComponent<PauseHandler>();
            Object.DontDestroyOnLoad(pauseHandlerObj);
            Logger.LogInfo("Pause handler initialized");
        }

        protected internal override void OnStop()
        {
            StatsNet.Shutdown();
            RunReportSources.Unregister(StatsModApi.Source);
            TitlesManager.Instance.ReleasePortraits();
            AccuracyTracker.Reset();
            if (UIManager.Instance != null)
            {
                Object.Destroy(UIManager.Instance.gameObject);
            }

            GameObject pauseHandler = GameObject.Find(PauseHandlerName);
            if (pauseHandler != null)
            {
                Object.Destroy(pauseHandler);
            }
        }

        public override void BuildSettings(SettingsPage page)
        {
            StatsSettingsPage.Build(page);
        }

        public override void BuildRecords(SettingsPage page)
        {
            StatsSettingsPage.BuildRecords(page);
        }

        public override void BuildCollection(SettingsPage page)
        {
            page.Button(StatsSettingsPage.TitlesButtonLabel, () => page.Open(StatsSettingsPage.TitlesPage));
        }
    }
}
