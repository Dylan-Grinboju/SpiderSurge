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
                aura.motes = SurgeVfx.Stream(holder.transform, Vector3.zero, ice, Color.white, 16f + 2f * radius, 2f, 6f, 0.8f, 1.5f, 1.3f, radius, gravity: -0.05f, radiusThickness: 0.5f);
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
