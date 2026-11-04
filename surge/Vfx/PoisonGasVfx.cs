using System.Linq;
using UnityEngine;
using Values = SpiderSurge.Consts.Values.PoisonGrenade;

namespace SpiderSurge.Vfx
{
    // The Poison Grenade's gas: a translucent cloud that fills exactly its kill radius and darkens toward its edge, with rolling puffs and rising bubbles
    public class PoisonGas : MonoBehaviour
    {
        private const int FillOrder = 200;
        private const int PuffOrder = 210;

        private static Sprite rim;
        private static Sprite puff;
        private static Material puffMaterial;

        private SpriteRenderer fill;
        private ParticleSystem puffs;
        private ParticleSystem bubbles;
        private Transform cloud;
        private float radius;
        private float fadeTime = -1f;
        private float fadeElapsed;

        // Unparented, so the cloud's scale stays off it; it removes itself once faded
        public static PoisonGas Burst(Transform cloud, float radius, bool ignition = true)
        {
            Vector3 position = cloud.position;
            return SurgeVfx.Safely(() => SurgeVfx.Create<PoisonGas>("PoisonGas", position, gas =>
            {
                gas.cloud = cloud;
                gas.radius = radius;
                if (ignition)
                {
                    SurgeVfx.Flash(position, Values.Color, 1.6f, radius + 8f, 0.35f);
                    SurgeVfx.Burst(position, Values.Color, Values.GasColor, 26, 25f, 50f, 1f, 2f, 0.25f, 0.45f, 3f, false);
                }

                var disc = new GameObject("PoisonGasFill");
                disc.transform.SetParent(gas.transform, false);
                gas.fill = disc.AddComponent<SpriteRenderer>();
                gas.fill.sprite = Rim;
                gas.fill.sortingLayerName = SurgeVfx.SortingLayer;
                gas.fill.sortingOrder = FillOrder;
                gas.puffs = gas.Puffs();
                gas.bubbles = SurgeVfx.Stream(gas.transform, Values.Color, Values.GasColor, 9f, 0.5f, 2f, 0.6f, 1.1f, 1.2f, radius * 0.85f, gravity: -0.4f);
                gas.Apply();
            }));
        }

        // A poisoned spider or enemy coughs up a puff of the gas
        public static void Poisoned(Vector3 position)
        {
            SurgeVfx.Safely(() => SurgeVfx.Burst(position, Values.Color, Values.GasColor, 14, 6f, 16f, 0.8f, 1.4f, 0.4f, 0.8f, 2f, false, gravity: -0.3f));
        }

        public void Fade(float duration)
        {
            if (fadeTime >= 0f) return;
            fadeTime = Mathf.Max(0.01f, duration);
            SurgeVfx.Safely(() =>
            {
                SurgeVfx.Fade(puffs);
                SurgeVfx.Fade(bubbles);
            });
        }

        private void Update()
        {
            if (cloud == null) Fade(Values.FadeTime);
            if (fadeTime >= 0f)
            {
                fadeElapsed += Time.deltaTime;
                if (fadeElapsed >= fadeTime)
                {
                    Destroy(gameObject);
                    return;
                }
            }
            Apply();
        }

        // The boundary shows the full danger radius immediately; the puffs swell inside it
        private void Apply()
        {
            if (fill == null) return;
            float scale = radius * 2f / fill.sprite.bounds.size.x;
            fill.transform.localScale = new Vector3(scale, scale, 1f);
            float alpha = Values.GasAlpha * (fadeTime >= 0f ? 1f - Mathf.Clamp01(fadeElapsed / fadeTime) : 1f);
            fill.color = SurgeVfx.SetAlpha(Values.GasColor, alpha);
        }

        private ParticleSystem Puffs()
        {
            return SurgeVfx.Create<ParticleSystem>("PoisonGasPuffs", transform.position, system =>
            {
                system.transform.SetParent(transform, true);
                system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

                ParticleSystem.MainModule main = system.main;
                main.loop = true;
                main.duration = 1f;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.scalingMode = ParticleSystemScalingMode.Shape;
                main.playOnAwake = false;
                main.maxParticles = 200;
                main.startLifetime = new ParticleSystem.MinMaxCurve(1.6f, 2.4f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.5f, 2f);
                main.startSize = new ParticleSystem.MinMaxCurve(radius * 0.4f, radius * 0.7f);
                main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                main.startColor = Values.GasColor;

                // Puff centers stay well inside, so the soft edge of the gas is the fill's
                ParticleSystem.ShapeModule shape = system.shape;
                shape.enabled = true;
                shape.shapeType = ParticleSystemShapeType.Circle;
                shape.radius = radius * 0.6f;
                shape.radiusThickness = 1f;
                shape.arc = 360f;

                ParticleSystem.EmissionModule emission = system.emission;
                emission.rateOverTime = 12f;
                emission.SetBursts(new[] { new ParticleSystem.Burst(0f, (short)14) });

                ParticleSystem.RotationOverLifetimeModule spin = system.rotationOverLifetime;
                spin.enabled = true;
                spin.z = new ParticleSystem.MinMaxCurve(-0.4f, 0.4f);

                ParticleSystem.SizeOverLifetimeModule size = system.sizeOverLifetime;
                size.enabled = true;
                size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.7f, 1f, 1.1f));

                Gradient gradient = new Gradient();
                gradient.SetKeys(
                    new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                    new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(Values.PuffAlpha, 0.3f), new GradientAlphaKey(Values.PuffAlpha, 0.6f), new GradientAlphaKey(0f, 1f) });
                ParticleSystem.ColorOverLifetimeModule color = system.colorOverLifetime;
                color.enabled = true;
                color.color = new ParticleSystem.MinMaxGradient(gradient);

                // The game's own smoke puff, as its launchers' exhaust uses
                ParticleSystem.TextureSheetAnimationModule sheet = system.textureSheetAnimation;
                sheet.enabled = true;
                sheet.mode = ParticleSystemAnimationMode.Sprites;
                sheet.AddSprite(Puff);

                ParticleSystemRenderer renderer = system.GetComponent<ParticleSystemRenderer>();
                renderer.renderMode = ParticleSystemRenderMode.Billboard;
                renderer.sharedMaterial = PuffMaterial;
                renderer.sortingLayerName = SurgeVfx.SortingLayer;
                renderer.sortingOrder = PuffOrder;

                system.gameObject.AddComponent<ReducedParticles>().Initialize(system);
                system.Play();
            });
        }

        private static Sprite Puff
        {
            get
            {
                if (puff == null) puff = Resources.FindObjectsOfTypeAll<Sprite>().FirstOrDefault(s => s.name == "puff") ?? SurgeVfx.SoftDisc;
                return puff;
            }
        }

        private static Material PuffMaterial
        {
            get
            {
                if (puffMaterial != null) return puffMaterial;
                Shader shader = Shader.Find("Sprites/Default");
                if (shader == null) throw new System.InvalidOperationException("The sprite shader is unavailable");
                puffMaterial = new Material(shader) { hideFlags = HideFlags.HideAndDontSave };
                return puffMaterial;
            }
        }

        // A faint disc that thickens into a firm rim just inside its edge, so the kill radius reads at a glance
        private static Sprite Rim
        {
            get
            {
                if (rim != null) return rim;
                const int size = 256;
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
                        float edge = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.75f, 0.95f, distance));
                        float outside = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.95f, 1f, distance));
                        texture.SetPixel(x, y, new Color(1f, 1f, 1f, (0.4f + 0.6f * edge) * (1f - outside)));
                    }
                }
                texture.Apply();
                rim = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
                rim.hideFlags = HideFlags.HideAndDontSave;
                return rim;
            }
        }
    }
}
