using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace SpiderTeams
{
    [HarmonyPatch]
    public static class WeaponExplode_Patch
    {
        [HarmonyTargetMethods]
        public static IEnumerable<MethodBase> TargetMethods()
        {
            return PatchScopes.Existing("WeaponExplode_Patch",
                AccessTools.Method(typeof(Grenade), "ExplodeClientRpc", new[] { typeof(Vector3) }),
                AccessTools.Method(typeof(Mine), "ExplodeClientRpc", new[] { typeof(Vector3) }),
                AccessTools.Method(typeof(BoomSpear), "ExplodeClientRpc", new[] { typeof(Vector3), typeof(int), typeof(Color) }));
        }

        // Recorded once on the host's send pass; the explosion it creates is matched to it by position.
        [HarmonyPrefix]
        public static void Prefix(Weapon __instance, Vector3 position)
        {
            try
            {
                if (TeamManager.IsHost && ReflectionCache.IsRpcSendStage(__instance))
                {
                    ExplosionOwners.Record(position, OwnerRegistry.OfWeapon(__instance));
                }
            }
            catch (Exception ex)
            {
                PatchScopes.Report("explosion owner tracking", ex);
            }
        }
    }

    [HarmonyPatch(typeof(BasicProjectile), "DestroyProjectileClientRpc", new[] { typeof(Vector3) })]
    public static class BasicProjectile_DestroyProjectileClientRpc_Patch
    {
        [HarmonyPrefix]
        public static void Prefix(BasicProjectile __instance, Vector3 position)
        {
            try
            {
                if (TeamManager.IsHost && ReflectionCache.IsRpcSendStage(__instance))
                {
                    ExplosionOwners.Record(position, OwnerRegistry.Get(__instance.gameObject));
                }
            }
            catch (Exception ex)
            {
                PatchScopes.Report("projectile blast tracking", ex);
            }
        }
    }

    [HarmonyPatch(typeof(RailShot), "SpawnImpactEffect", new[] { typeof(Vector2) })]
    public static class RailShot_SpawnImpactEffect_Patch
    {
        // The rail's impact splash is an Explosion; a stopping shot spawns it from inside another RPC with no send pass, so it is recorded here.
        [HarmonyPrefix]
        public static void Prefix(RailShot __instance, Vector2 hitPoint)
        {
            try
            {
                if (TeamManager.IsHost)
                {
                    ExplosionOwners.Record(hitPoint, RailShot_ProcessHit_Patch.Owner(__instance));
                }
            }
            catch (Exception ex)
            {
                PatchScopes.Report("rail impact tracking", ex);
            }
        }
    }

    [HarmonyPatch(typeof(Explosion), "KnockBack")]
    public static class Explosion_KnockBack_Patch
    {
        // An explosion set off inside another attack, like the Pulse ultimate's, belongs to that attacker.
        [HarmonyPrefix]
        [HarmonyPriority(Priority.First)]
        public static void Prefix(Explosion __instance, out bool __state)
        {
            __state = PatchScopes.EnterAttack(__instance, explosion =>
            {
                int owner = ExplosionOwners.Resolve(explosion);
                return owner != OwnerRegistry.Unknown ? owner : DamageContext.Current;
            });
        }

        [HarmonyFinalizer]
        public static void Finalizer(bool __state)
        {
            PatchScopes.ExitAttack(__state);
        }
    }
}
