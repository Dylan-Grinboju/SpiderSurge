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
            if (page.Current == SurgeGuide.Page || page.Current.StartsWith(SurgeGuide.Page + ".", StringComparison.Ordinal)) { SurgeGuide.Build(page); return; }
            if (page.Current == AchievementsPage.Page) { AchievementsPage.Build(page); return; }
            page.Title("Surge");
            page.Note("To play Surge, stand on the Surge Survival platform above and left of Wave Survival in the lobby.");
            page.Button("How to play", () => page.Open(SurgeGuide.Page));
            page.Note("Start with a guided first run, then explore abilities, synergies, enemies, the Surge meter and multiplayer.");

            page.Spacer();
            page.Header("Play");
            page.Toggle("Unlimited time to choose perks", ModConfig.UnlimitedPerkTimeKey);
            page.Note("Removes the perk-choice countdown. Select a card to lock it, then move down to Confirm when everyone is ready. The starting ability choice works the same way.");
            page.Toggle("Play without abilities", ModConfig.NoAbilitiesKey);
            page.Note("Surge Survival runs have no abilities, no ultimates and no ability perks: the first perk choice is a normal one, and waves 30 and 60 bring no ultimate choice. Luck still appears. Applies from the next run.");
            page.Button("Ability and ultimate buttons", () => page.Open(ControlsPage.Page));
            page.Note($"Ability: {Controls.Display(Controls.AbilityKeyboard)} or {Controls.Display(Controls.AbilityController)}. Ultimate: {Controls.Display(Controls.UltimateKeyboard)} or {Controls.Display(Controls.UltimateController)}.");

            page.Spacer();
            page.Header("Effects");
            page.Toggle("Reduced visual effects", ModConfig.ReducedEffectsKey);
            page.Note("Softens decorative flashes and particles. Danger rings and warnings stay visible.");

            page.Spacer();
            page.Header("Surge meter");
            page.Slider("Charge per player per wave", ModConfig.MeterChargePerWaveKey, Consts.Values.Meter.MinChargePerWave, Consts.Values.Meter.MaxChargePerWave, 0.5f, effectiveValue: ModConfig.MeterChargePerWave);
            page.Note($"Base ability charge for each wave toward one chaos point. Multiply it by Waves per chaos point for the solo target. Bigger teams need more charge. Lower values fill the meter faster. Default: {Consts.Values.Meter.DefaultChargePerWave:0.##}. Applies at the next wave.");
            page.Slider("Waves per chaos point", ModConfig.MeterWavesPerPointKey, Consts.Values.Meter.MinWavesPerPoint, Consts.Values.Meter.MaxWavesPerPoint, 0.5f, effectiveValue: ModConfig.MeterWavesPerPoint);
            page.Note($"One chaos point takes that charge for each of those waves: {MeterExample()} for one player. A bigger team's point grows with the extra enemies its waves bring. The team can earn at most one point per wave from abilities; Blood Pact is separate. Defaults: {Consts.Values.Meter.DefaultChargePerWave:0.##} and {Consts.Values.Meter.DefaultWavesPerPoint:0.##}. Changes apply at the next wave.");

            page.Spacer();
            page.Header("Sound");
            page.Slider("Ability volume", ModConfig.AbilityVolumeKey, 0f, ModConfig.MaxAbilityVolume, 5f, "%", ModConfig.AbilityVolume * ModConfig.OriginalAbilityVolume);
            page.Note("Every Surge sound: abilities, ultimates and the perk choice sounds. The default is 50%; 80% plays them at their original volume.");

            page.Spacer();
            BuildIndicator(page, false);
            page.Spacer();
            page.ConfirmButton("Reset Surge settings to defaults", () => Settings.Reset("surge", ModConfig.StatsLoggingKey, ModConfig.TelemetryKey, ModConfig.ShowTelemetryPromptKey));
        }

        private static void BuildIndicator(SettingsPage page, bool standalone = true)
        {
            if (standalone) page.Title("Ability indicator");
            else page.Header("Ability indicator");
            page.Toggle("Use compact ability HUD", ModConfig.IndicatorCompactHudKey);
            page.Note("Replaces the ring beside each spider with a fixed panel at the bottom right. Active effects count down; cooldown bars fill until ready. Changes apply immediately.");
            page.Header("Ring position and size");
            page.Slider("Size", ModConfig.IndicatorRadiusKey, ModConfig.MinIndicatorRadius, ModConfig.MaxIndicatorRadius, 0.5f, effectiveValue: ModConfig.IndicatorRadius);
            page.Note("Size of the ability ring beside each spider. This affects the world ring; the compact HUD keeps its own size.");
            page.Slider("Sideways offset", ModConfig.IndicatorOffsetXKey, -10f, 10f, 0.5f);
            page.Note("Moves the ring left or right relative to the spider. This only affects the world ring.");
            page.Slider("Height", ModConfig.IndicatorOffsetYKey, -5f, 20f, 0.5f);
            page.Note("Moves the ring up or down relative to the spider. This only affects the world ring.");
            page.Header("Colors and visibility");
            page.Cycle("Ready color", ModConfig.IndicatorAvailableColorKey, Colors, ColorNames, IsInUse(ModConfig.IndicatorAvailableColorKey));
            page.Note("Color used when the ability is ready. Applies to the ring and the compact HUD. Select to cycle through the colors.");
            page.Cycle("Cooldown color", ModConfig.IndicatorCooldownColorKey, Colors, ColorNames, IsInUse(ModConfig.IndicatorCooldownColorKey));
            page.Note("Color used while the shared ability and ultimate cooldown recharges. Applies to both displays.");
            page.Cycle("Active color", ModConfig.IndicatorActiveColorKey, Colors, ColorNames, IsInUse(ModConfig.IndicatorActiveColorKey));
            page.Note("Color used while an ability or ultimate is active. Applies to both displays.");
            page.Toggle("Only show when ready", ModConfig.IndicatorShowOnlyWhenReadyKey);
            page.Note("Applies to both displays. Turn this off to see active time and cooldown progress.");
        }

        private static string MeterExample()
        {
            return Mathf.RoundToInt(ModConfig.MeterChargePerWave * ModConfig.MeterWavesPerPoint).ToString();
        }

        // A value that isn't a color matches the default the indicator falls back to
        private static Func<string, bool> IsInUse(string key)
        {
            Color inUse = ModConfig.ReadColor(key);
            return value => ModConfig.TryParseColor(value, out Color color) && color == inUse;
        }

        // On the General page, since plain Survival runs are recorded and shared too
        public static void BuildTelemetry(SettingsPage page)
        {
            page.Header("Telemetry");
            page.Toggle("Record each run", ModConfig.StatsLoggingKey);
            page.Note("Builds a report of every Survival run: waves, perks, deaths, and each player's ability and ultimate use. Surge runs save it as a text file in Silk\\Logs\\SpiderSurge and give the Stats titles their ability counts. Sharing new reports needs it on.");
            page.Toggle("Share anonymous run reports", ModConfig.TelemetryKey);
            page.Note("Uploads the report of each finished Survival run, with or without Surge, to help balance and improve the mod. No personal identifiers are sent.");
            if (ModConfig.TelemetryEnabled && !ModConfig.EnableStatsLogging)
            {
                page.Warning("No new reports are shared while Record each run is off. Older reports already queued may still upload while Share anonymous run reports is on.");
            }
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
