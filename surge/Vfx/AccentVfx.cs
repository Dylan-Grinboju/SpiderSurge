using System.Collections.Generic;
using UnityEngine;

namespace SpiderSurge.Vfx
{
    public static class AccentVfx
    {
        private static Material shards;

        private static Material ShardMaterial
        {
            get
            {
                if (shards != null) return shards;
                var texture = new Texture2D(32, 32, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
                for (int y = 0; y < 32; y++)
                    for (int x = 0; x < 32; x++)
                    {
                        float edge = Mathf.Abs((x + 0.5f) / 16f - 1f) + Mathf.Abs((y + 0.5f) / 16f - 1f);
                        texture.SetPixel(x, y, new Color(1f, 1f, 1f, Mathf.Clamp01((1f - edge) * 12f)));
                    }
                texture.Apply();
                shards = new Material(Shader.Find("Sprites/Default")) { mainTexture = texture, hideFlags = HideFlags.HideAndDontSave };
                return shards;
            }
        }

        public static ParticleSystem Shards(Vector3 position, Color color, int count, float speed, float radius = 2f, float life = 0.45f)
        {
            var particles = SurgeVfx.Burst(position, color, color, count, speed * 0.35f, speed, 1f, 2f, life * 0.5f, life, radius, false, gravity: 0.6f);
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = particles.main;
            main.startSize = new ParticleSystem.MinMaxCurve(0.8f, 2.2f);
            main.startColor = Color.Lerp(color, Color.white, 0.35f) * 2f;
            var rotation = particles.rotationOverLifetime;
            rotation.enabled = true;
            rotation.z = new ParticleSystem.MinMaxCurve(-6f, 6f);
            particles.GetComponent<ParticleSystemRenderer>().sharedMaterial = ShardMaterial;
            particles.Play();
            return particles;
        }

        public static void Impact(Vector3 position, Vector3 direction, bool deadly)
        {
            Color color = deadly ? Consts.Values.Vfx.PulseHotColor : Consts.Values.Pulse.RingColor;
            Shards(position, color, 9, 55f, 1.5f, 0.32f);
            ArcFlash.Draw(position, direction, color, 4f, 1.5f, 0.14f);
            SurgeVfx.Flash(position, color, 0.8f, 12f, 0.12f);
        }
    }

    public sealed class ArcFlash : MonoBehaviour
    {
        private LineRenderer line;
        private Color color;
        private float width;
        private float duration;
        private float elapsed;
        private Transform follow;
        private Vector3 offset;

        public static void Draw(Vector3 position, Vector3 direction, Color color, float radius, float width, float duration, Transform follow = null)
        {
            SurgeVfx.Create<ArcFlash>("ImpactArc", position, arc =>
            {
                arc.color = color;
                arc.width = width;
                arc.duration = duration;
                arc.follow = follow;
                arc.offset = follow != null ? position - follow.position : Vector3.zero;
                arc.line = arc.gameObject.AddComponent<LineRenderer>();
                arc.line.useWorldSpace = false;
                arc.line.positionCount = 17;
                arc.line.sharedMaterial = SurgeVfx.SoftLine;
                arc.line.sortingLayerName = SurgeVfx.SortingLayer;
                arc.line.sortingOrder = SurgeVfx.Order + 12;
                float angle = Mathf.Atan2(direction.y, direction.x);
                for (int i = 0; i < 17; i++)
                {
                    float a = angle + Mathf.Lerp(-0.7f, 0.7f, i / 16f);
                    arc.line.SetPosition(i, new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * radius);
                }
                arc.Apply(0f);
            });
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            if (elapsed >= duration) { Destroy(gameObject); return; }
            if (follow != null) transform.position = follow.position + offset;
            Apply(elapsed / duration);
        }

        private void Apply(float t)
        {
            line.widthMultiplier = width * (1f - t * 0.7f);
            line.startColor = line.endColor = SurgeVfx.SetAlpha(Color.Lerp(color, Color.white, 0.4f) * 1.8f, (1f - t) * (ModConfig.ReducedEffects ? 0.35f : 1f));
        }
    }

    public sealed class OrbitParticles : MonoBehaviour
    {
        private ParticleSystem system;
        private readonly ParticleSystem.Particle[] particles = new ParticleSystem.Particle[128];
        private float radius;
        private float core;

        public static ParticleSystem Create(Transform body, float radius, float core)
        {
            var system = SurgeVfx.Stream(body, Vector3.zero, Consts.Values.Storage.RingColor, Color.white, 28f, 0f, 0f, 0.8f, 1.5f, 0.85f, radius, trails: true, radiusThickness: 0f);
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = system.main;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.maxParticles = 128;
            var trails = system.trails;
            trails.worldSpace = false;
            trails.lifetime = 0.045f;
            var orbit = system.gameObject.AddComponent<OrbitParticles>();
            orbit.system = system;
            orbit.radius = radius;
            orbit.core = core;
            system.Play();
            return system;
        }

        private void LateUpdate()
        {
            int count = system.GetParticles(particles);
            for (int i = 0; i < count; i++)
            {
                float t = 1f - particles[i].remainingLifetime / particles[i].startLifetime;
                float angle = Mathf.Atan2(particles[i].position.y, particles[i].position.x) + Time.deltaTime * 4.1f;
                float r = Mathf.Lerp(radius, core, t * t);
                particles[i].position = new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * r;
                particles[i].velocity = Vector3.zero;
            }
            system.SetParticles(particles, count);
        }
    }

    public sealed class WeaponEcho : MonoBehaviour
    {
        private readonly List<SpriteRenderer> sprites = new List<SpriteRenderer>();
        private Vector3 start;
        private Vector3 destination;
        private Vector3 scale;
        private Transform follow;
        private float elapsed;
        private bool retrieve;

        // Copy only the weapon's visible sprites; the real weapon and its colliders never animate or wait.
        public static void Draw(Weapon weapon, Vector3 position, float angle, Vector3 scale, Vector3 destination, Transform follow, bool retrieve)
        {
            SurgeVfx.Create<WeaponEcho>("PocketWeaponEcho", position, echo =>
            {
                echo.start = position;
                echo.destination = destination;
                echo.follow = follow;
                echo.scale = scale;
                echo.retrieve = retrieve;
                echo.transform.rotation = Quaternion.Euler(0f, 0f, angle);
                foreach (var source in weapon.GetComponentsInChildren<SpriteRenderer>(true))
                {
                    if (!source.enabled || source.sprite == null || echo.sprites.Count >= 16) continue;
                    var holder = new GameObject("WeaponSilhouette");
                    holder.transform.SetParent(echo.transform, false);
                    holder.transform.localPosition = weapon.transform.InverseTransformPoint(source.transform.position);
                    holder.transform.localRotation = Quaternion.Inverse(weapon.transform.rotation) * source.transform.rotation;
                    Vector3 root = weapon.transform.lossyScale;
                    Vector3 child = source.transform.lossyScale;
                    holder.transform.localScale = new Vector3(child.x / Mathf.Max(0.001f, Mathf.Abs(root.x)), child.y / Mathf.Max(0.001f, Mathf.Abs(root.y)), 1f);
                    var copy = holder.AddComponent<SpriteRenderer>();
                    copy.sprite = source.sprite;
                    copy.flipX = source.flipX;
                    copy.flipY = source.flipY;
                    copy.sortingLayerName = SurgeVfx.SortingLayer;
                    copy.sortingOrder = SurgeVfx.Order + 4;
                    echo.sprites.Add(copy);
                }
                echo.Apply(0f);
            });
        }

        private void Update()
        {
            elapsed += Time.deltaTime;
            if (elapsed >= 0.22f) { Destroy(gameObject); return; }
            Apply(elapsed / 0.22f);
        }

        private void Apply(float t)
        {
            Vector3 target = follow != null ? follow.position : destination;
            float fold = retrieve ? 1f - Mathf.Pow(1f - t, 3f) : 1f - t * t;
            transform.position = retrieve ? target : Vector3.Lerp(start, target, t * t);
            transform.localScale = scale * Mathf.Max(0.04f, fold);
            Color color = Color.Lerp(Consts.Values.Storage.RingColor, Color.white, retrieve ? 1f - t : t);
            color.a = (1f - t) * (ModConfig.ReducedEffects ? 0.35f : 0.8f);
            foreach (var sprite in sprites) sprite.color = color;
        }
    }
}
