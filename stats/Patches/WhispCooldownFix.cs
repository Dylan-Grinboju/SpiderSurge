using HarmonyLib;
using Logger = Silk.Logger;
using System;
using UnityEngine;
using System.Reflection;

namespace WhispCooldownFix
{
    [HarmonyPatch(typeof(WhispBrain), "Start")]
    public class WhispCooldownStartPatch
    {
        private static readonly FieldInfo shotCooldownTillField = AccessTools.Field(typeof(WhispBrain), "_shotCooldownTill");
        private static readonly FieldInfo shotCooldownField = AccessTools.Field(typeof(WhispBrain), "shotCooldown");

        static void Postfix(WhispBrain __instance)
        {
            try
            {
                if (shotCooldownTillField != null && shotCooldownField != null)
                {
                    float shotCooldown = (float)shotCooldownField.GetValue(__instance);
                    shotCooldownTillField.SetValue(__instance, Time.time + shotCooldown);
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
