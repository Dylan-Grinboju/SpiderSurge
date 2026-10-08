using System;
using System.Collections.Generic;
using UnityEngine;
using Logger = Silk.Logger;

namespace SpiderTeams
{
    // Announcer holds a single pending popup, so broadcasts are spaced out instead of sent in the same frame.
    internal static class Announcements
    {
        private const int MaxQueued = 40;

        private struct Pending
        {
            public string Text;
            public string[] Args;
            public Color Color;
            public bool Important;
        }

        private struct Delayed
        {
            public float Due;
            public Pending Item;
        }

        private static readonly Queue<Pending> _queue = new Queue<Pending>();
        private static readonly List<Delayed> _delayed = new List<Delayed>();
        private static readonly Dictionary<int, float> _pickDue = new Dictionary<int, float>();
        private static readonly List<int> _duePicks = new List<int>();
        private static float _nextSend;
        private static bool _loggedError;

        public static void Broadcast(string text, Color color, bool important)
        {
            Enqueue(text, null, color, important);
        }

        public static void BroadcastTerm(string term, string[] args, Color color, bool important)
        {
            Enqueue(term, args, color, important);
        }

        public static void BroadcastLater(string text, Color color, bool important, float delay)
        {
            if (!string.IsNullOrEmpty(text))
            {
                _delayed.Add(new Delayed { Due = Time.unscaledTime + delay, Item = new Pending { Text = text, Color = color, Important = important } });
            }
        }

        public static void QueuePick(int playerId)
        {
            _pickDue[playerId] = Time.unscaledTime + Consts.Timing.PickAnnounceDebounce;
        }

        public static void ClearPicks()
        {
            _pickDue.Clear();
        }

        public static void Clear()
        {
            _queue.Clear();
            _delayed.Clear();
            _pickDue.Clear();
        }

        public static void Tick()
        {
            try
            {
                float now = Time.unscaledTime;
                if (_pickDue.Count > 0)
                {
                    FlushPicks(now);
                }
                if (_delayed.Count > 0)
                {
                    FlushDelayed(now);
                }

                if (_queue.Count == 0 || now < _nextSend)
                {
                    return;
                }

                var announcer = Announcer.instance;
                if (announcer == null || !TeamManager.IsHost)
                {
                    _queue.Clear();
                    return;
                }

                Pending next = _queue.Dequeue();
                _nextSend = now + Consts.Timing.AnnounceSpacing;
                announcer.Announce(next.Text, next.Args, next.Color, next.Important, false, null);
            }
            catch (Exception ex)
            {
                if (!_loggedError)
                {
                    _loggedError = true;
                    Logger.LogError($"SpiderTeams announcement failed: {ex}");
                }
            }
        }

        private static void FlushPicks(float now)
        {
            _duePicks.Clear();
            foreach (var pair in _pickDue)
            {
                if (now >= pair.Value)
                {
                    _duePicks.Add(pair.Key);
                }
            }

            var manager = TeamManager.Instance;
            for (int i = 0; i < _duePicks.Count; i++)
            {
                int playerId = _duePicks[i];
                _pickDue.Remove(playerId);

                var player = TeamManager.FindPlayer(playerId);
                if (manager == null || player == null)
                {
                    continue;
                }

                int team = manager.GetTeam(player);
                Broadcast($"{TeamManager.RichName(player)} joined {TeamPalette.TeamName(team)}", TeamPalette.TeamColor(team), false);
            }
        }

        private static void FlushDelayed(float now)
        {
            for (int i = 0; i < _delayed.Count; i++)
            {
                if (now >= _delayed[i].Due)
                {
                    Pending item = _delayed[i].Item;
                    _delayed.RemoveAt(i--);
                    Enqueue(item.Text, item.Args, item.Color, item.Important);
                }
            }
        }

        private static void Enqueue(string text, string[] args, Color color, bool important)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            while (_queue.Count >= MaxQueued)
            {
                _queue.Dequeue();
            }
            _queue.Enqueue(new Pending { Text = text, Args = args, Color = color, Important = important });
        }
    }
}
