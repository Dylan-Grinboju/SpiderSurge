using System.Collections.Generic;
using HarmonyLib;
using SpiderSurge.Common;
using Unity.Netcode;
using UnityEngine;
using Logger = Silk.Logger;

namespace SpiderSurge.Maps
{
    // The base game's networked level gadgets a Surge layout places: boxes, spinners, swings, rails and cages.
    // The host spawns and configures them; their own network components carry scale, motion and joints to clients.
    public static class SurgeParts
    {
        // A loose body this far outside the camera box is lost, and comes back at its place after the delay
        private const float LostMargin = 60f;
        private const float RespawnDelay = 3f;
        private const float CheckInterval = 0.5f;

        private static readonly AccessTools.FieldRef<GameController, List<GameObject>> Prefabs =
            AccessTools.FieldRefAccess<GameController, List<GameObject>>("Prefabs");
        private static readonly AccessTools.FieldRef<PlatformDescender, bool> CageRising =
            AccessTools.FieldRefAccess<PlatformDescender, bool>("isUp");

        private sealed class Placed
        {
            public MapGadget gadget;
            public NetworkObject net;
            public Rigidbody2D[] bodies;
            public float respawnAt = -1f;
        }

        private static readonly List<Placed> _placed = new List<Placed>();
        private static SurgeMap _map;
        private static Keeper _keeper;

        // Host only, once the map's elements are built
        public static void Build(SurgeMap map)
        {
            Clear();
            if (!Net.IsAuthority || map?.parts == null || map.parts.Count == 0) return;
            _map = map;
            foreach (MapGadget gadget in map.parts)
            {
                var placed = new Placed { gadget = gadget };
                if (Spawn(placed)) _placed.Add(placed);
            }
            _keeper = new GameObject("SurgeParts").AddComponent<Keeper>();
        }

        private static void Clear()
        {
            foreach (Placed placed in _placed) Despawn(placed);
            _placed.Clear();
            _map = null;
            if (_keeper != null) Object.Destroy(_keeper.gameObject);
            _keeper = null;
        }

        private static bool Spawn(Placed placed)
        {
            MapGadget g = placed.gadget;
            GameObject prefab = FindPrefab(g.o);
            if (prefab == null)
            {
                Logger.LogWarning($"[SurgeParts] No network prefab named '{g.o}'");
                return false;
            }
            GameObject go = Object.Instantiate(prefab, new Vector3(g.x, g.y, 0f), Quaternion.Euler(0f, 0f, g.r));
            Vector3 scale = prefab.transform.localScale;
            go.transform.localScale = new Vector3(g.s, g.s, scale.z);
            Configure(go, g, g.s / scale.x);
            Physics2D.SyncTransforms();
            placed.net = go.GetComponent<NetworkObject>();
            placed.bodies = go.GetComponentsInChildren<Rigidbody2D>(true);
            placed.respawnAt = -1f;
            placed.net.Spawn(destroyWithScene: true);
            return true;
        }

        // Host-side physics only: clients follow the host's bodies, and the root scale reaches them on its own
        private static void Configure(GameObject go, MapGadget g, float factor)
        {
            foreach (DistanceJoint2D joint in go.GetComponentsInChildren<DistanceJoint2D>(true))
            {
                if (!joint.autoConfigureDistance) joint.distance *= factor;
            }
            foreach (PlatformDescender cage in go.GetComponentsInChildren<PlatformDescender>(true))
            {
                cage.maxDist *= factor;
                cage.minDist *= factor;
                // A second cage runs the opposite way: it starts low and rises first
                CageRising(cage) = g.phase == 1;
                foreach (SpringJoint2D spring in cage.springJoints)
                {
                    if (spring != null) spring.distance = g.phase == 1 ? cage.maxDist : spring.distance * factor;
                }
            }
            if (g.motor != 0f)
            {
                foreach (HingeJoint2D hinge in go.GetComponents<HingeJoint2D>())
                {
                    hinge.useMotor = true;
                    hinge.motor = new JointMotor2D { motorSpeed = g.motor, maxMotorTorque = 1000000f };
                }
            }
        }

        private static void Despawn(Placed placed)
        {
            if (placed.net == null) return;
            if (placed.net.IsSpawned) placed.net.Despawn(true);
            else Object.Destroy(placed.net.gameObject);
            placed.net = null;
        }

        private static GameObject FindPrefab(string name)
        {
            List<GameObject> prefabs = GameController.instance != null ? Prefabs(GameController.instance) : null;
            return prefabs?.Find(prefab => prefab != null && prefab.name == name);
        }

        private static bool IsLost(Placed placed)
        {
            if (placed.net == null || !placed.net.gameObject.activeInHierarchy) return true;
            MapRect box = _map.camera;
            foreach (Rigidbody2D body in placed.bodies)
            {
                if (body == null) continue;
                Vector2 p = body.position;
                if (p.x < box.xmin - LostMargin || p.x > box.xmax + LostMargin || p.y < box.ymin - LostMargin || p.y > box.ymax + LostMargin)
                {
                    return true;
                }
            }
            return false;
        }

        // Puts back boxes and balls lost to the lava, or drifting away in zero gravity
        private sealed class Keeper : MonoBehaviour
        {
            private float _nextCheck;

            private void Update()
            {
                if (_map == null || _map != SurgeMaps.Current || !Net.IsAuthority || _map.camera == null || Time.time < _nextCheck) return;
                _nextCheck = Time.time + CheckInterval;
                foreach (Placed placed in _placed)
                {
                    if (placed.respawnAt < 0f)
                    {
                        if (!IsLost(placed)) continue;
                        Despawn(placed);
                        placed.respawnAt = Time.time + RespawnDelay;
                    }
                    else if (Time.time >= placed.respawnAt)
                    {
                        Spawn(placed);
                    }
                }
            }

            // The scene took the gadgets with it
            private void OnDestroy()
            {
                if (_keeper != this) return;
                _keeper = null;
                _placed.Clear();
                _map = null;
            }
        }
    }
}
