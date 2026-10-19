using System.Collections.Generic;
using UnityEngine;

namespace SpiderTeams
{
    // Explosions are plain MonoBehaviours with no owner, so the host notes where each owned blast was requested and matches by position.
    internal static class ExplosionOwners
    {
        private struct Entry
        {
            public Vector3 Position;
            public int Owner;
            public float Time;
        }

        private static readonly List<Entry> _records = new List<Entry>(16);
        private static readonly Dictionary<int, int> _resolved = new Dictionary<int, int>();

        public static void Record(Vector3 position, int owner)
        {
            if (owner < 0)
            {
                return;
            }

            Prune(UnityEngine.Time.time);
            _records.Add(new Entry { Position = position, Owner = owner, Time = UnityEngine.Time.time });
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
