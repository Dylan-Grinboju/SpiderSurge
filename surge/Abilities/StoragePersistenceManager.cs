using System.Collections.Generic;

namespace SpiderSurge
{
    public static class StoragePersistenceManager
    {
        public struct SavedWeaponData
        {
            public SerializationWeaponName WeaponName;
            public float Ammo;
            public bool IsUltimateSlot;
        }

        private static readonly Dictionary<int, List<SavedWeaponData>> _storedWeapons = new Dictionary<int, List<SavedWeaponData>>();

        public static void SaveStoredWeapons(int playerId, List<SavedWeaponData> weapons)
        {
            if (weapons == null || weapons.Count == 0)
            {
                if (_storedWeapons.ContainsKey(playerId))
                {
                    _storedWeapons.Remove(playerId);
                }
                return;
            }

            // Clone list to ensure safety
            _storedWeapons[playerId] = new List<SavedWeaponData>(weapons);

        }

        public static List<SavedWeaponData> GetStoredWeapons(int playerId)
        {
            if (_storedWeapons.TryGetValue(playerId, out var list))
            {
                return list;
            }
            return null;
        }

        public static void ClearStoredWeapons(int playerId)
        {
            if (_storedWeapons.ContainsKey(playerId))
            {
                _storedWeapons.Remove(playerId);
            }
        }

        public static void ClearAllStoredWeapons()
        {
            _storedWeapons.Clear();
        }
    }
}
