using HarmonyLib;
using TMPro;
using I2.Loc;
using UnityEngine;
using Doozy.Engine.UI;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using SpiderSurge.Common;

namespace SpiderSurge
{
    [HarmonyPatch(typeof(SurvivalModeHud), "ShowPerkChoicesClientRpc")]
    public class SurvivalModeHud_ShowPerkChoicesClientRpc_Patch
    {
        private static SurvivalModifierChoiceCard _builtFor;

        [HarmonyPostfix]
        public static void Postfix(SurvivalModeHud __instance)
        {
            Apply(__instance, false);
        }

        // The host runs this twice per show (Netcode runs its own copy of the RPC inside the send) and every machine runs it for a resend aimed at a joining player, so the widgets are only built for new cards
        internal static void Apply(SurvivalModeHud __instance, bool rebuild)
        {
            PerkChoiceMenu.Attach(__instance);
            if (!SurgeGameModeManager.IsSurgeRunActive) return;
            PerkChoiceHud.Attach(__instance);
            if (PerksManager.Instance == null) return;

            PerkChoiceWidget.HideStaleCards(__instance);
            var cards = PerkChoiceWidget.Cards(__instance);
            var first = cards.Count > 0 ? cards[0] : null;
            if (first != _builtFor)
            {
                _builtFor = first;
                PerkChoiceWidget.ResetChoice();
                rebuild = true;
            }
            if (rebuild)
            {
                PerkConfirmButton.Build(__instance);
                PerkShopPanel.Build(__instance);
                PerkHint.Build(__instance);
                PerkChoiceMenu.LinkNavigation(__instance);
            }
            MapBonusLabels.Refresh();

            var view = __instance.perkChoiceView;
            if (view == null) return;

            var heading = view.transform.Find("Heading");
            if (heading == null) return;

            var tmpro = heading.GetComponent<TextMeshProUGUI>();
            var localize = heading.GetComponent<Localize>();

            if (tmpro == null) return;

            bool isSpecialRound = false;
            string specialTitle = "";

            if (PerksManager.Instance.IsFirstNormalPerkSelection)
            {
                isSpecialRound = true;
                specialTitle = "CHOOSE YOUR ABILITY";
            }
            else if (PerksManager.Instance.IsUltSwapPerkSelection)
            {
                isSpecialRound = true;
                specialTitle = "SWITCH ULTIMATE";
            }
            else if (PerksManager.Instance.IsUltUpgradePerkSelection)
            {
                isSpecialRound = true;
                specialTitle = "CHOOSE YOUR ULTIMATE";
            }

            if (isSpecialRound)
            {
                if (localize != null) localize.enabled = false;
                tmpro.text = specialTitle;
            }
            else
            {
                if (localize != null)
                {
                    localize.enabled = true;
                    // Force update to ensure text resets to "You must choose!"
                    localize.OnLocalize(true);
                }
            }
        }
    }

    // A client that joins during a perk choice builds the cards before the host's Surge state arrives, so they are redrawn then, while the view may still be animating in
    public static class PerkChoiceRefresh
    {
        // The HUD keeps these private and Mono enforces that at run time, so they are reached through Harmony's field access
        private static readonly AccessTools.FieldRef<SurvivalModeHud, PerkChoiseTimer> ChoiceTimer = AccessTools.FieldRefAccess<SurvivalModeHud, PerkChoiseTimer>("choiseTimerComponent");

        public static void Refresh()
        {
            var hud = SurvivalModeHud.instance;
            if (hud == null || hud.perkChoiceView == null || !(hud.perkChoiceView.IsVisible || hud.perkChoiceView.IsShowing))
            {
                return;
            }
            foreach (var card in PerkChoiceWidget.Cards(hud))
            {
                if (card.modifier != null)
                {
                    if (card.modifierIconImage != null && card.modifier.data != null) card.modifierIconImage.sprite = card.modifier.data.icon;
                    SurvivalModifierChoiceCard_SetupCard_Patch.Postfix(card, card.modifier, card.level, GameFields.CardIndex(card), false);
                }
            }
            SurvivalModeHud_ShowPerkChoicesClientRpc_Patch.Apply(hud, true);
            var timer = ChoiceTimer(hud);
            if (timer != null)
            {
                PerkChoiseTimer_SetTimerValue_Patch.Postfix(timer, false);
            }
        }
    }

    // A new perk screen draws new shop items; a reroll re-shows the cards and keeps them, so a reroll can't be used to fish for items
    [HarmonyPatch(typeof(SurvivalModeHud), "ShowPerkChoices")]
    public class SurvivalModeHud_ShowPerkChoices_Patch
    {
        [HarmonyPrefix]
        public static void Prefix(List<Modifier> modifiers)
        {
            if (SurgeGameModeManager.IsSurgeRunActive && ChaosWallet.Enabled && !AbilitySelection.IsChoosing && !PerkShop.RerollInProgress) PerkShop.BeginVisit(modifiers);
        }
    }

    [HarmonyPatch(typeof(SurvivalModeHud), "PerkChoiceSubmitClientRpc")]
    public class SurvivalModeHud_PerkChoiceSubmitClientRpc_Patch
    {
        [HarmonyPostfix]
        public static void Postfix()
        {
            if (SurgeGameModeManager.IsSurgeRunActive) PerkChoiceWidget.MarkChoiceMade();
        }
    }

    [HarmonyPatch(typeof(SurvivalModeHud), "ActivateChoiseViewTimer")]
    public class SurvivalModeHud_ActivateChoiseViewTimer_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(ref IEnumerator __result) => __result = PauseWithMenu(__result);

        // Preserve the real-time countdown while a player uses the pause menu.
        private static IEnumerator PauseWithMenu(IEnumerator timer)
        {
            while (timer.MoveNext())
            {
                if (timer.Current is WaitForSecondsRealtime wait)
                {
                    float remaining = wait.waitTime;
                    while (remaining > 0f)
                    {
                        yield return null;
                        if (HudController.instance == null || !HudController.instance.paused) remaining -= Time.unscaledDeltaTime;
                    }
                }
                else yield return timer.Current;
                while (HudController.instance != null && HudController.instance.paused) yield return null;
            }
        }

        [HarmonyPrefix]
        public static bool Prefix(SurvivalModeHud __instance, ref IEnumerator __result)
        {
            // A reroll re-shows the cards while the first countdown still runs, so it must not start a second one
            if (SurgeGameModeManager.IsSurgeRunActive && (AbilitySelection.IsChoosing || ModConfig.UnlimitedPerkChoosingTime || PerkShop.RerollInProgress))
            {
                __result = EmptyEnumerator();
                return false;
            }
            return true;
        }

        private static IEnumerator EmptyEnumerator()
        {
            yield break;
        }
    }

    [HarmonyPatch(typeof(PerkChoiseTimer), "SetTimerValue")]
    public class PerkChoiseTimer_SetTimerValue_Patch
    {
        private static readonly AccessTools.FieldRef<PerkChoiseTimer, TextMeshProUGUI> TimerText = AccessTools.FieldRefAccess<PerkChoiseTimer, TextMeshProUGUI>("timerTextComponent");
        private static readonly Regex s_trailingDigits = new Regex(@"\d+$", RegexOptions.Compiled);

        // The game rebuilds the text every frame while the cards show, so once it reads ∞ the rebuild is skipped
        [HarmonyPrefix]
        public static bool Prefix(PerkChoiseTimer __instance, out bool __state)
        {
            __state = false;
            if (!PerkConfirm.Active) return true;
            var timerText = TimerText(__instance);
            string text = timerText != null ? timerText.text : null;
            __state = !string.IsNullOrEmpty(text) && text.EndsWith("∞", System.StringComparison.Ordinal);
            return !__state;
        }

        // Text that already ends in ∞ has no digits left to replace
        [HarmonyPostfix]
        public static void Postfix(PerkChoiseTimer __instance, bool __state)
        {
            if (__state || !PerkConfirm.Active) return;

            var timerText = TimerText(__instance);
            if (timerText == null || string.IsNullOrEmpty(timerText.text)) return;

            timerText.text = s_trailingDigits.Replace(timerText.text, "∞");
        }
    }
}
