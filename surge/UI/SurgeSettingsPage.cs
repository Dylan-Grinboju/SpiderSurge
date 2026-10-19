using System;
using SpiderSurge.Common;
using UnityEngine;

namespace SpiderSurge
{
    internal static class SurgeSettingsPage
    {
        private static readonly string[] Colors = { "#00FF00", "#FF0000", "#87CEEB", "#FFFF00", "#FFA500", "#FF00FF", "#00FFFF", "#FFFFFF" };
        private static readonly string[] ColorNames = { "Green", "Red", "Sky blue", "Yellow", "Orange", "Magenta", "Cyan", "White" };

        public static void Build(SettingsPage page)
        {
            if (page.Current == "surge.indicator") { BuildIndicator(page); return; }
            if (page.Current == "surge.telemetry") { BuildTelemetry(page); return; }
            if (page.Current == SurgeGuide.Page) { SurgeGuide.Build(page); return; }
            if (page.Current == AchievementsPage.Page) { AchievementsPage.Build(page); return; }
            page.Title("Surge");
            page.Note("To play Surge, stand on the Surge Survival platform above and left of Wave Survival in the lobby.");
            page.Button("Surge guide", () => page.Open(SurgeGuide.Page));

            page.Spacer();
            page.Header("Play");
            page.Toggle("Unlimited time to choose perks", ModConfig.UnlimitedPerkTimeKey);
            page.Toggle("Play without abilities", ModConfig.NoAbilitiesKey);
            page.Note("Surge Survival runs have no abilities, no ultimates and no ability perks: the first perk choice is a normal one, and waves 30 and 60 bring no ultimate choice. Luck still appears. Applies from the next run.");
            page.Toggle("Ultimate on the D-pad", ModConfig.DpadUltimateKey);
            page.Note("Any D-pad direction casts the ultimate, instead of L3 and R3 together. Applies from the next run. While it's on, the D-pad doesn't switch the Stats windows.");

            page.Spacer();
            page.Header("Sound");
            page.Slider("Ability volume", ModConfig.AbilityVolumeKey, 0f, ModConfig.MaxAbilityVolume, 5f, "%");
            page.Note("Every Surge sound: abilities, ultimates and the perk choice sounds. 80% is the original volume.");

            page.Spacer();
            page.Button("Ability indicator", () => page.Open("surge.indicator"));
            page.Button("Run stats and telemetry", () => page.Open("surge.telemetry"));
            page.Spacer();
            page.ConfirmButton("Reset Surge settings to defaults", () => Settings.Reset("surge", ModConfig.TelemetryKey, ModConfig.ShowTelemetryPromptKey, ModConfig.ShowTutorialKey));
        }

        private static void BuildIndicator(SettingsPage page)
        {
            page.Title("Ability indicator");
            page.Slider("Size", ModConfig.IndicatorRadiusKey, ModConfig.MinIndicatorRadius, ModConfig.MaxIndicatorRadius, 0.5f);
            page.Slider("Sideways offset", ModConfig.IndicatorOffsetXKey, -10f, 10f, 0.5f);
            page.Slider("Height", ModConfig.IndicatorOffsetYKey, -5f, 20f, 0.5f);
            page.Cycle("Ready color", ModConfig.IndicatorAvailableColorKey, Colors, ColorNames, IsInUse(ModConfig.IndicatorAvailableColorKey));
            page.Cycle("Cooldown color", ModConfig.IndicatorCooldownColorKey, Colors, ColorNames, IsInUse(ModConfig.IndicatorCooldownColorKey));
            page.Cycle("Active color", ModConfig.IndicatorActiveColorKey, Colors, ColorNames, IsInUse(ModConfig.IndicatorActiveColorKey));
            page.Toggle("Only show when ready", ModConfig.IndicatorShowOnlyWhenReadyKey);
        }

        // A value that isn't a color matches the default the indicator falls back to
        private static Func<string, bool> IsInUse(string key)
        {
            Color inUse = ModConfig.ReadColor(key);
            return value => ModConfig.TryParseColor(value, out Color color) && color == inUse;
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

    }
}
