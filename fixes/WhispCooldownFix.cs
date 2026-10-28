using HarmonyLib;
using Logger = Silk.Logger;
using System;
using UnityEngine;
using System.Reflection;

namespace SpiderSurge.Fixes
{
    [HarmonyPatch(typeof(WhispBrain), "Start")]
    public class WhispCooldownStartPatch
    {
        private static readonly FieldInfo shotCooldownTillField = AccessTools.Field(typeof(WhispBrain), "_shotCooldownTill");

        static void Postfix(WhispBrain __instance)
        {
            try
            {
                if (shotCooldownTillField != null)
                {
                    shotCooldownTillField.SetValue(__instance, Time.time + __instance.shotCooldown);
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
