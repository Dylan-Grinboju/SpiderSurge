using System.Collections.Generic;
using HarmonyLib;
using SpiderSurge.Common;
using Unity.Netcode;
using UnityEngine;
using Logger = Silk.Logger;

namespace SpiderSurge.Enemies
{
    // Each custom enemy clone gets its own hash and prefab handler; clients without the mod are sent the original's hash
    public static class CustomEnemyNet
    {
        private sealed class Handler : INetworkPrefabInstanceHandler
        {
            private readonly GameObject _prefab;

            public Handler(GameObject prefab)
            {
                _prefab = prefab;
            }

            public NetworkObject Instantiate(ulong ownerClientId, Vector3 position, Quaternion rotation)
            {
                GameObject instance = Object.Instantiate(_prefab, position, rotation);
                instance.SetActive(true);
                return instance.GetComponent<NetworkObject>();
            }

            public void Destroy(NetworkObject networkObject)
            {
                if (networkObject != null)
                {
                    Object.Destroy(networkObject.gameObject);
                }
            }
        }

        private sealed class Entry
        {
            public GameObject Prefab;
            public uint CustomHash;
            public uint OriginalHash;
        }

        private static readonly Dictionary<GameObject, Entry> _entries = new Dictionary<GameObject, Entry>();
        private static readonly Dictionary<uint, Entry> _byHash = new Dictionary<uint, Entry>();

        public static uint HashFor(string name) => Net.Hash("SpiderSurge.Enemy:" + name);

        public static void Register(GameObject prefab, string name)
        {
            if (prefab == null || _entries.ContainsKey(prefab))
            {
                return;
            }
            var networkObject = prefab.GetComponent<NetworkObject>();
            if (networkObject == null)
            {
                Logger.LogWarning($"[SpiderSurge] {name} has no NetworkObject, clients will not see it");
                return;
            }
            uint originalHash = networkObject.GlobalObjectIdHash;
            if (_byHash.TryGetValue(originalHash, out var parent))
            {
                originalHash = parent.OriginalHash;
            }
            var entry = new Entry { Prefab = prefab, OriginalHash = originalHash, CustomHash = HashFor(name) };
            networkObject.GlobalObjectIdHash = entry.CustomHash;
            _entries[prefab] = entry;
            _byHash[entry.CustomHash] = entry;
            AddHandler(entry);
        }

        public static void RegisterHandlers()
        {
            foreach (var entry in _entries.Values)
            {
                AddHandler(entry);
            }
        }

        private static void AddHandler(Entry entry)
        {
            var manager = NetworkManager.Singleton;
            if (manager == null || manager.PrefabHandler == null)
            {
                return;
            }
            if (!manager.PrefabHandler.ContainsHandler(entry.CustomHash))
            {
                manager.PrefabHandler.AddHandler(entry.CustomHash, new Handler(entry.Prefab));
            }
        }

        public static uint HashForClient(uint hash, ulong targetClientId)
        {
            if (!_byHash.TryGetValue(hash, out var entry) || Net.ClientRunsMod(targetClientId))
            {
                return hash;
            }
            return entry.OriginalHash;
        }
    }
}
