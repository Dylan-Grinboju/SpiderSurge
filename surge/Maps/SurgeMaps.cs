using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using HarmonyLib;
using I2.Loc;
using SpiderSurge.Common;
using UnityEngine;
using Logger = Silk.Logger;

namespace SpiderSurge.Maps
{
    [DataContract]
    public class MapPart
    {
        [DataMember] public string o;
        [DataMember] public float x;
        [DataMember] public float y;
        [DataMember] public float r;
        [DataMember] public float sx;
        [DataMember] public float sy;
    }

    // An element and the parts the game links to it, such as a portal's exit
    [DataContract]
    public class MapElement : MapPart
    {
        [DataMember] public List<MapPart> with;
    }

    // A base-game level gadget, placed by its prefab root at a uniform scale
    [DataContract]
    public class MapGadget
    {
        [DataMember] public string o;
        [DataMember] public float x;
        [DataMember] public float y;
        [DataMember] public float r;
        [DataMember] public float s;
        [DataMember] public float motor;
        [DataMember] public int phase;
    }

    [DataContract]
    public class MapPoint
    {
        [DataMember] public float x;
        [DataMember] public float y;
    }

    [DataContract]
    public class MapRect
    {
        [DataMember] public float xmin;
        [DataMember] public float xmax;
        [DataMember] public float ymin;
        [DataMember] public float ymax;
    }

    [DataContract]
    public class SurgeMap
    {
        [DataMember] public string id;
        [DataMember] public string name;
        [DataMember] public int background;
        [DataMember] public int lava;
        [DataMember] public bool beginner;
        [DataMember] public bool zeroGravity;
        [DataMember] public List<MapElement> elements;
        [DataMember] public List<MapGadget> parts;
        [DataMember] public List<MapPoint> enemies;
        [DataMember] public MapRect camera;

        public string json;
        public GameLevel level;

        public string Path => SurgeMaps.PathPrefix + id;
        public string Term => "SpiderSurge/Maps/" + id;
    }

    // Surge-only maps built from the game's own custom map elements, so they load the same way on every build.
    // The host picks them like any survival map; the custom map scene is loaded and filled from embedded layouts.
    public static class SurgeMaps
    {
        public const string PathPrefix = "SpiderSurge/";
        public const string SceneName = "MapEditorBase";
        private const string Channel = "surge.map";
        private const string SpawnRootName = "SurgeEnemySpawns";

        private static readonly List<SurgeMap> _maps = new List<SurgeMap>();
        private static readonly Dictionary<string, SurgeMap> _byId = new Dictionary<string, SurgeMap>();
        private static readonly HashSet<GameLevel> _levels = new HashSet<GameLevel>();
        private static bool _loaded;
        private static bool _termsAdded;

        private static readonly AccessTools.FieldRef<EnemySpawner, GameObject[]> EnemySpawns =
            AccessTools.FieldRefAccess<EnemySpawner, GameObject[]>("_enemySpawns");
        private static readonly AccessTools.FieldRef<CustomMapEditor, BoxCollider2D> CameraConfiner =
            AccessTools.FieldRefAccess<CustomMapEditor, BoxCollider2D>("cameraConfiner");

        // The map the host is playing or about to load; clients learn it from the host
        public static SurgeMap Current { get; private set; }

        public static IReadOnlyList<SurgeMap> All
        {
            get
            {
                Load();
                return _maps;
            }
        }

        public static bool IsSurgeLevel(GameLevel level) => level != null && _levels.Contains(level);

        public static SurgeMap Of(GameLevel level) => IsSurgeLevel(level) ? _maps.FirstOrDefault(map => map.level == level) : null;

        public static SurgeMap ByPath(string path)
        {
            if (string.IsNullOrEmpty(path) || !path.StartsWith(PathPrefix, StringComparison.Ordinal)) return null;
            Load();
            _byId.TryGetValue(path.Substring(PathPrefix.Length), out SurgeMap map);
            return map;
        }

        // A name for stats and telemetry that tells the Surge maps apart, since they all share one scene
        public static string MapKey(string sceneName) =>
            sceneName == SceneName && Current != null ? "Surge " + Current.name : sceneName;

        public static void Initialize()
        {
            Load();
            Net.RegisterFromHost(Channel, OnMapFromHost);
            Net.PeerReady += OnPeerReady;
        }

        public static void Shutdown()
        {
            Net.Unregister(Channel);
            Net.PeerReady -= OnPeerReady;
        }

        private static void Load()
        {
            if (_loaded) return;
            _loaded = true;
            EmbeddedResources.Load(".json", "SurgeMaps", "map", nameof(DataContractJsonSerializer), (name, data) =>
            {
                SurgeMap map;
                using (var stream = new MemoryStream(data)) map = (SurgeMap)new DataContractJsonSerializer(typeof(SurgeMap)).ReadObject(stream);
                if (map == null || string.IsNullOrEmpty(map.id) || map.elements == null || map.enemies == null || _byId.ContainsKey(map.id))
                {
                    Logger.LogWarning($"[SurgeMaps] Skipped map resource '{name}'");
                    return;
                }
                map.json = BuildGameJson(map);
                _maps.Add(map);
                _byId[map.id] = map;
            });
            // The host and its clients add the levels in one order, since perk cards send map indices
            _maps.Sort((a, b) => string.CompareOrdinal(a.id, b.id));
            Logger.LogInfo($"[SurgeMaps] Loaded {_maps.Count} Surge maps");
        }

        // Adds the Surge maps to the default survival config once, so perk cards can name them by index on every machine
        public static void Register(SurvivalMode mode)
        {
            Load();
            AddTerms();
            if (mode == null || mode._defaultConfig == null || mode._defaultConfig.maps == null) return;
            List<GameLevel> maps = mode._defaultConfig.maps;
            foreach (SurgeMap map in _maps)
            {
                if (map.level == null) map.level = CreateLevel(map);
                if (!maps.Contains(map.level)) maps.Add(map.level);
            }
        }

        // The vanilla list without the Surge maps, plus them during a Surge run
        public static List<GameLevel> Filter(List<GameLevel> levels)
        {
            if (levels == null) return null;
            bool surge = SurgeGameModeManager.IsSurgeRunActive;
            var result = new List<GameLevel>(levels.Count + _maps.Count);
            foreach (GameLevel level in levels)
            {
                if (!IsSurgeLevel(level)) result.Add(level);
            }
            if (surge)
            {
                foreach (SurgeMap map in _maps)
                {
                    if (map.level != null) result.Add(map.level);
                }
            }
            return result;
        }

        private static GameLevel CreateLevel(SurgeMap map)
        {
            IconLoader.LoadAllIcons();
            var level = ScriptableObject.CreateInstance<GameLevel>();
            // The level's name is the scene LevelController loads
            level.name = SceneName;
            level.label = map.Term;
            level.AuthorName = "thmrgnd";
            level.serializationMapName = SerializationMapMame.NonPlayable;
            level.beginnerFriendly = map.beginner;
            // The game sets gravity from the active level, which every peer resolves to the current Surge map
            level.zeroGravity = map.zeroGravity;
            level.thumbnail = IconLoader.GetIcon("map_" + map.id) ?? LevelController.instance?.customMapGameLevel?.thumbnail;
            level.hideFlags = HideFlags.DontUnloadUnusedAsset;
            _levels.Add(level);
            return level;
        }

        // Map names are proper names, so every language shows the English one
        private static void AddTerms()
        {
            if (_termsAdded || LocalizationManager.Sources == null || LocalizationManager.Sources.Count == 0) return;
            LanguageSourceData source = LocalizationManager.Sources[0];
            foreach (SurgeMap map in _maps)
            {
                TermData term = source.AddTerm(map.Term);
                for (int i = 0; i < term.Languages.Length; i++) term.Languages[i] = map.name;
            }
            source.UpdateDictionary(true);
            _termsAdded = true;
        }

        // The host chose the next level: point the custom map scene at a Surge map, or clear it for any other level
        public static void OnLevelChosen(LevelController controller, GameLevel level, ref string nameOverride)
        {
            if (!Net.IsAuthority) return;
            SurgeMap map = Of(level);
            Current = map;
            Broadcast();
            if (map == null) return;
            CustomMapEditor.editorState = EditorState.Playing;
            controller.activeCustomMapPath = map.Path;
            controller.customMapName = map.name;
            VersusMode.CustomMapString = map.json;
            if (string.IsNullOrEmpty(nameOverride)) nameOverride = map.name;
        }

        // A client that leaves its host forgets the map; the next host sends its own
        public static void Forget() => Current = null;

        public static void EndRun()
        {
            if (Current == null) return;
            Current = null;
            Broadcast();
        }

        private static void Broadcast() => Net.SendToAll(Channel, WriteCurrent);

        private static void WriteCurrent(BinaryWriter writer) => writer.Write(Current?.id ?? "");

        private static void OnPeerReady(ulong client)
        {
            if (Current != null) Net.SendTo(client, Channel, WriteCurrent);
        }

        // A client reads its copy of the same layout, so the next custom map scene builds it without asking the host
        private static void OnMapFromHost(BinaryReader reader)
        {
            string id = reader.ReadString();
            Load();
            _byId.TryGetValue(id, out SurgeMap map);
            Current = map;
            if (map == null) return;
            CustomMapEditor.editorState = EditorState.Playing;
            VersusMode.CustomMapString = map.json;
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name == SceneName)
            {
                // A joiner's scene loaded before the host identified the map; refresh the level that controls gravity.
                if (LevelController.instance != null) LevelController.instance.activeLevel = map.level;
                BuildOnClient(CustomMapEditor.instance);
            }
        }

        public static void BuildOnClient(CustomMapEditor editor)
        {
            if (Net.IsAuthority || Current == null || editor == null || editor.objParent == null) return;
            if (CustomMapEditor.editorState != EditorState.Playing || editor.objParent.childCount > 0) return;
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != SceneName) return;
            editor.LoadMap(Current.Path);
        }

        // Host and clients: the custom map scene's camera box spans its whole build area, so it is fitted to the map's own.
        // The editor's Start resets the box, and the host builds the map before that, so both call this.
        public static void FitCamera(CustomMapEditor editor)
        {
            SurgeMap map = Current;
            if (map?.camera == null || editor == null || CustomMapEditor.editorState != EditorState.Playing) return;
            if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != SceneName) return;
            BoxCollider2D confiner = CameraConfiner(editor);
            if (confiner == null) return;
            MapRect box = map.camera;
            confiner.transform.position = new Vector3((box.xmin + box.xmax) / 2f, (box.ymin + box.ymax) / 2f, confiner.transform.position.z);
            confiner.offset = Vector2.zero;
            confiner.size = new Vector2(box.xmax - box.xmin, box.ymax - box.ymin);
            Physics2D.SyncTransforms();
        }

        // Host only: the enemy spawn points and gadgets the custom map elements don't have, and a pathfinding graph that
        // knows the new walls
        public static void OnMapBuilt(SerializedMap serialized)
        {
            SurgeMap map = Current;
            if (!Net.IsAuthority || map == null || serialized == null || serialized.mapName != map.name) return;

            GameObject old = GameObject.Find(SpawnRootName);
            if (old != null) UnityEngine.Object.DestroyImmediate(old);
            var root = new GameObject(SpawnRootName);
            var spawns = new List<GameObject>();
            for (int i = 0; i < map.enemies.Count; i++)
            {
                var spawn = new GameObject("EnemySpawn " + i) { tag = "EnemySpawn" };
                spawn.transform.SetParent(root.transform, false);
                spawn.transform.position = new Vector3(map.enemies[i].x, map.enemies[i].y, 0f);
                spawns.Add(spawn);
            }
            EnemySpawner spawner = EnemySpawner.instance;
            if (spawner != null)
            {
                EnemySpawns(spawner) = spawns.ToArray();
                spawner.RandomizeSpawns();
            }
            SurgeParts.Build(map);
            FitLava(map);
            Physics2D.SyncTransforms();
            Rescan(map);
        }

        // The scene's lava on every side starts far outside a Surge map's camera box. As on the base-game zero-gravity
        // arenas, the host moves each wall in to LavaGap past the box; its network transform carries the move to clients.
        private const int LavaAllSides = 3;
        private const float LavaGap = 30f; // layouts.py LAVA_GAP

        private static void FitLava(SurgeMap map)
        {
            GameObject[] sets = CustomMapEditor.instance?.deathZones;
            if (map.lava != LavaAllSides || map.camera == null || sets == null || map.lava >= sets.Length || sets[map.lava] == null) return;
            MapRect box = map.camera;
            var corners = new[]
            {
                new Vector2(box.xmin - LavaGap, box.ymin - LavaGap), new Vector2(box.xmax + LavaGap, box.ymin - LavaGap),
                new Vector2(box.xmin - LavaGap, box.ymax + LavaGap), new Vector2(box.xmax + LavaGap, box.ymax + LavaGap),
            };
            foreach (DeathZone zone in sets[map.lava].GetComponentsInChildren<DeathZone>(true))
            {
                // Each wall faces the arena along its up direction; its surface is its colliders' furthest point that way
                Transform wall = zone.transform;
                Vector2 up = wall.up;
                float surface = float.MinValue;
                foreach (PolygonCollider2D collider in zone.GetComponents<PolygonCollider2D>())
                {
                    if (!collider.enabled) continue;
                    foreach (Vector2 point in collider.points)
                    {
                        surface = Mathf.Max(surface, Vector2.Dot(up, wall.TransformPoint(point + collider.offset)));
                    }
                }
                if (surface == float.MinValue) continue;
                float target = corners.Min(corner => Vector2.Dot(up, corner));
                wall.position += (Vector3)(up * (target - surface));
            }
        }

        // The custom map scene's grid covers only part of its play area, so it is fitted to the map before the scan
        private const float GraphPadding = 80f;
        private const float GraphNodeSize = 6f;
        private static MethodInfo _scan;
        private static FieldInfo _active;

        private static void Rescan(SurgeMap map)
        {
            try
            {
                Type astar = AccessTools.TypeByName("AstarPath");
                if (_active == null)
                {
                    _active = astar?.GetField("active", BindingFlags.Public | BindingFlags.Static);
                    _scan = astar?.GetMethods().FirstOrDefault(m => m.Name == "Scan" && m.GetParameters().Length == 1
                        && m.GetParameters()[0].ParameterType.IsArray);
                }
                object active = _active?.GetValue(null);
                if (active == null || _scan == null) return;
                Rect area = Area(map);
                object data = Traverse.Create(active).Field("data").GetValue();
                if (Traverse.Create(data).Field("graphs").GetValue() is Array graphs)
                {
                    foreach (object graph in graphs)
                    {
                        if (graph == null || graph.GetType().Name != "GridGraph") continue;
                        Traverse.Create(graph).Field("center").SetValue(new Vector3(area.center.x, area.center.y, 0f));
                        AccessTools.Method(graph.GetType(), "SetDimensions").Invoke(graph, new object[]
                        {
                            Mathf.CeilToInt(area.width / GraphNodeSize), Mathf.CeilToInt(area.height / GraphNodeSize), GraphNodeSize
                        });
                    }
                }
                _scan.Invoke(active, new object[] { null });
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"[SurgeMaps] Could not rescan the pathfinding graph: {ex.Message}");
            }
        }

        private static Rect Area(SurgeMap map)
        {
            float minX = float.MaxValue, minY = float.MaxValue, maxX = float.MinValue, maxY = float.MinValue;
            void Add(float x, float y)
            {
                minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x);
                minY = Mathf.Min(minY, y); maxY = Mathf.Max(maxY, y);
            }
            foreach (MapElement element in map.elements) Add(element.x, element.y);
            if (map.parts != null) foreach (MapGadget gadget in map.parts) Add(gadget.x, gadget.y);
            foreach (MapPoint point in map.enemies) Add(point.x, point.y);
            return Rect.MinMaxRect(minX - GraphPadding, minY - GraphPadding, maxX + GraphPadding, maxY + GraphPadding);
        }

        // The game's custom map format: the same JSON its map editor saves
        private static string BuildGameJson(SurgeMap map)
        {
            var json = new StringBuilder(4096);
            json.Append("{\"mapStatus\":0,\"mapType\":0,\"mapName\":").Append(Quote(map.name));
            json.Append(",\"mapDescription\":\"\",\"serializedMapElements\":[");
            for (int i = 0; i < map.elements.Count; i++)
            {
                if (i > 0) json.Append(',');
                AppendElement(json, map.elements[i]);
            }
            json.Append("],\"background\":").Append(map.background);
            json.Append(",\"deathZone\":").Append(map.lava);
            json.Append(",\"steamID\":0,\"useWalls\":false,\"playerHighScore\":0.0,\"creatorHighScore\":0.0,\"mods\":[],\"gameVersion\":\"\",\"userName\":\"SpiderSurge\"}");
            return json.ToString();
        }

        private static void AppendElement(StringBuilder json, MapPart element)
        {
            float half = element.r * Mathf.Deg2Rad / 2f;
            json.Append("{\"objName\":").Append(Quote(element.o));
            json.Append(",\"position\":{\"x\":").Append(F(element.x)).Append(",\"y\":").Append(F(element.y)).Append(",\"z\":0.0}");
            json.Append(",\"rotation\":{\"x\":0.0,\"y\":0.0,\"z\":").Append(F(Mathf.Sin(half))).Append(",\"w\":").Append(F(Mathf.Cos(half))).Append('}');
            json.Append(",\"localScale\":{\"x\":").Append(F(element.sx)).Append(",\"y\":").Append(F(element.sy)).Append(",\"z\":1.0}");
            json.Append(",\"associatedObjects\":[");
            if (element is MapElement linked && linked.with != null)
            {
                for (int i = 0; i < linked.with.Count; i++)
                {
                    if (i > 0) json.Append(',');
                    AppendElement(json, linked.with[i]);
                }
            }
            json.Append("]}");
        }

        private static string F(float value) => value.ToString("R", CultureInfo.InvariantCulture);

        private static string Quote(string text) => "\"" + (text ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
    }
}
