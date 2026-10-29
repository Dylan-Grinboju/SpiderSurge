using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SpiderSurge.Common
{
    // The lobby notice offers the update and stays open through its download; only its own buttons close it
    public static partial class ModUpdater
    {
        private static ModsMenuPopup popup;
        private static UpdateRelease offered;
        private static Button firstButton;
        private static bool popupPending;
        private static string downloadProblem;

        private static void ShowOffer(UpdateRelease found)
        {
            if (resultPending) ReadPreviousResult();
            var shown = Announcer.ModsPopup("");
            if (shown == null) return;
            popup = shown;
            offered = found;
            downloadProblem = null;
            foreach (var child in shown.GetComponentsInChildren<Transform>(true))
                if (child.name == "Button -Back") child.gameObject.SetActive(false);
            shown.popup.ShowBehavior.OnFinished.Event.AddListener(() => { if (popup == shown) SelectFirstButton(); });
            BuildPopup();
        }

        // Rebuilt after the click that changed the state has finished, since that click's button is relabelled
        private static void RefreshPopup()
        {
            if (popup == null || popupPending) return;
            popupPending = true;
            MainThread.Enqueue(BuildPopup);
        }

        // Existing rows are relabelled rather than replaced, so a clicked button finishes its press and keeps its highlight
        private static void BuildPopup()
        {
            popupPending = false;
            if (popup == null) return;
            var rows = popup.objectParent.Cast<Transform>().Select(row => row.GetComponent<Button>()).ToList();
            var buttons = new List<Button>();
            void Add(string label, UnityAction onClick)
            {
                if (buttons.Count >= rows.Count)
                {
                    buttons.Add(((ModsMenu)popup).CreateButton(label, onClick));
                    return;
                }
                var row = rows[buttons.Count];
                row.onClick.RemoveAllListeners();
                row.onClick.AddListener(onClick);
                row.GetComponentInChildren<TMP_Text>().text = label;
                buttons.Add(row);
            }
            string version = offered.Version;
            if (downloading)
            {
                SetPopupText("Downloading SpiderSurge " + version + "...");
                Add("Cancel", () => { downloadCancellation?.Cancel(); ClosePopup(); });
            }
            else if (staged)
            {
                SetPopupText("SpiderSurge " + version + " is downloaded.\nRestart the game to finish the update.");
                Add("OK", ClosePopup);
            }
            else if (downloadProblem != null)
            {
                SetPopupText("Could not download SpiderSurge " + version + ".\n" + downloadProblem + "\nYour installed mod has not changed.");
                Add("Try again", () => StartDownload(offered));
                Add("Close", ClosePopup);
            }
            else
            {
                bool installable = offered.TryGetInstallableAsset(out _, out _);
                SetPopupText("SpiderSurge " + version + " is available."
                    + (!installable ? "\nTo install it by hand, download SpiderSurge.dll from its release page and replace the one in SpiderHeck's Silk/Mods folder while the game is closed."
                        : resultPending ? "\nThe installer from your last session is still running. Install it from Mods > SpiderSurge > Settings > Advanced > Updates in a minute." : ""));
                if (installable && !resultPending) Add("Install now", () => StartDownload(offered));
                Add("Release notes", () => OpenUrl(offered.PageUrl));
                Add("Later", ClosePopup);
            }
            foreach (var extra in rows.Skip(buttons.Count)) Object.DestroyImmediate(extra.gameObject);
            for (int i = 0; i < buttons.Count; i++)
                buttons[i].navigation = new Navigation { mode = Navigation.Mode.Explicit, selectOnUp = buttons[(i + buttons.Count - 1) % buttons.Count], selectOnDown = buttons[(i + 1) % buttons.Count] };
            firstButton = buttons[0];
            FitPopup(buttons);
            SelectFirstButton();
        }

        // Shrinks the full-screen mods popup to its text above its buttons, padded inside the frame that overhangs the body; rows are summed because a queued popup is inactive and has no layout yet
        private static void FitPopup(List<Button> buttons)
        {
            const float width = 440, pad = 16, gap = 10;
            var content = (RectTransform)popup.objectParent;
            var viewport = (RectTransform)content.parent;
            var scroll = (RectTransform)viewport.parent;
            var body = (RectTransform)scroll.parent;
            var frame = (RectTransform)body.Find("frame");
            var message = (RectTransform)body.Find("Message");
            float textHeight = message.GetComponent<TextMeshProUGUI>().GetPreferredValues(width - 2 * pad, 0).y;
            var rows = content.GetComponent<VerticalLayoutGroup>();
            float rowsHeight = buttons.Sum(b => ((RectTransform)b.transform).rect.height) + rows.spacing * (buttons.Count - 1) + rows.padding.vertical;
            float scale = scroll.localScale.y;
            body.anchorMin = body.anchorMax = new Vector2(0.5f, 0.5f);
            body.sizeDelta = new Vector2(width, 2 * pad + textHeight + gap + rowsHeight * scale - (frame.offsetMax.y - frame.offsetMin.y));
            message.anchorMin = new Vector2(0, 1);
            message.anchorMax = Vector2.one;
            message.pivot = new Vector2(0.5f, 1);
            message.sizeDelta = new Vector2(-2 * pad, textHeight);
            message.anchoredPosition = new Vector2(0, frame.offsetMax.y - pad);
            viewport.sizeDelta = Vector2.zero;
            scroll.anchorMin = scroll.anchorMax = scroll.pivot = new Vector2(0.5f, 0);
            scroll.sizeDelta = new Vector2((width - 2 * pad) / scale, rowsHeight);
            scroll.anchoredPosition = new Vector2(0, frame.offsetMin.y + pad);
        }

        private static void SetPopupText(string text) => popup.popup.Data.SetLabelsTexts(text);

        private static void SelectFirstButton()
        {
            if (firstButton != null && EventSystem.current != null) EventSystem.current.SetSelectedGameObject(firstButton.gameObject);
        }

        private static void ClosePopup()
        {
            var closing = popup;
            popup = null;
            if (closing != null) closing.popup.Hide();
        }
    }
}
