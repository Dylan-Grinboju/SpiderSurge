using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Logger = Silk.Logger;

namespace SpiderSurge.Enemies
{
    public static class CustomEnemies
    {
        public static GameObject TwinBladeMeleeWhispPrefab;
        public static GameObject TwinBladePowerMeleeWhispPrefab;
        public static GameObject TwinWhispPrefab;
        public static GameObject MissileWhispPrefab;
        public static GameObject ShieldedMissileWhispPrefab;
        public static GameObject ShieldedTwinWhispPrefab;
        public static GameObject WaspCocoonPrefab;
        public static GameObject ShieldedWaspCocoonPrefab;
        public static readonly Dictionary<string, GameObject> LaserPrefabs = new Dictionary<string, GameObject>();

        public static IEnumerable<GameObject> CustomBasePrefabs => new[] { TwinBladeMeleeWhispPrefab, TwinBladePowerMeleeWhispPrefab, TwinWhispPrefab, MissileWhispPrefab };

        public static void CreateTwinWhisp(GameObject original, GameObject shieldSourceEnemy = null)
        {
            if (TwinWhispPrefab == null)
            {
                TwinWhispPrefab = CreateBaseEnemy(original, "TwinWhisp");
                AddTwinCore(TwinWhispPrefab);
            }

            // Create Shielded Variant if source is provided
            if (shieldSourceEnemy != null && ShieldedTwinWhispPrefab == null)
            {
                ShieldedTwinWhispPrefab = CreateShieldedEnemy(TwinWhispPrefab, shieldSourceEnemy, "ShieldedTwinWhisp");
            }
        }

        public static void CreateTwinBladeMeleeWhisp(GameObject original)
        {
            if (TwinBladeMeleeWhispPrefab != null) return;

            TwinBladeMeleeWhispPrefab = CreateBaseEnemy(original, "TwinBladeMeleeWhisp");
            AddTwinBlade(TwinBladeMeleeWhispPrefab);
        }

        public static void CreateTwinBladePowerMeleeWhisp(GameObject original)
        {
            if (TwinBladePowerMeleeWhispPrefab != null) return;

            TwinBladePowerMeleeWhispPrefab = CreateBaseEnemy(original, "TwinBladePowerMeleeWhisp");
            AddTwinBlade(TwinBladePowerMeleeWhispPrefab);
        }

        public static void CreateMissileWhisp(GameObject original, GameObject rocketProjectilePrefab, GameObject shieldSourceEnemy = null)
        {
            if (MissileWhispPrefab != null && ShieldedMissileWhispPrefab != null) return;

            if (MissileWhispPrefab == null)
            {
                var turret = Resources.FindObjectsOfTypeAll<TurretBrain>().FirstOrDefault(t => t.name == "RocketTurret");
                if (turret == null)
                {
                    Logger.LogWarning("[SpiderSurge] RocketTurret appearance is not available yet.");
                    return;
                }

                MissileWhispPrefab = CreateBaseEnemy(original, "MissileWhisp");
                SetupMissileAppearance(MissileWhispPrefab, turret);
                SetupMissileProjectile(MissileWhispPrefab, rocketProjectilePrefab);
                SetupOffScreenIndicator(MissileWhispPrefab, Consts.Values.Colors.MissileWhispColor);
            }

            // Create Shielded Variant if source is provided
            if (shieldSourceEnemy != null && ShieldedMissileWhispPrefab == null && MissileWhispPrefab != null)
            {
                ShieldedMissileWhispPrefab = CreateShieldedEnemy(MissileWhispPrefab, shieldSourceEnemy, "ShieldedMissileWhisp", Consts.Values.Colors.MissileWhispShieldColor);
            }
        }

        // A stationary cocoon from Parkour that hatches a Wasp, or a Shielded Wasp while its own shield is up
        public static void CreateWaspCocoon()
        {
            if (WaspCocoonPrefab != null && ShieldedWaspCocoonPrefab != null) return;

            GameObject cocoon = FindEnemyPrefab("ParkourTarget");
            GameObject wasp = FindEnemyPrefab("Wasp");
            GameObject shieldedWasp = FindEnemyPrefab("Wasp Shielded");
            if (cocoon == null || wasp == null)
            {
                Logger.LogWarning($"[SpiderSurge] Missing prefabs for WaspCocoon. ParkourTarget: {cocoon != null}, Wasp: {wasp != null}");
                return;
            }

            if (WaspCocoonPrefab == null)
            {
                WaspCocoonPrefab = CreateBaseEnemy(cocoon, "WaspCocoon");
                WaspCocoonPrefab.tag = "Untagged";
                WaspCocoonPrefab.AddComponent<SpawnerBrain>().Initialize(wasp, shieldedWasp, Consts.Values.Enemies.CocoonSpawnInterval, Consts.Values.Enemies.CocoonMaxWasps);
            }

            if (shieldedWasp != null && ShieldedWaspCocoonPrefab == null)
            {
                ShieldedWaspCocoonPrefab = CreateShieldedEnemy(WaspCocoonPrefab, shieldedWasp, "ShieldedWaspCocoon");
                ShieldedWaspCocoonPrefab.GetComponent<EnemyHealthSystem>().serializationEnemyName = SerializationEnemyName.ShieldedWasp;
                ShieldedWaspCocoonPrefab.GetComponent<SpawnerBrain>().Initialize(wasp, shieldedWasp, Consts.Values.Enemies.CocoonSpawnInterval, Consts.Values.Enemies.CocoonMaxWasps);
            }
        }

        private static GameObject FindEnemyPrefab(string name)
        {
            return Resources.FindObjectsOfTypeAll<EnemyHealthSystem>()
                .FirstOrDefault(e => e.name == name && !e.gameObject.scene.IsValid())?.gameObject;
        }

        // Each listed enemy gets a copy whose red shield carries a pulsing laser ring; breaking the shield ends the ring
        public static void CreateLaserVariants(IEnumerable<GameObject> baseEnemies, GameObject shieldSourceEnemy)
        {
            if (shieldSourceEnemy == null) return;

            var laserCannon = Resources.FindObjectsOfTypeAll<LaserCannon>().FirstOrDefault(w => w.name == "Laser Cannon");
            var deathRay = Resources.FindObjectsOfTypeAll<DeathRay>().FirstOrDefault(w => w.name == "DeathRay");
            if (laserCannon == null || deathRay == null)
            {
                Logger.LogWarning($"[SpiderSurge] Laser enemies need the Laser Cannon and Death Ray. LaserCannon: {laserCannon != null}, DeathRay: {deathRay != null}");
                return;
            }

            foreach (var baseEnemy in baseEnemies.ToList())
            {
                if (baseEnemy == null || !Consts.Values.LaserRing.Variants.TryGetValue(baseEnemy.name, out string laserName)) continue;
                if (LaserPrefabs.ContainsKey(laserName) || FailedLaserVariants.Contains(laserName)) continue;

                var baseHealth = baseEnemy.GetComponent<EnemyHealthSystem>();
                if (baseHealth == null || (baseHealth.shield != null && baseHealth.shield.activeSelf)) continue;

                var laserEnemy = CreateShieldedEnemy(baseEnemy, shieldSourceEnemy, laserName, Consts.Values.LaserRing.Color);
                var laserHealth = laserEnemy?.GetComponent<EnemyHealthSystem>();
                if (laserHealth == null || laserHealth.shield == null || laserHealth.shieldShatterEffect == null)
                {
                    Logger.LogWarning($"[SpiderSurge] Could not give {laserName} a shield.");
                    // The copy is already made and registered, so it isn't made again on the next run
                    FailedLaserVariants.Add(laserName);
                    continue;
                }

                laserHealth.shield.AddComponent<LaserRing>().Initialize(laserHealth, laserCannon, deathRay);
                LaserPrefabs[laserName] = laserEnemy;
            }
        }

        private static readonly HashSet<string> FailedLaserVariants = new HashSet<string>();

        private static GameObject CreateBaseEnemy(GameObject original, string name, Color? color = null)
        {
            // Instantiating under an inactive holder keeps the copy's OnEnable/OnDisable from running (HornetShamanBrain.OnDisable throws without a network)
            var holder = new GameObject("SpiderSurgeEnemyHolder");
            holder.SetActive(false);
            GameObject newEnemyObj = Object.Instantiate(original, holder.transform);
            newEnemyObj.name = name;
            newEnemyObj.SetActive(false);
            newEnemyObj.transform.SetParent(null, false);
            Object.Destroy(holder);
            Object.DontDestroyOnLoad(newEnemyObj);

            if (color.HasValue)
            {
                var renderers = newEnemyObj.GetComponentsInChildren<SpriteRenderer>(true);
                foreach (var renderer in renderers)
                {
                    renderer.color = color.Value;
                }
            }

            AddToEnemyList(newEnemyObj);
            CustomEnemyNet.Register(newEnemyObj, name);
            return newEnemyObj;
        }

        // Two of the whisp's own cores side by side, one behind each of its two shots, in the whisp's colors
        private static void AddTwinCore(GameObject enemyObj)
        {
            var brain = enemyObj.GetComponent<WhispBrain>();
            Transform core = brain != null && brain.core != null ? brain.core : enemyObj.transform.Find("CoreBase");
            if (core == null)
            {
                Logger.LogWarning("[SpiderSurge] TwinWhisp has no core to double.");
                return;
            }

            float offset = Consts.Values.Enemies.TwinWhispCoreOffset;
            foreach (string part in new[] { "BaseSprite", "Eye" })
            {
                Transform first = core.Find(part);
                if (first == null) continue;
                Transform second = Object.Instantiate(first, core, false);
                second.name = part + " (twin)";
                second.localPosition = first.localPosition + Vector3.right * offset;
                first.localPosition -= Vector3.right * offset;
            }
        }

        private static void AddTwinBlade(GameObject enemyObj)
        {
            var brain = enemyObj.GetComponent<WhispBrain>();
            Transform meleeWeaponTr = null;
            Transform[] allChildren = enemyObj.GetComponentsInChildren<Transform>(true);
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
        }

        private static void SetupMissileProjectile(GameObject enemyObj, GameObject rocketProjectilePrefab)
        {
            var brain = enemyObj.GetComponent<WhispBrain>();
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
        }

        private static void SetupMissileAppearance(GameObject enemyObj, TurretBrain turret)
        {
            foreach (string path in new[] { "Body", "CoreBase/Eye", "CoreBase/BaseSprite" })
            {
                var source = turret.transform.Find(path).GetComponent<SpriteRenderer>();
                var target = enemyObj.transform.Find(path).GetComponent<SpriteRenderer>();
                target.sprite = source.sprite;
                target.color = source.color;
            }

            enemyObj.transform.Find("GlowBase").GetComponent<SpriteRenderer>().enabled = false;
            GameObject thrusters = new GameObject("Thrusters");
            thrusters.transform.SetParent(enemyObj.transform, false);
            foreach (Transform child in turret.transform)
            {
                if (child.name.StartsWith("circle"))
                {
                    Object.Instantiate(child, thrusters.transform, false).name = child.name;
                }
            }

            foreach (var renderer in enemyObj.GetComponentsInChildren<SpriteRenderer>(true))
            {
                Color color = Color.Lerp(renderer.color, Consts.Values.Colors.MissileWhispColor, 0.7f);
                color.a = renderer.color.a;
                renderer.color = color;
            }

            var brain = enemyObj.GetComponent<WhispBrain>();
            typeof(WhispBrain).GetField("glowRender", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(brain, thrusters);
            brain.energyColor = turret.energyColor;
            brain.chargeColor = turret.chargeColor;
            var charge = brain.chargeParticles.main;
            charge.startColor = turret.chargeParticles.main.startColor;
            var launch = brain.launchParticles.main;
            launch.startColor = turret.launchParticles.main.startColor;
        }

        private static void SetupOffScreenIndicator(GameObject enemyObj, Color color)
        {
            var offScreen = enemyObj.GetComponentInChildren<OffScreenIndicator>(true);
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
                    offScreen = enemyObj.AddComponent<OffScreenIndicator>();

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
                type.GetField("color", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(offScreen, color);
                type.GetField("_mainCamera", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(offScreen, Camera.main); // Initialize camera

                // Tracked target is the sprite renderer
                var sr = enemyObj.GetComponentInChildren<SpriteRenderer>(true);
                type.GetField("trackedTarget", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(offScreen, sr);
            }
        }

        private static GameObject CreateShieldedEnemy(GameObject baseEnemy, GameObject shieldSourceEnemy, string name, Color? shieldTint = null)
        {
            if (baseEnemy == null || shieldSourceEnemy == null) return null;

            GameObject newEnemyObj = CreateBaseEnemy(baseEnemy, name);

            var healthSystem = newEnemyObj.GetComponent<EnemyHealthSystem>();
            var sourceHealth = shieldSourceEnemy.GetComponent<EnemyHealthSystem>();

            if (sourceHealth != null && sourceHealth.shield != null)
            {
                GameObject newShield = new GameObject(sourceHealth.shield.name);
                newShield.SetActive(false);
                newShield.transform.SetParent(newEnemyObj.transform, false);

                var shieldRenderer = sourceHealth.shield.GetComponentInChildren<SpriteRenderer>(true);
                Color shieldColor = shieldTint ?? (shieldRenderer != null ? shieldRenderer.color : Color.magenta);
                newShield.AddComponent<ShieldedEnemyVisuals>().Initialize(
                    newEnemyObj.GetComponentsInChildren<SpriteRenderer>(true), shieldColor);

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
                    if (shieldTint.HasValue)
                    {
                        foreach (var particles in newShatter.GetComponentsInChildren<ParticleSystem>(true))
                        {
                            var main = particles.main;
                            main.startColor = shieldTint.Value;
                        }
                    }
                }
            }
            return newEnemyObj;
        }

        private static void AddToEnemyList(GameObject enemyObj)
        {
            var healthSystem = enemyObj.GetComponent<EnemyHealthSystem>();
            if (healthSystem == null)
            {
                Logger.LogError($"[SpiderSurge] Cannot register {enemyObj.name}: No EnemyHealthSystem found.");
                return;
            }

            bool registered = false;

            if (CustomTiersScreen.instance != null && CustomTiersScreen.instance.allElements != null)
            {
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
                Logger.LogWarning($"[SpiderSurge] Could not add {enemyObj.name} to the enemy list: CustomTiersScreen unavailable.");
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

            if (whispPrefab != null)
            {
                CreateTwinWhisp(whispPrefab, shieldSource);
            }

            if (meleeWhispPrefab != null) CreateTwinBladeMeleeWhisp(meleeWhispPrefab);
            if (powerMeleeWhispPrefab != null) CreateTwinBladePowerMeleeWhisp(powerMeleeWhispPrefab);
            CreateWaspCocoon();

            if (elements.allEnemies != null)
            {
                CreateLaserVariants(elements.allEnemies.Where(e => e != null).Select(e => e.gameObject).Concat(CustomBasePrefabs), shieldSource);
            }
        }
    }
}
