using System.Collections.Generic;
using HarmonyLib;
using Interfaces;
using SpiderSurge.Common;
using SpiderSurge.Vfx;
using UnityEngine;
using Values = SpiderSurge.Consts.Values.Repulsor;

namespace SpiderSurge.Weapons
{
    // A Baby Shotgun whose blast shoves everything in its pellet cone like the Kinetic Pulse, and harms nothing
    public static class Repulsor
    {
        private const string SpriteResource = "SpiderSurge.RepulsorSprite";

        public static GameObject Prefab { get; private set; }

        public static bool Is(Weapon weapon) => weapon != null && weapon.serializationWeaponName == Values.Id;

        public static void EnsureCreated()
        {
            if (Prefab != null) return;
            var shotgun = CustomWeapons.FindPrefab<Shotgun>("Mini Shotgun");
            if (shotgun != null) Prefab = Build(shotgun);
        }

        private static GameObject Build(Shotgun source)
        {
            GameObject go = Object.Instantiate(source.gameObject, CustomWeapons.Holder);
            go.name = Values.Name;
            var gun = go.GetComponent<Shotgun>();
            gun.serializationWeaponName = Values.Id;
            gun.usedInCustomTiers = false;
            gun.maxAmmo = Values.Charges;
            Sprite sprite = CustomWeapons.LoadSprite(SpriteResource, Values.Name, source.GetComponent<SpriteRenderer>().sprite, Values.PixelsPerUnit);
            if (sprite != null) go.GetComponent<SpriteRenderer>().sprite = sprite;

            // No pellets and no bang: the blast pushes, and plays the pulse's effects instead
            foreach (DamageOnParticle pellets in go.GetComponentsInChildren<DamageOnParticle>(true)) Object.DestroyImmediate(pellets);
            foreach (ParticleSystem pellets in gun.shotParticles.GetComponentsInChildren<ParticleSystem>(true))
            {
                ParticleSystem.EmissionModule emission = pellets.emission;
                emission.enabled = false;
            }
            foreach (AudioSource bang in gun.shotSounds) bang.volume = 0f;
            CustomWeapons.ReplaceIndicator(go, gun.activeIndicator, "SpiderSurge.RepulsorReadySprite", Values.PixelsPerUnit);
            go.AddComponent<RepulsorBlast>();
            CustomPrefabNet.RegisterWeapon(go, Values.Name);
            return go;
        }
    }

    public class RepulsorBlast : MonoBehaviour
    {
        private static int _layers;

        private Collider2D[] _hits = new Collider2D[64];
        private readonly HashSet<EnemyHealthSystem> _launch = new HashSet<EnemyHealthSystem>();
        private readonly HashSet<int> _accents = new HashSet<int>();

        internal Weapon Weapon { get; private set; }

        private void Awake()
        {
            Weapon = GetComponent<Weapon>();
        }

        // The host pushes everything in the cone, as the Kinetic Pulse pushes everything in its ring. Stats and Teams take its holder as the attacker
        internal void Blast()
        {
            if (_layers == 0) _layers = LayerMask.GetMask("Player", "Item", "Weapon", "Enemy", "EnemyWeapon", "DynamicWorld");
            Vector2 origin = Weapon.point != null ? (Vector2)Weapon.point.position : (Vector2)transform.position;
            Vector2 aim = transform.up;
            float angle = Mathf.Atan2(aim.y, aim.x);
            RepulsorVfx.Draw(origin, angle);
            SurgeNet.SendFx(SurgeFx.RepulsorBlast, -1, origin, angle, false);

            int count;
            while ((count = Physics2D.OverlapCircleNonAlloc(origin, Values.Range, _hits, _layers)) == _hits.Length)
            {
                System.Array.Resize(ref _hits, _hits.Length * 2);
            }

            float strength = Consts.Values.Pulse.AbilityBaseKnockbackStrength * PulseKnockback.GravityStrengthMultiplier;
            _launch.Clear();
            _accents.Clear();
            // Taken before any push lands, as the pulse does
            for (int i = 0; i < count; i++)
            {
                if (!InCone(_hits[i], origin, aim, out _, out _)) continue;
                EnemyHealthSystem enemy = _hits[i].GetComponentInParent<EnemyHealthSystem>();
                if (PulseKnockback.CanLaunch(enemy)) _launch.Add(enemy);
            }

            for (int i = 0; i < count; i++)
            {
                Collider2D collider = _hits[i];
                if (!InCone(collider, origin, aim, out Vector2 point, out Vector2 direction) || IsHolders(collider)) continue;
                if (!collider.TryGetComponent(out IDamageable damageable)) damageable = collider.GetComponentInParent<IDamageable>();
                if (damageable == null) continue;

                Rigidbody2D body = collider.attachedRigidbody;
                if (body != null) body.velocity = Vector2.zero;
                damageable.Impact(direction * strength, point, true, false);
                var component = damageable as Component;
                if (component != null && _accents.Add(component.GetInstanceID()))
                {
                    SurgeVfx.Safely(static hit => AccentVfx.Impact(hit.point, hit.direction, false), (point, direction));
                    SurgeNet.SendFx(SurgeFx.PulseImpact, -1, point, Mathf.Atan2(direction.y, direction.x), false);
                }
            }

            float speed = Consts.Values.Pulse.AbilityBaseEnemyLaunchSpeed * PulseKnockback.GravityStrengthMultiplier;
            foreach (EnemyHealthSystem enemy in _launch)
            {
                if (enemy != null && !enemy.dead) PulseKnockback.LaunchEnemy(enemy, origin, speed);
            }
            _launch.Clear();
        }

        private static bool InCone(Collider2D collider, Vector2 origin, Vector2 aim, out Vector2 point, out Vector2 direction)
        {
            point = default;
            direction = aim;
            if (collider == null) return false;
            point = collider.ClosestPoint(origin);
            Vector2 offset = point - origin;
            float distance = offset.magnitude;
            if (distance > Values.Range) return false;
            if (distance < 0.5f) return true;
            direction = offset / distance;
            return Vector2.Angle(aim, offset) <= Values.ConeHalfAngle;
        }

        // The holder's spider and the Repulsor itself are never pushed
        private bool IsHolders(Collider2D collider)
        {
            if (collider.GetComponentInParent<Weapon>() == Weapon) return true;
            WeaponManager holder = Weapon.owner;
            if (holder == null || holder.healthSystem == null) return false;
            if (collider.GetComponentInParent<SpiderHealthSystem>() == holder.healthSystem) return true;
            PlayerController player = holder.healthSystem.GetComponentInParent<PlayerController>();
            if (player != null && collider.GetComponentInParent<PlayerController>() == player) return true;
            Weapon weapon = collider.GetComponentInParent<Weapon>();
            return weapon != null && weapon.equipped && weapon.owner == holder;
        }
    }

    // The game's shot still runs for the recoil, reload and charge count; its pellets are gone
    [HarmonyPatch(typeof(Shotgun), "Shoot")]
    internal static class Repulsor_Shoot_Patch
    {
        [HarmonyPrefix]
        private static void Prefix(Shotgun __instance)
        {
            if (!Net.IsAuthority || !Repulsor.Is(__instance)) return;
            RepulsorBlast blast = __instance.GetComponent<RepulsorBlast>();
            if (blast != null) blast.Blast();
        }
    }
}
