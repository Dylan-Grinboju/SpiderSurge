using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using SpiderSurge.Common;
using SpiderSurge.Vfx;
using Unity.Netcode;
using UnityEngine;
using Values = SpiderSurge.Consts.Values.PoisonGrenade;

namespace SpiderSurge.Weapons
{
    // A Grenade that bursts into a cloud of poison gas, which stays where it burst and kills like the Hornet Shaman's cloud
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
            Prefab = Build(grenade, BuildCloud(cloud, CustomWeapons.FindPrefab<Flare>("Flare")));
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
            // The fuse flashes a green glow, as the Grenade's flashes red
            if (grenade.lights != null)
            {
                var fuse = new GameObject("FuseLight");
                SurgeVfx.PointLight(fuse, Values.Color, Values.FuseLightIntensity, Values.FuseLightRadius);
                fuse.transform.SetParent(grenade.lights.transform, false);
                fuse.transform.position = go.transform.position;
            }
            CustomPrefabNet.RegisterWeapon(go, Values.Name);
            return go;
        }

        // The Shaman's cloud keeps only its sound sources, so they play through the game's mixer: the Flare's ignition and hiss replace its thunder
        private static GameObject BuildCloud(DeathCloud source, Flare flare)
        {
            GameObject go = Object.Instantiate(source.gameObject, CustomWeapons.Holder);
            go.name = Values.CloudName;
            DeathCloud vanilla = go.GetComponent<DeathCloud>();
            PoisonCloud cloud = go.AddComponent<PoisonCloud>();
            cloud.Take(vanilla, flare);
            foreach (NetworkBehaviour behaviour in go.GetComponentsInChildren<NetworkBehaviour>(true)) Object.DestroyImmediate(behaviour);
            foreach (NetworkObject networkObject in go.GetComponentsInChildren<NetworkObject>(true)) Object.DestroyImmediate(networkObject);
            foreach (Collider2D trigger in go.GetComponentsInChildren<Collider2D>(true)) Object.DestroyImmediate(trigger);
            foreach (Rigidbody2D body in go.GetComponentsInChildren<Rigidbody2D>(true)) Object.DestroyImmediate(body);
            // Its lightning, zaps and glow are leaf children
            foreach (ParticleSystem system in go.GetComponentsInChildren<ParticleSystem>(true)) if (system.gameObject != go) Object.DestroyImmediate(system.gameObject);
            foreach (SpriteRenderer sprite in go.GetComponentsInChildren<SpriteRenderer>(true)) if (sprite.gameObject != go) Object.DestroyImmediate(sprite.gameObject);
            return go;
        }
    }

    // Each peer runs its own cloud for the gas and sounds; the host's copy kills whatever stays inside it, like the Shaman's cloud
    public class PoisonCloud : MonoBehaviour
    {
        internal static Grenade Bursting;
        private static readonly Dictionary<ulong, PoisonCloud> Active = new Dictionary<ulong, PoisonCloud>();
        private static ulong? replaying;
        private ulong? identity;
        private bool authoritative, replay;

        [SerializeField] private AudioSource burstSound;
        [SerializeField] private AudioSource baseLoop;

        private static int _layers;
        private static Collider2D[] _hits = new Collider2D[64];
        private readonly Dictionary<Component, float> _inRange = new Dictionary<Component, float>();
        private readonly HashSet<Component> _struck = new HashSet<Component>();
        private readonly HashSet<Component> _seen = new HashSet<Component>();
        private float _until;
        private bool _dying;
        private PoisonGas _gas;

        internal PlayerController Owner { get; private set; }
        internal Weapon Source { get; private set; }

        internal void Take(DeathCloud cloud, Flare flare)
        {
            burstSound = cloud.zapSound;
            baseLoop = cloud.baseLoop;
            if (cloud.killSound != null) cloud.killSound.enabled = false;
            if (burstSound != null) burstSound.clip = flare != null && flare.triggerSound != null ? flare.triggerSound.clip : null;
            if (baseLoop == null) return;
            if (flare != null && flare.flareSound != null) baseLoop.clip = flare.flareSound.clip;
            else baseLoop.enabled = false;
        }

        private void Awake()
        {
            replay = replaying.HasValue;
            authoritative = Net.IsAuthority && !replay;
            identity = replaying ?? (Bursting != null ? (ulong?)Bursting.NetworkObjectId : null);
            if (identity.HasValue)
            {
                if (Active.TryGetValue(identity.Value, out var existing) && existing != null)
                {
                    enabled = false;
                    Destroy(gameObject);
                    return;
                }
                Active[identity.Value] = this;
            }
            transform.rotation = Quaternion.identity;
            _until = Time.time + Values.CloudDuration;
            // Like the Grenade's blast, the cloud is the player's who armed it, or else its last holder's
            if (Bursting != null) Owner = Owners.ExplosiveOwner(Bursting);
            Source = Bursting;
            if (_layers == 0) _layers = LayerMask.GetMask("Player", "Enemy");
        }

        private void Start()
        {
            _gas = PoisonGas.Burst(transform, Values.KillRadius, !replay);
            if (!replay && burstSound != null && burstSound.clip != null)
            {
                burstSound.pitch = Random.Range(0.8f, 0.9f);
                burstSound.Play();
            }
        }

        private void FixedUpdate()
        {
            if (_dying) return;
            if (Time.time >= _until)
            {
                _dying = true;
                if (_gas != null) _gas.Fade(Values.FadeTime);
                Destroy(gameObject, Values.FadeTime);
                return;
            }
            if (baseLoop != null) baseLoop.volume = Mathf.Lerp(baseLoop.volume, 0.2f, 0.1f);

            int count;
            while ((count = Physics2D.OverlapCircleNonAlloc(transform.position, Values.KillRadius, _hits, _layers)) == _hits.Length)
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

            if (authoritative) Poison(target, position);
            // The kill effect plays once; the host keeps poisoning whatever a shield, immunity or a teammate's spared hit kept alive
            if (_struck.Add(target)) PoisonGas.Poisoned(position);
        }

        private void Leave(Component target)
        {
            _inRange.Remove(target);
            _struck.Remove(target);
        }

        private void OnDestroy()
        {
            if (identity.HasValue && Active.TryGetValue(identity.Value, out var cloud) && cloud == this) Active.Remove(identity.Value);
        }

        internal static void ClearRemote()
        {
            foreach (var cloud in new List<PoisonCloud>(Active.Values))
                if (cloud != null && !cloud.authoritative) { cloud.enabled = false; Destroy(cloud.gameObject); }
            Active.Clear();
        }

        internal static void WriteSnapshot(BinaryWriter writer)
        {
            int count = 0;
            foreach (var cloud in Active.Values) if (cloud != null && cloud.authoritative && !cloud._dying && cloud._until > Time.time) count++;
            writer.Write(count);
            foreach (var pair in Active)
            {
                var cloud = pair.Value;
                if (cloud == null || !cloud.authoritative || cloud._dying || cloud._until <= Time.time) continue;
                writer.Write(pair.Key);
                writer.Write(cloud.transform.position);
                writer.Write(cloud._until - Time.time);
            }
        }

        internal static void ReadSnapshot(BinaryReader reader)
        {
            int count = reader.ReadInt32();
            if (count < 0 || count > 4096) throw new InvalidDataException("Invalid poison cloud count");
            for (int i = 0; i < count; i++)
            {
                ulong id = reader.ReadUInt64();
                Vector3 position = reader.ReadVector3();
                float remaining = reader.ReadSingle();
                if (remaining <= 0f) continue;
                if (!Active.TryGetValue(id, out var cloud) || cloud == null)
                {
                    PoisonGrenade.EnsureCreated();
                    if (PoisonGrenade.Prefab == null) continue;
                    var previous = replaying;
                    replaying = id;
                    try { cloud = Instantiate(PoisonGrenade.Prefab.GetComponent<Grenade>().explosion, position, Quaternion.identity).GetComponent<PoisonCloud>(); }
                    finally { replaying = previous; }
                }
                if (!cloud._dying) cloud._until = Time.time + Mathf.Min(remaining, Values.CloudDuration);
            }
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
