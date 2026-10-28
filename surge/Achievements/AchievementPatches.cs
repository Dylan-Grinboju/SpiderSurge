using System;
using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using SpiderSurge.Common;
using SpiderTeams;
using Unity.Netcode;
using UnityEngine;
using Logger = Silk.Logger;

namespace SpiderSurge
{
    internal static class AchievementTracker
    {
        public const string BigGrenadeExplosion = "BigExplosion";
        public const string TwinBladePowerWhisp = "TwinBladePowerMeleeWhisp";
        public const string NothingPerk = "nothing";
        public const float LowBladeDurability = 0.15f;
        public const int ThermalTeammateKills = 3;
        public const float BlastSurvivalMargin = 0.25f;

        private static readonly Dictionary<(int caster, int revived), int> Revives = new Dictionary<(int, int), int>();
        private static readonly HashSet<ParticleBlade> FullyUpgradedBlades = new HashSet<ParticleBlade>();

        // Every player is a teammate in survival; in versus only players of the same team, when Teams runs a team match
        public static bool AreTeammates(PlayerController player, PlayerController other)
        {
            if (player == null || other == null || player == other) return false;
            VersusMode versus = VersusMode.instance;
            if (versus == null || !versus.GameModeActive()) return true;
            return Parts.Teams.Running && SameTeam(player, other);
        }

        private static bool SameTeam(PlayerController player, PlayerController other)
        {
            TeamManager manager = TeamManager.Instance;
            return manager != null && TeamManager.TeamMatchActive &&
                   manager.TryGetTeamOfPlayerId(player.playerID.Value, out int team) &&
                   manager.TryGetTeamOfPlayerId(other.playerID.Value, out int otherTeam) &&
                   team == otherTeam;
        }

        public static bool Gone(SpiderHealthSystem spider) => spider == null || spider.dead || spider.astralDead || GameFields.SpiderDying(spider);

        public static void Run(string what, Action action) => Run(what, static run => run(), action);

        // Patches on hot paths pass their values with a static lambda, so nothing is allocated per call
        public static void Run<T>(string what, Action<T> action, T arg)
        {
            try
            {
                action(arg);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Achievement tracking failed ({what}): {ex.Message}");
            }
        }

        public static void ResetRevives()
        {
            Revives.Clear();
            AbilityAchievementTracker.ClearWave();
        }

        public static int CountRevive(PlayerController caster, PlayerController revived)
        {
            var key = (caster.playerID.Value, revived.playerID.Value);
            Revives.TryGetValue(key, out int count);
            Revives[key] = ++count;
            return count;
        }

        public static void RememberBlade(ParticleBlade blade, bool fullyUpgraded)
        {
            FullyUpgradedBlades.RemoveWhere(destroyed => destroyed == null);
            if (fullyUpgraded) FullyUpgradedBlades.Add(blade);
            else FullyUpgradedBlades.Remove(blade);
        }

        public static bool IsFullyUpgraded(Weapon weapon) => weapon is ParticleBlade blade && FullyUpgradedBlades.Contains(blade);

        public static PlayerController HolderOf(Weapon weapon) => weapon != null && weapon.equipped && weapon.owner != null ? Players.Of(weapon.owner) : null;
    }

    // Black Hole and I Will Keep You Safe, called once the Black Hole has swallowed its target
    public static class SwallowedAchievements
    {
        public static void Enemy(EnemyHealthSystem enemy, PlayerController caster)
        {
            AchievementTracker.Run("Black Hole", () =>
            {
                if (enemy.GetComponent<HornetShamanBrain>() != null) SurgeAchievements.Unlock(SurgeAchievements.BlackHole, caster);
            });
        }

        public static void Spider(SpiderHealthSystem spider, PlayerController caster)
        {
            AchievementTracker.Run("Black Hole", () =>
            {
                if (AchievementTracker.AreTeammates(caster, Players.Of(spider))) SurgeAchievements.Unlock(SurgeAchievements.KeepYouSafe, caster);
            });
        }
    }

    // Total Destruction counts the teammates one Thermal Detonation kills
    [HarmonyPatch(typeof(PulseAbility), "ApplyPulsePhysics")]
    public static class PulseAbility_Achievement_Patch
    {
        [HarmonyPrefix]
        public static void Prefix(PulseAbility __instance, bool deadly, out List<SpiderHealthSystem> __state)
        {
            __state = null;
            PlayerController caster = PulseAbility.CasterOf(__instance);
            if (!deadly || caster == null) return;

            var teammates = new List<SpiderHealthSystem>();
            AchievementTracker.Run("Thermal Detonation", () =>
            {
                foreach (PlayerController player in Players.All)
                {
                    SpiderHealthSystem spider = player != null ? player.spiderHealthSystem : null;
                    if (AchievementTracker.AreTeammates(caster, player) && !AchievementTracker.Gone(spider)) teammates.Add(spider);
                }
            });
            if (teammates.Count >= AchievementTracker.ThermalTeammateKills) __state = teammates;
        }

        [HarmonyFinalizer]
        public static void Finalizer(PulseAbility __instance, List<SpiderHealthSystem> __state)
        {
            if (__state == null) return;

            AchievementTracker.Run("Thermal Detonation", () =>
            {
                int killed = __state.FindAll(AchievementTracker.Gone).Count;
                if (killed >= AchievementTracker.ThermalTeammateKills)
                {
                    SurgeAchievements.Unlock(SurgeAchievements.TotalDestruction, PulseAbility.CasterOf(__instance));
                }
            });
        }
    }

    // An explosion only calls Damage on a spider inside its death radius, so this is a Big Grenade blast that would have killed
    public static class NullFieldBlastAchievement
    {
        private static readonly AccessTools.FieldRef<Explosion, float> BlastEnd = AccessTools.FieldRefAccess<Explosion, float>("_stopAt");
        private static readonly HashSet<(int spider, int blast)> Watched = new HashSet<(int, int)>();

        internal static void OnDamage(SpiderHealthSystem spider, Vector3 force, Vector3 point, bool stun)
        {
            if (!SurgeAttacks.IsBigGrenadeBlast(out Explosion explosion)) return;

            AchievementTracker.Run("Null Field", static hit => Watch(hit.spider, hit.explosion), (spider, explosion));
        }

        private static void Watch(SpiderHealthSystem spider, Explosion explosion)
        {
            ImmuneAbility nullField = ImmuneAbility.GetByHealthSystem(spider);
            if (nullField == null || !nullField.IsImmune || !nullField.isActiveAndEnabled || AchievementTracker.Gone(spider)) return;

            var key = (spider.GetInstanceID(), explosion.GetInstanceID());
            if (Watched.Add(key))
            {
                nullField.StartCoroutine(UnlockIfSurvived(spider, key, BlastEnd(explosion)));
            }
        }

        // The blast keeps hitting every tick and Null Field can run out first, so the spider has to outlive the blast
        private static IEnumerator UnlockIfSurvived(SpiderHealthSystem spider, (int, int) key, float blastEnd)
        {
            while (Time.time < blastEnd + AchievementTracker.BlastSurvivalMargin)
            {
                yield return null;
            }
            Watched.Remove(key);

            AchievementTracker.Run("Null Field", () =>
            {
                if (!AchievementTracker.Gone(spider))
                {
                    SurgeAchievements.Unlock(SurgeAchievements.UnstoppableForce, Players.Of(spider));
                }
            });
        }
    }

    // Healer and You Are Not Done Yet; the ghosts are read before the revive, including one Teams makes in versus
    public static class ReviveAchievements
    {
        public static List<PlayerController> Ghosts()
        {
            var ghosts = new List<PlayerController>();
            AchievementTracker.Run("Neural Backup", () =>
            {
                foreach (PlayerController player in Players.All)
                {
                    if (ImmuneAbility.IsGhost(player)) ghosts.Add(player);
                }
            });
            return ghosts;
        }

        public static void Revived(ImmuneAbility ability, PlayerController revived, List<PlayerController> ghosts)
        {
            AchievementTracker.Run("Neural Backup", () =>
            {
                PlayerController caster = ability.GetComponentInParent<PlayerController>() ?? Players.ById(ability.PlayerId);
                if (caster == null) return;
                if (ghosts.Contains(revived))
                {
                    SurgeAchievements.Unlock(SurgeAchievements.NotDoneYet, caster);
                }
                if (revived != caster && AchievementTracker.CountRevive(caster, revived) >= 2)
                {
                    SurgeAchievements.Unlock(SurgeAchievements.Healer, caster);
                }
            });
        }
    }

    // Doomsday Scenario: Pocket Dimension takes a Big Grenade whose fuse is already lit
    public static class DoomsdayAchievement
    {
        private static readonly AccessTools.FieldRef<Grenade, bool> Armed = AccessTools.FieldRefAccess<Grenade, bool>("_armed");

        public static void Stored(StorageAbility ability, GameObject heldWeaponObj)
        {
            if (heldWeaponObj == null) return;
            AchievementTracker.Run("Pocket Dimension", () =>
            {
                Grenade grenade = heldWeaponObj.GetComponent<Grenade>();
                if (grenade != null && grenade.serializationWeaponName == SerializationWeaponName.BigGrenade && Armed(grenade))
                {
                    SurgeAchievements.Unlock(SurgeAchievements.Doomsday, Players.ById(ability.PlayerId));
                }
            });
        }
    }

    // A blade takes its Size Matters and Thick Blades levels when it spawns, so they are read there
    [HarmonyPatch(typeof(ParticleBlade), nameof(ParticleBlade.OnNetworkSpawn))]
    public static class ParticleBlade_OnNetworkSpawn_Achievement_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(ParticleBlade __instance)
        {
            AchievementTracker.Run("Particle Blade", () =>
            {
                ModifierManager modifiers = ModifierManager.instance;
                if (modifiers == null || !Net.IsAuthority) return;
                AchievementTracker.RememberBlade(__instance, modifiers.GetModLevel("sizematters") >= 2 && modifiers.GetModLevel("thickblades") >= 2);
            });
        }
    }

    // Unlimited Power: Bottomless Clip cast while holding a fully upgraded blade
    public static class UnlimitedPowerAchievement
    {
        public static void Cast(AmmoAbility ability)
        {
            AchievementTracker.Run("Bottomless Clip", () =>
            {
                PlayerController caster = Players.ById(ability.PlayerId);
                SpiderHealthSystem spider = caster != null ? caster.spiderHealthSystem : null;
                SpiderWeaponManager weapons = spider != null ? spider.GetComponentInChildren<SpiderWeaponManager>() : null;
                if (weapons != null && AchievementTracker.IsFullyUpgraded(weapons.equippedWeapon))
                {
                    SurgeAchievements.Unlock(SurgeAchievements.UnlimitedPower, caster);
                }
            });
        }
    }

    // Kiss of Death: a player's blade field touches the whisp's body while the blade is nearly spent
    [HarmonyPatch(typeof(ForceField), "Damage")]
    public static class ForceField_Damage_Achievement_Patch
    {
        [HarmonyPrefix]
        public static void Prefix(ForceField __instance, Collision2D other)
        {
            if (other == null || other.gameObject == null) return;
            AchievementTracker.Run("Particle Blade hit", static hit => CheckHit(hit.field, hit.other), (field: __instance, other));
        }

        private static void CheckHit(ForceField field, Collision2D other)
        {
            EnemyHealthSystem enemy = other.gameObject.GetComponent<EnemyHealthSystem>();
            if (enemy == null || enemy.IsDead() || !enemy.name.StartsWith(AchievementTracker.TwinBladePowerWhisp, StringComparison.Ordinal)) return;

            ParticleBlade blade = field.GetComponentInParent<ParticleBlade>();
            if (blade == null || blade.maxAmmo <= 0f || blade.ammo <= 0f || blade.ammo >= blade.maxAmmo * AchievementTracker.LowBladeDurability) return;

            SurgeAchievements.Unlock(SurgeAchievements.KissOfDeath, AchievementTracker.HolderOf(blade));
        }
    }

    // Not On My Watch: a laser stops on a spider inside Null Field, and the next thing on its line is a teammate it would have hit
    internal static class LaserBlock
    {
        private static readonly RaycastHit2D[] Behind = new RaycastHit2D[16];

        public static bool Watching => Net.IsAuthority && ImmuneAbility.AnyImmune && !SurgeAchievements.IsUnlocked(SurgeAchievements.NotOnMyWatch);

        public static SpiderHealthSystem SpiderOf(Collider2D collider)
        {
            if (collider == null) return null;
            SpiderHealthSystem spider = collider.GetComponent<SpiderHealthSystem>();
            if (spider == null && collider.attachedRigidbody != null) spider = collider.attachedRigidbody.GetComponent<SpiderHealthSystem>();
            return spider;
        }

        // The beam ends harmlessly on its own weapon or the shooter's body, so those never count as the teammate
        public static void Check(RaycastHit2D hit, Vector2 origin, Vector2 direction, float radius, float distance, LayerMask layers, GameObject weapon, GameObject shooter)
        {
            SpiderHealthSystem protector = SpiderOf(hit.collider);
            if (protector == null || AchievementTracker.Gone(protector)) return;
            ImmuneAbility nullField = ImmuneAbility.GetByHealthSystem(protector);
            if (nullField == null || !nullField.IsImmune || !nullField.isActiveAndEnabled) return;

            PlayerController protectorPlayer = Players.Of(protector);
            Transform protectorRoot = protector.rootObject != null ? protector.rootObject.transform : protector.transform;
            int count = Physics2D.CircleCastNonAlloc(origin, radius, direction, Behind, distance, layers);
            RaycastHit2D next = default;
            float nearest = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                RaycastHit2D candidate = Behind[i];
                if (candidate.collider == null || candidate.distance < hit.distance || candidate.distance >= nearest) continue;
                if (candidate.collider.transform.IsChildOf(protectorRoot)) continue;
                Weapon held = candidate.collider.GetComponentInParent<Weapon>();
                if (held != null && held.gameObject != weapon && AchievementTracker.HolderOf(held) == protectorPlayer) continue;
                next = candidate;
                nearest = candidate.distance;
            }
            if (next.collider == null || next.collider.gameObject == weapon || next.collider.gameObject == shooter) return;

            SpiderHealthSystem teammate = SpiderOf(next.collider);
            if (teammate == null || teammate == protector || AchievementTracker.Gone(teammate) || GameFields.SpiderImmuneTill(teammate) > Time.time) return;
            ImmuneAbility teammateField = ImmuneAbility.GetByHealthSystem(teammate);
            if (teammateField != null && teammateField.IsImmune) return;

            if (AchievementTracker.AreTeammates(protectorPlayer, Players.Of(teammate)))
            {
                SurgeAchievements.Unlock(SurgeAchievements.NotOnMyWatch, protectorPlayer);
            }
        }
    }

    // Repeats the cannon's casts from the same frame: each segment stops at its first hit and reflects from there
    [HarmonyPatch(typeof(LaserCannon), "Beam")]
    public static class LaserCannon_Beam_Achievement_Patch
    {
        private const float Range = 2000f;
        private static readonly AccessTools.FieldRef<LaserCannon, Vector2> StartPoint = AccessTools.FieldRefAccess<LaserCannon, Vector2>("startPoint");

        [HarmonyPostfix]
        public static void Postfix(LaserCannon __instance)
        {
            if (!LaserBlock.Watching) return;
            AchievementTracker.Run("Laser block", static cannon => Trace(cannon), __instance);
        }

        private static void Trace(LaserCannon cannon)
        {
            GameObject shooter = cannon.equipped && cannon.owner != null && cannon.owner.healthSystem != null ? cannon.owner.healthSystem.gameObject : null;
            Vector2 origin = StartPoint(cannon);
            Vector2 direction = cannon.transform.up;
            for (int i = 0; i < cannon.countRays; i++)
            {
                RaycastHit2D hit = Physics2D.CircleCast(origin, cannon.halfBeamWidth, direction, Range, cannon.laserColliderLayers);
                if (!hit) return;
                GameObject hitObject = hit.collider.gameObject;
                if (hitObject == shooter || hitObject == cannon.gameObject) return;
                LaserBlock.Check(hit, origin, direction, cannon.halfBeamWidth, Range, cannon.laserColliderLayers, cannon.gameObject, shooter);
                origin = hit.normal * 2f + hit.point;
                direction = Vector2.Reflect(direction, hit.normal);
            }
        }
    }

    [HarmonyPatch(typeof(LaserCube), nameof(LaserCube.Beam))]
    public static class LaserCube_Beam_Achievement_Patch
    {
        private const float Range = 500f;

        [HarmonyPostfix]
        public static void Postfix(LaserCube __instance, Vector3 direction, LineRenderer lineRenderer)
        {
            if (!LaserBlock.Watching || lineRenderer == null) return;
            AchievementTracker.Run("Laser block", static beam => Trace(beam.cube, beam.direction, beam.lineRenderer), (cube: __instance, direction, lineRenderer));
        }

        private static void Trace(LaserCube cube, Vector3 direction, LineRenderer lineRenderer)
        {
            float radius = cube.beamWidth / 2f;
            Vector2 origin = lineRenderer.transform.position + direction * radius;
            RaycastHit2D hit = Physics2D.CircleCast(origin, radius, direction, Range, cube.laserColliderLayers);
            if (!hit || hit.collider.gameObject == cube.gameObject) return;
            LaserBlock.Check(hit, origin, direction, radius, Range, cube.laserColliderLayers, cube.gameObject, null);
        }
    }

    // Yay I Guess?: Luck made the Nothing perk a Lucky Upgrade and a player picked it. The perk timer picks a card itself when it
    // runs out, and that pick is nobody's; the submit comes 0.25 s before the choice lands
    internal static class LuckAchievement
    {
        private static bool _timedOut;

        internal static void OnPerkSubmitted() => _timedOut = Logging.SpiderSurgeStatsManager.PerkChoiceTimerRanOut();

        internal static void OnPerkChoosing(Modifier modifier, GameLevel level)
        {
            if (SurgeGameModeManager.IsSurgeRunActive && !_timedOut &&
                modifier != null && modifier.data != null && modifier.data.key == AchievementTracker.NothingPerk &&
                modifier.levelInSurvival == 0 && ModifierManager_GetNonMaxedSurvivalMods_Patch.LuckyPerkKeys.Contains(AchievementTracker.NothingPerk))
            {
                SurgeAchievements.Unlock(SurgeAchievements.YayIGuess, null);
            }
        }
    }
}
