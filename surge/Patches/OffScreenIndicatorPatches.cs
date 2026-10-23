using HarmonyLib;
using SpiderSurge.Enemies;
using UnityEngine;

namespace SpiderSurge
{
    [HarmonyPatch(typeof(OffScreenIndicator), "Update")]
    internal static class OffScreenIndicator_LaserShield_Patch
    {
        private static readonly AccessTools.FieldRef<OffScreenIndicator, EnemyHealthSystem> Health = AccessTools.FieldRefAccess<OffScreenIndicator, EnemyHealthSystem>("hs");
        private static readonly AccessTools.FieldRef<OffScreenIndicator, SpriteRenderer> Indicator = AccessTools.FieldRefAccess<OffScreenIndicator, SpriteRenderer>("_indicatorSprite");
        private static readonly AccessTools.FieldRef<OffScreenIndicator, Color> OriginalColor = AccessTools.FieldRefAccess<OffScreenIndicator, Color>("color");

        [HarmonyPostfix]
        private static void Postfix(OffScreenIndicator __instance)
        {
            EnemyHealthSystem enemy = Health(__instance);
            SpriteRenderer indicator = Indicator(__instance);
            if (enemy == null || indicator == null) return;
            GameObject shield = enemy.shield;
            Color color = shield != null && shield.activeInHierarchy && shield.GetComponent<LaserRing>() != null
                ? Consts.Values.LaserRing.Color : OriginalColor(__instance);
            if (indicator.color != color) indicator.color = color;
        }
    }
}
