using UnityEngine;
using UnityEngine.UI;

namespace SpiderSurge
{
    internal sealed class PlayerChoiceIcon : MaskableGraphic
    {
        private static readonly Vector2[][] Legs =
        {
            new[] { new Vector2(5, 7), new Vector2(16, 17), new Vector2(18, 28), new Vector2(22, 28), new Vector2(20, 15), new Vector2(8, 3) },
            new[] { new Vector2(8, 4), new Vector2(23, 9), new Vector2(28, 19), new Vector2(31, 17), new Vector2(26, 6), new Vector2(10, 0) }
        };

        // Solid geometry keeps the small player markers sharp at every canvas scale.
        public override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect rect = GetPixelAdjustedRect();
            float scale = Mathf.Min(rect.width, rect.height) / 64f;
            Vector2 center = rect.center;
            foreach (var leg in Legs)
                for (int x = -1; x <= 1; x += 2)
                    for (int y = -1; y <= 1; y += 2)
                    {
                        int start = mesh.currentVertCount;
                        foreach (var point in leg)
                            mesh.AddVert(center + new Vector2(point.x * x, point.y * y) * scale, color, Vector2.zero);
                        mesh.AddTriangle(start, start + 1, start + 4);
                        mesh.AddTriangle(start, start + 4, start + 5);
                        mesh.AddTriangle(start + 1, start + 2, start + 3);
                        mesh.AddTriangle(start + 1, start + 3, start + 4);
                    }
            int body = mesh.currentVertCount;
            mesh.AddVert(center, color, Vector2.zero);
            const int segments = 32;
            for (int i = 0; i < segments; i++)
            {
                float angle = i * Mathf.PI * 2 / segments;
                mesh.AddVert(center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * (10f * scale), color, Vector2.zero);
            }
            for (int i = 0; i < segments; i++)
                mesh.AddTriangle(body, body + 1 + i, body + 1 + (i + 1) % segments);
        }
    }
}
