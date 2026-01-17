using HarmonyLib;
using Silk;
using Logger = Silk.Logger;
using UnityEngine.SceneManagement;
using System;
using System.Reflection;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SpiderSurge
{
    [HarmonyPatch(typeof(SurvivalMode), "StopGameMode")]
    public class SurvivalMode_StopGameMode_Patch
    {
        [HarmonyPostfix]
        public static void Postfix()
        {
            if (SurgeGameModeManager.Instance.IsActive)
            {
                Logger.LogInfo("Surge mode ended");
                SurgeGameModeManager.Instance.SetActive(false);
            }
        }
    }

    [HarmonyPatch(typeof(LobbyController), "OnSceneLoaded")]
    public class LobbyController_OnSceneLoaded_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(Scene scene)
        {
            if (scene.name == "Lobby" && SurgeGameModeManager.Instance.IsActive)
            {
                Logger.LogInfo("Surge mode ended (lobby entered)");
                SurgeGameModeManager.Instance.SetActive(false);
            }
        }
    }

    [HarmonyPatch(typeof(SpiderHealthSystem), "ExplodeInDirection")]
    public class SpiderHealthSystem_ExplodeInDirection_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(SpiderHealthSystem __instance)
        {
            // Reset timers on player death
            PlayerInput playerInput = __instance.rootObject.GetComponentInParent<PlayerInput>();
            if (playerInput != null && PlayerStateTracker.Instance != null)
            {
                PlayerStateTracker.Instance.ResetTime(playerInput, "stillness");
                PlayerStateTracker.Instance.ResetTime(playerInput, "airborne");
                Logger.LogInfo($"Reset timers for player {playerInput.playerIndex} on death");
            }
        }
    }

    [HarmonyPatch(typeof(PlayerController), "SpawnCharacter", new Type[] { typeof(Vector3), typeof(Quaternion) })]
    public class PlayerController_SpawnCharacter_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(PlayerController __instance)
        {
            // Reset timers on player respawn
            PlayerInput playerInput = __instance.GetComponent<PlayerInput>();
            if (playerInput != null && PlayerStateTracker.Instance != null)
            {
                PlayerStateTracker.Instance.ResetTime(playerInput, "stillness");
                PlayerStateTracker.Instance.ResetTime(playerInput, "airborne");
                Logger.LogInfo($"Reset timers for player {playerInput.playerIndex} on respawn");
            }
        }
    }
}