using System.Collections.Generic;
using HarmonyLib;
using SpiderSurge.Common;
using Unity.Netcode;
using UnityEngine;
using Logger = Silk.Logger;

namespace SpiderSurge
{
    // Each custom clone (enemy or weapon) gets its own hash and prefab handler, so clients build the clone instead of the prefab it copies
    public static class CustomPrefabNet
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

        // Netcode keeps this internal and Mono enforces that at run time, so it is reached through reflection
        private static readonly AccessTools.FieldRef<NetworkObject, uint> GlobalObjectIdHash = AccessTools.FieldRefAccess<NetworkObject, uint>("GlobalObjectIdHash");

        private static readonly Dictionary<GameObject, uint> _entries = new Dictionary<GameObject, uint>();

        public static void RegisterEnemy(GameObject prefab, string name) => Register(prefab, "SpiderSurge.Enemy:" + name, name);

        public static void RegisterWeapon(GameObject prefab, string name) => Register(prefab, "SpiderSurge.Weapon:" + name, name);

        private static void Register(GameObject prefab, string key, string name)
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
            uint hash = Net.Hash(key);
            GlobalObjectIdHash(networkObject) = hash;
            _entries[prefab] = hash;
            AddHandler(prefab, hash);
        }

        public static void RegisterHandlers()
        {
            foreach (var entry in _entries)
            {
                AddHandler(entry.Key, entry.Value);
            }
        }

        private static void AddHandler(GameObject prefab, uint hash)
        {
            var manager = NetworkManager.Singleton;
            if (manager == null || manager.PrefabHandler == null)
            {
                return;
            }
            manager.PrefabHandler.AddHandler(hash, new Handler(prefab));
        }
    }
}
