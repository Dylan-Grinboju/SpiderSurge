using UnityEngine;

namespace SpiderSurge;

public class PulseRingEffect : MonoBehaviour
{
    private LineRenderer lineRenderer;
    private float currentRadius = 0f;
    private float maxRadius = 100f;
    private float speed;
    private int segments = 60;

    public void Setup(float targetRadius)
    {
        this.maxRadius = targetRadius;
        this.speed = Consts.Values.Pulse.RingSpeed;

        lineRenderer = gameObject.AddComponent<LineRenderer>();
        lineRenderer.useWorldSpace = false;
        lineRenderer.loop = true;
        lineRenderer.positionCount = segments;
        lineRenderer.startWidth = Consts.Values.Pulse.RingWidth;
        lineRenderer.endWidth = Consts.Values.Pulse.RingWidth;

        // Use a simple sprite shader that supports color
        Material material = new(Shader.Find("Sprites/Default"));
        lineRenderer.material = material;

        lineRenderer.startColor = Consts.Values.Pulse.RingColor;
        lineRenderer.endColor = Consts.Values.Pulse.RingColor;

        // Initial draw
        DrawRing();
    }

    private void Update()
    {
        currentRadius += speed * Time.deltaTime;

        if (currentRadius >= maxRadius)
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
