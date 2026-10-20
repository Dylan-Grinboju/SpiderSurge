using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace SpiderSurge.Common
{
    internal static class EnemyIllustration
    {
        private static bool Visible(SpriteRenderer renderer, Transform root)
        {
            if (!renderer.enabled || renderer.sprite == null || renderer.color.a <= 0f) return false;
            for (var node = renderer.transform; node != null && node != root; node = node.parent)
                if (!node.gameObject.activeSelf) return false;
            return true;
        }

        private static Bounds BoundsOf(SpriteRenderer renderer)
        {
            var local = renderer.sprite.bounds;
            var center = local.center;
            if (renderer.flipX) center.x = -center.x;
            if (renderer.flipY) center.y = -center.y;
            var bounds = new Bounds(renderer.transform.TransformPoint(center), Vector3.zero);
            foreach (int x in new[] { -1, 1 }) foreach (int y in new[] { -1, 1 })
                bounds.Encapsulate(renderer.transform.TransformPoint(center + new Vector3(local.extents.x * x, local.extents.y * y)));
            return bounds;
        }

        // Compose the prefab's sprites without spawning its gameplay components.
        public static void Add(Transform parent, GameObject prefab)
        {
            var sprites = prefab.GetComponentsInChildren<SpriteRenderer>(true).Where(r => Visible(r, prefab.transform))
                .OrderBy(r => SortingLayer.GetLayerValueFromID(r.sortingLayerID)).ThenBy(r => r.sortingOrder).ToArray();
            if (sprites.Length == 0) return;
            var holder = new GameObject("Enemy illustration", typeof(RectTransform), typeof(LayoutElement));
            holder.transform.SetParent(parent, false);
            var size = holder.GetComponent<LayoutElement>();
            size.minWidth = size.preferredWidth = 100f;
            size.minHeight = size.preferredHeight = 100f;
            size.flexibleWidth = 0f;
            var bounds = BoundsOf(sprites[0]);
            foreach (var renderer in sprites) bounds.Encapsulate(BoundsOf(renderer));
            float scale = 90f / Mathf.Max(0.001f, Mathf.Max(bounds.size.x, bounds.size.y));
            var shield = prefab.GetComponent<EnemyHealthSystem>()?.shield;
            var tint = shield != null && shield.activeSelf ? shield.GetComponent<SpiderSurge.Enemies.ShieldedEnemyVisuals>() : null;
            foreach (var renderer in sprites)
            {
                var picture = new GameObject(renderer.name, typeof(RectTransform), typeof(Image));
                picture.transform.SetParent(holder.transform, false);
                var image = picture.GetComponent<Image>();
                image.sprite = renderer.sprite;
                image.color = tint != null ? tint.PreviewColor(renderer) : renderer.color;
                image.raycastTarget = false;
                var rect = picture.GetComponent<RectTransform>();
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
                var spriteBounds = renderer.sprite.bounds;
                var center = spriteBounds.center;
                if (renderer.flipX) center.x = -center.x;
                if (renderer.flipY) center.y = -center.y;
                rect.anchoredPosition = (renderer.transform.TransformPoint(center) - bounds.center) * scale;
                var right = renderer.transform.TransformVector(Vector3.right);
                var up = renderer.transform.TransformVector(Vector3.up);
                rect.sizeDelta = new Vector2(spriteBounds.size.x * right.magnitude, spriteBounds.size.y * up.magnitude) * scale;
                rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(right.y, right.x) * Mathf.Rad2Deg);
                rect.localScale = new Vector3(renderer.flipX ? -1f : 1f, renderer.flipY ? -1f : 1f, 1f);
            }
        }
    }
}
