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
            page.Title("Surge");
            page.Note("To play Surge, stand on the Surge Survival platform above and left of Wave Survival in the lobby.");

            page.Spacer();
            page.Header("Play");
            page.Toggle("Unlimited time to choose perks", ModConfig.UnlimitedPerkTimeKey);
            page.Toggle("Ultimate on the D-pad", ModConfig.DpadUltimateKey);
            page.Note("Any D-pad direction casts the ultimate, instead of L3 and R3 together. Applies from the next run. While it's on, the D-pad doesn't switch the Stats windows.");
            page.Toggle("Show the tutorial", ModConfig.ShowTutorialKey);
            page.Note("Opens the Surge guide once you close the menu. The ` key opens it at any time; B or Start on a gamepad closes it.");

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
            ColorCycle(page, "Ready color", ModConfig.IndicatorAvailableColorKey);
            ColorCycle(page, "Cooldown color", ModConfig.IndicatorCooldownColorKey);
            ColorCycle(page, "Active color", ModConfig.IndicatorActiveColorKey);
            page.Toggle("Only show when ready", ModConfig.IndicatorShowOnlyWhenReadyKey);
        }

        // Shows and cycles from the color the indicator uses, so a value that isn't a color reads as the default it falls back to
        private static void ColorCycle(SettingsPage page, string label, string key)
        {
            Color inUse = ModConfig.ReadColor(key);
            int index = Array.FindIndex(Colors, value => ModConfig.TryParseColor(value, out Color color) && color == inUse);
            string shown = index >= 0 ? ColorNames[index] : $"<noparse>{Settings.GetString(key)}</noparse>";
            page.Button($"{label}: {shown}", () => Settings.Set(key, Colors[(index + 1) % Colors.Length]));
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
