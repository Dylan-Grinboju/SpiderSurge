using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SpiderSurge
{
    public sealed class ChaosIcon : MaskableGraphic
    {
        public static ChaosIcon Add(Transform parent, Vector2 position, float size, Color tint, Vector2? anchor = null)
        {
            var icon = new GameObject("Chaos spark", typeof(RectTransform), typeof(CanvasRenderer), typeof(ChaosIcon)).GetComponent<ChaosIcon>();
            icon.transform.SetParent(parent, false);
            icon.raycastTarget = false;
            icon.color = tint;
            icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = anchor ?? new Vector2(.5f, .5f);
            icon.rectTransform.sizeDelta = new Vector2(size, size);
            icon.rectTransform.anchoredPosition = position;
            return icon;
        }

        // Centers the icon and a centered text as one group; call again whenever the text changes
        public static void CenterBefore(ChaosIcon icon, TMP_Text text, float gap = 6f)
        {
            float size = icon.rectTransform.sizeDelta.x;
            text.margin = Vector4.zero;
            float width = text.GetPreferredValues(text.text, float.PositiveInfinity, float.PositiveInfinity).x;
            text.margin = new Vector4(size + gap, 0, 0, 0);
            icon.rectTransform.anchorMin = icon.rectTransform.anchorMax = new Vector2(.5f, .5f);
            icon.rectTransform.anchoredPosition = new Vector2(-width / 2f - gap / 2f, 0);
        }

        public override void OnPopulateMesh(VertexHelper mesh)
        {
            mesh.Clear();
            Rect rect = GetPixelAdjustedRect();
            float scale = Mathf.Min(rect.width, rect.height) * .5f;
            Vector2 center = rect.center;
            for (int segment = 0; segment < 3; segment++)
                for (int step = 0; step < 12; step++)
                {
                    float a = (segment * 120 + step * 7 + 12) * Mathf.Deg2Rad;
                    float b = a + 7 * Mathf.Deg2Rad;
                    Vector2 outerA = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                    Vector2 outerB = new Vector2(Mathf.Cos(b), Mathf.Sin(b));
                    int first = mesh.currentVertCount;
                    mesh.AddVert(center + outerA * scale, color, Vector2.zero);
                    mesh.AddVert(center + outerB * scale, color, Vector2.zero);
                    mesh.AddVert(center + outerB * scale * .78f, color, Vector2.zero);
                    mesh.AddVert(center + outerA * scale * .78f, color, Vector2.zero);
                    mesh.AddTriangle(first, first + 1, first + 2);
                    mesh.AddTriangle(first, first + 2, first + 3);
                }
            Vector2[] spark = { new Vector2(.18f, .7f), new Vector2(-.43f, -.1f), new Vector2(-.04f, -.1f), new Vector2(-.18f, -.7f), new Vector2(.43f, .13f), new Vector2(.05f, .13f) };
            int offset = mesh.currentVertCount;
            foreach (Vector2 point in spark) mesh.AddVert(center + point * scale, color, Vector2.zero);
            mesh.AddTriangle(offset, offset + 1, offset + 2);
            mesh.AddTriangle(offset, offset + 2, offset + 5);
            mesh.AddTriangle(offset + 2, offset + 3, offset + 4);
            mesh.AddTriangle(offset + 2, offset + 4, offset + 5);
        }
    }
}
