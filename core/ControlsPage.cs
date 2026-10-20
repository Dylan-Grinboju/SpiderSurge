using System.Linq;

namespace SpiderSurge.Common
{
    internal static class ControlsPage
    {
        public const string Page = "controls";

        public static void Build(SettingsPage page)
        {
            page.Title("Controls");
            page.Note("Select a control, then press the new key or button. On a controller, Start cancels and Select turns the control off; on the keyboard, Escape cancels and Backspace turns it off.");

            if (Parts.Surge.Running)
            {
                page.Spacer();
                page.Header("Surge");
                page.Binding(Controls.AbilityKeyboard);
                page.Binding(Controls.AbilityController);
                page.Note("Once a spider has an ability, its ability button stops doing what it does in the game, so by default L1 no longer jumps. Each player can also pick their own controller button in the game: hold Start or Select and double-tap it. Holding Start or Select for 3 seconds goes back to the button above.");
                page.Binding(Controls.UltimateKeyboard);
                page.Binding(Controls.UltimateController);
                page.Note("For the controller ultimate, press a second button while holding the first to make a combo, or press any D-pad direction to use the whole D-pad.");
                page.Binding(Controls.SurgeGuide);
            }

            if (Parts.Stats.Running)
            {
                page.Spacer();
                page.Header("Stats windows");
                page.Binding(Controls.SmallWindow);
                page.Binding(Controls.BigWindow);
                page.Binding(Controls.TitlesWindow);
                page.Note("The titles key also skips the reveal at the end of a run.");
                page.Binding(Controls.SwitchWindows);
                page.Note("Steps through the small window, the big window and the titles on any controller.");
                string effective = StatsMod.ModConfig.SwitchWindowsBinding;
                if (effective != Controls.Get(Controls.SwitchWindows))
                {
                    page.Warning(effective == Controls.None
                        ? "Surge's ultimate uses the same buttons, so they cast the ultimate and don't switch the windows."
                        : $"Surge's ultimate uses some of the same buttons, so only <noparse>{Controls.Label(effective)}</noparse> switches the windows.");
                }
            }

            if (Parts.Teams.Running)
            {
                page.Spacer();
                page.Header("Teams");
                page.Binding(Controls.TeamKeyboard);
                page.Binding(Controls.TeamController);
                page.Note("Switches to the next team in the lobby. A team button that is also the game's show-names button (Tab or Y) only switches team there.");
            }

            page.Spacer();
            page.ConfirmButton("Reset controls to defaults", Controls.ResetAll);
        }

        public static string RowLabel(Control control)
        {
            string device = control.Device == ControlDevice.Keyboard ? "keyboard" : "controller";
            string value = ControlCapture.Current == control
                ? $"<color={SettingsPage.Orange}>{ControlCapture.Prompt}</color>"
                : $"<noparse>{Controls.Label(control)}</noparse>";
            return $"{control.Name} ({device}): {value}";
        }

        public static string ClashWarning(Control control)
        {
            var clashes = Controls.ClashesOf(control).Where(other => !Resolved(control, other)).Select(other => $"{other.Name} ({Owner(other)})").ToList();
            return clashes.Count == 0 ? null : "Also used for " + string.Join(", ", clashes) + ".";
        }

        // The ultimate takes a button it shares with the Stats switch, so that pair needs no warning unless a combo still shares one
        private static bool Resolved(Control control, Control other)
        {
            bool pair = (control == Controls.SwitchWindows && other == Controls.UltimateController) || (control == Controls.UltimateController && other == Controls.SwitchWindows);
            string ultimate = Controls.Get(Controls.UltimateController);
            return pair && !Controls.Overlap(Controls.Without(Controls.Get(Controls.SwitchWindows), ultimate), ultimate);
        }

        private static string Owner(Control control) => control.Owner != null ? control.Owner.Title : "SpiderSurge";
    }
}
