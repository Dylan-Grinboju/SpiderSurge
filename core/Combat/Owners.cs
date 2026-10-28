using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Unity.Netcode;
using UnityEngine;
using Logger = Silk.Logger;

namespace SpiderSurge.Common
{
    // Who owns weapons, launched objects and explosives, keyed by the GameObject's instance id so recycled network ids never collide
    public static class Owners
    {
        public struct Record
        {
            public GameObject Target;
            // Who equipped it, who launched it, or who last turned an energy ball
            public PlayerController Holder;
            public Weapon Launcher;
            public PlayerController LaunchedBy;
            public float LaunchedAt;
            public PlayerController Armer;
            public float ArmedAt;
            public PlayerController Reflector;
            public float ReflectedAt;
        }

        private const float PruneInterval = 30f;

        private static readonly Dictionary<int, Record> Records = new Dictionary<int, Record>();
        private static readonly List<(Weapon launcher, PlayerController player)> Launches = new List<(Weapon, PlayerController)>(4);
        private static float _lastPrune;

        public static event Action<Weapon> Equipped;
        public static event Action<EnergyBall> Reflected;
        public static event Action<Explosion, NetworkBehaviour> ExplosionCreated;

        public static bool TryGet(GameObject target, out Record record)
        {
            if (target != null && Records.TryGetValue(target.GetInstanceID(), out record))
            {
                return true;
            }
            record = default;
            return false;
        }

        public static PlayerController HolderOf(GameObject target) => TryGet(target, out Record record) ? record.Holder : null;

        public static PlayerController ManagerPlayer(WeaponManager manager)
        {
            if (manager == null)
            {
                return null;
            }

            var player = manager.GetComponentInParent<PlayerController>();
            if (player == null && manager.healthSystem != null)
            {
                player = manager.healthSystem.GetComponentInParent<PlayerController>();
            }
            return player;
        }

        // The game keeps a weapon's owner after it is dropped or thrown; without one, the player who last equipped or launched it
        public static PlayerController PlayerOf(Weapon weapon)
        {
            if (weapon == null)
            {
                return null;
            }
            return weapon.owner != null ? ManagerPlayer(weapon.owner) : HolderOf(weapon.gameObject);
        }

        public static void SetHolder(GameObject target, PlayerController player)
        {
            if (target == null)
            {
                return;
            }
            Record record = Get(target);
            record.Holder = player;
            Store(record);
        }

        public static void SetArmer(Weapon explosive, PlayerController player)
        {
            Record record = Get(explosive.gameObject);
            record.Armer = player;
            record.ArmedAt = Time.time;
            Store(record);
        }

        // The recorded holder, else the player, weapon or network object the target is part of
        public static PlayerController Resolve(GameObject target)
        {
            if (target == null)
            {
                return null;
            }

            PlayerController player = HolderOf(target);
            if (player != null)
            {
                return player;
            }

            player = target.GetComponentInParent<PlayerController>();
            if (player != null)
            {
                return player;
            }

            var weapon = target.GetComponentInParent<Weapon>();
            if (weapon != null)
            {
                return PlayerOf(weapon);
            }

            var networkObject = target.GetComponentInParent<NetworkObject>();
            return networkObject != null && networkObject.gameObject != target ? HolderOf(networkObject.gameObject) : null;
        }

        public static void Clear()
        {
            Records.Clear();
            Launches.Clear();
            _lastPrune = Time.time;
        }

        internal static void OnEquipped(Weapon weapon, WeaponManager manager)
        {
            Record record = Get(weapon.gameObject);
            record.Holder = ManagerPlayer(manager);
            record.Launcher = null;
            record.LaunchedBy = null;
            Store(record);
            Equipped?.Invoke(weapon);
        }

        internal static void BeginLaunch(Weapon launcher)
        {
            PlayerController player = null;
            try
            {
                player = PlayerOf(launcher);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Could not find who fired {launcher?.name}: {ex.Message}");
            }
            Launches.Add((launcher, player));
        }

        internal static void EndLaunch() => Launches.RemoveAt(Launches.Count - 1);

        // Every network object spawned while a weapon fires is that shot's; a spawn outside one starts with no owner
        internal static void OnSpawned(GameObject spawned)
        {
            if (Launches.Count == 0)
            {
                Records.Remove(spawned.GetInstanceID());
                return;
            }

            (Weapon launcher, PlayerController player) = Launches[Launches.Count - 1];
            float now = Time.time;
            var record = new Record { Target = spawned, Holder = player, Launcher = launcher, LaunchedBy = player, LaunchedAt = now };
            // A Grenade Launcher's grenades come out armed, so they belong to the launcher's player from the start
            if (player != null && launcher is ProjectileLauncher && spawned.TryGetComponent(out Grenade grenade) && grenade.preArmed)
            {
                record.Armer = player;
                record.ArmedAt = now;
            }
            Store(record);
        }

        // A held Particle Blade that overloads an energy ball makes the ball its own owner, so the blade's player turned it
        internal static void OnOverloaded(EnergyBall ball, GameObject by)
        {
            ParticleBlade blade = by != null ? by.GetComponent<ParticleBlade>() : null;
            if (blade == null)
            {
                return;
            }

            Record record = Get(ball.gameObject);
            bool reflected = blade.owner != null;
            if (reflected)
            {
                record.Reflector = PlayerOf(blade);
                record.ReflectedAt = Time.time;
            }
            PlayerController player = Resolve(by);
            if (player != null)
            {
                record.Holder = player;
            }
            Store(record);
            if (reflected)
            {
                Reflected?.Invoke(ball);
            }
        }

        // The explode ClientRpcs store the owner's client id here, which is 0 both for player 0 and for blasts nobody owns
        public static void SetExplosionOwner(Explosion explosion, ulong ownerId, NetworkBehaviour source)
        {
            explosion.explosionOwnerId = ownerId;
            try
            {
                ExplosionCreated?.Invoke(explosion, source);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error recording explosion owner: {ex.Message}");
            }
        }

        private static Record Get(GameObject target)
        {
            return Records.TryGetValue(target.GetInstanceID(), out Record record) ? record : new Record { Target = target };
        }

        private static void Store(Record record)
        {
            PruneIfDue();
            Records[record.Target.GetInstanceID()] = record;
        }

        private static void PruneIfDue()
        {
            float now = Time.time;
            if (now - _lastPrune <= PruneInterval)
            {
                return;
            }
            _lastPrune = now;

            List<int> destroyed = null;
            foreach (var entry in Records)
            {
                if (entry.Value.Target == null)
                {
                    (destroyed ??= new List<int>()).Add(entry.Key);
                }
            }
            destroyed?.ForEach(id => Records.Remove(id));
        }
    }

    [HarmonyPatch(typeof(Weapon), nameof(Weapon.Equip), new[] { typeof(WeaponManager) })]
    internal static class OwnerEquipPatch
    {
        private static void Postfix(Weapon __instance, WeaponManager wm)
        {
            try
            {
                Owners.OnEquipped(__instance, wm);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error recording who equipped {__instance.name}: {ex.Message}");
            }
        }
    }

    [HarmonyPatch]
    internal static class OwnerLaunchPatch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            MethodInfo[] launchers =
            {
                AccessTools.Method(typeof(ProjectileLauncher), "Shoot", Type.EmptyTypes),
                AccessTools.Method(typeof(BurstProjectileLauncher), "Shoot", Type.EmptyTypes),
                AccessTools.Method(typeof(DiscLauncher), "LaunchDisc", Type.EmptyTypes),
                AccessTools.Method(typeof(ClusterMine), "CreateObject", new[] { typeof(GameObject), typeof(float) }),
                AccessTools.Method(typeof(Railvolver), "ShootProjectile", new[] { typeof(Vector3) }),
                AccessTools.Method(typeof(KhepriStaff), "LaunchProjectile", Type.EmptyTypes),
            };
            foreach (MethodInfo launcher in launchers)
            {
                if (launcher != null)
                {
                    yield return launcher;
                }
                else
                {
                    Logger.LogWarning("A weapon launcher method is missing, the owner of what it fires is not tracked");
                }
            }
        }

        [HarmonyPriority(Priority.First)]
        private static void Prefix(Weapon __instance, out bool __state)
        {
            Owners.BeginLaunch(__instance);
            __state = true;
        }

        [HarmonyPriority(Priority.First)]
        private static void Finalizer(bool __state)
        {
            if (__state)
            {
                Owners.EndLaunch();
            }
        }
    }

    [HarmonyPatch]
    internal static class OwnerSpawnPatch
    {
        private static MethodBase TargetMethod()
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

        private static void Postfix(NetworkObject __instance)
        {
            try
            {
                Owners.OnSpawned(__instance.gameObject);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error recording who spawned {__instance.name}: {ex.Message}");
            }
        }
    }

    [HarmonyPatch(typeof(EnergyBall), "Overload", new[] { typeof(GameObject) })]
    internal static class OwnerOverloadPatch
    {
        private static void Prefix(EnergyBall __instance, GameObject go)
        {
            try
            {
                Owners.OnOverloaded(__instance, go);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error recording energy ball reflection: {ex.Message}");
            }
        }
    }

    [HarmonyPatch(typeof(EnergyBall), nameof(EnergyBall.ChangeOwner))]
    internal static class OwnerChangePatch
    {
        private static void Postfix(EnergyBall __instance, GameObject newOwner)
        {
            try
            {
                Owners.SetHolder(__instance.gameObject, Owners.Resolve(newOwner));
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error recording energy ball owner: {ex.Message}");
            }
        }
    }

    // Each explode ClientRpc hands over the Explosion it creates and its source where it stores the explosion's owner
    [HarmonyPatch]
    internal static class OwnerExplosionPatch
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            MethodInfo[] rpcs =
            {
                AccessTools.Method(typeof(Grenade), "ExplodeClientRpc"),
                AccessTools.Method(typeof(Mine), "ExplodeClientRpc"),
                AccessTools.Method(typeof(BoomSpear), "ExplodeClientRpc"),
                AccessTools.Method(typeof(BasicProjectile), "DestroyProjectileClientRpc"),
            };
            foreach (MethodInfo rpc in rpcs)
            {
                if (rpc != null)
                {
                    yield return rpc;
                }
                else
                {
                    Logger.LogWarning("An explode ClientRpc is missing, the owner of its explosions is not tracked");
                }
            }
        }

        // Turns "explosion.explosionOwnerId = value" into "Owners.SetExplosionOwner(explosion, value, this)", which does the same store
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, MethodBase original)
        {
            FieldInfo field = AccessTools.Field(typeof(Explosion), nameof(Explosion.explosionOwnerId));
            MethodInfo setter = AccessTools.Method(typeof(Owners), nameof(Owners.SetExplosionOwner));
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

    [HarmonyPatch(typeof(LobbyController), "OnSceneLoaded")]
    internal static class OwnerSceneLoadedPatch
    {
        private static void Postfix() => Owners.Clear();
    }
}
