using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Netcode;
using Silk;
using HarmonyLib;
using System.Linq;
using System.Collections.Generic;
using Logger = Silk.Logger;

namespace SpiderSurge
{
    /// <summary>
    /// Manages cheat functionality for testing.
    /// F9 - Freeze/unfreeze enemy movement
    /// F4 - Give shield to all players
    /// F5 - Add one life (if possible)
    /// F6 - Respawn dead players
    /// F8 - Toggle god mode (invincibility, phase through walls, fly)
    /// \ - Freeze/Unfreeze enemy spawning
    /// = - Toggle spawn menus (weapons/enemies)
    /// - - Toggle modifiers menu (regular mods + surge perks)

    /// </summary>
    public class CheatManager : MonoBehaviour
    {
        public static CheatManager Instance { get; private set; }

        private bool _enemiesFrozen = false;
        private static bool _cheatsEnabled = true;
        private bool _godModeEnabled = false;
        private List<SpiderHealthSystem> _godModePlayers = new List<SpiderHealthSystem>();

        private static ElementLists _elements;
        private bool _spawningFrozen = false;
        private bool _showSpawnMenus = false;
        private bool _showModifiersMenu = false;
        private Vector2 _weaponScrollPos = Vector2.zero;
        private Vector2 _enemyScrollPos = Vector2.zero;
        private Vector2 _modifierScrollPos = Vector2.zero;
        private List<GameObject> _spawnedWeapons = new List<GameObject>();
        private List<GameObject> _spawnedEnemies = new List<GameObject>();

        // Surge perks for cheat menu
        private static readonly (string key, string title)[] _surgePerks = new (string key, string title)[]
        {
            ("shieldAbility", "Shield Ability"),
            ("infiniteAmmoAbility", "Infinite Ammo Ability"),
            ("explosionAbility", "Explosion Ability"),
            ("abilityCooldown", "Ability Cooldown"),
            ("abilityDuration", "Ability Duration"),
            // Ability upgrades (require base ability)
            ("shieldAbilityUpgrade", "Shield Immunity (Upgrade)"),
            ("infiniteAmmoAbilityUpgrade", "Weapon Arsenal (Upgrade)"),
            ("explosionAbilityUpgrade", "Deadly Explosion (Upgrade)")
        };

        public bool SpawningFrozen => _spawningFrozen;

        public static bool CheatsEnabled
        {
            get => _cheatsEnabled;
            set => _cheatsEnabled = value;
        }

        public bool EnemiesFrozen => _enemiesFrozen;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void Update()
        {
            if (!_cheatsEnabled)
                return;

            if (Keyboard.current == null)
                return;

            // F9 - Freeze/Unfreeze enemies
            if (Keyboard.current.f9Key.wasPressedThisFrame)
            {
                ToggleFreezeEnemies();
            }

            // F4 - Give shield to players
            if (Keyboard.current.f4Key.wasPressedThisFrame)
            {
                GiveShieldToPlayers();
            }

            // F5 - Add one life
            if (Keyboard.current.f5Key.wasPressedThisFrame)
            {
                AddLife();
            }

            // F6 - Respawn dead players
            if (Keyboard.current.f6Key.wasPressedThisFrame)
            {
                RespawnDeadPlayers();
            }
            // F8 - Toggle god mode
            if (Keyboard.current.f8Key.wasPressedThisFrame)
            {
                ToggleGodMode();
            }
            // \ - Freeze/Unfreeze enemy spawning
            if (Keyboard.current.backslashKey.wasPressedThisFrame)
            {
                ToggleSpawnFreeze();
            }

            // = - Toggle spawn menus (hides modifiers menu)
            if (Keyboard.current.equalsKey.wasPressedThisFrame)
            {
                _showModifiersMenu = false;
                _showSpawnMenus = !_showSpawnMenus;
            }

            // - - Toggle modifiers menu (hides spawn menus)
            if (Keyboard.current.minusKey.wasPressedThisFrame)
            {
                _showSpawnMenus = false;
                _showModifiersMenu = !_showModifiersMenu;
            }


        }

        public void ToggleFreezeEnemies()
        {
            _enemiesFrozen = !_enemiesFrozen;

            EnemyBrain[] enemies = FindObjectsOfType<EnemyBrain>();

            foreach (var enemy in enemies)
            {
                if (enemy != null)
                {
                    enemy.enabled = !_enemiesFrozen;

                    var rb = enemy.GetComponent<Rigidbody2D>();
                    if (rb != null)
                    {
                        if (_enemiesFrozen)
                        {
                            rb.velocity = Vector2.zero;
                            rb.angularVelocity = 0f;
                            rb.constraints = RigidbodyConstraints2D.FreezeAll;
                        }
                        else
                        {
                            rb.constraints = RigidbodyConstraints2D.None;
                        }
                    }
                }
            }

            string status = _enemiesFrozen ? "FROZEN" : "UNFROZEN";
        }

        public void GiveShieldToPlayers()
        {
            SpiderHealthSystem[] players = FindObjectsOfType<SpiderHealthSystem>();
            int shieldsGiven = 0;

            foreach (var player in players)
            {
                if (player != null && !player.HasShield())
                {
                    player.EnableShield();
                    shieldsGiven++;
                }
            }
        }



        public void AddLife()
        {
            if (SurvivalMode.instance == null)
            {
                return;
            }

            if (!SurvivalMode.instance.GameModeActive())
            {
                return;
            }

            int currentLives = SurvivalMode.instance.Lives;
            int maxLives = SurvivalMode.instance.MaxLives;

            // Use reflection to set the Lives property since the setter might be private
            try
            {
                var livesProperty = typeof(SurvivalMode).GetProperty("Lives");
                if (livesProperty != null && livesProperty.CanWrite)
                {
                    livesProperty.SetValue(SurvivalMode.instance, currentLives + 1);

                    if (Announcer.instance != null)
                    {
                        Announcer.instance.AnnounceHealth("+1");
                    }
                }
                else
                {
                    // Fallback: Try to access the private field directly
                    var livesField = typeof(SurvivalMode).GetField("_lives",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);

                    if (livesField != null)
                    {
                        var networkVar = livesField.GetValue(SurvivalMode.instance);
                        if (networkVar != null)
                        {
                            var valueProperty = networkVar.GetType().GetProperty("Value");
                            if (valueProperty != null)
                            {
                                valueProperty.SetValue(networkVar, currentLives + 1);

                            }
                        }
                    }
                }
            }
            catch (System.Exception ex)
            {
                Logger.LogError($"Failed to add life: {ex.Message}");
            }
        }

        public void RespawnDeadPlayers()
        {
            if (LobbyController.instance == null)
            {
                return;
            }

            var playerControllers = LobbyController.instance.GetPlayerControllers();
            if (playerControllers == null || !playerControllers.Any())
            {
                return;
            }

            var spawnPoints = LobbyController.instance.GetSpawnPoints();
            if (spawnPoints == null || spawnPoints.Length == 0)
            {
                return;
            }

            int respawnedCount = 0;
            foreach (var playerController in playerControllers)
            {
                if (playerController != null && !playerController.isAlive)
                {
                    var spawnPoint = spawnPoints[UnityEngine.Random.Range(0, spawnPoints.Length)];
                    playerController.SpawnCharacter(spawnPoint.position, spawnPoint.rotation);
                    respawnedCount++;
                }
            }
        }

        public void ToggleGodMode()
        {
            _godModeEnabled = !_godModeEnabled;

            SpiderHealthSystem[] players = FindObjectsOfType<SpiderHealthSystem>();
            _godModePlayers.Clear();

            foreach (var player in players)
            {
                if (player != null)
                {
                    _godModePlayers.Add(player);
                    ApplyGodMode(player, _godModeEnabled);
                }
            }
        }

        private void ApplyGodMode(SpiderHealthSystem player, bool enable)
        {
            if (player == null) return;

            var rb = player.GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                if (enable)
                {
                    rb.gravityScale = 0f;
                }
                else
                {
                    rb.gravityScale = 2f;
                }
            }

            var colliders = player.GetComponents<Collider2D>();
            foreach (var collider in colliders)
            {
                if (collider != null)
                {
                    collider.isTrigger = enable;
                }
            }

            if (enable)
            {
                try
                {
                    var immuneTimeField = typeof(SpiderHealthSystem).GetField("_immuneTime",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (immuneTimeField != null)
                    {
                        immuneTimeField.SetValue(player, float.MaxValue);
                    }
                }
                catch (System.Exception ex)
                {
                    Logger.LogError($"Failed to set immune time: {ex.Message}");
                }
            }
            else
            {
                try
                {
                    var immuneTimeField = typeof(SpiderHealthSystem).GetField("_immuneTime",
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (immuneTimeField != null)
                    {
                        immuneTimeField.SetValue(player, 0f);
                    }
                }
                catch (System.Exception ex)
                {
                    Logger.LogError($"Failed to reset immune time: {ex.Message}");
                }
            }
        }

        public void ToggleSpawnFreeze()
        {
            _spawningFrozen = !_spawningFrozen;

            if (_spawningFrozen && EnemySpawner.instance != null)
            {
                EnemySpawner.instance.StopAllCoroutines();
            }
        }

        private void SpawnWeapon(Weapon weapon)
        {
            if (weapon == null) return;

            var controllers = LobbyController.instance?.GetPlayerControllers();
            if (controllers == null || !controllers.Any()) return;

            var localPlayer = controllers.FirstOrDefault(c => c.isLocalPlayer);
            if (localPlayer?.spiderHealthSystem == null) return;

            Vector3 spawnPos = localPlayer.spiderHealthSystem.transform.position;

            var spawnable = new SpawnableWeapon(weapon, 1);
            var spawnedObj = Object.Instantiate(spawnable.weaponObject, spawnPos, Quaternion.identity);
            spawnedObj.SetActive(true);

            var netObj = spawnedObj.GetComponent<NetworkObject>();
            if (netObj != null)
            {
                netObj.Spawn();
                netObj.DestroyWithScene = true;
            }

            _spawnedWeapons.Add(spawnedObj);
        }

        private void SpawnEnemy(EnemyHealthSystem enemy)
        {
            if (enemy == null) return;

            GameObject[] spawnPoints = GameObject.FindGameObjectsWithTag("EnemySpawn");
            if (spawnPoints.Length == 0)
            {
                return;
            }

            Transform spawnPoint = spawnPoints[Random.Range(0, spawnPoints.Length)].transform;

            var spawnedObj = Object.Instantiate(enemy.gameObject, spawnPoint.position, spawnPoint.rotation);
            spawnedObj.SetActive(true);

            var netObj = spawnedObj.GetComponent<NetworkObject>();
            if (netObj != null)
            {
                netObj.Spawn();
                netObj.DestroyWithScene = true;
            }

            if (EnemySpawner.instance != null)
            {
                EnemySpawner.instance.spawnedEnemies.Add(spawnedObj);
            }

            _spawnedEnemies.Add(spawnedObj);
        }

        private void ClearSpawnedWeapons()
        {
            foreach (var weapon in _spawnedWeapons)
            {
                if (weapon != null)
                {
                    var netObj = weapon.GetComponent<NetworkObject>();
                    if (netObj != null && netObj.IsSpawned)
                        netObj.Despawn();
                    else
                        Destroy(weapon);
                }
            }
            _spawnedWeapons.Clear();
        }

        private void ClearSpawnedEnemies()
        {
            foreach (var enemy in _spawnedEnemies)
            {
                if (enemy != null)
                {
                    var netObj = enemy.GetComponent<NetworkObject>();
                    if (netObj != null && netObj.IsSpawned)
                        netObj.Despawn();
                    else
                        Destroy(enemy);
                }
            }
            _spawnedEnemies.Clear();
        }

        private void OnGUI()
        {
            if (!_cheatsEnabled) return;

            if (_spawningFrozen)
            {
                GUI.color = Color.red;
                GUI.Label(new Rect(Screen.width / 2 - 100, 10, 200, 30), "SPAWNING FROZEN",
                    new GUIStyle(GUI.skin.label) { fontSize = 20, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold });
                GUI.color = Color.white;
            }

            if (_showModifiersMenu && _elements != null)
            {
                DrawModifiersMenu();
                return;
            }

            if (!_showSpawnMenus || _elements == null) return;

            float menuWidth = 600f;
            float menuHeight = Screen.height - 100f;
            float startY = 50f;

            GUILayout.BeginArea(new Rect(10, startY, menuWidth, menuHeight));
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label($"WEAPONS ({_spawnedWeapons.Count} spawned)", new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold });
            if (GUILayout.Button("Clear Spawned Weapons")) ClearSpawnedWeapons();
            _weaponScrollPos = GUILayout.BeginScrollView(_weaponScrollPos);

            if (_elements.allWeapons != null)
            {
                foreach (var weapon in _elements.allWeapons)
                {
                    if (weapon != null && GUILayout.Button(weapon.name))
                        SpawnWeapon(weapon);
                }
            }

            GUILayout.EndScrollView();
            GUILayout.EndVertical();
            GUILayout.EndArea();

            GUILayout.BeginArea(new Rect(620, startY, menuWidth, menuHeight));
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label($"ENEMIES ({_spawnedEnemies.Count} spawned)", new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold });
            if (GUILayout.Button("Clear Spawned Enemies")) ClearSpawnedEnemies();
            _enemyScrollPos = GUILayout.BeginScrollView(_enemyScrollPos);

            if (_elements.allEnemies != null)
            {
                foreach (var enemy in _elements.allEnemies)
                {
                    if (enemy != null && GUILayout.Button(enemy.name))
                        SpawnEnemy(enemy);
                }
            }

            GUILayout.EndScrollView();
            GUILayout.EndVertical();
            GUILayout.EndArea();
        }

        private void DrawModifiersMenu()
        {
            if (!_showModifiersMenu || _elements == null) return;

            float menuWidth = 1210f;
            float menuHeight = Screen.height - 100f;
            float startY = 50f;

            GUILayout.BeginArea(new Rect(10, startY, menuWidth, menuHeight));
            GUILayout.BeginVertical(GUI.skin.box);
            GUILayout.Label("MODIFIERS", new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold });
            if (GUILayout.Button("Reset All Modifiers")) ResetAllModifiers();
            _modifierScrollPos = GUILayout.BeginScrollView(_modifierScrollPos);

            // Regular modifiers
            if (_elements.allModifiers != null)
            {
                foreach (var modifierData in _elements.allModifiers)
                {
                    if (modifierData == null) continue;

                    int currentLevel = ModifierManager.instance != null
                        ? ModifierManager.instance.GetModLevel(modifierData.key)
                        : 0;

                    string levelSuffix = currentLevel == 2 ? " ++" : currentLevel == 1 ? " +" : "";
                    string buttonText = $"{modifierData.key}{levelSuffix}";

                    Color originalColor = GUI.backgroundColor;
                    if (currentLevel == 2)
                        GUI.backgroundColor = new Color(1f, 0.4f, 1f);
                    else if (currentLevel == 1)
                        GUI.backgroundColor = Color.green;

                    if (GUILayout.Button(buttonText))
                        ToggleModifier(modifierData);

                    GUI.backgroundColor = originalColor;
                }
            }

            // Surge perks section
            GUILayout.Label("SURGE PERKS", new GUIStyle(GUI.skin.label) { fontSize = 14, fontStyle = FontStyle.Bold });
            if (GUILayout.Button("Reset All Surge Perks")) ResetAllSurgePerks();

            foreach (var (key, title) in _surgePerks)
            {
                int currentLevel = SurgeGameModeManager.Instance != null
                    ? PerksManager.Instance.GetPerkLevel(key)
                    : 0;

                string levelSuffix = currentLevel == 2 ? " ++" : currentLevel == 1 ? " +" : "";
                string buttonText = $"{title}{levelSuffix}";

                Color originalColor = GUI.backgroundColor;
                if (currentLevel == 2)
                    GUI.backgroundColor = new Color(1f, 0.4f, 1f); // Purple for level 2
                else if (currentLevel == 1)
                    GUI.backgroundColor = Color.cyan;

                if (GUILayout.Button(buttonText))
                    ToggleSurgePerk(key);

                GUI.backgroundColor = originalColor;
            }

            GUILayout.EndScrollView();
            GUILayout.EndVertical();
            GUILayout.EndArea();
        }

        private void ToggleModifier(ModifierData modifierData)
        {
            if (ModifierManager.instance == null || modifierData == null) return;

            int currentLevel = ModifierManager.instance.GetModLevel(modifierData.key);
            int nextLevel;

            if (currentLevel == 0)
                nextLevel = 1;
            else if (currentLevel == 1 && modifierData.maxLevel >= 2)
                nextLevel = 2;
            else
                nextLevel = 0;

            ModifierManager.instance.SetModSurvivalLevel(modifierData.key, nextLevel);
        }

        private void ResetAllModifiers()
        {
            if (ModifierManager.instance == null) return;
            ModifierManager.instance.ResetAllSurvivalModifiers();
        }

        private void ToggleSurgePerk(string key)
        {
            if (SurgeGameModeManager.Instance == null) return;

            int currentLevel = PerksManager.Instance.GetPerkLevel(key);
            int maxLevel = GetMaxLevelForPerk(key);
            int nextLevel;

            if (currentLevel == 0)
                nextLevel = 1;
            else if (currentLevel == 1 && maxLevel >= 2)
                nextLevel = 2;
            else
                nextLevel = 0;

            PerksManager.Instance.SetPerkLevel(key, nextLevel);

            // Apply perk effects for abilities
            if (nextLevel > 0)
            {
                if (key == "shieldAbility")
                {
                    PerksManager.EnableShieldAbility();
                }
                else if (key == "infiniteAmmoAbility")
                {
                    PerksManager.EnableInfiniteAmmoAbility();
                }
                else if (key == "explosionAbility")
                {
                    PerksManager.EnableExplosionAbility();
                }

                // Enable upgrade perk when ability reaches level 2
                if (nextLevel == 2)
                {
                    string upgradePerkKey = key + "Upgrade";
                    PerksManager.Instance.SetPerkLevel(upgradePerkKey, 1);
                }
            }
            else
            {
                // When resetting ability to 0, also reset its upgrade
                string upgradePerkKey = key + "Upgrade";
                PerksManager.Instance.SetPerkLevel(upgradePerkKey, 0);
            }
        }

        private int GetMaxLevelForPerk(string key)
        {
            return PerksManager.Instance.GetMaxLevel(key);
        }

        private void ResetAllSurgePerks()
        {
            if (SurgeGameModeManager.Instance == null) return;

            foreach (var (key, _) in _surgePerks)
            {
                PerksManager.Instance.SetPerkLevel(key, 0);
            }
        }

        public static void Initialize()
        {
            if (Instance != null)
            {
                return;
            }

            var go = new GameObject("CheatsModCheatManager");
            go.AddComponent<CheatManager>();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public static void SetElementLists(ElementLists elements)
        {
            _elements = elements;
        }
    }

    [HarmonyPatch(typeof(CustomTiersScreen), nameof(CustomTiersScreen.Start))]
    public static class CustomTiersScreenPatch
    {
        public static void Postfix(CustomTiersScreen __instance)
        {
            if (__instance.allElements != null)
            {
                CheatManager.SetElementLists(__instance.allElements);
            }
        }
    }

    [HarmonyPatch(typeof(EnemySpawner), nameof(EnemySpawner.SpawnEnemy))]
    public static class EnemySpawnerPatch
    {
        public static bool Prefix()
        {
            if (CheatManager.Instance != null && CheatManager.Instance.SpawningFrozen)
            {
                return false;
            }
            return true;
        }
    }
}