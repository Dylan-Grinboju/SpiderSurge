using HarmonyLib;
using Silk;
using System.Collections;
using System.Linq;
using System.Reflection;
using Unity.Netcode;
using UnityEngine;

namespace SpiderSurge
{
    [HarmonyPatch(typeof(SurvivalModeHud), "ActivateChoiseViewTimer")]
    public class SurvivalModeHud_ActivateChoiseViewTimer_Patch
    {
        [HarmonyPrefix]
        public static bool Prefix(SurvivalModeHud __instance, ref IEnumerator __result)
        {
            if (ModConfig.UnlimitedPerkChoosingTime)
            {
                // Access private field perkChoiseTimer
                var field = typeof(SurvivalModeHud).GetField("perkChoiseTimer", BindingFlags.NonPublic | BindingFlags.Instance);
                if (field != null)
                {
                    var networkVar = field.GetValue(__instance) as NetworkVariable<uint>;
                    if (networkVar != null)
                    {
                        // Use NetworkManager.Singleton.IsHost instead of __instance.IsHost to avoid access exception
                        bool isHost = false;
                        if (NetworkManager.Singleton != null)
                        {
                            isHost = NetworkManager.Singleton.IsHost;
                        }

                        if (isHost)
                        {
                            networkVar.Value = 999;
                        }
                    }
                }

                // Return an empty enumerator so the coroutine finishes immediately and does nothing
                // (No countdown, no auto-pick)
                __result = Enumerable.Empty<object>().GetEnumerator();
                return false;
            }
            return true;
        }
    }
}
