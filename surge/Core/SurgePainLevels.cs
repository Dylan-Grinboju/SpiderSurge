using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using SpiderSurge.Common;
using UnityEngine;

namespace SpiderSurge
{
    internal static class SurgePainLevels
    {
        private static readonly FieldInfo Selected = AccessTools.Field(typeof(PainLevelsScreen), "painLevelID");
        private static readonly FieldInfo Current = AccessTools.Field(typeof(PainLevelsScreen), "currentLvl");
        private static PainLevelsScreen screen;
        private static SurvivalLevelConfig normal;
        private static SurvivalLevelConfig surge;

        internal static bool SelectedEight => SurgeGameModeManager.IsModeEnabled
            && SurvivalModeHud.instance != null && SurvivalModeHud.instance.currentPainLevel.Value == 8;
        internal static bool ActiveEight => SurgeGameModeManager.IsSurgeRunActive && SelectedEight;

        // Clone the menu's configs so its Setup method cannot change vanilla unlock progression.
        internal static void Refresh(PainLevelsScreen target)
        {
            if (target == null) return;
            if (screen != target)
            {
                screen = target;
                normal = target.painLevels;
                surge = Object.Instantiate(normal);
                surge.painLevels = new List<SurvivalLevelsScriptable>();
                foreach (var level in normal.painLevels)
                {
                    var copy = Object.Instantiate(level);
                    copy.survivalConfig = Object.Instantiate(level.survivalConfig);
                    copy.survivalConfig.name = level.survivalConfig.name;
                    surge.painLevels.Add(copy);
                }
                var eight = Object.Instantiate(surge.painLevels[6]);
                eight.survivalConfig = Object.Instantiate(eight.survivalConfig);
                eight.survivalConfig.name = "SurgePain8";
                eight.survivalConfig.nextPainLevel = -1;
                surge.painLevels.Add(eight);
                SurvivalConfig_GetHighScore_Patch.RegisterSurgeConfig(eight.survivalConfig);
            }
            target.painLevels = SurgeGameModeManager.IsModeEnabled ? surge : normal;
            int index = Mathf.Clamp((int)Selected.GetValue(target), 0, target.painLevels.painLevels.Count - 1);
            Selected.SetValue(target, index);
            Current.SetValue(target, target.painLevels.painLevels[index]);
        }

        internal static SurvivalConfig EightConfig
        {
            get
            {
                Refresh(PainLevelsScreen.instance);
                return surge.painLevels[7].survivalConfig;
            }
        }

        internal static bool EightUnlocked
        {
            get
            {
                Refresh(PainLevelsScreen.instance);
                return surge != null && SurgeHighScores.Get(surge.painLevels[6].survivalConfig.name) >= surge.painLevels[7].levelNeededToUnlock;
            }
        }

        internal static void GrantStartingUltimate()
        {
            if (!ActiveEight || !Net.IsAuthority) return;
            string key = PerksManager.Instance.GetChosenAbilityUltimate();
            var modifier = ModifierManager.instance.GetModById(ModifierManager.instance.GetModId(key));
            ModifierManager.instance.SetModLevel(modifier, GameMode.Wave, 1);
            StatsMod.GameSessionManager.Instance?.RecordPerk(PerksManager.Instance.GetDisplayName(key));
        }

        internal static void SpawnWaveEnemy(EnemySpawner spawner, GameObject enemy)
        {
            spawner.SpawnEnemy(enemy);
            if (Net.IsAuthority && ActiveEight) spawner.SpawnEnemy(enemy);
        }
    }

    // Lobby discovery checks this before the host's Surge state arrives; the vanilla menu still has seven entries.
    [HarmonyPatch(typeof(PainLevelsScreen), nameof(PainLevelsScreen.GetHighestUnlockedPainLevel))]
    internal static class SurgePainJoinPatch
    {
        static void Postfix(ref int __result)
        {
            if (SurgePainLevels.EightUnlocked) __result = 8;
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
            if (SurgeGameModeManager.IsModeEnabled && __instance.GetPainLevel() == 8)
                __instance.painLevelInfo.text = "Start with your ability AND its ultimate.\nTwice as many enemies and twice the enemy capacity.\n\nAll Pain 7 modifiers apply. Abilities stay enabled.";
        }
    }

    [HarmonyPatch(typeof(SurvivalMode), nameof(SurvivalMode.GetSurvivalConfig))]
    internal static class SurgePainConfigPatch
    {
        static bool Prefix(int painLevel, ref SurvivalConfig __result)
        {
            if (painLevel != 8 && (painLevel > 0 || SurvivalModeHud.instance == null || SurvivalModeHud.instance.currentPainLevel.Value != 8)) return true;
            __result = SurgePainLevels.EightConfig;
            return false;
        }
    }
}
