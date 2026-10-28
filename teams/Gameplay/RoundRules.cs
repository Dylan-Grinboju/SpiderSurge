using System.Collections.Generic;
using SpiderSurge.Common;
using UnityEngine;
using Logger = Silk.Logger;

namespace SpiderTeams
{
    internal static class RoundRules
    {
        private static readonly List<PlayerController> _members = new List<PlayerController>(Consts.MaxTeams);

        public static bool SuppressScorePrompts { get; private set; }

        public static bool Available => ReflectionCache.RoundRulesAvailable;

        // Vanilla LateUpdate, with "one team left" in place of "one spider left".
        public static void LateUpdate(VersusMode versus)
        {
            if (ReflectionCache.IsWaitingRoom(versus) || !ReflectionCache.RoundInProgress)
            {
                return;
            }

            var lobby = LobbyController.instance;
            if (lobby.spawnedPlayers.Count == 0)
            {
                GameController.OpenLobby();
                return;
            }
            if (ReflectionCache.RoundEnding)
            {
                return;
            }

            uint present, alive;
            TeamManager.Instance.GetTeamMasks(out present, out alive);
            if (TeamManager.PopCount(alive) <= 1 && (TeamManager.PopCount(present) > 1 || TeamManager.Instance.StartedWithTeams))
            {
                ReflectionCache.RoundEnding = true;
                ReflectionCache.DoSlowMotion(versus);
                versus.Invoke("DeclareWinner", Consts.Timing.RoundEndDelay);
                return;
            }

            if (lobby.CountAlivePlayers() == 0)
            {
                ReflectionCache.RoundEnding = true;
                ReflectionCache.DeclareWinner(versus);
            }
        }

        public static void DeclareWinner(VersusMode versus)
        {
            uint present, alive;
            TeamManager.Instance.GetTeamMasks(out present, out alive);
            if (TeamManager.PopCount(alive) == 1)
            {
                AwardRound(versus, TeamManager.LowestTeam(alive));
                return;
            }

            string draw = ReflectionCache.Term(ReflectionCache.VersusDrawString, versus) ?? "Draw!";
            Announcements.BroadcastTerm(draw, null, Color.white, true);
            ReflectionCache.SetAchievement(versus, Achievements.AchievementGetADraw);
            Logger.LogInfo("SpiderTeams round ended in a draw");
            versus.Invoke("NextRound", Consts.Timing.NextRoundDelay);
        }

        public static void ScheduleNextRoundAfterFailure(VersusMode versus)
        {
            if (versus != null && !versus.IsInvoking("NextRound") && !versus.IsInvoking("OpenWinScreen"))
            {
                versus.Invoke("NextRound", Consts.Timing.NextRoundDelay);
            }
        }

        // Every member scores, alive or dead, so the game's own scoreboard stays right.
        private static void AwardRound(VersusMode versus, int team)
        {
            var manager = TeamManager.Instance;
            int teamScore = manager.AddTeamPoint(team);
            int winningScore = GameSettings.Instance.VersusWinningScore(0);

            manager.CollectMembers(team, _members);
            SuppressScorePrompts = true;
            try
            {
                for (int i = 0; i < _members.Count; i++)
                {
                    var member = _members[i];
                    int score = ScoreKeeper.AddScore(member, 1);
                    ReflectionCache.ShowScorePrompts(versus, member.NetworkObjectId, score);
                }
            }
            finally
            {
                SuppressScorePrompts = false;
            }

            Logger.LogInfo($"SpiderTeams round won by {TeamPalette.TeamName(team)} ({teamScore}/{winningScore})");
            AnnounceRound(versus, team, teamScore, winningScore);

            if (teamScore >= winningScore)
            {
                ReflectionCache.SetAchievement(versus, Achievements.AchievementFirstVersusGame);
                versus.Invoke("OpenWinScreen", Consts.Timing.WinScreenDelay);
                return;
            }
            versus.Invoke("NextRound", Consts.Timing.NextRoundDelay);
        }

        // A one-player team is announced by name, like vanilla; bigger teams by team name.
        private static void AnnounceRound(VersusMode versus, int team, int teamScore, int winningScore)
        {
            bool solo = _members.Count == 1 && _members[0] != null;
            Color color = solo && _members[0].spiderCosmetics != null ? _members[0].spiderCosmetics.primary : TeamPalette.TeamColor(team);
            string teamName = TeamPalette.TeamName(team);

            if (winningScore - teamScore == 1)
            {
                string name = solo ? Players.NameOf(_members[0]) : teamName;
                string almostWon = ReflectionCache.Term(ReflectionCache.VersusAlmostWonString, versus);
                if (almostWon != null)
                {
                    Announcements.BroadcastTerm(almostWon, new[] { name }, color, true);
                }
                else
                {
                    Announcements.Broadcast(teamName + " needs one more point!", color, true);
                }
                return;
            }

            if (teamScore < winningScore)
            {
                Announcements.Broadcast(solo ? "+1" : teamName + " +1", color, true);
                return;
            }

            string congratulations = ReflectionCache.Term(ReflectionCache.VersusCongratulationsString, versus) ?? (teamName + " wins!");
            Announcements.BroadcastTerm(congratulations, null, color, true);
            var audio = AudioController.instance;
            if (audio != null)
            {
                audio.StopBattleMusic();
                audio.PlayVictoryTheme();
            }
        }
    }
}
