using HarmonyLib;
using Unity.Netcode;

namespace SpiderSurge.Fixes
{
    // The lobby's PainLevelColourUnlockChecker has no NetworkObject, so its RPC throws and aborts the colour unlock loop.
    [HarmonyPatch(typeof(PainLevelColourUnlockChecker), "SetAchievementClientRpc")]
    internal static class PainLevelColourUnlockRpcPatch
    {
        static bool Prefix(PainLevelColourUnlockChecker __instance, Achievements.AchievementInfo info)
        {
            if (__instance.GetComponentInParent<NetworkObject>() != null)
            {
                return true;
            }
            if (info != null)
            {
                Achievements.SetAchievement(info);
            }
            return false;
        }
    }
}
