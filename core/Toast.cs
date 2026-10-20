using System;
using Doozy.Engine.UI;
using TMPro;
using UnityEngine;
using Logger = Silk.Logger;

namespace SpiderSurge.Common
{
    internal static class Toast
    {
        // Shows a popup on this machine only, the same way Announcer does locally.
        public static void Show(string text, Color color, bool important = false)
        {
            try
            {
                UIPopup popup = UIPopup.GetPopup(important ? "ImportantPopUp" : "TextPopUp");
                if (popup == null)
                {
                    return;
                }

                popup.Data.SetLabelsTexts(new string[] { text });
                if (popup.Data.Labels.Count > 0 && popup.Data.Labels[0] != null)
                {
                    var label = popup.Data.Labels[0].GetComponent<TextMeshProUGUI>();
                    if (label != null)
                    {
                        label.color = color;
                    }
                }
                UIPopupManager.ShowPopup(popup, true, false);
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"SpiderSurge could not show a popup: {ex.Message}");
            }
        }
    }
}
