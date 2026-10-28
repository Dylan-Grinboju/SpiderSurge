using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using UnityEngine;
using SpiderSurge.Common;
using Logger = Silk.Logger;

namespace StatsMod
{
    // Cache reflection field info for performance
    // Local and remote players alike enter the lobby's list once their PlayerController has spawned
    [HarmonyPatch(typeof(LobbyController), nameof(LobbyController.AddPlayer))]
    public class LobbyAddPlayerPatch
    {
        static void Postfix(PlayerController player)
        {
            try
            {
                PlayerTracker.Instance.RegisterPlayer(player);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error in LobbyController.AddPlayer patch: {ex.Message}");
            }
        }
    }

    [HarmonyPatch(typeof(LobbyController), nameof(LobbyController.RemovePlayer))]
    public class LobbyRemovePlayerPatch
    {
        [HarmonyBefore("com.SpiderSurge.teams")]
        static void Prefix(PlayerController player)
        {
            try
            {
                PlayerTracker.Instance.UnregisterPlayer(player);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error in LobbyController.RemovePlayer patch: {ex.Message}");
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

    [HarmonyPatch(typeof(SpiderHealthSystem), "DisableDeathEffect")]
    public class SpiderHealthSystemDisableDeathEffect
    {
        static void Prefix(SpiderHealthSystem __instance)
        {
            try
            {
                if (!GameSessionManager.Instance.IsActive) return;
                PlayerTracker.Instance.UndoPlayerDeath(__instance);
                HitLogic.OnPlayerRevived(__instance);
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
                // SpiderSurge's Black Hole kills through the same dissolve, without lava
                if (!GameSessionManager.Instance.IsActive || __instance.dead || __instance.astralDead || __instance.rootObject == null ||
                    SpiderSurge.SwallowedTarget.IsSwallowed(__instance.rootObject))
                    return;

                PlayerController playerInput = __instance.rootObject.GetComponentInParent<PlayerController>();
                if (playerInput != null)
                {
                    Logger.LogInfo($"Lava death for Player {playerInput.playerID.Value + 1} at {__instance.transform.position}");
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
                PlayerController playerInput = __instance.GetComponentInParent<PlayerController>();
                if (playerInput != null)
                {
                    PlayerTracker.Instance.UpdatePlayerColor(playerInput, __instance.GetPrimaryColor());
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
                HitLogic.OnPlayerRespawned(__instance);
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
                    PlayerController victimPlayerController = __instance.rootObject.GetComponentInParent<PlayerController>();
                    if (victimPlayerController != null)
                    {
                        PlayerTracker.Instance.IncrementShieldsLost(victimPlayerController);
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
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            int tracked = 0;
            MethodInfo raycast = AccessTools.Method(typeof(WebMaker), nameof(WebMaker.RaycastWeb));
            foreach (CodeInstruction instruction in instructions)
            {
                yield return instruction;
                if (!instruction.Calls(raycast)) continue;
                yield return new CodeInstruction(OpCodes.Ldarg_0);
                yield return CodeInstruction.Call(typeof(WebMakerShootWebPatch), nameof(RecordSwing));
                tracked++;
            }
            if (tracked != 1) Logger.LogWarning($"WebMaker.ShootWeb raycast changed ({tracked} sites); check web swing tracking");
        }

        private static RaycastHit2D RecordSwing(RaycastHit2D hit, WebMaker webMaker)
        {
            try
            {
                if (!hit)
                {
                    return hit;
                }

                if (webMaker.spiderController != null && GameSessionManager.Instance.IsActive && !IsGone(webMaker.spiderHealthSystem))
                {
                    PlayerController playerInput = webMaker.spiderController.GetComponentInParent<PlayerController>();
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
            return hit;
        }

        internal static bool IsGone(SpiderHealthSystem health) => health != null && (health.dead || health.astralDead);
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
                    PlayerController playerInput = __instance.spiderController.GetComponentInParent<PlayerController>();
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
        private static readonly Dictionary<Stabilizer, (PlayerController, SpiderController, SpiderHealthSystem)> _componentCache = new Dictionary<Stabilizer, (PlayerController, SpiderController, SpiderHealthSystem)>();
        private static readonly object _cacheLock = new object();

        static void Postfix(Stabilizer __instance)
        {
            try
            {
                if (!GameSessionManager.Instance.IsActive)
                    return;

                var (playerInput, spider, health) = GetCachedComponents(__instance);
                if (playerInput == null || spider == null) return;

                if (WebMakerShootWebPatch.IsGone(health))
                {
                    PlayerTracker.Instance.StopAirborneTimer(playerInput);
                    PlayerTracker.Instance.StopWebSwingTimer(playerInput);
                    return;
                }

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

        private static (PlayerController, SpiderController, SpiderHealthSystem) GetCachedComponents(Stabilizer stabilizer)
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
                    PlayerController playerInput = spider.GetComponentInParent<PlayerController>();
                    if (playerInput != null && spider != null)
                    {
                        var components = (playerInput, spider, spider.GetComponentInChildren<SpiderHealthSystem>());
                        _componentCache[stabilizer] = components;
                        return components;
                    }
                }

                return (null, null, null);
            }
        }

        internal static void Forget(Stabilizer stabilizer)
        {
            PlayerController player;
            lock (_cacheLock)
            {
                if (!_componentCache.TryGetValue(stabilizer, out var cached)) return;
                _componentCache.Remove(stabilizer);
                player = cached.Item1;
            }
            if (player != null)
            {
                PlayerTracker.Instance.StopAirborneTimer(player);
                PlayerTracker.Instance.StopWebSwingTimer(player);
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

    [HarmonyPatch(typeof(Stabilizer), "OnDestroy")]
    public class StabilizerOnDestroyPatch
    {
        static void Prefix(Stabilizer __instance)
        {
            try
            {
                StabilizerFixedUpdatePatch.Forget(__instance);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error in Stabilizer.OnDestroy patch: {ex.Message}");
            }
        }
    }
}
