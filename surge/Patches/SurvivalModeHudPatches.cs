using HarmonyLib;
using TMPro;
using I2.Loc;
using UnityEngine;
using System.Reflection;
using Doozy.Engine.UI;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace SpiderSurge
{
    [HarmonyPatch(typeof(SurvivalModeHud), "ShowPerkChoicesClientRpc")]
    public class SurvivalModeHud_ShowPerkChoicesClientRpc_Patch
    {
        [HarmonyPostfix]
        public static void Postfix(SurvivalModeHud __instance)
        {
            if (!SurgeGameModeManager.IsSurgeRunActive) return;
            if (PerksManager.Instance == null) return;

            PerkConfirmButton.Build(__instance);
            PerkShopPanel.Build(__instance);

            // Access perkChoiceView using reflection since it might be private
            var viewField = typeof(SurvivalModeHud).GetField("perkChoiceView", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (viewField == null) return;

            var view = viewField.GetValue(__instance) as Component;
            if (view == null) return;

            // Find the Heading child
            var heading = view.transform.Find("Heading");
            if (heading == null)
            {
                // Try recursive find if direct child fails, though screenshot suggests direct child
                heading = view.transform.Find("View - PerkChoice/Heading");
                if (heading == null)
                {
                    // Fallback: search children
                    var headings = view.GetComponentsInChildren<TextMeshProUGUI>(true);
                    foreach (var h in headings)
                    {
                        if (h.name == "Heading")
                        {
                            heading = h.transform;
                            break;
                        }
                    }
                }
            }

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

    // A client that joins during a perk choice builds the cards before the host's Surge state arrives, so they are redrawn then
    public static class PerkChoiceRefresh
    {
        // The HUD keeps these private and Mono enforces that at run time, so they are reached through Harmony's field access
        private static readonly AccessTools.FieldRef<SurvivalModeHud, List<SurvivalModifierChoiceCard>> Cards = AccessTools.FieldRefAccess<SurvivalModeHud, List<SurvivalModifierChoiceCard>>("survivalModifierChoiceCards");
        private static readonly AccessTools.FieldRef<SurvivalModeHud, PerkChoiseTimer> ChoiceTimer = AccessTools.FieldRefAccess<SurvivalModeHud, PerkChoiseTimer>("choiseTimerComponent");
        private static readonly AccessTools.FieldRef<SurvivalModifierChoiceCard, int> CardIndex = AccessTools.FieldRefAccess<SurvivalModifierChoiceCard, int>("cardIndex");

        public static void Refresh()
        {
            var hud = SurvivalModeHud.instance;
            if (hud == null || hud.perkChoiceView == null || !hud.perkChoiceView.IsVisible)
            {
                return;
            }
            var cards = Cards(hud);
            if (cards != null)
            {
                foreach (var card in cards)
                {
                    if (card != null && card.modifier != null)
                    {
                        SurvivalModifierChoiceCard_SetupCard_Patch.Postfix(card, card.modifier, card.level, CardIndex(card), false);
                    }
                }
            }
            SurvivalModeHud_ShowPerkChoicesClientRpc_Patch.Postfix(hud);
            var timer = ChoiceTimer(hud);
            if (timer != null)
            {
                PerkChoiseTimer_SetTimerValue_Patch.Postfix(timer);
            }
        }
    }

    // A new perk screen draws new shop items; a reroll re-shows the cards and keeps them, so a reroll can't be used to fish for items
    [HarmonyPatch(typeof(SurvivalModeHud), "ShowPerkChoices")]
    public class SurvivalModeHud_ShowPerkChoices_Patch
    {
        [HarmonyPrefix]
        public static void Prefix()
        {
            if (SurgeGameModeManager.IsSurgeRunActive && SurgeMeter.Enabled && !AbilitySelection.IsChoosing && !PerkShop.RerollInProgress) PerkShop.RollOffer();
        }
    }

    [HarmonyPatch(typeof(SurvivalModeHud), "ActivateChoiseViewTimer")]
    public class SurvivalModeHud_ActivateChoiseViewTimer_Patch
    {
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
        private static readonly FieldInfo _timerTextField = typeof(PerkChoiseTimer).GetField("timerTextComponent", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly Regex s_trailingDigits = new Regex(@"\d+$", RegexOptions.Compiled);

        [HarmonyPostfix]
        public static void Postfix(PerkChoiseTimer __instance)
        {
            if (!PerkConfirm.Active && !(SurgeGameModeManager.IsSurgeRunActive && AbilitySelection.IsChoosing)) return;

            var timerText = _timerTextField?.GetValue(__instance) as TextMeshProUGUI;
            if (timerText == null || string.IsNullOrEmpty(timerText.text)) return;

            timerText.text = s_trailingDigits.Replace(timerText.text, "∞");
        }
    }
}
