using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Unity.Netcode;
using UnityEngine;

namespace SpiderSurge
{
    // A shot held by Freeze Time: it stops where it is with its lifetime paused, still hurts on contact, and flies on with its old velocity when it thaws
    public class FrozenShot : MonoBehaviour
    {
        private const string Disintegrate = "Disintegrate";
        private static readonly HashSet<int> FrozenIds = new HashSet<int>();
        private static readonly Dictionary<SawDisc, float> DiscStarts = new Dictionary<SawDisc, float>();
        private static readonly AccessTools.FieldRef<BasicProjectile, float> SpawnedAt = AccessTools.FieldRefAccess<BasicProjectile, float>("_spawnedAt");
        private static readonly AccessTools.FieldRef<SawDiscAttractor, GameObject> GravityZone = AccessTools.FieldRefAccess<SawDiscAttractor, GameObject>("attractor");
        private static readonly AccessTools.FieldRef<SawDiscAttractor, float> GravitySpawnedAt = AccessTools.FieldRefAccess<SawDiscAttractor, float>("_spawnTime");

        private int _id;
        private float _thawTime;
        private Rigidbody2D _body;
        private Vector2 _velocity;
        private float _angularVelocity;
        private RigidbodyConstraints2D _constraints;
        private SawDisc _disc;
        private float? _discLifeLeft;
        private SawDiscAttractor _gravity;
        private float _frozenAt;
        private readonly List<Collider2D> _pull = new List<Collider2D>();
        private readonly Dictionary<SpriteRenderer, (Color original, Color shown)> _tints = new Dictionary<SpriteRenderer, (Color, Color)>();

        public float TimeLeft => Mathf.Max(0f, _thawTime - Time.time);

        public static bool IsFrozen(Component shot) => shot != null && FrozenIds.Contains(shot.gameObject.GetInstanceID());

        // Every shot and effect in the area at the cast, whoever fired it; held weapons and spiders keep theirs, so a Shotgun still fires
        public static void FreezeAround(Vector2 origin, float range, float duration)
        {
            float until = Time.time + duration;
            foreach (BasicProjectile shot in Object.FindObjectsOfType<BasicProjectile>()) TryFreeze(shot, shot.rb2d, origin, range, until);
            foreach (RailShot shot in Object.FindObjectsOfType<RailShot>()) TryFreeze(shot, shot.GetComponent<Rigidbody2D>(), origin, range, until);
            foreach (SawDisc disc in Object.FindObjectsOfType<SawDisc>()) TryFreeze(disc, disc.rb2d, origin, range, until);
            FrozenCloud.FreezeAround(origin, range, until);
            FrozenParticles.FreezeAround(origin, range, until);
        }

        internal static void RecordStart(SawDisc disc)
        {
            foreach (SawDisc gone in DiscStarts.Keys.Where(key => key == null).ToList()) DiscStarts.Remove(gone);
            DiscStarts[disc] = Time.time;
        }

        // Called from the projectiles' FixedUpdate; a held shot's clock waits with it
        internal static bool Hold(BasicProjectile shot)
        {
            if (!IsFrozen(shot)) return false;
            SpawnedAt(shot) += Time.fixedDeltaTime;
            return true;
        }

        private static void TryFreeze(Component shot, Rigidbody2D body, Vector2 origin, float range, float until)
        {
            if (shot == null || body == null || !shot.gameObject.activeInHierarchy) return;
            if (Vector2.Distance(origin, body.position) > range) return;

            FrozenShot frozen = Apply(shot.gameObject, body, shot as SawDisc, until);
            SurgeNet.SendEnemyFx(EnemyFx.ShotFrozen, shot.GetComponent<NetworkObject>(), frozen.TimeLeft);
        }

        // Online clients hold it too: its position syncs from the host, but a Gravity Saw pulls each spider on its own player's machine
        public static void ShowFrozen(NetworkObject target, float duration)
        {
            Rigidbody2D body = target.GetComponent<Rigidbody2D>();
            if (body == null || duration <= 0f || !target.gameObject.activeInHierarchy) return;
            Apply(target.gameObject, body, target.GetComponent<SawDisc>(), Time.time + duration);
        }

        private static FrozenShot Apply(GameObject shot, Rigidbody2D body, SawDisc disc, float until)
        {
            FrozenShot frozen = shot.GetComponent<FrozenShot>();
            if (frozen != null)
            {
                frozen._thawTime = Mathf.Max(frozen._thawTime, until);
            }
            else
            {
                frozen = shot.AddComponent<FrozenShot>();
                frozen.Hold(body, disc, until);
            }
            FrozenParticles.FreezeUnder(shot, frozen._thawTime);
            return frozen;
        }

        private void Hold(Rigidbody2D body, SawDisc disc, float until)
        {
            _id = gameObject.GetInstanceID();
            FrozenIds.Add(_id);
            _thawTime = until;
            _body = body;
            _velocity = body.velocity;
            _angularVelocity = body.angularVelocity;
            _constraints = body.constraints;
            body.velocity = Vector2.zero;
            body.angularVelocity = 0f;
            body.constraints = RigidbodyConstraints2D.FreezeAll;

            _frozenAt = Time.time;
            _disc = disc;
            HoldGravity(disc as SawDiscAttractor);
            if (disc != null && disc.IsInvoking(Disintegrate) && DiscStarts.TryGetValue(disc, out float start))
            {
                disc.CancelInvoke(Disintegrate);
                _discLifeLeft = Mathf.Max(0f, disc.lifeTime - (Time.time - start));
            }

            foreach (SpriteRenderer sprite in GetComponentsInChildren<SpriteRenderer>(true))
            {
                Color tinted = Color.Lerp(sprite.color, Consts.Values.Freeze.TintColor, Consts.Values.Freeze.TintStrength);
                tinted.a = sprite.color.a;
                _tints[sprite] = (sprite.color, tinted);
                sprite.color = tinted;
            }
        }

        // A Gravity Saw stops pulling, and one not yet open waits as long as it was held before it opens
        private void HoldGravity(SawDiscAttractor gravity)
        {
            if (gravity == null) return;
            _gravity = gravity;
            GameObject zone = GravityZone(gravity);
            if (zone == null || !zone.activeSelf) return;
            foreach (Collider2D pull in zone.GetComponentsInChildren<Collider2D>())
            {
                if (!pull.enabled) continue;
                pull.enabled = false;
                _pull.Add(pull);
            }
        }

        private void Update()
        {
            if (Time.time < _thawTime) return;
            Thaw();
            Destroy(this);
        }

        private void Thaw()
        {
            FrozenIds.Remove(_id);
            if (_body != null)
            {
                _body.constraints = _constraints;
                _body.velocity = _velocity;
                _body.angularVelocity = _angularVelocity;
            }
            if (_disc != null && _discLifeLeft.HasValue)
            {
                DiscStarts[_disc] = Time.time - (_disc.lifeTime - _discLifeLeft.Value);
                _disc.Invoke(Disintegrate, _discLifeLeft.Value);
                _discLifeLeft = null;
            }
            if (_gravity != null)
            {
                GravitySpawnedAt(_gravity) += Time.time - _frozenAt;
                foreach (Collider2D pull in _pull)
                {
                    if (pull != null) pull.enabled = true;
                }
                _pull.Clear();
            }
            foreach (var pair in _tints)
            {
                if (pair.Key != null && pair.Key.color == pair.Value.shown) pair.Key.color = pair.Value.original;
            }
            _tints.Clear();
        }

        private void OnDestroy()
        {
            FrozenIds.Remove(_id);
        }
    }

    // A particle system held by Freeze Time: its particles stop in place and it carries on from there when it thaws
    public class FrozenParticles : MonoBehaviour
    {
        private ParticleSystem _particles;
        private float _speed;
        private float _thawTime;

        public static void Freeze(ParticleSystem particles, float until)
        {
            if (particles == null) return;
            FrozenParticles frozen = particles.GetComponent<FrozenParticles>();
            if (frozen == null)
            {
                frozen = particles.gameObject.AddComponent<FrozenParticles>();
                frozen._particles = particles;
                ParticleSystem.MainModule main = particles.main;
                frozen._speed = main.simulationSpeed;
                main.simulationSpeed = 0f;
            }
            frozen._thawTime = Mathf.Max(frozen._thawTime, until);
        }

        // Only what is playing now; one that starts later, like a shield's shatter, plays as usual
        public static void FreezeUnder(GameObject owner, float until)
        {
            foreach (ParticleSystem particles in owner.GetComponentsInChildren<ParticleSystem>())
            {
                Freeze(particles, until);
            }
        }

        // Effects spawned during play; the level's own particles, spiders and weapons are left out, and so is the spawn warning, since the enemy still arrives on time. A poison cloud is frozen whole by FrozenCloud
        public static void FreezeAround(Vector2 origin, float range, float until)
        {
            string spawnWarning = EnemySpawner.instance != null && EnemySpawner.instance.spawnEffect != null ? EnemySpawner.instance.spawnEffect.name + "(Clone)" : null;
            foreach (ParticleSystem particles in FindObjectsOfType<ParticleSystem>())
            {
                if (Vector2.Distance(origin, particles.transform.position) > range) continue;
                Transform root = particles.transform.root;
                if (!root.name.EndsWith("(Clone)") || root.name == spawnWarning) continue;
                if (particles.GetComponentInParent<PlayerController>() != null || particles.GetComponentInParent<SpiderHealthSystem>() != null || particles.GetComponentInParent<Weapon>() != null) continue;
                if (particles.GetComponentInParent<DeathCloud>() != null) continue;
                Freeze(particles, until);
            }
        }

        private void Update()
        {
            if (Time.time < _thawTime) return;
            if (_particles != null)
            {
                ParticleSystem.MainModule main = _particles.main;
                main.simulationSpeed = _speed;
            }
            Destroy(this);
        }
    }

    [HarmonyPatch]
    public static class FrozenShot_Projectile_Patch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.DeclaredMethod(typeof(BasicProjectile), "FixedUpdate");
            yield return AccessTools.DeclaredMethod(typeof(EnergyBall), "FixedUpdate");
        }

        private static bool Prefix(BasicProjectile __instance) => !FrozenShot.Hold(__instance);
    }

    [HarmonyPatch]
    public static class FrozenShot_SawDiscAttractor_Patch
    {
        private static MethodBase TargetMethod() => AccessTools.DeclaredMethod(typeof(SawDiscAttractor), "FixedUpdate");

        private static bool Prefix(SawDiscAttractor __instance) => !FrozenShot.IsFrozen(__instance);
    }

    [HarmonyPatch(typeof(SawDisc), "Start")]
    public static class FrozenShot_SawDiscStart_Patch
    {
        private static void Postfix(SawDisc __instance) => FrozenShot.RecordStart(__instance);
    }
}
