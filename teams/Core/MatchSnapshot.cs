using System.Collections.Generic;
using UnityEngine;

namespace SpiderTeams
{
    internal sealed class MatchSnapshot
    {
        public sealed class Member
        {
            public string Name;
            public Color Color;
        }

        public sealed class Team
        {
            public int Index;
            public string Name;
            public Color Color;
            public int Score;
            public readonly List<Member> Members = new List<Member>();
        }

        public readonly List<Team> Teams = new List<Team>();
        public Team Winner;
        public bool Celebrate;

        public static MatchSnapshot Current { get; private set; }

        public static void Clear()
        {
            Current = null;
        }

        public static MatchSnapshot Consume()
        {
            var snapshot = Current;
            Current = null;
            return snapshot;
        }

        // Taken before colors are restored, so member colors come from the cached originals.
        public static void Capture(bool celebrate)
        {
            var manager = TeamManager.Instance;
            if (manager == null)
            {
                return;
            }

            var snapshot = new MatchSnapshot { Celebrate = celebrate };
            var members = new List<PlayerController>();
            for (int team = 0; team < manager.TeamCount; team++)
            {
                manager.CollectMembers(team, members);
                if (members.Count == 0)
                {
                    continue;
                }

                var entry = new Team
                {
                    Index = team,
                    Name = TeamPalette.TeamName(team),
                    Color = TeamPalette.TeamColor(team),
                    Score = manager.GetTeamScore(team)
                };
                for (int i = 0; i < members.Count; i++)
                {
                    entry.Members.Add(new Member
                    {
                        Name = TeamManager.RichName(members[i]),
                        Color = TeamVisuals.OriginalPrimary(members[i])
                    });
                }
                snapshot.Teams.Add(entry);
            }

            if (snapshot.Teams.Count == 0)
            {
                return;
            }

            snapshot.Teams.Sort((a, b) => a.Score != b.Score ? b.Score.CompareTo(a.Score) : a.Index.CompareTo(b.Index));
            if (celebrate)
            {
                snapshot.Winner = snapshot.Teams[0];
            }
            Current = snapshot;
        }
    }
}
