using HarmonyLib;
using SpiderSurge.Enemies;
using UnityEngine;

namespace SpiderSurge
{
    [HarmonyPatch(typeof(OffScreenIndicator), "Update")]
    internal static class OffScreenIndicator_EnemyColor_Patch
    {
        private static readonly AccessTools.FieldRef<OffScreenIndicator, EnemyHealthSystem> Health = AccessTools.FieldRefAccess<OffScreenIndicator, EnemyHealthSystem>("hs");
        private static readonly AccessTools.FieldRef<OffScreenIndicator, SpriteRenderer> Indicator = AccessTools.FieldRefAccess<OffScreenIndicator, SpriteRenderer>("_indicatorSprite");

        [HarmonyPostfix]
        private static void Postfix(OffScreenIndicator __instance)
        {
            EnemyHealthSystem enemy = Health(__instance);
            SpriteRenderer indicator = Indicator(__instance);
            if (enemy == null || indicator == null) return;
            GameObject shield = enemy.shield;
            Color color = Color.yellow;
            if (enemy.GetComponent<ExplodingRoller>() != null) color = Color.red;
            else if (shield != null && shield.activeInHierarchy)
                color = shield.GetComponent<LaserRing>() != null ? Color.red : Consts.Values.Colors.ShieldedEnemyIndicatorColor;
            if (indicator.color != color) indicator.color = color;
        }
    }
}
