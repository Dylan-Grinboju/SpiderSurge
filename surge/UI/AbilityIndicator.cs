using SpiderSurge.Common;
using UnityEngine;

namespace SpiderSurge
{
    public class AbilityIndicator : MonoBehaviour
    {
        public float indicatorRadius = 1.5f;
        public Vector3 offset = new Vector3(3f, 8f, 0f);
        public Color availableColor = Color.green;
        public Color cooldownColor = Color.red;
        public Color activeColor = Color.blue;
        public bool showOnlyWhenReady;
        private const int Segments = 64;
        private BaseAbility ability;
        private Transform target;
        private LineRenderer track;
        private LineRenderer fill;
        private SpriteRenderer weapon;
        private Material material;
        private float drawnProgress = -1f;
        private Color? drawnColor;
        private readonly Vector3[] points = new Vector3[Segments + 1];

        public void Initialize(BaseAbility tracked, Transform follow)
        {
            ability = tracked;
            target = follow;
            material = new Material(Shader.Find("Sprites/Default"));
            track = MakeRing("CooldownTrack", 32765);
            fill = MakeRing("CooldownFill", 32766);
            var holder = new GameObject("StoredWeaponPreview");
            holder.transform.SetParent(transform, false);
            weapon = holder.AddComponent<SpriteRenderer>();
            weapon.sortingLayerName = "Foreground";
            weapon.sortingOrder = 32767;
            Settings.Changed += Changed;
            ApplySettings();
            AbilityHud.Track(tracked);
        }

        private LineRenderer MakeRing(string name, int order)
        {
            var holder = new GameObject(name);
            holder.transform.SetParent(transform, false);
            var ring = holder.AddComponent<LineRenderer>();
            ring.useWorldSpace = false;
            ring.sharedMaterial = material;
            ring.sortingLayerName = "Foreground";
            ring.sortingOrder = order;
            ring.numCapVertices = 3;
            ring.numCornerVertices = 2;
            return ring;
        }

        private void Changed(string key)
        {
            if (Settings.Affects(key, "surge.indicator") || key.StartsWith("surge.indicator.")) ApplySettings();
        }

        private void ApplySettings()
        {
            indicatorRadius = ModConfig.IndicatorRadius;
            offset = new Vector3(ModConfig.IndicatorOffsetX, ModConfig.IndicatorOffsetY, 0f);
            availableColor = ModConfig.IndicatorAvailableColor;
            cooldownColor = ModConfig.IndicatorCooldownColor;
            activeColor = ModConfig.IndicatorActiveColor;
            showOnlyWhenReady = ModConfig.IndicatorShowOnlyWhenReady;
            Resize();
        }

        private void Resize()
        {
            if (track == null) return;
            track.startWidth = track.endWidth = fill.startWidth = fill.endWidth = indicatorRadius * 0.2f;
            DrawArc(track, 1f);
            drawnProgress = -1f;
        }

        private void DrawArc(LineRenderer ring, float progress)
        {
            int count = Mathf.Max(2, Mathf.CeilToInt(Segments * progress) + 1);
            ring.positionCount = count;
            for (int i = 0; i < count; i++)
            {
                float angle = progress * Mathf.PI * 2f * i / (count - 1);
                points[i] = new Vector3(Mathf.Sin(angle), Mathf.Cos(angle), 0f) * indicatorRadius;
            }
            // Points past positionCount are left from a longer arc; SetPositions ignores them
            ring.SetPositions(points);
        }

        private void Update()
        {
            if (target == null || ability == null || track == null) return;
            transform.position = target.position + offset;
            bool alive = ability.IsPlayerAlive() && ability.IsUnlocked();
            bool active = ability.IsActive();
            bool ready = !active && !ability.IsOnCooldown();
            bool shown = !ModConfig.IndicatorCompactHud && alive && (!showOnlyWhenReady || ready);
            float progress = active ? ability.ActiveProgress : ability.CooldownProgress;
            if (track.enabled != shown) track.enabled = shown;
            bool fillShown = shown && progress > 0f;
            if (fill.enabled != fillShown) fill.enabled = fillShown;
            Color color = active ? activeColor : ready ? availableColor : cooldownColor;
            if (!drawnColor.HasValue || !drawnColor.Value.Equals(color))
            {
                track.startColor = track.endColor = new Color(color.r, color.g, color.b, 0.2f);
                fill.startColor = fill.endColor = color;
                drawnColor = color;
            }
            if (!fillShown)
            {
                drawnProgress = -1f;
            }
            else if (Mathf.Abs(progress - drawnProgress) > 0.001f || progress == 1f && drawnProgress != 1f)
            {
                DrawArc(fill, progress);
                drawnProgress = progress;
            }
            Sprite sprite = ability is StorageAbility storage ? storage.StoredWeaponSprite : null;
            weapon.enabled = !ModConfig.IndicatorCompactHud && alive && sprite != null;
            if (sprite != null)
            {
                weapon.sprite = sprite;
                float size = Mathf.Max(sprite.bounds.size.x, sprite.bounds.size.y);
                weapon.transform.localScale = Vector3.one * (indicatorRadius * 2.2f / Mathf.Max(0.01f, size));
                weapon.transform.localPosition = new Vector3(indicatorRadius * 2.6f, 0f, 0f);
            }
        }

        private void OnDestroy()
        {
            Settings.Changed -= Changed;
            if (material != null) Destroy(material);
        }
    }
}
