using HarmonyLib;
using UnityEngine;

namespace OffScreenIndicatorFix
{
    [HarmonyPatch(typeof(OffScreenIndicator), "FollowTarget")]
    public class OffScreenIndicatorFollowTargetPatch
    {
        static bool Prefix(SpriteRenderer ____indicatorSprite)
        {
            return ____indicatorSprite != null;
        }
    }
}
