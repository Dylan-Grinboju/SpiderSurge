using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using SpiderSurge.Common;
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

        // True on a client too, once the host's roster says team mode is on; host-only work checks HostTeamMatch
        public static bool TeamMatchActive
        {
            get
            {
                var manager = Instance;
                return TeamModeRunning(manager) && (manager.StartedWithTeams || manager.CountPresentTeams() >= 2);
            }
        }

        public static bool HostTeamMatch => Net.IsHost && TeamMatchActive;

        // HostTeamMatch without counting teams, which hands a team to anyone still without one; it holds whenever HostTeamMatch does
        internal static bool HostTeamMode => Net.IsHost && TeamModeRunning(Instance);

        // A team match stays one after a whole team leaves online, so the team that is left wins its rounds
        public bool StartedWithTeams => MatchLocked && _teamsAtLock >= 2;
        private int _teamsAtLock;

        private static bool TeamModeRunning(TeamManager manager)
        {
            if (manager == null || !manager.TeamModeThisMatch)
            {
                return false;
            }

            var versus = VersusMode.instance;
            return versus != null && versus.GameModeActive();
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
            Announcements.Tick();

            if (_rosterDirty)
            {
                // A newcomer gets a team before the roster goes out, so clients never have to guess one
                if (Net.IsHost)
                {
                    NormalizeRoster();
                }
                _rosterDirty = false;
                RaiseRosterChanged();
                if (Net.IsHost)
                {
                    TeamNet.BroadcastRoster();
                }
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

        // A client shows the host's roster and never assigns teams itself
        private static bool HostOwnsRoster => Net.IsRemoteClient;

        private void OnConfigChanged()
        {
            if (!MatchLocked && !HostOwnsRoster)
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

        // An online match opens in the WaitingRoom, where players still join and pick, so the roster locks at the first real round
        public void BeginMatch(bool lockNow)
        {
            Array.Clear(_teamScores, 0, _teamScores.Length);
            MatchSnapshot.Clear();
            Announcements.ClearPicks();
            TeamModeThisMatch = ModConfig.TeamsEnabled && Net.IsHost;
            MatchLocked = false;
            if (lockNow)
            {
                LockRoster();
            }
            else
            {
                NormalizeRoster();
                MarkRosterChanged();
                Logger.LogInfo("SpiderTeams: waiting room, teams stay open until the first round");
            }
        }

        public void LockRoster()
        {
            if (MatchLocked)
            {
                return;
            }
            _matchTeamCount = ModConfig.TeamCount;
            MatchLocked = true;
            NormalizeRoster();
            _teamsAtLock = TeamModeThisMatch ? CountPresentTeams() : 0;
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
            _teamsAtLock = 0;
            MarkRosterChanged();
        }

        public void OnRoundStarting()
        {
            NormalizeRoster();
        }

        public void OnLobbyLoaded()
        {
            if (HostOwnsRoster)
            {
                MarkRosterChanged();
                return;
            }
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
            Logger.LogInfo("SpiderTeams: the host's team rules apply");
            Toast.Show("SpiderSurge: the host's team rules apply", Color.white);
        }

        // A joining client's roster still describes its own local session, whose playerIDs mean other people here
        public void OnJoinedHost()
        {
            ResetSession();
        }

        public void OnLeftHost()
        {
            ResetSession();
        }

        private void ResetSession()
        {
            _teamOf.Clear();
            Array.Clear(_teamScores, 0, _teamScores.Length);
            MatchLocked = false;
            TeamModeThisMatch = false;
            _teamsAtLock = 0;
            _clientNoticeShown = false;
            MarkRosterChanged();
        }

        public void WriteState(BinaryWriter writer)
        {
            writer.Write(TeamCount);
            writer.Write(MatchLocked);
            writer.Write(TeamModeThisMatch);
            writer.Write(_teamOf.Count);
            foreach (var pair in _teamOf)
            {
                writer.Write(pair.Key);
                writer.Write(pair.Value);
            }
            writer.Write(_teamScores.Length);
            for (int team = 0; team < _teamScores.Length; team++)
            {
                writer.Write(_teamScores[team]);
            }
            writer.Write(_teamsAtLock);
        }

        public void ReadState(BinaryReader reader)
        {
            int teamCount = reader.ReadInt32();
            bool locked = reader.ReadBoolean();
            TeamModeThisMatch = reader.ReadBoolean();
            _teamOf.Clear();
            int count = reader.ReadInt32();
            for (int i = 0; i < count; i++)
            {
                int id = reader.ReadInt32();
                _teamOf[id] = reader.ReadInt32();
            }
            int scores = reader.ReadInt32();
            for (int team = 0; team < scores; team++)
            {
                _teamScores[team] = reader.ReadInt32();
            }
            _teamsAtLock = reader.ReadInt32();
            // A client keeps the host's team count in force through MatchLocked, whose TeamCount reads _matchTeamCount
            _matchTeamCount = teamCount;
            MatchLocked = locked || Net.IsRemoteClient;
            _remoteLocked = locked;
            MarkRosterChanged();
        }

        private bool _remoteLocked;

        // Whether the host still lets players pick; on the host this is !MatchLocked
        public bool PicksOpen => Net.IsRemoteClient ? !_remoteLocked : !MatchLocked;

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

            if (HostOwnsRoster)
            {
                return SmallestTeam(count, id);
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
            var players = Players.All;
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
            var players = Players.All;
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
            var players = Players.All;
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

        public void GetTeamMasks(out uint present, out uint alive)
        {
            present = 0;
            alive = 0;
            var players = Players.All;
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

        public bool CycleTeam(PlayerController player, int step = 1)
        {
            if (player == null || !PicksOpen)
            {
                return false;
            }
            if (Net.IsRemoteClient)
            {
                TeamNet.RequestPick(player.playerID.Value);
                return false;
            }

            int count = TeamCount;
            _teamOf[player.playerID.Value] = ((GetTeam(player) + step) % count + count) % count;
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
            var players = Players.All;
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
            if (player != null)
            {
                _teamOf.Remove(player.playerID.Value);
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
                    builder.Append(Players.NameOf(_scratch[i]));
                }
            }
            return builder.Length > 0 ? builder.ToString() : "no players";
        }

        public static string RichName(PlayerController player)
        {
            return "<noparse>" + NoParseTag.Replace(Players.NameOf(player), string.Empty) + "</noparse>";
        }

        private int SmallestTeam(int count, int excludeId)
        {
            Array.Clear(_assignSizes, 0, _assignSizes.Length);
            var players = Players.All;
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

        private static void RaiseRosterChanged() => Listeners.Raise(RosterChanged, "SpiderTeams roster listener failed");
    }
}
