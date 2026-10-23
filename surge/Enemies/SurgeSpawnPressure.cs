using SpiderSurge.Common;
using UnityEngine;

namespace SpiderSurge
{
    internal static class SurgeSpawnPressure
    {
        private static int _wave;
        private static float _waitingSince = -1f;

        private static bool Enabled => Net.IsAuthority && SurgeGameModeManager.IsSurgeRunActive;

        internal static void Reset()
        {
            _wave = 0;
            _waitingSince = -1f;
        }

        // Between full slots, admit one extra enemy after a wait that shrinks each wave toward the next milestone
        internal static int CountForCap(int alive)
        {
            if (!Enabled || SurvivalMode.instance == null) return alive;
            int wave = Mathf.Max(1, SurvivalMode.instance.CurrentWave);
            if (_wave != wave)
            {
                Reset();
                _wave = wave;
            }

            alive = Mathf.Max(0, alive - Enemies.SpawnerBrain.CountHatchlingsAlive());
            int threshold = Consts.Values.Enemies.WavesPerCapacityIncrease;
            int capacity = 2 + wave / threshold;
            float fraction = (wave % threshold) / (float)threshold;
            if (alive != capacity || fraction == 0f)
            {
                _waitingSince = -1f;
                return alive;
            }

            if (_waitingSince < 0f) _waitingSince = Time.time;
            float interval = Consts.Values.Enemies.SpawnInterval + ModifierManager.instance.GetModLevel("interference") * 1.5f;
            float wait = interval * (1f - fraction) / fraction;
            if (Time.time - _waitingSince < wait) return alive;
            _waitingSince = -1f;
            return alive - 1;
        }

        internal static float SpawnInterval(float original) => Enabled ? Consts.Values.Enemies.SpawnInterval : original;
        internal static float PollInterval(float original) => Enabled ? Consts.Values.Enemies.CapacityPollInterval : original;
        internal static int BurstCount(int original) => Enabled ? 0 : original;
    }
}
