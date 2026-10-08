using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Logger = Silk.Logger;

namespace SpiderTeams
{
    // Lives on the Mods popup and rebuilds only the objects it created, so Silk's own lines below it survive.
    public class SpiderTeamsMenuPage : MonoBehaviour
    {
        private ModsMenuPopup _menu;
        private readonly List<GameObject> _created = new List<GameObject>();
        private readonly List<PlayerController> _members = new List<PlayerController>();
        private int _baseIndex;
        private bool _subscribed;
        private Coroutine _pending;

        public void Build(ModsMenuPopup menu)
        {
            DestroyCreated();
            _menu = menu;
            _baseIndex = menu.objectParent != null ? menu.objectParent.childCount : 0;
            if (!_subscribed)
            {
                ModConfig.Changed += RequestRebuild;
                TeamManager.RosterChanged += RequestRebuild;
                _subscribed = true;
            }
            Rebuild(-1);
        }

        private void OnDestroy()
        {
            if (_subscribed)
            {
                ModConfig.Changed -= RequestRebuild;
                TeamManager.RosterChanged -= RequestRebuild;
                _subscribed = false;
            }
        }

        private void RequestRebuild()
        {
            if (this == null || !isActiveAndEnabled || _pending != null)
            {
                return;
            }
            _pending = StartCoroutine(RebuildNextFrame());
        }

        private IEnumerator RebuildNextFrame()
        {
            yield return null;
            _pending = null;
            Rebuild(SelectedIndex());
        }

        private void Rebuild(int selectIndex)
        {
            try
            {
                DestroyCreated();
                if (_menu == null || _menu.objectParent == null)
                {
                    return;
                }

                AddContent();
                for (int i = 0; i < _created.Count; i++)
                {
                    if (_created[i] != null)
                    {
                        _created[i].transform.SetSiblingIndex(_baseIndex + i);
                    }
                }
                Select(selectIndex);
            }
            catch (Exception ex)
            {
                Logger.LogError($"SpiderTeams menu page failed: {ex}");
            }
        }

        private void AddContent()
        {
            var manager = TeamManager.Instance;
            bool locked = manager != null && manager.MatchLocked;
            var network = NetworkManager.Singleton;
            bool remoteClient = network != null && network.IsListening && !network.IsHost;

            AddParagraph($"<size=200%>\n<b><color={Consts.Formatting.Gold}>SpiderTeams</color></b></size>");
            AddParagraph($"<size=115%><b>Team versus:</b> {(ModConfig.TeamsEnabled ? Colored("Enabled", Consts.Formatting.Green) : Colored("Disabled", Consts.Formatting.Red))}</size>");
            AddParagraph(Note(PickHint()));
            if (remoteClient)
            {
                AddParagraph(Warning("Teams only apply when you host."));
            }

            if (locked)
            {
                AddParagraph(Warning("Team mode, team count and teams are locked during a match."));
            }
            else
            {
                AddCheckbox("Team versus", ModConfig.TeamsEnabled, ModConfig.SetTeamsEnabled);
                AddParagraph($"<size=115%><b>Teams:</b> {ModConfig.TeamCount}</size>");
                AddButton("Fewer teams", () => ModConfig.SetTeamCount(ModConfig.TeamCount - 1));
                AddButton("More teams", () => ModConfig.SetTeamCount(ModConfig.TeamCount + 1));
            }

            AddSpacer();
            AddButton($"Friendly fire: {FriendlyFireLabel(ModConfig.FriendlyFire)}", CycleFriendlyFire);
            AddParagraph(Note(FriendlyFireDescription(ModConfig.FriendlyFire)));

            AddSpacer();
            AddCheckbox("Recolor spiders by team (during matches)", ModConfig.RecolorSpiders, ModConfig.SetRecolorSpiders);
            AddCheckbox("Show a team marker above spiders", ModConfig.ShowMarker, ModConfig.SetShowMarker);
            AddCheckbox("Spawn teammates together", ModConfig.GroupTeammates, ModConfig.SetGroupTeammates);

            AddSpacer();
            AddButton($"Team button (controller): {GamepadLabel()}", CycleGamepadBinding);
            AddButton($"Team button (keyboard): {KeyboardLabel()}", CycleKeyboardBinding);

            if (manager != null && TeamManager.IsHost && ModConfig.TeamsEnabled)
            {
                AddRoster(manager, locked);
            }

            AddSpacer();
            AddCheckbox("Check for updates", ModConfig.CheckForUpdates, ModConfig.SetCheckForUpdates);
            AddButton("Reload from file", ModConfig.ReloadFromFile);
            AddButton("Reset to defaults", ModConfig.ResetToDefaults);
            AddParagraph($"<size=85%><color={Consts.Formatting.Gray}>Config file: <noparse>{ModConfig.ConfigPath}</noparse></color></size>");
            AddSpacer();
        }

        private void AddRoster(TeamManager manager, bool locked)
        {
            var players = TeamManager.Players;
            if (players == null || players.Count == 0)
            {
                return;
            }

            AddSpacer();
            AddParagraph("<size=130%><b>Teams</b></size>");
            int count = manager.TeamCount;
            for (int team = 0; team < count; team++)
            {
                manager.CollectMembers(team, _members);
                string hex = TeamPalette.Hex(TeamPalette.TeamColor(team));
                string size = Plural(_members.Count, "player");
                string score = locked && manager.TeamModeThisMatch ? ", " + Plural(manager.GetTeamScore(team), "point") : string.Empty;
                AddParagraph($"<size=115%><color=#{hex}><b>{TeamPalette.TeamName(team)}</b></color> ({size}{score})</size>");

                for (int i = 0; i < _members.Count; i++)
                {
                    PlayerController member = _members[i];
                    if (locked)
                    {
                        AddParagraph($"<color=#{hex}>{TeamManager.RichName(member)}</color>");
                    }
                    else
                    {
                        string next = TeamPalette.TeamName((team + 1) % count);
                        AddButton($"{TeamManager.RichName(member)} > {next}", () => MovePlayer(member));
                    }
                }
            }

            if (!locked)
            {
                AddButton("Shuffle teams", () => manager.Shuffle());
                AddButton("Balance teams", () => manager.Balance());
            }
        }

        private static void MovePlayer(PlayerController player)
        {
            var manager = TeamManager.Instance;
            if (player != null && manager != null && manager.CycleTeam(player))
            {
                Announcements.QueuePick(player.playerID.Value);
            }
        }

        private static void CycleFriendlyFire()
        {
            switch (ModConfig.FriendlyFire)
            {
                case FriendlyFireMode.Push:
                    ModConfig.SetFriendlyFire(FriendlyFireMode.Ignore);
                    break;
                case FriendlyFireMode.Ignore:
                    ModConfig.SetFriendlyFire(FriendlyFireMode.Damage);
                    break;
                default:
                    ModConfig.SetFriendlyFire(FriendlyFireMode.Push);
                    break;
            }
        }

        private static string FriendlyFireLabel(FriendlyFireMode mode)
        {
            switch (mode)
            {
                case FriendlyFireMode.Ignore:
                    return "Ignore";
                case FriendlyFireMode.Damage:
                    return "Damage";
                default:
                    return "Push";
            }
        }

        private static string FriendlyFireDescription(FriendlyFireMode mode)
        {
            switch (mode)
            {
                case FriendlyFireMode.Ignore:
                    return "Teammates' hits do nothing at all.";
                case FriendlyFireMode.Damage:
                    return "Teammates' hits work as normal and can kill.";
                default:
                    return "Teammates' hits knock you back, but never kill you or break your shield.";
            }
        }

        private static void CycleGamepadBinding()
        {
            ModConfig.SetCycleTeamGamepad(NextPreset(Consts.Bindings.GamepadPaths, ModConfig.CycleTeamGamepad));
        }

        private static void CycleKeyboardBinding()
        {
            ModConfig.SetCycleTeamKeyboard(NextPreset(Consts.Bindings.KeyboardPaths, ModConfig.CycleTeamKeyboard));
        }

        private static string GamepadLabel()
        {
            return PresetLabel(Consts.Bindings.GamepadPaths, Consts.Bindings.GamepadLabels, ModConfig.CycleTeamGamepad);
        }

        private static string KeyboardLabel()
        {
            return PresetLabel(Consts.Bindings.KeyboardPaths, Consts.Bindings.KeyboardLabels, ModConfig.CycleTeamKeyboard);
        }

        private static string PresetLabel(string[] paths, string[] labels, string current)
        {
            int index = Array.FindIndex(paths, path => string.Equals(path, current, StringComparison.OrdinalIgnoreCase));
            return index >= 0 ? labels[index] : "<noparse>" + current + "</noparse>";
        }

        private static string NextPreset(string[] paths, string current)
        {
            int index = Array.FindIndex(paths, path => string.Equals(path, current, StringComparison.OrdinalIgnoreCase));
            return paths[(index + 1) % paths.Length];
        }

        private static string PickHint()
        {
            bool gamepad = ModConfig.CycleTeamGamepad != Consts.Bindings.None;
            bool keyboard = ModConfig.CycleTeamKeyboard != Consts.Bindings.None;
            if (gamepad && keyboard)
            {
                return $"In the lobby, press {GamepadLabel()} (controller) or {KeyboardLabel()} (keyboard) to switch team.";
            }
            if (gamepad)
            {
                return $"In the lobby, press {GamepadLabel()} on a controller to switch team.";
            }
            if (keyboard)
            {
                return $"In the lobby, press {KeyboardLabel()} on the keyboard to switch team.";
            }
            return "The team buttons are off; the host can move players in the list below.";
        }

        private static string Plural(int count, string noun)
        {
            return count == 1 ? "1 " + noun : count + " " + noun + "s";
        }

        private static string Colored(string text, string hex)
        {
            return $"<color={hex}>{text}</color>";
        }

        private static string Note(string text)
        {
            return $"<size=95%><color={Consts.Formatting.Gray}>{text}</color></size>";
        }

        private static string Warning(string text)
        {
            return $"<size=95%><color={Consts.Formatting.Orange}>{text}</color></size>";
        }

        private void AddParagraph(string text)
        {
            TMP_Text paragraph = _menu.CreateParagraph(text);
            if (paragraph != null)
            {
                _created.Add(paragraph.gameObject);
            }
        }

        private void AddSpacer()
        {
            AddParagraph("<size=100%>\n </size>");
        }

        private void AddCheckbox(string label, bool value, UnityAction<bool> onToggle)
        {
            CheckboxScript checkbox = _menu.CreateCheckbox(value2 => Run(() => onToggle(value2)), label, value);
            if (checkbox != null)
            {
                _created.Add(checkbox.gameObject);
            }
        }

        private void AddButton(string label, UnityAction onClick)
        {
            Button button = ((ModsMenu)_menu).CreateButton(label, () => Run(onClick));
            if (button == null)
            {
                return;
            }

            _created.Add(button.gameObject);
            var rect = button.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.sizeDelta = new Vector2(rect.sizeDelta.x * 1.1f, rect.sizeDelta.y * 1.45f);
            }

            var text = button.GetComponentInChildren<TMP_Text>();
            if (text != null)
            {
                text.fontSize *= 1.2f;
            }
        }

        private static void Run(UnityAction action)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                Logger.LogError($"SpiderTeams menu action failed: {ex}");
            }
        }

        private void DestroyCreated()
        {
            for (int i = 0; i < _created.Count; i++)
            {
                if (_created[i] != null)
                {
                    DestroyImmediate(_created[i]);
                }
            }
            _created.Clear();
        }

        private int SelectedIndex()
        {
            var events = EventSystem.current;
            GameObject selected = events != null ? events.currentSelectedGameObject : null;
            if (selected == null)
            {
                return -1;
            }

            for (int i = 0; i < _created.Count; i++)
            {
                if (_created[i] != null && selected.transform.IsChildOf(_created[i].transform))
                {
                    return i;
                }
            }
            return -1;
        }

        // Keeps controller navigation alive: reselect the nearest control at or above the old position.
        private void Select(int index)
        {
            var events = EventSystem.current;
            if (index < 0 || events == null || _created.Count == 0)
            {
                return;
            }

            int start = Mathf.Min(index, _created.Count - 1);
            for (int distance = 0; distance < _created.Count; distance++)
            {
                if (TrySelect(events, start - distance) || TrySelect(events, start + distance))
                {
                    return;
                }
            }
        }

        private bool TrySelect(EventSystem events, int index)
        {
            if (index < 0 || index >= _created.Count || _created[index] == null)
            {
                return false;
            }

            Selectable selectable = _created[index].GetComponentInChildren<Selectable>();
            if (selectable == null || !selectable.interactable || !selectable.gameObject.activeInHierarchy)
            {
                return false;
            }

            events.SetSelectedGameObject(selectable.gameObject);
            return true;
        }
    }
}
