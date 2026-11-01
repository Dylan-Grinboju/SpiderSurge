using HarmonyLib;
using UnityEngine.SceneManagement;

namespace SpiderSurge.Maps
{
    internal static class SurgeMapHud
    {
        internal static bool Active => SurgeGameModeManager.IsSurgeRunActive && SurgeMaps.Current != null &&
            SceneManager.GetActiveScene().name == SurgeMaps.SceneName;
    }

    [HarmonyPatch(typeof(SurvivalModeHud), "DisplaySurvivalHud")]
    internal static class SurgeMapSurvivalHudPatch
    {
        // The custom scene normally hides Survival UI because its maps are Versus or Parkour.
        static bool Prefix(SurvivalModeHud __instance)
        {
            if (!SurgeMapHud.Active) return true;
            bool show = SurvivalMode.instance.GameModeHudActive();
            if (show && __instance.survivalHud.IsHidden) __instance.survivalHud.Show();
            if (!show && !__instance.survivalHud.IsHidden) __instance.survivalHud.Hide();
            return false;
        }
    }

    [HarmonyPatch(typeof(SurvivalModeHud), "UpdateWaveCounter")]
    internal static class SurgeMapWaveCounterPatch
    {
        static bool Prefix(SurvivalModeHud __instance)
        {
            if (!SurgeMapHud.Active) return true;
            __instance.waveCounter.text = SurvivalMode.instance.CurrentWave.ToString();
            __instance.waveCounter.transform.parent.gameObject.SetActive(true);
            return false;
        }
    }
}
