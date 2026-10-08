using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using Unity.Netcode;
using UnityEngine;
using Logger = Silk.Logger;

namespace SpiderTeams
{
    public class TeamManager : MonoBehaviour
    {
        public static TeamManager Instance { get; private set; }
        public static event Action RosterChanged;

        private static readonly Regex NoParseTag = new Regex("</?noparse>", RegexOptions.IgnoreCase);
        private static readonly Comparison<PlayerController> ByPlayerId = (a, b) => a.playerID.Value.CompareTo(b.playerID.Value);

        private readonly Dictionary<int, int> _teamOf = new Dictionary<int, int>();
        private readonly int[] _teamScores = new int[Consts.MaxTeams];
        private readonly int[] _sizes = new int[Consts.MaxTeams];
        private readonly int[] _assignSizes = new int[Consts.MaxTeams];
        private readonly List<PlayerController> _scratch = new List<PlayerController>(Consts.MaxTeams);
        private int _matchTeamCount = Consts.Defaults.TeamCount;
        private bool _rosterDirty;
        private float _nextLabelRefresh;
        private bool _clientNoticeShown;

        public bool MatchLocked { get; private set; }
        public bool TeamModeThisMatch { get; private set; }
        public int TeamCount => MatchLocked ? _matchTeamCount : ModConfig.TeamCount;

        public static bool IsHost
        {
            get
            {
                var network = NetworkManager.Singleton;
                return network != null && network.IsListening && network.IsHost;
            }
        }

        public static bool TeamMatchActive
        {
            get
            {
                var manager = Instance;
                if (manager == null || !manager.TeamModeThisMatch || !IsHost)
                {
                    return false;
                }

                var versus = VersusMode.instance;
                if (versus == null || !versus.GameModeActive())
                {
                    return false;
                }
                return manager.CountPresentTeams() >= 2;
            }
        }

        public static List<PlayerController> Players
        {
            get
            {
                var lobby = LobbyController.instance;
                return lobby != null ? lobby.spawnedPlayers : null;
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnEnable()
        {
            ModConfig.Changed += OnConfigChanged;
        }

        private void OnDisable()
        {
            ModConfig.Changed -= OnConfigChanged;
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Update()
        {
            MainThread.Drain();
            Announcements.Tick();

            if (_rosterDirty)
            {
                _rosterDirty = false;
                RaiseRosterChanged();
            }

            if (Time.unscaledTime >= _nextLabelRefresh)
            {
                _nextLabelRefresh = Time.unscaledTime + Consts.Timing.LobbyLabelInterval;
                if (LobbyController.inLobby)
                {
                    LobbyLabel.Refresh();
                }
            }
        }

        private void OnConfigChanged()
        {
            if (!MatchLocked)
            {
                NormalizeRoster();
                MarkRosterChanged();
            }
            LobbyLabel.Refresh();
        }

        public void MarkRosterChanged()
        {
            _rosterDirty = true;
        }

        public void BeginMatch()
        {
            Array.Clear(_teamScores, 0, _teamScores.Length);
            MatchSnapshot.Clear();
            Announcements.ClearPicks();
            _matchTeamCount = ModConfig.TeamCount;
            MatchLocked = true;
            TeamModeThisMatch = ModConfig.TeamsEnabled && IsHost;
            NormalizeRoster();
            MarkRosterChanged();

            if (TeamModeThisMatch)
            {
                Logger.LogInfo($"SpiderTeams match started with {_matchTeamCount} teams: {DescribeRoster()}");
            }
            else
            {
                Logger.LogInfo("SpiderTeams: team mode is off for this match");
            }
        }

        public void EndMatch()
        {
            if (!MatchLocked && !TeamModeThisMatch)
            {
                return;
            }
            MatchLocked = false;
            TeamModeThisMatch = false;
            MarkRosterChanged();
        }

        public void OnRoundStarting()
        {
            NormalizeRoster();
        }

        public void OnLobbyLoaded()
        {
            var versus = VersusMode.instance;
            if (MatchLocked && (versus == null || !versus.GameModeActive()))
            {
                EndMatch();
            }
            NormalizeRoster();
            MarkRosterChanged();
        }

        public void ShowClientNotice()
        {
            if (_clientNoticeShown || !ModConfig.TeamsEnabled)
            {
                return;
            }
            _clientNoticeShown = true;
            Logger.LogInfo("SpiderTeams: someone else is hosting this match, so their rules apply");
            Toast.Show("SpiderTeams: teams only apply when you host", Color.white);
        }

        public int AddTeamPoint(int team)
        {
            return team >= 0 && team < Consts.MaxTeams ? ++_teamScores[team] : 0;
        }

        public int GetTeamScore(int team)
        {
            return team >= 0 && team < Consts.MaxTeams ? _teamScores[team] : 0;
        }

        // A player with no team, or one past the current count, joins the smallest team.
        public int GetTeam(PlayerController player)
        {
            if (player == null)
            {
                return -1;
            }

            int id = player.playerID.Value;
            int count = TeamCount;
            int team;
            if (_teamOf.TryGetValue(id, out team) && team >= 0 && team < count)
            {
                return team;
            }

            team = SmallestTeam(count, id);
            _teamOf[id] = team;
            MarkRosterChanged();
            return team;
        }

        public bool TryGetTeamOfPlayerId(int playerId, out int team)
        {
            return _teamOf.TryGetValue(playerId, out team) && team >= 0 && team < TeamCount;
        }

        public int GetMemberRank(PlayerController player)
        {
            var players = Players;
            if (player == null || players == null)
            {
                return 0;
            }

            int team = GetTeam(player);
            int id = player.playerID.Value;
            int rank = 0;
            for (int i = 0; i < players.Count; i++)
            {
                var other = players[i];
                if (other != null && other != player && other.playerID.Value < id && GetTeam(other) == team)
                {
                    rank++;
                }
            }
            return rank;
        }

        public void CollectPlayers(List<PlayerController> into)
        {
            into.Clear();
            var players = Players;
            if (players == null)
            {
                return;
            }

            for (int i = 0; i < players.Count; i++)
            {
                if (players[i] != null)
                {
                    into.Add(players[i]);
                }
            }
        }

        public void CollectMembers(int team, List<PlayerController> into)
        {
            into.Clear();
            var players = Players;
            if (players == null)
            {
                return;
            }

            for (int i = 0; i < players.Count; i++)
            {
                var player = players[i];
                if (player != null && GetTeam(player) == team)
                {
                    into.Add(player);
                }
            }
            into.Sort(ByPlayerId);
        }

        public static PlayerController FindPlayer(int playerId)
        {
            var players = Players;
            if (players == null)
            {
                return null;
            }

            for (int i = 0; i < players.Count; i++)
            {
                var player = players[i];
                if (player != null && player.playerID.Value == playerId)
                {
                    return player;
                }
            }
            return null;
        }

        public void GetTeamMasks(out uint present, out uint alive)
        {
            present = 0;
            alive = 0;
            var players = Players;
            if (players == null)
            {
                return;
            }

            for (int i = 0; i < players.Count; i++)
            {
                var player = players[i];
                if (player == null)
                {
                    continue;
                }

                int team = GetTeam(player);
                if (team < 0)
                {
                    continue;
                }

                uint bit = 1u << team;
                present |= bit;
                if (player.isAlive)
                {
                    alive |= bit;
                }
            }
        }

        public int CountPresentTeams()
        {
            uint present, alive;
            GetTeamMasks(out present, out alive);
            return PopCount(present);
        }

        public static int PopCount(uint mask)
        {
            int count = 0;
            while (mask != 0)
            {
                mask &= mask - 1;
                count++;
            }
            return count;
        }

        public static int LowestTeam(uint mask)
        {
            for (int team = 0; team < Consts.MaxTeams; team++)
            {
                if ((mask & (1u << team)) != 0)
                {
                    return team;
                }
            }
            return -1;
        }

        public bool CycleTeam(PlayerController player)
        {
            if (player == null || MatchLocked)
            {
                return false;
            }

            _teamOf[player.playerID.Value] = (GetTeam(player) + 1) % TeamCount;
            MarkRosterChanged();
            return true;
        }

        public void Shuffle()
        {
            if (MatchLocked)
            {
                return;
            }

            CollectPlayers(_scratch);
            for (int i = _scratch.Count - 1; i > 0; i--)
            {
                int j = UnityEngine.Random.Range(0, i + 1);
                var swap = _scratch[i];
                _scratch[i] = _scratch[j];
                _scratch[j] = swap;
            }

            int count = TeamCount;
            for (int i = 0; i < _scratch.Count; i++)
            {
                _teamOf[_scratch[i].playerID.Value] = i % count;
            }
            MarkRosterChanged();
        }

        // Moves the highest-numbered player from the largest team to the smallest until sizes differ by at most one.
        public void Balance()
        {
            if (MatchLocked)
            {
                return;
            }

            int count = TeamCount;
            NormalizeRoster();
            CollectPlayers(_scratch);
            for (int guard = 0; guard < Consts.MaxTeams * Consts.MaxTeams; guard++)
            {
                Array.Clear(_sizes, 0, _sizes.Length);
                for (int i = 0; i < _scratch.Count; i++)
                {
                    _sizes[GetTeam(_scratch[i])]++;
                }

                int largest = 0;
                int smallest = 0;
                for (int team = 1; team < count; team++)
                {
                    if (_sizes[team] > _sizes[largest])
                    {
                        largest = team;
                    }
                    if (_sizes[team] < _sizes[smallest])
                    {
                        smallest = team;
                    }
                }
                if (_sizes[largest] - _sizes[smallest] <= 1)
                {
                    break;
                }

                PlayerController mover = null;
                for (int i = 0; i < _scratch.Count; i++)
                {
                    var player = _scratch[i];
                    if (GetTeam(player) == largest && (mover == null || player.playerID.Value > mover.playerID.Value))
                    {
                        mover = player;
                    }
                }
                if (mover == null)
                {
                    break;
                }
                _teamOf[mover.playerID.Value] = smallest;
            }
            MarkRosterChanged();
        }

        public void NormalizeRoster()
        {
            var players = Players;
            if (players == null)
            {
                return;
            }

            for (int i = 0; i < players.Count; i++)
            {
                if (players[i] != null)
                {
                    GetTeam(players[i]);
                }
            }
        }

        public void Forget(PlayerController player)
        {
            if (player != null && _teamOf.Remove(player.playerID.Value))
            {
                MarkRosterChanged();
            }
        }

        public string DescribeRoster()
        {
            var builder = new StringBuilder();
            for (int team = 0; team < TeamCount; team++)
            {
                CollectMembers(team, _scratch);
                if (_scratch.Count == 0)
                {
                    continue;
                }

                if (builder.Length > 0)
                {
                    builder.Append(" | ");
                }
                builder.Append(TeamPalette.TeamName(team)).Append(": ");
                for (int i = 0; i < _scratch.Count; i++)
                {
                    if (i > 0)
                    {
                        builder.Append(", ");
                    }
                    builder.Append(DisplayName(_scratch[i]));
                }
            }
            return builder.Length > 0 ? builder.ToString() : "no players";
        }

        public static string DisplayName(PlayerController player)
        {
            if (player == null)
            {
                return "?";
            }

            string name = null;
            try
            {
                name = player.playerName;
            }
            catch
            {
            }
            return string.IsNullOrEmpty(name) ? "P" + (player.playerID.Value + 1) : name;
        }

        public static string RichName(PlayerController player)
        {
            return "<noparse>" + NoParseTag.Replace(DisplayName(player), string.Empty) + "</noparse>";
        }

        private int SmallestTeam(int count, int excludeId)
        {
            Array.Clear(_assignSizes, 0, _assignSizes.Length);
            var players = Players;
            if (players != null)
            {
                for (int i = 0; i < players.Count; i++)
                {
                    var player = players[i];
                    if (player == null)
                    {
                        continue;
                    }

                    int id = player.playerID.Value;
                    int team;
                    if (id != excludeId && _teamOf.TryGetValue(id, out team) && team >= 0 && team < count)
                    {
                        _assignSizes[team]++;
                    }
                }
            }

            int best = 0;
            for (int team = 1; team < count; team++)
            {
                if (_assignSizes[team] < _assignSizes[best])
                {
                    best = team;
                }
            }
            return best;
        }

        private static void RaiseRosterChanged()
        {
            Action handler = RosterChanged;
            if (handler == null)
            {
                return;
            }

            foreach (Delegate listener in handler.GetInvocationList())
            {
                try
                {
                    ((Action)listener)();
                }
                catch (Exception ex)
                {
                    Logger.LogError($"SpiderTeams roster listener failed: {ex}");
                }
            }
        }
    }
}
