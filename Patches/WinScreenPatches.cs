using System;
using System.Reflection;
using System.Text;
using HarmonyLib;
using TMPro;
using UnityEngine;
using Logger = Silk.Logger;

namespace SpiderTeams
{
    internal static class WinScreenText
    {
        private const string WinFallback = "{0} wins!";
        private const string ScoreFallback = "{0}: {1} points";
        private const string SingleScoreFallback = "{0}: {1} point";

        // A one-player team reads like vanilla: the player's name in the player's color.
        public static string Label(MatchSnapshot.Team team)
        {
            return team.Members.Count == 1 ? team.Members[0].Name : team.Name;
        }

        public static Color ColorOf(MatchSnapshot.Team team)
        {
            return team.Members.Count == 1 ? team.Members[0].Color : team.Color;
        }

        public static string WinPrompt(FieldInfo field, object owner, string name)
        {
            return Format(ReflectionCache.Text(field, owner, WinFallback), WinFallback, name, 0);
        }

        public static string Score(FieldInfo many, FieldInfo single, object owner, MatchSnapshot.Team team)
        {
            string fallback = team.Score != 1 ? ScoreFallback : SingleScoreFallback;
            string format = ReflectionCache.Text(team.Score != 1 ? many : single, owner, fallback);
            return Format(format, fallback, Label(team), team.Score);
        }

        public static string Members(MatchSnapshot.Team team)
        {
            var builder = new StringBuilder();
            for (int i = 0; i < team.Members.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append(", ");
                }
                builder.Append("<color=#").Append(TeamPalette.Hex(team.Members[i].Color)).Append('>')
                    .Append(team.Members[i].Name).Append("</color>");
            }
            return builder.ToString();
        }

        public static string Colored(string text, Color color)
        {
            return "<color=#" + TeamPalette.Hex(color) + ">" + text + "</color>";
        }

        private static string Format(string format, string fallback, string name, int score)
        {
            try
            {
                return string.Format(format, name, score);
            }
            catch (FormatException)
            {
                return string.Format(fallback, name, score);
            }
        }
    }

    [HarmonyPatch(typeof(VersusRestartTimer), nameof(VersusRestartTimer.StartTimer))]
    public static class VersusRestartTimer_StartTimer_Patch
    {
        // Rebuilds the vanilla winner list as one line per team, with the members under it.
        [HarmonyPostfix]
        public static void Postfix(VersusRestartTimer __instance)
        {
            try
            {
                MatchSnapshot snapshot = MatchSnapshot.Current;
                if (snapshot == null || !snapshot.Celebrate || ReflectionCache.TimerWinnerView == null)
                {
                    return;
                }

                var view = ReflectionCache.TimerWinnerView.GetValue(__instance) as TMP_Text;
                if (view == null)
                {
                    return;
                }

                var builder = new StringBuilder();
                if (snapshot.Winner != null)
                {
                    string name = "<nobr> " + WinScreenText.Colored(WinScreenText.Label(snapshot.Winner), WinScreenText.ColorOf(snapshot.Winner)) + "</nobr>";
                    builder.Append("<size=140%>")
                        .Append(WinScreenText.WinPrompt(ReflectionCache.TimerWinPrompt, __instance, name))
                        .Append("<size=100%>\n");
                }

                for (int i = 0; i < snapshot.Teams.Count; i++)
                {
                    MatchSnapshot.Team team = snapshot.Teams[i];
                    builder.Append("<color=#").Append(TeamPalette.Hex(WinScreenText.ColorOf(team))).Append("><nobr> ")
                        .Append(WinScreenText.Score(ReflectionCache.TimerFinalScore, ReflectionCache.TimerFinalSingleScore, __instance, team))
                        .Append("</nobr></color>\n");
                    if (team.Members.Count > 1)
                    {
                        builder.Append("<size=75%>").Append(WinScreenText.Members(team)).Append("<size=100%>\n");
                    }
                }
                view.text = builder.ToString();
            }
            catch (Exception ex)
            {
                Logger.LogError($"SpiderTeams could not build the win screen: {ex}");
            }
        }
    }

    [HarmonyPatch(typeof(LobbyScreen), "Start")]
    public static class LobbyScreen_Start_Patch
    {
        [HarmonyPrefix]
        public static void Prefix(LobbyScreen __instance, out int __state)
        {
            __state = -1;
            try
            {
                if (MatchSnapshot.Current != null && __instance.scoreBoardRoot != null)
                {
                    __state = __instance.scoreBoardRoot.transform.childCount;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"SpiderTeams lobby scoreboard check failed: {ex}");
            }
        }

        // Swaps the per-player entries vanilla just added for one entry per team.
        [HarmonyPostfix]
        public static void Postfix(LobbyScreen __instance, int __state)
        {
            if (__state < 0)
            {
                return;
            }

            try
            {
                MatchSnapshot snapshot = MatchSnapshot.Consume();
                if (snapshot == null || __instance.scoreBoardEntry == null)
                {
                    return;
                }

                Transform root = __instance.scoreBoardRoot.transform;
                for (int i = root.childCount - 1; i >= __state; i--)
                {
                    GameObject entry = root.GetChild(i).gameObject;
                    entry.SetActive(false);
                    UnityEngine.Object.Destroy(entry);
                }

                for (int i = 0; i < snapshot.Teams.Count; i++)
                {
                    MatchSnapshot.Team team = snapshot.Teams[i];
                    var text = UnityEngine.Object.Instantiate(__instance.scoreBoardEntry, root).GetComponent<TextMeshProUGUI>();
                    if (text == null)
                    {
                        continue;
                    }

                    string line = WinScreenText.Score(ReflectionCache.LobbyScreenFinalScore, ReflectionCache.LobbyScreenFinalSingleScore, __instance, team);
                    if (team.Members.Count > 1)
                    {
                        line += " <size=70%>" + WinScreenText.Members(team) + "</size>";
                    }
                    text.text = line;
                    text.color = WinScreenText.ColorOf(team);
                }

                if (snapshot.Celebrate && snapshot.Winner != null && __instance.winnerPrompt != null &&
                    __instance.versusResults != null && __instance.versusResults.activeSelf)
                {
                    __instance.winnerPrompt.text = WinScreenText.WinPrompt(ReflectionCache.LobbyScreenWinPrompt, __instance, WinScreenText.Label(snapshot.Winner));
                    __instance.winnerPrompt.color = WinScreenText.ColorOf(snapshot.Winner);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"SpiderTeams could not build the lobby scoreboard: {ex}");
            }
        }
    }
}
