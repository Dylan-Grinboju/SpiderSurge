using System.Collections.Generic;
using HarmonyLib;
using SpiderSurge.Common;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace SpiderSurge.Maps
{
    [HarmonyPatch(typeof(SurvivalMode), "Start")]
    internal static class SurvivalModeStartPatch
    {
        static void Postfix(SurvivalMode __instance) => SurgeMaps.Register(__instance);
    }

    [HarmonyPatch(typeof(SurvivalMode), nameof(SurvivalMode.Levels), MethodType.Getter)]
    internal static class SurvivalLevelsPatch
    {
        static void Postfix(SurvivalMode __instance, ref List<GameLevel> __result)
        {
            SurgeMaps.Register(__instance);
            __result = SurgeMaps.Filter(__result);
        }
    }

    // A client resolves the host's perk cards by index into the same list
    [HarmonyPatch(typeof(SurvivalModeHud), "ShowPerkChoicesClientRpc")]
    internal static class PerkChoicesClientPatch
    {
        static void Prefix() => SurgeMaps.Register(SurvivalMode.instance);
    }

    [HarmonyPatch(typeof(LevelController), nameof(LevelController.LoadLevelWithTransition))]
    internal static class LoadSurgeMapPatch
    {
        static void Prefix(LevelController __instance, GameLevel level, ref string nameOverride)
        {
            if (level != null) SurgeMaps.OnLevelChosen(__instance, level, ref nameOverride);
        }
    }

    // Every Surge map shares one scene, so a reload has to find the map that is loaded rather than the first by scene name
    [HarmonyPatch(typeof(LevelController), "GetActiveLevel")]
    internal static class ActiveSurgeMapPatch
    {
        static void Postfix(ref GameLevel __result)
        {
            if (SurgeMaps.Current?.level != null && SceneManager.GetActiveScene().name == SurgeMaps.SceneName)
            {
                __result = SurgeMaps.Current.level;
            }
        }
    }

    [HarmonyPatch(typeof(CustomMapEditor), nameof(CustomMapEditor.DeserializeMap))]
    internal static class DeserializeSurgeMapPatch
    {
        static bool Prefix(string mapPath, ref SerializedMap __result)
        {
            SurgeMap map = SurgeMaps.ByPath(mapPath);
            if (map == null) return true;
            if (Net.IsAuthority) VersusMode.CustomMapString = map.json;
            __result = CustomMapEditor.SerializeCustomMap(map.json);
            return false;
        }
    }

    [HarmonyPatch(typeof(CustomMapEditor), nameof(CustomMapEditor.LoadSerializedMap))]
    internal static class SurgeMapBuiltPatch
    {
        static void Postfix(SerializedMap serializedMap, bool inEditor)
        {
            if (!inEditor) SurgeMaps.OnMapBuilt(serializedMap);
        }
    }

    // The host builds a Surge map from SurvivalMode's scene load; the editor's own handler would build it a second time
    [HarmonyPatch(typeof(CustomMapEditor), "OnSceneLoaded")]
    internal static class SurgeMapSceneLoadedPatch
    {
        static bool Prefix() => !(Net.IsAuthority && SurgeMaps.Current != null);
    }

    // A client's first visit to the scene has no earlier editor to catch the load, so the new one builds the map itself
    [HarmonyPatch(typeof(CustomMapEditor), "Start")]
    internal static class SurgeMapClientStartPatch
    {
        static void Postfix(CustomMapEditor __instance) => SurgeMaps.BuildOnClient(__instance);
    }

    // A client that joins mid-map has no map text until the host sends it
    [HarmonyPatch(typeof(CustomMapEditor), nameof(CustomMapEditor.LoadMap))]
    internal static class EmptyClientMapPatch
    {
        static bool Prefix() => Net.IsAuthority || !string.IsNullOrEmpty(VersusMode.CustomMapString);
    }
}
