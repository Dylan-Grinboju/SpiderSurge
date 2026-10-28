using System.Collections.Generic;
using System.Linq;
using SpiderSurge.Common;
using Unity.Netcode;
using UnityEngine;
using SpiderSurge.Vfx;
using Logger = Silk.Logger;

namespace SpiderSurge
{
    public class FreezeAbility : BaseAbility
    {
        public override string PerkName => Consts.PerkNames.FreezeAbility;
        public override AbilityKind Kind => AbilityKind.Freeze;

        public override float AbilityBaseDuration => Consts.Values.Freeze.AbilityBaseDuration;
        public override float AbilityBaseCooldown => Consts.Values.Freeze.AbilityBaseCooldown;
        public override float UltimateBaseDuration => 0f;
        public override float UltimateBaseCooldown => Consts.Values.Freeze.UltimateBaseCooldown;
        public override float AbilityDurationPerPerkLevel => Consts.Values.Freeze.AbilityDurationIncreasePerLevel;
        public override float AbilityCooldownPerPerkLevel => Consts.Values.Freeze.AbilityCooldownReductionPerLevel;
        public override float UltimateCooldownPerPerkLevel => Consts.Values.Freeze.UltimateCooldownReductionPerLevel;

        public override bool HasUltimate => true;
        public override string UltimatePerkName => Consts.PerkNames.FreezeAbilityUltimate;
        public override string UltimatePerkDisplayName => "Rewind";
        public override string UltimatePerkDescription => "Turns back time on nearby enemies, turning them into wasps. Shielded enemies keep their shield.";

        private static readonly Dictionary<string, GameObject> EnemyPrefabs = new Dictionary<string, GameObject>();
        private static readonly RaycastHit2D[] WallHit = new RaycastHit2D[1];
        private static readonly ContactFilter2D Walls = new ContactFilter2D
        {
            useLayerMask = true,
            layerMask = LayerMask.GetMask("World", "DynamicWorld", "Honeycomb", "dummyPlatform"),
            useTriggers = false
        };

        private bool _frozeAny;
        private bool _refundDirty;
        private float _nextRefundSync;
        private float _lastRefundSample;

        private void Update()
        {
            if (!IsAuthority) return;
            float elapsed = FreezeSlowMotion.ElapsedSince(_lastRefundSample);
            _lastRefundSample = Time.realtimeSinceStartup;
            bool refunding = onCooldown && IsUnlocked() && IsPlayerAlive() && GameMenus.AcceptPlayerInput
                && FreezeSlowMotion.Active;
            if (refunding)
            {
                ReduceCooldown(elapsed * Consts.Values.Freeze.SlowMotionCooldownRefund);
                _refundDirty = true;
            }
            if (_refundDirty && (!refunding || CooldownRemaining <= 0f || Time.unscaledTime >= _nextRefundSync))
            {
                SurgeNet.SendFreezeCooldown(PlayerId, CooldownRemaining);
                _nextRefundSync = Time.unscaledTime + 0.25f;
                _refundDirty = false;
            }
        }

        public float FreezeRange
        {
            get
            {
                int durationLevel = PerksManager.Instance?.GetPerkLevel(Consts.PerkNames.AbilityDuration) ?? 0;
                int shortTermLevel = PerksManager.Instance?.GetPerkLevel(Consts.PerkNames.ShortTermInvestment) ?? 0;
                int longTermLevel = PerksManager.Instance?.GetPerkLevel(Consts.PerkNames.LongTermInvestment) ?? 0;

                float range = Consts.Values.Freeze.AbilityBaseRange;
                if (durationLevel >= 1) range += Consts.Values.Freeze.AbilityRangeIncreasePerLevel;
                if (shortTermLevel > 0) range += Consts.Values.Freeze.AbilityRangeIncreasePerLevel;
                if (longTermLevel > 0) range -= Consts.Values.Freeze.AbilityRangeIncreasePerLevel;
                return range;
            }
        }

        public float RewindRange
        {
            get
            {
                int durationLevel = PerksManager.Instance?.GetPerkLevel(Consts.PerkNames.AbilityDuration) ?? 0;
                int shortTermLevel = PerksManager.Instance?.GetPerkLevel(Consts.PerkNames.ShortTermInvestment) ?? 0;
                int longTermLevel = PerksManager.Instance?.GetPerkLevel(Consts.PerkNames.LongTermInvestment) ?? 0;

                float range = Consts.Values.Freeze.UltimateBaseRange;
                if (durationLevel == 2) range += Consts.Values.Freeze.UltimateRangeIncreasePerLevel;
                if (shortTermLevel > 0) range -= Consts.Values.Freeze.UltimateRangeIncreasePerLevel;
                if (longTermLevel > 0) range += Consts.Values.Freeze.UltimateRangeIncreasePerLevel;
                return range;
            }
        }

        // The ability stays active for the freeze time, so the indicator shows it and the cooldown starts when targets thaw.
        protected override void OnActivate()
        {
            if (!IsRemote)
            {
                SoundManager.Instance?.PlaySound(
                    Consts.SoundNames.FreezeAbility,
                    Consts.SoundVolumes.FreezeAbility * Consts.SoundVolumes.MasterVolume
                );
            }

            _frozeAny = false;
            if (spiderHealthSystem == null) return;

            Vector2 origin = spiderHealthSystem.transform.position;
            float range = FreezeRange;
            SpawnRing(origin, range);
            if (Net.IsHost)
            {
                SurgeNet.SendFx(SurgeFx.Freeze, PlayerId, origin, range, false);
            }

            if (!IsAuthority) return;

            float duration = AbilityDuration;
            var achievementCast = new FreezeAchievementCast(PlayerId, Time.time + duration);
            foreach (EnemyHealthSystem enemy in EnemiesInRange(origin, range))
            {
                Vector2 position = enemy.rb2D != null ? enemy.rb2D.position : (Vector2)enemy.transform.position;
                if (!HasLineOfSight(origin, position)) continue;
                var laser = enemy.GetComponentInChildren<Enemies.LaserRing>();
                bool firing = !FrozenEnemy.IsHeld(enemy) && laser != null && laser.IsFiring;
                if (FrozenEnemy.Freeze(enemy, duration, PlayerId))
                {
                    _frozeAny = true;
                    AchievementTracker.Run("Freeze Time", () => AbilityAchievementTracker.FreezeEnemy(enemy, achievementCast, firing));
                }
            }
            if (FrozenSpider.FreezeAround(origin, range, duration, PlayerId)) _frozeAny = true;
            FrozenShot.FreezeAround(origin, range, duration);
        }

        public static bool HasLineOfSight(Vector2 origin, Vector2 target) => Physics2D.Linecast(origin, target, Walls, WallHit) == 0;

        // Frozen enemies play the thaw sound as they thaw, so a cast that froze nothing ends like any other ability
        protected override void PlayAbilityEndedSound(bool wasUltimate)
        {
            if (wasUltimate || !_frozeAny) base.PlayAbilityEndedSound(wasUltimate);
        }

        protected override void OnActivateUltimate()
        {
            if (!IsRemote)
            {
                SoundManager.Instance?.PlaySound(
                    Consts.SoundNames.FreezeUlt,
                    Consts.SoundVolumes.FreezeUlt * Consts.SoundVolumes.MasterVolume
                );
            }

            if (spiderHealthSystem != null)
            {
                Transform body = spiderHealthSystem.transform;
                float range = RewindRange;
                SpawnRewindRing(body, range);
                if (Net.IsHost)
                {
                    SurgeNet.SendFx(SurgeFx.Freeze, PlayerId, body.position, range, true);
                }

                if (IsAuthority)
                {
                    RewindEnemies(body.position, range);
                }
            }

            isActive = false;
            isUltimateActive = false;
            StartCooldown(wasUltimate: true);
        }

        // The rings other players' casts draw on a client; the host decides what they hit
        public static void DrawRemote(int playerId, Vector3 position, float radius, bool rewind)
        {
            if (!rewind)
            {
                SpawnRing(position, radius);
                return;
            }
            SpiderHealthSystem health = Players.HealthOf(playerId);
            if (health != null)
            {
                SpawnRewindRing(health.transform, radius);
            }
        }

        private static void SpawnRing(Vector2 origin, float range)
        {
            FreezeVfx.Cast(origin, range);
        }

        private static void SpawnRewindRing(Transform body, float range)
        {
            FreezeVfx.Rewind(body, range);
        }

        private static List<EnemyHealthSystem> EnemiesInRange(Vector2 origin, float range)
        {
            List<EnemyHealthSystem> found = new List<EnemyHealthSystem>();
            if (EnemyHealthSystem.s_activeEnemies == null) return found;

            foreach (EnemyHealthSystem enemy in EnemyHealthSystem.s_activeEnemies)
            {
                if (!FrozenEnemy.CanFreeze(enemy)) continue;

                Vector2 position = enemy.rb2D != null ? enemy.rb2D.position : (Vector2)enemy.transform.position;
                if (Vector2.Distance(origin, position) <= range)
                {
                    found.Add(enemy);
                }
            }
            return found;
        }

        // Every enemy in range that isn't already a wasp is replaced by a wasp in its place, a shielded one if its shield was up, and stays frozen if it was
        private void RewindEnemies(Vector2 origin, float range)
        {
            GameObject wasp = FindEnemyPrefab(Consts.Values.Freeze.WaspName);
            if (wasp == null)
            {
                Logger.LogWarning("FreezeAbility: Wasp prefab not found, Rewind does nothing");
                return;
            }
            GameObject shieldedWasp = FindEnemyPrefab(Consts.Values.Freeze.ShieldedWaspName);

            foreach (EnemyHealthSystem enemy in EnemiesInRange(origin, range))
            {
                if (IsWasp(enemy, wasp, shieldedWasp)) continue;
                RewindEnemy(enemy, wasp, shieldedWasp);
            }
        }

        private static bool IsWasp(EnemyHealthSystem enemy, GameObject wasp, GameObject shieldedWasp)
        {
            string name = enemy.name.Replace("(Clone)", "").Trim();
            return name == wasp.name || (shieldedWasp != null && name == shieldedWasp.name);
        }

        private void RewindEnemy(EnemyHealthSystem enemy, GameObject wasp, GameObject shieldedWasp)
        {
            bool shielded = enemy.shield != null && enemy.shield.activeInHierarchy;
            GameObject prefab = shielded && shieldedWasp != null ? shieldedWasp : wasp;
            Vector3 position = enemy.rb2D != null ? (Vector3)enemy.rb2D.position : enemy.transform.position;
            float frozenFor = FrozenEnemy.TimeLeftOn(enemy);
            int frozenBy = FrozenEnemy.FreezerOf(enemy);
            FreezeVfx.RewindPop(position);

            GameObject spawned = Instantiate(prefab, position, Quaternion.identity);
            spawned.SetActive(true);
            NetworkObject net = spawned.GetComponent<NetworkObject>();
            if (net != null) net.Spawn(true);
            if (EnemySpawner.instance != null && !EnemySpawner.instance.spawnedEnemies.Contains(spawned))
            {
                EnemySpawner.instance.spawnedEnemies.Add(spawned);
            }

            EnemyHealthSystem health = spawned.GetComponent<EnemyHealthSystem>();
            if (health != null)
            {
                if (shielded && prefab == wasp && health.shield != null) health.shield.SetActive(true);
                if (frozenFor > 0f) FrozenEnemy.Freeze(health, frozenFor, frozenBy);
                if (enemy.GetComponent<Enemies.SpawnerBrain>() != null)
                    AchievementTracker.Run("Factory Reset", () => SurgeAchievements.Unlock(SurgeAchievements.FactoryReset, Players.ById(PlayerId)));
            }

            Destroy(enemy.gameObject);
        }

        private static GameObject FindEnemyPrefab(string name)
        {
            if (EnemyPrefabs.TryGetValue(name, out GameObject cached) && cached != null) return cached;

            GameObject found = null;
            foreach (ElementLists lists in Resources.FindObjectsOfTypeAll<ElementLists>())
            {
                EnemyHealthSystem match = lists.allEnemies?.FirstOrDefault(e => e != null && e.name == name);
                if (match != null)
                {
                    found = match.gameObject;
                    break;
                }
            }

            if (found == null)
            {
                found = Resources.FindObjectsOfTypeAll<EnemyHealthSystem>()
                    .Where(e => e != null && e.name == name && !e.gameObject.scene.IsValid())
                    .Select(e => e.gameObject)
                    .FirstOrDefault();
            }

            if (found != null) EnemyPrefabs[name] = found;
            return found;
        }
    }
}
