using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Silk;
using UnityEngine;
using SpiderSurge.Common;
using Logger = Silk.Logger;

namespace StatsMod
{
    public class PlayerTracker
    {
        private static readonly Lazy<PlayerTracker> _lazy = new Lazy<PlayerTracker>(() => new PlayerTracker());
        public static PlayerTracker Instance => _lazy.Value;

        private Dictionary<PlayerController, PlayerData> activePlayers = new Dictionary<PlayerController, PlayerData>();

        // Cache for expensive player lookups
        private static PlayerController[] cachedPlayerControllers;
        private static float lastPlayerCacheUpdate = 0f;
        private static float playerCacheRefreshInterval = 60f; // Refresh every 60 seconds
        private static readonly object playerCacheLock = new object();

        private bool isPaused = false;

        // Changes whenever a player joins, leaves or gets a new playerID
        public int RosterVersion { get; private set; }

        // Raised with +1 when a tracked player's death is recorded and -1 when one is undone
        public event Action<int> DeathCountChanged;

        // What one player kill added to its killer's stats, so an astral revive can take it back
        public struct KillCredit
        {
            public PlayerController Killer;
            public bool Friendly;
            public string Weapon;
            internal int KillerDeaths;
            internal bool Streak;
            internal bool RaisedMax;
            internal bool Airborne;
            internal bool Solo;
            internal bool RaisedSoloMax;
        }

        public class PlayerData
        {
            public int PlayerId { get; set; }
            internal bool? RemoteAlive { get; set; }
            public int Deaths { get; set; }
            public int LavaDeaths { get; set; }
            public int Kills { get; set; }
            public int KillsWhileAirborne { get; set; }
            public int KillsWhileSolo { get; set; }
            public int FriendlyKills { get; set; }
            public int WaveClutches { get; set; }
            public int AstralReturns { get; set; }
            public int EnemyShieldsTakenDown { get; set; }
            public int FriendlyShieldsHit { get; set; }
            public int ShieldsLost { get; set; }
            public int KillStreak { get; set; }
            public int MaxKillStreak { get; set; }
            public int KillStreakWhileSolo { get; set; }
            public int MaxKillStreakWhileSolo { get; set; }
            public string PlayerName { get; set; }
            internal bool HasLocalName { get; set; }
            public float JoinTime { get; set; }
            public Color PlayerColor { get; set; }
            public float? CurrentAliveStartTime { get; set; }
            public float TotalAliveTime { get; set; }
            public bool WasAliveWhenPaused { get; set; }
            public int WebSwings { get; set; }
            public float WebSwingTime { get; set; }
            public float? CurrentWebSwingStartTime { get; set; }
            public bool WasSwingingWhenPaused { get; set; }
            public float AirborneTime { get; set; }
            public float? CurrentAirborneStartTime { get; set; }
            public bool WasAirborneWhenPaused { get; set; }
            public float HighestPoint { get; set; }
            public Color SecondaryColor { get; set; }
            public Dictionary<string, int> WeaponHits { get; set; }
            public WeaponAccuracy ShootingAccuracy { get; } = new WeaponAccuracy();
            public Dictionary<string, WeaponAccuracy> AccuracyByWeapon { get; } = new Dictionary<string, WeaponAccuracy>();
            public Dictionary<string, int> EnemyKills { get; set; }
            public Dictionary<string, int> DeathsPerMap { get; set; }

            public PlayerData(int id, string name = "Player")
            {
                PlayerId = id;
                Deaths = 0;
                LavaDeaths = 0;
                Kills = 0;
                KillsWhileAirborne = 0;
                KillsWhileSolo = 0;
                FriendlyKills = 0;
                WaveClutches = 0;
                AstralReturns = 0;
                EnemyShieldsTakenDown = 0;
                FriendlyShieldsHit = 0;
                ShieldsLost = 0;
                KillStreak = 0;
                MaxKillStreak = 0;
                KillStreakWhileSolo = 0;
                MaxKillStreakWhileSolo = 0;
                PlayerName = name;
                JoinTime = Time.unscaledTime;
                PlayerColor = Color.white;
                CurrentAliveStartTime = null;
                TotalAliveTime = 0f;
                WasAliveWhenPaused = false;
                WebSwings = 0;
                WebSwingTime = 0f;
                CurrentWebSwingStartTime = null;
                WasSwingingWhenPaused = false;
                AirborneTime = 0f;
                CurrentAirborneStartTime = null;
                WasAirborneWhenPaused = false;
                HighestPoint = 0;
                SecondaryColor = Color.white;
                WeaponHits = new Dictionary<string, int>
                {
                    { "Shotgun", 0 },
                    { "RailShot", 0 },
                    { "DeathCube", 0 },
                    { "DeathRay", 0 },
                    { "EnergyBall", 0 },
                    { "Particle Blade", 0 },
                    { "KhepriStaff", 0 },
                    { "Laser Cannon", 0 },
                    { "Laser Cube", 0 },
                    { "SawDisc", 0 },
                    { "Explosions", 0 },
                    { "Snowball", 0 },
                    { "Pulse", 0 }
                };
                EnemyKills = new Dictionary<string, int>
                {
                    { "Wasp", 0 },
                    { "Wasp Cocoon", 0 },
                    { "Power Wasp", 0 },
                    { "Roller", 0 },
                    { "Whisp", 0 },
                    { "Power Whisp", 0 },
                    { "Melee Whisp", 0 },
                    { "Power Melee Whisp", 0 },
                    { "Khepri", 0 },
                    { "Power Khepri", 0 },
                    { "Hornet Shaman", 0 },
                    { "Hornet", 0 },
                    { "Player", 0 }
                };
                DeathsPerMap = new Dictionary<string, int>();
            }

            public bool IsAlive => RemoteAlive ?? (CurrentAliveStartTime.HasValue || WasAliveWhenPaused);

            public TimeSpan GetCurrentAliveTime()
            {
                if (CurrentAliveStartTime.HasValue)
                {
                    return TimeSpan.FromSeconds(TotalAliveTime + (Time.unscaledTime - CurrentAliveStartTime.Value));
                }
                return TimeSpan.FromSeconds(TotalAliveTime);
            }

            public TimeSpan GetCurrentWebSwingTime()
            {
                if (CurrentWebSwingStartTime.HasValue)
                {
                    return TimeSpan.FromSeconds(WebSwingTime + (Time.unscaledTime - CurrentWebSwingStartTime.Value));
                }
                return TimeSpan.FromSeconds(WebSwingTime);
            }

            public TimeSpan GetCurrentAirborneTime()
            {
                if (CurrentAirborneStartTime.HasValue)
                {
                    return TimeSpan.FromSeconds(AirborneTime + (Time.unscaledTime - CurrentAirborneStartTime.Value));
                }
                return TimeSpan.FromSeconds(AirborneTime);
            }

            public void StopAliveTimer()
            {
                if (CurrentAliveStartTime.HasValue)
                {
                    TotalAliveTime += Time.unscaledTime - CurrentAliveStartTime.Value;
                    CurrentAliveStartTime = null;
                }
                WasAliveWhenPaused = false;
            }

            public void StopWebSwingTimer()
            {
                if (CurrentWebSwingStartTime.HasValue)
                {
                    WebSwingTime += Time.unscaledTime - CurrentWebSwingStartTime.Value;
                    CurrentWebSwingStartTime = null;
                }
            }

            public void StopAirborneTimer()
            {
                if (CurrentAirborneStartTime.HasValue)
                {
                    AirborneTime += Time.unscaledTime - CurrentAirborneStartTime.Value;
                    CurrentAirborneStartTime = null;
                }
            }
        }

        public PlayerTracker()
        {
            Logger.LogInfo("Player tracker initialized");
        }


        public void RegisterPlayer(PlayerController player)
        {
            if (player == null) return;

            if (activePlayers.ContainsKey(player))
            {
                return;
            }

            PlayerData playerData = new PlayerData(player.playerID.Value, NameFor(player));

            // Try to get the spider customizer and set the initial colors
            SpiderCustomizer customizer = player.GetComponentInChildren<SpiderCustomizer>();
            if (customizer != null)
            {
                if (ReflectionCache.PrimaryColorField != null)
                {
                    Color primaryColor = (Color)ReflectionCache.PrimaryColorField.GetValue(customizer);
                    playerData.PlayerColor = primaryColor;
                }
                if (ReflectionCache.SecondaryColorField != null)
                {
                    Color secondaryColor = (Color)ReflectionCache.SecondaryColorField.GetValue(customizer);
                    playerData.SecondaryColor = secondaryColor;
                }
            }

            activePlayers[player] = playerData;
            RosterVersion++;

            RefreshPlayerCache();
        }

        private static string NameFor(PlayerController player)
        {
            return Net.IsOnline ? Players.NameOf(player) : $"Player {player.playerID.Value + 1}";
        }

        // playerID can still be unset when the lobby adds a player, so ids and names are re-read when a session starts
        public void RefreshIdentities()
        {
            if (!Net.IsAuthority) return;
            bool online = Net.IsOnline;
            foreach (var entry in activePlayers)
            {
                if (entry.Key == null) continue;
                int id = entry.Key.playerID.Value;
                if (!online && entry.Value.HasLocalName && entry.Value.PlayerId == id) continue;
                if (entry.Value.PlayerId != id) RosterVersion++;
                // Names change too when a local game goes online (Player 1 becomes the account name)
                entry.Value.PlayerId = id;
                entry.Value.PlayerName = NameFor(entry.Key);
                entry.Value.HasLocalName = !online;
            }
        }

        // On a client the host's snapshot is read straight into the data of the matching player
        internal PlayerData RemoteDataFor(int playerId)
        {
            PlayerController player = Players.ById(playerId);
            if (player == null) return null;
            if (!activePlayers.TryGetValue(player, out PlayerData data))
            {
                RegisterPlayer(player);
                activePlayers.TryGetValue(player, out data);
            }
            return data;
        }


        public void UnregisterPlayer(PlayerController player)
        {
            if (player == null) return;

            if (activePlayers.TryGetValue(player, out PlayerData playerData))
            {
                activePlayers.Remove(player);
                RosterVersion++;
                ForgetLooseWeapons(player);

                RefreshPlayerCache();

                StabilizerFixedUpdatePatch.ClearCache();
            }
        }

        // A thrown weapon keeps its thrower's playerID, which the next player to join reuses, so a leaver's weapons lose their owner
        private static void ForgetLooseWeapons(PlayerController player)
        {
            if (!Net.IsAuthority) return;
            ulong id = (ulong)player.playerID.Value;
            foreach (Weapon weapon in UnityEngine.Object.FindObjectsOfType<Weapon>())
            {
                if (weapon.ownerWeaponClientId == id)
                {
                    weapon.ownerWeaponClientId = ulong.MaxValue;
                }
            }
        }

        public void RecordPlayerDeath(SpiderHealthSystem spiderHealth)
        {
            if (spiderHealth == null)
            {
                Logger.LogWarning("RecordPlayerDeath called with null spiderHealth");
                return;
            }

            try
            {
                PlayerController playerInput = Players.Of(spiderHealth.rootObject);
                if (spiderHealth.rootObject == null)
                {
                    Logger.LogWarning("SpiderHealthSystem has null rootObject");
                }

                // A client mirrors the host's counts, so it records nothing itself
                if (!Net.IsAuthority) return;
                if (playerInput != null && activePlayers.TryGetValue(playerInput, out PlayerData data))
                {
                    data.Deaths++;
                    DeathCountChanged?.Invoke(1);
                    data.KillStreak = 0;
                    data.KillStreakWhileSolo = 0;
                    data.StopAliveTimer();
                    data.StopWebSwingTimer();
                    data.StopAirborneTimer();

                    // Track death on the current map
                    string currentMap = GameSessionManager.Instance.CurrentMapName;
                    if (data.DeathsPerMap.ContainsKey(currentMap))
                        data.DeathsPerMap[currentMap]++;
                    else
                        data.DeathsPerMap[currentMap] = 1;
                }
                else
                {
                    Logger.LogWarning($"Could not find player data for death event");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error recording player death: {ex.Message}");
            }
        }

        public void UndoPlayerDeath(SpiderHealthSystem spiderHealth)
        {
            if (spiderHealth == null)
            {
                Logger.LogWarning("UndoPlayerDeath called with null spiderHealth");
                return;
            }

            try
            {
                PlayerController playerInput = Players.Of(spiderHealth.rootObject);

                if (Net.IsAuthority && playerInput != null && activePlayers.TryGetValue(playerInput, out PlayerData data))
                {
                    data.Deaths--;
                    data.AstralReturns++;
                    DeathCountChanged?.Invoke(-1);

                    // Undo death on the current map
                    string currentMap = GameSessionManager.Instance.CurrentMapName;
                    if (data.DeathsPerMap.ContainsKey(currentMap) && data.DeathsPerMap[currentMap] > 0)
                    {
                        data.DeathsPerMap[currentMap]--;
                        if (data.DeathsPerMap[currentMap] == 0)
                            data.DeathsPerMap.Remove(currentMap);
                    }

                    StartAliveTimer(playerInput);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error undoing player death: {ex.Message}");
            }
        }

        public void ResetPlayerStats()
        {
            foreach (var entry in activePlayers)
            {
                entry.Value.RemoteAlive = null;
                entry.Value.ShootingAccuracy.Reset();
                entry.Value.AccuracyByWeapon.Clear();
                entry.Value.Deaths = 0;
                entry.Value.LavaDeaths = 0;
                entry.Value.Kills = 0;
                entry.Value.KillsWhileAirborne = 0;
                entry.Value.KillsWhileSolo = 0;
                entry.Value.FriendlyKills = 0;
                entry.Value.WaveClutches = 0;
                entry.Value.AstralReturns = 0;
                entry.Value.EnemyShieldsTakenDown = 0;
                entry.Value.FriendlyShieldsHit = 0;
                entry.Value.ShieldsLost = 0;
                entry.Value.WebSwings = 0;
                entry.Value.WebSwingTime = 0f;
                entry.Value.AirborneTime = 0f;
                entry.Value.KillStreak = 0;
                entry.Value.MaxKillStreak = 0;
                entry.Value.KillStreakWhileSolo = 0;
                entry.Value.MaxKillStreakWhileSolo = 0;
                entry.Value.TotalAliveTime = 0f;
                entry.Value.CurrentAliveStartTime = null;
                entry.Value.CurrentWebSwingStartTime = null;
                entry.Value.CurrentAirborneStartTime = null;
                entry.Value.HighestPoint = 0;
                foreach (var weaponKey in entry.Value.WeaponHits.Keys.ToList())
                {
                    entry.Value.WeaponHits[weaponKey] = 0;
                }
                foreach (var enemyKey in entry.Value.EnemyKills.Keys.ToList())
                {
                    entry.Value.EnemyKills[enemyKey] = 0;
                }
                entry.Value.DeathsPerMap.Clear();
            }
        }

        // In playerID order, so tables list players the same way after someone leaves and another joins
        public Dictionary<PlayerController, PlayerData> GetActivePlayers()
        {
            RefreshIdentities();
            var players = new Dictionary<PlayerController, PlayerData>(activePlayers.Count);
            foreach (var entry in activePlayers.OrderBy(p => p.Value.PlayerId))
            {
                players.Add(entry.Key, entry.Value);
            }
            return players;
        }

        internal bool TryGetPlayer(PlayerController player, out PlayerData data) => activePlayers.TryGetValue(player, out data);

        public bool IsOnlyOnePlayerAlive()
        {
            int aliveCount = activePlayers.Count(p => p.Value.IsAlive);
            return aliveCount == 1;
        }

        public PlayerController GetOnlyAlivePlayer()
        {
            if (!IsOnlyOnePlayerAlive())
                return null;

            return activePlayers.FirstOrDefault(p => p.Value.IsAlive).Key;
        }

        private bool HasLivingTeammate(PlayerController player)
        {
            return activePlayers.Any(p => p.Value.IsAlive && PlayerRelations.AreTeammates(player, p.Key));
        }

        public KillCredit IncrementPlayerKill(PlayerController player)
        {
            KillCredit credit = new KillCredit { Killer = player };
            if (Net.IsAuthority && player != null && activePlayers.TryGetValue(player, out PlayerData data))
            {
                data.Kills++;
                credit.KillerDeaths = data.Deaths;

                // A kill credited after the killer died (the target reached lava or blew up later) starts no streak
                if (!data.IsAlive) return credit;

                credit.Streak = true;
                data.KillStreak++;
                if (data.KillStreak > data.MaxKillStreak)
                {
                    data.MaxKillStreak = data.KillStreak;
                    credit.RaisedMax = true;
                }

                SpiderController spider = player.GetComponentInChildren<SpiderController>();
                if (spider != null)
                {
                    Stabilizer stabilizer = spider.GetComponentInChildren<Stabilizer>();
                    if (stabilizer != null && !stabilizer.grounded)
                    {
                        data.KillsWhileAirborne++;
                        credit.Airborne = true;
                    }
                }

                if (!HasLivingTeammate(player))
                {
                    credit.Solo = true;
                    data.KillsWhileSolo++;
                    data.KillStreakWhileSolo++;
                    if (data.KillStreakWhileSolo > data.MaxKillStreakWhileSolo)
                    {
                        data.MaxKillStreakWhileSolo = data.KillStreakWhileSolo;
                        credit.RaisedSoloMax = true;
                    }
                }
            }
            return credit;
        }

        // A streak the killer has since lost to a death stays as it was; a streak still running loses this kill
        public void RevertKill(KillCredit credit)
        {
            if (!Net.IsAuthority || credit.Killer == null || !activePlayers.TryGetValue(credit.Killer, out PlayerData data)) return;

            if (credit.Friendly)
            {
                if (data.FriendlyKills > 0) data.FriendlyKills--;
            }
            else
            {
                if (data.Kills > 0) data.Kills--;
                if (credit.Airborne && data.KillsWhileAirborne > 0) data.KillsWhileAirborne--;
                if (credit.Solo && data.KillsWhileSolo > 0) data.KillsWhileSolo--;
                if (credit.Streak && data.Deaths == credit.KillerDeaths)
                {
                    if (credit.RaisedMax && data.MaxKillStreak == data.KillStreak) data.MaxKillStreak--;
                    if (data.KillStreak > 0) data.KillStreak--;
                    if (credit.Solo)
                    {
                        if (credit.RaisedSoloMax && data.MaxKillStreakWhileSolo == data.KillStreakWhileSolo) data.MaxKillStreakWhileSolo--;
                        if (data.KillStreakWhileSolo > 0) data.KillStreakWhileSolo--;
                    }
                }
            }

            if (credit.Weapon != null && data.WeaponHits.TryGetValue(credit.Weapon, out int hits) && hits > 0)
            {
                data.WeaponHits[credit.Weapon] = hits - 1;
            }
            if (data.EnemyKills.TryGetValue("Player", out int kills) && kills > 0)
            {
                data.EnemyKills["Player"] = kills - 1;
            }
        }

        public void IncrementFriendlyKill(PlayerController player)
        {
            if (Net.IsAuthority && player != null && activePlayers.TryGetValue(player, out PlayerData data))
            {
                data.FriendlyKills++;
            }
        }

        public void IncrementWaveClutch(PlayerController player)
        {
            if (Net.IsAuthority && player != null && activePlayers.TryGetValue(player, out PlayerData data))
            {
                data.WaveClutches++;
            }
        }

        public void IncrementEnemyShieldsTakenDown(PlayerController player)
        {
            if (Net.IsAuthority && player != null && activePlayers.TryGetValue(player, out PlayerData data))
            {
                data.EnemyShieldsTakenDown++;
            }
        }

        public void IncrementFriendlyShieldsHit(PlayerController player)
        {
            if (Net.IsAuthority && player != null && activePlayers.TryGetValue(player, out PlayerData data))
            {
                data.FriendlyShieldsHit++;
            }
        }

        public void IncrementShieldsLost(PlayerController player)
        {
            if (Net.IsAuthority && player != null && activePlayers.TryGetValue(player, out PlayerData data))
            {
                data.ShieldsLost++;
            }
        }

        public void IncrementLavaDeaths(PlayerController player)
        {
            if (Net.IsAuthority && player != null && activePlayers.TryGetValue(player, out PlayerData data))
            {
                data.LavaDeaths++;
            }
        }

        public void IncrementWeaponHit(PlayerController player, string weaponName)
        {
            if (Net.IsAuthority && player != null && activePlayers.TryGetValue(player, out PlayerData data))
            {
                if (data.WeaponHits.ContainsKey(weaponName))
                {
                    data.WeaponHits[weaponName]++;
                }
                else
                {
                    Logger.LogError($"Unknown weapon name: {weaponName}. This weapon is not pre-populated in WeaponHits dictionary.");
                }
            }
        }

        public void IncrementEnemyKillByName(PlayerController player, string enemyName)
        {
            if (Net.IsAuthority && player != null && activePlayers.TryGetValue(player, out PlayerData data))
            {
                if (data.EnemyKills.ContainsKey(enemyName))
                {
                    data.EnemyKills[enemyName]++;
                }
                else
                {
                    Logger.LogError($"Unknown enemy name: {enemyName}. This enemy is not pre-populated in EnemyKills dictionary.");
                }
            }
        }

        public void IncrementWebSwings(PlayerController player)
        {
            if (Net.IsAuthority && player != null && activePlayers.TryGetValue(player, out PlayerData data))
            {
                data.WebSwings++;
            }
        }

        public void StartWebSwingTimer(PlayerController player)
        {
            if (Net.IsAuthority && player != null && activePlayers.TryGetValue(player, out PlayerData data))
            {
                if (data.CurrentWebSwingStartTime.HasValue)
                {
                    return;
                }
                data.CurrentWebSwingStartTime = Time.unscaledTime;
            }
        }

        public void StopWebSwingTimer(PlayerController player)
        {
            if (Net.IsAuthority && player != null && activePlayers.TryGetValue(player, out PlayerData data))
            {
                data.StopWebSwingTimer();
            }
        }

        public void StartAirborneTimer(PlayerController player)
        {
            if (Net.IsAuthority && player != null && activePlayers.TryGetValue(player, out PlayerData data))
            {
                if (data.CurrentAirborneStartTime.HasValue)
                {
                    return;
                }
                data.CurrentAirborneStartTime = Time.unscaledTime;
            }
        }

        public void StopAirborneTimer(PlayerController player)
        {
            if (Net.IsAuthority && player != null && activePlayers.TryGetValue(player, out PlayerData data))
            {
                data.StopAirborneTimer();
            }
        }

        public void UpdatePlayerColor(PlayerController player, Color color)
        {
            if (Net.IsAuthority && player != null && activePlayers.TryGetValue(player, out PlayerData data))
            {
                data.PlayerColor = color;
            }
        }

        public void UpdateHighestPoint(PlayerController player, SpiderController spider)
        {
            if (Net.IsAuthority && player != null && spider != null && activePlayers.TryGetValue(player, out PlayerData data))
            {
                if (spider.bodyRigidbody2D != null)
                {
                    float currentY = spider.bodyRigidbody2D.position.y;
                    if (currentY > data.HighestPoint)
                    {
                        data.HighestPoint = currentY;
                    }
                }
            }
        }

        public static PlayerController[] GetCachedPlayerControllers()
        {
            lock (playerCacheLock)
            {
                float currentTime = Time.time;
                if (cachedPlayerControllers == null || currentTime - lastPlayerCacheUpdate > playerCacheRefreshInterval)
                {
                    cachedPlayerControllers = UnityEngine.Object.FindObjectsOfType<PlayerController>();
                    lastPlayerCacheUpdate = currentTime;
                }
                return cachedPlayerControllers;
            }
        }

        public static PlayerController FindPlayerById(ulong playerId)
        {
            return playerId <= int.MaxValue ? Players.ById((int)playerId) : null;
        }


        public static void RefreshPlayerCache()
        {
            lock (playerCacheLock)
            {
                cachedPlayerControllers = null;
                lastPlayerCacheUpdate = 0f;
            }
        }

        public void StartAliveTimer(PlayerController playerInput)
        {
            if (!Net.IsAuthority) return;
            if (playerInput != null && activePlayers.TryGetValue(playerInput, out PlayerData data))
            {
                if (data.CurrentAliveStartTime.HasValue)
                {
                    return;
                }
                data.CurrentAliveStartTime = Time.unscaledTime;

                foreach (var entry in activePlayers)
                {
                    if (HasLivingTeammate(entry.Key))
                    {
                        entry.Value.KillStreakWhileSolo = 0;
                    }
                }
            }
            else
            {
                Logger.LogError($"Failed to start alive timer - player not found or null");
            }
        }

        public void StartAllAliveTimers()
        {
            foreach (var entry in activePlayers)
            {
                if (!entry.Value.CurrentAliveStartTime.HasValue)
                {
                    entry.Value.CurrentAliveStartTime = Time.unscaledTime;
                    entry.Value.TotalAliveTime = 0f;
                }
            }
        }

        public void StopAllAliveTimers()
        {
            foreach (var entry in activePlayers)
            {
                entry.Value.StopAliveTimer();
            }
        }

        public void StopAllWebSwingTimers()
        {
            foreach (var entry in activePlayers)
            {
                entry.Value.StopWebSwingTimer();
            }
        }

        public void StopAllAirborneTimers()
        {
            foreach (var entry in activePlayers)
            {
                entry.Value.StopAirborneTimer();
            }
        }

        public void PauseTimers()
        {
            if (isPaused)
                return;

            isPaused = true;
            foreach (var entry in activePlayers)
            {
                if (entry.Value.CurrentAliveStartTime.HasValue)
                {
                    float aliveSession = Time.unscaledTime - entry.Value.CurrentAliveStartTime.Value;
                    entry.Value.TotalAliveTime += aliveSession;
                    entry.Value.CurrentAliveStartTime = null;
                    entry.Value.WasAliveWhenPaused = true;
                }
                else
                {
                    entry.Value.WasAliveWhenPaused = false;
                }

                if (entry.Value.CurrentWebSwingStartTime.HasValue)
                {
                    float swingSession = Time.unscaledTime - entry.Value.CurrentWebSwingStartTime.Value;
                    entry.Value.WebSwingTime += swingSession;
                    entry.Value.CurrentWebSwingStartTime = null;
                    entry.Value.WasSwingingWhenPaused = true;
                }
                else
                {
                    entry.Value.WasSwingingWhenPaused = false;
                }

                if (entry.Value.CurrentAirborneStartTime.HasValue)
                {
                    float airborneSession = Time.unscaledTime - entry.Value.CurrentAirborneStartTime.Value;
                    entry.Value.AirborneTime += airborneSession;
                    entry.Value.CurrentAirborneStartTime = null;
                    entry.Value.WasAirborneWhenPaused = true;
                }
                else
                {
                    entry.Value.WasAirborneWhenPaused = false;
                }
            }
        }

        // A session that stops while paused never sees the unpause, so the next one must not start paused
        public void ClearPause()
        {
            isPaused = false;
            foreach (var entry in activePlayers)
            {
                entry.Value.WasAliveWhenPaused = false;
                entry.Value.WasSwingingWhenPaused = false;
                entry.Value.WasAirborneWhenPaused = false;
            }
        }

        public void ResumeTimers()
        {
            if (!isPaused)
                return;

            isPaused = false;
            foreach (var entry in activePlayers)
            {
                if (entry.Value.WasAliveWhenPaused)
                {
                    entry.Value.CurrentAliveStartTime = Time.unscaledTime;
                    entry.Value.WasAliveWhenPaused = false;
                }

                if (entry.Value.WasSwingingWhenPaused)
                {
                    entry.Value.CurrentWebSwingStartTime = Time.unscaledTime;
                    entry.Value.WasSwingingWhenPaused = false;
                }

                if (entry.Value.WasAirborneWhenPaused)
                {
                    entry.Value.CurrentAirborneStartTime = Time.unscaledTime;
                    entry.Value.WasAirborneWhenPaused = false;
                }
            }
        }


        public void RecordPlayerRespawn(PlayerController playerController)
        {
            if (playerController == null)
            {
                Logger.LogWarning("RecordPlayerRespawn called with null playerController");
                return;
            }

            try
            {
                StartAliveTimer(playerController);
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error recording player respawn: {ex.Message}");
            }
        }
    }
}
