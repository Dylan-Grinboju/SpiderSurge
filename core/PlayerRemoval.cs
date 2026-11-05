using System.IO;
using System.Linq;
using HarmonyLib;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SpiderSurge.Common
{
    internal static class PlayerRemoval
    {
        private const string RequestChannel = "core.remove-player.request";
        private const string InputChannel = "core.remove-player.input";
        private const string ResultChannel = "core.remove-player.result";

        internal static void Initialize()
        {
            Net.RegisterFromClients(RequestChannel, OnRequest, offline: true);
            Net.RegisterFromHost(InputChannel, OnRemoveInput);
            Net.RegisterFromHost(ResultChannel, reader => Announcer.InformationPopup(reader.ReadString()));
        }

        internal static void Shutdown()
        {
            Net.Unregister(RequestChannel);
            Net.Unregister(InputChannel);
            Net.Unregister(ResultChannel);
        }

        internal static bool InGame => !LobbyController.inLobby && !CustomMapEditor.EditingMap();

        internal static string Problem(PlayerController player, ulong sender, ulong objectId)
        {
            if (!InGame || LevelController.IsLoading) return "Wait until the game has finished loading.";
            if (player == null || !player.IsSpawned || player.NetworkObjectId != objectId)
                return "That player has already left the game.";
            if (!Players.MayActFor(player.playerID.Value, sender)) return "Only the host can remove another player's spider.";
            if (Players.All.Count(candidate => candidate != null && candidate.IsSpawned) <= 1)
                return "Keep at least one player in the game. Use Return to lobby to end the game.";
            return null;
        }

        internal static void Request(PlayerController player, ulong objectId)
        {
            string problem = Problem(player, Net.LocalClientId, objectId);
            if (problem != null) { Announcer.InformationPopup(problem); return; }
            Net.SendToServer(RequestChannel, writer =>
            {
                writer.Write(player.playerID.Value);
                writer.Write(objectId);
            });
        }

        private static void OnRequest(ulong sender, BinaryReader reader)
        {
            int playerId = reader.ReadInt32();
            ulong objectId = reader.ReadUInt64();
            var player = Players.ById(playerId);
            string problem = Problem(player, sender, objectId);
            if (problem != null)
            {
                if (sender == Net.LocalClientId) Announcer.InformationPopup(problem);
                else Net.SendTo(sender, ResultChannel, writer => writer.Write(problem));
                return;
            }

            // Release only this slot's local input; the host despawns the network objects.
            if (Players.IsLocal(player)) RemoveInput(player.playerInputIndex.Value, objectId);
            else Net.SendTo(player.OwnerClientId, InputChannel, writer =>
            {
                writer.Write(player.playerInputIndex.Value);
                writer.Write(objectId);
            });
            player.DespawnCharacter();
            player.NetworkObject.Despawn(true);
        }

        private static void OnRemoveInput(BinaryReader reader) => RemoveInput(reader.ReadInt32(), reader.ReadUInt64());

        private static void RemoveInput(int index, ulong objectId)
        {
            var input = PlayerInput.GetPlayerByIndex(index);
            if (input == null) return;
            var controller = input.GetComponentInChildren<PlayerController>();
            if (controller != null)
            {
                if (controller.NetworkObjectId != objectId) return;
                // DestroyLocalPlayerInput otherwise sends a second DestroyServerRpc.
                controller.transform.SetParent(null);
            }
            input.DeactivateInput();
            LobbyController.instance.DestroyLocalPlayerInput(index);
        }
    }

    [HarmonyPatch(typeof(GameController), nameof(GameController.CanRemovePlayers))]
    internal static class InGameRemovePlayersAvailablePatch
    {
        private static void Postfix(ref bool __result)
        {
            if (PlayerRemoval.InGame)
                __result = !LevelController.IsLoading && Players.All != null
                    && Players.All.Any(player => player != null && player.IsSpawned && (Net.IsAuthority || Players.IsLocal(player)));
        }
    }

    [HarmonyPatch(typeof(GameController), nameof(GameController.RemovePlayers))]
    internal static class InGameRemovePlayersPatch
    {
        private static bool Prefix()
        {
            if (LobbyController.inLobby) return true;
            PlayerRemovalMenu.Show();
            return false;
        }
    }

    [HarmonyPatch(typeof(HudController), nameof(HudController.ResetLobby))]
    internal static class InGameResetLobbyPatch
    {
        private static bool Prefix()
        {
            if (LobbyController.inLobby) return true;
            PlayerRemovalMenu.Show();
            return false;
        }
    }
}
