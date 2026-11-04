using System.Collections.Generic;
using System.IO;
using SpiderSurge.Common;
using Unity.Netcode;
using UnityEngine;

namespace SpiderSurge
{
    // Observer HUD state is separate from ability execution and the caster's local prediction.
    internal static class AbilityDisplayNet
    {
        private const string Channel = "surge.ability-display";
        private readonly struct Entry
        {
            public readonly ulong Player, Spider;
            public readonly AbilityDisplayState State;
            public Entry(ulong player, ulong spider, AbilityDisplayState state) { Player = player; Spider = spider; State = state; }
        }
        private static readonly Dictionary<int, Entry> Remote = new Dictionary<int, Entry>();
        private static readonly List<BaseAbility> Abilities = new List<BaseAbility>();
        private static readonly List<(int id, Entry entry)> Snapshot = new List<(int, Entry)>();
        private static float receivedAt, nextSend;

        public static void Initialize()
        {
            Net.RegisterFromHost(Channel, Read);
            Net.JoinedHost += Clear;
            Net.LeftHost += Clear;
        }

        public static void Shutdown()
        {
            Net.Unregister(Channel);
            Net.JoinedHost -= Clear;
            Net.LeftHost -= Clear;
            Clear();
        }

        private static void Clear() { Remote.Clear(); nextSend = 0f; }

        public static void Tick()
        {
            if (!Net.HasRemotePlayers || Time.unscaledTime < nextSend) return;
            nextSend = Time.unscaledTime + 0.25f;
            Net.SendToAll(Channel, Write);
        }

        public static void SendTo(ulong client) => Net.SendTo(client, Channel, Write);

        private static void Write(BinaryWriter writer)
        {
            Snapshot.Clear();
            if (Players.All != null)
                foreach (var player in Players.All)
                {
                    if (player == null) continue;
                    var spider = Players.SpiderOf(player.playerID.Value);
                    var identity = spider != null ? spider.GetComponent<NetworkObject>() : null;
                    if (identity == null || !identity.IsSpawned) continue;
                    spider.GetComponents(Abilities);
                    foreach (var ability in Abilities)
                    {
                        if (!ability.IsRegularUnlocked()) continue;
                        Snapshot.Add((player.playerID.Value, new Entry(player.NetworkObjectId, identity.NetworkObjectId, ability.StatusSource.CaptureDisplayState())));
                        break;
                    }
                }
            writer.Write(Snapshot.Count);
            foreach (var item in Snapshot)
            {
                writer.Write(item.id);
                writer.Write(item.entry.Player);
                writer.Write(item.entry.Spider);
                item.entry.State.Write(writer);
            }
        }

        private static void Read(BinaryReader reader)
        {
            Remote.Clear();
            receivedAt = Time.time;
            int count = reader.ReadInt32();
            if (count < 0 || count > 1024) throw new InvalidDataException("Invalid ability display count");
            for (int i = 0; i < count; i++)
            {
                int id = reader.ReadInt32();
                Remote[id] = new Entry(reader.ReadUInt64(), reader.ReadUInt64(), AbilityDisplayState.Read(reader));
            }
        }

        public static AbilityDisplayState For(BaseAbility ability)
        {
            if (!Net.IsRemoteClient || Players.IsLocal(ability.PlayerId)) return ability.StatusSource.CaptureDisplayState();
            var player = Players.ById(ability.PlayerId);
            var spider = ability.GetComponent<NetworkObject>();
            if (player == null || spider == null || !Remote.TryGetValue(ability.PlayerId, out var entry)
                || entry.Player != player.NetworkObjectId || entry.Spider != spider.NetworkObjectId) return default;
            return entry.State.Advance(Time.time - receivedAt);
        }
    }
}
