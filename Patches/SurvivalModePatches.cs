using HarmonyLib;
using System;
using System.Reflection;
using UnityEngine;
using SpiderSurge.Enemies;
using Logger = Silk.Logger;

namespace SpiderSurge
{
    [HarmonyPatch(typeof(SurvivalMode), "StartGame")]
    public class SurvivalMode_StartGame_Patch
    {
        [HarmonyPrefix]
        public static void Prefix(ref SurvivalConfig survivalConfig)
        {
            if (ModConfig.enableSurgeMode && survivalConfig != null)
            {
                // Find templates
                GameObject meleeWhispPrefab = null;
                GameObject powerMeleeWhispPrefab = null;
                GameObject whispPrefab = null;
                GameObject shieldSource = null;

                foreach (var enemy in survivalConfig.enemies)
                {
                    if (enemy.enemyObject != null)
                    {
                        if (enemy.enemyObject.name.Contains("MeleeWhisp") && !enemy.enemyObject.name.Contains("Power"))
                        {
                            meleeWhispPrefab = enemy.enemyObject;
                        }
                        else if (enemy.enemyObject.name.Contains("MeleeWhisp") && enemy.enemyObject.name.Contains("Power"))
                        {
                            powerMeleeWhispPrefab = enemy.enemyObject;
                        }
                        else if (enemy.enemyObject.name.Contains("Whisp") && !enemy.enemyObject.name.Contains("Melee") && !enemy.enemyObject.name.Contains("Power"))
                        {
                            whispPrefab = enemy.enemyObject;
                        }

                        if (shieldSource == null)
                        {
                            var ehs = enemy.enemyObject.GetComponent<EnemyHealthSystem>();
                            if (ehs != null && ehs.shield != null)
                            {
                                shieldSource = enemy.enemyObject;
                            }
                        }
                    }
                }

                GameObject rocketProjectile = null;
                if (CustomEnemies.MissileWhispPrefab == null)
                {
                    var projectiles = Resources.FindObjectsOfTypeAll<BasicProjectile>();
                    foreach (var p in projectiles)
                    {
                        if (p.name == "Rocket")
                        {
                            rocketProjectile = p.gameObject;
                            break;
                        }
                    }
                }

                // Initialize Custom Enemies if missing
                if (CustomEnemies.TwinBladeMeleeWhispPrefab == null && meleeWhispPrefab != null)
                {
                    CustomEnemies.CreateTwinBladeMeleeWhisp(meleeWhispPrefab);
                }
                if (CustomEnemies.TwinBladePowerMeleeWhispPrefab == null && powerMeleeWhispPrefab != null)
                {
                    CustomEnemies.CreateTwinBladePowerMeleeWhisp(powerMeleeWhispPrefab);
                }
                if (CustomEnemies.MissileWhispPrefab == null && whispPrefab != null && rocketProjectile != null)
                {
                    CustomEnemies.CreateMissileWhisp(whispPrefab, rocketProjectile, shieldSource);
                }

                SurvivalConfig surgeConfig = UnityEngine.Object.Instantiate(survivalConfig);
                surgeConfig.name = survivalConfig.name + "_Surge";

                // Alter the enemy budget
                surgeConfig.startingBudget *= Consts.Values.Enemies.SpawnCountMultiplier;
                surgeConfig.budgetPerWave *= Consts.Values.Enemies.SpawnCountMultiplier;
                surgeConfig.budgetPerPlayer *= Consts.Values.Enemies.SpawnCountMultiplier;

                // Update enemy stats from config
                foreach (var enemy in surgeConfig.enemies)
                {
                    if (enemy.enemyObject != null && Consts.Values.CustomEnemyStats.TryGetValue(enemy.enemyObject.name, out var stats))
                    {
                        enemy.cost = stats.Cost;
                        enemy.minWave = stats.MinWave;
                        enemy.maxWave = stats.MaxWave;
                    }
                }

                // Add custom enemies to the surge config
                if (CustomEnemies.TwinBladeMeleeWhispPrefab != null && Consts.Values.CustomEnemyStats.TryGetValue("TwinBladeMeleeWhisp", out var doubleStats))
                {
                    surgeConfig.enemies.Add(new SurvivalEnemy(CustomEnemies.TwinBladeMeleeWhispPrefab, doubleStats.Cost, doubleStats.MinWave, doubleStats.MaxWave));
                }

                if (CustomEnemies.TwinBladePowerMeleeWhispPrefab != null && Consts.Values.CustomEnemyStats.TryGetValue("TwinBladePowerMeleeWhisp", out var twinStats))
                {
                    surgeConfig.enemies.Add(new SurvivalEnemy(CustomEnemies.TwinBladePowerMeleeWhispPrefab, twinStats.Cost, twinStats.MinWave, twinStats.MaxWave));
                }

                if (CustomEnemies.MissileWhispPrefab != null && Consts.Values.CustomEnemyStats.TryGetValue("MissileWhisp", out var missileStats))
                {
                    surgeConfig.enemies.Add(new SurvivalEnemy(CustomEnemies.MissileWhispPrefab, missileStats.Cost, missileStats.MinWave, missileStats.MaxWave));
                }

                if (CustomEnemies.ShieldedMissileWhispPrefab != null && Consts.Values.CustomEnemyStats.TryGetValue("ShieldedMissileWhisp", out var shieldedStats))
                {
                    surgeConfig.enemies.Add(new SurvivalEnemy(CustomEnemies.ShieldedMissileWhispPrefab, shieldedStats.Cost, shieldedStats.MinWave, shieldedStats.MaxWave));
                }

                if (CustomEnemies.ShieldedTwinWhispPrefab != null && Consts.Values.CustomEnemyStats.TryGetValue("ShieldedTwinWhisp", out var shieldedTwinStats))
                {
                    surgeConfig.enemies.Add(new SurvivalEnemy(CustomEnemies.ShieldedTwinWhispPrefab, shieldedTwinStats.Cost, shieldedTwinStats.MinWave, shieldedTwinStats.MaxWave));
                }

                if (CustomEnemies.TwinWhispPrefab == null && whispPrefab != null)
                {
                    CustomEnemies.CreateTwinWhisp(whispPrefab, shieldSource);
                }

                if (CustomEnemies.TwinWhispPrefab != null && Consts.Values.CustomEnemyStats.TryGetValue("TwinWhisp", out var twinWhispStats))
                {
                    surgeConfig.enemies.Add(new SurvivalEnemy(CustomEnemies.TwinWhispPrefab, twinWhispStats.Cost, twinWhispStats.MinWave, twinWhispStats.MaxWave));
                }

                survivalConfig = surgeConfig;
            }
        }

        [HarmonyPostfix]
        public static void Postfix(bool __result, SurvivalConfig survivalConfig)
        {
            if (__result && ModConfig.enableSurgeMode)
            {
                if (SurgeGameModeManager.Instance == null) return;
                SurgeGameModeManager.Instance.SetActive(true);
                // Reset perk selection for new game
                PerksManager.Instance.ResetPerks();
                // Refresh high score display to show Surge scores
                var eventField = typeof(SurvivalMode).GetField("onHighScoreUpdated", BindingFlags.Public | BindingFlags.Static);
                if (eventField != null)
                {
                    var action = (Action<int>)eventField.GetValue(null);
                    action?.Invoke(SurvivalMode.instance.GetHighScore());
                }
            }
        }
    }

    [HarmonyPatch(typeof(SurvivalMode), "set_CurrentWave")]
    public class SurvivalMode_set_CurrentWave_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(int value)
        {
            if (SurgeGameModeManager.Instance != null && SurgeGameModeManager.Instance.IsActive && value > 0)
            {
                Logger.LogInfo($"[Surge] Survival Mode Wave Set: {value}");
                // Reset cooldowns for all abilities for all active players
                if (PlayerAbilityHandler.ActiveSpiderControllers != null)
                {
                    for (int i = PlayerAbilityHandler.ActiveSpiderControllers.Count - 1; i >= 0; i--)
                    {
                        var controller = PlayerAbilityHandler.ActiveSpiderControllers[i];
                        if (controller == null)
                        {
                            PlayerAbilityHandler.ActiveSpiderControllers.RemoveAt(i);
                            continue;
                        }

                        var playerAbilities = controller.GetComponents<BaseAbility>();
                        foreach (var ability in playerAbilities)
                        {
                            ability.SetCooldownToZero();
                        }
                    }
                }

                // At wave 30 (Ult Upgrade), set flag for special perk selection
                if (value == Consts.Values.Waves.UltUpgradeWave)
                {
                    Logger.LogInfo($"[Surge] Reached Wave {value}. Triggering Ult Upgrade Selection.");
                    PerksManager.Instance.IsUltUpgradePerkSelection = true;
                }

                // At wave 60 (Ult Switch), set flag for special perk selection
                if (value == Consts.Values.Waves.UltSwapWave)
                {
                    Logger.LogInfo($"[Surge] Reached Wave {value}. Triggering Ult Switch Selection.");
                    PerksManager.Instance.IsUltSwapPerkSelection = true;
                }

                // Update InterdimensionalStorageAbility cache
                var abilities = UnityEngine.Object.FindObjectsOfType<InterdimensionalStorageAbility>();
                foreach (var ab in abilities)
                {
                    ab.UpdateCachedModifierLevels();
                }
            }
        }
    }

}