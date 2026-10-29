using System;
using HarmonyLib;
using UnityEngine;

namespace SpiderTeams
{
    [HarmonyPatch(typeof(RailShot), "SpawnImpactEffect", new[] { typeof(Vector2) })]
    public static class RailShot_SpawnImpactEffect_Patch
    {
        // The rail's impact splash is an Explosion; a stopping shot spawns it from inside another RPC with no send pass, so it is recorded here.
        [HarmonyPrefix]
        public static void Prefix(RailShot __instance, Vector2 hitPoint)
        {
            try
            {
                if (TeamManager.HostTeamMode)
                {
                    BlastOwners.RecordRail(hitPoint, RailShotOwner.Of(__instance));
                }
            }
            catch (Exception ex)
            {
                PatchScopes.Report("rail impact tracking", ex);
            }
        }
    }
}
