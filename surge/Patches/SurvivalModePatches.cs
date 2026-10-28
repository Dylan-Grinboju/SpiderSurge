using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using SpiderSurge.Enemies;
using Logger = Silk.Logger;

namespace SpiderSurge
{
    [HarmonyPatch(typeof(SurvivalMode), "StartGame")]
    public class SurvivalMode_StartGame_Patch
    {
        private static bool ShouldApplySurge(SurvivalConfig survivalConfig)
        {
            return SurgeGameModeManager.IsModeEnabled
                && survivalConfig != null
                && survivalConfig.type == SurvivalConfig.Type.EndlessSurvival;
        }

        private static SurvivalConfig _originalConfig;

        [HarmonyPrefix]
        public static void Prefix(ref SurvivalConfig survivalConfig)
        {
            if (survivalConfig != null && survivalConfig.name.EndsWith("_Surge"))
            {
                if (_originalConfig != null)
                {
                    survivalConfig = _originalConfig;
                }
            }
            else
            {
                _originalConfig = survivalConfig;
            }

            if (ShouldApplySurge(survivalConfig))
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
                if (meleeWhispPrefab != null)
                {
                    CustomEnemies.CreateTwinBladeMeleeWhisp(meleeWhispPrefab);
                }
                if (powerMeleeWhispPrefab != null)
                {
                    CustomEnemies.CreateTwinBladePowerMeleeWhisp(powerMeleeWhispPrefab);
                }
                if (whispPrefab != null)
                {
                    if (CustomEnemies.MissileWhispPrefab == null && rocketProjectile != null)
                    {
                        CustomEnemies.CreateMissileWhisp(whispPrefab, rocketProjectile, shieldSource);
                    }
                    if (CustomEnemies.TwinWhispPrefab == null)
                    {
                        CustomEnemies.CreateTwinWhisp(whispPrefab, shieldSource);
                    }
                }
                CustomEnemies.CreateWaspCocoon();

                CustomEnemies.CreateLaserVariants(survivalConfig.enemies.Select(e => e.enemyObject).Concat(CustomEnemies.CustomBasePrefabs), shieldSource);

                SurvivalConfig surgeConfig = UnityEngine.Object.Instantiate(survivalConfig);
                surgeConfig.name = survivalConfig.name + "_Surge";

                surgeConfig.startingBudget *= Consts.Values.Enemies.SpawnCountMultiplier;
                surgeConfig.budgetPerWave *= Consts.Values.Enemies.SpawnCountMultiplier;
                surgeConfig.budgetPerPlayer *= Consts.Values.Enemies.SpawnCountMultiplier;
                surgeConfig.difficultyIncreaseThreshold = Consts.Values.Enemies.WavesPerCapacityIncrease;

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
                var customEnemies = new (GameObject prefab, string name)[]
                {
                    (CustomEnemies.TwinBladeMeleeWhispPrefab, "TwinBladeMeleeWhisp"),
                    (CustomEnemies.TwinBladePowerMeleeWhispPrefab, "TwinBladePowerMeleeWhisp"),
                    (CustomEnemies.TwinWhispPrefab, "TwinWhisp"),
                    (CustomEnemies.MissileWhispPrefab, "MissileWhisp"),
                    (CustomEnemies.ShieldedMissileWhispPrefab, "ShieldedMissileWhisp"),
                    (CustomEnemies.ShieldedTwinWhispPrefab, "ShieldedTwinWhisp"),
                    (CustomEnemies.WaspCocoonPrefab, "WaspCocoon"),
                    (CustomEnemies.ShieldedWaspCocoonPrefab, "ShieldedWaspCocoon"),
                };
                foreach (var (prefab, name) in customEnemies)
                {
                    if (prefab != null && Consts.Values.CustomEnemyStats.TryGetValue(name, out var customStats))
                    {
                        surgeConfig.enemies.Add(new SurvivalEnemy(prefab, customStats.Cost, customStats.MinWave, customStats.MaxWave));
                    }
                }

                foreach (var laser in CustomEnemies.LaserPrefabs)
                {
                    if (laser.Value != null && Consts.Values.CustomEnemyStats.TryGetValue(laser.Key, out var laserStats))
                    {
                        surgeConfig.enemies.Add(new SurvivalEnemy(laser.Value, laserStats.Cost, laserStats.MinWave, laserStats.MaxWave));
                    }
                }

                // If Pain Level is 2 or higher, set min wave to 0 for all enemies
                if (SurvivalModeHud.instance != null && SurvivalModeHud.instance.currentPainLevel.Value >= 2)
                {

                    foreach (var enemy in surgeConfig.enemies)
                    {
                        enemy.minWave = 0;
                    }
                }

                survivalConfig = surgeConfig;
            }
        }

        [HarmonyPostfix]
        public static void Postfix(bool __result, SurvivalConfig survivalConfig)
        {
            if (__result)
            {
                if (ShouldApplySurge(survivalConfig))
                {
                    Logger.LogInfo($"=== SURVIVAL CONFIG: {survivalConfig.name} ===");
                    Logger.LogInfo($"Type: {survivalConfig.type} | Total Waves: {survivalConfig.totalWaves}");
                    Logger.LogInfo($"Budget - Start: {survivalConfig.startingBudget}, PerWave: {survivalConfig.budgetPerWave}, PerPlayer: {survivalConfig.budgetPerPlayer}");
                    Logger.LogInfo($"Enemies:");
                    foreach (var enemy in survivalConfig.enemies)
                    {
                        var name = enemy.enemyObject != null ? enemy.enemyObject.name : "Null";
                        Logger.LogInfo($" - {name} | Cost: {enemy.cost} | Waves: {enemy.minWave}-{enemy.maxWave}");
                    }
                    Logger.LogInfo($"=====================================");
                }

                SurgeGameModeManager.EndRun(RunEnd.Starting);

                if (ShouldApplySurge(survivalConfig))
                {
                    if (SurgeGameModeManager.Instance == null) return;
                    SurgeGameModeManager.Instance.SetActive(true);
                    AbilitySelection.Arm();
                    if (SurvivalMode.instance != null && SurvivalMode.instance.CurrentWave > 0) SurvivalMode_set_CurrentWave_Patch.LogWaveStart(SurvivalMode.instance.CurrentWave);

                    if (PlayerAbilityHandler.ActiveSpiderControllers != null)
                    {
                        PlayerAbilityHandler.ResetSpawnTracking();
                    }

                    var eventField = AccessTools.Field(typeof(SurvivalMode), "onHighScoreUpdated");
                    if (eventField != null && SurvivalMode.instance != null)
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
            private static readonly AccessTools.FieldRef<SurvivalMode, SurvivalConfig> ActiveConfig = AccessTools.FieldRefAccess<SurvivalMode, SurvivalConfig>("_config");

            [HarmonyPostfix]
            public static void Postfix(int value)
            {
                if (SurgeGameModeManager.IsSurgeRunActive && SurvivalMode.instance != null && SurvivalMode.instance.GameModeActive() && value > 0)
                {
                    LogWaveStart(value);
                }

                if (SurgeGameModeManager.IsSurgeRunActive && value > 0)
                {
                    PlayerAbilityHandler.OnWaveStarted(value);

                    // At wave 30 (Ult Upgrade), set flag for special perk selection
                    if (value == Consts.Values.Waves.UltUpgradeWave)
                    {
                        if (PerksManager.Instance != null)
                        {
                            PerksManager.Instance.IsUltUpgradePerkSelection = true;
                        }
                    }

                    // At wave 60 (Ult Switch), set flag for special perk selection
                    if (value == Consts.Values.Waves.UltSwapWave)
                    {
                        if (PerksManager.Instance != null)
                        {
                            PerksManager.Instance.IsUltSwapPerkSelection = true;
                        }
                    }

                    SurgeNet.SendStateNow();
                }
            }

            // StartGame sets wave 1 before Surge turns on, so it logs that wave itself
            internal static void LogWaveStart(int value)
            {
                var config = ActiveConfig(SurvivalMode.instance);
                if (config == null) return;

                float budget = config.startingBudget + (float)value * config.budgetPerWave + (float)(LobbyController.instance != null ? LobbyController.instance.GetPlayerCount() : 1) * config.budgetPerPlayer;
                int minPotential = 0, maxPotential = 0;
                float minCost = float.MaxValue, maxCost = 0f;
                foreach (var e in config.enemies)
                {
                    if (e.cost <= budget && e.minWave <= value && (e.maxWave == 0 || e.maxWave >= value))
                    {
                        if (e.cost < minCost) minCost = e.cost;
                        if (e.cost > maxCost) maxCost = e.cost;
                    }
                }
                if (minCost != float.MaxValue && minCost > 0f) maxPotential = (int)(budget / minCost);
                if (maxCost > 0f) minPotential = (int)(budget / maxCost);

                Logger.LogInfo($"[SpiderSurge] --- Wave {value} Started ---");
                Logger.LogInfo($"[SpiderSurge] Budget: {budget} | Estimated amount of enemies that will spawn: {minPotential} to {maxPotential}");
            }
        }

    }

    // Only Surge changes capacity, pacing and bursts; the game's spawn warnings and wave-end checks stay intact
    [HarmonyPatch]
    public class SurvivalMode_SpawnEnemies_Patch
    {
        static MethodBase TargetMethod() => AccessTools.EnumeratorMoveNext(AccessTools.Method(typeof(SurvivalMode), "SpawnEnemies"));

        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            MethodInfo enemiesAlive = AccessTools.Method(typeof(SurvivalMode), "EnemiesAlive");
            MethodInfo min = AccessTools.Method(typeof(Mathf), nameof(Mathf.Min), new[] { typeof(int), typeof(int) });
            var result = new List<CodeInstruction>();
            int countPatch = 0, intervalPatch = 0, pollPatch = 0, burstPatch = 0;
            int stage = 0;
            foreach (CodeInstruction instruction in instructions)
            {
                result.Add(instruction);
                if (stage == 0 && instruction.Calls(enemiesAlive)) stage = 1;
                else if (stage == 1 && instruction.IsStloc()) stage = 2;
                else if (stage == 2 && instruction.IsLdloc())
                {
                    result.Add(CodeInstruction.Call(typeof(SurgeSpawnPressure), nameof(SurgeSpawnPressure.CountForCap)));
                    countPatch++;
                    stage = 3;
                }
                if (stage == 3 && instruction.LoadsConstant(1f))
                {
                    result.Add(CodeInstruction.Call(typeof(SurgeSpawnPressure), nameof(SurgeSpawnPressure.PollInterval)));
                    pollPatch++;
                }
                if (instruction.LoadsConstant(3f))
                {
                    result.Add(CodeInstruction.Call(typeof(SurgeSpawnPressure), nameof(SurgeSpawnPressure.SpawnInterval)));
                    intervalPatch++;
                }
                if (instruction.Calls(min))
                {
                    result.Add(CodeInstruction.Call(typeof(SurgeSpawnPressure), nameof(SurgeSpawnPressure.BurstCount)));
                    burstPatch++;
                }
            }
            if (countPatch != 1 || intervalPatch != 1 || pollPatch != 1 || burstPatch != 1)
                throw new InvalidOperationException("SurvivalMode.SpawnEnemies no longer matches Surge's capacity and pacing patch");
            return result;
        }
    }
}
