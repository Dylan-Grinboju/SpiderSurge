using SpiderSurge.Common;
using UnityEngine;

namespace SpiderSurge.Vfx
{
    internal sealed class ReducedParticles : MonoBehaviour
    {
        private ParticleSystem system;
        private float rate;
        private bool trails;
        private ParticleSystem.Burst[] bursts;
        private ParticleSystem.Burst[] adjusted;

        public void Initialize(ParticleSystem value)
        {
            system = value;
            rate = system.emission.rateOverTimeMultiplier;
            trails = system.trails.enabled;
            bursts = new ParticleSystem.Burst[system.emission.burstCount];
            adjusted = new ParticleSystem.Burst[bursts.Length];
            system.emission.GetBursts(bursts);
            Settings.Changed += Changed;
            Apply();
        }

        private void Changed(string key)
        {
            if (Settings.Affects(key, ModConfig.ReducedEffectsKey)) Apply();
        }

        private void Apply()
        {
            if (system == null) return;
            float factor = ModConfig.ReducedEffects ? 0.25f : 1f;
            var emission = system.emission;
            emission.rateOverTimeMultiplier = rate * factor;
            for (int i = 0; i < bursts.Length; i++)
            {
                adjusted[i] = bursts[i];
                adjusted[i].count = new ParticleSystem.MinMaxCurve(Mathf.Max(1f, Mathf.Ceil(bursts[i].count.constant * factor)));
            }
            emission.SetBursts(adjusted);
            var trail = system.trails;
            trail.enabled = trails && !ModConfig.ReducedEffects;
            if (ModConfig.ReducedEffects && system.particleCount > 0) system.Clear();
        }

        private void OnDestroy() => Settings.Changed -= Changed;
    }
}
