using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace SpiderSurge.Common
{
    // Players are identified by PlayerController.playerID, the host's slot number, which is the same on every machine
    public static class Players
    {
        public static List<PlayerController> All
        {
            get
            {
                var lobby = LobbyController.instance;
                return lobby != null ? lobby.spawnedPlayers : null;
            }
        }

        public static PlayerController ById(int playerId)
        {
            var players = All;
            if (players == null || playerId < 0)
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

        public static PlayerController Of(Component component)
        {
            return component != null ? component.GetComponentInParent<PlayerController>() : null;
        }

        public static PlayerController Of(GameObject gameObject)
        {
            return gameObject != null ? gameObject.GetComponentInParent<PlayerController>() : null;
        }

        public static int IdOf(Component component)
        {
            var player = Of(component);
            return player != null ? player.playerID.Value : -1;
        }

        public static int IdOf(GameObject gameObject)
        {
            var player = Of(gameObject);
            return player != null ? player.playerID.Value : -1;
        }

        public static bool IsLocal(PlayerController player)
        {
            return player != null && (player.IsOwner || !Net.Available);
        }

        public static bool IsLocal(int playerId) => IsLocal(ById(playerId));


        public static bool OwnedBy(int playerId, ulong clientId)
        {
            var player = ById(playerId);
            return player != null && player.OwnerClientId == clientId;
        }

        // The host acts for every player; a client only for its own
        public static bool MayActFor(int playerId, ulong sender) => sender == Net.ServerId || OwnedBy(playerId, sender);

        public static SpiderHealthSystem HealthOf(int playerId)
        {
            var player = ById(playerId);
            return player != null ? player.spiderHealthSystem : null;
        }

        public static GameObject SpiderOf(int playerId)
        {
            var health = HealthOf(playerId);
            if (health == null)
            {
                return null;
            }
            return health.rootObject != null ? health.rootObject : health.gameObject;
        }

        public static string NameOf(PlayerController player)
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

        public static string NameOf(int playerId) => NameOf(ById(playerId));
    }
}
