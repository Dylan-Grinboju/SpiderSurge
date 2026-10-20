using UnityEngine;

namespace SpiderSurge.Vfx
{
    // Kinetic Pulse: a flash and a shockwave whose front carries sparks, with slower embers spreading behind it
    public static class PulseVfx
    {
        public static void Draw(Vector3 position, float radius, bool deadly)
        {
            Color color = deadly ? Consts.Values.Vfx.PulseHotColor : Consts.Values.Pulse.RingColor;
            float speed = Consts.Values.Pulse.RingSpeed;
            float travel = radius / speed;

            Shockwave.Expand(position, radius, color, speed, 2.5f);
            SurgeVfx.Flash(position, color, deadly ? 2f : 1.3f, radius * 0.6f, 0.3f);
            SurgeVfx.Disc(position, Color.white, 2f, radius * 0.25f, 0.2f, SurgeVfx.Order + 2);
            SurgeVfx.Burst(position, Color.white, color, 36, speed, speed, 1.5f, 2.5f, travel, travel, 1f, trails: true);
            SurgeVfx.Burst(position, color, color, 50, 30f, 140f, 0.5f, 1.2f, 0.4f, 0.9f, 3f, trails: false);
        }
    }

    // Freeze Time: an ice ring, shards flying with it, and a frost haze that settles over the frozen area
    public static class FreezeVfx
    {
        public static void Cast(Vector3 position, float range)
        {
            Color ring = Consts.Values.Freeze.RingColor;
            float speed = Consts.Values.Pulse.RingSpeed;
            float travel = range / speed;

            Shockwave.Expand(position, range, ring, speed, 2f, 0.25f);
            SurgeVfx.Flash(position, Consts.Values.Freeze.TintColor, 1.5f, range * 0.7f, 0.5f);
            SurgeVfx.Disc(position, Consts.Values.Vfx.FrostColor, range * 0.3f, range, 0.7f);
            SurgeVfx.Burst(position, Color.white, ring, 30, speed * 0.9f, speed, 1f, 2f, travel, travel, 1f, trails: true);
            SurgeVfx.Burst(position, Color.white, Consts.Values.Freeze.TintColor, 60, 4f, 16f, 0.5f, 1.1f, 1f, 1.8f, range * 0.9f, trails: false, gravity: -0.02f);
        }

        public static void Thaw(Vector3 position)
        {
            Shockwave.Expand(position, 12f, Consts.Values.Freeze.RingColor, 120f, 1.2f, 0.1f);
            SurgeVfx.Burst(position, Color.white, Consts.Values.Freeze.TintColor, 18, 40f, 80f, 0.6f, 1.2f, 0.4f, 0.7f, 2f, trails: true, gravity: 0.5f);
        }

        // Rewind: a gold ring closes on the caster and motes stream in from the edge of its range
        public static void Rewind(Transform body, float range)
        {
            Color gold = Consts.Values.Freeze.RewindRingColor;
            float time = Consts.Values.Freeze.RewindRingTime;
            Shockwave.Collapse(body, range, gold, time, 2f, () => SurgeVfx.Flash(body.position, gold, 2.5f, range * 0.5f, 0.4f));
            SurgeVfx.Burst(body.position, gold, Color.white, 60, -range / time, -range / time, 0.8f, 1.6f, time, time, range, trails: true, radiusThickness: 0f);
        }

        // Each enemy turned back into a wasp pops where it stood
        public static void RewindPop(Vector3 position)
        {
            Color gold = Consts.Values.Freeze.RewindRingColor;
            SurgeVfx.Flash(position, gold, 2f, 20f, 0.3f);
            SurgeVfx.Burst(position, Color.white, gold, 24, 20f, 60f, 0.8f, 1.6f, 0.4f, 0.8f, 3f, trails: false);
        }
    }

    // Pocket Dimension and Banish: purple
    public static class StorageVfx
    {
        // The held weapon folds into the hand
        public static void Send(Vector3 position)
        {
            Color purple = Consts.Values.Storage.RingColor;
            Shockwave.Collapse(Still(position), 14f, purple, 0.25f, 1.2f);
            SurgeVfx.Burst(position, purple, Color.white, 20, -56f, -56f, 0.6f, 1.2f, 0.25f, 0.25f, 14f, trails: true, radiusThickness: 0f);
            SurgeVfx.Flash(position, purple, 1.5f, 15f, 0.3f);
        }

        // A stored weapon unfolds where it comes back
        public static void Retrieve(Vector3 position)
        {
            Color purple = Consts.Values.Storage.RingColor;
            Shockwave.Expand(position, 18f, purple, 150f, 1.5f, 0.1f);
            SurgeVfx.Disc(position, Color.white, 1f, 8f, 0.2f, SurgeVfx.Order + 2);
            SurgeVfx.Burst(position, Color.white, purple, 24, 20f, 50f, 0.6f, 1.2f, 0.3f, 0.6f, 2f, trails: true);
            SurgeVfx.Flash(position, purple, 2f, 20f, 0.3f);
        }

        // Banish: a dark singularity opens on the caster while the ring and motes fall into it, then it snaps shut
        public static void Banish(Transform body, float range)
        {
            Color purple = Consts.Values.Storage.RingColor;
            float time = Consts.Values.Storage.BanishPullTime;
            float hold = time + 0.15f;
            SurgeVfx.Disc(body.position, purple, 0f, 13f, hold, SurgeVfx.Order + 8, pulse: true, follow: body, hard: true);
            SurgeVfx.Disc(body.position, Color.black, 0f, 9f, hold, SurgeVfx.Order + 9, pulse: true, follow: body, hard: true);
            SurgeVfx.Burst(body.position, purple, Color.white, 48, -range / time, -range / time, 0.8f, 1.6f, time, time, range, trails: true, radiusThickness: 0f);
            Shockwave.Collapse(body, range, purple, time, 2f, () =>
            {
                SurgeVfx.Flash(body.position, purple, 3f, range * 0.4f, 0.35f);
                SurgeVfx.Burst(body.position, Color.white, purple, 30, 40f, 120f, 0.6f, 1.4f, 0.3f, 0.6f, 2f, trails: true);
            });
        }

        private static Transform Still(Vector3 position)
        {
            GameObject anchor = new GameObject("SurgeAnchor");
            anchor.transform.position = position;
            Object.Destroy(anchor, 1f);
            return anchor.transform;
        }
    }

    // Bottomless Clip: the weapon smolders with rising embers and its own light for as long as the ammo lasts
    public static class ClipVfx
    {
        public static void Cast(Vector3 position)
        {
            Color color = Consts.Values.Vfx.ClipColor;
            Shockwave.Expand(position, 22f, color, 180f, 1.5f, 0.1f);
            SurgeVfx.Flash(position, color, 2f, 25f, 0.3f);
            SurgeVfx.Burst(position, Color.white, color, 20, 30f, 70f, 0.6f, 1.2f, 0.3f, 0.6f, 3f, trails: true);
        }

        // Care Package: each spawner shows the game's incoming-weapon particles and a white puff where the weapon lands
        public static void Drop(WeaponSpawner spawner)
        {
            if (spawner.preSpawnParticles != null) spawner.preSpawnParticles.Play();
            Vector3 position = spawner.transform.position;
            SurgeVfx.Disc(position, Color.white, 1f, 7f, 0.25f, SurgeVfx.Order + 2);
            SurgeVfx.Burst(position, Color.white, Color.white, 20, 8f, 14f, 0.4f, 1f, 0.8f, 1.2f, 5f, trails: false);
        }
    }

    public class WeaponAura : MonoBehaviour
    {
        private ParticleSystem embers;
        private LightFade glow;
        private Follow follow;

        public static WeaponAura Attach(Transform weapon)
        {
            GameObject holder = new GameObject("WeaponAura");
            holder.transform.position = weapon.position;
            WeaponAura aura = holder.AddComponent<WeaponAura>();
            aura.follow = holder.AddComponent<Follow>();
            aura.follow.Target = weapon;
            aura.follow.DestroyWithTarget = false;
            Color color = Consts.Values.Vfx.ClipColor;
            aura.embers = SurgeVfx.Stream(holder.transform, Vector3.zero, Color.white, color, 55f, 8f, 20f, 0.7f, 1.4f, 0.7f, 3f, gravity: -0.3f);
            aura.glow = SurgeVfx.Flash(weapon.position, color, 1.5f, 12f, float.MaxValue, holder.transform);
            return aura;
        }

        public void Retarget(Transform weapon)
        {
            if (follow != null) follow.Target = weapon;
            if (embers != null)
            {
                ParticleSystem.EmissionModule emission = embers.emission;
                emission.enabled = weapon != null;
            }
            if (glow != null) glow.gameObject.SetActive(weapon != null);
        }

        public void Stop()
        {
            SurgeVfx.Fade(embers);
            if (glow != null) Destroy(glow.gameObject);
            Destroy(gameObject);
        }
    }

    // Null Field: a golden bubble around the spider, with motes orbiting it; it ripples when it takes a hit and pops when it ends
    public class NullFieldBubble : MonoBehaviour
    {
        private const float Radius = 7f;
        private SpriteRenderer shell;
        private ParticleSystem motes;
        private Transform body;
        private float spriteSize;
        private float flash;
        private float ending;
        private bool popped;

        public static NullFieldBubble Attach(Transform body)
        {
            GameObject holder = new GameObject("NullFieldBubble");
            holder.transform.position = body.position;
            NullFieldBubble bubble = holder.AddComponent<NullFieldBubble>();
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
            bubble.shell.color = SurgeVfx.WithAlpha(gold, forceField != null ? Consts.Values.Vfx.NullFieldShellAlpha : 0.25f);
            bubble.motes = SurgeVfx.Stream(holder.transform, Vector3.zero, Color.white, gold, 14f, 2f, 5f, 0.5f, 0.9f, 1.2f, Radius, radiusThickness: 0.2f);
            bubble.flash = 1f;
            return bubble;
        }

        public void Hit()
        {
            flash = 1f;
            Shockwave.Expand(body != null ? body.position : transform.position, 20f, Consts.Values.Vfx.NullFieldColor, 160f, 1.5f, 0.1f);
            SurgeVfx.Flash(transform.position, Consts.Values.Vfx.NullFieldColor, 2f, 25f, 0.25f);
        }

        public void End()
        {
            if (popped) return;
            popped = true;
            ending = 0.001f;
            SurgeVfx.Fade(motes);
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
                    Color gold = Consts.Values.Vfx.NullFieldColor;
                    SurgeVfx.Burst(transform.position, Color.white, gold, 24, 30f, 70f, 0.6f, 1.2f, 0.4f, 0.7f, Radius * 0.5f, trails: true);
                    SurgeVfx.Flash(transform.position, gold, 2f, 25f, 0.3f);
                    Destroy(gameObject);
                    return;
                }
                scale = Radius * 2f / spriteSize * (1f - t * 0.6f);
            }
            else
            {
                float pulse = 1f + 0.05f * Mathf.Sin(Time.time * 6f);
                scale = Radius * 2f / spriteSize * pulse;
            }
            shell.transform.localScale = new Vector3(scale, scale, 1f);
            shell.transform.Rotate(0f, 0f, 40f * Time.deltaTime);

            flash = Mathf.MoveTowards(flash, 0f, Time.deltaTime * 4f);
            Color color = shell.color;
            float baseAlpha = SurgeVfx.ForceField != null ? Consts.Values.Vfx.NullFieldShellAlpha : 0.25f;
            color = Color.Lerp(Consts.Values.Vfx.NullFieldColor, Color.white, flash * 0.7f);
            color.a = Mathf.Lerp(baseAlpha, Mathf.Min(1f, baseAlpha * 3f), flash);
            shell.color = color;
        }
    }

    // Neural Backup: motes stream into the caster for the whole cast, brighter the closer it is to done
    public class ChannelEffect : MonoBehaviour
    {
        private ParticleSystem motes;
        private LightFade glow;

        public static ChannelEffect Attach(Transform body, float castTime)
        {
            GameObject holder = new GameObject("NeuralBackupChannel");
            holder.transform.position = body.position;
            ChannelEffect channel = holder.AddComponent<ChannelEffect>();
            Follow follow = holder.AddComponent<Follow>();
            follow.Target = body;
            Color color = Consts.Values.Vfx.ReviveColor;
            channel.motes = SurgeVfx.Stream(holder.transform, Vector3.zero, color, Color.white, 60f, -50f, -70f, 0.4f, 0.9f, 0.7f, 42f, trails: true, radiusThickness: 0f);
            channel.glow = SurgeVfx.Flash(body.position, color, 1.2f, 22f, float.MaxValue, holder.transform);
            return channel;
        }

        public void Finish(bool completed)
        {
            SurgeVfx.Fade(motes);
            if (glow != null) Destroy(glow.gameObject);
            if (completed)
            {
                Color color = Consts.Values.Vfx.ReviveColor;
                Shockwave.Expand(transform.position, 40f, color, 250f, 2f);
                SurgeVfx.Flash(transform.position, color, 2f, 32f, 0.4f);
                SurgeVfx.Burst(transform.position, Color.white, color, 40, 40f, 120f, 0.6f, 1.2f, 0.4f, 0.8f, 3f, trails: true);
            }
            Destroy(gameObject);
        }
    }

    public static class NullFieldVfx
    {
        // A revived spider comes back in a column of light, like a respawn turned up
        public static void Revive(Vector3 position)
        {
            Color color = Consts.Values.Vfx.ReviveColor;
            Shockwave.Expand(position, 30f, color, 200f, 2f);
            SurgeVfx.Disc(position, Color.white, 2f, 8f, 0.25f, SurgeVfx.Order + 2);
            SurgeVfx.Flash(position, color, 1.5f, 24f, 0.4f);
            SurgeVfx.Burst(position, Color.Lerp(color, Color.white, 0.5f), color, 36, -100f, -140f, 0.6f, 1.2f, 0.4f, 0.4f, 45f, trails: true, radiusThickness: 0f);
            SurgeVfx.Burst(position, Color.white, color, 30, 20f, 60f, 0.6f, 1.2f, 0.6f, 1f, 3f, trails: false, gravity: -0.2f);
        }

        public static void Summon(Vector3 position)
        {
            Color color = Consts.Values.Vfx.ReviveColor;
            SurgeVfx.Flash(position, color, 2f, 25f, 0.4f);
            SurgeVfx.Burst(position, Color.white, color, 24, 15f, 45f, 0.6f, 1.2f, 0.5f, 0.9f, 4f, trails: false);
        }
    }

    public static class ReadyVfx
    {
        // The ability coming off cooldown shows a small ring from the spider in the indicator's ready color
        public static void Pulse(Vector3 position, Color color)
        {
            Shockwave.Expand(position, 16f, color, 110f, 1.2f, 0.1f);
            SurgeVfx.Flash(position, color, 1.5f, 18f, 0.3f);
        }
    }
}
