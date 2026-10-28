using System;
using System.Collections.Generic;
using System.Text;
using SpiderSurge.Common;
using UnityEngine;
using Logger = Silk.Logger;

namespace SpiderTeams
{
    // Announcer holds a single pending popup, so broadcasts are spaced out instead of sent in the same frame.
    internal static class Announcements
    {
        private const int MaxQueued = 40;
        private const int MaxBannerBytes = 480;

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
        private static readonly List<int> _picks = new List<int>();
        private static readonly List<int> _pickTeams = new List<int>();
        private static readonly List<string> _pickNames = new List<string>();
        private static readonly List<string> _lineNames = new List<string>();
        private static readonly StringBuilder _banner = new StringBuilder();
        private static int _bannerBytes;
        private static float _picksFirst;
        private static float _picksDue;
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

        // Picks made close together share one banner, and a steady stream of picks still shows one every PickAnnounceMaxWait.
        public static void QueuePick(int playerId)
        {
            float now = Time.unscaledTime;
            if (_picks.Count == 0)
            {
                _picksFirst = now;
            }
            if (!_picks.Contains(playerId))
            {
                _picks.Add(playerId);
            }
            _picksDue = Mathf.Min(now + Consts.Timing.PickAnnounceDebounce, _picksFirst + Consts.Timing.PickAnnounceMaxWait);
        }

        public static void ClearPicks()
        {
            _picks.Clear();
        }

        public static void Clear()
        {
            _queue.Clear();
            _delayed.Clear();
            _picks.Clear();
        }

        public static void Tick()
        {
            try
            {
                float now = Time.unscaledTime;
                if (_picks.Count > 0 && now >= _picksDue)
                {
                    FlushPicks();
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
                if (announcer == null || !Net.IsHost)
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

        // One line per team, tinted when the banner names more than one team.
        private static void FlushPicks()
        {
            var manager = TeamManager.Instance;
            _pickTeams.Clear();
            _pickNames.Clear();
            for (int i = 0; i < _picks.Count && manager != null; i++)
            {
                var player = Players.ById(_picks[i]);
                if (player != null)
                {
                    _pickTeams.Add(manager.GetTeam(player));
                    _pickNames.Add(TeamManager.RichName(player));
                }
            }
            _picks.Clear();
            if (_pickTeams.Count == 0)
            {
                return;
            }

            int firstTeam = _pickTeams[0];
            bool oneTeam = _pickTeams.TrueForAll(team => team == firstTeam);
            Color color = oneTeam ? TeamPalette.TeamColor(firstTeam) : Color.white;
            for (int i = 0; i < _pickTeams.Count; i++)
            {
                if (_pickTeams.IndexOf(_pickTeams[i]) == i)
                {
                    AddTeamLines(_pickTeams[i], !oneTeam, color);
                }
            }
            SendBanner(color);
        }

        // Announcements travel as 512-byte network strings, so a long list of names wraps onto more lines and banners.
        private static void AddTeamLines(int team, bool tinted, Color color)
        {
            string prefix = tinted ? "<color=#" + TeamPalette.Hex(TeamPalette.TeamColor(team)) + ">" : string.Empty;
            string suffix = " joined " + TeamPalette.TeamName(team) + (tinted ? "</color>" : string.Empty);
            int budget = MaxBannerBytes - Bytes(prefix) - Bytes(suffix);
            int used = 0;
            _lineNames.Clear();
            for (int i = 0; i < _pickTeams.Count; i++)
            {
                if (_pickTeams[i] != team)
                {
                    continue;
                }

                int cost = Bytes(_pickNames[i]) + 5;
                if (_lineNames.Count > 0 && used + cost > budget)
                {
                    AddLine(prefix, suffix, color);
                    used = 0;
                }
                _lineNames.Add(_pickNames[i]);
                used += cost;
            }
            AddLine(prefix, suffix, color);
        }

        private static void AddLine(string prefix, string suffix, Color color)
        {
            var line = new StringBuilder(prefix);
            for (int i = 0; i < _lineNames.Count; i++)
            {
                if (i > 0)
                {
                    line.Append(i == _lineNames.Count - 1 ? " and " : ", ");
                }
                line.Append(_lineNames[i]);
            }
            line.Append(suffix);
            _lineNames.Clear();

            string text = line.ToString();
            int bytes = Bytes(text);
            if (_banner.Length > 0 && _bannerBytes + 1 + bytes > MaxBannerBytes)
            {
                SendBanner(color);
            }
            if (_banner.Length > 0)
            {
                _banner.Append('\n');
                _bannerBytes++;
            }
            _banner.Append(text);
            _bannerBytes += bytes;
        }

        private static void SendBanner(Color color)
        {
            if (_banner.Length > 0)
            {
                Broadcast(_banner.ToString(), color, false);
            }
            _banner.Length = 0;
            _bannerBytes = 0;
        }

        private static int Bytes(string text)
        {
            return Encoding.UTF8.GetByteCount(text);
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
