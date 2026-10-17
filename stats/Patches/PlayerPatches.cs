using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.InputSystem;
using Logger = Silk.Logger;

namespace StatsMod
{
    // Cache reflection field info for performance
    internal static class ReflectionCache
    {
        public static readonly FieldInfo PrimaryColorField =
            typeof(SpiderCustomizer).GetField("_primaryColor", BindingFlags.NonPublic | BindingFlags.Instance);

        public static readonly FieldInfo SecondaryColorField =
            typeof(SpiderCustomizer).GetField("_secondaryColor", BindingFlags.NonPublic | BindingFlags.Instance);

        public static readonly AccessTools.FieldRef<SpiderHealthSystem, float> SpiderImmuneTill =
            AccessTools.FieldRefAccess<SpiderHealthSystem, float>("_immuneTill");

        public static readonly AccessTools.FieldRef<SpiderHealthSystem, bool> SpiderDying =
            AccessTools.FieldRefAccess<SpiderHealthSystem, bool>("_dying");
    }

    [HarmonyPatch(typeof(PlayerInput), "OnEnable")]
    public class PlayerInputEnablePatch
    {
        static void Postfix(PlayerInput __instance)
        {
            try
            {
                // Guard against race condition: if the player left before OnEnable completed
                // Happens when playing with controllers and in the pause menu of a match you use mouse/keyboard
                if (__instance == null || !__instance.enabled || !__instance.gameObject.activeInHierarchy)
                {
                    Logger.LogDebug($"Skipping registration for inactive/destroyed player: {__instance}");
                    return;
                }

                PlayerTracker.Instance.RegisterPlayer(__instance);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error in PlayerInput.OnEnable patch: {ex.Message}");
            }
        }
    }

    [HarmonyPatch(typeof(PlayerInput), "OnDisable")]
    public class PlayerInputDisablePatch
    {
        static void Prefix(PlayerInput __instance)
        {
            try
            {
                PlayerTracker.Instance.UnregisterPlayer(__instance);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error in PlayerInput.OnDisable patch: {ex.Message}");
            }
        }
    }

    [HarmonyPatch(typeof(SpiderHealthSystem), "DisintegrateLegsAndDestroy")]
    public class SpiderHealthSystemDisintegrateLegsAndDestroyPatch
    {
        static void Prefix(SpiderHealthSystem __instance)
        {
            try
            {
                if (!GameSessionManager.Instance.IsActive) return;
                PlayerTracker.Instance.RecordPlayerDeath(__instance);
                HitLogic.OnPlayerDeath(__instance);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error in SpiderHealthSystem.DisintegrateLegsAndDestroy patch: {ex.Message}");
            }
        }
    }

    [HarmonyPatch(typeof(SpiderSurge.BanishedTarget), nameof(SpiderSurge.BanishedTarget.KillSpider))]
    public class SpiderBanishedPatch
    {
        static void Prefix(SpiderHealthSystem spider, PlayerInput caster)
        {
            try
            {
                HitLogic.OnPlayerBanished(spider, caster);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error recording banished spider: {ex.Message}");
            }
        }
    }

    [HarmonyPatch(typeof(SpiderHealthSystem), "DisableDeathEffect")]
    public class SpiderHealthSystemDisableDeathEffect
    {
        static void Prefix(SpiderHealthSystem __instance)
        {
            try
            {
                if (!GameSessionManager.Instance.IsActive) return;
                PlayerTracker.Instance.UndoPlayerDeath(__instance);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error in SpiderHealthSystem.DisableDeathEffect patch: {ex.Message}");
            }
        }
    }

    // Lava and falling out of the map call Disintegrate every tick, also on immune, shielded and already dying spiders.
    // This coroutine starts once per spider, when the disintegration kills it; a dead or astral spider only dissolves.
    [HarmonyPatch(typeof(SpiderHealthSystem), "DisintegrateSpiderAndDestroy")]
    public class SpiderHealthSystemDisintegratePatch
    {
        static void Prefix(SpiderHealthSystem __instance)
        {
            try
            {
                // SpiderSurge's Banish kills through the same dissolve, without lava
                if (!GameSessionManager.Instance.IsActive || __instance.dead || __instance.astralDead || __instance.rootObject == null ||
                    SpiderSurge.BanishedTarget.IsBanished(__instance.rootObject))
                    return;

                PlayerInput playerInput = __instance.rootObject.GetComponentInParent<PlayerInput>();
                if (playerInput != null)
                {
                    Logger.LogInfo($"Lava death for Player {playerInput.playerIndex + 1} at {__instance.transform.position}");
                    PlayerTracker.Instance.IncrementLavaDeaths(playerInput);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error in SpiderHealthSystem.DisintegrateSpiderAndDestroy patch: {ex.Message}");
            }
        }
    }

    [HarmonyPatch(typeof(SpiderCustomizer), "SetSpiderColor")]
    public class SpiderCustomizerSetSpiderColorPatch
    {
        static void Postfix(SpiderCustomizer __instance)
        {
            try
            {
                PlayerInput playerInput = __instance.GetComponentInParent<PlayerInput>();
                if (playerInput != null && ReflectionCache.PrimaryColorField != null)
                {
                    Color primaryColor = (Color)ReflectionCache.PrimaryColorField.GetValue(__instance);
                    PlayerTracker.Instance.UpdatePlayerColor(playerInput, primaryColor);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error in SpiderCustomizer.SetSpiderColor patch: {ex.Message}");
            }
        }
    }

    [HarmonyPatch(typeof(PlayerController), "SpawnCharacter", new Type[] { typeof(Vector3), typeof(Quaternion) })]
    public class PlayerControllerSpawnCharacterPatch
    {
        static void Postfix(PlayerController __instance)
        {
            try
            {
                if (GameSessionManager.Instance.IsActive)
                {
                    PlayerTracker.Instance.RecordPlayerRespawn(__instance);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error in PlayerController.SpawnCharacter patch: {ex.Message}");
            }
        }
    }

    [HarmonyPatch(typeof(SpiderHealthSystem), "BreakShield")]
    class PlayerShieldBreakPatch
    {
        static void Prefix(SpiderHealthSystem __instance)
        {
            try
            {
                if (!GameSessionManager.Instance.IsActive) return;
                if (__instance.rootObject != null)
                {
                    PlayerInput victimPlayerInput = __instance.rootObject.GetComponentInParent<PlayerInput>();
                    if (victimPlayerInput != null)
                    {
                        PlayerTracker.Instance.IncrementShieldsLost(victimPlayerInput);
                    }
                }
                HitLogic.OnPlayerShieldBroken(__instance);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error tracking player shield loss: {ex.Message}");
            }
        }
    }

    [HarmonyPatch(typeof(WebMaker), "ShootWeb")]
    public class WebMakerShootWebPatch
    {
        static void Postfix(WebMaker __instance, GameObject ___target)
        {
            try
            {
                if (___target == null)
                {
                    return;
                }

                if (__instance.spiderController != null && GameSessionManager.Instance.IsActive)
                {
                    PlayerInput playerInput = __instance.spiderController.GetComponentInParent<PlayerInput>();
                    if (playerInput != null)
                    {
                        PlayerTracker.Instance.IncrementWebSwings(playerInput);
                        PlayerTracker.Instance.StartWebSwingTimer(playerInput);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error tracking web swing: {ex.Message}");
            }
        }
    }

    [HarmonyPatch(typeof(WebMaker), "DisconnectWeb")]
    public class WebMakerDisconnectWebPatch
    {
        static void Prefix(WebMaker __instance)
        {
            try
            {
                if (__instance.spiderController != null)
                {
                    PlayerInput playerInput = __instance.spiderController.GetComponentInParent<PlayerInput>();
                    if (playerInput != null)
                    {
                        PlayerTracker.Instance.StopWebSwingTimer(playerInput);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error stopping web swing timer: {ex.Message}");
            }
        }
    }

    [HarmonyPatch(typeof(Stabilizer), "FixedUpdate")]
    public class StabilizerFixedUpdatePatch
    {
        // Cache to avoid expensive GetComponentInParent calls every FixedUpdate
        private static readonly Dictionary<Stabilizer, (PlayerInput, SpiderController)> _componentCache = new Dictionary<Stabilizer, (PlayerInput, SpiderController)>();
        private static readonly object _cacheLock = new object();

        static void Postfix(Stabilizer __instance)
        {
            try
            {
                if (!GameSessionManager.Instance.IsActive)
                    return;

                var (playerInput, spider) = GetCachedComponents(__instance);
                if (playerInput == null || spider == null) return;

                PlayerTracker.Instance.UpdateHighestPoint(playerInput, spider);

                if (!__instance.grounded)
                {
                    PlayerTracker.Instance.StartAirborneTimer(playerInput);
                }
                else
                {
                    PlayerTracker.Instance.StopAirborneTimer(playerInput);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error tracking airborne time: {ex.Message}");
            }
        }

        private static (PlayerInput, SpiderController) GetCachedComponents(Stabilizer stabilizer)
        {
            lock (_cacheLock)
            {
                if (_componentCache.TryGetValue(stabilizer, out var cached))
                {
                    // Verify the cached references are still valid
                    if (cached.Item1 != null && cached.Item2 != null) return cached;
                    _componentCache.Remove(stabilizer);
                }

                // Cache miss - do the expensive lookup once
                if (stabilizer.TryGetComponent(out SpiderController spider) ||
                    (spider = stabilizer.GetComponentInParent<SpiderController>()) != null)
                {
                    PlayerInput playerInput = spider.GetComponentInParent<PlayerInput>();
                    if (playerInput != null && spider != null)
                    {
                        var components = (playerInput, spider);
                        _componentCache[stabilizer] = components;
                        return components;
                    }
                }

                return (null, null);
            }
        }

        // Call this when players are removed to clean up cache
        public static void ClearCache()
        {
            lock (_cacheLock)
            {
                _componentCache.Clear();
            }
        }
    }
}
