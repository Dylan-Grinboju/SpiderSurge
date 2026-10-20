using System.Collections;
using SpiderSurge.Common;
using UnityEngine;
using UnityEngine.UI;

namespace SpiderSurge
{
    // Shared handling for the objects Surge adds to the perk choice view: the shop panel and the confirm button
    public static class PerkChoiceWidget
    {
        // The RPC postfix runs twice per show on the host, so every old copy goes, renamed so the next pass can't find it again
        public static void Remove(SurvivalModeHud hud, string name)
        {
            if (hud == null || hud.perkChoiceView == null) return;
            Transform parent = hud.perkChoiceView.transform;
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                Transform child = parent.GetChild(i);
                if (child.name != name) continue;
                child.name = name + " (old)";
                child.SetParent(null, false);
                foreach (MonoBehaviour behaviour in child.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (behaviour != null && behaviour.GetType().Assembly == typeof(PerkChoiceWidget).Assembly) behaviour.enabled = false;
                }
                SettingsPage.StopClickAnimations(child.gameObject);
                if (hud.isActiveAndEnabled) hud.StartCoroutine(DestroyAfterFrame(child.gameObject));
                else Object.Destroy(child.gameObject);
            }
        }

        // Out of the canvas the old object neither draws nor takes input, but the button Doozy is still clicking stays alive for the rest of the frame; its tweens are killed again once the frame is over
        private static IEnumerator DestroyAfterFrame(GameObject widget)
        {
            yield return new WaitForEndOfFrame();
            if (widget == null) yield break;
            SettingsPage.StopClickAnimations(widget);
            Object.Destroy(widget);
        }

        // Hides the card parts a widget made from the perk card prefab has no use for; the card component itself goes afterwards
        public static void HideCardParts(SurvivalModifierChoiceCard card, bool keepIcon, bool keepDescription)
        {
            if (card == null) return;
            if (!keepIcon && card.modifierIconImage != null) card.modifierIconImage.gameObject.SetActive(false);
            if (!keepDescription && card.perkDescriptionText != null) card.perkDescriptionText.gameObject.SetActive(false);
            if (card.mapImage != null) card.mapImage.gameObject.SetActive(false);
            if (card.mapNameText != null) card.mapNameText.gameObject.SetActive(false);
            if (card.twitchVotes != null) card.twitchVotes.SetActive(false);
            if (card.voteImages != null)
            {
                foreach (Image vote in card.voteImages)
                {
                    if (vote == null) continue;
                    if (vote.transform.parent != null && vote.transform.parent != card.transform) vote.transform.parent.gameObject.SetActive(false);
                    else vote.gameObject.SetActive(false);
                }
            }
        }
    }
}
