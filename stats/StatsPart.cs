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
        public override bool HasSettings => true;

        protected internal override void OnStart()
        {
            UIManager.Initialize();
            Logger.LogInfo("UI Manager initialized");
            StatsNet.Initialize();

            GameObject pauseHandlerObj = new GameObject(PauseHandlerName);
            pauseHandlerObj.AddComponent<PauseHandler>();
            Object.DontDestroyOnLoad(pauseHandlerObj);
            Logger.LogInfo("Pause handler initialized");
        }

        protected internal override void OnStop()
        {
            StatsNet.Shutdown();
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
    }
}
