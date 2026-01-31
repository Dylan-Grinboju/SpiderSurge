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

        [SerializeField]
        [Tooltip("Color when ability is currently active")]
        public Color activeColor = Color.blue;

        [Header("Glow Settings")]
        [SerializeField]
        [Range(0.1f, 2f)]
        [Tooltip("Glow pulse speed")]
        public float glowPulseSpeed = 0.5f;

        [SerializeField]
        [Range(0.1f, 1f)]
        [Tooltip("Minimum glow intensity")]
        public float minGlowIntensity = 0.3f;

        [SerializeField]
        [Range(0.1f, 1f)]
        [Tooltip("Maximum glow intensity")]
        public float maxGlowIntensity = 0.8f;

        [Header("Visibility")]
        [SerializeField]
        [Tooltip("If true, the indicator will only be visible when the ability is ready")]
        public bool showOnlyWhenReady = false;
        private BaseAbility trackedAbility;
        private GameObject circleObject;
        private SpriteRenderer spriteRenderer;
        private GameObject glowObject;
        private SpriteRenderer glowRenderer;
        private Transform targetTransform;

        public void Initialize(BaseAbility ability, Transform followTarget)
        {
            trackedAbility = ability;
            targetTransform = followTarget;

            CreateIndicatorVisual();
            UpdateIndicatorState();
        }

        private static Sprite _cachedCircleSprite;
        private static Sprite _cachedGlowSprite;

        private Sprite CreateCircleSprite()
        {
            if (_cachedCircleSprite != null) return _cachedCircleSprite;

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
            _cachedCircleSprite = Sprite.Create(texture, new Rect(0, 0, textureSize, textureSize), new Vector2(0.5f, 0.5f), 100f);
            return _cachedCircleSprite;
        }

        private Sprite CreateGlowSprite()
        {
            if (_cachedGlowSprite != null) return _cachedGlowSprite;

            // Create a larger texture for the glow
            int textureSize = 128;
            Texture2D texture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false);
            texture.filterMode = FilterMode.Bilinear;

            Color[] pixels = new Color[textureSize * textureSize];
            float center = textureSize / 2f;
            float innerRadius = textureSize / 4f; // Smaller inner radius for glow
            float outerRadius = textureSize / 2f - 1;

            for (int y = 0; y < textureSize; y++)
            {
                for (int x = 0; x < textureSize; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                    if (distance <= outerRadius)
                    {
                        // Create glow effect: high alpha near center, fading to transparent
                        float alpha;
                        if (distance <= innerRadius)
                        {
                            alpha = 1.0f; // Strong glow in center
                        }
                        else
                        {
                            // Fade out from inner to outer radius
                            float fadeFactor = (outerRadius - distance) / (outerRadius - innerRadius);
                            alpha = Mathf.Clamp01(fadeFactor * 0.8f); // Max 0.8 alpha for glow
                        }
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
            _cachedGlowSprite = Sprite.Create(texture, new Rect(0, 0, textureSize, textureSize), new Vector2(0.5f, 0.5f), 100f);
            return _cachedGlowSprite;
        }

        private void CreateIndicatorVisual()
        {
            // Create glow object first (behind)
            glowObject = new GameObject("AbilityIndicatorGlow");
            glowObject.transform.SetParent(transform);

            glowRenderer = glowObject.AddComponent<SpriteRenderer>();
            glowRenderer.sprite = CreateGlowSprite();
            glowRenderer.sortingLayerName = "Foreground";
            glowRenderer.sortingOrder = 32766; // One less than main

            // Create main circle object
            circleObject = new GameObject("AbilityIndicatorDot");
            circleObject.transform.SetParent(transform);

            spriteRenderer = circleObject.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = CreateCircleSprite();

            // Set to highest possible sorting order to render on top of other sprites
            spriteRenderer.sortingLayerName = "Foreground";
            spriteRenderer.sortingOrder = 32767;
            UpdateIndicatorScale();
        }

        private void UpdateGlowPulse()
        {
            if (glowRenderer == null) return;

            // Create pulsing effect using sine wave
            float pulse = Mathf.Sin(Time.time * glowPulseSpeed * 2f * Mathf.PI) * 0.5f + 0.5f; // 0 to 1
            float intensity = Mathf.Lerp(minGlowIntensity, maxGlowIntensity, pulse);

            Color currentColor = glowRenderer.color;
            currentColor.a = currentColor.a * (intensity / maxGlowIntensity); // Scale based on max intensity
            glowRenderer.color = currentColor;
        }

        private void UpdateIndicatorScale()
        {
            if (circleObject != null)
            {
                float targetDiameter = indicatorRadius * 2f;
                float currentDiameter = 0.64f;
                float scale = targetDiameter / currentDiameter;
                circleObject.transform.localScale = new Vector3(scale, scale, 1f);
            }

            if (glowObject != null)
            {
                float targetDiameter = indicatorRadius * 4f; // Glow is 2x larger
                float currentDiameter = 1.28f; // Since glow texture is 128px vs 64px for main
                float scale = targetDiameter / currentDiameter;
                glowObject.transform.localScale = new Vector3(scale, scale, 1f);
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

            UpdateIndicatorState();

            UpdateGlowPulse();
        }

        private void UpdateIndicatorState()
        {
            if (spriteRenderer == null || glowRenderer == null || trackedAbility == null)
            {
                return;
            }

            bool isActive = trackedAbility.IsActive();
            bool canUse = trackedAbility.IsUnlocked() && !trackedAbility.IsOnCooldown() && !isActive;

            Color baseColor;
            // Priority: Active (blue) > Cooldown (red) > Available (green)
            if (isActive)
            {
                baseColor = activeColor;
            }
            else if (canUse)
            {
                baseColor = availableColor;
            }
            else
            {
                baseColor = cooldownColor;
            }

            // Main indicator uses full color
            spriteRenderer.color = baseColor;

            // Glow uses the same color but with base alpha (will be modulated in Update)
            Color glowColor = baseColor;
            glowColor.a = baseColor.a * maxGlowIntensity; // Start with max intensity
            glowRenderer.color = glowColor;

            if (showOnlyWhenReady)
            {
                spriteRenderer.enabled = canUse;
                glowRenderer.enabled = canUse;
            }
            else
            {
                spriteRenderer.enabled = true;
                glowRenderer.enabled = true;
            }
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

        public void SetAvailableColor(Color c)
        {
            availableColor = c;
        }

        public void SetCooldownColor(Color c)
        {
            cooldownColor = c;
        }

        public void SetShowOnlyWhenReady(bool v)
        {
            showOnlyWhenReady = v;
        }

        public void SetActiveColor(Color c)
        {
            activeColor = c;
        }

        private void OnDestroy()
        {
            if (circleObject != null)
            {
                Destroy(circleObject);
            }
            if (glowObject != null)
            {
                Destroy(glowObject);
            }
        }
    }
}
