using UnityEngine;
using System.Linq;
using Logger = Silk.Logger;

namespace SpiderSurge.Enemies
{
    public static class CustomEnemies
    {
        public static GameObject TwinBladeMeleeWhispPrefab;
        public static GameObject TwinBladePowerMeleeWhispPrefab;
        public static GameObject MissileWhispPrefab;
        public static GameObject ShieldedMissileWhispPrefab;

        public static void CreateTwinBladeMeleeWhisp(GameObject original)
        {
            if (TwinBladeMeleeWhispPrefab != null) return;

            GameObject newEnemyObj = Object.Instantiate(original);
            newEnemyObj.name = "TwinBladeMeleeWhisp";
            Object.DontDestroyOnLoad(newEnemyObj);
            newEnemyObj.SetActive(false);
            TwinBladeMeleeWhispPrefab = newEnemyObj;

            var brain = newEnemyObj.GetComponent<WhispBrain>();
            Transform meleeWeaponTr = null;
            Transform[] allChildren = newEnemyObj.GetComponentsInChildren<Transform>(true);
            meleeWeaponTr = Enumerable.FirstOrDefault(allChildren, t => t.name == "MeleeWeapon");

            if (meleeWeaponTr == null && brain != null) meleeWeaponTr = brain.rotatingBase;

            if (meleeWeaponTr != null)
            {
                Transform bladehandle = null;
                foreach (Transform child in meleeWeaponTr)
                {
                    if (child.name.ToLower().Contains("handle") || child.name.ToLower().Contains("blade"))
                    {
                        bladehandle = child;
                        break;
                    }
                }

                if (bladehandle != null)
                {
                    Transform blade2 = Object.Instantiate(bladehandle, meleeWeaponTr);
                    blade2.name = "SecondBladeHandle";
                    blade2.localPosition = -bladehandle.localPosition;
                    blade2.localRotation = bladehandle.localRotation * Quaternion.Euler(0, 0, 180);
                }
            }

            // Register for Cheats
            RegisterEnemyForCheats(newEnemyObj);
        }

        public static void CreateTwinBladePowerMeleeWhisp(GameObject original)
        {
            if (TwinBladePowerMeleeWhispPrefab != null) return;

            GameObject newEnemyObj = Object.Instantiate(original);
            newEnemyObj.name = "TwinBladePowerMeleeWhisp";
            Object.DontDestroyOnLoad(newEnemyObj);
            newEnemyObj.SetActive(false);
            TwinBladePowerMeleeWhispPrefab = newEnemyObj;

            var brain = newEnemyObj.GetComponent<WhispBrain>();
            Transform meleeWeaponTr = null;
            Transform[] allChildren = newEnemyObj.GetComponentsInChildren<Transform>(true);
            meleeWeaponTr = Enumerable.FirstOrDefault(allChildren, t => t.name == "MeleeWeapon");

            if (meleeWeaponTr == null && brain != null) meleeWeaponTr = brain.rotatingBase;

            if (meleeWeaponTr != null)
            {
                Transform bladehandle = null;
                foreach (Transform child in meleeWeaponTr)
                {
                    if (child.name.ToLower().Contains("handle") || child.name.ToLower().Contains("blade"))
                    {
                        bladehandle = child;
                        break;
                    }
                }

                if (bladehandle != null)
                {
                    Transform blade2 = Object.Instantiate(bladehandle, meleeWeaponTr);
                    blade2.name = "SecondBladeHandle";
                    blade2.localPosition = -bladehandle.localPosition;
                    blade2.localRotation = bladehandle.localRotation * Quaternion.Euler(0, 0, 180);
                }
            }

            // Register for Cheats
            RegisterEnemyForCheats(newEnemyObj);
        }

        public static void CreateMissileWhisp(GameObject original, GameObject rocketProjectilePrefab, GameObject shieldSourceEnemy = null)
        {
            if (MissileWhispPrefab != null && ShieldedMissileWhispPrefab != null) return;

            GameObject newEnemyObj = Object.Instantiate(original);
            newEnemyObj.name = "MissileWhisp";
            Object.DontDestroyOnLoad(newEnemyObj);
            newEnemyObj.SetActive(false);
            MissileWhispPrefab = newEnemyObj;

            // Change color
            var renderers = newEnemyObj.GetComponentsInChildren<SpriteRenderer>(true);
            foreach (var renderer in renderers)
            {
                renderer.color = Consts.Values.Colors.MissileWhispColor;
            }

            // Assign Projectile
            var brain = newEnemyObj.GetComponent<WhispBrain>();
            if (brain != null && rocketProjectilePrefab != null)
            {
                // Configure Rocket Logic
                var bp = rocketProjectilePrefab.GetComponent<BasicProjectile>();
                if (bp != null)
                {
                    bp.destroyOnCollision = true;

                    // Find "Explosion" effect if missing
                    if (bp.destroyEffect == null)
                    {
                        var explosionPrefab = Resources.FindObjectsOfTypeAll<Explosion>().FirstOrDefault(e => e.name == "Explosion")?.gameObject;
                        if (explosionPrefab != null)
                        {
                            bp.destroyEffect = explosionPrefab;
                        }
                    }
                }

                brain.projectile = rocketProjectilePrefab;
                brain.shotForce = Consts.Values.Enemies.MissileWhispShotForce;
            }
            else
            {
                Logger.LogWarning($"[SpiderSurge] Failed to setup MissileWhisp weapon. Missing Brain or Projectile.");
            }

            // Handle Offscreen Indicator
            var offScreen = newEnemyObj.GetComponentInChildren<OffScreenIndicator>(true);
            var type = typeof(OffScreenIndicator);

            if (offScreen == null)
            {
                GameObject indicatorPrefab = null;
                // Find indicator prefab from sources
                var rollers = Resources.FindObjectsOfTypeAll<ExplodingRoller>();
                if (rollers != null && rollers.Length > 0)
                {
                    // Try to get from first available roller
                    var rollerIndicator = rollers.Select(r => r.GetComponent<OffScreenIndicator>()).FirstOrDefault(i => i != null);

                    if (rollerIndicator != null)
                    {
                        // Access 'indicator' field via reflection since it is private
                        var field = type.GetField("indicator", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                        if (field != null) indicatorPrefab = (GameObject)field.GetValue(rollerIndicator);
                    }
                }

                if (indicatorPrefab != null)
                {
                    offScreen = newEnemyObj.AddComponent<OffScreenIndicator>();

                    // Set fields via reflection
                    type.GetField("indicator", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(offScreen, indicatorPrefab);

                    // Offset
                    type.GetField("offset", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(offScreen, Vector2.zero);
                }
                else
                {
                    Logger.LogWarning("[SpiderSurge] Could not find 'indicator' prefab for OffScreenIndicator.");
                }
            }

            if (offScreen != null)
            {
                type.GetField("color", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(offScreen, Consts.Values.Colors.MissileWhispColor);
                type.GetField("_mainCamera", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(offScreen, Camera.main); // Initialize camera

                // Tracked target is the sprite renderer
                var sr = newEnemyObj.GetComponentInChildren<SpriteRenderer>();
                type.GetField("trackedTarget", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(offScreen, sr);
            }

            // Register for Cheats
            RegisterEnemyForCheats(newEnemyObj);

            // Create Shielded Variant if source is provided
            if (shieldSourceEnemy != null && ShieldedMissileWhispPrefab == null)
            {
                CreateShieldedVariant(newEnemyObj, shieldSourceEnemy);
            }
        }

        private static void CreateShieldedVariant(GameObject baseEnemy, GameObject shieldSourceEnemy)
        {
            if (baseEnemy == null || shieldSourceEnemy == null) return;

            GameObject newEnemyObj = Object.Instantiate(baseEnemy);
            newEnemyObj.name = "ShieldedMissileWhisp";
            Object.DontDestroyOnLoad(newEnemyObj);
            newEnemyObj.SetActive(false);
            ShieldedMissileWhispPrefab = newEnemyObj;

            var healthSystem = newEnemyObj.GetComponent<EnemyHealthSystem>();
            var sourceHealth = shieldSourceEnemy.GetComponent<EnemyHealthSystem>();

            if (sourceHealth != null && sourceHealth.shield != null)
            {
                // Clone Shield
                GameObject newShield = Object.Instantiate(sourceHealth.shield, newEnemyObj.transform);
                newShield.name = sourceHealth.shield.name;
                newShield.transform.localPosition = sourceHealth.shield.transform.localPosition;
                newShield.transform.localRotation = sourceHealth.shield.transform.localRotation;
                newShield.transform.localScale = sourceHealth.shield.transform.localScale;

                healthSystem.shield = newShield;
                newShield.SetActive(true);

                // Clone Shatter Effect if available
                if (sourceHealth.shieldShatterEffect != null)
                {
                    GameObject newShatter = Object.Instantiate(sourceHealth.shieldShatterEffect, newEnemyObj.transform);
                    newShatter.name = sourceHealth.shieldShatterEffect.name;
                    newShatter.transform.localPosition = sourceHealth.shieldShatterEffect.transform.localPosition;
                    newShatter.transform.localRotation = sourceHealth.shieldShatterEffect.transform.localRotation;
                    newShatter.transform.localScale = sourceHealth.shieldShatterEffect.transform.localScale;

                    healthSystem.shieldShatterEffect = newShatter;
                    newShatter.SetActive(false);
                }
            }

            RegisterEnemyForCheats(newEnemyObj);
        }



        private static void RegisterEnemyForCheats(GameObject enemyObj)
        {
            var healthSystem = enemyObj.GetComponent<EnemyHealthSystem>();
            if (healthSystem == null)
            {
                Logger.LogError($"[SpiderSurge] Cannot register {enemyObj.name}: No EnemyHealthSystem found.");
                return;
            }

            bool registered = false;

            if (CheatManager.Instance != null)
            {
                CheatManager.Instance.RegisterCheatEnemy(healthSystem);
                registered = true;
            }
            else if (CustomTiersScreen.instance != null && CustomTiersScreen.instance.allElements != null)
            {
                // Fallback check to avoid duplicates manually if CheatManager is missing
                bool alreadyExists = false;
                foreach (var enemy in CustomTiersScreen.instance.allElements.allEnemies)
                {
                    if (enemy.name == enemyObj.name)
                    {
                        alreadyExists = true;
                        break;
                    }
                }

                if (!alreadyExists)
                {
                    CustomTiersScreen.instance.allElements.allEnemies.Add(healthSystem);
                }
                registered = true;
            }

            if (!registered)
            {
                Logger.LogWarning($"[SpiderSurge] Could not register {enemyObj.name} for cheats: CheatManager and CustomTiersScreen unavailable.");
            }
        }

        public static void InitializeFromLists(ElementLists elements)
        {
            Logger.LogInfo("[SpiderSurge] Initializing Custom Enemies from lists...");

            GameObject whispPrefab = null;

            GameObject meleeWhispPrefab = null;
            GameObject powerMeleeWhispPrefab = null;
            GameObject rocketProjectile = null;

            GameObject shieldSource = null;

            if (elements.allEnemies != null)
            {
                foreach (var enemy in elements.allEnemies)
                {
                    if (enemy == null) continue;
                    if (enemy.name.Contains("MeleeWhisp") && !enemy.name.Contains("Power")) meleeWhispPrefab = enemy.gameObject;
                    else if (enemy.name.Contains("MeleeWhisp") && enemy.name.Contains("Power")) powerMeleeWhispPrefab = enemy.gameObject;
                    else if (enemy.name.Contains("Whisp") && !enemy.name.Contains("Melee") && !enemy.name.Contains("Power") && !enemy.name.Contains("Missile")) whispPrefab = enemy.gameObject;

                    // Look for a shield source
                    if (enemy.shield != null && shieldSource == null)
                    {
                        shieldSource = enemy.gameObject;
                    }
                }
            }

            // Attempt to find the Rocket projectile directly
            var basicProjectiles = Resources.FindObjectsOfTypeAll<BasicProjectile>();
            foreach (var bp in basicProjectiles)
            {
                if (bp.name == "Rocket")
                {
                    rocketProjectile = bp.gameObject;
                    break;
                }
            }

            if (rocketProjectile == null)
            {
                Logger.LogWarning("[SpiderSurge] 'Rocket' projectile not found explicitly.");
            }

            // Invoke Missile Whisp creation (and shielded variant if possible)
            if (whispPrefab != null && rocketProjectile != null)
            {
                CreateMissileWhisp(whispPrefab, rocketProjectile, shieldSource);
            }
            else
            {
                Logger.LogWarning($"[SpiderSurge] Missing prefabs for MissileWhisp. Whisp: {whispPrefab != null}, RocketProjectile: {rocketProjectile != null}");
            }

            if (meleeWhispPrefab != null) CreateTwinBladeMeleeWhisp(meleeWhispPrefab);
            if (powerMeleeWhispPrefab != null) CreateTwinBladePowerMeleeWhisp(powerMeleeWhispPrefab);
        }
    }
}
