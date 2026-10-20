using System;
using System.Collections.Generic;
using HarmonyLib;
using SpiderSurge.Common;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using Logger = Silk.Logger;

namespace SpiderTeams
{
    // Global actions on the configured buttons; the pressing device tells which local player picked.
    public class LobbyTeamPicker : MonoBehaviour
    {
        private const string ShowNamesAction = "ShowNames";
        private static readonly HashSet<InputDevice> _held = new HashSet<InputDevice>();
        private static LobbyTeamPicker _current;

        private InputAction _gamepadAction;
        private InputAction _keyboardAction;
        private string _gamepadPath;
        private string _keyboardPath;
        private bool _active;
        private bool _checkedBindings;

        private void OnEnable()
        {
            _current = this;
            ModConfig.Changed += Rebuild;
            Rebuild();
        }

        private void OnDisable()
        {
            if (_current == this) _current = null;
            ModConfig.Changed -= Rebuild;
            DisposeActions();
        }

        private void Update()
        {
            var manager = TeamManager.Instance;
            bool room = LobbyController.inLobby || LobbyController.inWaitingRoom;
            bool wanted = manager != null && manager.PicksOpen && ModConfig.TeamsEnabled && room;
            if (wanted == _active)
            {
                return;
            }

            _active = wanted;
            _held.Clear();
            SetEnabled(_gamepadAction, wanted);
            SetEnabled(_keyboardAction, wanted);
            if (wanted && !_checkedBindings)
            {
                CheckBindings();
            }
        }

        // Warns when a team button is also one of the game's own bindings, except ShowNames, which picking overrides; lists them all at debug level.
        private void CheckBindings()
        {
            try
            {
                foreach (PlayerInput input in PlayerInput.all)
                {
                    if (input == null || input.actions == null)
                    {
                        continue;
                    }

                    _checkedBindings = true;
                    foreach (InputAction action in input.actions)
                    {
                        foreach (InputBinding binding in action.bindings)
                        {
                            string path = binding.effectivePath;
                            if (string.IsNullOrEmpty(path))
                            {
                                continue;
                            }

                            Logger.LogDebug($"SpiderTeams: game binding {action.name} = {path}");
                            if (action.name != ShowNamesAction && (Uses(_gamepadPath, path) || Uses(_keyboardPath, path)))
                            {
                                Logger.LogWarning($"SpiderTeams: {path} is also the game's '{action.name}' button; pick another team button in Mods > SpiderSurge > Controls if they clash");
                            }
                        }
                    }
                    return;
                }
            }
            catch (Exception ex)
            {
                _checkedBindings = true;
                Logger.LogWarning($"SpiderTeams could not read the game's bindings: {ex.Message}");
            }
        }

        // True while the lobby picker is on and one of this player's devices holds a team button.
        internal static bool HoldsTeamButton(PlayerInput input)
        {
            LobbyTeamPicker picker = _current;
            if (picker == null || !picker._active || input == null)
            {
                return false;
            }

            foreach (InputDevice device in input.devices)
            {
                if (IsPressed(device, picker._gamepadPath) || IsPressed(device, picker._keyboardPath))
                {
                    return true;
                }
            }
            return false;
        }

        private static bool IsPressed(InputDevice device, string binding)
        {
            foreach (string path in Controls.PressPaths(binding))
            {
                if (InputControlPath.TryFindControl(device, path) is ButtonControl button && button.isPressed)
                {
                    return true;
                }
            }
            return false;
        }

        private static bool Uses(string binding, string path)
        {
            foreach (string bound in Controls.PressPaths(binding))
            {
                if (string.Equals(bound, path, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }
            return false;
        }

        private void Rebuild()
        {
            if (_gamepadPath == ModConfig.CycleTeamGamepad && _keyboardPath == ModConfig.CycleTeamKeyboard)
            {
                return;
            }

            DisposeActions();
            _gamepadPath = ModConfig.CycleTeamGamepad;
            _keyboardPath = ModConfig.CycleTeamKeyboard;
            _gamepadAction = CreateAction("SpiderTeamsCycleGamepad", _gamepadPath);
            _keyboardAction = CreateAction("SpiderTeamsCycleKeyboard", _keyboardPath);
            _active = false;
            _checkedBindings = false;
        }

        // Pass-through skips the conflict resolution that drops a second pad's press while another pad holds the button.
        private InputAction CreateAction(string name, string binding)
        {
            if (string.IsNullOrEmpty(binding) || binding == Controls.None)
            {
                return null;
            }

            try
            {
                var action = new InputAction(name, InputActionType.PassThrough);
                foreach (string path in Controls.PressPaths(binding))
                {
                    action.AddBinding(path);
                }
                action.performed += OnPerformed;
                return action;
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"SpiderTeams could not bind '{binding}': {ex.Message}");
                return null;
            }
        }

        private void DisposeActions()
        {
            DisposeAction(_gamepadAction);
            DisposeAction(_keyboardAction);
            _gamepadAction = null;
            _keyboardAction = null;
            _gamepadPath = null;
            _keyboardPath = null;
            _active = false;
            _held.Clear();
        }

        private void DisposeAction(InputAction action)
        {
            if (action == null)
            {
                return;
            }

            try
            {
                action.performed -= OnPerformed;
                action.Disable();
                action.Dispose();
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"SpiderTeams could not release an input action: {ex.Message}");
            }
        }

        private static void SetEnabled(InputAction action, bool enabled)
        {
            if (action == null)
            {
                return;
            }

            if (enabled)
            {
                action.Enable();
            }
            else
            {
                action.Disable();
            }
        }

        // Pass-through reports every change, so each device switches once per press and not again until it lets go.
        private static void OnPerformed(InputAction.CallbackContext context)
        {
            try
            {
                InputDevice device = context.control != null ? context.control.device : null;
                if (device == null)
                {
                    return;
                }
                if (!context.ReadValueAsButton())
                {
                    _held.Remove(device);
                    return;
                }
                if (!_held.Add(device))
                {
                    return;
                }

                if (Controls.Ignored(device))
                {
                    return;
                }

                var game = GameController.instance;
                var manager = TeamManager.Instance;
                if (manager == null || !manager.PicksOpen || (game != null && !game.acceptingInputs))
                {
                    return;
                }

                PlayerController player = FindLocalPlayer(device);
                if (player != null && manager.CycleTeam(player))
                {
                    Announcements.QueuePick(player.playerID.Value);
                }
            }
            catch (Exception ex)
            {
                Logger.LogError($"SpiderTeams team pick failed: {ex}");
            }
        }

        private static PlayerController FindLocalPlayer(InputDevice device)
        {
            if (device == null)
            {
                return null;
            }

            foreach (PlayerInput input in PlayerInput.all)
            {
                if (input == null)
                {
                    continue;
                }

                foreach (InputDevice owned in input.devices)
                {
                    if (owned == device)
                    {
                        return input.GetComponentInChildren<PlayerController>();
                    }
                }
            }
            return null;
        }
    }

    // The game's Y button toggles player names; in the lobby it is the default team button, so a team pick doesn't also toggle them.
    [HarmonyPatch(typeof(PlayerController), "OnShowNames")]
    internal static class PlayerController_OnShowNames_Patch
    {
        [HarmonyPrefix]
        private static bool Prefix(PlayerController __instance)
        {
            try
            {
                return !LobbyTeamPicker.HoldsTeamButton(__instance.GetComponentInParent<PlayerInput>());
            }
            catch (Exception ex)
            {
                Logger.LogWarning($"SpiderTeams could not check the team button: {ex.Message}");
                return true;
            }
        }
    }
}
