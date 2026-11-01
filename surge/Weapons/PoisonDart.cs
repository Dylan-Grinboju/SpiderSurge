using System.Collections.Generic;
using HarmonyLib;
using SpiderSurge.Common;
using SpiderSurge.Vfx;
using UnityEngine;
using UnityEngine.Experimental.Rendering.Universal;
using Values = SpiderSurge.Consts.Values.PoisonDart;

namespace SpiderSurge.Weapons
{
    // A one-shot launcher built from the Grenade Launcher. Its dart flies in an arc and dazes the enemy or spider it hits
    public static class PoisonDart
    {
        private const string LauncherResource = "SpiderSurge.PoisonDartSprite";
        private const string DartResource = "SpiderSurge.PoisonDartShotSprite";

        public static GameObject Prefab { get; private set; }
        private static bool _failed;

        public static bool Is(Weapon weapon) => weapon != null && weapon.serializationWeaponName == Values.Id;

        public static void EnsureCreated()
        {
            if (Prefab != null || _failed) return;

            var launcher = CustomWeapons.FindPrefab<ProjectileLauncher>("Grenade Launcher");
            var rocket = CustomWeapons.FindPrefab<BasicProjectile>("MiniRocket");
            if (launcher == null || rocket == null) return;

            Sprite launcherSprite = CustomWeapons.LoadSprite(LauncherResource, Values.Name, launcher.GetComponent<SpriteRenderer>().sprite, Values.LauncherPixelsPerUnit);
            Sprite dartSprite = CustomWeapons.LoadSprite(DartResource, Values.ShotName, null, Values.DartPixelsPerUnit);
            if (launcherSprite == null || dartSprite == null)
            {
                _failed = true;
                return;
            }

            GameObject dart = BuildDart(rocket, dartSprite);
            Prefab = BuildLauncher(launcher, launcherSprite, dart);
        }

        private static GameObject BuildLauncher(ProjectileLauncher source, Sprite sprite, GameObject dart)
        {
            GameObject go = Object.Instantiate(source.gameObject, CustomWeapons.Holder);
            go.name = Values.Name;
            var launcher = go.GetComponent<ProjectileLauncher>();
            launcher.serializationWeaponName = Values.Id;
            launcher.type = new List<Weapon.WeaponType> { Weapon.WeaponType.Gun };
            launcher.usedInCustomTiers = false;
            launcher.maxAmmo = 1f;
            launcher.projectile = dart;
            launcher.shotForce = Values.ShotForce;
            launcher.rotationForce = 0f;
            go.GetComponent<SpriteRenderer>().sprite = sprite;
            // The ready light glows poison green instead of the launcher's orange
            if (launcher.readyIndicator != null)
            {
                foreach (SpriteRenderer light in launcher.readyIndicator.GetComponentsInChildren<SpriteRenderer>(true))
                {
                    light.color = SurgeVfx.SetAlpha(Values.Color, light.color.a);
                }
            }
            CustomPrefabNet.RegisterWeapon(go, Values.Name);
            return go;
        }

        private static GameObject BuildDart(BasicProjectile rocket, Sprite sprite)
        {
            GameObject go = Object.Instantiate(rocket.gameObject, CustomWeapons.Holder);
            go.name = Values.ShotName;
            var projectile = go.GetComponent<BasicProjectile>();
            projectile.movementForce = 0f;
            projectile.destroyAfter = Values.DartLifetime;
            projectile.destroyOnCollision = true;
            projectile.destroyEffect = BuildPuff();
            projectile.rb2d.gravityScale = 1f;
            // A wasp's body is about as wide as the dart's travel in one physics step
            projectile.rb2d.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            go.GetComponent<SpriteRenderer>().sprite = sprite;
            var box = go.GetComponent<BoxCollider2D>();
            if (box != null)
            {
                box.size = Values.DartCollider;
                box.offset = Vector2.zero;
            }
            // A dart has no rocket flame or engine
            foreach (ParticleSystem flame in go.GetComponentsInChildren<ParticleSystem>(true))
            {
                ParticleSystem.EmissionModule emission = flame.emission;
                emission.enabled = false;
                ParticleSystem.MainModule main = flame.main;
                main.playOnAwake = false;
                var renderer = flame.GetComponent<ParticleSystemRenderer>();
                if (renderer != null) renderer.enabled = false;
            }
            foreach (PlayRandomSound engineSound in go.GetComponentsInChildren<PlayRandomSound>(true)) engineSound.playOnStart = false;
            foreach (AudioSource engine in go.GetComponentsInChildren<AudioSource>(true)) engine.enabled = false;
            foreach (Light2D glow in go.GetComponentsInChildren<Light2D>(true)) glow.enabled = false;
            go.AddComponent<PoisonDartShot>();
            CustomPrefabNet.RegisterWeapon(go, Values.ShotName);
            return go;
        }

        // Every peer draws it where the dart ends, from the projectile's own destroy message
        private static GameObject BuildPuff()
        {
            var puff = new GameObject(Values.ShotName + " Puff");
            puff.transform.SetParent(CustomWeapons.Holder, false);
            puff.AddComponent<PoisonDartPuff>();
            return puff;
        }

        // The dart and the enemy or player it dazed, on the host
        public static event System.Action<Component, Component> Dazed;
        private const string ListenerFailed = "SpiderSurge Daze Dart listener failed";

        // The host decides what the dart hit: an enemy or another spider is dazed through its shield, which holds
        internal static void Hit(Component dart, Collider2D collider, PlayerController shooter)
        {
            if (collider == null) return;
            EnemyHealthSystem enemy = collider.GetComponentInParent<EnemyHealthSystem>();
            if (enemy == null)
            {
                WaspStinger stinger = collider.GetComponentInParent<WaspStinger>();
                if (stinger != null) enemy = stinger.enemyHealthSystem;
            }
            if (enemy != null)
            {
                if (enemy.IsDead()) return;
                if (DazedEnemy.Daze(enemy, Values.DazeDuration)) Listeners.Raise(Dazed, dart, (Component)enemy, ListenerFailed);
                return;
            }

            // A dart in a spider's held weapon reaches the spider holding it
            Weapon held = collider.GetComponentInParent<Weapon>();
            var holder = held != null && held.equipped && held.owner != null ? held.owner.healthSystem as SpiderHealthSystem : null;
            PlayerController player = holder != null ? holder.GetComponentInParent<PlayerController>() : collider.GetComponentInParent<PlayerController>();
            SpiderHealthSystem spider = player != null ? player.spiderHealthSystem : null;
            if (spider == null || player == shooter || spider.dead || spider.astralDead || Immune(spider)) return;
            if (shooter != null && SpiderTeams.FriendlyFireGate.SparesTeammate(shooter.playerID.Value, spider)) return;
            if (DazedSpider.Daze(spider, Values.DazeDuration)) Listeners.Raise(Dazed, dart, (Component)player, ListenerFailed);
        }

        private static readonly AccessTools.FieldRef<SpiderHealthSystem, float> ImmuneTill = AccessTools.FieldRefAccess<SpiderHealthSystem, float>("_immuneTill");

        // A spider no hit can hurt, such as one just spawned or under Immunity, shrugs the dart off
        private static bool Immune(SpiderHealthSystem spider)
        {
            if (Time.time < ImmuneTill(spider)) return true;
            ImmuneAbility immunity = ImmuneAbility.GetByHealthSystem(spider);
            return immunity != null && immunity.IsImmune;
        }
    }

    // The dart points where it flies, and the host dazes whatever it first hits
    public class PoisonDartShot : MonoBehaviour
    {
        internal static ProjectileLauncher Launching;

        private Rigidbody2D _body;
        private PlayerController _shooter;
        private bool _spent;

        private void Awake()
        {
            _body = GetComponent<Rigidbody2D>();
            WeaponManager owner = Launching != null ? Launching.owner : null;
            if (owner != null) _shooter = owner.GetComponentInParent<PlayerController>();
        }

        private void FixedUpdate()
        {
            if (!Net.IsAuthority || _body == null || _spent) return;
            Vector2 velocity = _body.velocity;
            if (velocity.sqrMagnitude < 1f) return;
            _body.angularVelocity = 0f;
            _body.MoveRotation(Mathf.Atan2(velocity.y, velocity.x) * Mathf.Rad2Deg - 90f);
        }

        private void OnCollisionEnter2D(Collision2D collision)
        {
            if (!Net.IsAuthority || _spent) return;
            _spent = true;
            PoisonDart.Hit(this, collision.collider, _shooter);
        }
    }

    public class PoisonDartPuff : MonoBehaviour
    {
        private void Start()
        {
            SurgeVfx.Burst(transform.position, Values.Color, Color.white, 10, 6f, 20f, 0.4f, 0.8f, 0.2f, 0.4f, 1f, false);
            Destroy(gameObject);
        }
    }

    // The launcher's shot is made inside Shoot, so the dart learns who fired it there
    [HarmonyPatch(typeof(ProjectileLauncher), "Shoot")]
    internal static class PoisonDart_Shoot_Patch
    {
        [HarmonyPrefix]
        private static void Prefix(ProjectileLauncher __instance) => PoisonDartShot.Launching = __instance;

        [HarmonyFinalizer]
        private static void Finalizer() => PoisonDartShot.Launching = null;
    }
}
