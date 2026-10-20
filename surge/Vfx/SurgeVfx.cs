using System.Linq;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Experimental.Rendering.Universal;

namespace SpiderSurge.Vfx
{
    // The game's own look, built at run time: its glow particle material, soft neon lines, 2D light flashes and soft discs
    public static class SurgeVfx
    {
        public const string SortingLayer = "Foreground";
        public const int Order = 285;
        // The glow material draws each dot about 2.5 times its nominal size, so sizes here are given in world units
        private const float GlowSizeScale = 0.4f;

        private static Material glowParticles;
        private static Material softLine;
        private static Sprite softDisc;
        private static Sprite hardDisc;
        private static Sprite forceField;
        private static Material forceFieldMaterial;
        private static int[] lightLayers;
        private static readonly AccessTools.FieldRef<Light2D, int[]> LightSortingLayers =
            AccessTools.FieldRefAccess<Light2D, int[]>("m_ApplyToSortingLayers");

        // The material the game's sparks, respawn and recall particles use; each quad is a soft glowing dot
        public static Material GlowParticles
        {
            get
            {
                if (glowParticles == null) glowParticles = FindMaterial("ParticleGlow") ?? FindMaterial("SpriteGlow") ?? SoftLine;
                return glowParticles;
            }
        }

        // A vertex-colored line whose edges fade out across its width
        public static Material SoftLine
        {
            get
            {
                if (softLine != null) return softLine;
                const int height = 32;
                Texture2D texture = new Texture2D(1, height, TextureFormat.RGBA32, false)
                {
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear,
                    hideFlags = HideFlags.HideAndDontSave
                };
                for (int y = 0; y < height; y++)
                {
                    float distance = Mathf.Abs((y + 0.5f) / height - 0.5f) * 2f;
                    texture.SetPixel(0, y, new Color(1f, 1f, 1f, (1f - distance) * (1f - distance)));
                }
                texture.Apply();
                softLine = new Material(Shader.Find("Sprites/Default")) { mainTexture = texture, hideFlags = HideFlags.HideAndDontSave };
                return softLine;
            }
        }

        // A glow that fades from its center
        public static Sprite SoftDisc
        {
            get
            {
                if (softDisc == null) softDisc = MakeDisc(distance => Mathf.Pow(Mathf.Clamp01(1f - distance), 1.6f));
                return softDisc;
            }
        }

        // A solid disc with a short soft rim
        public static Sprite HardDisc
        {
            get
            {
                if (hardDisc == null) hardDisc = MakeDisc(distance => Mathf.Clamp01((1f - distance) / 0.18f));
                return hardDisc;
            }
        }

        private static Sprite MakeDisc(System.Func<float, float> alphaAt)
        {
            const int size = 128;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                hideFlags = HideFlags.HideAndDontSave
            };
            float half = size / 2f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(half, half)) / half;
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alphaAt(distance)));
                }
            }
            texture.Apply();
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }

        // The game's shield bubble sprite and its dissolving glow material, when a shielded spider or enemy has loaded them
        public static Sprite ForceField
        {
            get
            {
                if (forceField == null) forceField = Resources.FindObjectsOfTypeAll<Sprite>().FirstOrDefault(s => s.name == "force_field-09");
                return forceField;
            }
        }

        public static Material ForceFieldMaterial
        {
            get
            {
                if (forceFieldMaterial == null) forceFieldMaterial = FindMaterial("GlowPhaseIn");
                return forceFieldMaterial;
            }
        }

        private static int[] LightLayers
        {
            get
            {
                if (lightLayers == null) lightLayers = UnityEngine.SortingLayer.layers.Select(layer => layer.id).ToArray();
                return lightLayers;
            }
        }

        private static Material FindMaterial(string name)
        {
            return Resources.FindObjectsOfTypeAll<Material>().FirstOrDefault(m => m.name == name);
        }

        public static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }

        // A point light that fades out like the game's explosion light
        public static LightFade Flash(Vector3 position, Color color, float intensity, float radius, float duration, Transform follow = null)
        {
            GameObject holder = new GameObject("SurgeFlash");
            holder.transform.position = position;
            Light2D light = holder.AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Point;
            light.color = WithAlpha(color, 1f);
            light.intensity = intensity;
            light.pointLightInnerRadius = 0f;
            light.pointLightOuterRadius = radius;
            LightSortingLayers(light) = LightLayers;
            LightFade fade = holder.AddComponent<LightFade>();
            fade.Setup(light, duration, follow);
            return fade;
        }

        // A soft disc that scales between two radii while it fades; a pulse grows to the end radius and shrinks back
        public static DiscFade Disc(Vector3 position, Color color, float startRadius, float endRadius, float duration, int order = Order, bool pulse = false, Transform follow = null, bool hard = false)
        {
            GameObject holder = new GameObject("SurgeDisc");
            holder.transform.position = position;
            SpriteRenderer renderer = holder.AddComponent<SpriteRenderer>();
            renderer.sprite = hard ? HardDisc : SoftDisc;
            renderer.sortingLayerName = SortingLayer;
            renderer.sortingOrder = order;
            DiscFade fade = holder.AddComponent<DiscFade>();
            fade.Setup(renderer, color, startRadius, endRadius, duration, pulse, follow);
            return fade;
        }

        // One burst of glowing dots from a circle, in the game's explosion and shield-break style; it removes itself when they are gone
        public static ParticleSystem Burst(Vector3 position, Color from, Color to, int count, float speedMin, float speedMax, float sizeMin, float sizeMax,
            float lifeMin, float lifeMax, float radius, bool trails, float gravity = 0f, float radiusThickness = 1f, int order = Order)
        {
            ParticleSystem system = NewSystem("SurgeBurst", position, null, from, to, speedMin, speedMax, sizeMin, sizeMax, lifeMin, lifeMax, radius, radiusThickness, trails, gravity, order);
            ParticleSystem.MainModule main = system.main;
            main.loop = false;
            main.duration = 0.1f;
            main.stopAction = ParticleSystemStopAction.Destroy;
            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)count) });
            system.Play();
            return system;
        }

        // A steady stream of glowing dots that follows its parent; stop it with Fade
        public static ParticleSystem Stream(Transform parent, Vector3 localPosition, Color from, Color to, float rate, float speedMin, float speedMax,
            float sizeMin, float sizeMax, float life, float radius, bool trails = false, float gravity = 0f, float radiusThickness = 1f, int order = Order)
        {
            ParticleSystem system = NewSystem("SurgeStream", parent.position, parent, from, to, speedMin, speedMax, sizeMin, sizeMax, life, life, radius, radiusThickness, trails, gravity, order);
            system.transform.localPosition = localPosition;
            ParticleSystem.MainModule main = system.main;
            main.loop = true;
            main.duration = 1f;
            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = rate;
            system.Play();
            return system;
        }

        // Stops emitting and removes the system once its last dot has faded
        public static void Fade(ParticleSystem system)
        {
            if (system == null) return;
            system.transform.SetParent(null, true);
            system.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            Object.Destroy(system.gameObject, system.main.startLifetime.constantMax + 0.5f);
        }

        private static ParticleSystem NewSystem(string name, Vector3 position, Transform parent, Color from, Color to, float speedMin, float speedMax,
            float sizeMin, float sizeMax, float lifeMin, float lifeMax, float radius, float radiusThickness, bool trails, float gravity, int order)
        {
            GameObject holder = new GameObject(name);
            holder.transform.position = position;
            if (parent != null) holder.transform.SetParent(parent, true);
            ParticleSystem system = holder.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = system.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            main.playOnAwake = false;
            main.maxParticles = 1000;
            main.startLifetime = new ParticleSystem.MinMaxCurve(lifeMin, lifeMax);
            main.startSpeed = new ParticleSystem.MinMaxCurve(speedMin, speedMax);
            main.startSize = new ParticleSystem.MinMaxCurve(sizeMin * GlowSizeScale, sizeMax * GlowSizeScale);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI);
            main.startColor = WithAlpha(from, 1f);
            main.gravityModifier = gravity;

            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = Mathf.Max(0.01f, radius);
            shape.radiusThickness = radiusThickness;
            shape.arc = 360f;

            ParticleSystem.ColorOverLifetimeModule color = system.colorOverLifetime;
            color.enabled = true;
            color.color = new ParticleSystem.MinMaxGradient(FadeGradient(from, to));

            ParticleSystem.SizeOverLifetimeModule size = system.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 1f, 1f, 0f));

            ParticleSystemRenderer renderer = holder.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.sharedMaterial = GlowParticles;
            renderer.sortingLayerName = SortingLayer;
            renderer.sortingOrder = order;
            renderer.minParticleSize = 0f;
            renderer.maxParticleSize = 0.5f;

            if (trails)
            {
                ParticleSystem.TrailModule trail = system.trails;
                trail.enabled = true;
                trail.lifetime = 0.15f;
                trail.widthOverTrail = 0.6f;
                trail.minVertexDistance = 0.5f;
                trail.inheritParticleColor = true;
                trail.dieWithParticles = true;
                trail.colorOverLifetime = new ParticleSystem.MinMaxGradient(FadeGradient(Color.white, Color.white));
                renderer.trailMaterial = GlowParticles;
            }
            return system;
        }

        private static Gradient FadeGradient(Color from, Color to)
        {
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(from, 0f), new GradientColorKey(to, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 0.4f), new GradientAlphaKey(0f, 1f) });
            return gradient;
        }
    }

    public class LightFade : MonoBehaviour
    {
        private Light2D light2D;
        private float startIntensity;
        private float duration;
        private float elapsed;
        private Transform follow;

        public void Setup(Light2D light, float fadeDuration, Transform followTarget)
        {
            light2D = light;
            startIntensity = light.intensity;
            duration = Mathf.Max(0.01f, fadeDuration);
            follow = followTarget;
        }

        private void Update()
        {
            if (follow != null) transform.position = follow.position;
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            if (t >= 1f)
            {
                Destroy(gameObject);
                return;
            }
            light2D.intensity = startIntensity * (1f - t);
        }
    }

    public class DiscFade : MonoBehaviour
    {
        private SpriteRenderer sprite;
        private Color color;
        private float startRadius;
        private float endRadius;
        private float duration;
        private float elapsed;
        private float spriteSize;
        private bool pulse;
        private Transform follow;

        public void Setup(SpriteRenderer spriteRenderer, Color discColor, float from, float to, float fadeDuration, bool isPulse, Transform followTarget)
        {
            sprite = spriteRenderer;
            color = discColor;
            startRadius = from;
            endRadius = to;
            duration = Mathf.Max(0.01f, fadeDuration);
            pulse = isPulse;
            follow = followTarget;
            spriteSize = spriteRenderer.sprite.bounds.size.x;
            Apply(0f);
        }

        private void Update()
        {
            if (follow != null) transform.position = follow.position;
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            if (t >= 1f)
            {
                Destroy(gameObject);
                return;
            }
            Apply(t);
        }

        private void Apply(float t)
        {
            float radius;
            float alpha;
            if (pulse)
            {
                float wave = Mathf.Sin(t * Mathf.PI);
                radius = Mathf.Lerp(startRadius, endRadius, wave);
                alpha = color.a * Mathf.Min(1f, wave * 1.5f);
            }
            else
            {
                radius = Mathf.Lerp(startRadius, endRadius, t);
                alpha = color.a * (1f - t * t);
            }
            float scale = radius * 2f / spriteSize;
            transform.localScale = new Vector3(scale, scale, 1f);
            sprite.color = SurgeVfx.WithAlpha(color, alpha);
        }
    }

    // Keeps an effect on a moving thing without parenting it, so freezes and scale changes on the target leave it alone
    public class Follow : MonoBehaviour
    {
        public Transform Target;
        public Vector3 Offset;
        public bool DestroyWithTarget = true;

        private void LateUpdate()
        {
            if (Target == null)
            {
                if (DestroyWithTarget) Destroy(gameObject);
                return;
            }
            transform.position = Target.position + Offset;
        }
    }
}
