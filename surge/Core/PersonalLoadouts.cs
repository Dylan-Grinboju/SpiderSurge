using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SpiderSurge
{
    internal sealed class PersonalLoadouts
    {
        private readonly Dictionary<int, string> abilities = new Dictionary<int, string>();
        private readonly Dictionary<int, string> ultimates = new Dictionary<int, string>();

        public string Get(int player, bool ultimate) => (ultimate ? ultimates : abilities).TryGetValue(player, out var key) ? key : null;
        public bool Contains(string key) => abilities.ContainsValue(key) || ultimates.ContainsValue(key);
        public bool HasUltimate => ultimates.Count > 0;
        public bool Any => abilities.Count > 0 || ultimates.Count > 0;
        public void Set(int player, string key, bool ultimate) => (ultimate ? ultimates : abilities)[player] = key;
        public void Remove(int player) { abilities.Remove(player); ultimates.Remove(player); }
        public void Clear() { abilities.Clear(); ultimates.Clear(); }

        public void Write(BinaryWriter writer)
        {
            Write(writer, abilities);
            Write(writer, ultimates);
        }

        private static void Write(BinaryWriter writer, Dictionary<int, string> values)
        {
            writer.Write(values.Count);
            foreach (var pair in values.OrderBy(pair => pair.Key)) { writer.Write(pair.Key); writer.Write(pair.Value); }
        }

        public void Read(BinaryReader reader)
        {
            Read(reader, abilities);
            Read(reader, ultimates);
        }

        private static void Read(BinaryReader reader, Dictionary<int, string> values)
        {
            values.Clear();
            int count = reader.ReadInt32();
            if (count < 0 || count > 1024) throw new InvalidDataException("Invalid loadout count");
            for (int i = 0; i < count; i++) values.Add(reader.ReadInt32(), reader.ReadString());
        }
    }

    internal sealed class PersonalChoiceRound
    {
        internal sealed class Pick
        {
            public int Index;
            public bool Ready;
            public int Revision;
        }

        public int Epoch { get; private set; }
        public bool Ultimate { get; private set; }
        public string[] Offers { get; private set; } = new string[0];
        public Dictionary<int, Pick> Picks { get; } = new Dictionary<int, Pick>();
        public bool Open => Offers.Length > 0;
        public bool Complete => Open && Picks.Count > 0 && Picks.Values.All(pick => pick.Ready);

        public void Begin(IEnumerable<string> offers, IEnumerable<int> players, bool ultimate)
        {
            Clear();
            Epoch++;
            Ultimate = ultimate;
            Offers = offers.Distinct().ToArray();
            Reconcile(players);
        }

        public bool Reconcile(IEnumerable<int> players)
        {
            var current = new HashSet<int>(players);
            bool changed = false;
            foreach (int player in Picks.Keys.Where(player => !current.Contains(player)).ToArray()) { Picks.Remove(player); changed = true; }
            foreach (int player in current)
                if (!Picks.ContainsKey(player)) { Picks[player] = new Pick(); changed = true; }
            return changed;
        }

        public bool Choose(int epoch, int player, int index, bool ready, int revision = 0)
        {
            if (!Open || epoch != Epoch || index < 0 || index >= Offers.Length || !Picks.TryGetValue(player, out var pick)) return false;
            if (revision > 0 && revision <= pick.Revision) return false;
            if (pick.Index == index && pick.Ready == ready && revision == 0) return false;
            pick.Revision = revision > 0 ? revision : pick.Revision + 1;
            pick.Index = index;
            pick.Ready = ready;
            return true;
        }

        public void InvalidateRequests() => Epoch++;

        public void Clear() { Offers = new string[0]; Picks.Clear(); }

        public void Write(BinaryWriter writer)
        {
            writer.Write(Epoch);
            writer.Write(Ultimate);
            writer.Write(Offers.Length);
            foreach (string key in Offers) writer.Write(key);
            writer.Write(Picks.Count);
            foreach (var pair in Picks.OrderBy(pair => pair.Key)) { writer.Write(pair.Key); writer.Write(pair.Value.Index); writer.Write(pair.Value.Ready); writer.Write(pair.Value.Revision); }
        }

        public void Read(BinaryReader reader)
        {
            Epoch = reader.ReadInt32();
            Ultimate = reader.ReadBoolean();
            int count = reader.ReadInt32();
            if (count < 0 || count > 32) throw new InvalidDataException("Invalid offer count");
            Offers = new string[count];
            for (int i = 0; i < count; i++) Offers[i] = reader.ReadString();
            Picks.Clear();
            count = reader.ReadInt32();
            if (count < 0 || count > 1024) throw new InvalidDataException("Invalid chooser count");
            for (int i = 0; i < count; i++)
            {
                int player = reader.ReadInt32();
                int index = reader.ReadInt32();
                bool ready = reader.ReadBoolean();
                int revision = reader.ReadInt32();
                if (index < 0 || index >= Offers.Length) throw new InvalidDataException("Invalid choice index");
                Picks.Add(player, new Pick { Index = index, Ready = ready, Revision = revision });
            }
        }
    }
}
