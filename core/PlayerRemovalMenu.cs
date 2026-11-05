using System.Collections.Generic;
using System.Linq;
using Doozy.Engine.UI;
using Doozy.Engine.UI.Input;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SpiderSurge.Common
{
    internal static class PlayerRemovalMenu
    {
        private static readonly AccessTools.FieldRef<ModsMenu, UIButton> BackButtonOf =
            AccessTools.FieldRefAccess<ModsMenu, UIButton>("backButton");
        private static UIPopup _popup;
        private static int _backFrame = -1;

        internal static bool HandlePause()
        {
            if (_backFrame == Time.frameCount) return true;
            if (_popup == null || _popup.IsHidden) return false;
            _backFrame = Time.frameCount;
            Hide();
            return true;
        }

        internal static void Hide()
        {
            if (_popup != null) _popup.Hide();
        }

        internal static void Show()
        {
            var hud = HudController.instance;
            if (hud == null || !hud.paused || !PlayerRemoval.InGame) return;
            var returnTo = hud.removePlayers.GetComponent<UIButton>();
            var menu = Announcer.ModsPopup("REMOVE PLAYER", returnTo);
            menu.CreateParagraph(Net.IsRemoteClient
                ? "Choose one of your local players to remove. The other players will stay in the game."
                : "Choose a player to remove. The other players will stay in the game.");
            var controls = new List<Button>();
            foreach (var player in (Players.All ?? new List<PlayerController>()).ToArray())
            {
                if (player == null || !player.IsSpawned || (Net.IsRemoteClient && !Players.IsLocal(player))) continue;
                ulong objectId = player.NetworkObjectId;
                string label = Label(player);
                var button = ((ModsMenu)menu).CreateButton(label, () =>
                {
                    menu.selectOnDismiss = null;
                    menu.popup.Hide();
                    Confirm(player, objectId, label, returnTo);
                });
                controls.Add(button);
            }
            if (controls.Count == 0) menu.CreateParagraph("You have no local players to remove.");
            Link(menu, controls);
        }

        private static string Label(PlayerController player)
        {
            string name = Players.NameOf(player);
            string suffix = " P" + (player.playerID.Value + 1);
            if (name.EndsWith(suffix, System.StringComparison.Ordinal)) name = name.Substring(0, name.Length - suffix.Length);
            return $"P{player.playerID.Value + 1} — <noparse>{name}</noparse>";
        }

        private static void Confirm(PlayerController player, ulong objectId, string label, UIButton returnTo)
        {
            string problem = PlayerRemoval.Problem(player, Net.LocalClientId, objectId);
            if (problem != null) { Announcer.InformationPopup(problem); return; }
            var menu = Announcer.ModsPopup("REMOVE PLAYER?", returnTo);
            menu.CreateParagraph(label);
            menu.CreateParagraph("Remove this player from the current game? Other players will keep playing.");
            var cancel = ((ModsMenu)menu).CreateButton("Cancel", () => menu.popup.Hide());
            var confirm = ((ModsMenu)menu).CreateButton("Remove player", () =>
            {
                menu.popup.Hide();
                PlayerRemoval.Request(player, objectId);
            });
            Link(menu, new List<Button> { cancel, confirm });
        }

        private static void Link(ModsMenuPopup menu, List<Button> buttons)
        {
            _popup = menu.popup;
            menu.popup.HideBehavior.OnFinished.Event.AddListener(() =>
            {
                if (_popup == menu.popup) _popup = null;
            });
            var layout = menu.objectParent.GetComponent<VerticalLayoutGroup>();
            if (layout != null)
            {
                layout.childControlWidth = layout.childControlHeight = true;
                layout.childForceExpandWidth = layout.childForceExpandHeight = false;
                layout.spacing = 10f;
            }
            float width = Mathf.Min(600f, ((RectTransform)menu.objectParent).rect.width - 32f);
            foreach (Transform row in menu.objectParent)
            {
                var text = row.GetComponentInChildren<TMP_Text>();
                if (text == null) continue;
                text.enableAutoSizing = false;
                text.fontSize = row.GetComponent<Button>() != null ? 28f : 22f;
                text.enableWordWrapping = true;
                var size = row.GetComponent<LayoutElement>() ?? row.gameObject.AddComponent<LayoutElement>();
                size.preferredWidth = width;
                size.preferredHeight = Mathf.Max(48f, text.GetPreferredValues(text.text, width - 32f, 0f).y + 16f);
            }
            var backButton = BackButtonOf(menu);
            var back = backButton != null ? backButton.GetComponent<Selectable>() : null;
            for (int i = 0; i < buttons.Count; i++)
                buttons[i].navigation = new Navigation
                {
                    mode = Navigation.Mode.Explicit,
                    selectOnUp = i == 0 ? back : buttons[i - 1],
                    selectOnDown = i == buttons.Count - 1 ? back : buttons[i + 1]
                };
            if (back != null)
                back.navigation = new Navigation
                {
                    mode = Navigation.Mode.Explicit,
                    selectOnUp = buttons.LastOrDefault(),
                    selectOnDown = buttons.FirstOrDefault()
                };
            menu.popup.ShowBehavior.OnFinished.Event.AddListener(() =>
            {
                if (buttons.Count > 0 && buttons[0] != null) buttons[0].Select();
                else if (backButton != null) backButton.SelectButton();
            });
        }
    }

    [HarmonyPatch(typeof(HudController), nameof(HudController.Pause))]
    internal static class PlayerRemovalPausePatch
    {
        private static bool Prefix() => !PlayerRemovalMenu.HandlePause();
    }

    [HarmonyPatch(typeof(BackButton), nameof(BackButton.Execute))]
    internal static class PlayerRemovalBackPatch
    {
        private static bool Prefix() => !PlayerRemovalMenu.HandlePause();
    }

    [HarmonyPatch(typeof(HudController), nameof(HudController.Resume))]
    internal static class PlayerRemovalResumePatch
    {
        private static void Prefix() => PlayerRemovalMenu.Hide();
    }
}
