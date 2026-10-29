using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Unity.Netcode;
using Logger = Silk.Logger;

namespace SpiderSurge.Common
{
    // Non-public game members that more than one place reads; Mono throws on direct access, so they go through AccessTools.
    // A member a game update renames is logged once and left null, and each caller's own guard decides what happens without it
    internal static class GameFields
    {
        public static readonly AccessTools.FieldRef<SpiderHealthSystem, float> SpiderImmuneTill = Field<SpiderHealthSystem, float>("_immuneTill");
        public static readonly AccessTools.FieldRef<SpiderHealthSystem, bool> SpiderDying = Field<SpiderHealthSystem, bool>("_dying");
        public static readonly AccessTools.FieldRef<EnemyHealthSystem, float> EnemyImmuneTime = Field<EnemyHealthSystem, float>("_immuneTime");
        public static readonly AccessTools.FieldRef<EnemyHealthSystem, bool> EnemyExploding = Field<EnemyHealthSystem, bool>("_exploding");

        public static readonly AccessTools.FieldRef<SurvivalMode, SurvivalConfig> ActiveConfig = Field<SurvivalMode, SurvivalConfig>("_config");
        public static readonly AccessTools.FieldRef<SurvivalMode, bool> WaveInProgress = Field<SurvivalMode, bool>("_waveInProgress");
        public static readonly AccessTools.FieldRef<SurvivalMode, NetworkVariable<bool>> WaitingForPerk = Field<SurvivalMode, NetworkVariable<bool>>("_waitingForPerk");
        public static readonly AccessTools.FieldRef<SurvivalMode, NetworkVariable<int>> Lives = Field<SurvivalMode, NetworkVariable<int>>("_lives");
        public static readonly AccessTools.FieldRef<SurvivalMode, List<SpawnableWeapon>> SurvivalWeapons = Field<SurvivalMode, List<SpawnableWeapon>>("_weapons");

        public static readonly AccessTools.FieldRef<ModifierManager, List<Modifier>> Modifiers = Field<ModifierManager, List<Modifier>>("_modifiers");
        public static readonly AccessTools.FieldRef<ModifierManager, ModifierManager.NetworkModifier[]> ModifierStates = Field<ModifierManager, ModifierManager.NetworkModifier[]>("_currModsState");
        public static readonly AccessTools.FieldRef<SurvivalModifierChoiceCard, int> CardIndex = Field<SurvivalModifierChoiceCard, int>("cardIndex");

        public static readonly AccessTools.FieldRef<WhispBrain, float> WhispShotCooldownTill = Field<WhispBrain, float>("_shotCooldownTill");
        public static readonly MethodInfo WhispStopChargeCosmetics = Method(typeof(WhispBrain), "StopChargeCosmeticsClientRpc");

        public static readonly AccessTools.FieldRef<NetworkObject, HashSet<ulong>> Observers = Field<NetworkObject, HashSet<ulong>>("Observers");

        private static AccessTools.FieldRef<T, F> Field<T, F>(string name)
        {
            FieldInfo field = AccessTools.Field(typeof(T), name);
            if (field != null)
            {
                try
                {
                    return AccessTools.FieldRefAccess<T, F>(field);
                }
                catch (Exception ex)
                {
                    Logger.LogError($"SpiderSurge could not bind {typeof(T).Name}.{name}: {ex.Message}");
                    return null;
                }
            }
            Logger.LogWarning($"SpiderSurge could not find {typeof(T).Name}.{name}; the feature that needs it is off");
            return null;
        }

        private static MethodInfo Method(Type type, string name)
        {
            MethodInfo method = AccessTools.Method(type, name);
            if (method == null) Logger.LogWarning($"SpiderSurge could not find {type.Name}.{name}; the feature that needs it is off");
            return method;
        }
    }
}
