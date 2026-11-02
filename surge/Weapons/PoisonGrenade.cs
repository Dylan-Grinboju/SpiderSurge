using System.Collections.Generic;
using HarmonyLib;
using SpiderSurge.Common;
using SpiderSurge.Vfx;
using Unity.Netcode;
using UnityEngine;
using Values = SpiderSurge.Consts.Values.PoisonGrenade;

namespace SpiderSurge.Weapons
{
    // A Grenade that bursts into a blue copy of the Hornet Shaman's cloud, which stays where it burst
    public static class PoisonGrenade
    {
        private const string SpriteResource = "SpiderSurge.PoisonGrenadeSprite";

        public static GameObject Prefab { get; private set; }

        public static bool Is(Weapon weapon) => weapon != null && weapon.serializationWeaponName == Values.Id;

        public static void EnsureCreated()
        {
            if (Prefab != null) return;
            var grenade = CustomWeapons.FindPrefab<Grenade>("Grenade");
            var cloud = CustomWeapons.FindPrefab<DeathCloud>("DeathSmoke");
            if (grenade == null || cloud == null) return;
            Prefab = Build(grenade, BuildCloud(cloud));
        }

        private static GameObject Build(Grenade source, GameObject cloud)
        {
            GameObject go = Object.Instantiate(source.gameObject, CustomWeapons.Holder);
            go.name = Values.Name;
            var grenade = go.GetComponent<Grenade>();
            grenade.serializationWeaponName = Values.Id;
            grenade.usedInCustomTiers = false;
            // Every peer makes its own cloud where the grenade bursts, as it makes the Grenade's explosion
            grenade.explosion = cloud;
            Sprite sprite = CustomWeapons.LoadSprite(SpriteResource, Values.Name, source.GetComponent<SpriteRenderer>().sprite, Values.PixelsPerUnit);
            if (sprite != null) go.GetComponent<SpriteRenderer>().sprite = sprite;
            Transform inactiveLights = go.transform.Find("InactiveLights");
            if (inactiveLights != null) inactiveLights.gameObject.SetActive(false);
            CustomWeapons.ReplaceIndicator(go, grenade.lights, "SpiderSurge.PoisonGrenadeArmedSprite", Values.PixelsPerUnit);
            CustomPrefabNet.RegisterWeapon(go, Values.Name);
            return go;
        }

        // The Shaman's cloud without its network parts or chase, recoloured blue
        private static GameObject BuildCloud(DeathCloud source)
        {
            GameObject go = Object.Instantiate(source.gameObject, CustomWeapons.Holder);
            go.name = Values.CloudName;
            DeathCloud vanilla = go.GetComponent<DeathCloud>();
            PoisonCloud cloud = go.AddComponent<PoisonCloud>();
            cloud.Take(vanilla);
            foreach (NetworkBehaviour behaviour in go.GetComponentsInChildren<NetworkBehaviour>(true)) Object.DestroyImmediate(behaviour);
            foreach (NetworkObject networkObject in go.GetComponentsInChildren<NetworkObject>(true)) Object.DestroyImmediate(networkObject);
            foreach (Collider2D trigger in go.GetComponentsInChildren<Collider2D>(true)) Object.DestroyImmediate(trigger);
            foreach (Rigidbody2D body in go.GetComponentsInChildren<Rigidbody2D>(true)) Object.DestroyImmediate(body);

            foreach (ParticleSystem system in go.GetComponentsInChildren<ParticleSystem>(true)) Recolor(system);
            foreach (SpriteRenderer sprite in go.GetComponentsInChildren<SpriteRenderer>(true)) sprite.color = Blue(sprite.color);
            return go;
        }

        private static Color Blue(Color color)
        {
            Color.RGBToHSV(color, out _, out float saturation, out float value);
            Color blue = Color.HSVToRGB(Values.Hue, saturation, value);
            blue.a = color.a;
            return blue;
        }

        private static void Recolor(ParticleSystem system)
        {
            ParticleSystem.MainModule main = system.main;
            ParticleSystem.MinMaxGradient start = main.startColor;
            start.color = Blue(start.color);
            start.colorMin = Blue(start.colorMin);
            start.colorMax = Blue(start.colorMax);
            if (start.gradient != null) start.gradient = Blue(start.gradient);
            if (start.gradientMin != null) start.gradientMin = Blue(start.gradientMin);
            if (start.gradientMax != null) start.gradientMax = Blue(start.gradientMax);
            main.startColor = start;

            ParticleSystem.ColorOverLifetimeModule overLife = system.colorOverLifetime;
            ParticleSystem.MinMaxGradient life = overLife.color;
            life.color = Blue(life.color);
            if (life.gradient != null) life.gradient = Blue(life.gradient);
            if (life.gradientMin != null) life.gradientMin = Blue(life.gradientMin);
            if (life.gradientMax != null) life.gradientMax = Blue(life.gradientMax);
            overLife.color = life;
        }

        private static Gradient Blue(Gradient gradient)
        {
            GradientColorKey[] keys = gradient.colorKeys;
            for (int i = 0; i < keys.Length; i++) keys[i].color = Blue(keys[i].color);
            var blue = new Gradient { mode = gradient.mode };
            blue.SetKeys(keys, gradient.alphaKeys);
            return blue;
        }
    }

    // Each peer runs its own cloud for the zaps and sounds; the host's copy kills whatever stays inside it, like the Shaman's cloud
    public class PoisonCloud : MonoBehaviour
    {
        internal static Grenade Bursting;

        [SerializeField] private ParticleSystem zap;
        [SerializeField] private AudioSource zapSound;
        [SerializeField] private AudioSource killSound;
        [SerializeField] private AudioSource baseLoop;
        [SerializeField] private ParticleSystem killPs;
        [SerializeField] private ParticleSystem mainEffect;
        [SerializeField] private SpriteRenderer glow;

        private static int _layers;
        private static Collider2D[] _hits = new Collider2D[64];
        private readonly Dictionary<Component, float> _inRange = new Dictionary<Component, float>();
        private readonly HashSet<Component> _struck = new HashSet<Component>();
        private readonly HashSet<Component> _seen = new HashSet<Component>();
        private float _until;
        private bool _dying;

        internal PlayerController Owner { get; private set; }

        internal void Take(DeathCloud cloud)
        {
            zap = cloud.zap;
            zapSound = cloud.zapSound;
            killSound = cloud.killSound;
            baseLoop = cloud.baseLoop;
            killPs = cloud.killPs;
            mainEffect = cloud.mainEffect;
            glow = cloud.glow;
        }

        private void Awake()
        {
            transform.rotation = Quaternion.identity;
            _until = Time.time + Values.CloudDuration;
            // Like the Grenade's blast, the cloud is the player's who armed it, or else its last holder's
            if (Bursting != null) Owner = Owners.ExplosiveOwner(Bursting);
            if (_layers == 0) _layers = LayerMask.GetMask("Player", "Enemy");
        }

        private void FixedUpdate()
        {
            if (_dying) return;
            if (Time.time >= _until)
            {
                _dying = true;
                if (mainEffect != null) mainEffect.Stop();
                Destroy(gameObject, Values.FadeTime);
                return;
            }
            if (baseLoop != null) baseLoop.volume = Mathf.Lerp(baseLoop.volume, 0.2f, 0.1f);

            int count;
            while ((count = Physics2D.OverlapCircleNonAlloc(transform.position, Values.ZapRadius, _hits, _layers)) == _hits.Length)
            {
                System.Array.Resize(ref _hits, _hits.Length * 2);
            }
            _seen.Clear();
            for (int i = 0; i < count; i++)
            {
                Component target = Target(_hits[i]);
                if (target == null || !_seen.Add(target)) continue;
                Touch(target);
            }
            // Anything that left the kill radius starts over
            if (_inRange.Count > 0)
            {
                var gone = new List<Component>();
                foreach (Component target in _inRange.Keys) if (target == null || !_seen.Contains(target)) gone.Add(target);
                foreach (Component target in gone) Leave(target);
            }
        }

        private void Update()
        {
            if (!_dying) return;
            if (glow != null) glow.color = Color.Lerp(glow.color, Color.clear, 0.05f);
            if (baseLoop != null) baseLoop.volume = Mathf.Lerp(baseLoop.volume, 0f, 0.05f);
        }

        // A living spider or enemy, once per body
        private static Component Target(Collider2D collider)
        {
            if (collider == null) return null;
            SpiderHealthSystem spider = collider.GetComponentInParent<SpiderHealthSystem>();
            if (spider != null) return spider.dead || spider.astralDead ? null : spider;
            EnemyHealthSystem enemy = collider.GetComponentInParent<EnemyHealthSystem>();
            return enemy != null && !enemy.dead ? enemy : null;
        }

        private void Touch(Component target)
        {
            Vector3 position = target.transform.position;
            float distance = Vector2.Distance(position, transform.position);
            if (zap != null && Random.Range(0, (int)distance) < Values.KillRadius / 5f)
            {
                zap.transform.right = -(position - transform.position);
                zap.transform.position = position;
                zap.Emit(1);
                if (zapSound != null)
                {
                    zapSound.pitch = Random.Range(0.9f, 1.1f);
                    zapSound.Play();
                }
            }

            if (distance >= Values.KillRadius)
            {
                Leave(target);
                return;
            }
            if (!_inRange.TryGetValue(target, out float since))
            {
                _inRange[target] = Time.time;
                return;
            }
            if (Time.time - since <= Values.KillTime) return;

            if (Net.IsAuthority) Poison(target, position);
            // The kill effect plays once; the host keeps poisoning whatever a shield, immunity or a teammate's spared hit kept alive
            if (!_struck.Add(target)) return;
            if (killSound != null)
            {
                killSound.pitch = Random.Range(0.9f, 1.1f);
                killSound.Play();
            }
            if (killPs != null) killPs.Play();
        }

        private void Leave(Component target)
        {
            _inRange.Remove(target);
            _struck.Remove(target);
        }

        // Stats and Teams take the thrower as the attacker
        internal void Poison(Component target, Vector3 position)
        {
            if (target is SpiderHealthSystem spider) spider.Damage(Vector3.zero, position, false);
            else if (target is EnemyHealthSystem enemy)
            {
                if (!enemy.ignoreDirectDamage)
                {
                    enemy.Damage(Vector3.zero, position, false);
                    return;
                }
                // An Exploding Roller shrugs off direct hits; the cloud kills it as a hit would, after a touch that marks who killed it.
                // Disintegrate would count as a lava death and unlock Environmental Warfare
                enemy.Impact(Vector3.zero, position, false);
                enemy.Explode();
            }
        }
    }

    // The game makes the burst inside ExplodeClientRpc on every peer, so the cloud learns which grenade made it there
    [HarmonyPatch(typeof(Grenade), "ExplodeClientRpc")]
    internal static class PoisonGrenade_Explode_Patch
    {
        [HarmonyPrefix]
        private static void Prefix(Grenade __instance) => PoisonCloud.Bursting = __instance;

        [HarmonyFinalizer]
        private static void Finalizer() => PoisonCloud.Bursting = null;
    }
}
