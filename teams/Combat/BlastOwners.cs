using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

namespace SpiderTeams
{
    // Who set off each explosion: the explode ClientRpcs name it, even nobody, as they create it; the rail's impact splash is matched by where it landed
    internal static class BlastOwners
    {
        private struct Entry
        {
            public int Explosion;
            public Vector3 Position;
            public int Owner;
            public float Time;
        }

        private static readonly List<Entry> _records = new List<Entry>(16);
        private static readonly Dictionary<int, int> _resolved = new Dictionary<int, int>();

        // Recorded on the host while team mode is on, the only time an attack reads it
        public static void OnExplosionCreated(Explosion explosion, NetworkBehaviour source)
        {
            try
            {
                if (!TeamManager.HostTeamMode)
                {
                    return;
                }

                int owner = source is BasicProjectile ? OwnerRegistry.Get(source.gameObject) : OwnerRegistry.OfWeapon(source as Weapon);
                Add(new Entry { Explosion = explosion.GetInstanceID(), Owner = owner });
            }
            catch (System.Exception ex)
            {
                PatchScopes.Report("explosion owner tracking", ex);
            }
        }

        public static void RecordRail(Vector3 position, int owner)
        {
            if (owner >= 0)
            {
                Add(new Entry { Position = position, Owner = owner });
            }
        }

        public static int Resolve(Explosion explosion)
        {
            if (explosion == null)
            {
                return OwnerRegistry.Unknown;
            }

            int key = explosion.GetInstanceID();
            int owner;
            if (_resolved.TryGetValue(key, out owner))
            {
                return owner;
            }

            owner = OwnerRegistry.Unknown;
            Vector3 position = explosion.transform.position;
            float now = UnityEngine.Time.time;
            float best = Consts.Timing.ExplosionMatchDistance * Consts.Timing.ExplosionMatchDistance;
            for (int i = 0; i < _records.Count; i++)
            {
                Entry record = _records[i];
                if (now - record.Time > Consts.Timing.ExplosionMatchWindow)
                {
                    continue;
                }

                if (record.Explosion != 0)
                {
                    if (record.Explosion == key)
                    {
                        owner = record.Owner;
                        break;
                    }
                    continue;
                }

                float distance = (record.Position - position).sqrMagnitude;
                if (distance <= best)
                {
                    best = distance;
                    owner = record.Owner;
                }
            }

            _resolved[key] = owner;
            return owner;
        }

        public static void Clear()
        {
            _records.Clear();
            _resolved.Clear();
        }

        private static void Add(Entry entry)
        {
            float now = UnityEngine.Time.time;
            Prune(now);
            entry.Time = now;
            _records.Add(entry);
        }

        private static void Prune(float now)
        {
            int keep = 0;
            for (int i = 0; i < _records.Count; i++)
            {
                if (now - _records[i].Time <= Consts.Timing.ExplosionMatchWindow)
                {
                    _records[keep++] = _records[i];
                }
            }
            _records.RemoveRange(keep, _records.Count - keep);
        }
    }
}
