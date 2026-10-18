using System.Collections.Generic;
using System.Linq;
using SpiderSurge.Common;
using Unity.Netcode;
using UnityEngine;
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

        // The ability stays active for the freeze time, so the indicator shows it and the cooldown starts when enemies thaw
        protected override void OnActivate()
        {
            if (!IsRemote)
            {
                SoundManager.Instance?.PlaySound(
                    Consts.SoundNames.FreezeAbility,
                    Consts.SoundVolumes.FreezeAbility * Consts.SoundVolumes.MasterVolume
                );
            }

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
            foreach (EnemyHealthSystem enemy in EnemiesInRange(origin, range))
            {
                FrozenEnemy.Freeze(enemy, duration);
            }
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
            GameObject ring = new GameObject("FreezeRing");
            ring.transform.position = origin;
            ring.AddComponent<PulseRingEffect>().Setup(range, Consts.Values.Freeze.RingColor);
        }

        private static void SpawnRewindRing(Transform body, float range)
        {
            GameObject ring = new GameObject("RewindRing");
            ring.transform.position = body.position;
            ring.AddComponent<PulseRingEffect>().SetupInward(range, Consts.Values.Freeze.RewindRingColor, Consts.Values.Freeze.RewindRingTime, body);
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

        private static void RewindEnemy(EnemyHealthSystem enemy, GameObject wasp, GameObject shieldedWasp)
        {
            bool shielded = enemy.shield != null && enemy.shield.activeInHierarchy;
            GameObject prefab = shielded && shieldedWasp != null ? shieldedWasp : wasp;
            Vector3 position = enemy.rb2D != null ? (Vector3)enemy.rb2D.position : enemy.transform.position;
            float frozenFor = FrozenEnemy.TimeLeftOn(enemy);

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
                if (frozenFor > 0f) FrozenEnemy.Freeze(health, frozenFor);
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
