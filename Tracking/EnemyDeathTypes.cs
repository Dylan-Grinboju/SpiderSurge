using UnityEngine;
using Silk;
using Logger = Silk.Logger;
using HarmonyLib;
using System;
using Interfaces;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;


namespace StatsMod
{
    public static class EnemyDeathHelper
    {
        private static HashSet<int> recentlyKilledEnemies = new HashSet<int>();
        private static float lastCleanupTime = 0f;
        private static float cleanupInterval = 10f; // Clean up 10 seconds after nobody acquires the lock
        private static readonly object recentlyKilledLock = new object();

        private static Dictionary<int, int> rollerBrainHealthTracker = new Dictionary<int, int>();
        private static readonly object rollerBrainLock = new object();

        // Dictionary to track disc projectiles and their owners
        private static Dictionary<int, PlayerInput> discOwnerTracker = new Dictionary<int, PlayerInput>();
        private static readonly object discOwnerLock = new object();

        // Cached reflection fields to avoid expensive lookups
        private static System.Reflection.PropertyInfo _isHostProperty;
        public static System.Reflection.PropertyInfo IsHostProperty
        {
            get
            {
                if (_isHostProperty == null)
                    _isHostProperty = AccessTools.Property(typeof(Unity.Netcode.NetworkBehaviour), "IsHost");
                return _isHostProperty;
            }
        }

        public static bool IsFirstTimeEnemyDies(GameObject enemy)
        {
            if (enemy == null) return false;

            // Find the EnemyBrain component in the enemy or its parents
            EnemyBrain enemyBrain = enemy.GetComponent<EnemyBrain>();
            if (enemyBrain == null)
            {
                enemyBrain = enemy.GetComponentInParent<EnemyBrain>();
            }

            if (enemyBrain == null)
            {
                Logger.LogError($"Could not find EnemyHealthSystem for enemy {enemy.name}");
                return false;
            }

            // Use the EnemyBrain's GameObject ID instead of the individual part's ID
            int enemyId = enemyBrain.gameObject.GetInstanceID();
            float currentTime = Time.time;

            lock (recentlyKilledLock)
            {
                // Clean up the set if enough time has passed since last lock acquisition
                if (currentTime - lastCleanupTime > cleanupInterval)
                {
                    recentlyKilledEnemies.Clear();
                }
                lastCleanupTime = currentTime;

                // Check if this enemy was already killed recently
                if (recentlyKilledEnemies.Contains(enemyId))
                {
                    return false; // Already recorded this kill
                }

                recentlyKilledEnemies.Add(enemyId);
                Logger.LogInfo($"Recording kill for enemy {enemyId}, name:{enemy.name} at time {Time.time}");
                return true;
            }
        }

        public static bool WillRollerStrutKillCauseRollerBrainDeath(RollerBrain rollerBrain)
        {
            // Use the rollerBrain's GameObject ID for tracking
            int rollerBrainId = rollerBrain.gameObject.GetInstanceID();

            lock (rollerBrainLock)
            {
                // Check if we've seen this brain before
                if (!rollerBrainHealthTracker.ContainsKey(rollerBrainId))
                {
                    // First time seeing this brain - get its actual health from the game
                    int currentAliveStrutCount = 0;
                    if (rollerBrain.struts != null)
                    {
                        for (int i = 0; i < rollerBrain.struts.transform.childCount; i++)
                        {
                            if (rollerBrain.struts.transform.GetChild(i).gameObject.activeSelf)
                            {
                                currentAliveStrutCount++;
                            }
                        }
                    }
                    // Initialize with current alive strut count
                    rollerBrainHealthTracker[rollerBrainId] = currentAliveStrutCount - 1;
                }
                else
                {
                    // We've seen this strut before - decrement its tracked health
                    rollerBrainHealthTracker[rollerBrainId]--;
                }

                // Check if this will cause the main enemy to die
                bool willCauseMainDeath = rollerBrainHealthTracker[rollerBrainId] < rollerBrain.minStrutCount;
                // Clean up the tracker if the main enemy will die
                if (willCauseMainDeath)
                {
                    rollerBrainHealthTracker.Remove(rollerBrainId);
                }

                return willCauseMainDeath;
            }
        }

        private static readonly string[] namesOfEnemiesThatCanDie = new string[]
        {
            "Wasp(Clone)",
            "Wasp Shielded(Clone)",
            "PowerWasp Variant(Clone)",
            "PowerWasp Variant Shield(Clone)",
            //no need as they are checked separately
            // "Strut1",
            // "Strut2",
            // "Strut3",
            "Whisp(Clone)",
            "PowerWhisp Variant(Clone)",
            "MeleeWhisp(Clone)",
            "PowerMeleeWhisp Variant(Clone)",
            "Head", //butterfly
            "Hornet_Shaman Variant(Clone)", //black hole
            "Shielded Hornet_Shaman Variant(Clone)", //not confirmed
            "Hornet Variant(Clone)", //darth maul
            "Shielded Hornet Variant(Clone)",
        };
        public static bool WillDieToDamage(GameObject enemy)
        {
            if (enemy == null)
            {
                Logger.LogError("Enemy is null, cannot check if it will die to damage.");
                return false;
            }

            EnemyHealthSystem enemyHealthSystem = enemy.GetComponent<EnemyHealthSystem>();
            if (enemyHealthSystem == null)
            {
                enemyHealthSystem = enemy.GetComponentInParent<EnemyHealthSystem>();
            }

            if (enemyHealthSystem != null)
            {
                var immuneTimeField = AccessTools.Field(typeof(EnemyHealthSystem), "_immuneTime");
                if (immuneTimeField != null)
                {
                    float immuneTime = (float)immuneTimeField.GetValue(enemyHealthSystem);
                    if (Time.time < immuneTime)
                    {
                        //currently immune
                        return false;
                    }
                }
                if (enemyHealthSystem.shield != null && enemyHealthSystem.shield.activeInHierarchy)
                {
                    //has shield
                    return false;
                }

            }
            RollerBrain rollerBrain = enemy.GetComponentInParent<RollerBrain>();
            if (rollerBrain != null)
            {
                return WillRollerStrutKillCauseRollerBrainDeath(rollerBrain);
            }
            return namesOfEnemiesThatCanDie.Contains(enemy.name);
        }

        public static void RegisterDiscOwner(GameObject discProjectile, PlayerInput owner)
        {
            if (discProjectile == null || owner == null) return;

            int discId = discProjectile.GetInstanceID();
            lock (discOwnerLock)
            {
                discOwnerTracker[discId] = owner;
                Logger.LogInfo($"Registered disc owner for disc {discId}, owner: {owner.name}");
            }
        }

        public static PlayerInput GetDiscOwner(GameObject discProjectile)
        {
            if (discProjectile == null) return null;

            int discId = discProjectile.GetInstanceID();
            lock (discOwnerLock)
            {
                if (discOwnerTracker.TryGetValue(discId, out PlayerInput owner))
                {
                    return owner;
                }
            }
            return null;
        }

        public static void CleanupDiscOwner(GameObject discProjectile)
        {
            if (discProjectile == null) return;

            int discId = discProjectile.GetInstanceID();
            lock (discOwnerLock)
            {
                if (discOwnerTracker.Remove(discId))
                {
                    Logger.LogInfo($"Cleaned up disc owner for disc {discId}");
                }
            }
        }

        public static PlayerInput FindPlayerInputByPlayerId(ulong playerId)
        {
            // Delegate to the cached version in PlayerTracker
            return PlayerTracker.FindPlayerInputByPlayerId(playerId);
        }

        public static void TryRecordKill(GameObject target, PlayerInput playerInput, string weaponName = "weapon")
        {
            if (target == null || playerInput == null) return;

            if (WillDieToDamage(target) && IsFirstTimeEnemyDies(target))
            {
                StatsManager.Instance.IncrementPlayerKill(playerInput);
            }
        }
    }


    //death function of an enemy, no matter the cause (even lava)
    [HarmonyPatch(typeof(EnemyHealthSystem), "Explode")]
    class EnemyDeathCountPatch
    {
        static void Postfix(EnemyHealthSystem __instance)
        {
            try
            {
                StatsManager.Instance.IncrementEnemyKilled();
            }
            catch (System.Exception ex)
            {
                Logger.LogError($"Error recording enemy kill: {ex.Message}");
            }
        }
    }

    // Shotgun
    [HarmonyPatch(typeof(DamageOnParticle), "OnParticleCollision")]
    class DamageOnParticleDamagePatch
    {
        static void Prefix(DamageOnParticle __instance, GameObject other)
        {
            try
            {
                if (other == __instance.ignoreWeapon.gameObject || other == __instance.ignoreWeapon.owner.healthSystem.gameObject)
                    return;

                IDamageable component = other.GetComponent<IDamageable>();
                if (component != null)
                {
                    PlayerInput playerInput = __instance.ignoreWeapon.owner.healthSystem.GetComponentInParent<PlayerInput>();
                    EnemyDeathHelper.TryRecordKill(other, playerInput, "shotgun");
                }
            }
            catch (System.Exception ex)
            {
                Logger.LogError($"Error logging shotgun damage info: {ex.Message}");
            }
        }
    }

    // RailShot
    [HarmonyPatch(typeof(RailShot), "ProcessHit")]
    class RailShotDamagePatch
    {
        static void Prefix(RailShot __instance, RaycastHit2D hit, Vector2 direction)
        {
            try
            {
                if (!(bool)EnemyDeathHelper.IsHostProperty.GetValue(__instance)) return;

                IDamageable component = hit.transform.GetComponent<IDamageable>();
                if (component != null)
                {
                    PlayerInput ownerPlayer = FindOwnerPlayerFromIgnoreList(__instance.ignore.ToArray());
                    EnemyDeathHelper.TryRecordKill(hit.transform.gameObject, ownerPlayer, "railgun");
                }
            }
            catch (System.Exception ex)
            {
                Logger.LogError($"Error tracking RailShot damage: {ex.Message}");
            }
        }

        private static PlayerInput FindOwnerPlayerFromIgnoreList(GameObject[] ignoreList)
        {
            int playerCount = 0;
            PlayerInput ownerPlayer = null;

            foreach (GameObject ignoreObj in ignoreList)
            {
                if (ignoreObj != null)
                {
                    SpiderHealthSystem spiderHealth = ignoreObj.GetComponent<SpiderHealthSystem>()
                        ?? ignoreObj.GetComponentInParent<SpiderHealthSystem>();

                    if (spiderHealth != null)
                    {
                        playerCount++;
                        if (ownerPlayer == null)
                            ownerPlayer = spiderHealth.GetComponentInParent<PlayerInput>();
                    }
                }
            }

            //not sure if needed, implemented this because this is a list and i was suspicious
            if (playerCount > 1)
                Logger.LogError($"RailShot: Found {playerCount} players in ignore list, expected only 1");
            if (ownerPlayer == null)
                Logger.LogError("RailShot: Could not find owner player in ignore list");

            return ownerPlayer;
        }
    }

    // DeathCube
    [HarmonyPatch(typeof(DeathCube), "Fire")]
    class DeathCubeDamagePatch
    {
        static void Prefix(DeathCube __instance, Vector3 direction, LineRenderer laser)
        {
            try
            {
                if (!(bool)EnemyDeathHelper.IsHostProperty.GetValue(__instance)) return;

                var laserColliderLayersField = AccessTools.Field(typeof(DeathCube), "laserColliderLayers");
                var beamWidthField = AccessTools.Field(typeof(DeathCube), "beamWidth");

                if (laserColliderLayersField == null || beamWidthField == null)
                {
                    Logger.LogWarning("Could not access DeathCube fields for laser damage tracking");
                    return;
                }

                LayerMask laserColliderLayers = (LayerMask)laserColliderLayersField.GetValue(__instance);
                float beamWidth = (float)beamWidthField.GetValue(__instance);

                RaycastHit2D[] hits = Physics2D.CircleCastAll(
                    laser.transform.position + direction * beamWidth,
                    beamWidth * 0.75f,
                    direction,
                    500f,
                    laserColliderLayers
                );

                PlayerInput ownerPlayer = __instance.owner.GetComponentInParent<PlayerInput>();
                foreach (RaycastHit2D hit in hits)
                {
                    IDamageable component = hit.transform.GetComponent<IDamageable>();
                    if (component != null)
                    {
                        EnemyDeathHelper.TryRecordKill(hit.transform.gameObject, ownerPlayer, "DeathCube");
                    }
                }
            }
            catch (System.Exception ex)
            {
                Logger.LogError($"Error tracking DeathCube laser damage: {ex.Message}");
            }
        }
    }

    // DeathRay
    [HarmonyPatch(typeof(DeathRay), "Fire")]
    class DeathRayDamagePatch
    {
        static void Prefix(DeathRay __instance)
        {
            try
            {
                if (!(bool)EnemyDeathHelper.IsHostProperty.GetValue(__instance)) return;

                var damageLayersField = AccessTools.Field(typeof(DeathRay), "damageLayers");
                var beamWidthField = AccessTools.Field(typeof(DeathRay), "beamWidth");
                var beamLengthField = AccessTools.Field(typeof(DeathRay), "beamLength");
                var pointField = AccessTools.Field(typeof(DeathRay), "point");

                if (damageLayersField == null || beamWidthField == null || beamLengthField == null || pointField == null)
                {
                    Logger.LogWarning("Could not access DeathRay fields for laser damage tracking");
                    return;
                }

                LayerMask damageLayers = (LayerMask)damageLayersField.GetValue(__instance);
                float beamWidth = (float)beamWidthField.GetValue(__instance);
                float beamLength = (float)beamLengthField.GetValue(__instance);
                Transform point = (Transform)pointField.GetValue(__instance);

                RaycastHit2D[] hits = Physics2D.CircleCastAll(
                    point.position + __instance.transform.up * beamWidth / 4f,
                    beamWidth / 4f,
                    __instance.transform.up,
                    beamLength,
                    damageLayers
                );

                PlayerInput ownerPlayer = __instance.owner.GetComponentInParent<PlayerInput>();
                foreach (RaycastHit2D hit in hits)
                {
                    IDamageable component = hit.transform.GetComponent<IDamageable>();
                    if (component != null)
                    {
                        EnemyDeathHelper.TryRecordKill(hit.transform.gameObject, ownerPlayer, "DeathRay");
                    }
                }
            }
            catch (System.Exception ex)
            {
                Logger.LogError($"Error tracking DeathRay laser damage: {ex.Message}");
            }
        }
    }

    // EnergyBall
    [HarmonyPatch(typeof(EnergyBall), "OnCollisionEnter2D")]
    class EnergyBallDamagePatch
    {
        static void Prefix(EnergyBall __instance, Collision2D other)
        {
            try
            {
                if (!(bool)EnemyDeathHelper.IsHostProperty.GetValue(__instance)) return;

                if (other.gameObject == __instance.ignore) return;

                IDamageable component = other.gameObject.GetComponent<IDamageable>();
                if (component == null) return;

                var damageEnemiesField = AccessTools.Field(typeof(EnergyBall), "damageEnemies");
                if (damageEnemiesField == null)
                {
                    Logger.LogWarning("Could not access EnergyBall damageEnemies field for damage tracking");
                    return;
                }

                bool damageEnemies = (bool)damageEnemiesField.GetValue(__instance);

                // Use reflection to access private methods
                var isEnemyMethod = AccessTools.Method(typeof(EnergyBall), "IsEnemy");
                var isDamageableMethod = AccessTools.Method(typeof(EnergyBall), "IsDamageable");

                if (isEnemyMethod == null || isDamageableMethod == null)
                {
                    Logger.LogWarning("Could not access EnergyBall IsEnemy or IsDamageable methods");
                    return;
                }

                bool isEnemy = (bool)isEnemyMethod.Invoke(__instance, new object[] { other.gameObject });
                if (isEnemy && !damageEnemies) return;

                bool isDamageable = (bool)isDamageableMethod.Invoke(__instance, new object[] { other.gameObject });
                if (isDamageable)
                {
                    PlayerInput ownerPlayer = __instance.owner.GetComponentInParent<PlayerInput>();
                    EnemyDeathHelper.TryRecordKill(other.gameObject, ownerPlayer, "EnergyBall");
                }
            }
            catch (System.Exception ex)
            {
                Logger.LogError($"Error tracking EnergyBall damage: {ex.Message}");
            }
        }
    }

    // Particle Blade
    [HarmonyPatch(typeof(ForceField), "Damage")]
    class ForceFieldDamagePatch
    {
        static void Prefix(ForceField __instance, Collision2D other)
        {
            try
            {
                IDamageable component = other.gameObject.GetComponent<IDamageable>();
                if (component == null) return;

                bool willCallDamage = !other.transform.CompareTag("Weapon");
                if (willCallDamage)
                {
                    Weapon parentWeapon = __instance.GetComponentInParent<Weapon>();
                    if (parentWeapon != null && parentWeapon.owner != null)
                    {
                        PlayerInput playerInput = parentWeapon.owner.healthSystem.GetComponentInParent<PlayerInput>();
                        EnemyDeathHelper.TryRecordKill(other.gameObject, playerInput, "sword");
                    }
                }
            }
            catch (System.Exception ex)
            {
                Logger.LogError($"Error tracking ForceField damage: {ex.Message}");
            }
        }
    }

    // KhepriStaff
    [HarmonyPatch(typeof(KhepriStaff), "Zap")]
    class KhepriStaffDamagePatch
    {
        static void Prefix(KhepriStaff __instance, GameObject target)
        {
            try
            {
                if (target == __instance.owner.healthSystem.gameObject) return;

                IDamageable component = target.GetComponent<IDamageable>();
                if (component == null) return;

                bool willCallDamage = (target.layer == LayerMask.NameToLayer("Enemy") ||
                                     target.layer == LayerMask.NameToLayer("EnemyWeapon") ||
                                     target.CompareTag("PlayerRigidbody"));

                if (willCallDamage)
                {
                    PlayerInput ownerPlayer = __instance.owner.healthSystem.GetComponentInParent<PlayerInput>();
                    EnemyDeathHelper.TryRecordKill(target, ownerPlayer, "KhepriStaff");
                }
            }
            catch (System.Exception ex)
            {
                Logger.LogError($"Error tracking KhepriStaff damage: {ex.Message}");
            }
        }
    }

    // Laser Cannon
    [HarmonyPatch(typeof(LaserCannon), "DamageHit")]
    class LaserCannonDamagePatch
    {
        static void Prefix(LaserCannon __instance, RaycastHit2D hit, IDamageable damageable)
        {
            try
            {
                if (damageable != null)
                {
                    PlayerInput ownerPlayer = __instance.owner.GetComponentInParent<PlayerInput>();
                    EnemyDeathHelper.TryRecordKill(hit.collider.gameObject, ownerPlayer, "LaserCannon");
                }
            }
            catch (System.Exception ex)
            {
                Logger.LogError($"Error tracking LaserCannon damage: {ex.Message}");
            }
        }
    }

    // Laser Cube
    [HarmonyPatch(typeof(LaserCube), "Beam")]
    class LaserCubeDamagePatch
    {
        static void Prefix(LaserCube __instance, Vector3 direction, LineRenderer lineRenderer)
        {
            try
            {
                if (!(bool)EnemyDeathHelper.IsHostProperty.GetValue(__instance)) return;

                RaycastHit2D hit = Physics2D.CircleCast(
                    lineRenderer.transform.position + direction * __instance.beamWidth / 2f,
                    __instance.beamWidth / 2f,
                    direction,
                    500f,
                    __instance.laserColliderLayers
                );

                if (hit && hit.collider.gameObject != __instance.gameObject)
                {
                    IDamageable component = hit.collider.gameObject.GetComponent<IDamageable>();
                    if (component != null)
                    {
                        PlayerInput ownerPlayer = __instance.owner.GetComponentInParent<PlayerInput>();
                        EnemyDeathHelper.TryRecordKill(hit.collider.gameObject, ownerPlayer, "LaserCube");
                    }
                }
            }
            catch (System.Exception ex)
            {
                Logger.LogError($"Error tracking LaserCube damage: {ex.Message}");
            }
        }
    }

    // SawDisc
    [HarmonyPatch(typeof(SawDisc), "TryDamage")]
    class SawDiscDamagePatch
    {
        static void Prefix(SawDisc __instance, Collision2D other)
        {
            try
            {
                IDamageable component = other.gameObject.GetComponent<IDamageable>();
                if (component == null) return;

                bool willCallDamage = !other.transform.CompareTag("Weapon");
                if (willCallDamage)
                {
                    // First try to get the owner from disc tracking
                    PlayerInput ownerPlayer = EnemyDeathHelper.GetDiscOwner(__instance.gameObject);

                    // Fallback to getting from parent (may not work for projectiles)
                    if (ownerPlayer == null)
                    {
                        ownerPlayer = __instance.GetComponentInParent<PlayerInput>();
                    }

                    EnemyDeathHelper.TryRecordKill(other.gameObject, ownerPlayer, "SawDisc");
                }
            }
            catch (System.Exception ex)
            {
                Logger.LogError($"Error tracking SawDisc damage: {ex.Message}");
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
                GameObject gameObject = UnityEngine.Object.Instantiate<GameObject>(__instance.discProjectile, __instance.mountedDisc.transform.position, __instance.point.rotation);
                gameObject.GetComponent<NetworkObject>().Spawn(true);
                gameObject.GetComponent<Rigidbody2D>().AddForce(__instance.transform.up * __instance.shotForce, ForceMode2D.Impulse);

                // **THIS IS OUR ADDITION** - Track the disc owner
                PlayerInput ownerPlayer = __instance.owner.healthSystem.GetComponentInParent<PlayerInput>();
                EnemyDeathHelper.RegisterDiscOwner(gameObject, ownerPlayer);

                // Reset phase effect and visual elements
                __instance.discPhaseEffect.ResetEffect();
                __instance.mountedDisc.SetActive(false);
                __instance.targetingLasers.SetActive(false);

                if ((bool)EnemyDeathHelper.IsHostProperty.GetValue(__instance))
                {
                    // Call LaunchDiscClientRpc using reflection
                    var launchDiscClientRpcMethod = AccessTools.Method(typeof(DiscLauncher), "LaunchDiscClientRpc");
                    if (launchDiscClientRpcMethod != null)
                    {
                        launchDiscClientRpcMethod.Invoke(__instance, null);
                    }
                }

                // Call Impact method
                var impactMethod = AccessTools.Method(typeof(DiscLauncher), "Impact");
                if (impactMethod != null)
                {
                    impactMethod.Invoke(__instance, new object[] { __instance.transform.up * -__instance.recoil, __instance.point.position, false, true });
                }

                return false; // Skip the original method
            }
            catch (System.Exception ex)
            {
                Logger.LogError($"Error in DiscLauncher LaunchDisc patch: {ex.Message}");
                return true; // Execute original method on error
            }
        }
    }

    // SawDisc - Clean up disc owner tracking when disc is destroyed
    [HarmonyPatch(typeof(SawDisc), "OnDesintegrateClientRpc")]
    class SawDiscCleanupPatch
    {
        static void Prefix(SawDisc __instance)
        {
            try
            {
                // Clean up the disc owner tracking when the disc is about to be destroyed
                EnemyDeathHelper.CleanupDiscOwner(__instance.gameObject);
            }
            catch (System.Exception ex)
            {
                Logger.LogError($"Error cleaning up SawDisc owner tracking: {ex.Message}");
            }
        }
    }

    // SpiderHealthSystem
    [HarmonyPatch(typeof(SpiderHealthSystem), "AfterlifeExplosion")]
    class SpiderHealthSystemAfterlifeExplosionPatch
    {
        static void Prefix(SpiderHealthSystem __instance)
        {
            try
            {
                var knockBackRadiusField = AccessTools.Field(typeof(SpiderHealthSystem), "knockBackRadius");
                var layersField = AccessTools.Field(typeof(SpiderHealthSystem), "layers");
                var deathRadiusField = AccessTools.Field(typeof(SpiderHealthSystem), "_deathRadius");

                if (knockBackRadiusField == null || layersField == null || deathRadiusField == null)
                {
                    Logger.LogWarning("Could not access SpiderHealthSystem fields for afterlife explosion tracking");
                    return;
                }

                float knockBackRadius = (float)knockBackRadiusField.GetValue(__instance);
                LayerMask layers = (LayerMask)layersField.GetValue(__instance);
                float deathRadius = (float)deathRadiusField.GetValue(__instance);

                Collider2D[] colliders = Physics2D.OverlapCircleAll(__instance.transform.position, knockBackRadius, layers);
                PlayerInput ownerPlayer = __instance.GetComponentInParent<PlayerInput>();

                foreach (Collider2D collider2D in colliders)
                {
                    if (collider2D.gameObject == __instance.gameObject) continue;

                    IDamageable component = collider2D.GetComponent<IDamageable>();
                    if (component == null) continue;

                    Vector3 position = __instance.transform.position;
                    Vector2 vector = collider2D.ClosestPoint(position);
                    float distance = Vector2.Distance(position, vector);

                    if (distance <= deathRadius)
                    {
                        EnemyDeathHelper.TryRecordKill(collider2D.gameObject, ownerPlayer, "afterlife explosion");
                    }
                }
            }
            catch (System.Exception ex)
            {
                Logger.LogError($"Error tracking SpiderHealthSystem afterlife explosion: {ex.Message}");
            }
        }
    }

    // Explosion
    [HarmonyPatch(typeof(Explosion), "KnockBack")]
    class ExplosionDamagePatch
    {
        static void Prefix(Explosion __instance)
        {
            try
            {
                if (!NetworkManager.Singleton.IsHost) return;

                var fields = GetExplosionFields(__instance);
                if (fields == null) return;

                var colliders = Physics2D.OverlapCircleAll(__instance.transform.position, fields.knockBackRadius, fields.layers);

                foreach (Collider2D collider2D in colliders)
                {
                    IDamageable componentInParent = collider2D.GetComponentInParent<IDamageable>();
                    if (componentInParent == null) continue;

                    if (WillCallExplosionDamage(__instance.transform.position, collider2D, fields))
                    {
                        PlayerInput ownerPlayer = EnemyDeathHelper.FindPlayerInputByPlayerId(fields.explosionOwnerId);
                        EnemyDeathHelper.TryRecordKill(collider2D.gameObject, ownerPlayer, "explosion");
                    }
                }
            }
            catch (System.Exception ex)
            {
                Logger.LogError($"Error tracking Explosion damage: {ex.Message}");
            }
        }

        private static ExplosionFields GetExplosionFields(Explosion instance)
        {
            var knockBackRadiusField = AccessTools.Field(typeof(Explosion), "knockBackRadius");
            var layersField = AccessTools.Field(typeof(Explosion), "layers");
            var deathRadiusField = AccessTools.Field(typeof(Explosion), "deathRadius");
            var playerDeathRadiusField = AccessTools.Field(typeof(Explosion), "_playerDeathRadius");
            var isBoomSpearField = AccessTools.Field(typeof(Explosion), "isBoomSpear");
            var playerExplosionIDField = AccessTools.Field(typeof(Explosion), "playerExplosionID");
            var explosionOwnerIdField = AccessTools.Field(typeof(Explosion), "explosionOwnerId");

            if (knockBackRadiusField == null || layersField == null || deathRadiusField == null ||
                playerDeathRadiusField == null || isBoomSpearField == null || playerExplosionIDField == null || explosionOwnerIdField == null)
            {
                Logger.LogWarning("Could not access Explosion fields for damage tracking");
                return null;
            }

            return new ExplosionFields
            {
                knockBackRadius = (float)knockBackRadiusField.GetValue(instance),
                layers = (LayerMask)layersField.GetValue(instance),
                deathRadius = (float)deathRadiusField.GetValue(instance),
                playerDeathRadius = (float)playerDeathRadiusField.GetValue(instance),
                isBoomSpear = (bool)isBoomSpearField.GetValue(instance),
                playerExplosionId = (int)playerExplosionIDField.GetValue(instance),
                explosionOwnerId = (ulong)explosionOwnerIdField.GetValue(instance)
            };
        }

        private static bool WillCallExplosionDamage(Vector3 explosionPosition, Collider2D collider, ExplosionFields fields)
        {
            bool isIceBlock = (collider.GetComponentInParent<IDamageable>() is IceBlock);
            Vector2 closestPoint = collider.ClosestPoint(explosionPosition);
            float distance = Vector2.Distance(explosionPosition, closestPoint);

            if (distance > fields.deathRadius && !isIceBlock) return false;
            if (collider.CompareTag("PlayerRigidbody") && distance > fields.playerDeathRadius) return false;

            if (fields.isBoomSpear && collider.CompareTag("PlayerRigidbody"))
            {
                PlayerController playerController;
                if (collider.transform.parent.parent.TryGetComponent<PlayerController>(out playerController) &&
                    playerController != null &&
                    playerController.playerID.Value == fields.playerExplosionId)
                {
                    return false; // Will call Impact, not Damage
                }
            }

            return true;
        }

        private class ExplosionFields
        {
            public float knockBackRadius;
            public LayerMask layers;
            public float deathRadius;
            public float playerDeathRadius;
            public bool isBoomSpear;
            public int playerExplosionId;
            public ulong explosionOwnerId;
        }
    }


    // ParticleBladeLauncher - Track particle blade projectiles and their owners
    [HarmonyPatch(typeof(ProjectileLauncher), "Shoot")]
    class ProjectileLauncherShootPatch
    {
        static bool Prefix(ProjectileLauncher __instance)
        {
            try
            {
                // Check if this launcher fires ParticleBlade projectiles
                ParticleBlade particleBladeComponent = __instance.projectile.GetComponent<ParticleBlade>();
                if (particleBladeComponent == null)
                {
                    return true; // Not a ParticleBlade launcher, let original method run
                }

                // Replicate the entire Shoot method with our owner tracking addition
                if (Physics2D.Raycast(__instance.point.transform.position, __instance.transform.up, 0.01f, GameController.instance.worldLayers).collider)
                {
                    return false; // Skip original
                }

                if ((bool)EnemyDeathHelper.IsHostProperty.GetValue(__instance))
                {
                    // Call ShotClientRpc using reflection
                    var shotClientRpcMethod = AccessTools.Method(typeof(ProjectileLauncher), "ShotClientRpc");
                    if (shotClientRpcMethod != null)
                    {
                        shotClientRpcMethod.Invoke(__instance, null);
                    }
                }

                GameObject gameObject;
                if (CustomMapEditor.ParkourActive())
                {
                    gameObject = UnityEngine.Object.Instantiate<GameObject>(__instance.projectile, __instance.point.position, __instance.transform.rotation, CustomMapEditor.instance.objParent);
                }
                else
                {
                    gameObject = UnityEngine.Object.Instantiate<GameObject>(__instance.projectile, __instance.point.position, __instance.transform.rotation);
                }

                BasicProjectile basicProjectileComponent = gameObject.GetComponent<BasicProjectile>();
                if (basicProjectileComponent != null)
                {
                    basicProjectileComponent.projectileOwnerId = __instance.GetOwnerClientId();
                }
                else
                {
                    Weapon weaponComponent = gameObject.GetComponent<Weapon>();
                    if (weaponComponent)
                    {
                        weaponComponent.ownerWeaponClientId = __instance.GetOwnerClientId();

                        // **THIS IS MY ADDITION** - Set the owner property for ParticleBlade tracking
                        weaponComponent.owner = __instance.owner;
                    }
                }

                gameObject.GetComponent<NetworkObject>().Spawn(true);
                PickupEffects componentInChildren = gameObject.GetComponentInChildren<PickupEffects>();
                if (componentInChildren != null)
                {
                    componentInChildren.StopFloat();
                }

                Rigidbody2D rigidbody = gameObject.GetComponent<Rigidbody2D>();
                rigidbody.AddForce(__instance.transform.up * __instance.shotForce, ForceMode2D.Impulse);
                rigidbody.AddTorque(__instance.rotationForce, ForceMode2D.Impulse);

                // Call Impact method using reflection
                var impactMethod = AccessTools.Method(typeof(ProjectileLauncher), "Impact");
                if (impactMethod != null)
                {
                    impactMethod.Invoke(__instance, new object[] { -__instance.recoil * __instance.transform.up, __instance.point.position, false, true });
                }

                // Handle collision ignoring
                var launcherColliderField = AccessTools.Field(typeof(ProjectileLauncher), "_launcherCollider");
                if (launcherColliderField != null)
                {
                    Collider2D launcherCollider = (Collider2D)launcherColliderField.GetValue(__instance);
                    Physics2D.IgnoreCollision(launcherCollider, gameObject.GetComponent<Collider2D>(), true);
                }

                if (__instance.equipped)
                {
                    Physics2D.IgnoreCollision(__instance.owner.healthSystem.GetComponent<Collider2D>(), gameObject.GetComponent<Collider2D>(), true);
                }

                float ammo = __instance.ammo;
                __instance.ammo = ammo - 1f;

                // Set hold offset using reflection
                var reloadOffsetField = AccessTools.Field(typeof(ProjectileLauncher), "reloadOffset");
                if (reloadOffsetField != null)
                {
                    __instance.holdOffset = (Vector2)reloadOffsetField.GetValue(__instance);
                }

                return false; // Skip the original method
            }
            catch (System.Exception ex)
            {
                Logger.LogError($"Error in ProjectileLauncher Shoot patch: {ex.Message}");
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
                // After the original Equip method runs, fix the ownerWeaponClientId
                if (wm != null && wm.healthSystem != null)
                {
                    PlayerController playerController = wm.healthSystem.GetComponentInParent<PlayerController>();
                    if (playerController != null)
                    {
                        // Override the ulong.MaxValue that was set by the original method
                        __instance.ownerWeaponClientId = (ulong)playerController.playerID.Value;
                        Logger.LogInfo($"Fixed weapon {__instance.serializationWeaponName} owner to: {playerController.playerID.Value}");
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
