using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace SpiderTeams
{
    // Maps weapons and spawned objects to the playerID that owns them, keyed by instance id so recycled network ids never collide.
    internal static class OwnerRegistry
    {
        public const int Unknown = -1;

        private static readonly Dictionary<int, int> _owners = new Dictionary<int, int>();

        public static void Set(GameObject target, int playerId)
        {
            if (target == null)
            {
                return;
            }

            if (playerId < 0)
            {
                _owners.Remove(target.GetInstanceID());
            }
            else
            {
                _owners[target.GetInstanceID()] = playerId;
            }
        }

        public static void Remove(GameObject target)
        {
            if (target != null)
            {
                _owners.Remove(target.GetInstanceID());
            }
        }

        public static int Get(GameObject target)
        {
            int playerId;
            return target != null && _owners.TryGetValue(target.GetInstanceID(), out playerId) ? playerId : Unknown;
        }

        public static void Clear()
        {
            _owners.Clear();
        }

        public static int OfPlayer(PlayerController player)
        {
            return player != null ? player.playerID.Value : Unknown;
        }

        public static int OfManager(WeaponManager manager)
        {
            if (manager == null)
            {
                return Unknown;
            }

            var player = manager.GetComponentInParent<PlayerController>();
            if (player == null && manager.healthSystem != null)
            {
                player = manager.healthSystem.GetComponentInParent<PlayerController>();
            }
            return OfPlayer(player);
        }

        // The live holder decides; once nobody holds it, the player who last equipped it.
        public static int OfWeapon(Weapon weapon)
        {
            if (weapon == null)
            {
                return Unknown;
            }

            if (weapon.owner != null)
            {
                return OfManager(weapon.owner);
            }
            return Get(weapon.gameObject);
        }

        public static int Resolve(GameObject target)
        {
            if (target == null)
            {
                return Unknown;
            }

            int playerId = Get(target);
            if (playerId != Unknown)
            {
                return playerId;
            }

            var player = target.GetComponentInParent<PlayerController>();
            if (player != null)
            {
                return player.playerID.Value;
            }

            var weapon = target.GetComponentInParent<Weapon>();
            if (weapon != null)
            {
                return OfWeapon(weapon);
            }

            var networkObject = target.GetComponentInParent<NetworkObject>();
            if (networkObject != null && networkObject.gameObject != target)
            {
                return Get(networkObject.gameObject);
            }
            return Unknown;
        }
    }
}
