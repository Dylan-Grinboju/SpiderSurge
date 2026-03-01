using HarmonyLib;
using TMPro;
using I2.Loc;
using UnityEngine;
using System.Reflection;
using Doozy.Engine.UI;
using System.Collections;
using System.Text.RegularExpressions;

namespace SpiderSurge;

[HarmonyPatch(typeof(SurvivalModeHud), "ShowPerkChoicesClientRpc")]
public class SurvivalModeHud_ShowPerkChoicesClientRpc_Patch
{
    [HarmonyPostfix]
    public static void Postfix(SurvivalModeHud __instance)
    {
        if (!SurgeGameModeManager.IsSurgeRunActive) return;
        if (PerksManager.Instance is null) return;

        // Access perkChoiceView using reflection since it might be private
        var viewField = typeof(SurvivalModeHud).GetField("perkChoiceView", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        if (viewField is null) return;

        if (viewField.GetValue(__instance) is not Component view) return;

        // Find the Heading child
        var heading = view.transform.Find("Heading");
        if (heading is null)
        {
            // Try recursive find if direct child fails, though screenshot suggests direct child
            heading = view.transform.Find("View - PerkChoice/Heading");
            if (heading is null)
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

        if (heading is null) return;

        var tmpro = heading.GetComponent<TextMeshProUGUI>();
        var localize = heading.GetComponent<Localize>();

        if (tmpro is null) return;

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
            localize?.enabled = false;
            tmpro.text = specialTitle;
        }
        else
        {
            if (localize is not null)
            {
                localize.enabled = true;
                // Force update to ensure text resets to "You must choose!"
                localize.OnLocalize(true);
            }
        }
    }
}

[HarmonyPatch(typeof(SurvivalModeHud), "ActivateChoiseViewTimer")]
public class SurvivalModeHud_ActivateChoiseViewTimer_Patch
{
    [HarmonyPrefix]
    public static bool Prefix(SurvivalModeHud __instance, ref IEnumerator __result)
    {
        if (SurgeGameModeManager.IsSurgeRunActive && ModConfig.UnlimitedPerkChoosingTime)
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
    private static readonly Regex s_trailingDigits = new(@"\d+$", RegexOptions.Compiled);

    [HarmonyPostfix]
    public static void Postfix(PerkChoiseTimer __instance)
    {
        if (!SurgeGameModeManager.IsSurgeRunActive || !ModConfig.UnlimitedPerkChoosingTime) return;

        if (_timerTextField?.GetValue(__instance) is not TextMeshProUGUI timerText || string.IsNullOrEmpty(timerText.text)) return;

        timerText.text = s_trailingDigits.Replace(timerText.text, "∞");
    }
}
