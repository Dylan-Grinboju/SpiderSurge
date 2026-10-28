using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.InputSystem;
using Logger = Silk.Logger;

namespace SpiderSurge.Fixes
{
    internal static class ControllerReconnectFix
    {
        private static readonly HashSet<InputDevice> PendingDevices = new HashSet<InputDevice>();
        private static readonly AccessTools.FieldRef<LobbyController, List<PlayerController>> Disconnected =
            AccessTools.FieldRefAccess<LobbyController, List<PlayerController>>("_disconnectedControllers");

        internal static void Start()
        {
            InputSystem.onAfterUpdate += RestorePendingDevices;
        }

        internal static void Stop()
        {
            InputSystem.onAfterUpdate -= RestorePendingDevices;
            PendingDevices.Clear();
        }

        internal static void DeviceAdded(InputDevice device)
        {
            if (device is Gamepad)
            {
                PendingDevices.Add(device);
            }
        }

        // InputUser restores lost devices during onDeviceChange, after the game's callback.
        private static void RestorePendingDevices()
        {
            if (PendingDevices.Count == 0)
            {
                return;
            }

            var devices = new List<InputDevice>(PendingDevices);
            PendingDevices.Clear();
            var lobby = LobbyController.instance;
            if (lobby == null)
            {
                return;
            }

            foreach (var device in devices)
            {
                if (!device.added || FindOwner(device, null) != null)
                {
                    continue;
                }
                var input = FindDisconnectedInput(lobby, device);
                if (input != null)
                {
                    Restore(lobby, input, device);
                }
            }
        }

        internal static PlayerInput FindOwner(InputDevice device, PlayerInput except)
        {
            foreach (var input in PlayerInput.all)
            {
                if (input == null || input == except)
                {
                    continue;
                }
                foreach (var paired in input.devices)
                {
                    if (paired == device)
                    {
                        return input;
                    }
                }
            }
            return null;
        }

        internal static PlayerInput FindDisconnectedInput(LobbyController lobby, InputDevice device)
        {
            PlayerInput fallback = null;
            foreach (var controller in Disconnected(lobby))
            {
                if (controller == null)
                {
                    continue;
                }
                var input = controller.GetComponentInParent<PlayerInput>();
                if (input == null || !input.user.valid || input.actions == null)
                {
                    continue;
                }
                bool hasGamepad = false;
                foreach (var paired in input.devices)
                {
                    hasGamepad |= paired is Gamepad && paired.added;
                }
                if (hasGamepad || !InputControlScheme.FindControlSchemeForDevices(
                    new[] { device }, input.actions.controlSchemes).HasValue)
                {
                    continue;
                }
                foreach (var lost in input.user.lostDevices)
                {
                    if (!(lost is Gamepad))
                    {
                        continue;
                    }
                    if (lost == device || lost.deviceId == device.deviceId || SameSerial(lost, device))
                    {
                        return input;
                    }
                    if (fallback == null)
                    {
                        fallback = input;
                    }
                }
            }
            return fallback;
        }

        private static bool SameSerial(InputDevice first, InputDevice second)
        {
            var a = first.description;
            var b = second.description;
            return !string.IsNullOrEmpty(a.serial) && a.serial == b.serial &&
                a.interfaceName == b.interfaceName && a.manufacturer == b.manufacturer && a.product == b.product;
        }

        internal static void Restore(LobbyController lobby, PlayerInput input, InputDevice device)
        {
            var scheme = InputControlScheme.FindControlSchemeForDevices(new[] { device }, input.actions.controlSchemes);
            if (!scheme.HasValue)
            {
                return;
            }
            input.user.UnpairDevices();
            input.SwitchCurrentControlScheme(scheme.Value.name, device);
            var controller = input.GetComponentInChildren<PlayerController>();
            if (controller != null)
            {
                Disconnected(lobby).RemoveAll(player => player == controller);
                lobby.OnDeviceRegained(controller, input);
            }
            Logger.LogInfo($"Restored gamepad to existing player input {input.playerIndex}");
        }

        internal static void RejectDuplicate(PlayerInput input)
        {
            input.DeactivateInput();
            input.enabled = false;
            Object.Destroy(input.gameObject);
        }
    }

    [HarmonyPatch(typeof(LobbyController), "ChangeControllerDevice")]
    internal static class ControllerDeviceAddedPatch
    {
        private static bool Prefix(InputDevice device, InputDeviceChange status)
        {
            if (status == InputDeviceChange.Added)
            {
                ControllerReconnectFix.DeviceAdded(device);
            }
            return false;
        }
    }

    [HarmonyPatch(typeof(LobbyController), "OnPlayerJoined")]
    internal static class ControllerPlayerJoinedPatch
    {
        private static bool Prefix(LobbyController __instance, PlayerInput playerInput,
            List<PlayerController> ____disconnectedControllers, out List<PlayerController> __state)
        {
            __state = null;
            if (playerInput == null || playerInput.transform.childCount > 0)
            {
                return true;
            }
            foreach (var device in playerInput.devices)
            {
                if (!(device is Gamepad))
                {
                    continue;
                }
                if (ControllerReconnectFix.FindOwner(device, playerInput) != null)
                {
                    ControllerReconnectFix.RejectDuplicate(playerInput);
                    return false;
                }
                var original = ControllerReconnectFix.FindDisconnectedInput(__instance, device);
                if (original != null)
                {
                    ControllerReconnectFix.RejectDuplicate(playerInput);
                    ControllerReconnectFix.Restore(__instance, original, device);
                    return false;
                }
            }
            if (____disconnectedControllers.Count > 0)
            {
                // Fresh joins must not run the game's FIFO reparenting of disconnected spiders.
                __state = new List<PlayerController>(____disconnectedControllers);
                ____disconnectedControllers.Clear();
            }
            return true;
        }

        private static void Finalizer(List<PlayerController> ____disconnectedControllers, List<PlayerController> __state)
        {
            if (__state == null)
            {
                return;
            }
            foreach (var controller in __state)
            {
                if (controller == null || ____disconnectedControllers.Contains(controller))
                {
                    continue;
                }
                var input = controller.GetComponentInParent<PlayerInput>();
                if (input != null && input.user.valid && input.user.lostDevices.Count > 0)
                {
                    ____disconnectedControllers.Add(controller);
                }
            }
        }
    }

    [HarmonyPatch(typeof(LobbyController), nameof(LobbyController.OnDeviceLost))]
    internal static class ControllerDeviceLostPatch
    {
        private static bool Prefix(PlayerController playerController, List<PlayerController> ____disconnectedControllers)
        {
            return playerController != null && !____disconnectedControllers.Contains(playerController);
        }
    }

    [HarmonyPatch(typeof(LobbyController), nameof(LobbyController.OnDeviceRegained))]
    internal static class ControllerDeviceRegainedPatch
    {
        private static void Prefix(PlayerController playerController, List<PlayerController> ____disconnectedControllers)
        {
            ____disconnectedControllers.RemoveAll(player => player == null || player == playerController);
        }
    }
}
