using UnityEngine;

namespace SpiderSurge.Vfx
{
    public class Shockwave : MonoBehaviour
    {
        private const int Segments = 72;
        private const float HaloWidth = 3f;
        private const float HaloAlpha = 0.5f;
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
        private bool holding;
        private Transform follow;
        private System.Action<Vector3> onComplete;

        public static Shockwave Expand(Vector3 position, float radius, Color color, float speed, float width = 2f, float linger = 0.15f)
        {
            Shockwave wave = Create(position, color, width);
            wave.maxRadius = radius;
            wave.speed = speed;
            wave.linger = linger;
            wave.Draw(1f, 1f);
            return wave;
        }

        // Starts at the radius and closes on the position, or on a followed transform, speeding up like the Black Hole's pull; onComplete gets where it closed
        public static Shockwave Collapse(Vector3 position, float radius, Color color, float duration, float width = 2f, Transform follow = null, System.Action<Vector3> onComplete = null)
        {
            Shockwave wave = Create(position, color, width);
            wave.maxRadius = radius;
            wave.radius = radius;
            wave.inward = true;
            wave.duration = Mathf.Max(0.01f, duration);
            wave.follow = follow;
            wave.onComplete = onComplete;
            wave.Draw(0.5f, 1f);
            return wave;
        }

        // Stays at the radius around a followed transform, breathing, until Close
        public static Shockwave Hold(Vector3 position, float radius, Color color, float width, Transform follow)
        {
            Shockwave wave = Create(position, color, width);
            wave.maxRadius = radius;
            wave.radius = radius;
            wave.holding = true;
            wave.follow = follow;
            wave.Draw(0f, 1f);
            return wave;
        }

        public void Close()
        {
            Destroy(gameObject);
        }

        private static Shockwave Create(Vector3 position, Color color, float width)
        {
            return SurgeVfx.Create<Shockwave>("Shockwave", position, wave =>
            {
                wave.color = color;
                wave.width = width;
                wave.halo = wave.MakeLine("Halo", SurgeVfx.Order);
                wave.core = wave.MakeLine("Core", SurgeVfx.Order + 1);
            });
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
            if (holding)
            {
                elapsed += Time.deltaTime;
                float breath = ModConfig.ReducedEffects ? 1f : 0.75f + 0.25f * Mathf.Sin(elapsed * 8f);
                alpha = Mathf.Min(1f, elapsed / 0.15f) * breath;
                widthScale = 1f;
            }
            else if (inward)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                if (t >= 1f)
                {
                    Destroy(gameObject);
                    if (onComplete != null) SurgeVfx.Safely(() => onComplete(transform.position));
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
            Color coreColor = SurgeVfx.SetAlpha(Color.Lerp(color, Color.white, 0.5f) * CoreBrightness, color.a * alpha);
            Color haloColor = SurgeVfx.SetAlpha(color, color.a * alpha * HaloAlpha);
            core.startColor = coreColor;
            core.endColor = coreColor;
            halo.startColor = haloColor;
            halo.endColor = haloColor;
        }
    }
}
