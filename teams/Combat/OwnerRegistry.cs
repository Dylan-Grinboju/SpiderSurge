using SpiderSurge.Common;
using UnityEngine;

namespace SpiderTeams
{
    // The playerID behind the core owner records
    internal static class OwnerRegistry
    {
        public const int Unknown = -1;

        public static int Get(GameObject target) => OfPlayer(Owners.HolderOf(target));

        public static int OfPlayer(PlayerController player)
        {
            return player != null ? player.playerID.Value : Unknown;
        }

        // The live holder decides; once nobody holds it, the player who last equipped it.
        public static int OfWeapon(Weapon weapon) => OfPlayer(Owners.PlayerOf(weapon));

        public static int Resolve(GameObject target) => OfPlayer(Owners.Resolve(target));
    }
}
