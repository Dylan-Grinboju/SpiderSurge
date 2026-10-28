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

        // The attack running now. In an attack patch's __state, the attack it interrupted
        public struct AttackScope
        {
            internal bool Saved;
            internal object Source;
            internal Func<object, PlayerController> FindPlayer;
            internal Func<object, Weapon> FindWeapon;
            internal bool PlayerFound;
            internal PlayerController Player;
            internal bool WeaponFound;
            internal Weapon WeaponObject;
            internal string Weapon;
            internal PlayerController EnclosingPlayer;

            internal bool Active => FindPlayer != null;
        }

        private static readonly Dictionary<int, HitRecord> lastEnemyHits = new Dictionary<int, HitRecord>();
        private static readonly Dictionary<int, HitRecord> lastPlayerHits = new Dictionary<int, HitRecord>();
        private static readonly Dictionary<int, HitRecord> rollerFuses = new Dictionary<int, HitRecord>();
        private static readonly Dictionary<int, Owner> explosionOwners = new Dictionary<int, Owner>();
        private static readonly Dictionary<int, Owner> projectileOwners = new Dictionary<int, Owner>();
        private static readonly Dictionary<int, Owner> discOwners = new Dictionary<int, Owner>();
        private static readonly Dictionary<int, Owner> ballReflectors = new Dictionary<int, Owner>();
        private static readonly Dictionary<int, Owner> explosiveArmers = new Dictionary<int, Owner>();
        private static readonly Dictionary<int, Owner>[] ownerMaps = { explosionOwners, projectileOwners, discOwners, ballReflectors, explosiveArmers };
        private static readonly Dictionary<Weapon, PlayerController> weaponPlayers = new Dictionary<Weapon, PlayerController>();
        private static readonly Dictionary<PlayerController, PlayerTracker.KillCredit> deathCredits = new Dictionary<PlayerController, PlayerTracker.KillCredit>();
        private static float lastPruneTime = 0f;

        private static AttackScope current;

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
        public static AttackScope BeginAttack(object source, Func<object, PlayerController> findPlayer, Func<object, Weapon> findWeapon, string weaponName)
        {
            PlayerController enclosing = CurrentAttacker;
            AttackScope previous = current;
            previous.Saved = true;
            current = new AttackScope { Source = source, FindPlayer = findPlayer, FindWeapon = findWeapon, Weapon = weaponName, EnclosingPlayer = enclosing };
            return previous;
        }

        public static void EndAttack(AttackScope previous)
        {
            if (previous.Saved) current = previous;
        }

        public static PlayerController CurrentAttacker
        {
            get
            {
                if (current.Active && !current.PlayerFound)
                {
                    current.PlayerFound = true;
                    current.Player = current.FindPlayer(current.Source);
                }
                return current.Player;
            }
        }

        public static PlayerController EnclosingAttacker => current.EnclosingPlayer;

        private static Weapon CurrentWeapon
        {
            get
            {
                if (current.Active && !current.WeaponFound)
                {
                    current.WeaponFound = true;
                    current.WeaponObject = current.FindWeapon?.Invoke(current.Source);
                }
                return current.WeaponObject;
            }
        }

        public static bool HasPlayerHits => lastPlayerHits.Count > 0;

        public static void Clear()
        {
            AccuracyTracker.Reset();
            lastEnemyHits.Clear();
            lastPlayerHits.Clear();
            rollerFuses.Clear();
            foreach (var owners in ownerMaps) owners.Clear();
            deathCredits.Clear();
            current = default;
            lastPruneTime = Time.time;
        }

        // An attack without a player, or damage from anything else (a friendly wasp, a hazard), takes the kill away
        // from the last player who hit the enemy. Bumps from enemies, pulls and portals outside an attack don't.
        public static void OnEnemyHit(EnemyHealthSystem enemy, bool damage)
        {
            if (!Net.IsAuthority || !GameSessionManager.Instance.IsActive) return;
            if (enemy.serializationEnemyName == SerializationEnemyName.FriendlyWasp) return;
            AccuracyTracker.EnemyHit(current.Source);

            PlayerController attacker = CurrentAttacker;
            if (attacker != null)
            {
                StoreHit(lastEnemyHits, enemy.GetInstanceID(), attacker, current.Weapon, CurrentWeapon);
            }
            else if (current.Active || damage)
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
            if (!Net.IsAuthority || !GameSessionManager.Instance.IsActive) return;
            PlayerController victim = Players.Of(spider.rootObject);
            if (!spider.dead && !spider.astralDead) AccuracyTracker.PlayerHit(current.Source, victim);
            int id = spider.GetInstanceID();
            PlayerController attacker = CurrentAttacker;
            if (attacker == null)
            {
                if (damage || (current.Active && stun)) lastPlayerHits.Remove(id);
                return;
            }

            if (victim == null) return;

            if (victim != attacker)
            {
                StoreHit(lastPlayerHits, id, attacker, current.Weapon, null);
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
        // (enemy explosions, weapons no player held), so the real owner is kept here, set where the game sets its own.
        // A grenade or mine belongs to the player who armed it, a BoomSpear to its last holder. Anything else belongs to
        // the player whose attack set it off: on the host the explode ClientRpc runs inside that attack.
        public static void SetExplosionOwner(Explosion explosion, ulong ownerId, NetworkBehaviour source)
        {
            explosion.explosionOwnerId = ownerId;
            try
            {
                AccuracyTracker.Transfer(source, explosion);
                Owner owner;
                if (source is BasicProjectile projectile) projectileOwners.TryGetValue(projectile.GetInstanceID(), out owner);
                else if (source is BoomSpear spear) owner = new Owner { Player = WeaponPlayer(spear), Weapon = spear };
                else explosiveArmers.TryGetValue(source.GetInstanceID(), out owner);
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
                StoreOwner(explosiveArmers, explosive.GetInstanceID(), player, explosive);
            }
        }

        public static void SetProjectileOwner(BasicProjectile projectile, ulong ownerId, NetworkBehaviour launcher)
        {
            projectile.projectileOwnerId = ownerId;
            try
            {
                Weapon weapon = launcher as Weapon;
                PlayerController player = WeaponPlayer(weapon);
                if (player != null)
                {
                    StoreOwner(projectileOwners, projectile.GetInstanceID(), player, weapon);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error recording projectile owner: {ex.Message}");
            }
        }

        public static PlayerController GetExplosionOwner(Explosion explosion)
        {
            return GetOwner(explosionOwners, explosion.GetInstanceID());
        }

        public static Weapon GetExplosionWeapon(Explosion explosion)
        {
            return explosionOwners.TryGetValue(explosion.GetInstanceID(), out Owner owner) ? owner.Weapon : null;
        }

        public static void RegisterDiscOwner(GameObject disc, PlayerController owner, Weapon launcher)
        {
            if (disc != null && owner != null)
            {
                StoreOwner(discOwners, disc.GetInstanceID(), owner, launcher);
            }
        }

        public static PlayerController GetDiscOwner(GameObject disc)
        {
            return disc != null ? GetOwner(discOwners, disc.GetInstanceID()) : null;
        }

        public static Weapon GetDiscWeapon(GameObject disc)
        {
            return disc != null && discOwners.TryGetValue(disc.GetInstanceID(), out Owner owner) ? owner.Weapon : null;
        }

        public static void OnBallReflected(EnergyBall ball, PlayerController reflector)
        {
            AccuracyTracker.Forget(ball);
            if (reflector != null)
            {
                StoreOwner(ballReflectors, ball.GetInstanceID(), reflector);
            }
            else
            {
                ballReflectors.Remove(ball.GetInstanceID());
            }
        }

        public static PlayerController GetBallReflector(EnergyBall ball)
        {
            return GetOwner(ballReflectors, ball.GetInstanceID());
        }

        public static PlayerController WeaponPlayer(Weapon weapon)
        {
            if (weapon == null) return null;

            PlayerController holder = Holder(weapon);
            if (holder != null) return holder;

            // Weapons only AI spiders held have none
            return weaponPlayers.TryGetValue(weapon, out PlayerController player) && player != null ? player : null;
        }

        public static void SetWeaponPlayer(Weapon weapon, PlayerController player)
        {
            if (weapon == null) return;
            PruneIfDue();
            if (player != null) weaponPlayers[weapon] = player;
            else weaponPlayers.Remove(weapon);
        }

        public static void SetLaunchedWeaponOwner(Weapon weapon, ulong ownerId, NetworkBehaviour launcher)
        {
            weapon.ownerWeaponClientId = ownerId;
            try
            {
                PlayerController player = WeaponPlayer(launcher as Weapon);
                SetWeaponPlayer(weapon, player);
                // A Grenade Launcher's grenades come out armed, so they belong to the launcher's player from the start
                if (weapon is Grenade grenade && grenade.preArmed && player != null) StoreOwner(explosiveArmers, weapon.GetInstanceID(), player, weapon);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error recording launched weapon owner: {ex.Message}");
            }
        }

        public static void ForgetWeaponsOf(PlayerController player)
        {
            List<Weapon> owned = null;
            foreach (var entry in weaponPlayers)
            {
                if (entry.Value == player) (owned ??= new List<Weapon>()).Add(entry.Key);
            }
            owned?.ForEach(weapon => weaponPlayers.Remove(weapon));
        }

        // The game keeps a weapon's owner after it is dropped or thrown
        private static PlayerController Holder(Weapon weapon)
        {
            WeaponManager owner = weapon.owner;
            return weapon.equipped && owner != null && owner.healthSystem != null
                ? owner.healthSystem.GetComponentInParent<PlayerController>()
                : null;
        }

        public static void OnEnemyDeath(EnemyHealthSystem enemy)
        {
            if (!Net.IsAuthority || !GameSessionManager.Instance.IsActive) return;
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
            if (enemy == null || !Net.IsAuthority || !GameSessionManager.Instance.IsActive) return;
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
            if (!Net.IsAuthority || !GameSessionManager.Instance.IsActive) return;
            if (!TryGetRecentHit(lastEnemyHits, enemy.GetInstanceID(), ShieldCreditWindow, out HitRecord hit)) return;

            Logger.LogDebug($"Recording shield hit on enemy {enemy.name} by {PlayerLabel(hit.Player)}");
            PlayerTracker.Instance.IncrementEnemyShieldsTakenDown(hit.Player);
            PlayerTracker.Instance.IncrementWeaponHit(hit.Player, hit.Weapon);
        }

        public static void OnPlayerShieldBroken(SpiderHealthSystem spider)
        {
            if (!Net.IsAuthority || !GameSessionManager.Instance.IsActive) return;
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
            if (!Net.IsAuthority || !GameSessionManager.Instance.IsActive) return;
            // Lava and falling out of the map mark the spider as dying before it dies, a damage kill happens on the hit
            float? window = ReflectionCache.SpiderDying(spider) ? (float?)null : DirectPlayerKillCreditWindow;
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
            PlayerTracker.Instance.IncrementFriendlyKill(killer);
            return new PlayerTracker.KillCredit { Killer = killer, Friendly = true };
        }

        // SpiderSurge's Black Hole kills the spider right after this, so its own death finds no hit to credit
        public static void OnPlayerSwallowed(SpiderHealthSystem spider, PlayerController caster)
        {
            if (spider == null || !Net.IsAuthority || !GameSessionManager.Instance.IsActive) return;
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
            foreach (Dictionary<int, Owner> owners in ownerMaps)
            {
                PruneStale(owners, owner => owner.Time, now);
            }
            List<Weapon> destroyed = null;
            foreach (Weapon weapon in weaponPlayers.Keys)
            {
                if (weapon == null) (destroyed ??= new List<Weapon>()).Add(weapon);
            }
            destroyed?.ForEach(weapon => weaponPlayers.Remove(weapon));
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
