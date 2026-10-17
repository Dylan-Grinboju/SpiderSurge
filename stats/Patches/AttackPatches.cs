using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;
using Logger = Silk.Logger;

namespace StatsMod
{
    internal static class PatchTargets
    {
        public static MethodInfo Find(Type type, string name)
        {
            MethodInfo method = AccessTools.Method(type, name);
            if (method == null)
            {
                Logger.LogWarning($"{type.Name}.{name} not found, it is not tracked");
            }
            return method;
        }

        public static IEnumerable<MethodBase> FindAll(params (Type type, string name)[] targets)
        {
            return targets.Select(target => Find(target.type, target.name)).Where(method => method != null).ToList();
        }
    }

    // Runs each attack inside HitLogic.BeginAttack/EndAttack; the receivers below record its hits
    [HarmonyPatch]
    class AttackScopePatch
    {
        private struct Attack
        {
            public string Weapon;
            public Func<object, PlayerController> Owner;
        }

        private static readonly Dictionary<MethodBase, Attack> attacks = new Dictionary<MethodBase, Attack>();

        static AttackScopePatch()
        {
            Add<DamageOnParticle>("OnParticleCollision", "Shotgun", particles => HitLogic.WeaponPlayer(particles.ignoreWeapon));
            Add<RailShot>("ProcessHit", "RailShot", RailShotOwner);
            Add<DeathCube>("Fire", "DeathCube", HitLogic.WeaponPlayer);
            Add<DeathRay>("Fire", "DeathRay", HitLogic.WeaponPlayer);
            Add<LaserCannon>("DamageHit", "Laser Cannon", HitLogic.WeaponPlayer);
            Add<LaserCube>("Beam", "Laser Cube", HitLogic.WeaponPlayer);
            Add<ForceField>("Damage", "Particle Blade", field => HitLogic.WeaponPlayer(field.GetComponentInParent<Weapon>()));
            Add<SawDisc>("TryDamage", "SawDisc", disc => HitLogic.GetDiscOwner(disc.gameObject));
            Add<EnergyBall>("OnCollisionEnter2D", "EnergyBall", EnergyBallOwner);
            Add<KhepriStaff>("Zap", "KhepriStaff", HitLogic.WeaponPlayer);
            Add<Explosion>("KnockBack", "Explosions", ExplosionOwner);
            Add<SpiderHealthSystem>("AfterlifeExplosion", "Explosions", spider => spider.GetComponentInParent<PlayerController>());
            Add<Snowballs>("ExplodeClientRpc", "Snowball", HitLogic.WeaponPlayer);
            Add<WhispBrain>("ProcessCollision", null, whisp => null);
            Add<SpiderSurge.PulseAbility>("ApplyPulsePhysics", "Pulse", SpiderSurge.PulseAbility.CasterOf);
        }

        private static void Add<T>(string method, string weapon, Func<T, PlayerController> owner)
        {
            MethodInfo target = PatchTargets.Find(typeof(T), method);
            if (target != null)
            {
                attacks[target] = new Attack { Weapon = weapon, Owner = instance => owner((T)instance) };
            }
        }

        static IEnumerable<MethodBase> TargetMethods() => attacks.Keys.ToList();

        static void Prefix(object __instance, MethodBase __originalMethod, out HitLogic.AttackScope __state)
        {
            __state = default;
            try
            {
                Attack attack = attacks[__originalMethod];
                __state = HitLogic.BeginAttack(__instance, attack.Owner, attack.Weapon);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error tracking {__originalMethod.DeclaringType?.Name}.{__originalMethod.Name} hit: {ex.Message}");
            }
        }

        static void Finalizer(HitLogic.AttackScope __state) => HitLogic.EndAttack(__state);

        private static PlayerController RailShotOwner(RailShot shot)
        {
            foreach (GameObject ignored in shot.ignore)
            {
                if (ignored == null) continue;

                SpiderHealthSystem spider = ignored.GetComponentInParent<SpiderHealthSystem>();
                if (spider != null) return spider.GetComponentInParent<PlayerController>();

                Weapon weapon = ignored.GetComponentInParent<Weapon>();
                if (weapon != null) return HitLogic.WeaponPlayer(weapon);
            }
            return null;
        }

        private static PlayerController EnergyBallOwner(EnergyBall ball)
        {
            PlayerController reflector = HitLogic.GetBallReflector(ball);
            if (reflector != null) return reflector;
            return ball.owner != null ? ball.owner.GetComponentInParent<PlayerController>() : null;
        }

        // SpiderSurge's ultimate Pulse runs an explosion without an owner inside the pulse, it keeps the pulse's player
        private static PlayerController ExplosionOwner(Explosion explosion)
        {
            PlayerController owner = HitLogic.GetExplosionOwner(explosion);
            return owner != null ? owner : HitLogic.EnclosingAttacker;
        }
    }

    // Receivers. Enemy stingers, blades, roller struts and Khepri armor pass their hits on to EnemyHealthSystem,
    // and equipped weapons pass theirs on to the holder's SpiderHealthSystem.
    [HarmonyPatch(typeof(EnemyHealthSystem), "Impact")]
    class EnemyImpactReceiverPatch
    {
        static void Prefix(EnemyHealthSystem __instance)
        {
            try
            {
                if (__instance.IsDead() || Time.time < EnemyReflectionCache.EnemyImmuneTime(__instance)) return;
                HitLogic.OnEnemyHit(__instance, damage: false);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error recording enemy knockback: {ex.Message}");
            }
        }
    }

    [HarmonyPatch(typeof(EnemyHealthSystem), "Damage")]
    class EnemyDamageReceiverPatch
    {
        static void Prefix(EnemyHealthSystem __instance)
        {
            try
            {
                if (__instance.IsDead() || __instance.ignoreDirectDamage ||
                    Time.time < EnemyReflectionCache.EnemyImmuneTime(__instance)) return;
                HitLogic.OnEnemyHit(__instance, damage: true);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error recording enemy damage: {ex.Message}");
            }
        }
    }

    [HarmonyPatch(typeof(SpiderHealthSystem), "Impact")]
    class SpiderImpactReceiverPatch
    {
        static void Prefix(SpiderHealthSystem __instance, bool stun)
        {
            try
            {
                HitLogic.OnPlayerHit(__instance, damage: false, stun);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error recording spider knockback: {ex.Message}");
            }
        }
    }

    [HarmonyPatch(typeof(SpiderHealthSystem), "Damage")]
    [HarmonyAfter("com.SpiderSurge.surge", "com.SpiderSurge.teams")]
    class SpiderDamageReceiverPatch
    {
        static void Prefix(SpiderHealthSystem __instance, bool stun, bool __runOriginal)
        {
            try
            {
                // Surge immunity and Teams friendly fire can cancel the hit
                if (!__runOriginal || Time.time < ReflectionCache.SpiderImmuneTill(__instance)) return;
                HitLogic.OnPlayerHit(__instance, damage: true, stun);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error recording spider damage: {ex.Message}");
            }
        }
    }

    [HarmonyPatch(typeof(SpiderHealthSystem), "FixedUpdate")]
    class SpiderFootingPatch
    {
        static void Postfix(SpiderHealthSystem __instance, Stabilizer ____stabilizer)
        {
            try
            {
                HitLogic.UpdatePlayerFooting(__instance, ____stabilizer);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error tracking spider footing: {ex.Message}");
            }
        }
    }

    [HarmonyPatch(typeof(DeathZone), "OnTriggerStay2D")]
    class DeathZoneContactPatch
    {
        static void Prefix(Collider2D other, float ____disintegrateDelayMod)
        {
            try
            {
                if (!HitLogic.HasPlayerHits || other.isTrigger || !other.CompareTag("PlayerRigidbody")) return;

                SpiderHealthSystem spider = other.GetComponent<SpiderHealthSystem>();
                if (spider != null)
                {
                    HitLogic.OnPlayerTouchedDeathZone(spider, ____disintegrateDelayMod);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error tracking lava contact: {ex.Message}");
            }
        }
    }

    // Losing its struts (RollerBrain.SelfDestruct) or a spider coming close lights the ExplodingRoller's fuse
    [HarmonyPatch(typeof(ExplodingRoller), "TriggerExplosive")]
    class ExplodingRollerFuseLitPatch
    {
        static void Prefix(ExplodingRoller __instance, bool ____triggered)
        {
            try
            {
                if (____triggered || __instance.healthSystem == null) return;
                HitLogic.OnRollerFuseLit(__instance.healthSystem, LostStruts(__instance));
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error tracking roller fuse: {ex.Message}");
            }
        }

        private static bool LostStruts(RollerBrain roller)
        {
            int active = 0;
            foreach (Transform strut in roller.struts.transform)
            {
                if (strut.gameObject.activeSelf) active++;
            }
            return active < roller.minStrutCount;
        }
    }

    // The fuse running out ends here without the roller dying first. A roller already dissolving in lava, or dying
    // any other way (which runs this after EnemyHealthSystem.Explode), keeps the kill it has
    [HarmonyPatch(typeof(ExplodingRoller), "Explode")]
    class ExplodingRollerFuseEndPatch
    {
        static void Prefix(ExplodingRoller __instance, bool ____exploded)
        {
            try
            {
                EnemyHealthSystem health = __instance.healthSystem;
                if (____exploded || health == null || health.IsDead() || EnemyReflectionCache.EnemyExploding(health)) return;
                HitLogic.OnRollerFuseEnded(health);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error tracking roller fuse: {ex.Message}");
            }
        }
    }

    [HarmonyPatch]
    class ExplosionOwnerPatch
    {
        static IEnumerable<MethodBase> TargetMethods() => PatchTargets.FindAll(
            (typeof(Grenade), "ExplodeClientRpc"),
            (typeof(Mine), "ExplodeClientRpc"),
            (typeof(BoomSpear), "ExplodeClientRpc"),
            (typeof(BasicProjectile), "DestroyProjectileClientRpc"));

        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase original) =>
            OwnerStoreRewriter.Rewrite(instructions, original,
                AccessTools.Field(typeof(Explosion), nameof(Explosion.explosionOwnerId)),
                AccessTools.Method(typeof(HitLogic), nameof(HitLogic.SetExplosionOwner)));
    }

    // A grenade or mine dropped without being armed belongs to nobody. Firing it in hand arms it, and so does a hit
    [HarmonyPatch]
    class ExplosiveFiredPatch
    {
        static IEnumerable<MethodBase> TargetMethods() => PatchTargets.FindAll((typeof(Grenade), "Use"), (typeof(Mine), "Use"));

        static void Prefix(Weapon __instance, bool fire, bool ____armed)
        {
            try
            {
                if (fire && !____armed) HitLogic.OnExplosiveArmed(__instance, byHolder: true);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error recording who armed {__instance.name}: {ex.Message}");
            }
        }
    }

    [HarmonyPatch]
    class ExplosiveHitPatch
    {
        static IEnumerable<MethodBase> TargetMethods() => PatchTargets.FindAll((typeof(Grenade), "Arm"), (typeof(Mine), "Impact"));

        static void Prefix(Weapon __instance, bool ____armed)
        {
            try
            {
                if (!____armed) HitLogic.OnExplosiveArmed(__instance, byHolder: false);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error recording who armed {__instance.name}: {ex.Message}");
            }
        }
    }

    // A held Particle Blade that reflects an energy ball makes the ball its own owner, its hits then belong to the blade's player
    [HarmonyPatch(typeof(EnergyBall), "Overload")]
    class EnergyBallReflectPatch
    {
        static void Prefix(EnergyBall __instance, GameObject go)
        {
            try
            {
                ParticleBlade blade = go.GetComponent<ParticleBlade>();
                if (blade != null && blade.owner != null)
                {
                    HitLogic.OnBallReflected(__instance, HitLogic.WeaponPlayer(blade));
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error recording energy ball reflection: {ex.Message}");
            }
        }
    }

    [HarmonyPatch]
    class ProjectileOwnerPatch
    {
        static IEnumerable<MethodBase> TargetMethods() => PatchTargets.FindAll(
            (typeof(ProjectileLauncher), "Shoot"),
            (typeof(BurstProjectileLauncher), "LaunchProjectile"));

        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase original) =>
            OwnerStoreRewriter.Rewrite(instructions, original,
                AccessTools.Field(typeof(BasicProjectile), nameof(BasicProjectile.projectileOwnerId)),
                AccessTools.Method(typeof(HitLogic), nameof(HitLogic.SetProjectileOwner)));
    }

    internal static class OwnerStoreRewriter
    {
        // Turns "target.field = value" into "setter(target, value, this)"; the setter does the same store
        public static IEnumerable<CodeInstruction> Rewrite(IEnumerable<CodeInstruction> instructions, MethodBase original, FieldInfo field, MethodInfo setter)
        {
            int replaced = 0;
            foreach (CodeInstruction instruction in instructions)
            {
                if (instruction.StoresField(field))
                {
                    CodeInstruction loadThis = new CodeInstruction(OpCodes.Ldarg_0);
                    loadThis.labels.AddRange(instruction.labels);
                    loadThis.blocks.AddRange(instruction.blocks);
                    yield return loadThis;
                    yield return new CodeInstruction(OpCodes.Call, setter);
                    replaced++;
                }
                else
                {
                    yield return instruction;
                }
            }

            if (replaced == 0)
            {
                Logger.LogWarning($"{original.DeclaringType?.Name}.{original.Name} no longer sets {field.Name}, its owner is not tracked");
            }
        }
    }
}
