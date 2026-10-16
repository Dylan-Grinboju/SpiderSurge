using SpiderSurge.Common;

namespace SpiderSurge
{
    internal static class SurgeSettingsPage
    {
        private static readonly string[] Colors = { "#00FF00", "#FF0000", "#87CEEB", "#FFFF00", "#FFA500", "#FF00FF", "#00FFFF", "#FFFFFF" };
        private static readonly string[] ColorNames = { "Green", "Red", "Sky blue", "Yellow", "Orange", "Magenta", "Cyan", "White" };

        internal static bool RunActive => SurvivalMode.instance != null && SurvivalMode.instance.GameModeActive();

        public static void Build(SettingsPage page)
        {
            if (page.Current == "surge.indicator") { BuildIndicator(page); return; }
            if (page.Current == "surge.telemetry") { BuildTelemetry(page); return; }
            page.Title("Surge");
            page.Paragraph($"<size=115%><b>Surge Mode:</b> {(ModConfig.enableSurgeMode ? $"<color={SettingsPage.Green}>Enabled</color>" : $"<color={SettingsPage.Red}>Disabled</color>")}</size>");
            page.Note("Surge Mode replaces Wave Survival with Surge Survival. Changes apply to the next run.");
            if (RunActive)
            {
                page.Warning("Surge Mode can't be changed while a Survival run is active.");
            }
            else
            {
                page.Checkbox("Surge Mode", ModConfig.enableSurgeMode, SetSurgeMode);
            }

            page.Spacer();
            page.Header("Play");
            page.Toggle("Unlimited time to choose perks", ModConfig.UnlimitedPerkTimeKey);
            page.Toggle("Ultimate on the D-pad", ModConfig.DpadUltimateKey);
            page.Note("Any D-pad direction casts the ultimate, instead of L3 and R3 together. Applies from the next run. While it's on, the D-pad doesn't switch the Stats windows.");
            page.Toggle("Show the tutorial", ModConfig.ShowTutorialKey);

            page.Spacer();
            page.Button("Ability indicator", () => page.Open("surge.indicator"));
            page.Button("Run stats and telemetry", () => page.Open("surge.telemetry"));
            page.Spacer();
            page.ConfirmButton("Reset Surge settings to defaults", () => Settings.Reset("surge", ModConfig.TelemetryKey, ModConfig.ShowTelemetryPromptKey));
        }

        private static void BuildIndicator(SettingsPage page)
        {
            page.Title("Ability indicator");
            page.Slider("Size", ModConfig.IndicatorRadiusKey, ModConfig.MinIndicatorRadius, ModConfig.MaxIndicatorRadius, 0.5f);
            page.Slider("Sideways offset", ModConfig.IndicatorOffsetXKey, -10f, 10f, 0.5f);
            page.Slider("Height", ModConfig.IndicatorOffsetYKey, -5f, 20f, 0.5f);
            page.Cycle("Ready color", ModConfig.IndicatorAvailableColorKey, Colors, ColorNames);
            page.Cycle("Cooldown color", ModConfig.IndicatorCooldownColorKey, Colors, ColorNames);
            page.Cycle("Active color", ModConfig.IndicatorActiveColorKey, Colors, ColorNames);
            page.Toggle("Only show when ready", ModConfig.IndicatorShowOnlyWhenReadyKey);
        }

        private static void BuildTelemetry(SettingsPage page)
        {
            page.Title("Run stats and telemetry");
            page.Toggle("Track ability use in each run", ModConfig.StatsLoggingKey);
            page.Note("Logs every run's ability and ultimate use, and feeds it to the Stats titles and the telemetry.");
            page.Toggle("Share anonymous run stats", ModConfig.TelemetryKey);
            page.Note("Helps balance and improve the mod. No personal identifiers are sent.");
            string anonId = Logging.SpiderSurgeTelemetryUploader.Instance.GetAnonymousIdOrNull() ?? "not set";
            page.Paragraph($"<b>Anonymous ID:</b> <noparse>{anonId}</noparse>");
            page.ConfirmButton("Reset telemetry ID", () =>
            {
                Logging.SpiderSurgeTelemetryUploader.Instance.ResetAnonymousId();
                SettingsPage.RefreshOpen();
            });

        }

        private static void SetSurgeMode(bool value)
        {
            if (RunActive)
            {
                Announcer.InformationPopup("Cannot change Surge Mode while a Survival run is active.");
                SettingsPage.RefreshOpen();
                return;
            }

            ModConfig.SetEnableSurgeMode(value);
        }
    }
}
