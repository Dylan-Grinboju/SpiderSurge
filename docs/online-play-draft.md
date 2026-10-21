# Online play draft

Status: merged in PR #35, and first run online on 2026-10-18 with two instances on one PC: the Steam build as host and the Xbox build as client, over EOS P2P with both sides modded (see Testing below). The online draft is everything under `core/Net`, `surge/Net`, `stats/Net`, `teams/Net` plus the edits listed per part below.

The first run found that a client dropped every message from the host: NGO turns the host's EOS transport id into a client id of 1 on the client, not 0. `Net.OnMessage` now names the sender `ServerId` on a client, since a client only ever hears from the host.

## Model

- **Host-authoritative.** The host runs every rule (teams, perks, abilities' world effects, stat credit) and broadcasts state. Clients render, hold local input, and send requests.
- **Identity** is `PlayerController.playerID` (the host's slot number, identical on every machine). Nothing is keyed by `PlayerInput` any more; `Players` (`core/Net/Players.cs`) wraps the lookups (`ById`, `Of`, `IdOf`, `IsLocal`, `OwnedBy`, `NameOf`).
- **Transport** is one NGO named message (`"SpiderSurge"`) carrying a channel hash and a `BinaryWriter` payload (`core/Net/Net.cs`), at most 63,000 bytes (NGO's fragmented limit is 64,000). Handlers re-bind after each `StartHost`/`StartClient` (`core/Net/NetPatches.cs`). A client says Hello once its handlers are bound, and the host answers with Welcome and the shared settings; the host only sends to clients that said Hello.
- **Same version only.** A client puts `SpiderSurge/<version>` in `NetworkConfig.ConnectionData`, and the host refuses it in `LobbyController.ConnectionApprovalCallback` (`Net.Approves`) unless it matches the host's version. Mixed lobbies, with vanilla players or another version, are not supported, so the code assumes every peer runs this build.
- **Settings.** Each part lists `SharedSettings` (`surge.unlimitedPerkTime`, and Teams' match rules: `teams.enabled`, `count`, `friendlyFire`, `recolorSpiders`, `groupTeammates` and `teams.surge.*`); the host pushes those to clients on join and on change, and `Settings.ApplyRemote` overrides the client's values while connected (`core/Settings.cs`, `core/Parts.cs`). A shared key is read-only on a client while connected and its row says "(set by host)". Everything else (UI, keybinds, windows) stays per machine.
- **P2P only.** On a dedicated server no mod code runs on the authority, so `HudController.StartMultiplayerMatch` shows a popup once and the parts stay off online.
- **Authority helper.** `Net.IsAuthority` is `!IsRemoteClient`, so offline play (the game is its own host) keeps today's code paths untouched.

## Teams (`teams/Net/TeamNet.cs`)

- `teams.roster` host → all: team count, lock state, mode flag, (playerId, team) pairs, scores. Sent when the roster is dirty and to every joiner. The host gives newcomers a team before it sends, and a client never assigns or re-normalizes teams itself, even when it loads the lobby.
- A team match keeps its team rules when a whole team leaves mid-match (`TeamManager.StartedWithTeams`), so the team that is left wins its rounds instead of the match turning into free-for-all.
- `teams.pick` client → host: a player's pick request. The host checks `OwnerClientId == sender`, respects `acceptingInputs`, then `CycleTeam`.
- `teams.snapshot` host → all: the end-of-match `MatchSnapshot` (`Write`/`Read`) so clients show the same results screen.
- Roster lock moved from `StartMatch` to the first `StartRound` that is not the waiting room, so late joiners in the waiting room can still pick.
- Client gates: `LateUpdate`/`DeclareWinner`/`EndMatch`, friendly fire, spawn grouping and recolouring only run their rule on the host; clients follow the roster.

## Surge (`surge/Net/SurgeNet.cs`, `surge/Net/CustomEnemyNet.cs`)

- `surge.state` host → all: mode on, run active, run used Surge, wave, the three perk-phase flags, nonzero perk levels, lucky perk keys. Flushed once per frame when dirty, plus synchronous sends at wave start and right after `ApplyPerkLuck` (before the perk cards' ClientRpc). Needed because the game's `SetModLevelClientRpc` writes the level directly on clients and bypasses our `SetModLevel` postfix.
- `surge.ability` owner → host: (playerId, kind, ultimate). The host validates ownership and replays through `BaseAbility.ActivateFromNetwork`, which skips the host's own menus and only refuses while the shared pause or input lock is on. On the client the ability still runs its cooldown, sounds and local visuals; world effects (`Immune` barrier, `Ammo` set, `Pulse` physics, `Storage` swap) are gated on `IsAuthority`.
- `surge.fx` host → all: `PulseRing`, `Immune` radiance, the Freeze Time and Rewind rings and the Bottomless Clip aura's start and end (`Clip`) for other players' spiders; the caster's own machine skips it. `CareDrop` marks each spawn point a Care Package filled, and every client draws it, the caster's too, since only the host spawns the weapons.
- `surge.enemy` host → all: effects on an enemy that only the host runs, keyed by `NetworkObjectId`. `Frozen` carries the freeze time, so clients tint the enemy while the host holds it. `LaserPhase` carries how far the laser ring is into its cycle whenever its warning starts; a client's ring stays idle until the first one, so a joiner never draws a beam out of step with the host's damage. `LaserHold` tells clients the host's ring stopped for a hold (clients don't see the Black Hole's hold); the next `LaserPhase` ends it. `CloudFrozen` tints a Hornet Shaman's cloud the host froze. `ShotFrozen` carries a shot's freeze time, so clients hold it too: a Gravity Saw's pull runs on each spider's own machine. `ChargeCalmed` puts a held Whisp's charge light back to normal, since the host's brain stops sending it. `HatchCancelled` removes a cocoon's spawn warning, a local effect on every machine, when its hatch is cancelled. `ShieldBroken` goes only to a joiner, once for each enemy whose shield is already gone, since the game shows a shield's break only to the players connected when it happens.
- Freeze Time and Rewind: the host freezes, and turns enemies into wasps (network spawns plus a despawn of the old enemy); clients get the rings and the tint.
- Abilities attach to every spider, including remote players' proxies (no `PlayerInput`, no input interceptor), so the host can run them and clients can draw them. `PlayerAbilityHandler.OnWaveStarted` refunds cooldowns on both sides.
- Custom enemies: each clone gets a deterministic `GlobalObjectIdHash` (`Net.Hash("SpiderSurge.Enemy:" + name)`) and an `INetworkPrefabInstanceHandler`, so clients build the clone.
- Respawn cooldown: the host counts each `ResetSpawnTracking` and sends the count in `surge.state`, so clients forget the same spawns and don't start Immune on cooldown at a new round or run.
- Starting ability choice: the host shows the cards on every machine (`ShowPerkChoicesClientRpc` to all clients), again to a client that finishes loading the level or joins during the choice (`ShowPerkChoicesServerRpc`), and closes them everywhere with `PerkChoiceSubmitClientRpc`. It waits until `LevelController.IsLoading` clears, because the game holds joiners while it is set and the paused choice would keep it set.
- A client joining mid-run attaches abilities to the spiders that already exist and redraws an open perk choice when its first `surge.state` arrives.
- Storage: a stored weapon is hidden from clients with `NetworkHide` and `CheckObjectVisibility` because NGO doesn't sync `SetActive`, then shown again when it comes back out.
- Object visibility: NGO 1.0.0-pre makes a joining client an observer of every object and puts every object in its join sync, ignoring `CheckObjectVisibility`. Two postfixes (`NetworkSpawnManager.UpdateObservedNetworkObjects`, `SceneEventData.AddSpawnedNetworkObjects`) apply it, so stored weapons stay hidden from joiners too.
- Leaving a host resets the run, perks, lucky keys and stored weapons.
- Black Hole: only the host's `BlackHoleField` holds and pulls enemies and spiders, since it moves every body, and swallows what reaches the inner circle. Clients see the swallow's shrink from a `surge.enemy` `Swallowed` message. Every machine draws the field and its inner ring (other machines from a `surge.fx` message), and only the caster's machine plays the sound, as with the other abilities. The kinetic pulse's enemy launch (`PulseKnockback`) runs inside `ApplyPulsePhysics`, so it is host-only too.

## Stats (`stats/Net/StatsNet.cs`)

- Re-keyed from `PlayerInput` to `PlayerController`. Players register through `LobbyController.AddPlayer`/`RemovePlayer` (runs on every machine for every player). `PlayerData.PlayerId` is now the game's `playerID`; ids and names are refreshed lazily because `AddPlayer` runs in `Awake`, before the host assigns `playerID`.
- Tracking (hits, kills, timers, maps, perks, pain level) runs only where `Net.IsAuthority`; `HitLogic` entry points and the `PlayerTracker` mutators return early on a client.
- `stats.snapshot` host → all about once a second while a session runs (from `PauseHandler.Update`), and immediately on session start/stop with a reason byte. It carries the session fields and every player's counters, totals, dictionaries and accuracy. The client reads it straight into its `PlayerTracker` data (`RemoteDataFor`), with `RemoteAlive` standing in for the alive timer.
- `stats.titles` host → all after `CalculateAndStoreTitles`; the client calls `TitlesManager.SetRemoteTitles` and mirrors the show/pull logic (`OnRemoteTitles`), using the host's `restartRequested` flag from the snapshot.
- Online names use the game's `playerName` (e.g. `Dylan P1`); offline keeps `Player N`.
- `StatsModApi.RegisterCustomTitle` now takes a `PlayerController` leader. That is a public API change; `API_VERSION` is still 1.

## Open questions and risks

1. **Cooldown drift.** A client fires an ability locally and asks the host; if the host's instance refuses (its cooldown or perk check differs), nothing tells the client. Either echo a result or make the client wait for the host's `surge.fx`/state before playing the effect.
2. **Perk index parity.** The game's `ModSyncResponceClientRpc` syncs `_currModsState` by index, so our perks must be appended in the same order on every machine. The same-version rule guarantees that.
3. **Prefab handler path.** `CustomEnemyNet` relies on NGO calling `PrefabHandler` before the prefab lookup on clients (verified in the decompiled 1.0.0-pre `NetworkSpawnManager.CreateLocalNetworkObject`), and on `GlobalObjectIdHash` being writable at runtime. Verified on 2026-10-18.
4. **Mid-session joiner.** A joiner gets the state/roster/snapshot at once, but the stats UI isn't told a session started (only the `Started` snapshot does that), so it stays on whatever page it was.
5. **Stats restart.** When the host restarts without visiting the lobby, clients only show titles if `restartRequested` arrived in the stopped snapshot; verify the ordering in practice.
6. **Pausing.** `PauseHandler` keys off `Time.timeScale`, which the host controls online. Clients ignore pause for their timers; session time on a client interpolates from the last snapshot and keeps running through a host pause until the next snapshot corrects it.
7. **Telemetry.** `SpiderSurgeStatsManager` records only on the authority and now logs `PlayerId`; the JSON key stays `playerIndex`. The relay sees online matches as the host's.
8. **Black Hole on a client.** The pull shows through the game's own position sync, a Wasp Cocoon's too, since the host moves its kinematic body and its `NetworkRigidBody2DTransform` sends the position.
9. **Not touched:** leaderboards, the Surge versus variant, SpiderInspector's `act/start?surge=` on clients, and any client-side prediction.
10. **Testing.** Needs two game instances with separate EOS logins. The Steam build only runs once per PC, so the Xbox build is the second instance, with SpiderInspector on another port (`spiderinspector-port.txt` next to its DLL, or `SPIDERINSPECTOR_PORT`). The host turns online mode on (`GameSettings.SaveGameMode(true)`) and starts through `HudController.StartSurvival(config, false)` or `StartVersus(false)`; the client sets `HudController.bucketId` to the host's `GameController.bucketId` and calls `ConnectBucketId` (the first attempt sometimes fails; retry once).

    Verified on 2026-10-18 with both sides modded: the handshake; shared settings, read-only on the client; Surge state and perk levels; a client's Pulse and Freeze Time run on the host; custom enemies with their names and shields; the laser ring held by Freeze Time on both sides; owned kill credit and the client's mirrored stats, ids and names; the perk vote from both players; a client leaving and rejoining mid-run (it gets the frozen enemies, stats and perks); the host leaving; the Teams roster, a client's pick, the host's team count over the client's own, a team round, and a whole team leaving mid-match.

    Not yet run: the end-of-match titles and win screen on a client, Surge versus, and more than one player per machine online.
