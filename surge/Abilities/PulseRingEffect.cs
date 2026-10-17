using UnityEngine;

namespace SpiderSurge
{
    public class PulseRingEffect : MonoBehaviour
    {
        private LineRenderer lineRenderer;
        private Material material;
        private float currentRadius = 0f;
        private float maxRadius = 100f;
        private float speed;
        private bool inward;
        private float closeTime;
        private float elapsed;
        private Transform follow;
        private int segments = 60;

        public void Setup(float targetRadius)
        {
            this.maxRadius = targetRadius;
            this.speed = Consts.Values.Pulse.RingSpeed;
            CreateLine(Consts.Values.Pulse.RingColor);
        }

        // Starts at the radius and closes on the followed transform, speeding up like Banish's pull
        public void SetupInward(float radius, Color color, float duration, Transform followTarget)
        {
            maxRadius = radius;
            currentRadius = radius;
            inward = true;
            closeTime = duration;
            follow = followTarget;
            CreateLine(color);
        }

        private void CreateLine(Color color)
        {
            lineRenderer = gameObject.AddComponent<LineRenderer>();
            lineRenderer.useWorldSpace = false;
            lineRenderer.loop = true;
            lineRenderer.positionCount = segments;
            lineRenderer.startWidth = Consts.Values.Pulse.RingWidth;
            lineRenderer.endWidth = Consts.Values.Pulse.RingWidth;

            // Use a simple sprite shader that supports color
            material = new Material(Shader.Find("Sprites/Default"));
            lineRenderer.material = material;

            lineRenderer.startColor = color;
            lineRenderer.endColor = color;

            // Initial draw
            DrawRing();
        }

        private void OnDestroy()
        {
            if (material != null)
            {
                Destroy(material);
            }
        }

        private void Update()
        {
            if (follow != null) transform.position = follow.position;

            bool done;
            if (inward)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / closeTime;
                currentRadius = maxRadius * (1f - t * t);
                done = t >= 1f;
            }
            else
            {
                currentRadius += speed * Time.deltaTime;
                done = currentRadius >= maxRadius;
            }

            if (done)
            {
                Destroy(gameObject);
                return;
            }

            DrawRing();
        }

        private void DrawRing()
        {
            if (lineRenderer == null) return;

            float angle = 0f;
            for (int i = 0; i < segments; i++)
            {
                float x = Mathf.Sin(Mathf.Deg2Rad * angle) * currentRadius;
                float y = Mathf.Cos(Mathf.Deg2Rad * angle) * currentRadius;

                // Assuming 2D game on XY plane
                lineRenderer.SetPosition(i, new Vector3(x, y, 0f));

                angle += (360f / segments);
            }
        }
    }
}
