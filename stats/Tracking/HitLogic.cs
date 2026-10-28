using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using SpiderSurge.Common;
using Logger = Silk.Logger;


namespace StatsMod
{
    // Hits only record which player last hit a target. Kills, friendly kills and shield breaks are credited when
    // the game actually applies them, so hits that don't kill (immunity, shields, roller struts with health left)
    // can't count early, and deaths the game decides later (struts, fuses, lava) still count.
    public static class HitLogic
    {
        private const float KillCreditWindow = 2.5f;
        private const float DirectPlayerKillCreditWindow = 0.5f;
        internal const string PulseWeapon = "Pulse";
        private const float ShieldCreditWindow = 0.1f;
        private const float StaleHitAge = 30f;
        // A lit fuse ends in an explosion or a death that removes its entry; Freeze Time can hold it far past StaleHitAge
        private const float StaleFuseAge = 300f;
        private const float FootingRegainedTime = 0.5f;
        private const float DeathZoneGrace = 0.5f;
        private const float EnemyDissolveTime = 0.2f;

        private struct HitRecord
        {
            public PlayerController Player;
            public string Weapon;
            public Weapon WeaponObject;
            public float Time;
            public float FootingSince;
            public float? DeathZoneDeadline;
        }

        private struct Owner
        {
            public PlayerController Player;
            public Weapon Weapon;
            public float Time;
        }

        internal sealed class Attack
        {
            internal string Name;
            internal string Weapon;
            internal Func<Component, PlayerController> Owner;
            internal Func<Component, Weapon> WeaponOf;
        }

        // One of Stats' attacks on the core attack stack
        internal struct AttackSlot
        {
            internal Func<Component, PlayerController> FindPlayer;
            internal Func<Component, Weapon> FindWeapon;
            internal bool PlayerFound;
            internal PlayerController Player;
            internal bool WeaponFound;
            internal Weapon WeaponObject;
            internal string Weapon;
            internal PlayerController EnclosingPlayer;
        }

        private static readonly Dictionary<int, HitRecord> lastEnemyHits = new Dictionary<int, HitRecord>();
        private static readonly Dictionary<int, HitRecord> lastPlayerHits = new Dictionary<int, HitRecord>();
        private static readonly Dictionary<int, HitRecord> rollerFuses = new Dictionary<int, HitRecord>();
        private static readonly Dictionary<int, Owner> explosionOwners = new Dictionary<int, Owner>();
        private static readonly Dictionary<PlayerController, PlayerTracker.KillCredit> deathCredits = new Dictionary<PlayerController, PlayerTracker.KillCredit>();
        private static float lastPruneTime = 0f;
        private static float ownersSince = float.NegativeInfinity;
        // Players who left no longer own the weapons they dropped
        private static readonly HashSet<PlayerController> departed = new HashSet<PlayerController>();

        internal static readonly AttackView<AttackSlot> Attacks = new AttackView<AttackSlot>(OpenAttack);

        private static readonly Dictionary<SerializationEnemyName, string> enemyDisplayNames = new Dictionary<SerializationEnemyName, string>
        {
            { SerializationEnemyName.Wasp, "Wasp" },
            { SerializationEnemyName.ShieldedWasp, "Wasp" },
            { SerializationEnemyName.PowerWasp, "Power Wasp" },
            { SerializationEnemyName.PowerWaspShield, "Power Wasp" },
            { SerializationEnemyName.Roller, "Roller" },
            { SerializationEnemyName.PowerRoller, "Roller" },
            { SerializationEnemyName.ExplodingRoller, "Roller" },
            { SerializationEnemyName.Whisp, "Whisp" },
            { SerializationEnemyName.PowerWhisp, "Power Whisp" },
            { SerializationEnemyName.MeleeWhisp, "Melee Whisp" },
            { SerializationEnemyName.PowerMeleeWhisp, "Power Melee Whisp" },
            { SerializationEnemyName.Khepri, "Khepri" },
            { SerializationEnemyName.PowerKhepri, "Power Khepri" },
            { SerializationEnemyName.HornetShaman, "Hornet Shaman" },
            { SerializationEnemyName.HornetVariant, "Hornet" },
            { SerializationEnemyName.ShieldedHornet, "Hornet" },
        };

        // While an attack runs (a shot, an explosion, a blade hit, a pulse), every enemy or spider the game damages
        // or knocks back counts as hit by its player, since a knockback can push the target into lava.
        // Explosions nobody owns, weapons no player holds and enemy saws run as attacks without a player.
        // The player is looked up only once the attack hits something.
        private static bool OpenAttack(ref AttackSlot slot, Component source, object info)
        {
            Attack attack = (Attack)info;
            try
            {
                PlayerController enclosing = CurrentAttacker;
                slot = new AttackSlot { FindPlayer = attack.Owner, FindWeapon = attack.WeaponOf, Weapon = attack.Weapon, EnclosingPlayer = enclosing };
                return true;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error tracking {attack.Name} hit: {ex.Message}");
                return false;
            }
        }

        public static PlayerController CurrentAttacker
        {
            get
            {
                int top = Attacks.Top;
                if (top < 0) return null;
                ref AttackSlot slot = ref Attacks.Slot(top);
                if (!slot.PlayerFound)
                {
                    slot.PlayerFound = true;
                    slot.Player = slot.FindPlayer(Attacks.Source);
                }
                return slot.Player;
            }
        }

        public static PlayerController EnclosingAttacker => Attacks.Active ? Attacks.Slot(Attacks.Top).EnclosingPlayer : null;

        private static string CurrentWeaponName => Attacks.Active ? Attacks.Slot(Attacks.Top).Weapon : null;

        private static Weapon CurrentWeapon
        {
            get
            {
                int top = Attacks.Top;
                if (top < 0) return null;
                ref AttackSlot slot = ref Attacks.Slot(top);
                if (!slot.WeaponFound)
                {
                    slot.WeaponFound = true;
                    slot.WeaponObject = slot.FindWeapon?.Invoke(Attacks.Source);
                }
                return slot.WeaponObject;
            }
        }

        public static bool HasPlayerHits => lastPlayerHits.Count > 0;

        public static void Clear()
        {
            AccuracyTracker.Reset();
            lastEnemyHits.Clear();
            lastPlayerHits.Clear();
            rollerFuses.Clear();
            explosionOwners.Clear();
            deathCredits.Clear();
            lastPruneTime = Time.time;
            ownersSince = Time.time;
        }

        // An attack without a player, or damage from anything else (a friendly wasp, a hazard), takes the kill away
        // from the last player who hit the enemy. Bumps from enemies, pulls and portals outside an attack don't.
        public static void OnEnemyHit(EnemyHealthSystem enemy, bool damage)
        {
            if (!GameSessionManager.IsRecording) return;
            if (enemy.serializationEnemyName == SerializationEnemyName.FriendlyWasp) return;
            AccuracyTracker.EnemyHit(Attacks.Source);

            PlayerController attacker = CurrentAttacker;
            if (attacker != null)
            {
                StoreHit(lastEnemyHits, enemy.GetInstanceID(), attacker, CurrentWeaponName, CurrentWeapon);
            }
            else if (Attacks.Active || damage)
            {
                lastEnemyHits.Remove(enemy.GetInstanceID());
            }
        }

        // When the fuse runs out, the kill goes to whoever broke the roller's last strut, or to nobody when a spider
        // lit it by coming close. Knocking it around during the fuse doesn't take the kill; killing it before does.
        public static void OnRollerFuseLit(EnemyHealthSystem roller, bool lostStruts)
        {
            int id = roller.GetInstanceID();
            if (rollerFuses.ContainsKey(id)) return;

            rollerFuses[id] = lostStruts && lastEnemyHits.TryGetValue(id, out HitRecord hit) && Time.time - hit.Time <= KillCreditWindow
                ? hit
                : new HitRecord { Time = Time.time };
        }

        public static void OnRollerFuseEnded(EnemyHealthSystem roller)
        {
            int id = roller.GetInstanceID();
            rollerFuses.TryGetValue(id, out HitRecord fuse);
            rollerFuses.Remove(id);

            if (lastEnemyHits.TryGetValue(id, out HitRecord last) && last.Player != fuse.Player)
            {
                Logger.LogInfo($"{roller.name}'s fuse ran out, the hit by {PlayerLabel(last.Player)} {Time.time - last.Time:F2}s earlier doesn't count");
            }

            if (fuse.Player != null)
            {
                fuse.Time = Time.time;
                lastEnemyHits[id] = fuse;
            }
            else
            {
                lastEnemyHits.Remove(id);
            }
        }

        // A hit by another player is recorded. Damage from anything else (an enemy, a hazard, an explosion nobody owns,
        // its own attack) replaces that record, and so does a stun from an attack. Bumps outside an attack don't.
        public static void OnPlayerHit(SpiderHealthSystem spider, bool damage, bool stun)
        {
            if (!GameSessionManager.IsRecording) return;
            PlayerController victim = Players.Of(spider.rootObject);
            if (!spider.dead && !spider.astralDead) AccuracyTracker.PlayerHit(Attacks.Source, victim);
            int id = spider.GetInstanceID();
            PlayerController attacker = CurrentAttacker;
            if (attacker == null)
            {
                if (damage || (Attacks.Active && stun)) lastPlayerHits.Remove(id);
                return;
            }

            if (victim == null) return;

            if (victim != attacker)
            {
                StoreHit(lastPlayerHits, id, attacker, CurrentWeaponName, null);
            }
            else if (damage || stun)
            {
                lastPlayerHits.Remove(id);
            }
        }

        // A hit spider that stands in control for a moment wasn't knocked anywhere, so a later fall into lava is its own
        public static void UpdatePlayerFooting(SpiderHealthSystem spider, Stabilizer stabilizer)
        {
            if (!HasPlayerHits) return;

            int id = spider.GetInstanceID();
            if (!lastPlayerHits.TryGetValue(id, out HitRecord hit) || hit.DeathZoneDeadline.HasValue) return;

            bool inControl = stabilizer != null && stabilizer.grounded && !spider.stunned;
            if (!inControl)
            {
                if (hit.FootingSince >= 0f)
                {
                    hit.FootingSince = -1f;
                    lastPlayerHits[id] = hit;
                }
            }
            else if (hit.FootingSince < 0f)
            {
                hit.FootingSince = Time.time;
                lastPlayerHits[id] = hit;
            }
            else if (Time.time - hit.FootingSince >= FootingRegainedTime)
            {
                lastPlayerHits.Remove(id);
            }
        }

        public static void OnPlayerTouchedDeathZone(SpiderHealthSystem spider, float disintegrateDelay)
        {
            MarkInDeathZone(lastPlayerHits, spider.GetInstanceID(), disintegrateDelay);
        }

        public static void OnEnemyDisintegrating(EnemyHealthSystem enemy)
        {
            MarkInDeathZone(lastEnemyHits, enemy.GetInstanceID(), EnemyDissolveTime);
        }

        // A target that reaches lava inside the kill window dies a little later, after it dissolves
        private static void MarkInDeathZone(Dictionary<int, HitRecord> hits, int id, float dyingDelay)
        {
            if (!hits.TryGetValue(id, out HitRecord hit) || hit.DeathZoneDeadline.HasValue) return;
            if (Time.time - hit.Time > KillCreditWindow) return;

            hit.DeathZoneDeadline = Time.time + dyingDelay + DeathZoneGrace;
            hits[id] = hit;
        }

        // The game stores an explosion's owner as a client id that is 0 both for player 0 and for explosions nobody owns
        // (enemy explosions, weapons no player held), so the real owner is kept here when the game creates the explosion.
        // A grenade or mine belongs to the player who armed it, a BoomSpear to its last holder. Anything else belongs to
        // the player whose attack set it off: on the host the explode ClientRpc runs inside that attack.
        public static void SetExplosionOwner(Explosion explosion, NetworkBehaviour source)
        {
            try
            {
                AccuracyTracker.Transfer(source, explosion);
                Owner owner = default;
                if (source is BasicProjectile projectile) owner = ProjectileOwner(projectile);
                else if (source is BoomSpear spear) owner = new Owner { Player = WeaponPlayer(spear), Weapon = spear };
                else if (Owners.TryGet(source.gameObject, out Owners.Record record) && record.Armer != null && Fresh(record.ArmedAt))
                    owner = new Owner { Player = record.Armer, Weapon = source as Weapon };
                if (owner.Player == null) owner = new Owner { Player = CurrentAttacker, Weapon = CurrentWeapon };
                if (owner.Player != null)
                {
                    StoreOwner(explosionOwners, explosion.GetInstanceID(), owner.Player, owner.Weapon);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error recording explosion owner: {ex.Message}");
            }
        }

        public static void OnExplosiveArmed(Weapon explosive, bool byHolder)
        {
            PlayerController player = byHolder ? Holder(explosive) : CurrentAttacker;
            if (player != null)
            {
                Owners.SetArmer(explosive, player);
            }
        }

        private static Owner ProjectileOwner(BasicProjectile projectile)
        {
            return Owners.TryGet(projectile.gameObject, out Owners.Record record) && record.Launcher is ProjectileLauncher &&
                   record.LaunchedBy != null && Fresh(record.LaunchedAt)
                ? new Owner { Player = record.LaunchedBy, Weapon = record.Launcher }
                : default;
        }

        public static PlayerController GetExplosionOwner(Explosion explosion)
        {
            return GetOwner(explosionOwners, explosion.GetInstanceID());
        }

        public static Weapon GetExplosionWeapon(Explosion explosion)
        {
            return explosionOwners.TryGetValue(explosion.GetInstanceID(), out Owner owner) ? owner.Weapon : null;
        }

        public static PlayerController GetDiscOwner(GameObject disc) => DiscRecord(disc, out Owners.Record record) ? record.LaunchedBy : null;

        public static Weapon GetDiscWeapon(GameObject disc) => DiscRecord(disc, out Owners.Record record) ? record.Launcher : null;

        // The launcher's holder owns the disc from the moment LaunchDisc spawns it
        private static bool DiscRecord(GameObject disc, out Owners.Record record)
        {
            return Owners.TryGet(disc, out record) && record.Launcher is DiscLauncher && record.LaunchedBy != null && Fresh(record.LaunchedAt);
        }

        public static PlayerController GetBallReflector(EnergyBall ball)
        {
            return Owners.TryGet(ball.gameObject, out Owners.Record record) && record.Reflector != null && Fresh(record.ReflectedAt) ? record.Reflector : null;
        }

        // The player who equips a weapon owns it until someone else equips it, also after it is thrown; a launched weapon
        // belongs to the launcher's player. Weapons only AI spiders held have none
        public static PlayerController WeaponPlayer(Weapon weapon)
        {
            if (weapon == null) return null;

            PlayerController holder = Holder(weapon);
            if (holder != null) return holder;

            return Owners.TryGet(weapon.gameObject, out Owners.Record record) && (record.Launcher == null || record.Launcher is ProjectileLauncher) &&
                   record.Holder != null && !departed.Contains(record.Holder)
                ? record.Holder
                : null;
        }

        public static void ForgetWeaponsOf(PlayerController player)
        {
            if (player != null) departed.Add(player);
        }

        // The game keeps a weapon's owner after it is dropped or thrown
        private static PlayerController Holder(Weapon weapon)
        {
            WeaponManager owner = weapon.owner;
            return weapon.equipped && owner != null && owner.healthSystem != null
                ? owner.healthSystem.GetComponentInParent<PlayerController>()
                : null;
        }

        // Launches, arms and reflections older than the last prune or session start were dropped with the hits
        private static bool Fresh(float at) => at >= ownersSince;

        public static void OnEnemyDeath(EnemyHealthSystem enemy)
        {
            if (!GameSessionManager.IsRecording) return;
            EnemiesTracker.Instance.IncrementEnemiesKilled();
            if (enemy.serializationEnemyName == SerializationEnemyName.FriendlyWasp) return;

            int id = enemy.GetInstanceID();
            rollerFuses.Remove(id);

            if (!TryTakeHit(lastEnemyHits, id, null, out HitRecord hit, out string reason))
            {
                Logger.LogInfo($"Enemy {enemy.name} died with no kill credited ({reason}) at time {Time.time}");
                return;
            }

            Logger.LogInfo($"Recording kill for {PlayerLabel(hit.Player)}, target:{enemy.name}, weapon:{hit.Weapon}, {Time.time - hit.Time:F2}s after the hit");
            PlayerTracker.Instance.IncrementPlayerKill(hit.Player);
            PlayerTracker.Instance.IncrementWeaponHit(hit.Player, hit.Weapon);
            if (hit.Weapon == PulseWeapon && hit.DeathZoneDeadline.HasValue && Parts.Surge.Running)
            {
                SpiderSurge.SurgeAchievements.Unlock(SpiderSurge.SurgeAchievements.Begone, hit.Player);
                SpiderSurge.AbilityAchievementTracker.Evicted(enemy, hit.Player);
            }

            if (TryGetKillCategory(enemy, out string displayName))
            {
                PlayerTracker.Instance.IncrementEnemyKillByName(hit.Player, displayName);
            }
            else
            {
                Logger.LogWarning($"No kill category for enemy {enemy.name} ({enemy.serializationEnemyName})");
            }
        }

        // Surge's wasp cocoons are cloned from the parkour target and carry the Wasp enemy name, so they get their own category by name
        private static bool TryGetKillCategory(EnemyHealthSystem enemy, out string displayName)
        {
            if (enemy.name.IndexOf("WaspCocoon", StringComparison.Ordinal) >= 0)
            {
                displayName = "Wasp Cocoon";
                return true;
            }
            return enemyDisplayNames.TryGetValue(enemy.serializationEnemyName, out displayName);
        }

        // SpiderSurge's Black Hole removes the enemy without a death; the caster gets the kill
        public static void OnEnemySwallowed(EnemyHealthSystem enemy, PlayerController caster)
        {
            if (enemy == null || !GameSessionManager.IsRecording) return;
            EnemiesTracker.Instance.IncrementEnemiesKilled();

            int id = enemy.GetInstanceID();
            rollerFuses.Remove(id);
            lastEnemyHits.Remove(id);
            if (caster == null) return;

            Logger.LogInfo($"Recording kill for {PlayerLabel(caster)}, target:{enemy.name}, Black Hole");
            PlayerTracker.Instance.IncrementPlayerKill(caster);

            if (TryGetKillCategory(enemy, out string displayName))
            {
                PlayerTracker.Instance.IncrementEnemyKillByName(caster, displayName);
            }
            else
            {
                Logger.LogWarning($"No kill category for enemy {enemy.name} ({enemy.serializationEnemyName})");
            }
        }

        public static void OnEnemyShieldDestroyed(EnemyHealthSystem enemy)
        {
            if (!GameSessionManager.IsRecording) return;
            if (!TryGetRecentHit(lastEnemyHits, enemy.GetInstanceID(), ShieldCreditWindow, out HitRecord hit)) return;

            Logger.LogDebug($"Recording shield hit on enemy {enemy.name} by {PlayerLabel(hit.Player)}");
            PlayerTracker.Instance.IncrementEnemyShieldsTakenDown(hit.Player);
            PlayerTracker.Instance.IncrementWeaponHit(hit.Player, hit.Weapon);
        }

        public static void OnPlayerShieldBroken(SpiderHealthSystem spider)
        {
            if (!GameSessionManager.IsRecording) return;
            if (!TryGetRecentHit(lastPlayerHits, spider.GetInstanceID(), ShieldCreditWindow, out HitRecord hit)) return;

            PlayerController victim = Players.Of(spider.rootObject);
            if (victim == null) return;

            bool friendly = PlayerRelations.AreTeammates(hit.Player, victim);
            Logger.LogDebug($"Recording {(friendly ? "friendly" : "opponent")} shield hit by {PlayerLabel(hit.Player)}");
            if (friendly) PlayerTracker.Instance.IncrementFriendlyShieldsHit(hit.Player);
            else PlayerTracker.Instance.IncrementEnemyShieldsTakenDown(hit.Player);
            PlayerTracker.Instance.IncrementWeaponHit(hit.Player, hit.Weapon);
        }

        public static void OnPlayerDeath(SpiderHealthSystem spider)
        {
            if (!GameSessionManager.IsRecording) return;
            // Lava and falling out of the map mark the spider as dying before it dies, a damage kill happens on the hit
            float? window = GameFields.SpiderDying(spider) ? (float?)null : DirectPlayerKillCreditWindow;
            if (!TryTakeHit(lastPlayerHits, spider.GetInstanceID(), window, out HitRecord hit, out _)) return;

            PlayerController victim = Players.Of(spider.rootObject);
            if (victim == null) return;

            bool friendly = PlayerRelations.AreTeammates(hit.Player, victim);
            Logger.LogInfo($"Recording {(friendly ? "friendly" : "opponent")} kill for {PlayerLabel(hit.Player)}, weapon:{hit.Weapon}, {Time.time - hit.Time:F2}s after the hit");
            PlayerTracker.KillCredit credit = CreditPlayerKill(hit.Player, friendly);
            credit.Weapon = hit.Weapon;
            deathCredits[victim] = credit;
            PlayerTracker.Instance.IncrementWeaponHit(hit.Player, hit.Weapon);
            PlayerTracker.Instance.IncrementEnemyKillByName(hit.Player, "Player");
        }

        // The Astral Heckhound modifier's revive undoes the death, so it takes back the kill too
        public static void OnPlayerRevived(SpiderHealthSystem spider)
        {
            if (spider == null || !Net.IsAuthority) return;
            PlayerController victim = Players.Of(spider.rootObject);
            if (victim == null || !deathCredits.TryGetValue(victim, out PlayerTracker.KillCredit credit)) return;

            deathCredits.Remove(victim);
            Logger.LogInfo($"{PlayerLabel(victim)} returned from the astral plane, taking back the kill from {PlayerLabel(credit.Killer)}");
            PlayerTracker.Instance.RevertKill(credit);
        }

        public static void OnPlayerRespawned(PlayerController player)
        {
            if (player != null) deathCredits.Remove(player);
        }

        private static PlayerTracker.KillCredit CreditPlayerKill(PlayerController killer, bool friendly)
        {
            if (!friendly) return PlayerTracker.Instance.IncrementPlayerKill(killer);
            return PlayerTracker.Instance.IncrementFriendlyKill(killer);
        }

        // SpiderSurge's Black Hole kills the spider right after this, so its own death finds no hit to credit
        public static void OnPlayerSwallowed(SpiderHealthSystem spider, PlayerController caster)
        {
            if (spider == null || !GameSessionManager.IsRecording) return;
            lastPlayerHits.Remove(spider.GetInstanceID());

            PlayerController victim = Players.Of(spider.rootObject);
            if (caster == null || victim == null || victim == caster) return;

            bool friendly = PlayerRelations.AreTeammates(caster, victim);
            Logger.LogInfo($"Recording {(friendly ? "friendly" : "opponent")} kill for {PlayerLabel(caster)}, Black Hole");
            deathCredits[victim] = CreditPlayerKill(caster, friendly);
            PlayerTracker.Instance.IncrementEnemyKillByName(caster, "Player");
        }

        private static void StoreHit(Dictionary<int, HitRecord> hits, int targetId, PlayerController player, string weaponName, Weapon weapon)
        {
            PruneIfDue();
            hits[targetId] = new HitRecord { Player = player, Weapon = weaponName, WeaponObject = weapon, Time = Time.time, FootingSince = -1f };
        }

        private static void StoreOwner(Dictionary<int, Owner> owners, int id, PlayerController player, Weapon weapon = null)
        {
            PruneIfDue();
            owners[id] = new Owner { Player = player, Weapon = weapon, Time = Time.time };
        }

        private static PlayerController GetOwner(Dictionary<int, Owner> owners, int id)
        {
            return owners.TryGetValue(id, out Owner owner) ? owner.Player : null;
        }

        private static void PruneIfDue()
        {
            float now = Time.time;
            if (now - lastPruneTime <= StaleHitAge) return;

            PruneStale(lastEnemyHits, hit => hit.Time, now);
            PruneStale(lastPlayerHits, hit => hit.Time, now);
            PruneStale(rollerFuses, hit => hit.Time, now, StaleFuseAge);
            PruneStale(explosionOwners, owner => owner.Time, now);
            ownersSince = Math.Max(ownersSince, now - StaleHitAge);
            departed.RemoveWhere(player => player == null);
            lastPruneTime = now;
        }

        private static void PruneStale<T>(Dictionary<int, T> entries, Func<T, float> time, float now, float maxAge = StaleHitAge)
        {
            List<int> stale = null;
            foreach (var entry in entries)
            {
                if (now - time(entry.Value) > maxAge)
                {
                    (stale ??= new List<int>()).Add(entry.Key);
                }
            }
            stale?.ForEach(id => entries.Remove(id));
        }

        private static bool TryGetRecentHit(Dictionary<int, HitRecord> hits, int targetId, float window, out HitRecord hit)
        {
            return hits.TryGetValue(targetId, out hit) && hit.Player != null && Time.time - hit.Time <= window;
        }

        // Without a fixed window, a target that reached lava in time has until its death zone deadline
        private static bool TryTakeHit(Dictionary<int, HitRecord> hits, int targetId, float? fixedWindow, out HitRecord hit, out string reason)
        {
            if (!hits.TryGetValue(targetId, out hit))
            {
                reason = "no player hit it last";
                return false;
            }

            hits.Remove(targetId);
            if (hit.Player == null)
            {
                reason = "the player who hit it left";
                return false;
            }

            float age = Time.time - hit.Time;
            float window = fixedWindow ?? (hit.DeathZoneDeadline - hit.Time) ?? KillCreditWindow;
            if (age > window)
            {
                reason = $"last hit by {PlayerLabel(hit.Player)} with {hit.Weapon} was {age:F1}s earlier";
                return false;
            }

            reason = null;
            return true;
        }

        private static string PlayerLabel(PlayerController player)
        {
            return player != null ? $"Player {player.playerID.Value + 1}" : "unknown player";
        }
    }
}
