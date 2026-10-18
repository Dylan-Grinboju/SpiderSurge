using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Silk;
using UnityEngine;
using UnityEngine.InputSystem;
using Logger = Silk.Logger;

namespace StatsMod
{
    public class PlayerTracker
    {
        private static readonly Lazy<PlayerTracker> _lazy = new Lazy<PlayerTracker>(() => new PlayerTracker());
        public static PlayerTracker Instance => _lazy.Value;

        private Dictionary<PlayerInput, PlayerData> activePlayers = new Dictionary<PlayerInput, PlayerData>();

        private Dictionary<ulong, PlayerInput> playerIds = new Dictionary<ulong, PlayerInput>();

        private ulong nextLocalPlayerId = 0;

        // Cache for expensive player lookups
        private static PlayerController[] cachedPlayerControllers;
        private static float lastPlayerCacheUpdate = 0f;
        private static float playerCacheRefreshInterval = 60f; // Refresh every 60 seconds
        private static readonly object playerCacheLock = new object();

        private bool isPaused = false;

        // Raised with +1 when a tracked player's death is recorded and -1 when one is undone
        public event Action<int> DeathCountChanged;

        public class PlayerData
        {
            public ulong PlayerId { get; set; }
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

            public PlayerData(ulong id, string name = "Player")
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

            public bool IsAlive => CurrentAliveStartTime.HasValue || WasAliveWhenPaused;

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


        public void RegisterPlayer(PlayerInput player)
        {
            if (player == null) return;

            if (activePlayers.ContainsKey(player))
            {
                return;
            }

            ulong playerId = nextLocalPlayerId++;

            string playerName = $"Player {player.playerIndex + 1}";
            PlayerData playerData = new PlayerData(playerId, playerName);

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
            playerIds[playerId] = player;

            RefreshPlayerCache();
        }


        public void UnregisterPlayer(PlayerInput player)
        {
            if (player == null) return;

            if (activePlayers.TryGetValue(player, out PlayerData playerData))
            {
                playerIds.Remove(playerData.PlayerId);
                activePlayers.Remove(player);

                RefreshPlayerCache();

                StabilizerFixedUpdatePatch.ClearCache();
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
                PlayerInput playerInput = null;
                if (spiderHealth.rootObject != null)
                {
                    playerInput = spiderHealth.rootObject.GetComponentInParent<PlayerInput>();
                }
                else
                {
                    Logger.LogWarning("SpiderHealthSystem has null rootObject");
                }

                if (playerInput != null && activePlayers.TryGetValue(playerInput, out PlayerData data))
                {
                    data.Deaths++;
                    DeathCountChanged?.Invoke(1);
                    data.KillStreak = 0;
                    data.KillStreakWhileSolo = 0;
                    data.StopAliveTimer();
                    StopWebSwingTimer(playerInput);

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
                PlayerInput playerInput = null;
                if (spiderHealth.rootObject != null)
                {
                    playerInput = spiderHealth.rootObject.GetComponentInParent<PlayerInput>();
                }

                if (playerInput != null && activePlayers.TryGetValue(playerInput, out PlayerData data))
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

        public Dictionary<PlayerInput, PlayerData> GetActivePlayers()
        {
            return new Dictionary<PlayerInput, PlayerData>(activePlayers);
        }

        internal bool TryGetPlayer(PlayerInput player, out PlayerData data) => activePlayers.TryGetValue(player, out data);

        public bool IsOnlyOnePlayerAlive()
        {
            int aliveCount = activePlayers.Count(p => p.Value.IsAlive);
            return aliveCount == 1;
        }

        public PlayerInput GetOnlyAlivePlayer()
        {
            if (!IsOnlyOnePlayerAlive())
                return null;

            return activePlayers.FirstOrDefault(p => p.Value.IsAlive).Key;
        }

        private bool HasLivingTeammate(PlayerInput player)
        {
            return activePlayers.Any(p => p.Value.IsAlive && PlayerRelations.AreTeammates(player, p.Key));
        }

        public void IncrementPlayerKill(PlayerInput player)
        {
            if (player != null && activePlayers.TryGetValue(player, out PlayerData data))
            {
                data.Kills++;

                // A kill credited after the killer died (the target reached lava or blew up later) starts no streak
                if (!data.IsAlive) return;

                data.KillStreak++;
                if (data.KillStreak > data.MaxKillStreak)
                {
                    data.MaxKillStreak = data.KillStreak;
                }

                SpiderController spider = player.GetComponentInChildren<SpiderController>();
                if (spider != null)
                {
                    Stabilizer stabilizer = spider.GetComponentInChildren<Stabilizer>();
                    if (stabilizer != null && !stabilizer.grounded)
                    {
                        data.KillsWhileAirborne++;
                    }
                }

                if (!HasLivingTeammate(player))
                {
                    data.KillsWhileSolo++;
                    data.KillStreakWhileSolo++;
                    if (data.KillStreakWhileSolo > data.MaxKillStreakWhileSolo)
                    {
                        data.MaxKillStreakWhileSolo = data.KillStreakWhileSolo;
                    }
                }
            }
        }

        public void IncrementFriendlyKill(PlayerInput player)
        {
            if (player != null && activePlayers.TryGetValue(player, out PlayerData data))
            {
                data.FriendlyKills++;
            }
        }

        public void IncrementWaveClutch(PlayerInput player)
        {
            if (player != null && activePlayers.TryGetValue(player, out PlayerData data))
            {
                data.WaveClutches++;
            }
        }

        public void IncrementEnemyShieldsTakenDown(PlayerInput player)
        {
            if (player != null && activePlayers.TryGetValue(player, out PlayerData data))
            {
                data.EnemyShieldsTakenDown++;
            }
        }

        public void IncrementFriendlyShieldsHit(PlayerInput player)
        {
            if (player != null && activePlayers.TryGetValue(player, out PlayerData data))
            {
                data.FriendlyShieldsHit++;
            }
        }

        public void IncrementShieldsLost(PlayerInput player)
        {
            if (player != null && activePlayers.TryGetValue(player, out PlayerData data))
            {
                data.ShieldsLost++;
            }
        }

        public void IncrementLavaDeaths(PlayerInput player)
        {
            if (player != null && activePlayers.TryGetValue(player, out PlayerData data))
            {
                data.LavaDeaths++;
            }
        }

        public void IncrementWeaponHit(PlayerInput player, string weaponName)
        {
            if (player != null && activePlayers.TryGetValue(player, out PlayerData data))
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

        public void IncrementEnemyKillByName(PlayerInput player, string enemyName)
        {
            if (player != null && activePlayers.TryGetValue(player, out PlayerData data))
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

        public void IncrementWebSwings(PlayerInput player)
        {
            if (player != null && activePlayers.TryGetValue(player, out PlayerData data))
            {
                data.WebSwings++;
            }
        }

        public void StartWebSwingTimer(PlayerInput player)
        {
            if (player != null && activePlayers.TryGetValue(player, out PlayerData data))
            {
                if (data.CurrentWebSwingStartTime.HasValue)
                {
                    return;
                }
                data.CurrentWebSwingStartTime = Time.unscaledTime;
            }
        }

        public void StopWebSwingTimer(PlayerInput player)
        {
            if (player != null && activePlayers.TryGetValue(player, out PlayerData data))
            {
                data.StopWebSwingTimer();
            }
        }

        public void StartAirborneTimer(PlayerInput player)
        {
            if (player != null && activePlayers.TryGetValue(player, out PlayerData data))
            {
                if (data.CurrentAirborneStartTime.HasValue)
                {
                    return;
                }
                data.CurrentAirborneStartTime = Time.unscaledTime;
            }
        }

        public void StopAirborneTimer(PlayerInput player)
        {
            if (player != null && activePlayers.TryGetValue(player, out PlayerData data))
            {
                data.StopAirborneTimer();
            }
        }

        public void UpdatePlayerColor(PlayerInput player, Color color)
        {
            if (player != null && activePlayers.TryGetValue(player, out PlayerData data))
            {
                data.PlayerColor = color;
            }
        }

        public void UpdateHighestPoint(PlayerInput player, SpiderController spider)
        {
            if (player != null && spider != null && activePlayers.TryGetValue(player, out PlayerData data))
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

        public static PlayerInput FindPlayerInputByPlayerId(ulong playerId)
        {
            PlayerController[] playerControllers = GetCachedPlayerControllers();

            foreach (PlayerController controller in playerControllers)
            {
                if (controller != null && (ulong)controller.playerID.Value == playerId)
                {
                    PlayerInput playerInput = controller.GetComponentInParent<PlayerInput>();
                    if (playerInput != null)
                    {
                        return playerInput;
                    }
                }
            }

            return null;
        }


        public static void RefreshPlayerCache()
        {
            lock (playerCacheLock)
            {
                cachedPlayerControllers = null;
                lastPlayerCacheUpdate = 0f;
            }
        }

        public void StartAliveTimer(PlayerInput playerInput)
        {
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
                PlayerInput playerInput = playerController.GetComponentInParent<PlayerInput>();
                if (playerInput != null)
                {
                    StartAliveTimer(playerInput);
                }
                else
                {
                    Logger.LogError($"Could not find PlayerInput for respawn event");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error recording player respawn: {ex.Message}");
            }
        }
    }
}
