using HarmonyLib;
using Logger = Silk.Logger;
using System;
using UnityEngine;
using System.Reflection;
using SpiderSurge.Common;

namespace SpiderSurge.Fixes
{
    [HarmonyPatch(typeof(WhispBrain), "Start")]
    public class WhispCooldownStartPatch
    {
        static void Postfix(WhispBrain __instance)
        {
            try
            {
                if (GameFields.WhispShotCooldownTill != null)
                {
                    GameFields.WhispShotCooldownTill(__instance) = Time.time + __instance.shotCooldown;
                }
                else
                {
                    Logger.LogWarning("WhispBrain patch: Could not access required fields for cooldown initialization");
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"Error in WhispBrain.Start patch: {ex.Message}");
            }
        }
    }
}
