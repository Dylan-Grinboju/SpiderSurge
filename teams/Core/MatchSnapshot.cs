using System.Collections.Generic;
using System.IO;
using SpiderSurge.Common;
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

        public static void SetRemote(MatchSnapshot snapshot)
        {
            Current = snapshot;
        }

        public void Write(BinaryWriter writer)
        {
            writer.Write(Celebrate);
            writer.Write(Winner != null ? Teams.IndexOf(Winner) : -1);
            writer.Write(Teams.Count);
            foreach (Team team in Teams)
            {
                writer.Write(team.Index);
                writer.WriteText(team.Name);
                writer.Write(team.Color);
                writer.Write(team.Score);
                writer.Write(team.Members.Count);
                foreach (Member member in team.Members)
                {
                    writer.WriteText(member.Name);
                    writer.Write(member.Color);
                }
            }
        }

        public static MatchSnapshot Read(BinaryReader reader)
        {
            var snapshot = new MatchSnapshot { Celebrate = reader.ReadBoolean() };
            int winner = reader.ReadInt32();
            int teams = reader.ReadInt32();
            for (int i = 0; i < teams; i++)
            {
                var team = new Team
                {
                    Index = reader.ReadInt32(),
                    Name = reader.ReadString(),
                    Color = reader.ReadColor(),
                    Score = reader.ReadInt32()
                };
                int members = reader.ReadInt32();
                for (int j = 0; j < members; j++)
                {
                    team.Members.Add(new Member { Name = reader.ReadString(), Color = reader.ReadColor() });
                }
                snapshot.Teams.Add(team);
            }
            if (winner >= 0 && winner < snapshot.Teams.Count)
            {
                snapshot.Winner = snapshot.Teams[winner];
            }
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
