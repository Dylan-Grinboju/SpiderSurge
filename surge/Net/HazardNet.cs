using System.IO;
using SpiderSurge.Common;
using SpiderSurge.Enemies;
using SpiderSurge.Weapons;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SpiderSurge
{
    internal static class HazardNet
    {
        private const string Channel = "surge.hazards";
        private const string RequestChannel = "surge.hazards-request";
        private static float nextSend;

        public static void Initialize()
        {
            Net.RegisterFromHost(Channel, Read);
            Net.RegisterFromClients(RequestChannel, OnRequest, offline: false);
            Net.JoinedHost += Clear;
            Net.LeftHost += Clear;
        }

        public static void Shutdown()
        {
            Net.Unregister(Channel);
            Net.Unregister(RequestChannel);
            Net.JoinedHost -= Clear;
            Net.LeftHost -= Clear;
            Clear();
        }

        private static void Clear() { nextSend = 0f; PoisonCloud.ClearRemote(); }

        // Echo the client's scene handle: custom arenas share a name, but old replies must not survive a reload.
        public static void Tick()
        {
            if (!Net.IsRemoteClient || LevelController.IsLoading || Time.unscaledTime < nextSend) return;
            nextSend = Time.unscaledTime + 0.25f;
            Net.SendToServer(RequestChannel, writer =>
            {
                var scene = SceneManager.GetActiveScene();
                writer.Write(scene.handle);
                writer.Write(scene.name);
            });
        }

        private static void OnRequest(ulong client, BinaryReader reader)
        {
            int scene = reader.ReadInt32();
            string name = reader.ReadString();
            if (LevelController.IsLoading || name != SceneManager.GetActiveScene().name) return;
            Net.SendTo(client, Channel, writer => { writer.Write(scene); Write(writer); });
        }

        private static void Write(BinaryWriter writer)
        {
            writer.Write(SceneManager.GetActiveScene().name);
            int count = 0;
            foreach (var ring in LaserRing.Active) if (ring != null && ring.CanSync) count++;
            writer.Write(count);
            foreach (var ring in LaserRing.Active)
            {
                if (ring == null || !ring.CanSync) continue;
                writer.Write(ring.NetworkId);
                writer.Write(ring.CycleTime);
                writer.Write(ring.IsHeld);
            }
            PoisonCloud.WriteSnapshot(writer);
        }

        private static void Read(BinaryReader reader)
        {
            if (reader.ReadInt32() != SceneManager.GetActiveScene().handle || LevelController.IsLoading) return;
            if (reader.ReadString() != SceneManager.GetActiveScene().name) return;
            int count = reader.ReadInt32();
            if (count < 0 || count > 4096) throw new InvalidDataException("Invalid laser ring count");
            for (int i = 0; i < count; i++)
            {
                ulong id = reader.ReadUInt64();
                float phase = reader.ReadSingle();
                bool held = reader.ReadBoolean();
                var manager = NetworkManager.Singleton;
                if (manager == null || !manager.SpawnManager.SpawnedObjects.TryGetValue(id, out var enemy)) continue;
                var ring = enemy.GetComponentInChildren<LaserRing>();
                if (ring == null) continue;
                ring.Sync(phase);
                if (held) ring.Hold();
            }
            PoisonCloud.ReadSnapshot(reader);
        }
    }
}
