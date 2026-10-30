using System;
using System.Collections.Generic;
using System.Reflection;
using Epic.OnlineServices;
using Epic.OnlineServices.Lobby;
using HarmonyLib;
using SpiderSurge.Common;
using UnityEngine;
using Logger = Silk.Logger;
using Object = UnityEngine.Object;

namespace SpiderSurge
{
    internal static class SurgePainLevels
    {
        internal const string LobbyAttribute = "surge";
        private static readonly FieldInfo Selected = AccessTools.Field(typeof(PainLevelsScreen), "painLevelID");
        private static readonly FieldInfo Current = AccessTools.Field(typeof(PainLevelsScreen), "currentLvl");
        private static SurvivalLevelConfig normal;
        private static SurvivalLevelConfig surge;

        internal static bool JoiningSurgeLobby { get; set; }

        // The extra level's number, one past the game's last pain level, or 0 before the pain menu exists
        internal static int Level => Ready(PainLevelsScreen.instance) ? surge.painLevels.Count : 0;
        internal static bool IsEight(int level) => level > 0 && level == Level;

        internal static bool SelectedEight => SurgeGameModeManager.IsModeEnabled
            && SurvivalModeHud.instance != null && IsEight(SurvivalModeHud.instance.currentPainLevel.Value);
        internal static bool ActiveEight => SurgeGameModeManager.IsSurgeRunActive && SelectedEight;

        internal static SurvivalConfig EightConfig => Ready(PainLevelsScreen.instance) ? surge.painLevels[surge.painLevels.Count - 1].survivalConfig : null;

        private static bool Ready(PainLevelsScreen target) => surge != null || Build(target);

        // Built once from the level asset every scene's pain menu shares; copies keep the menu's Setup off vanilla unlock progression
        private static bool Build(PainLevelsScreen target)
        {
            if (target == null || target.painLevels == null || target.painLevels == normal) return false;
            normal = target.painLevels;
            try
            {
                var copy = Object.Instantiate(normal);
                copy.painLevels = new List<SurvivalLevelsScriptable>();
                foreach (var level in normal.painLevels)
                {
                    var entry = Object.Instantiate(level);
                    entry.survivalConfig = Object.Instantiate(level.survivalConfig);
                    entry.survivalConfig.name = level.survivalConfig.name;
                    copy.painLevels.Add(entry);
                }
                var last = copy.painLevels[copy.painLevels.Count - 1];
                var eight = Object.Instantiate(last);
                eight.survivalConfig = Object.Instantiate(last.survivalConfig);
                eight.survivalConfig.name = "SurgePain" + (copy.painLevels.Count + 1);
                copy.painLevels.Add(eight);
                for (int i = 0; i < copy.painLevels.Count; i++)
                    copy.painLevels[i].survivalConfig.nextPainLevel = i + 1 < copy.painLevels.Count ? copy.painLevels[i + 1].levelNeededToUnlock : -1;
                SurvivalConfig_GetHighScore_Patch.RegisterSurgeConfig(eight.survivalConfig);
                surge = copy;
            }
            catch (Exception ex)
            {
                Logger.LogError($"Surge could not add its extra pain level: {ex}");
            }
            return surge != null;
        }

        // Shows Surge's list in the menu while Surge Survival is selected
        internal static void Refresh(PainLevelsScreen target)
        {
            if (target == null || !Ready(target)) return;
            target.painLevels = SurgeGameModeManager.IsModeEnabled ? surge : normal;
            int index = Mathf.Clamp((int)Selected.GetValue(target), 0, target.painLevels.painLevels.Count - 1);
            Selected.SetValue(target, index);
            Current.SetValue(target, target.painLevels.painLevels[index]);
        }

        // The game's GetHighestUnlockedPainLevel over Surge's own scores
        internal static int HighestUnlocked(PainLevelsScreen target, int vanilla)
        {
            if (!Ready(target)) return vanilla;
            int highest = 1;
            for (int i = 1; i < surge.painLevels.Count; i++)
            {
                if (SurgeHighScores.Get(surge.painLevels[i - 1].survivalConfig.name) >= surge.painLevels[i].levelNeededToUnlock) highest = i + 1;
            }
            return highest;
        }

        internal static bool IsSurgeLobby(LobbyDetails lobby)
        {
            var options = new LobbyDetailsCopyAttributeByKeyOptions { AttrKey = LobbyAttribute };
            if (lobby == null || lobby.CopyAttributeByKey(ref options, out var attribute) != Result.Success
                || !attribute.HasValue || !attribute.Value.Data.HasValue) return false;
            string value = attribute.Value.Data.Value.Value.AsUtf8;
            return value == "1";
        }

        internal static void SpawnWaveEnemy(EnemySpawner spawner, GameObject enemy)
        {
            spawner.SpawnEnemy(enemy);
            if (ActiveEight) spawner.SpawnEnemy(enemy);
        }
    }

    // A lobby carries its pain level but not its mode, so the host publishes whether it runs Surge beside it
    [HarmonyPatch(typeof(EOSLobbyManager), nameof(EOSLobbyManager.SetLobbyStringAttribute))]
    internal static class SurgeLobbyAttributePatch
    {
        static void Postfix(EOSLobbyManager __instance, string key)
        {
            if (key == ILobbyManager.PAIN_LEVEL_ATTRIBUTE)
                __instance.SetLobbyStringAttribute(SurgePainLevels.LobbyAttribute, SurgeGameModeManager.IsModeEnabled ? "1" : "0");
        }
    }

    // The game checks the lobby's pain level before its first await, so the flag covers that check
    [HarmonyPatch(typeof(EOSLobbyManager), "JoinToJobby")]
    internal static class SurgeLobbyJoinPatch
    {
        static void Prefix(LobbyDetails lobbyDetails) => SurgePainLevels.JoiningSurgeLobby = SurgePainLevels.IsSurgeLobby(lobbyDetails);
        static void Postfix() => SurgePainLevels.JoiningSurgeLobby = false;
    }

    // A Surge lobby's pain level is compared with Surge progress, which includes the extra level
    [HarmonyPatch(typeof(PainLevelsScreen), nameof(PainLevelsScreen.GetHighestUnlockedPainLevel))]
    internal static class SurgePainJoinPatch
    {
        static void Postfix(PainLevelsScreen __instance, ref int __result)
        {
            if (SurgePainLevels.JoiningSurgeLobby) __result = SurgePainLevels.HighestUnlocked(__instance, __result);
        }
    }

    [HarmonyPatch(typeof(PainLevelsScreen), "OnEnable")]
    internal static class SurgePainMenuPatch
    {
        static void Prefix(PainLevelsScreen __instance) => SurgePainLevels.Refresh(__instance);
    }

    [HarmonyPatch(typeof(PainLevelsScreen), "RefreshScreen")]
    internal static class SurgePainDescriptionPatch
    {
        static void Postfix(PainLevelsScreen __instance)
        {
            int level = __instance.GetPainLevel();
            if (SurgeGameModeManager.IsModeEnabled && SurgePainLevels.IsEight(level))
                __instance.painLevelInfo.text = $"Start with your ability AND its ultimate.\nTwice as many enemies and twice the enemy capacity.\n\nAll Pain {level - 1} modifiers apply. Abilities stay enabled.";
        }
    }

    [HarmonyPatch(typeof(SurvivalMode), nameof(SurvivalMode.GetSurvivalConfig))]
    internal static class SurgePainConfigPatch
    {
        static bool Prefix(SurvivalMode __instance, int painLevel, ref SurvivalConfig __result)
        {
            var hud = SurvivalModeHud.instance;
            int level = painLevel > 0 ? painLevel : hud != null ? hud.currentPainLevel.Value : -1;
            if (!SurgePainLevels.IsEight(level)) return true;
            // Tiers of Heck and Parkour leave the last pain level in place, so outside Surge Survival it means the running mode's own config
            if (painLevel <= 0 && !SurgeGameModeManager.IsModeEnabled)
            {
                SurvivalConfig active = GameFields.ActiveConfig(__instance);
                __result = active != null ? active : HudController.instance.survivalConfig;
                return false;
            }
            __result = SurgePainLevels.EightConfig;
            return false;
        }
    }
}
