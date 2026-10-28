using System;
using System.IO;
using SpiderSurge.Common;
using Logger = Silk.Logger;

namespace SpiderTeams
{
    // The host owns the roster. Clients ask for team changes and mirror the roster, the scores and the end-of-match snapshot.
    internal static class TeamNet
    {
        private const string RosterChannel = "teams.roster";
        private const string PickChannel = "teams.pick";
        private const string SnapshotChannel = "teams.snapshot";

        private static bool _initialized;

        public static void Initialize()
        {
            if (_initialized)
            {
                return;
            }
            _initialized = true;
            Net.Register(RosterChannel, OnRoster);
            Net.Register(PickChannel, OnPick);
            Net.Register(SnapshotChannel, OnSnapshot);
            Net.PeerReady += OnPeerReady;
            Net.JoinedHost += OnJoinedHost;
            Net.LeftHost += OnLeftHost;
        }

        public static void Shutdown()
        {
            if (!_initialized)
            {
                return;
            }
            _initialized = false;
            Net.Unregister(RosterChannel);
            Net.Unregister(PickChannel);
            Net.Unregister(SnapshotChannel);
            Net.PeerReady -= OnPeerReady;
            Net.JoinedHost -= OnJoinedHost;
            Net.LeftHost -= OnLeftHost;
        }

        public static void BroadcastRoster()
        {
            var manager = TeamManager.Instance;
            if (manager == null || !Net.HasRemotePlayers)
            {
                return;
            }
            Net.SendToAll(RosterChannel, manager.WriteState);
        }

        public static void BroadcastSnapshot(MatchSnapshot snapshot)
        {
            if (snapshot == null || !Net.HasRemotePlayers)
            {
                return;
            }
            Net.SendToAll(SnapshotChannel, snapshot.Write);
        }

        public static void RequestPick(int playerId)
        {
            Net.SendToServer(PickChannel, writer => writer.Write(playerId));
        }

        private static void OnPeerReady(ulong clientId)
        {
            var manager = TeamManager.Instance;
            if (manager != null)
            {
                Net.SendTo(clientId, RosterChannel, manager.WriteState);
            }
        }

        private static void OnJoinedHost()
        {
            TeamManager.Instance?.OnJoinedHost();
        }

        private static void OnLeftHost()
        {
            TeamManager.Instance?.OnLeftHost();
            MatchSnapshot.Clear();
        }

        private static void OnRoster(ulong sender, BinaryReader reader)
        {
            if (!Net.IsRemoteClient)
            {
                return;
            }
            TeamManager.Instance?.ReadState(reader);
        }

        // A client may only move its own players; the host applies the change and the roster broadcast carries it back
        private static void OnPick(ulong sender, BinaryReader reader)
        {
            if (!Net.IsHost)
            {
                return;
            }
            int playerId = reader.ReadInt32();
            var manager = TeamManager.Instance;
            var player = Players.ById(playerId);
            if (manager == null || player == null)
            {
                return;
            }
            if (player.OwnerClientId != sender)
            {
                Logger.LogWarning($"SpiderTeams: client {sender} tried to move P{playerId + 1}, who belongs to client {player.OwnerClientId}");
                return;
            }
            var game = GameController.instance;
            if (game != null && !game.acceptingInputs)
            {
                return;
            }
            if (manager.CycleTeam(player))
            {
                Announcements.QueuePick(playerId);
            }
        }

        private static void OnSnapshot(ulong sender, BinaryReader reader)
        {
            if (!Net.IsRemoteClient)
            {
                return;
            }
            try
            {
                MatchSnapshot.SetRemote(MatchSnapshot.Read(reader));
            }
            catch (Exception ex)
            {
                Logger.LogError($"SpiderTeams could not read the match snapshot: {ex}");
            }
        }
    }
}
