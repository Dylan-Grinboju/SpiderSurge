using HarmonyLib;
using Silk;
using Logger = Silk.Logger;
using UnityEngine.SceneManagement;
using System;
using System.Reflection;

namespace SpiderSurge
{
    [HarmonyPatch(typeof(EOSLeaderboards), "SetRecord", new Type[] { typeof(uint), typeof(int) })]
    public class EOSLeaderboards_SetRecord_Patch
    {
        [HarmonyPrefix]
        public static bool Prefix()
        {
            if (SurgeGameModeManager.Instance.IsActive)
            {
                return false; // Skip leaderboard submission for Surge mode
            }
            return true;
        }
    }
}