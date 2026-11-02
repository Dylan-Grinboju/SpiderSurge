using System;
using HarmonyLib;
using UnityEngine;
using Logger = Silk.Logger;

namespace SpiderSurge.Common
{
    // A client that joins after a spider spawned never gets its SpawnSpiderClientRpc, so the player's spider fields are set here once the spider sits under its player
    internal sealed class SpiderLink : MonoBehaviour
    {
        private void Start() => Link();

        private void OnTransformParentChanged() => Link();

        private void Link()
        {
            try
            {
                var player = GetComponentInParent<PlayerController>();
                if (player == null || player.spiderHealthSystem != null)
                {
                    return;
                }

                var health = GetComponentInChildren<SpiderHealthSystem>();
                if (health == null)
                {
                    return;
                }
                GameFields.SetSpiderHealthSystem?.Invoke(player, new object[] { health });
                if (GameFields.ActivePlayerCharacter != null && GameFields.ActivePlayerCharacter(player) == null)
                {
                    GameFields.ActivePlayerCharacter(player) = gameObject;
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Could not link a late-joined spider to its player: {ex.Message}");
            }
        }
    }

    [HarmonyPatch(typeof(SpiderController), nameof(SpiderController.OnNetworkSpawn))]
    internal static class SpiderLinkSpawnPatch
    {
        private static void Postfix(SpiderController __instance)
        {
            if (!__instance.IsServer && __instance.GetComponent<SpiderLink>() == null)
            {
                __instance.gameObject.AddComponent<SpiderLink>();
            }
        }
    }
}
