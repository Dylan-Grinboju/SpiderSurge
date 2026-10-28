using UnityEngine;

namespace SpiderSurge.Vfx
{
    public class FrostAura : MonoBehaviour
    {
        private ParticleSystem motes;

        public static FrostAura Attach(Transform target, float radius)
        {
            return SurgeVfx.Safely(() => SurgeVfx.Create<FrostAura>("FrostAura", target.position, aura =>
            {
                GameObject holder = aura.gameObject;
                Follow follow = holder.AddComponent<Follow>();
                follow.Target = target;
                Color ice = Consts.Values.Freeze.TintColor;
                AccentVfx.Shards(target.position, ice, 10, 12f, radius * 0.6f, 0.22f);
                SurgeVfx.Flash(target.position, ice, 1f, radius + 8f, 0.12f);
                aura.motes = SurgeVfx.Stream(holder.transform, ice, Color.white, 5f + 0.4f * radius, 0f, 0.8f, 0.5f, 0.9f, 0.7f, radius, gravity: -0.05f, radiusThickness: 0.5f);
            }));
        }

        public void Release(bool thawed)
        {
            SurgeVfx.Safely(() => SurgeVfx.Fade(motes));
            if (thawed) FreezeVfx.Thaw(transform.position);
            Destroy(gameObject);
        }
    }
}
