$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot
$source = [IO.File]::ReadAllText((Join-Path $repo 'core/PlayerRemoval.cs'))
$source = $source.Substring(0, $source.IndexOf('    [HarmonyPatch(')) + '}'
$source = $source.Replace('internal static', 'public static')
$source = $source -replace '(?m)^using [^;]+;\r?\n', ''
$stubs = @'
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine.InputSystem;
namespace HarmonyLib { }
namespace UnityEngine { }
namespace UnityEngine.InputSystem {
    public class PlayerInput {
        public static Dictionary<int, PlayerInput> Inputs = new Dictionary<int, PlayerInput>();
        public PlayerController Controller;
        public bool Deactivated, Removed;
        public static PlayerInput GetPlayerByIndex(int index) => Inputs.ContainsKey(index) ? Inputs[index] : null;
        public T GetComponentInChildren<T>() where T : class => Controller as T;
        public void DeactivateInput() { Deactivated = true; }
    }
}
public class Variable { public int Value; }
public class PlayerController {
    public Variable playerID = new Variable(), playerInputIndex = new Variable();
    public bool IsSpawned = true, CharacterRemoved;
    public ulong NetworkObjectId, OwnerClientId;
    public Transform transform;
    public NetworkObject NetworkObject;
    public PlayerController() { transform = new Transform(this); NetworkObject = new NetworkObject(this); }
    public void DespawnCharacter() { CharacterRemoved = true; }
}
public class Transform {
    PlayerController player;
    public Transform(PlayerController player) { this.player = player; }
    public void SetParent(object value) {
        foreach (var input in UnityEngine.InputSystem.PlayerInput.Inputs.Values)
            if (input.Controller == player) input.Controller = null;
    }
}
public class NetworkObject {
    PlayerController player;
    public NetworkObject(PlayerController player) { this.player = player; }
    public void Despawn(bool destroy) { player.IsSpawned = false; }
}
public static class CustomMapEditor { public static bool Editing; public static bool EditingMap() => Editing; }
public static class LevelController { public static bool IsLoading; }
public static class Announcer {
    public static string Message;
    public static void InformationPopup(string text) { Message = text; }
}
public class LobbyController {
    public static bool inLobby;
    public static LobbyController instance = new LobbyController();
    public void DestroyLocalPlayerInput(int index) {
        var input = UnityEngine.InputSystem.PlayerInput.GetPlayerByIndex(index);
        if (input.Controller != null) throw new Exception("Duplicate destroy RPC would be sent");
        input.Removed = true;
    }
}
namespace SpiderSurge.Common {
    public static class Players {
        public static List<PlayerController> All = new List<PlayerController>();
        public static PlayerController ById(int id) => All.FirstOrDefault(p => p != null && p.playerID.Value == id);
        public static bool IsLocal(PlayerController player) => player.OwnerClientId == Net.LocalClientId;
        public static bool MayActFor(int id, ulong sender) => sender == 0 || ById(id).OwnerClientId == sender;
    }
    public static class Net {
        public static ulong LocalClientId, Sender;
        public static Dictionary<string, Action<ulong, BinaryReader>> Handlers = new Dictionary<string, Action<ulong, BinaryReader>>();
        public static List<string> Sent = new List<string>();
        public static void RegisterFromClients(string channel, Action<ulong, BinaryReader> handler, bool offline) { Handlers[channel] = handler; }
        public static void RegisterFromHost(string channel, Action<BinaryReader> handler) { Handlers[channel] = (sender, reader) => handler(reader); }
        public static void Unregister(string channel) { Handlers.Remove(channel); }
        public static void SendToServer(string channel, Action<BinaryWriter> write) { Deliver(channel, Sender, write); }
        public static void SendTo(ulong target, string channel, Action<BinaryWriter> write) { Sent.Add(target + ":" + channel); }
        public static void Deliver(string channel, ulong sender, Action<BinaryWriter> write) {
            using (var stream = new MemoryStream()) {
                var writer = new BinaryWriter(stream); write(writer); writer.Flush(); stream.Position = 0;
                Handlers[channel](sender, new BinaryReader(stream));
            }
        }
        public static void Remove(int slot, ulong objectId, ulong sender) {
            Deliver("core.remove-player.request", sender, writer => { writer.Write(slot); writer.Write(objectId); });
        }
        public static void RemoveInput(int index, ulong objectId) {
            Deliver("core.remove-player.input", 0, writer => { writer.Write(index); writer.Write(objectId); });
        }
    }
}
'@
Add-Type -TypeDefinition ($stubs + $source)
[SpiderSurge.Common.PlayerRemoval]::Initialize()
$checks = 0
function Assert-True([bool]$condition, [string]$label) {
    if (-not $condition) { throw $label }
    $script:checks++
}
function Add-Player([int]$slot, [ulong]$objectId, [ulong]$owner) {
    $player = [PlayerController]::new()
    $player.playerID.Value = $slot
    $player.playerInputIndex.Value = $slot
    $player.NetworkObjectId = $objectId
    $player.OwnerClientId = $owner
    [SpiderSurge.Common.Players]::All.Add($player)
    $player
}
$hostPlayer = Add-Player 0 100 0
$remote = Add-Player 1 101 7
$otherRemote = Add-Player 2 102 8
Assert-True ($null -eq [SpiderSurge.Common.PlayerRemoval]::Problem($remote, 0, 101)) 'Host can remove a remote slot'
Assert-True ($null -eq [SpiderSurge.Common.PlayerRemoval]::Problem($remote, 7, 101)) 'Client can remove its own slot'
[SpiderSurge.Common.Net]::Remove(1, 101, 8)
Assert-True $remote.IsSpawned 'A client cannot remove another client'
Assert-True ([SpiderSurge.Common.Net]::Sent.Contains('8:core.remove-player.result')) 'Rejected client receives a reason'
[SpiderSurge.Common.Net]::Remove(0, 100, 7)
Assert-True $hostPlayer.IsSpawned 'A client cannot remove the host'
[SpiderSurge.Common.Net]::Remove(1, 999, 0)
Assert-True $remote.IsSpawned 'Stale object identity cannot remove a reused slot'
[LevelController]::IsLoading = $true
[SpiderSurge.Common.Net]::Remove(1, 101, 0)
Assert-True $remote.IsSpawned 'Removal is refused during a level load'
[LevelController]::IsLoading = $false
[LobbyController]::inLobby = $true
[SpiderSurge.Common.Net]::Remove(1, 101, 0)
Assert-True $remote.IsSpawned 'In-game requests cannot invoke lobby-wide removal'
[LobbyController]::inLobby = $false
[CustomMapEditor]::Editing = $true
[SpiderSurge.Common.Net]::Remove(1, 101, 0)
Assert-True $remote.IsSpawned 'Removal is refused in the map editor'
[CustomMapEditor]::Editing = $false
[SpiderSurge.Common.Net]::Remove(1, 101, 7)
Assert-True (-not $remote.IsSpawned -and $remote.CharacterRemoved) 'An owner request removes its spider and network controller'
Assert-True $otherRemote.IsSpawned 'Removing a slot preserves other remote slots'
Assert-True ([SpiderSurge.Common.Net]::Sent.Contains('7:core.remove-player.input')) 'Input cleanup is sent only to the slot owner'
$input = [UnityEngine.InputSystem.PlayerInput]::new()
$input.Controller = $hostPlayer
[UnityEngine.InputSystem.PlayerInput]::Inputs[0] = $input
[SpiderSurge.Common.Net]::Remove(0, 100, 0)
Assert-True ($input.Removed -and $input.Deactivated -and $null -eq $input.Controller) 'Local input is detached before destruction without a duplicate RPC'
Assert-True (-not $hostPlayer.IsSpawned -and $otherRemote.IsSpawned) 'Host local removal leaves the remote player running'
[SpiderSurge.Common.Net]::Remove(2, 102, 0)
Assert-True $otherRemote.IsSpawned 'Back-to-back requests cannot remove the last player, even before list cleanup'
$replacementInput = [UnityEngine.InputSystem.PlayerInput]::new()
$replacementInput.Controller = $otherRemote
[UnityEngine.InputSystem.PlayerInput]::Inputs[1] = $replacementInput
[SpiderSurge.Common.Net]::RemoveInput(1, 101)
Assert-True (-not $replacementInput.Removed) 'Delayed cleanup cannot destroy an input slot now bound to another player'
[SpiderSurge.Common.Net]::Remove(90, 800, 0)
Assert-True $otherRemote.IsSpawned 'A missing player request is harmless'
[SpiderSurge.Common.PlayerRemoval]::Shutdown()
Assert-True ([SpiderSurge.Common.Net]::Handlers.Count -eq 0) 'Unload unregisters all handlers'
Write-Output "PASS: $checks player removal checks"
