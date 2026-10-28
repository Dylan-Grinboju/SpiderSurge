using System.Collections.Generic;
using SpiderSurge.Common;

namespace SpiderTeams
{
    internal static class TeamsSettingsPage
    {
        private static readonly List<PlayerController> _members = new List<PlayerController>();

        public static void Build(VersusTeamsPage page)
        {
            page.Checkbox(Settings.IsRemote(Consts.Keys.TeamsEnabled) ? "Team versus (set by host)" : "Team versus", () => ModConfig.TeamsEnabled, ModConfig.SetTeamsEnabled);
            page.Choice("Team count", ModConfig.TeamCount.ToString(),
                () => ModConfig.SetTeamCount(ModConfig.TeamCount - 1),
                () => ModConfig.SetTeamCount(ModConfig.TeamCount + 1));
            page.Choice("Friendly fire", FriendlyFireLabel(ModConfig.FriendlyFire),
                () => ModConfig.SetFriendlyFire((FriendlyFireMode)(((int)ModConfig.FriendlyFire + 2) % 3)), CycleFriendlyFire);
            page.Note(FriendlyFireDescription(ModConfig.FriendlyFire));
            if (Net.IsRemoteClient)
            {
                page.Note("You are in another player's match: their team settings apply until you leave.");
            }
            page.PlacePrimaryControls();

            page.Spacer();
            page.Header("Team options");
            page.Note(PickHint());

            page.Spacer();
            page.Checkbox("Recolor spiders by team", () => ModConfig.RecolorSpiders, ModConfig.SetRecolorSpiders);
            page.Checkbox("Show team markers", () => ModConfig.ShowMarker, ModConfig.SetShowMarker);
            page.Checkbox("Spawn teammates together", () => ModConfig.GroupTeammates, ModConfig.SetGroupTeammates);


            var manager = TeamManager.Instance;
            if (manager != null && ModConfig.TeamsEnabled)
            {
                AddRoster(page, manager, readOnly: Net.IsRemoteClient || !manager.PicksOpen);
            }

            AddSurge(page);

            page.Spacer();
            page.ConfirmButton("Reset Teams settings to defaults", ModConfig.ResetToDefaults);
        }

        private static void AddRoster(VersusTeamsPage page, TeamManager manager, bool readOnly)
        {
            var players = Players.All;
            if (players == null || players.Count == 0)
            {
                return;
            }

            page.Spacer();
            page.Header("Teams");
            if (!readOnly)
            {
                page.Note("Use the arrows next to a player to move them to another team.");
            }
            int count = manager.TeamCount;
            for (int team = 0; team < count; team++)
            {
                manager.CollectMembers(team, _members);
                string hex = TeamPalette.Hex(TeamPalette.TeamColor(team));
                string name = $"<color=#{hex}>{TeamPalette.TeamName(team)}</color>";
                page.Paragraph($"<size=115%><b>{name}</b> ({Plural(_members.Count, "player")})</size>");

                for (int i = 0; i < _members.Count; i++)
                {
                    PlayerController member = _members[i];
                    if (readOnly)
                    {
                        page.Paragraph(TeamManager.RichName(member));
                    }
                    else
                    {
                        page.Choice(TeamManager.RichName(member), name, () => MovePlayer(member, -1), () => MovePlayer(member, 1));
                    }
                }
            }

            if (readOnly)
            {
                return;
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
                page.Note("Surge isn't running, so its abilities can't be used in versus. Mods > SpiderSurge > Settings > Advanced > Mod parts and game fixes shows why.");
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

            page.Button(page.AdvancedAbilities ? "Hide advanced ability rules" : "Advanced ability rules", page.ToggleAdvancedAbilities);
            if (page.AdvancedAbilities)
            {
                page.Button($"{Consts.Surge.CooldownName}: {PerkLevel(ModConfig.SurgeCooldown)}", () => ModConfig.SetSurgeCooldown(NextPerkLevel(ModConfig.SurgeCooldown)));
                page.Note(Consts.Surge.CooldownDescription);
                page.Button($"{Consts.Surge.DurationNames[ability]}: {PerkLevel(ModConfig.SurgeDuration)}", () => ModConfig.SetSurgeDuration(NextPerkLevel(ModConfig.SurgeDuration)));
                page.Note(Consts.Surge.DurationDescription);
                page.Checkbox(Consts.Surge.ShortTermName, () => ModConfig.SurgeShortTerm, ModConfig.SetSurgeShortTerm);
                page.Note(Consts.Surge.ShortTermDescription);
                page.Checkbox(Consts.Surge.LongTermName, () => ModConfig.SurgeLongTerm, ModConfig.SetSurgeLongTerm);
                page.Note(Consts.Surge.LongTermDescription);
            }

            page.Note($"Ability: {Controls.Display(Controls.AbilityKeyboard)} or {Controls.Display(Controls.AbilityController)} (hold Select and double-tap a button to pick your own). Ultimate: {Controls.Display(Controls.UltimateKeyboard)} or {Controls.Display(Controls.UltimateController)}.");
            page.Note("Online, every player runs the same SpiderSurge version; the host turns away anyone else.");
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

        private static void MovePlayer(PlayerController player, int step)
        {
            var manager = TeamManager.Instance;
            if (player != null && manager != null && manager.CycleTeam(player, step))
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

        private static string GamepadLabel() => Controls.Display(Controls.TeamController);

        private static string KeyboardLabel() => Controls.Display(Controls.TeamKeyboard);

        private static string PickHint() => $"In the lobby, press {GamepadLabel()} (controller) or {KeyboardLabel()} (keyboard) to switch team.";

        private static string Plural(int count, string noun)
        {
            return count == 1 ? "1 " + noun : count + " " + noun + "s";
        }
    }
}
