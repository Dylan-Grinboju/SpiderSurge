using System;

namespace SpiderSurge
{
    public sealed class HoldConfirmation
    {
        public const float Duration = 2f;
        private bool _armed;
        private float? _started;
        public float Progress { get; private set; }

        public bool Tick(float now, bool held, bool released, bool available)
        {
            if (!available)
            {
                Reset();
                return false;
            }
            if (!held)
            {
                _armed = released;
                _started = null;
                Progress = 0;
                return false;
            }
            if (!_armed) return false;
            if (!_started.HasValue) _started = now;
            Progress = Math.Max(0f, Math.Min(1f, (now - _started.Value) / Duration));
            if (Progress < 1f) return false;
            Reset();
            return true;
        }

        public void Reset()
        {
            _armed = false;
            _started = null;
            Progress = 0;
        }
    }
}
