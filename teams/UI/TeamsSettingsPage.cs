using System;
using System.Collections.Generic;

namespace SpiderTeams
{
    internal static class TeamsSettingsPage
    {
        private static readonly List<PlayerController> _members = new List<PlayerController>();

        public static void Build(VersusTeamsPage page)
        {
            page.Checkbox("Team versus", () => ModConfig.TeamsEnabled, ModConfig.SetTeamsEnabled);
            page.Choice("Team count", ModConfig.TeamCount.ToString(),
                () => ModConfig.SetTeamCount(ModConfig.TeamCount - 1),
                () => ModConfig.SetTeamCount(ModConfig.TeamCount + 1));
            page.Choice("Friendly fire", FriendlyFireLabel(ModConfig.FriendlyFire),
                () => ModConfig.SetFriendlyFire((FriendlyFireMode)(((int)ModConfig.FriendlyFire + 2) % 3)), CycleFriendlyFire);
            page.Note(FriendlyFireDescription(ModConfig.FriendlyFire));
            page.PlacePrimaryControls();

            page.Spacer();
            page.Header("Team options");
            page.Note(PickHint());

            page.Spacer();
            page.Checkbox("Recolor spiders by team", () => ModConfig.RecolorSpiders, ModConfig.SetRecolorSpiders);
            page.Checkbox("Show team markers", () => ModConfig.ShowMarker, ModConfig.SetShowMarker);
            page.Checkbox("Spawn teammates together", () => ModConfig.GroupTeammates, ModConfig.SetGroupTeammates);

            page.Spacer();
            page.Button($"Team button (controller): {GamepadLabel()}", CycleGamepadBinding);
            page.Button($"Team button (keyboard): {KeyboardLabel()}", CycleKeyboardBinding);

            var manager = TeamManager.Instance;
            if (manager != null && TeamManager.IsHost && ModConfig.TeamsEnabled)
            {
                AddRoster(page, manager);
            }

            AddSurge(page);

            page.Spacer();
            page.Button("Reset Teams settings to defaults", ModConfig.ResetToDefaults);
        }

        private static void AddRoster(VersusTeamsPage page, TeamManager manager)
        {
            var players = TeamManager.Players;
            if (players == null || players.Count == 0)
            {
                return;
            }

            page.Spacer();
            page.Header("Teams");
            int count = manager.TeamCount;
            for (int team = 0; team < count; team++)
            {
                manager.CollectMembers(team, _members);
                string hex = TeamPalette.Hex(TeamPalette.TeamColor(team));
                page.Paragraph($"<size=115%><color=#{hex}><b>{TeamPalette.TeamName(team)}</b></color> ({Plural(_members.Count, "player")})</size>");

                string next = TeamPalette.TeamName((team + 1) % count);
                for (int i = 0; i < _members.Count; i++)
                {
                    PlayerController member = _members[i];
                    page.Button($"{TeamManager.RichName(member)} > {next}", () => MovePlayer(member));
                }
            }

            page.Button("Shuffle teams", () => manager.Shuffle());
            page.Button("Balance teams", () => manager.Balance());
        }

        private static void AddSurge(VersusTeamsPage page)
        {
            page.Spacer();
            page.Header("Surge abilities in versus");
            if (!SurgeBridge.Available)
            {
                page.Note("Surge isn't running, so its abilities can't be used in versus. Mods > SpiderSurge shows why.");
                return;
            }

            page.Checkbox("Give everyone an ability", () => ModConfig.SurgeEnabled, ModConfig.SetSurgeEnabled);
            if (!ModConfig.SurgeEnabled)
            {
                page.Note("Turn this on to give every player the same Surge ability in versus.");
                return;
            }

            int ability = (int)ModConfig.SurgeAbility;
            bool random = ModConfig.SurgeAbility == SurgeAbility.Random;
            page.Button($"Ability: {Consts.Surge.AbilityNames[ability]}", CycleSurgeAbility);
            page.Note(random ? Consts.Surge.AbilityDescriptions[ability] : Consts.Surge.AbilityDescriptions[ability] + " Everyone gets it.");
            if (random && SurgeVersus.RoundActive && SurgeVersus.CurrentAbility >= 0)
            {
                page.Note($"This round: {Consts.Surge.AbilityNames[SurgeVersus.CurrentAbility]}");
            }

            page.Checkbox(random ? "Ultimates" : $"Ultimate: {Consts.Surge.UltimateNames[ability]}", () => ModConfig.SurgeUltimate, ModConfig.SetSurgeUltimate);
            if (ModConfig.SurgeUltimate && !random)
            {
                page.Note(Consts.Surge.UltimateDescriptions[ability]);
            }

            page.Button($"{Consts.Surge.CooldownName}: {PerkLevel(ModConfig.SurgeCooldown)}", () => ModConfig.SetSurgeCooldown(NextPerkLevel(ModConfig.SurgeCooldown)));
            page.Note(Consts.Surge.CooldownDescription);
            page.Button($"{Consts.Surge.DurationNames[ability]}: {PerkLevel(ModConfig.SurgeDuration)}", () => ModConfig.SetSurgeDuration(NextPerkLevel(ModConfig.SurgeDuration)));
            page.Note(Consts.Surge.DurationDescription);
            page.Checkbox(Consts.Surge.ShortTermName, () => ModConfig.SurgeShortTerm, ModConfig.SetSurgeShortTerm);
            page.Note(Consts.Surge.ShortTermDescription);
            page.Checkbox(Consts.Surge.LongTermName, () => ModConfig.SurgeLongTerm, ModConfig.SetSurgeLongTerm);
            page.Note(Consts.Surge.LongTermDescription);

            page.Note("Ability: Q or L1 (hold Start and double-tap a button to remap it). Ultimate: C, or L3 and R3 together. Changes apply from the next round.");
            page.Note("Abilities only work for players on the host's machine, so a match with anyone online plays without them.");
        }

        private static void CycleSurgeAbility()
        {
            int next = ((int)ModConfig.SurgeAbility + 1) % Consts.Surge.AbilityConfigNames.Length;
            ModConfig.SetSurgeAbility((SurgeAbility)next);
        }

        private static string PerkLevel(int level)
        {
            return level == 0 ? "Off" : "Level " + level;
        }

        private static int NextPerkLevel(int level)
        {
            return (level + 1) % (Consts.Surge.MaxPerkLevel + 1);
        }

        private static void MovePlayer(PlayerController player)
        {
            var manager = TeamManager.Instance;
            if (player != null && manager != null && manager.CycleTeam(player))
            {
                Announcements.QueuePick(player.playerID.Value);
            }
        }

        private static void CycleFriendlyFire()
        {
            switch (ModConfig.FriendlyFire)
            {
                case FriendlyFireMode.Push:
                    ModConfig.SetFriendlyFire(FriendlyFireMode.Ignore);
                    break;
                case FriendlyFireMode.Ignore:
                    ModConfig.SetFriendlyFire(FriendlyFireMode.Damage);
                    break;
                default:
                    ModConfig.SetFriendlyFire(FriendlyFireMode.Push);
                    break;
            }
        }

        private static string FriendlyFireLabel(FriendlyFireMode mode)
        {
            switch (mode)
            {
                case FriendlyFireMode.Ignore:
                    return "Ignore";
                case FriendlyFireMode.Damage:
                    return "Damage";
                default:
                    return "Push";
            }
        }

        private static string FriendlyFireDescription(FriendlyFireMode mode)
        {
            switch (mode)
            {
                case FriendlyFireMode.Ignore:
                    return "Teammates' hits do nothing at all.";
                case FriendlyFireMode.Damage:
                    return "Teammates' hits work as normal and can kill.";
                default:
                    return "Teammates' hits knock you back, but never kill you or break your shield.";
            }
        }

        private static void CycleGamepadBinding()
        {
            ModConfig.SetCycleTeamGamepad(NextPreset(Consts.Bindings.GamepadPaths, ModConfig.CycleTeamGamepad));
        }

        private static void CycleKeyboardBinding()
        {
            ModConfig.SetCycleTeamKeyboard(NextPreset(Consts.Bindings.KeyboardPaths, ModConfig.CycleTeamKeyboard));
        }

        private static string GamepadLabel()
        {
            return PresetLabel(Consts.Bindings.GamepadPaths, Consts.Bindings.GamepadLabels, ModConfig.CycleTeamGamepad);
        }

        private static string KeyboardLabel()
        {
            return PresetLabel(Consts.Bindings.KeyboardPaths, Consts.Bindings.KeyboardLabels, ModConfig.CycleTeamKeyboard);
        }

        private static string PresetLabel(string[] paths, string[] labels, string current)
        {
            int index = Array.FindIndex(paths, path => string.Equals(path, current, StringComparison.OrdinalIgnoreCase));
            return index >= 0 ? labels[index] : "<noparse>" + current + "</noparse>";
        }

        private static string NextPreset(string[] paths, string current)
        {
            int index = Array.FindIndex(paths, path => string.Equals(path, current, StringComparison.OrdinalIgnoreCase));
            return paths[(index + 1) % paths.Length];
        }

        private static string PickHint()
        {
            bool gamepad = ModConfig.CycleTeamGamepad != Consts.Bindings.None;
            bool keyboard = ModConfig.CycleTeamKeyboard != Consts.Bindings.None;
            if (gamepad && keyboard)
            {
                return $"In the lobby, press {GamepadLabel()} (controller) or {KeyboardLabel()} (keyboard) to switch team.";
            }
            if (gamepad)
            {
                return $"In the lobby, press {GamepadLabel()} on a controller to switch team.";
            }
            if (keyboard)
            {
                return $"In the lobby, press {KeyboardLabel()} on the keyboard to switch team.";
            }
            return "The team buttons are off; the host can move players in the list below.";
        }

        private static string Plural(int count, string noun)
        {
            return count == 1 ? "1 " + noun : count + " " + noun + "s";
        }
    }
}
