using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Interfaces;
using SpiderSurge.Common;
using Unity.Netcode;
using UnityEngine;
using Logger = Silk.Logger;

namespace SpiderTeams
{
    internal static class PatchScopes
    {
        private static readonly HashSet<string> _reported = new HashSet<string>();

        public static bool EnterSpawn<T>(T source, Func<T, int> resolve)
        {
            SpawnScope.Push(Resolve(source, resolve));
            return true;
        }

        public static void ExitSpawn(bool entered)
        {
            if (entered)
            {
                SpawnScope.Pop();
            }
        }

        public static IEnumerable<MethodBase> Existing(string patch, params MethodInfo[] methods)
        {
            for (int i = 0; i < methods.Length; i++)
            {
                if (methods[i] != null)
                {
                    yield return methods[i];
                }
                else
                {
                    Logger.LogWarning($"SpiderTeams {patch}: a target method is missing, friendly fire from it is not tracked");
                }
            }
        }

        public static void Report(string where, Exception ex)
        {
            if (_reported.Add(where))
            {
                Logger.LogError($"SpiderTeams {where} failed: {ex}");
            }
        }

        public static int Resolve<T>(T source, Func<T, int> resolve, string label = null)
        {
            try
            {
                return resolve(source);
            }
            catch (Exception ex)
            {
                Report("owner lookup for " + (label ?? typeof(T).Name), ex);
                return OwnerRegistry.Unknown;
            }
        }
    }

    // Damage() has no attacker argument, so each damage source is a Teams entry on the core attack scope holding its attacker
    internal static class TeamAttacks
    {
        private sealed class Resolver
        {
            public string Label;
            public Func<Component, int> Owner;
        }

        internal static readonly AttackView<int> View = new AttackView<int>(Open);

        public static void Register()
        {
            Add<ForceField>("ForceField_Damage_Patch", field => OwnerRegistry.OfWeapon(field.GetComponentInParent<Weapon>()),
                AccessTools.Method(typeof(ForceField), "Damage", new[] { typeof(Collision2D) }));
            Add<DamageOnParticle>("DamageOnParticle_OnParticleCollision_Patch", particles => OwnerRegistry.OfWeapon(particles.ignoreWeapon),
                AccessTools.Method(typeof(DamageOnParticle), "OnParticleCollision", new[] { typeof(GameObject) }));
            Add<Weapon>("WeaponAttack_Patch", weapon => OwnerRegistry.OfWeapon(weapon),
                AccessTools.Method(typeof(DeathRay), "Fire", Type.EmptyTypes),
                AccessTools.Method(typeof(DeathCube), "Fire", new[] { typeof(Vector3), typeof(LineRenderer) }),
                AccessTools.Method(typeof(LaserCube), "Beam", new[] { typeof(Vector3), typeof(LineRenderer) }),
                AccessTools.Method(typeof(LaserCannon), "DamageHit", new[] { typeof(RaycastHit2D), typeof(IDamageable) }),
                AccessTools.Method(typeof(KhepriStaff), "Zap", new[] { typeof(GameObject) }),
                AccessTools.Method(typeof(Snowballs), "ExplodeClientRpc", new[] { typeof(Vector3) }));
            Add<RailShot>("RailShot_ProcessHit_Patch", RailShotOwner.Of,
                AccessTools.Method(typeof(RailShot), "ProcessHit", new[] { typeof(RaycastHit2D), typeof(Vector2) }));
            Add<SawDisc>("SawDisc_TryDamage_Patch", disc => OwnerRegistry.Get(disc.gameObject),
                AccessTools.Method(typeof(SawDisc), "TryDamage", new[] { typeof(Collision2D) }));
            Add<EnergyBall>("EnergyBall_OnCollisionEnter2D_Patch", EnergyBallOwner,
                AccessTools.Method(typeof(EnergyBall), nameof(EnergyBall.OnCollisionEnter2D), new[] { typeof(Collision2D) }));
            Add<SpiderHealthSystem>("SpiderHealthSystem_AfterlifeExplosion_Patch", health => Players.IdOf(health),
                AccessTools.Method(typeof(SpiderHealthSystem), "AfterlifeExplosion"));
            // An explosion set off inside another attack, like the Pulse ultimate's, belongs to that attacker.
            Add<Explosion>("Explosion_KnockBack_Patch", explosion =>
                {
                    int owner = ExplosionOwners.Resolve(explosion);
                    return owner != OwnerRegistry.Unknown ? owner : DamageContext.Current;
                },
                AccessTools.Method(typeof(Explosion), "KnockBack"));
            // The pulse knocks back and, as an ultimate, kills; its caster becomes the attacker so the friendly fire rule applies.
            Add<Component>("PulseAbility_ApplyPulsePhysics_Patch", ability => Players.IdOf(ability),
                AccessTools.Method(typeof(SpiderSurge.PulseAbility), "ApplyPulsePhysics"));
        }

        public static void Unregister() => View.Unregister();

        // Only the host's team match reads the attacker, so the owner lookup runs only while the host has team mode on
        private static bool Open(ref int slot, Component source, object info)
        {
            Resolver resolver = (Resolver)info;
            slot = TeamManager.HostTeamMode ? PatchScopes.Resolve(source, resolver.Owner, resolver.Label) : OwnerRegistry.Unknown;
            return true;
        }

        private static void Add<T>(string patch, Func<T, int> owner, params MethodInfo[] methods) where T : Component
        {
            var resolver = new Resolver { Label = typeof(T).Name, Owner = source => owner((T)source) };
            foreach (MethodBase method in PatchScopes.Existing(patch, methods))
            {
                View.Register(method, resolver);
            }
        }

        private static int EnergyBallOwner(EnergyBall ball)
        {
            int owner = OwnerRegistry.Get(ball.gameObject);
            return owner != OwnerRegistry.Unknown ? owner : OwnerRegistry.Resolve(ball.owner);
        }
    }

    // Friendly fire decides whether a teammate's hit lands, after Surge's Pulse protection
    internal static class FriendlyFireHits
    {
        public static void Hook(bool on)
        {
            SpiderHits.Damage.Gate(AllowDamage, on);
            SpiderHits.Impact.Gate(AllowImpact, on);
        }

        private static bool AllowDamage(SpiderHealthSystem spider, Vector3 force, Vector3 point, bool stun)
        {
            try
            {
                return FriendlyFireGate.Allow(spider, force, point, stun);
            }
            catch (Exception ex)
            {
                PatchScopes.Report("friendly fire gate", ex);
                return true;
            }
        }

        private static bool AllowImpact(SpiderHealthSystem spider, Vector3 force, Vector3 point, bool stun)
        {
            try
            {
                return FriendlyFireGate.AllowImpact(spider);
            }
            catch (Exception ex)
            {
                PatchScopes.Report("friendly fire knockback gate", ex);
                return true;
            }
        }
    }

    internal static class RailShotOwner
    {
        // The ignore list holds the shooter, or the force field that reflected the shot.
        internal static int Of(RailShot shot)
        {
            List<GameObject> ignore = shot.ignore;
            if (ignore != null)
            {
                for (int i = 0; i < ignore.Count; i++)
                {
                    int owner = OwnerRegistry.Resolve(ignore[i]);
                    if (owner != OwnerRegistry.Unknown)
                    {
                        return owner;
                    }
                }
            }
            return OwnerRegistry.Get(shot.gameObject);
        }
    }

    [HarmonyPatch]
    public static class WeaponSpawn_Patch
    {
        [HarmonyTargetMethods]
        public static IEnumerable<MethodBase> TargetMethods()
        {
            return PatchScopes.Existing("WeaponSpawn_Patch",
                AccessTools.Method(typeof(ProjectileLauncher), "Shoot", Type.EmptyTypes),
                AccessTools.Method(typeof(BurstProjectileLauncher), "Shoot", Type.EmptyTypes),
                AccessTools.Method(typeof(DiscLauncher), "LaunchDisc", Type.EmptyTypes),
                AccessTools.Method(typeof(ClusterMine), "CreateObject", new[] { typeof(GameObject), typeof(float) }),
                AccessTools.Method(typeof(Railvolver), "ShootProjectile", new[] { typeof(Vector3) }),
                AccessTools.Method(typeof(KhepriStaff), "LaunchProjectile", Type.EmptyTypes));
        }

        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        public static void Prefix(Weapon __instance, out bool __state)
        {
            __state = PatchScopes.EnterSpawn(__instance, weapon => OwnerRegistry.OfWeapon(weapon));
        }

        [HarmonyFinalizer]
        public static void Finalizer(bool __state)
        {
            PatchScopes.ExitSpawn(__state);
        }
    }

    [HarmonyPatch]
    public static class NetworkObject_SpawnInternal_Patch
    {
        [HarmonyTargetMethod]
        public static MethodBase TargetMethod()
        {
            foreach (MethodInfo method in AccessTools.GetDeclaredMethods(typeof(NetworkObject)))
            {
                if (method.Name == "SpawnInternal")
                {
                    return method;
                }
            }
            return null;
        }

        // Spawns outside a weapon's scope clear any owner left on a pooled object.
        [HarmonyPostfix]
        public static void Postfix(NetworkObject __instance)
        {
            try
            {
                if (SpawnScope.Active)
                {
                    OwnerRegistry.Set(__instance.gameObject, SpawnScope.Owner);
                }
                else
                {
                    OwnerRegistry.Remove(__instance.gameObject);
                }
            }
            catch (Exception ex)
            {
                PatchScopes.Report("spawn owner tracking", ex);
            }
        }
    }

    [HarmonyPatch(typeof(Weapon), nameof(Weapon.Equip), new[] { typeof(WeaponManager) })]
    public static class Weapon_Equip_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(Weapon __instance)
        {
            try
            {
                if (__instance.owner != null)
                {
                    OwnerRegistry.Set(__instance.gameObject, OwnerRegistry.OfManager(__instance.owner));
                }
            }
            catch (Exception ex)
            {
                PatchScopes.Report("weapon owner tracking", ex);
            }
        }
    }

    [HarmonyPatch(typeof(EnergyBall), nameof(EnergyBall.ChangeOwner))]
    public static class EnergyBall_ChangeOwner_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(EnergyBall __instance, GameObject newOwner)
        {
            try
            {
                OwnerRegistry.Set(__instance.gameObject, OwnerRegistry.Resolve(newOwner));
            }
            catch (Exception ex)
            {
                PatchScopes.Report("energy ball owner tracking", ex);
            }
        }
    }

    [HarmonyPatch(typeof(EnergyBall), "Overload", new[] { typeof(GameObject) })]
    public static class EnergyBall_Overload_Patch
    {
        // Overload hands the ball to itself, so the blade that overloaded it becomes the owner.
        [HarmonyPostfix]
        public static void Postfix(EnergyBall __instance, GameObject go)
        {
            try
            {
                int owner = OwnerRegistry.Resolve(go);
                if (owner != OwnerRegistry.Unknown)
                {
                    OwnerRegistry.Set(__instance.gameObject, owner);
                }
            }
            catch (Exception ex)
            {
                PatchScopes.Report("energy ball overload tracking", ex);
            }
        }
    }
}
