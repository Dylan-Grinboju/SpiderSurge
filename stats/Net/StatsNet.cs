using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SpiderSurge.Common;
using UnityEngine;
using Logger = Silk.Logger;

namespace StatsMod
{
    public enum SnapshotReason : byte
    {
        Tick = 0,
        Started = 1,
        Stopped = 2,
    }

    // The host tracks every stat and sends clients a snapshot about once a second, plus the titles once a session ends.
    // Clients only display what they receive.
    public static class StatsNet
    {
        private const string SnapshotChannel = "stats.snapshot";
        private const string TitlesChannel = "stats.titles";
        private const float SnapshotInterval = 1f;
        private const int MaxMapCounts = 8;

        private static bool _initialized;
        private static float _nextSnapshotAt;

        public static void Initialize()
        {
            if (_initialized)
            {
                return;
            }
            _initialized = true;
            Net.RegisterFromHost(SnapshotChannel, OnSnapshot);
            Net.RegisterFromHost(TitlesChannel, OnTitles);
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
            Net.Unregister(SnapshotChannel);
            Net.Unregister(TitlesChannel);
            Net.PeerReady -= OnPeerReady;
            Net.JoinedHost -= OnJoinedHost;
            Net.LeftHost -= OnLeftHost;
        }

        // Called every frame from PauseHandler.Update
        public static void Tick()
        {
            if (!Net.HasRemotePlayers || !GameSessionManager.Instance.IsActive)
            {
                return;
            }
            if (Time.unscaledTime < _nextSnapshotAt)
            {
                return;
            }
            SendSnapshot(SnapshotReason.Tick);
        }

        public static void SendSnapshot(SnapshotReason reason)
        {
            _nextSnapshotAt = Time.unscaledTime + SnapshotInterval;
            Net.SendToAll(SnapshotChannel, writer => WriteSnapshot(writer, reason));
        }

        public static void SendTitles()
        {
            Net.SendToAll(TitlesChannel, WriteTitles);
        }

        private static void OnPeerReady(ulong clientId)
        {
            Net.SendTo(clientId, SnapshotChannel, writer => WriteSnapshot(writer, SnapshotReason.Tick));
            if (TitlesManager.Instance.HasGameEndedTitles)
            {
                Net.SendTo(clientId, TitlesChannel, WriteTitles);
            }
        }

        private static void OnJoinedHost()
        {
            RunRecap.Clear();
            RunRecap.Begin();
            GameSessionManager.Instance.ClearRemote();
            TitlesManager.Instance.ClearTitles();
        }

        private static void OnLeftHost()
        {
            GameSessionManager.Instance.ClearRemote();
            PlayerTracker.Instance.ResetPlayerStats();
            TitlesManager.Instance.ClearTitles();
            UIManager.AutoPullHUD();
        }

        private static void WriteSnapshot(BinaryWriter writer, SnapshotReason reason)
        {
            writer.Write((byte)reason);
            GameSessionManager.Instance.WriteSession(writer);
            RunRecap.Write(writer);
            var players = PlayerTracker.Instance.GetActivePlayers();
            writer.Write(players.Count);
            foreach (var pair in players)
            {
                WritePlayer(writer, pair.Key != null ? pair.Key.playerID.Value : -1, pair.Value);
            }
        }

        private static void WritePlayer(BinaryWriter writer, int playerId, PlayerTracker.PlayerData data)
        {
            writer.Write(playerId);
            writer.WriteText(data.PlayerName);
            writer.Write(data.PlayerColor);
            writer.Write(data.SecondaryColor);
            writer.Write(data.IsAlive);
            writer.Write(data.Deaths);
            writer.Write(data.LavaDeaths);
            writer.Write(data.Kills);
            writer.Write(data.KillsWhileAirborne);
            writer.Write(data.KillsWhileSolo);
            writer.Write(data.FriendlyKills);
            writer.Write(data.WaveClutches);
            writer.Write(data.AstralReturns);
            writer.Write(data.EnemyShieldsTakenDown);
            writer.Write(data.FriendlyShieldsHit);
            writer.Write(data.ShieldsLost);
            writer.Write(data.KillStreak);
            writer.Write(data.MaxKillStreak);
            writer.Write(data.KillStreakWhileSolo);
            writer.Write(data.MaxKillStreakWhileSolo);
            writer.Write(data.WebSwings);
            writer.Write((float)data.GetCurrentAliveTime().TotalSeconds);
            writer.Write((float)data.GetCurrentWebSwingTime().TotalSeconds);
            writer.Write((float)data.GetCurrentAirborneTime().TotalSeconds);
            writer.Write(data.HighestPoint);
            WriteCounts(writer, data.WeaponHits);
            WriteCounts(writer, data.EnemyKills);
            // Up to 32 players with a count per map would outgrow one message, so only the maps with the most deaths go
            WriteCounts(writer, data.DeathsPerMap.Count <= MaxMapCounts
                ? data.DeathsPerMap
                : data.DeathsPerMap.OrderByDescending(pair => pair.Value).Take(MaxMapCounts).ToDictionary(pair => pair.Key, pair => pair.Value));
            writer.Write(data.ShootingAccuracy.ShotsFired);
            writer.Write(data.ShootingAccuracy.ShotsHit);
            writer.Write(data.AccuracyByWeapon.Count);
            foreach (var pair in data.AccuracyByWeapon)
            {
                writer.WriteText(pair.Key);
                writer.Write(pair.Value.ShotsFired);
                writer.Write(pair.Value.ShotsHit);
            }
        }

        private static void OnSnapshot(BinaryReader reader)
        {
            try
            {
                var reason = (SnapshotReason)reader.ReadByte();
                var session = GameSessionManager.Instance;
                session.ReadSession(reader);
                RunRecap.Read(reader);

                int count = reader.ReadInt32();
                for (int i = 0; i < count; i++)
                {
                    ReadPlayer(reader);
                }

                session.OnRemoteSnapshot(reason);
            }
            catch (Exception ex)
            {
                Logger.LogError($"SpiderSurge could not read the stats snapshot: {ex}");
            }
        }

        private static void ReadPlayer(BinaryReader reader)
        {
            int playerId = reader.ReadInt32();
            var data = PlayerTracker.Instance.RemoteDataFor(playerId);
            var scratch = data ?? new PlayerTracker.PlayerData(playerId);
            // The client registered this player before the host assigned its playerID, so the id is taken from the host
            scratch.PlayerId = playerId;
            scratch.PlayerName = reader.ReadString();
            scratch.PlayerColor = reader.ReadColor();
            scratch.SecondaryColor = reader.ReadColor();
            scratch.RemoteAlive = reader.ReadBoolean();
            scratch.Deaths = reader.ReadInt32();
            scratch.LavaDeaths = reader.ReadInt32();
            scratch.Kills = reader.ReadInt32();
            scratch.KillsWhileAirborne = reader.ReadInt32();
            scratch.KillsWhileSolo = reader.ReadInt32();
            scratch.FriendlyKills = reader.ReadInt32();
            scratch.WaveClutches = reader.ReadInt32();
            scratch.AstralReturns = reader.ReadInt32();
            scratch.EnemyShieldsTakenDown = reader.ReadInt32();
            scratch.FriendlyShieldsHit = reader.ReadInt32();
            scratch.ShieldsLost = reader.ReadInt32();
            scratch.KillStreak = reader.ReadInt32();
            scratch.MaxKillStreak = reader.ReadInt32();
            scratch.KillStreakWhileSolo = reader.ReadInt32();
            scratch.MaxKillStreakWhileSolo = reader.ReadInt32();
            scratch.WebSwings = reader.ReadInt32();
            scratch.TotalAliveTime = reader.ReadSingle();
            scratch.WebSwingTime = reader.ReadSingle();
            scratch.AirborneTime = reader.ReadSingle();
            scratch.HighestPoint = reader.ReadSingle();
            scratch.CurrentAliveStartTime = null;
            scratch.CurrentWebSwingStartTime = null;
            scratch.CurrentAirborneStartTime = null;
            ReadCounts(reader, scratch.WeaponHits);
            ReadCounts(reader, scratch.EnemyKills);
            ReadCounts(reader, scratch.DeathsPerMap);
            scratch.ShootingAccuracy.Set(reader.ReadInt32(), reader.ReadInt32());
            scratch.AccuracyByWeapon.Clear();
            int weapons = reader.ReadInt32();
            for (int i = 0; i < weapons; i++)
            {
                string weapon = reader.ReadString();
                var accuracy = new WeaponAccuracy();
                accuracy.Set(reader.ReadInt32(), reader.ReadInt32());
                scratch.AccuracyByWeapon[weapon] = accuracy;
            }
        }

        private static void WriteTitles(BinaryWriter writer)
        {
            var manager = TitlesManager.Instance;
            WriteTitleList(writer, manager.CurrentTitles);
            WriteTitleList(writer, manager.AllTitles);
        }

        private static void WriteTitleList(BinaryWriter writer, List<TitleEntry> titles)
        {
            writer.Write(titles.Count);
            foreach (var title in titles)
            {
                writer.WriteText(title.TitleName);
                writer.WriteText(title.Description);
                writer.WriteText(title.PlayerName);
                writer.Write(title.PrimaryColor);
                writer.Write(title.SecondaryColor);
                writer.Write(title.PlayerId);
                writer.Write(title.Priority);
            }
        }

        private static void OnTitles(BinaryReader reader)
        {
            try
            {
                var current = ReadTitleList(reader);
                var all = ReadTitleList(reader);
                TitlesManager.Instance.SetRemoteTitles(current, all);
                GameSessionManager.Instance.OnRemoteTitles(current.Count);
            }
            catch (Exception ex)
            {
                Logger.LogError($"SpiderSurge could not read the stats titles: {ex}");
            }
        }

        private static List<TitleEntry> ReadTitleList(BinaryReader reader)
        {
            int count = reader.ReadInt32();
            var titles = new List<TitleEntry>(count);
            for (int i = 0; i < count; i++)
            {
                var title = new TitleEntry
                {
                    TitleName = reader.ReadString(),
                    Description = reader.ReadString(),
                    PlayerName = reader.ReadString(),
                    PrimaryColor = reader.ReadColor(),
                    SecondaryColor = reader.ReadColor(),
                    PlayerId = reader.ReadInt32(),
                    Priority = reader.ReadInt32(),
                };
                title.Player = Players.ById(title.PlayerId);
                titles.Add(title);
            }
            return titles;
        }

        private static void WriteCounts(BinaryWriter writer, Dictionary<string, int> counts)
        {
            writer.Write(counts.Count);
            foreach (var pair in counts)
            {
                writer.WriteText(pair.Key);
                writer.Write(pair.Value);
            }
        }

        private static void ReadCounts(BinaryReader reader, Dictionary<string, int> counts)
        {
            counts.Clear();
            int count = reader.ReadInt32();
            for (int i = 0; i < count; i++)
            {
                string key = reader.ReadString();
                counts[key] = reader.ReadInt32();
            }
        }
    }
}
