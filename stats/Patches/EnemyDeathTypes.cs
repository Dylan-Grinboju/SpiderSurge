using UnityEngine;
using UnityEngine.InputSystem;
using Logger = Silk.Logger;
using HarmonyLib;
using Unity.Netcode;
using System.Reflection;


namespace StatsMod
{
    // All AccessTools calls are done once at static initialization to avoid per-call overhead.
    internal static class EnemyReflectionCache
    {
        // NetworkBehaviour
        public static readonly PropertyInfo IsHostProperty =
            AccessTools.Property(typeof(NetworkBehaviour), "IsHost");

        // EnemyHealthSystem fields
        public static readonly AccessTools.FieldRef<EnemyHealthSystem, bool> EnemyExploding =
            AccessTools.FieldRefAccess<EnemyHealthSystem, bool>("_exploding");
        public static readonly AccessTools.FieldRef<EnemyHealthSystem, float> EnemyImmuneTime =
            AccessTools.FieldRefAccess<EnemyHealthSystem, float>("_immuneTime");

        // DiscLauncher methods
        public static readonly MethodInfo DiscLauncherLaunchDiscClientRpc =
            AccessTools.Method(typeof(DiscLauncher), "LaunchDiscClientRpc");
        public static readonly MethodInfo DiscLauncherImpact =
            AccessTools.Method(typeof(DiscLauncher), "Impact");
    }

    // Every enemy death ends here: direct damage, lava (Disintegrate), roller struts, stuck in walls.
    // Explode can be called more than once, the same checks as the original keep only the call that kills.
    [HarmonyPatch(typeof(EnemyHealthSystem), "Explode")]
    class EnemyDeathCountPatch
    {
        static void Prefix(EnemyHealthSystem __instance)
        {
            try
            {
                if (EnemyReflectionCache.EnemyExploding(__instance) ||
                    __instance.ignoreIndirectDamage ||
                    !(bool)EnemyReflectionCache.IsHostProperty.GetValue(__instance))
                {
                    return;
                }

                HitLogic.OnEnemyDeath(__instance);
            }
            catch (System.Exception ex)
            {
                Logger.LogError($"Error recording enemy kill: {ex.Message}");
            }
        }
    }

    [HarmonyPatch(typeof(EnemyHealthSystem), "Disintegrate")]
    class EnemyDisintegratePatch
    {
        static void Prefix(EnemyHealthSystem __instance)
        {
            try
            {
                if (__instance.IsDead() || __instance.ignoreIndirectDamage) return;
                HitLogic.OnEnemyDisintegrating(__instance);
            }
            catch (System.Exception ex)
            {
                Logger.LogError($"Error tracking enemy lava contact: {ex.Message}");
            }
        }
    }

    [HarmonyPatch(typeof(SpiderSurge.BanishedTarget), nameof(SpiderSurge.BanishedTarget.BeginEnemy))]
    class EnemyBanishedPatch
    {
        static void Prefix(EnemyHealthSystem enemy, PlayerInput caster)
        {
            try
            {
                if (enemy == null || SpiderSurge.BanishedTarget.IsBanished(enemy.gameObject)) return;
                HitLogic.OnEnemyBanished(enemy, caster);
            }
            catch (System.Exception ex)
            {
                Logger.LogError($"Error recording banished enemy: {ex.Message}");
            }
        }
    }

    [HarmonyPatch(typeof(EnemyHealthSystem), "DestroyShield")]
    class EnemyShieldDestroyedPatch
    {
        static void Prefix(EnemyHealthSystem __instance)
        {
            try
            {
                HitLogic.OnEnemyShieldDestroyed(__instance);
            }
            catch (System.Exception ex)
            {
                Logger.LogError($"Error recording enemy shield break: {ex.Message}");
            }
        }
    }

    // DiscLauncher - Track disc projectiles and their owners
    [HarmonyPatch(typeof(DiscLauncher), "LaunchDisc")]
    class DiscLauncherLaunchDiscPatch
    {
        static bool Prefix(DiscLauncher __instance)
        {
            try
            {
                // Replicate the entire LaunchDisc function with our addition
                if (__instance.ammo <= 0f)
                {
                    return false; // Skip original
                }

                float ammo = __instance.ammo;
                __instance.ammo = ammo - 1f;

                // Create the disc projectile
                GameObject gameObject = Object.Instantiate(__instance.discProjectile, __instance.mountedDisc.transform.position, __instance.point.rotation);
                gameObject.GetComponent<NetworkObject>().Spawn(true);
                gameObject.GetComponent<Rigidbody2D>().AddForce(__instance.transform.up * __instance.shotForce, ForceMode2D.Impulse);

                // **THIS IS OUR ADDITION** - Track the disc owner
                PlayerInput ownerPlayer = __instance.owner?.healthSystem?.GetComponentInParent<PlayerInput>();
                HitLogic.RegisterDiscOwner(gameObject, ownerPlayer);
                AccuracyTracker.Fire(__instance, gameObject);

                // Reset phase effect and visual elements
                __instance.discPhaseEffect.ResetEffect();
                __instance.mountedDisc.SetActive(false);
                __instance.targetingLasers.SetActive(false);

                if ((bool)EnemyReflectionCache.IsHostProperty.GetValue(__instance))
                {
                    // Call LaunchDiscClientRpc using cached reflection
                    EnemyReflectionCache.DiscLauncherLaunchDiscClientRpc?.Invoke(__instance, null);
                }

                // Call Impact method using cached reflection
                EnemyReflectionCache.DiscLauncherImpact?.Invoke(__instance, new object[] { __instance.transform.up * -__instance.recoil, __instance.point.position, false, true });

                return false; // Skip the original method
            }
            catch (System.Exception ex)
            {
                Logger.LogError($"Error in DiscLauncher LaunchDisc patch: {ex.Message}");
                return true; // Execute original method on error
            }
        }
    }

    // Patch Weapon.Equip to set the correct ownerWeaponClientId after the original method runs
    [HarmonyPatch(typeof(Weapon), "Equip")]
    class WeaponEquipPatch
    {
        static void Postfix(Weapon __instance, WeaponManager wm)
        {
            try
            {
                AccuracyTracker.Forget(__instance);
                // After the original Equip method runs, fix the ownerWeaponClientId
                if (wm != null && wm.healthSystem != null)
                {
                    PlayerController playerController = wm.healthSystem.GetComponentInParent<PlayerController>();
                    if (playerController != null)
                    {
                        // Override the ulong.MaxValue that was set by the original method
                        __instance.ownerWeaponClientId = (ulong)playerController.playerID.Value;
                    }

                }
            }
            catch (System.Exception ex)
            {
                Logger.LogError($"Error in WeaponEquipPatch: {ex.Message}");
            }
        }
    }

}
