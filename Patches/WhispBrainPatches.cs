using HarmonyLib;
using UnityEngine;
using Unity.Netcode;
using System.Reflection;
using System.Collections.Generic;

namespace SpiderSurge.Patches;

[HarmonyPatch(typeof(WhispBrain), "LaunchChargeAttack")]
public class WhispBrain_LaunchChargeAttack_Patch
{
    private static MethodInfo _stopChargeCosmeticsRpc;
    private static MethodInfo _playAttackCosmeticsRpc;
    private static FieldInfo _shotCooldownTillField;
    private static FieldInfo _targetField;
    private static FieldInfo _obstacleLayerField;

    [HarmonyPrefix]
    public static bool Prefix(WhispBrain __instance)
    {
        if (!SurgeGameModeManager.IsSurgeRunActive) return true;
        if (!__instance.gameObject.activeSelf) return false;

        // Initialize reflection cache
        _stopChargeCosmeticsRpc ??= AccessTools.Method(typeof(WhispBrain), "StopChargeCosmeticsClientRpc");
        _playAttackCosmeticsRpc ??= AccessTools.Method(typeof(WhispBrain), "PlayAttackCosmeticsClientRpc");
        _shotCooldownTillField ??= AccessTools.Field(typeof(WhispBrain), "_shotCooldownTill");
        _targetField ??= AccessTools.Field(typeof(WhispBrain), "target"); // On EnemyBrain usually
        _obstacleLayerField ??= AccessTools.Field(typeof(WhispBrain), "obstacleLayer"); // On EnemyBrain

        _stopChargeCosmeticsRpc.Invoke(__instance, null);

        var target = (Transform)_targetField.GetValue(__instance);

        if (target)
        {
            float maxEngagementRange = __instance.maxEngagementRange;
            LayerMask obstacleLayer = (LayerMask)_obstacleLayerField.GetValue(__instance);

            RaycastHit2D hit = Physics2D.Raycast(__instance.transform.position, target.position - __instance.transform.position, maxEngagementRange * 2f, obstacleLayer);
            if (!hit || hit.transform != target)
            {
                return false;
            }

            GameObject projectilePrefab = __instance.projectile;
            Transform gunPoint = __instance.gunPoint;

            // Twin Whisp Logic
            if (__instance.name.Contains("TwinWhisp"))
            {
                float margin = Consts.Values.Enemies.TwinWhispShotMargin;
                Vector3 right = gunPoint.right;

                Vector3[] spawnPositions =
                [
                    gunPoint.position + right * margin,
                    gunPoint.position - right * margin
                ];

                foreach (var pos in spawnPositions)
                {
                    GameObject gameObject = UnityEngine.Object.Instantiate(projectilePrefab, pos, gunPoint.rotation);
                    InitializeProjectile(gameObject, __instance, gunPoint);
                }
            }
            else
            {
                // Standard logic
                GameObject gameObject = UnityEngine.Object.Instantiate(projectilePrefab, gunPoint.position, gunPoint.rotation);
                InitializeProjectile(gameObject, __instance, gunPoint);
            }

            // Play Cosmetics
            _playAttackCosmeticsRpc.Invoke(__instance, null);

            // Apply Recoil
            if (__instance.TryGetComponent<Rigidbody2D>(out var rb))
            {
                rb.AddForce(-gunPoint.up * __instance.movementForce, ForceMode2D.Impulse);
            }
            else
            {
                Debug.LogWarning($"[WhispBrainPatch] WhispBrain '{__instance.name}' (ID: {__instance.NetworkObjectId}) missing Rigidbody2D. Cannot apply recoil.");
            }

            // Set Cooldown
            _shotCooldownTillField.SetValue(__instance, Time.time + __instance.shotCooldown);
        }

        return false;
    }

    private static void InitializeProjectile(GameObject gameObject, WhispBrain __instance, Transform gunPoint)
    {
        // Handle RailShot if present
        RailShot rs = gameObject.GetComponent<RailShot>();
        rs?.RailFromEnemy = true;

        // Handle NetworkObject
        NetworkObject netObj = gameObject.GetComponent<NetworkObject>();
        netObj?.Spawn(true);

        // Add Force
        float force = __instance.shotForce;
        Vector3 v = gunPoint.up * force;
        var rb = gameObject.GetComponent<Rigidbody2D>();
        rb?.AddForce(v, ForceMode2D.Impulse);

        // Configure Projectile Properties
        if (rs is not null)
        {
            rs.ignore.Add(__instance.gameObject);
            rs.railColor = __instance.energyColor;
        }
        else
        {
            // Custom logic for non-RailShot projectiles (Explosive/BasicProjectile)
            BasicProjectile bp = gameObject.GetComponent<BasicProjectile>();
            bp?.projectileOwnerId = __instance.NetworkObjectId;

            // Ignore collision with shooter
            var projCollider = gameObject.GetComponent<Collider2D>();
            var shooterCollider = __instance.GetComponent<Collider2D>();
            if (projCollider is not null && shooterCollider is not null)
            {
                Physics2D.IgnoreCollision(projCollider, shooterCollider, true);
            }
        }
    }
}
