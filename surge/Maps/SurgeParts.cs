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
    // As on the base-game arenas, a crate or ball lost to the lava stays lost until the level reloads, which rebuilds them all.
    public static class SurgeParts
    {
        private static readonly AccessTools.FieldRef<GameController, List<GameObject>> Prefabs =
            AccessTools.FieldRefAccess<GameController, List<GameObject>>("Prefabs");
        private static readonly AccessTools.FieldRef<PlatformDescender, bool> CageRising =
            AccessTools.FieldRefAccess<PlatformDescender, bool>("isUp");

        private static readonly List<NetworkObject> _spawned = new List<NetworkObject>();

        // Host only, once the map's elements are built
        public static void Build(SurgeMap map)
        {
            Clear();
            if (!Net.IsAuthority || map?.parts == null) return;
            foreach (MapGadget gadget in map.parts)
            {
                NetworkObject net = Spawn(gadget);
                if (net != null) _spawned.Add(net);
            }
        }

        private static void Clear()
        {
            foreach (NetworkObject net in _spawned)
            {
                if (net == null) continue;
                if (net.IsSpawned) net.Despawn(true);
                else Object.Destroy(net.gameObject);
            }
            _spawned.Clear();
        }

        private static NetworkObject Spawn(MapGadget g)
        {
            GameObject prefab = FindPrefab(g.o);
            if (prefab == null)
            {
                Logger.LogWarning($"[SurgeParts] No network prefab named '{g.o}'");
                return null;
            }
            GameObject go = Object.Instantiate(prefab, new Vector3(g.x, g.y, 0f), Quaternion.Euler(0f, 0f, g.r));
            Vector3 scale = prefab.transform.localScale;
            go.transform.localScale = new Vector3(g.s, g.s, scale.z);
            Configure(go, g, g.s / scale.x);
            Physics2D.SyncTransforms();
            NetworkObject net = go.GetComponent<NetworkObject>();
            net.Spawn(destroyWithScene: true);
            return net;
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

        private static GameObject FindPrefab(string name)
        {
            List<GameObject> prefabs = GameController.instance != null ? Prefabs(GameController.instance) : null;
            return prefabs?.Find(prefab => prefab != null && prefab.name == name);
        }
    }
}
