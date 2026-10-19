using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using I2.Loc;
using Unity.Netcode;
using UnityEngine;
using Logger = Silk.Logger;

namespace SpiderTeams
{
    internal static class ReflectionCache
    {
        #region NetworkBehaviour
        public static readonly FieldInfo RpcExecStage = AccessTools.Field(typeof(NetworkBehaviour), "__rpc_exec_stage");
        #endregion

        #region VersusMode
        public static readonly FieldInfo VersusRoundInProgress = AccessTools.Field(typeof(VersusMode), "_roundInProgress");
        public static readonly FieldInfo VersusRoundEnding = AccessTools.Field(typeof(VersusMode), "_roundEnding");
        public static readonly FieldInfo VersusWaitingRoom = AccessTools.Field(typeof(VersusMode), "_waitingRoom");
        public static readonly FieldInfo VersusDrawString = AccessTools.Field(typeof(VersusMode), "_drawString");
        public static readonly FieldInfo VersusAlmostWonString = AccessTools.Field(typeof(VersusMode), "_almostWonString");
        public static readonly FieldInfo VersusCongratulationsString = AccessTools.Field(typeof(VersusMode), "_versusCongratulationsString");
        public static readonly MethodInfo VersusDeclareWinner = AccessTools.Method(typeof(VersusMode), "DeclareWinner");
        public static readonly MethodInfo VersusDoSlowMotion = AccessTools.Method(typeof(VersusMode), "DoSlowMotionClientRpc");
        public static readonly MethodInfo VersusShowScorePrompts = AccessTools.Method(typeof(VersusMode), "ShowScorePromptsClientRpc", new[] { typeof(ulong), typeof(int) });
        public static readonly MethodInfo VersusSetAchievement = AccessTools.Method(typeof(VersusMode), "SetAchievementClientRpc");
        #endregion

        #region LobbyController
        public static readonly FieldInfo LobbySpawnPoints = AccessTools.Field(typeof(LobbyController), "_spawnPoints");
        #endregion

        #region PlayerController
        public static readonly FieldInfo PlayerActiveCharacter = AccessTools.Field(typeof(PlayerController), "_activePlayerCharacter");
        #endregion

        #region SpiderHealthSystem
        public static readonly FieldInfo SpiderImmuneTill = AccessTools.Field(typeof(SpiderHealthSystem), "_immuneTill");
        #endregion

        #region Win screens
        public static readonly FieldInfo TimerWinnerView = AccessTools.Field(typeof(VersusRestartTimer), "winnerview");
        public static readonly FieldInfo TimerWinPrompt = AccessTools.Field(typeof(VersusRestartTimer), "_winPromptString");
        public static readonly FieldInfo TimerFinalScore = AccessTools.Field(typeof(VersusRestartTimer), "_finalScoreString");
        public static readonly FieldInfo TimerFinalSingleScore = AccessTools.Field(typeof(VersusRestartTimer), "_finalSingleScoreString");
        #endregion

        private static readonly AccessTools.FieldRef<bool> _roundInProgress = StaticRef<bool>(VersusRoundInProgress);
        private static readonly AccessTools.FieldRef<bool> _roundEnding = StaticRef<bool>(VersusRoundEnding);
        private static readonly AccessTools.FieldRef<VersusMode, bool> _waitingRoom = InstanceRef<VersusMode, bool>(VersusWaitingRoom);
        private static readonly AccessTools.FieldRef<SpiderHealthSystem, float> _immuneTill = InstanceRef<SpiderHealthSystem, float>(SpiderImmuneTill);

        public static bool RoundRulesAvailable =>
            _roundInProgress != null && _roundEnding != null && _waitingRoom != null &&
            VersusDeclareWinner != null && VersusDoSlowMotion != null &&
            VersusShowScorePrompts != null && VersusSetAchievement != null;

        public static bool RoundInProgress => _roundInProgress != null && _roundInProgress();

        public static bool RoundEnding
        {
            get { return _roundEnding != null && _roundEnding(); }
            set
            {
                if (_roundEnding != null)
                {
                    _roundEnding() = value;
                }
            }
        }

        public static bool IsWaitingRoom(VersusMode versus)
        {
            return versus != null && _waitingRoom != null && _waitingRoom(versus);
        }

        public static float ImmuneTill(SpiderHealthSystem health)
        {
            return health != null && _immuneTill != null ? _immuneTill(health) : 0f;
        }

        // On the host a ClientRpc body runs twice: once to send (stage 0) and once nested to execute.
        public static bool IsRpcSendStage(NetworkBehaviour behaviour)
        {
            if (RpcExecStage == null || behaviour == null)
            {
                return true;
            }

            try
            {
                return Convert.ToInt32(RpcExecStage.GetValue(behaviour)) == 0;
            }
            catch
            {
                return true;
            }
        }

        public static GameObject ActiveCharacter(PlayerController player)
        {
            if (player == null || PlayerActiveCharacter == null)
            {
                return null;
            }
            return PlayerActiveCharacter.GetValue(player) as GameObject;
        }

        public static void DeclareWinner(VersusMode versus)
        {
            VersusDeclareWinner.Invoke(versus, null);
        }

        public static void DoSlowMotion(VersusMode versus)
        {
            VersusDoSlowMotion.Invoke(versus, null);
        }

        public static void ShowScorePrompts(VersusMode versus, ulong objectId, int score)
        {
            VersusShowScorePrompts.Invoke(versus, new object[] { objectId, score });
        }

        public static void SetAchievement(VersusMode versus, Achievements.AchievementInfo info)
        {
            VersusSetAchievement.Invoke(versus, new object[] { info });
        }

        public static string Term(FieldInfo field, object owner)
        {
            LocalizedString value;
            return TryGetLocalized(field, owner, out value) && !string.IsNullOrEmpty(value.mTerm) ? value.mTerm : null;
        }

        public static string Text(FieldInfo field, object owner, string fallback)
        {
            LocalizedString value;
            if (!TryGetLocalized(field, owner, out value))
            {
                return fallback;
            }

            string text = value;
            return string.IsNullOrEmpty(text) ? fallback : text;
        }

        public static void ReportMissing()
        {
            var members = new Dictionary<string, object>
            {
                { "NetworkBehaviour.__rpc_exec_stage", RpcExecStage },
                { "VersusMode._roundInProgress", _roundInProgress },
                { "VersusMode._roundEnding", _roundEnding },
                { "VersusMode._waitingRoom", _waitingRoom },
                { "VersusMode._drawString", VersusDrawString },
                { "VersusMode._almostWonString", VersusAlmostWonString },
                { "VersusMode._versusCongratulationsString", VersusCongratulationsString },
                { "VersusMode.DeclareWinner", VersusDeclareWinner },
                { "VersusMode.DoSlowMotionClientRpc", VersusDoSlowMotion },
                { "VersusMode.ShowScorePromptsClientRpc", VersusShowScorePrompts },
                { "VersusMode.SetAchievementClientRpc", VersusSetAchievement },
                { "LobbyController._spawnPoints", LobbySpawnPoints },
                { "PlayerController._activePlayerCharacter", PlayerActiveCharacter },
                { "SpiderHealthSystem._immuneTill", _immuneTill },
                { "VersusRestartTimer.winnerview", TimerWinnerView },
                { "VersusRestartTimer._winPromptString", TimerWinPrompt },
            };

            foreach (var member in members)
            {
                if (member.Value == null)
                {
                    Logger.LogWarning($"SpiderTeams could not find {member.Key}; the feature that needs it falls back to vanilla");
                }
            }
        }

        private static bool TryGetLocalized(FieldInfo field, object owner, out LocalizedString value)
        {
            value = default(LocalizedString);
            if (field == null || owner == null)
            {
                return false;
            }

            object boxed = field.GetValue(owner);
            if (!(boxed is LocalizedString))
            {
                return false;
            }

            value = (LocalizedString)boxed;
            return true;
        }

        private static AccessTools.FieldRef<F> StaticRef<F>(FieldInfo field)
        {
            if (field == null)
            {
                return null;
            }

            try
            {
                return AccessTools.StaticFieldRefAccess<F>(field);
            }
            catch (Exception ex)
            {
                Logger.LogError($"SpiderTeams could not bind {field.DeclaringType?.Name}.{field.Name}: {ex.Message}");
                return null;
            }
        }

        private static AccessTools.FieldRef<T, F> InstanceRef<T, F>(FieldInfo field)
        {
            if (field == null)
            {
                return null;
            }

            try
            {
                return AccessTools.FieldRefAccess<T, F>(field);
            }
            catch (Exception ex)
            {
                Logger.LogError($"SpiderTeams could not bind {field.DeclaringType?.Name}.{field.Name}: {ex.Message}");
                return null;
            }
        }
    }
}
