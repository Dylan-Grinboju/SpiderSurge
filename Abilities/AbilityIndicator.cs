using UnityEngine;

namespace SpiderSurge
{
    public class AbilityIndicator : MonoBehaviour
    {
        [Header("Indicator Settings")]
        [SerializeField]
        [Range(0.5f, 10f)]
        [Tooltip("Radius of the indicator dot")]
        public float indicatorRadius = 1.5f;

        [SerializeField]
        [Tooltip("Offset from the spider's position")]
        public Vector3 offset = new Vector3(3f, 8f, 0f);

        [Header("Colors")]
        [SerializeField]
        [Tooltip("Color when ability is available")]
        public Color availableColor = Color.green;

        [SerializeField]
        [Tooltip("Color when ability is on cooldown")]
        public Color cooldownColor = Color.red;

        // Reference to the ability this indicator is tracking
        private BaseAbility trackedAbility;

        // The visual circle GameObject
        private GameObject circleObject;
        private SpriteRenderer spriteRenderer;

        // Transform to follow
        private Transform targetTransform;

        public void Initialize(BaseAbility ability, Transform followTarget)
        {
            trackedAbility = ability;
            targetTransform = followTarget;

            CreateIndicatorVisual();
            UpdateIndicatorState();
        }

        private void CreateIndicatorVisual()
        {
            // Create a child GameObject for the indicator
            circleObject = new GameObject("AbilityIndicatorDot");
            circleObject.transform.SetParent(transform);
            
            // Add SpriteRenderer for the dot
            spriteRenderer = circleObject.AddComponent<SpriteRenderer>();

            // Create a simple circle sprite programmatically
            spriteRenderer.sprite = CreateCircleSprite();

            // Set to highest possible sorting order to render on top of other sprites
            spriteRenderer.sortingLayerName = "Foreground";
            spriteRenderer.sortingOrder = 32767;

            // Set initial scale based on radius
            UpdateIndicatorScale();
        }

        private Sprite CreateCircleSprite()
        {
            // Create a small texture for the circle
            int textureSize = 64;
            Texture2D texture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Bilinear;

            Color[] pixels = new Color[textureSize * textureSize];
            float center = textureSize / 2f;
            float radius = textureSize / 2f - 1;

            for (int y = 0; y < textureSize; y++)
            {
                for (int x = 0; x < textureSize; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                    if (distance <= radius)
                    {
                        // Smooth edge with anti-aliasing
                        float alpha = Mathf.Clamp01((radius - distance) / 2f);
                        pixels[y * textureSize + x] = new Color(1f, 1f, 1f, alpha);
                    }
                    else
                    {
                        pixels[y * textureSize + x] = Color.clear;
                    }
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();

            // Create sprite from texture (100 pixels per unit)
            return Sprite.Create(texture, new Rect(0, 0, textureSize, textureSize), new Vector2(0.5f, 0.5f), 100f);
        }

        private void UpdateIndicatorScale()
        {
            if (circleObject != null)
            {
                // Scale the sprite to match the desired radius
                // The sprite is 64 pixels at 100 pixels/unit = 0.64 units diameter
                // We want it to have diameter = indicatorRadius * 2
                float targetDiameter = indicatorRadius * 2f;
                float currentDiameter = 0.64f;
                float scale = targetDiameter / currentDiameter;
                circleObject.transform.localScale = new Vector3(scale, scale, 1f);
            }
        }

        private void Update()
        {
            if (targetTransform == null || circleObject == null)
            {
                return;
            }

            // Follow the target with offset
            transform.position = targetTransform.position + offset;

            // Update the visual state
            UpdateIndicatorState();

            // Update scale in case it was modified in inspector
            UpdateIndicatorScale();
        }

        private void UpdateIndicatorState()
        {
            if (spriteRenderer == null || trackedAbility == null)
            {
                return;
            }

            // Determine the color based on ability state
            // Green if ability is unlocked and not on cooldown
            // Red if ability is on cooldown or not unlocked
            bool canUse = trackedAbility.IsUnlocked() && !trackedAbility.IsOnCooldown() && !trackedAbility.IsActive();

            spriteRenderer.color = canUse ? availableColor : cooldownColor;
        }

        public void SetRadius(float newRadius)
        {
            indicatorRadius = Mathf.Clamp(newRadius, 0.5f, 10f);
            UpdateIndicatorScale();
        }

        public void SetOffset(Vector3 newOffset)
        {
            offset = newOffset;
        }

        private void OnDestroy()
        {
            if (circleObject != null)
            {
                Destroy(circleObject);
            }
        }
    }
}
