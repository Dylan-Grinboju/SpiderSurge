using System.Collections.Generic;
using HarmonyLib;
using SpiderSurge.Enemies;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SpiderSurge
{
    [HarmonyPatch(typeof(OffScreenIndicator), "Update")]
    internal static class OffScreenIndicator_EnemyColor_Patch
    {
        private static readonly AccessTools.FieldRef<OffScreenIndicator, EnemyHealthSystem> Health = AccessTools.FieldRefAccess<OffScreenIndicator, EnemyHealthSystem>("hs");
        private static readonly AccessTools.FieldRef<OffScreenIndicator, SpriteRenderer> Indicator = AccessTools.FieldRefAccess<OffScreenIndicator, SpriteRenderer>("_indicatorSprite");

        private sealed class Kind
        {
            public bool Roller;
            public GameObject Shield;
            public bool Laser;
        }

        private static readonly Dictionary<int, Kind> Kinds = new Dictionary<int, Kind>();

        static OffScreenIndicator_EnemyColor_Patch()
        {
            SceneManager.sceneLoaded += (scene, mode) => Kinds.Clear();
        }

        // An enemy's components don't change after it spawns, so each indicator looks them up once, and again only if the enemy gets another shield
        [HarmonyPostfix]
        private static void Postfix(OffScreenIndicator __instance)
        {
            EnemyHealthSystem enemy = Health(__instance);
            SpriteRenderer indicator = Indicator(__instance);
            if (enemy == null || indicator == null) return;
            GameObject shield = enemy.shield;
            int id = __instance.GetInstanceID();
            if (!Kinds.TryGetValue(id, out Kind kind))
            {
                kind = new Kind { Roller = enemy.GetComponent<ExplodingRoller>() != null };
                Kinds[id] = kind;
            }
            if (!ReferenceEquals(kind.Shield, shield))
            {
                kind.Shield = shield;
                kind.Laser = shield != null && shield.GetComponent<LaserRing>() != null;
            }
            Color color = Consts.Values.Colors.UnshieldedEnemyIndicatorColor;
            if (kind.Roller) color = Color.red;
            else if (shield != null && shield.activeInHierarchy)
                color = kind.Laser ? Color.red : Consts.Values.Colors.ShieldedEnemyIndicatorColor;
            if (indicator.color != color) indicator.color = color;
        }
    }
}
