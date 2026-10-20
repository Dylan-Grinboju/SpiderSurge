using UnityEngine;

namespace SpiderSurge.Vfx
{
    // A neon ring: a bright core over a wide soft halo, expanding at a set speed or closing on a target
    public class Shockwave : MonoBehaviour
    {
        private const int Segments = 72;
        private const float HaloWidth = 3f;
        private const float HaloAlpha = 0.5f;
        // Over 1 so the core blooms the way the game's bright sprites do
        private const float CoreBrightness = 1.8f;

        private LineRenderer core;
        private LineRenderer halo;
        private readonly Vector3[] points = new Vector3[Segments];
        private Color color;
        private float maxRadius;
        private float radius;
        private float speed;
        private float width;
        private float linger;
        private float lingered;
        private float duration;
        private float elapsed;
        private bool inward;
        private Transform follow;
        private System.Action onComplete;

        public static Shockwave Expand(Vector3 position, float radius, Color color, float speed, float width = 2f, float linger = 0.15f)
        {
            Shockwave wave = Create(position, color, width);
            wave.maxRadius = radius;
            wave.speed = speed;
            wave.linger = linger;
            wave.Draw(1f, 1f);
            return wave;
        }

        // Starts at the radius and closes on the followed transform, speeding up like Banish's pull
        public static Shockwave Collapse(Transform follow, float radius, Color color, float duration, float width = 2f, System.Action onComplete = null)
        {
            Shockwave wave = Create(follow.position, color, width);
            wave.maxRadius = radius;
            wave.radius = radius;
            wave.inward = true;
            wave.duration = Mathf.Max(0.01f, duration);
            wave.follow = follow;
            wave.onComplete = onComplete;
            wave.Draw(0.5f, 1f);
            return wave;
        }

        private static Shockwave Create(Vector3 position, Color color, float width)
        {
            GameObject holder = new GameObject("Shockwave");
            holder.transform.position = position;
            Shockwave wave = holder.AddComponent<Shockwave>();
            wave.color = color;
            wave.width = width;
            wave.halo = wave.MakeLine("Halo", SurgeVfx.Order);
            wave.core = wave.MakeLine("Core", SurgeVfx.Order + 1);
            return wave;
        }

        private LineRenderer MakeLine(string name, int order)
        {
            GameObject holder = new GameObject(name);
            holder.transform.SetParent(transform, false);
            LineRenderer line = holder.AddComponent<LineRenderer>();
            line.useWorldSpace = false;
            line.loop = true;
            line.positionCount = Segments;
            line.textureMode = LineTextureMode.Stretch;
            line.sharedMaterial = SurgeVfx.SoftLine;
            line.sortingLayerName = SurgeVfx.SortingLayer;
            line.sortingOrder = order;
            line.numCapVertices = 0;
            line.numCornerVertices = 0;
            return line;
        }

        private void Update()
        {
            if (follow != null) transform.position = follow.position;

            float alpha;
            float widthScale;
            if (inward)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                if (t >= 1f)
                {
                    onComplete?.Invoke();
                    Destroy(gameObject);
                    return;
                }
                radius = maxRadius * (1f - t * t);
                alpha = 0.5f + 0.5f * t;
                widthScale = 1f + t;
            }
            else
            {
                radius += speed * Time.deltaTime;
                if (radius >= maxRadius)
                {
                    radius = maxRadius;
                    lingered += Time.deltaTime;
                    if (lingered >= linger)
                    {
                        Destroy(gameObject);
                        return;
                    }
                    alpha = 0.4f * (1f - lingered / linger);
                    widthScale = 0.5f;
                }
                else
                {
                    float t = radius / maxRadius;
                    alpha = t < 0.6f ? 1f : Mathf.Lerp(1f, 0.4f, (t - 0.6f) / 0.4f);
                    widthScale = Mathf.Lerp(1f, 0.5f, t);
                }
            }

            Draw(alpha, widthScale);
        }

        private void Draw(float alpha, float widthScale)
        {
            float step = Mathf.PI * 2f / Segments;
            for (int i = 0; i < Segments; i++)
            {
                float angle = i * step;
                points[i] = new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f);
            }
            core.SetPositions(points);
            halo.SetPositions(points);

            core.widthMultiplier = width * widthScale;
            halo.widthMultiplier = width * widthScale * HaloWidth;
            Color coreColor = SurgeVfx.WithAlpha(Color.Lerp(color, Color.white, 0.5f) * CoreBrightness, color.a * alpha);
            Color haloColor = SurgeVfx.WithAlpha(color, color.a * alpha * HaloAlpha);
            core.startColor = coreColor;
            core.endColor = coreColor;
            halo.startColor = haloColor;
            halo.endColor = haloColor;
        }
    }
}
