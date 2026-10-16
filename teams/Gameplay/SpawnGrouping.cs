using System.Collections.Generic;
using UnityEngine;

namespace SpiderTeams
{
    internal static class SpawnGrouping
    {
        // Spreads one anchor per team with farthest-point sampling, then gives each team the free points nearest its anchor.
        public static bool TrySpawn(LobbyController lobby)
        {
            var manager = TeamManager.Instance;
            if (manager == null || lobby == null)
            {
                return false;
            }

            var points = new List<Transform>();
            Transform[] raw = lobby.GetSpawnPoints();
            if (raw != null)
            {
                for (int i = 0; i < raw.Length; i++)
                {
                    if (raw[i] != null)
                    {
                        points.Add(raw[i]);
                    }
                }
            }
            if (points.Count == 0)
            {
                return false;
            }

            Shuffle(points);
            if (ReflectionCache.LobbySpawnPoints != null)
            {
                ReflectionCache.LobbySpawnPoints.SetValue(null, points.ToArray());
            }

            List<List<PlayerController>> groups = GroupByTeam(manager, lobby.spawnedPlayers);
            if (groups.Count == 0)
            {
                return false;
            }
            Shuffle(groups);

            List<Vector3> anchors = PickAnchors(points, groups.Count);
            var used = new bool[points.Count];
            int rounds = 0;
            for (int i = 0; i < groups.Count; i++)
            {
                rounds = Mathf.Max(rounds, groups[i].Count);
            }

            for (int round = 0; round < rounds; round++)
            {
                for (int group = 0; group < groups.Count; group++)
                {
                    if (round >= groups[group].Count)
                    {
                        continue;
                    }

                    int index = ClosestPoint(points, used, anchors[group], true);
                    if (index < 0)
                    {
                        index = ClosestPoint(points, used, anchors[group], false);
                    }
                    used[index] = true;

                    var player = groups[group][round];
                    player.SpawnCharacter(points[index]);
                    lobby.assignedSpawnPoints[player] = points[index];
                }
            }
            return true;
        }

        private static List<List<PlayerController>> GroupByTeam(TeamManager manager, List<PlayerController> players)
        {
            var groups = new List<List<PlayerController>>();
            var groupOfTeam = new Dictionary<int, int>();
            for (int i = 0; i < players.Count; i++)
            {
                var player = players[i];
                if (player == null)
                {
                    continue;
                }

                int team = manager.GetTeam(player);
                if (team < 0)
                {
                    continue;
                }

                int group;
                if (!groupOfTeam.TryGetValue(team, out group))
                {
                    group = groups.Count;
                    groupOfTeam[team] = group;
                    groups.Add(new List<PlayerController>());
                }
                groups[group].Add(player);
            }
            return groups;
        }

        private static List<Vector3> PickAnchors(List<Transform> points, int count)
        {
            var anchors = new List<Vector3>(count) { points[0].position };
            while (anchors.Count < count)
            {
                int best = 0;
                float bestDistance = -1f;
                for (int i = 0; i < points.Count; i++)
                {
                    float nearest = float.MaxValue;
                    for (int a = 0; a < anchors.Count; a++)
                    {
                        nearest = Mathf.Min(nearest, (points[i].position - anchors[a]).sqrMagnitude);
                    }
                    if (nearest > bestDistance)
                    {
                        bestDistance = nearest;
                        best = i;
                    }
                }
                anchors.Add(points[best].position);
            }
            return anchors;
        }

        private static int ClosestPoint(List<Transform> points, bool[] used, Vector3 anchor, bool freeOnly)
        {
            int best = -1;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < points.Count; i++)
            {
                if (freeOnly && used[i])
                {
                    continue;
                }

                float distance = (points[i].position - anchor).sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = i;
                }
            }
            return best;
        }

        private static void Shuffle<T>(List<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                T swap = list[i];
                list[i] = list[j];
                list[j] = swap;
            }
        }
    }
}
