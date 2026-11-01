using UnityEngine;

namespace SpiderSurge.Vfx
{
    // Poison motes circling above a dazed enemy or spider, like the stars of a cartoon daze
    public class DazeVfx : MonoBehaviour
    {
        private const int MoteCount = 3;
        private const float OrbitSpeed = 5f;
        private const float FadeTime = 0.25f;

        private readonly SpriteRenderer[] _motes = new SpriteRenderer[MoteCount];
        private Transform _target;
        private float _radius;
        private float _height;
        private float _moteScale;
        private float _fadeStart = -1f;

        public static DazeVfx Attach(Transform target, float radius)
        {
            return SurgeVfx.Safely(() => SurgeVfx.Create<DazeVfx>("SurgeDaze", target.position, daze =>
            {
                daze._target = target;
                daze._radius = Mathf.Clamp(radius * 0.8f, 4.5f, 9f);
                daze._height = radius + 2f;
                Sprite sprite = SurgeVfx.HardDisc;
                daze._moteScale = Mathf.Clamp(radius * 0.3f, 1.8f, 2.8f) / sprite.bounds.size.x;
                for (int i = 0; i < MoteCount; i++)
                {
                    var mote = new GameObject("Mote").AddComponent<SpriteRenderer>();
                    mote.transform.SetParent(daze.transform, false);
                    mote.sprite = sprite;
                    mote.sortingLayerName = SurgeVfx.SortingLayer;
                    mote.sortingOrder = SurgeVfx.Order;
                    daze._motes[i] = mote;
                }
                daze.Place();
                Color poison = Consts.Values.PoisonDart.Color;
                SurgeVfx.Burst(target.position, poison, Color.white, 14, 10f, 30f, 0.5f, 1f, 0.25f, 0.45f, radius * 0.5f, false);
                SurgeVfx.Flash(target.position, poison, 1f, radius + 10f, 0.15f);
            }));
        }

        public void Release()
        {
            if (_fadeStart < 0f) _fadeStart = Time.time;
        }

        private void LateUpdate()
        {
            if (_target == null || !_target.gameObject.activeInHierarchy || (_fadeStart >= 0f && Time.time - _fadeStart >= FadeTime))
            {
                Destroy(gameObject);
                return;
            }
            Place();
        }

        // The motes ride a flat ring above the head, smaller and dimmer behind it
        private void Place()
        {
            transform.position = _target.position + Vector3.up * _height;
            float fade = _fadeStart >= 0f ? 1f - (Time.time - _fadeStart) / FadeTime : 1f;
            float spin = Time.time * OrbitSpeed;
            for (int i = 0; i < MoteCount; i++)
            {
                float angle = spin + i * Mathf.PI * 2f / MoteCount;
                float depth = (Mathf.Sin(angle) + 1f) * 0.5f;
                SpriteRenderer mote = _motes[i];
                mote.transform.localPosition = new Vector3(Mathf.Cos(angle) * _radius, Mathf.Sin(angle) * _radius * 0.3f, 0f);
                mote.transform.localScale = Vector3.one * _moteScale * Mathf.Lerp(1f, 0.6f, depth);
                Color color = Color.Lerp(Consts.Values.PoisonDart.Color, Color.white, 0.35f * (1f - depth));
                color.a = Mathf.Lerp(1f, 0.7f, depth) * Mathf.Clamp01(fade);
                mote.color = color;
            }
        }
    }
}
