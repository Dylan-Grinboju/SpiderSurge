using System.Collections.Generic;

namespace SpiderSurge
{
    public sealed class CleanHandsProgress
    {
        private readonly HashSet<object> harms = new HashSet<object>();
        public bool Failed => harms.Count > 0;

        public object RecordHarm()
        {
            var harm = new object();
            harms.Add(harm);
            return harm;
        }

        public bool Revert(object harm) => harm != null && harms.Remove(harm);
        public void Reset() => harms.Clear();
    }
}
