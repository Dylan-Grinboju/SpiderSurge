using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Unity.Netcode;
using UnityEngine;
using Logger = Silk.Logger;

namespace StatsMod
{
    [HarmonyPatch]
    internal static class AccuracyProjectilePatch
    {
        static IEnumerable<MethodBase> TargetMethods() => PatchTargets.FindAll(
            (typeof(Railvolver), "ShootProjectile"),
            (typeof(ProjectileLauncher), "Shoot"),
            (typeof(BurstProjectileLauncher), "LaunchProjectile"),
            (typeof(KhepriStaff), "LaunchProjectile"));

        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase original) =>
            TrackSpawns(instructions, original, AccessTools.Method(typeof(AccuracyTracker), nameof(AccuracyTracker.Spawned)));

        internal static IEnumerable<CodeInstruction> TrackSpawns(IEnumerable<CodeInstruction> instructions, MethodBase original, MethodInfo callback)
        {
            int tracked = 0;
            foreach (CodeInstruction instruction in instructions)
            {
                yield return instruction;
                if (instruction.operand is MethodInfo method && method.DeclaringType == typeof(Object) &&
                    method.Name == "Instantiate" && method.ReturnType == typeof(GameObject))
                {
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Call, callback);
                    tracked++;
                }
            }
            if (tracked == 0) Logger.LogWarning($"{original.DeclaringType?.Name}.{original.Name} no longer spawns projectiles; accuracy is not tracked");
        }
    }

    [HarmonyPatch(typeof(RailShot), "SpawnImpactEffectClientRpc")]
    internal static class AccuracyRailImpactPatch
    {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase original) =>
            AccuracyProjectilePatch.TrackSpawns(instructions, original, AccessTools.Method(typeof(AccuracyTracker), nameof(AccuracyTracker.EffectSpawned)));
    }

    [HarmonyPatch(typeof(DeathRay), "Fire")]
    internal static class AccuracyDeathRayPatch
    {
        static void Prefix(DeathRay __instance) => AccuracyTracker.Fire(__instance, __instance.gameObject);
    }

    [HarmonyPatch(typeof(LaserCannon), "Use")]
    internal static class AccuracyLaserPatch
    {
        static void Prefix(float ____beamCooldownTill, out float __state) => __state = ____beamCooldownTill;

        static void Postfix(LaserCannon __instance, float ____beamCooldownTill, float __state)
        {
            if (____beamCooldownTill > __state) AccuracyTracker.Fire(__instance, __instance.gameObject);
        }
    }

    [HarmonyPatch(typeof(RailShot), "ReflectShot")]
    internal static class AccuracyRailReflectionPatch
    {
        static void Prefix(RailShot __instance) => AccuracyTracker.Forget(__instance);
    }

    [HarmonyPatch(typeof(Shotgun), "ShootClientRpc")]
    internal static class AccuracyShotgunPatch
    {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            bool shotParticles = false;
            int tracked = 0;
            foreach (CodeInstruction instruction in instructions)
            {
                if (instruction.LoadsField(AccessTools.Field(typeof(Shotgun), nameof(Shotgun.shotParticles)))) shotParticles = true;
                if (shotParticles && instruction.Calls(AccessTools.Method(typeof(ParticleSystem), nameof(ParticleSystem.Play), System.Type.EmptyTypes)))
                {
                    var load = new CodeInstruction(OpCodes.Ldarg_0);
                    load.labels.AddRange(instruction.labels);
                    load.blocks.AddRange(instruction.blocks);
                    yield return load;
                    yield return CodeInstruction.Call(typeof(AccuracyShotgunPatch), nameof(PlayVolley));
                    shotParticles = false;
                    tracked++;
                }
                else yield return instruction;
            }
            if (tracked != 1) Logger.LogWarning($"Shotgun particle emission changed ({tracked} sites); check shotgun accuracy tracking");
        }

        private sealed class Emitter
        {
            public ParticleSystem Particles;
            public float BusyUntil;
        }

        private static readonly AccessTools.FieldRef<DamageOnParticle, ParticleSystem> DamageParticles =
            AccessTools.FieldRefAccess<DamageOnParticle, ParticleSystem>("_part");

        private static readonly ConditionalWeakTable<Shotgun, List<Emitter>> emitters = new ConditionalWeakTable<Shotgun, List<Emitter>>();

        // Separate emitters give overlapping volleys their own shot identity in Unity's grouped collision callbacks.
        // Idle ones are reused, the gun's own first; copies live until DamageOnParticle destroys them with the gun.
        private static void PlayVolley(ParticleSystem particles, Shotgun weapon)
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsHost || !GameSessionManager.Instance.IsActive)
            {
                particles.Play();
                return;
            }

            List<Emitter> pool = emitters.GetValue(weapon, _ => new List<Emitter> { new Emitter { Particles = particles } });
            pool.RemoveAll(e => e.Particles == null);
            Emitter emitter = pool.Find(e => Time.time >= e.BusyUntil && e.Particles.particleCount == 0);
            if (emitter == null)
            {
                emitter = new Emitter { Particles = Object.Instantiate(particles, particles.transform.position, particles.transform.rotation) };
                DamageOnParticle damage = emitter.Particles.GetComponent<DamageOnParticle>();
                if (damage != null)
                {
                    DamageParticles(damage) = emitter.Particles;
                    damage.collisionEvents = new List<ParticleCollisionEvent>();
                }
                pool.Add(emitter);
            }
            else if (emitter.Particles != particles)
            {
                emitter.Particles.transform.SetPositionAndRotation(particles.transform.position, particles.transform.rotation);
            }

            ParticleSystem volley = emitter.Particles;
            volley.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            AccuracyTracker.Fire(weapon, volley.gameObject);
            volley.Play();
            emitter.BusyUntil = Time.time + volley.main.startLifetime.constantMax;
        }
    }
}
