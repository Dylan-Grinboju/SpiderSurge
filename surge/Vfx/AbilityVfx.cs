using UnityEngine;

namespace SpiderSurge.Vfx
{
    public static class PulseVfx
    {
        public static void Draw(Vector3 position, float radius, bool deadly)
        {
            SurgeVfx.Safely(() =>
            {
                Color color = deadly ? Consts.Values.Vfx.PulseHotColor : Consts.Values.Pulse.RingColor;
                float speed = Consts.Values.Pulse.RingSpeed;
                float travel = radius / speed;

                Shockwave.Expand(position, radius, color, speed, 2.5f);
                SurgeVfx.Flash(position, color, deadly ? 1.5f : 1.8f, Mathf.Min(radius * 0.45f, 35f), 0.14f);
                SurgeVfx.Disc(position, SurgeVfx.SetAlpha(color, 0.8f), 2f, 12f, 0.1f, SurgeVfx.Order + 2);
                SurgeVfx.Burst(position, Color.Lerp(color, Color.white, 0.3f), color, deadly ? 14 : 22, speed * 0.35f, speed * 0.95f, 0.8f, 1.8f, travel * 0.45f, travel, 1f, trails: true);
                AccentVfx.Shards(position, color, deadly ? 16 : 26, 100f, 3f, 0.5f);
            });
        }
    }

    public static class FreezeVfx
    {
        public static void Cast(Vector3 position, float range)
        {
            SurgeVfx.Safely(() =>
            {
                Color ring = Consts.Values.Freeze.RingColor;
                float speed = Consts.Values.Pulse.RingSpeed;
                float travel = range / speed;

                Shockwave.Expand(position, range, ring, speed, 2f, 0.25f);
                SurgeVfx.Flash(position, Consts.Values.Freeze.TintColor, 1.2f, Mathf.Min(32f, range * 0.4f), 0.18f);
                SurgeVfx.Disc(position, SurgeVfx.SetAlpha(Consts.Values.Vfx.FrostColor, 0.18f), range * 0.3f, range, 0.35f);
                AccentVfx.Shards(position, ring, 32, 25f, range * 0.8f, 0.5f);
                SurgeVfx.Burst(position, Consts.Values.Freeze.TintColor, ring, 24, 0.5f, 3f, 0.4f, 0.8f, 0.5f, 1f, range * 0.9f, trails: false);
            });
        }

        public static void Thaw(Vector3 position)
        {
            SurgeVfx.Safely(() =>
            {
                AccentVfx.Shards(position, Consts.Values.Freeze.TintColor, 16, 65f, 3f, 0.38f);
                SurgeVfx.Flash(position, Consts.Values.Freeze.TintColor, 0.8f, 14f, 0.1f);
            });
        }

        // Rewind: a gold ring closes on the caster and motes stream in from the edge of its range
        public static void Rewind(Transform body, float range)
        {
            SurgeVfx.Safely(() =>
            {
                Color gold = Consts.Values.Freeze.RewindRingColor;
                float time = Consts.Values.Freeze.RewindRingTime;
                Vector3 position = body.position;
                Shockwave.Collapse(position, range, gold, time, 2f, body, at => SurgeVfx.Flash(at, gold, 2.5f, range * 0.5f, 0.4f));
                SurgeVfx.Burst(position, gold, Color.white, 60, -range / time, -range / time, 0.8f, 1.6f, time, time, range, trails: true, radiusThickness: 0f);
            });
        }

        // Each enemy turned back into a wasp pops where it stood
        public static void RewindPop(Vector3 position)
        {
            SurgeVfx.Safely(() =>
            {
                Color gold = Consts.Values.Freeze.RewindRingColor;
                SurgeVfx.Flash(position, gold, 2f, 20f, 0.3f);
                SurgeVfx.Burst(position, Color.white, gold, 24, 20f, 60f, 0.8f, 1.6f, 0.4f, 0.8f, 3f, trails: false);
            });
        }
    }

    public static class StorageVfx
    {
        // The held weapon folds into the hand
        public static void Send(Vector3 position)
        {
            SurgeVfx.Safely(() =>
            {
                Color purple = Consts.Values.Storage.RingColor;

                SurgeVfx.Burst(position, purple, purple, 8, -28f, -28f, 0.4f, 0.7f, 0.25f, 0.25f, 7f, trails: true, radiusThickness: 0f);
                SurgeVfx.Flash(position, purple, 1.5f, 15f, 0.3f);
            });
        }

        // A stored weapon unfolds where it comes back
        public static void Retrieve(Vector3 position)
        {
            SurgeVfx.Safely(() =>
            {
                Color purple = Consts.Values.Storage.RingColor;

                SurgeVfx.Disc(position, Color.white, 1f, 8f, 0.2f, SurgeVfx.Order + 2);
                AccentVfx.Shards(position, purple, 8, 24f, 2f, 0.3f);
                SurgeVfx.Flash(position, purple, 2f, 20f, 0.3f);
            });
        }

        // Black Hole: a dark singularity grows on the caster inside a ring that moves with them, while motes fall in from its edge
        public static BlackHoleVfx BlackHoleOpen(Transform body, float range, float time)
        {
            return SurgeVfx.Safely(() =>
            {
                Color purple = Consts.Values.Storage.RingColor;
                Vector3 position = body.position;
                float core = Consts.Values.Storage.BlackHoleCoreRadius;
                var effect = new BlackHoleVfx
                {
                    Ring = Shockwave.Hold(position, range, SurgeVfx.SetAlpha(purple, 0.22f), 0.9f, body),
                    Inner = Shockwave.Hold(position, core, purple, 2.4f, body),
                    Glow = SurgeVfx.Disc(position, SurgeVfx.SetAlpha(purple, 0.75f), core * 1.15f, core * 1.15f, float.MaxValue, -2, follow: body),
                    Core = SurgeVfx.Disc(position, SurgeVfx.SetAlpha(Color.black, 0.9f), core * 0.7f, core * 0.7f, float.MaxValue, -1, follow: body, hard: true),
                    Motes = OrbitParticles.Create(body, range, core * 0.65f),
                };
                Object.Destroy(effect.Ring.gameObject, time + 1f);
                Object.Destroy(effect.Inner.gameObject, time + 1f);
                Object.Destroy(effect.Glow.gameObject, time + 1f);
                Object.Destroy(effect.Core.gameObject, time + 1f);
                Object.Destroy(effect.Motes.gameObject, time + 1f);
                return effect;
            });
        }

        // The ring snaps shut on the singularity, which flashes out
        public static void BlackHoleClose(BlackHoleVfx open, Vector3 position, float range, Transform body)
        {
            SurgeVfx.Safely(() =>
            {
                if (open != null)
                {
                    if (open.Ring != null) open.Ring.Close();
                    if (open.Inner != null) open.Inner.Close();
                    if (open.Glow != null) Object.Destroy(open.Glow.gameObject);
                    if (open.Core != null) Object.Destroy(open.Core.gameObject);
                    SurgeVfx.Fade(open.Motes);
                }
                Color purple = Consts.Values.Storage.RingColor;
                Shockwave.Collapse(position, range, SurgeVfx.SetAlpha(purple, 0.3f), Consts.Values.Storage.BlackHoleCloseTime, 1f, body);
                Shockwave.Collapse(position, Consts.Values.Storage.BlackHoleCoreRadius, purple, 0.14f, 3f, body, at =>
                {
                    SurgeVfx.Flash(at, purple, 1.8f, 28f, 0.14f);
                    AccentVfx.Shards(at, purple, 12, 45f, 2f, 0.3f);
                });
            });
        }
    }

    public class BlackHoleVfx
    {
        public Shockwave Ring;
        public Shockwave Inner;
        public DiscFade Glow;
        public DiscFade Core;
        public ParticleSystem Motes;
    }

    public static class ClipVfx
    {
        public static void Cast(Vector3 position)
        {
            SurgeVfx.Safely(() =>
            {
                Color color = Consts.Values.Vfx.ClipColor;
                SurgeVfx.Flash(position, color, 0.9f, 12f, 0.15f);
                AccentVfx.Shards(position, color, 8, 20f, 2f, 0.3f);
            });
        }

        // Care Package: each spawner shows the game's incoming-weapon particles and a white puff where the weapon lands
        public static void Drop(WeaponSpawner spawner)
        {
            SurgeVfx.Safely(() =>
            {
                if (spawner.preSpawnParticles != null) spawner.preSpawnParticles.Play();
                Vector3 position = spawner.transform.position;
                SurgeVfx.Disc(position, Color.white, 1f, 7f, 0.25f, SurgeVfx.Order + 2);
                SurgeVfx.Burst(position, Color.white, Color.white, 20, 8f, 14f, 0.4f, 1f, 0.8f, 1.2f, 5f, trails: false);
            });
        }
    }

    public class WeaponAura : MonoBehaviour
    {
        private ParticleSystem embers;
        private LightFade glow;
        private Follow follow;
        private bool shown;

        public static WeaponAura Attach(Transform weapon)
        {
            return SurgeVfx.Safely(() => SurgeVfx.Create<WeaponAura>("WeaponAura", weapon.position, aura =>
            {
                GameObject holder = aura.gameObject;
                aura.follow = holder.AddComponent<Follow>();
                aura.follow.Target = weapon;
                aura.follow.DestroyWithTarget = false;
                Color color = Consts.Values.Vfx.ClipColor;
                aura.embers = SurgeVfx.Stream(holder.transform, Vector3.zero, color, color, 12f, 1f, 4f, 0.4f, 0.7f, 0.35f, 1.5f, gravity: -0.3f);
                aura.glow = SurgeVfx.Flash(weapon.position, color, 0.65f, 8f, float.MaxValue);
                aura.glow.transform.SetParent(holder.transform, true);
                aura.shown = true;
            }));
        }

        public void Retarget(Transform weapon)
        {
            SurgeVfx.Safely(() => Show(weapon));
        }

        private void Show(Transform weapon)
        {
            shown = weapon != null;
            if (follow != null) follow.Target = weapon != null ? (weapon.GetComponent<Weapon>()?.grip ?? weapon) : null;
            if (embers != null)
            {
                ParticleSystem.EmissionModule emission = embers.emission;
                emission.enabled = shown;
                if (!shown) embers.Clear();
            }
            if (glow != null) glow.gameObject.SetActive(shown);
        }

        // A weapon destroyed in the hand, like a grenade going off, is never unequipped, so the aura hides once its weapon is gone
        private void LateUpdate()
        {
            if (shown && (follow == null || follow.Target == null)) Retarget(null);
        }

        public void Stop()
        {
            SurgeVfx.Safely(() => SurgeVfx.Fade(embers));
            Destroy(gameObject);
        }
    }

    public class NullFieldBubble : MonoBehaviour
    {
        private const float Radius = 7f;
        private SpriteRenderer shell;
        private ParticleSystem motes;
        private Transform body;
        private float spriteSize;
        private float baseAlpha;
        private float flash;
        private float ending;
        private bool popped;
        private float hitTime = -10f;
        private Vector3 hitDirection;

        public static NullFieldBubble Attach(Transform body)
        {
            return SurgeVfx.Safely(() => SurgeVfx.Create<NullFieldBubble>("NullFieldBubble", body.position, bubble =>
            {
                GameObject holder = bubble.gameObject;
                bubble.body = body;
                Follow follow = holder.AddComponent<Follow>();
                follow.Target = body;

                Color gold = Consts.Values.Vfx.NullFieldColor;
                GameObject shellHolder = new GameObject("Shell");
                shellHolder.transform.SetParent(holder.transform, false);
                bubble.shell = shellHolder.AddComponent<SpriteRenderer>();
                Sprite forceField = SurgeVfx.ForceField;
                bubble.shell.sprite = forceField != null ? forceField : SurgeVfx.SoftDisc;
                if (forceField != null && SurgeVfx.ForceFieldMaterial != null) bubble.shell.sharedMaterial = SurgeVfx.ForceFieldMaterial;
                bubble.shell.sortingLayerName = SurgeVfx.SortingLayer;
                bubble.shell.sortingOrder = SurgeVfx.Order;
                bubble.spriteSize = bubble.shell.sprite.bounds.size.x;
                bubble.baseAlpha = forceField != null ? Consts.Values.Vfx.NullFieldShellAlpha : 0.25f;
                bubble.shell.color = SurgeVfx.SetAlpha(gold, bubble.baseAlpha);
                bubble.motes = SurgeVfx.Stream(holder.transform, Vector3.zero, Color.white, gold, 14f, 2f, 5f, 0.5f, 0.9f, 1.2f, Radius, radiusThickness: 0.2f);
                bubble.flash = 1f;
            }));
        }

        // The caller throttles sustained hits before broadcasting them to every peer.
        public void Hit(Vector3 direction)
        {
            flash = 1f;
            hitTime = Time.time;
            hitDirection = direction.sqrMagnitude > 0.001f ? direction.normalized : Vector3.down;
            SurgeVfx.Safely(() =>
            {
                Color gold = Consts.Values.Vfx.NullFieldColor;
                ArcFlash.Draw(transform.position, hitDirection, gold, Radius, 2f, 0.18f, body);
                AccentVfx.Shards(transform.position + hitDirection * Radius, gold, 7, 24f, 1f, 0.25f);
                SurgeVfx.Flash(transform.position, gold, 0.9f, 14f, 0.12f);
            });
        }

        public void End()
        {
            if (popped) return;
            popped = true;
            ending = 0.001f;
            SurgeVfx.Safely(() => SurgeVfx.Fade(motes));
            motes = null;
        }

        private void Update()
        {
            float scale;
            if (ending > 0f)
            {
                ending += Time.deltaTime;
                float t = ending / 0.15f;
                if (t >= 1f)
                {
                    Destroy(gameObject);
                    SurgeVfx.Safely(() =>
                    {
                        Color gold = Consts.Values.Vfx.NullFieldColor;
                        SurgeVfx.Burst(transform.position, Color.white, gold, 24, 30f, 70f, 0.6f, 1.2f, 0.4f, 0.7f, Radius * 0.5f, trails: true);
                        SurgeVfx.Flash(transform.position, gold, 2f, 25f, 0.3f);
                    });
                    return;
                }
                scale = Radius * 2f / spriteSize * (1f - t * 0.6f);
            }
            else
            {
                float pulse = ModConfig.ReducedEffects ? 1f : 1f + 0.05f * Mathf.Sin(Time.time * 6f);
                scale = Radius * 2f / spriteSize * pulse;
            }
            float hitAge = Time.time - hitTime;
            float squash = !ModConfig.ReducedEffects && hitAge < 0.25f ? Mathf.Sin(hitAge / 0.25f * Mathf.PI * 2f) * 0.12f * (1f - hitAge / 0.25f) : 0f;
            shell.transform.localScale = new Vector3(scale * (1f - squash), scale * (1f + squash), 1f);
            shell.transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(hitDirection.y, hitDirection.x) * Mathf.Rad2Deg);

            flash = Mathf.MoveTowards(flash, 0f, Time.deltaTime * 4f);
            float visibleFlash = flash * (ModConfig.ReducedEffects ? 0.25f : 1f);
            Color color = Color.Lerp(Consts.Values.Vfx.NullFieldColor, Color.white, visibleFlash * 0.7f);
            color.a = Mathf.Lerp(baseAlpha, Mathf.Min(1f, baseAlpha * 1.8f), visibleFlash);
            shell.color = color;
        }
    }

    public class ChannelEffect : MonoBehaviour
    {
        private ParticleSystem motes;

        public static ChannelEffect Attach(Transform body)
        {
            return SurgeVfx.Safely(() => SurgeVfx.Create<ChannelEffect>("NeuralBackupChannel", body.position, channel =>
            {
                GameObject holder = channel.gameObject;
                Follow follow = holder.AddComponent<Follow>();
                follow.Target = body;
                Color color = Consts.Values.Vfx.ReviveColor;
                channel.motes = SurgeVfx.Stream(holder.transform, Vector3.zero, color, Color.white, 60f, -50f, -70f, 0.4f, 0.9f, 0.7f, 42f, trails: true, radiusThickness: 0f);
                LightFade glow = SurgeVfx.Flash(body.position, color, 1.2f, 22f, float.MaxValue);
                glow.transform.SetParent(holder.transform, true);
            }));
        }

        public void Finish(bool completed)
        {
            SurgeVfx.Safely(() =>
            {
                SurgeVfx.Fade(motes);
                if (completed)
                {
                    Color color = Consts.Values.Vfx.ReviveColor;
                    Shockwave.Expand(transform.position, 40f, color, 250f, 2f);
                    SurgeVfx.Flash(transform.position, color, 1.5f, 26f, 0.35f);
                    SurgeVfx.Burst(transform.position, Color.Lerp(color, Color.white, 0.5f), color, 30, 40f, 120f, 0.5f, 1f, 0.4f, 0.8f, 3f, trails: true);
                }
            });
            Destroy(gameObject);
        }
    }

    public static class NullFieldVfx
    {
        // A revived spider comes back in a column of light, like a respawn turned up
        public static void Revive(Vector3 position)
        {
            SurgeVfx.Safely(() =>
            {
                Color color = Consts.Values.Vfx.ReviveColor;
                Shockwave.Expand(position, 30f, color, 200f, 2f);
                SurgeVfx.Disc(position, Color.white, 2f, 8f, 0.25f, SurgeVfx.Order + 2);
                SurgeVfx.Flash(position, color, 1.5f, 24f, 0.4f);
                SurgeVfx.Burst(position, Color.Lerp(color, Color.white, 0.5f), color, 36, -100f, -140f, 0.6f, 1.2f, 0.4f, 0.4f, 45f, trails: true, radiusThickness: 0f);
                SurgeVfx.Burst(position, Color.white, color, 30, 20f, 60f, 0.6f, 1.2f, 0.6f, 1f, 3f, trails: false, gravity: -0.2f);
            });
        }

        public static void Summon(Vector3 position)
        {
            SurgeVfx.Safely(() =>
            {
                Color color = Consts.Values.Vfx.ReviveColor;
                SurgeVfx.Flash(position, color, 2f, 25f, 0.4f);
                SurgeVfx.Burst(position, Color.white, color, 24, 15f, 45f, 0.6f, 1.2f, 0.5f, 0.9f, 4f, trails: false);
            });
        }
    }

    public static class ReadyVfx
    {
        // Readiness glints on the indicator without competing with an activation.
        public static void Pulse(Vector3 position, Color color)
        {
            SurgeVfx.Safely(() =>
            {
                SurgeVfx.Disc(position, color, 0.5f, 2.5f, 0.16f, 32767);
                SurgeVfx.Burst(position, color, color, 4, 4f, 10f, 0.3f, 0.5f, 0.12f, 0.2f, 0.5f, trails: false, order: 32767);
            });
        }
    }
}
