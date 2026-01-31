using HarmonyLib;
using System.Collections;
using System.Reflection;
using Unity.Netcode;

namespace SpiderSurge
{
    [HarmonyPatch(typeof(SurvivalModeHud), "ActivateChoiseViewTimer")]
    public class SurvivalModeHud_ActivateChoiseViewTimer_Patch
    {
        private static FieldInfo _perkChoiseTimerField;

        private static IEnumerator EmptyEnumerator()
        {
            yield break;
        }

        [HarmonyPrefix]
        public static bool Prefix(SurvivalModeHud __instance, ref IEnumerator __result)
        {
            if (ModConfig.UnlimitedPerkChoosingTime)
            {
                // Access private field perkChoiseTimer
                if (_perkChoiseTimerField == null)
                {
                    _perkChoiseTimerField = typeof(SurvivalModeHud).GetField("perkChoiseTimer", BindingFlags.NonPublic | BindingFlags.Instance);
                }

                if (_perkChoiseTimerField != null)
                {
                    var networkVar = _perkChoiseTimerField.GetValue(__instance) as NetworkVariable<uint>;
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
                __result = EmptyEnumerator();
                return false;
            }
            return true;
        }
    }
}
