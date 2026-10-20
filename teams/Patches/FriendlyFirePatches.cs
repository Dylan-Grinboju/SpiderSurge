using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Interfaces;
using Unity.Netcode;
using UnityEngine;
using Logger = Silk.Logger;

namespace SpiderTeams
{
    internal static class PatchScopes
    {
        private static readonly HashSet<string> _reported = new HashSet<string>();

        public static bool EnterAttack<T>(T source, Func<T, int> resolve)
        {
            DamageContext.Push(Resolve(source, resolve));
            return true;
        }

        public static void ExitAttack(bool entered)
        {
            if (entered)
            {
                DamageContext.Pop();
            }
        }

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

        private static int Resolve<T>(T source, Func<T, int> resolve)
        {
            try
            {
                return resolve(source);
            }
            catch (Exception ex)
            {
                Report("owner lookup for " + typeof(T).Name, ex);
                return OwnerRegistry.Unknown;
            }
        }
    }

    [HarmonyPatch(typeof(SpiderHealthSystem), nameof(SpiderHealthSystem.Damage), new[] { typeof(Vector3), typeof(Vector3), typeof(bool) })]
    public static class SpiderHealthSystem_Damage_Patch
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        public static bool Prefix(SpiderHealthSystem __instance, Vector3 force, Vector3 point, bool stun)
        {
            try
            {
                return FriendlyFireGate.Allow(__instance, force, point, stun);
            }
            catch (Exception ex)
            {
                PatchScopes.Report("friendly fire gate", ex);
                return true;
            }
        }
    }

    [HarmonyPatch(typeof(SpiderHealthSystem), nameof(SpiderHealthSystem.Impact), new[] { typeof(Vector3), typeof(Vector3), typeof(bool), typeof(bool) })]
    public static class SpiderHealthSystem_Impact_Patch
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        public static bool Prefix(SpiderHealthSystem __instance)
        {
            try
            {
                return FriendlyFireGate.AllowImpact(__instance);
            }
            catch (Exception ex)
            {
                PatchScopes.Report("friendly fire knockback gate", ex);
                return true;
            }
        }
    }

    [HarmonyPatch(typeof(ForceField), "Damage", new[] { typeof(Collision2D) })]
    public static class ForceField_Damage_Patch
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        public static void Prefix(ForceField __instance, out bool __state)
        {
            __state = PatchScopes.EnterAttack(__instance, field => OwnerRegistry.OfWeapon(field.GetComponentInParent<Weapon>()));
        }

        [HarmonyFinalizer]
        public static void Finalizer(bool __state)
        {
            PatchScopes.ExitAttack(__state);
        }
    }

    [HarmonyPatch(typeof(DamageOnParticle), "OnParticleCollision", new[] { typeof(GameObject) })]
    public static class DamageOnParticle_OnParticleCollision_Patch
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        public static void Prefix(DamageOnParticle __instance, out bool __state)
        {
            __state = PatchScopes.EnterAttack(__instance, particles => OwnerRegistry.OfWeapon(particles.ignoreWeapon));
        }

        [HarmonyFinalizer]
        public static void Finalizer(bool __state)
        {
            PatchScopes.ExitAttack(__state);
        }
    }

    [HarmonyPatch]
    public static class WeaponAttack_Patch
    {
        [HarmonyTargetMethods]
        public static IEnumerable<MethodBase> TargetMethods()
        {
            return PatchScopes.Existing("WeaponAttack_Patch",
                AccessTools.Method(typeof(DeathRay), "Fire", Type.EmptyTypes),
                AccessTools.Method(typeof(DeathCube), "Fire", new[] { typeof(Vector3), typeof(LineRenderer) }),
                AccessTools.Method(typeof(LaserCube), "Beam", new[] { typeof(Vector3), typeof(LineRenderer) }),
                AccessTools.Method(typeof(LaserCannon), "DamageHit", new[] { typeof(RaycastHit2D), typeof(IDamageable) }),
                AccessTools.Method(typeof(KhepriStaff), "Zap", new[] { typeof(GameObject) }),
                AccessTools.Method(typeof(Snowballs), "ExplodeClientRpc", new[] { typeof(Vector3) }));
        }

        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        public static void Prefix(Weapon __instance, out bool __state)
        {
            __state = PatchScopes.EnterAttack(__instance, weapon => OwnerRegistry.OfWeapon(weapon));
        }

        [HarmonyFinalizer]
        public static void Finalizer(bool __state)
        {
            PatchScopes.ExitAttack(__state);
        }
    }

    [HarmonyPatch(typeof(RailShot), "ProcessHit", new[] { typeof(RaycastHit2D), typeof(Vector2) })]
    public static class RailShot_ProcessHit_Patch
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        public static void Prefix(RailShot __instance, out bool __state)
        {
            __state = PatchScopes.EnterAttack(__instance, source => Owner(source));
        }

        [HarmonyFinalizer]
        public static void Finalizer(bool __state)
        {
            PatchScopes.ExitAttack(__state);
        }

        // The ignore list holds the shooter, or the force field that reflected the shot.
        internal static int Owner(RailShot shot)
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

    [HarmonyPatch(typeof(SawDisc), "TryDamage", new[] { typeof(Collision2D) })]
    public static class SawDisc_TryDamage_Patch
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        public static void Prefix(SawDisc __instance, out bool __state)
        {
            __state = PatchScopes.EnterAttack(__instance, disc => OwnerRegistry.Get(disc.gameObject));
        }

        [HarmonyFinalizer]
        public static void Finalizer(bool __state)
        {
            PatchScopes.ExitAttack(__state);
        }
    }

    [HarmonyPatch(typeof(EnergyBall), nameof(EnergyBall.OnCollisionEnter2D), new[] { typeof(Collision2D) })]
    public static class EnergyBall_OnCollisionEnter2D_Patch
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        public static void Prefix(EnergyBall __instance, out bool __state)
        {
            __state = PatchScopes.EnterAttack(__instance, source => Owner(source));
        }

        [HarmonyFinalizer]
        public static void Finalizer(bool __state)
        {
            PatchScopes.ExitAttack(__state);
        }

        private static int Owner(EnergyBall ball)
        {
            int owner = OwnerRegistry.Get(ball.gameObject);
            return owner != OwnerRegistry.Unknown ? owner : OwnerRegistry.Resolve(ball.owner);
        }
    }

    [HarmonyPatch(typeof(SpiderHealthSystem), "AfterlifeExplosion")]
    public static class SpiderHealthSystem_AfterlifeExplosion_Patch
    {
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        public static void Prefix(SpiderHealthSystem __instance, out bool __state)
        {
            __state = PatchScopes.EnterAttack(__instance, health => OwnerRegistry.OfPlayer(health.GetComponentInParent<PlayerController>()));
        }

        [HarmonyFinalizer]
        public static void Finalizer(bool __state)
        {
            PatchScopes.ExitAttack(__state);
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
