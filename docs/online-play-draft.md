# Online play draft

Status: drafted and compiling (`dotnet build SpiderSurge.csproj`, 0 warnings), not run in the game. Lives on `dylan/online-play`, rebased onto `main` after PRs 24-34; the online draft is everything under `core/Net`, `surge/Net`, `stats/Net`, `teams/Net` plus the edits listed per part below.

## Model

- **Host-authoritative.** The host runs every rule (teams, perks, abilities' world effects, stat credit) and broadcasts state. Clients render, hold local input, and send requests.
- **Identity** is `PlayerController.playerID` (the host's slot number, identical on every machine). Nothing is keyed by `PlayerInput` any more; `Players` (`core/Net/Players.cs`) wraps the lookups (`ById`, `Of`, `IdOf`, `IsLocal`, `OwnedBy`, `NameOf`).
- **Transport** is one NGO named message (`"SpiderSurge"`) carrying a channel hash and a `BinaryWriter` payload (`core/Net/Net.cs`), at most 63,000 bytes (NGO's fragmented limit is 64,000). Handlers re-bind after each `StartHost`/`StartClient` (`core/Net/NetPatches.cs`). Vanilla peers drop the message silently, so a Hello/Welcome handshake tells each side who runs the mod (`Net.HostHasMod`, `Net.ClientHasMod`). The host only sends to modded clients.
- **Connection data.** A modded client puts `SpiderSurge/<version>` in `NetworkConfig.ConnectionData`, and the host reads it in `LobbyController.ConnectionApprovalCallback` (`Net.ClientRunsMod`), so it knows before the join sync, which comes before the Hello.
- **Settings.** Each part lists `SharedSettings` (`surge.unlimitedPerkTime`, `teams.enabled`); the host pushes those to clients on join and on change, and `Settings.ApplyRemote` overrides the client's values while connected (`core/Settings.cs`, `core/Parts.cs`). A shared key is read-only on a client while connected and its row says "(set by host)". Everything else (UI, keybinds, windows) stays per machine.
- **P2P only.** On a dedicated server no mod code runs on the authority, so `HudController.StartMultiplayerMatch` shows a popup once and the parts stay off online.
- **Authority helper.** `Net.IsAuthority` is `!IsRemoteClient`, so offline play (the game is its own host) keeps today's code paths untouched.

## Teams (`teams/Net/TeamNet.cs`)

- `teams.roster` host → all: team count, lock state, mode flag, (playerId, team) pairs, scores. Sent when the roster is dirty and to every joiner.
- `teams.pick` client → host: a player's pick request. The host checks `OwnerClientId == sender`, respects `acceptingInputs`, then `CycleTeam`.
- `teams.snapshot` host → all: the end-of-match `MatchSnapshot` (`Write`/`Read`) so clients show the same results screen.
- Roster lock moved from `StartMatch` to the first `StartRound` that is not the waiting room, so late joiners in the waiting room can still pick. `TeamMatchActive` requires `IsHost || Net.HostHasMod`.
- Client gates: `LateUpdate`/`DeclareWinner`/`EndMatch`, friendly fire, spawn grouping and recolouring only run their rule on the host; clients follow the roster. The lobby picker is hidden on a client whose host lacks the mod.

## Surge (`surge/Net/SurgeNet.cs`, `surge/Net/CustomEnemyNet.cs`)

- `surge.state` host → all: mode on, run active, run used Surge, wave, the three perk-phase flags, nonzero perk levels, lucky perk keys. Flushed once per frame when dirty, plus synchronous sends at wave start and right after `ApplyPerkLuck` (before the perk cards' ClientRpc). Needed because the game's `SetModLevelClientRpc` writes the level directly on clients and bypasses our `SetModLevel` postfix.
- `surge.ability` owner → host: (playerId, kind, ultimate). The host validates ownership and replays through `BaseAbility.ActivateFromNetwork`. On the client the ability still runs its cooldown, sounds and local visuals; world effects (`Immune` barrier, `Ammo` set, `Pulse` physics, `Storage` swap) are gated on `IsAuthority`.
- `surge.fx` host → all: `PulseRing`, `Immune` radiance and the Freeze Time and Rewind rings for other players' spiders; the caster's own machine skips it.
- `surge.enemy` host → all: effects on an enemy that only the host runs, keyed by `NetworkObjectId`. `Frozen` carries the freeze time, so clients tint the enemy while the host holds it. `LaserPhase` carries how far the laser ring is into its cycle whenever its warning starts; a client's ring stays idle until the first one, so a joiner never draws a beam out of step with the host's damage.
- Freeze Time and Rewind: the host freezes, and turns enemies into wasps (network spawns plus a despawn of the old enemy); clients get the rings and the tint.
- Abilities attach to every spider, including remote players' proxies (no `PlayerInput`, no input interceptor), so the host can run them and clients can draw them. `PlayerAbilityHandler.OnWaveStarted` refunds cooldowns on both sides.
- Custom enemies: each clone gets a deterministic `GlobalObjectIdHash` (`Net.Hash("SpiderSurge.Enemy:" + name)`) and an `INetworkPrefabInstanceHandler`, so modded clients build the clone. A postfix on `NetworkObject.GetMessageSceneObject` swaps in the original's hash for each target client that doesn't run the mod, in spawn messages and in the join sync alike, so a vanilla client sees the base enemy.
- Respawn cooldown: the host counts each `ResetSpawnTracking` and sends the count in `surge.state`, so clients forget the same spawns and don't start Immune on cooldown at a new round or run.
- A client joining mid-run attaches abilities to the spiders that already exist and redraws an open perk choice when its first `surge.state` arrives.
- Storage: a stored weapon is hidden from clients with `NetworkHide` and `CheckObjectVisibility` because NGO doesn't sync `SetActive`, then shown again when it comes back out.
- Wasp Cocoon: its base, `ParkourTarget`, is not one of the game's network prefabs, so no vanilla client can build any form of it. `CustomEnemyNet.PrepareForSpawn` hides such clones from clients without the mod; they still see the wasps it hatches.
- Object visibility: NGO 1.0.0-pre makes a joining client an observer of every object and puts every object in its join sync, ignoring `CheckObjectVisibility`. Two postfixes (`NetworkSpawnManager.UpdateObservedNetworkObjects`, `SceneEventData.AddSpawnedNetworkObjects`) apply it, so stored weapons and cocoons stay hidden from joiners too.
- Leaving a host resets the run, perks, lucky keys and stored weapons.
- Banish: only the host picks and pulls the target (`TryBanishNearest`); every machine draws the ring and plays the sound. The kinetic pulse's enemy launch (`PulseKnockback`) runs inside `ApplyPulsePhysics`, so it is host-only too.

## Stats (`stats/Net/StatsNet.cs`)

- Re-keyed from `PlayerInput` to `PlayerController`. Players register through `LobbyController.AddPlayer`/`RemovePlayer` (runs on every machine for every player). `PlayerData.PlayerId` is now the game's `playerID`; ids and names are refreshed lazily because `AddPlayer` runs in `Awake`, before the host assigns `playerID`.
- Tracking (hits, kills, timers, maps, perks, pain level) runs only where `Net.IsAuthority`; `HitLogic` entry points and the `PlayerTracker` mutators return early on a client.
- `stats.snapshot` host → all about once a second while a session runs (from `PauseHandler.Update`), and immediately on session start/stop with a reason byte. It carries the session fields and every player's counters, totals, dictionaries and accuracy. The client reads it straight into its `PlayerTracker` data (`RemoteDataFor`), with `RemoteAlive` standing in for the alive timer.
- `stats.titles` host → all after `CalculateAndStoreTitles`; the client calls `TitlesManager.SetRemoteTitles` and mirrors the show/pull logic (`OnRemoteTitles`), using the host's `restartRequested` flag from the snapshot.
- Online names use the game's `playerName` (e.g. `Dylan P1`); offline keeps `Player N`.
- `StatsModApi.RegisterCustomTitle` now takes a `PlayerController` leader. That is a public API change; `API_VERSION` is still 1.

## Open questions and risks

1. **Cooldown drift.** A client fires an ability locally and asks the host; if the host's instance refuses (its cooldown or perk check differs), nothing tells the client. Either echo a result or make the client wait for the host's `surge.fx`/state before playing the effect.
2. **Perk index parity.** The game's `ModSyncResponceClientRpc` syncs `_currModsState` by index, so our perks must be appended in the same order on every machine (same mod version). A version mismatch between host and client should probably disable Surge; `Net.HostVersion` is available for that check but unused.
3. **Prefab handler path.** `CustomEnemyNet` relies on NGO calling `PrefabHandler` before the prefab lookup on clients (verified in the decompiled 1.0.0-pre `NetworkSpawnManager.CreateLocalNetworkObject`), and on `GlobalObjectIdHash` being writable at runtime. Untested.
4. **Mid-session joiner.** A joiner gets the state/roster/snapshot at once, but the stats UI isn't told a session started (only the `Started` snapshot does that), so it stays on whatever page it was.
5. **Stats restart.** When the host restarts without visiting the lobby, clients only show titles if `restartRequested` arrived in the stopped snapshot; verify the ordering in practice.
6. **Pausing.** `PauseHandler` keys off `Time.timeScale`, which the host controls online. Clients ignore pause for their timers; session time on a client interpolates from the last snapshot and keeps running through a host pause until the next snapshot corrects it.
7. **Telemetry.** `SpiderSurgeStatsManager` records only on the authority and now logs `PlayerId`; the JSON key stays `playerIndex`. The relay sees online matches as the host's.
8. **Banish on a client.** The caster's machine cannot know whether the pull hit, so it never gets the miss refund on its cooldown, and the target's shrink while being pulled is not replicated (positions are, through the game's own sync).
9. **Not touched:** leaderboards, the Surge versus variant beyond `SurgeVersus.AllPlayersLocal`, SpiderInspector's `act/start?surge=` on clients, and any client-side prediction.
10. **Testing.** Needs two game instances with separate EOS logins. SpiderInspector drives one game per PC; a second PC or the Xbox build as the second instance is the practical option. Suggested order: Hello/Welcome handshake and settings push, then Teams roster/pick/snapshot, then Surge state and one ability per kind, then Stats snapshot and titles, each first with both sides modded and then with a vanilla client.
