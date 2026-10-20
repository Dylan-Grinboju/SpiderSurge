using UnityEngine;

namespace SpiderSurge.Vfx
{
    // Frost drifting off an enemy held by Freeze Time; it isn't a child of the enemy, so the freeze doesn't stop its own particles
    public class FrostAura : MonoBehaviour
    {
        private ParticleSystem motes;

        public static FrostAura Attach(Transform target, float radius)
        {
            GameObject holder = new GameObject("FrostAura");
            holder.transform.position = target.position;
            FrostAura aura = holder.AddComponent<FrostAura>();
            Follow follow = holder.AddComponent<Follow>();
            follow.Target = target;
            Color ice = Consts.Values.Freeze.TintColor;
            aura.motes = SurgeVfx.Stream(holder.transform, Vector3.zero, ice, Color.white, 16f + 2f * radius, 2f, 6f, 0.8f, 1.5f, 1.3f, radius, gravity: -0.05f, radiusThickness: 0.5f);
            return aura;
        }

        public void Release(bool thawed)
        {
            SurgeVfx.Fade(motes);
            if (thawed) FreezeVfx.Thaw(transform.position);
            Destroy(gameObject);
        }
    }
}
