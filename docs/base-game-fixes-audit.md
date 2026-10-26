# Base-game fixes audit

Focused review of survival perk selection, shotgun particle/sound handling, and the return-to-lobby path. These findings are additional to the controller, Whisp cooldown, off-screen indicator, colour-unlock, and custom-map warning fixes already on main.

Source: the local Steam decompile of `MyScriptAssembly.dll` (Steam build 13634498, assembly SHA-256 prefix `0e3f5d4ddec8837a`). Source locations below are relative to that assembly's decompiled folder. The affected methods also match the local Xbox decompile. No decompiled game files are included or modified by this PR.

## Findings and patches

| Priority | Bug and trigger | Evidence | Patch |
| --- | --- | --- | --- |
| P2 | The last survival perk/map card can never win when the timer expires. With two cards, the first always wins. | `SurvivalModeHud.cs:455`, `ActivateChoiseViewTimer`: the integer random upper bound is `availableModifiers.Length - 1`, although the bound is exclusive. | `RandomArrayIndexFix` patches the coroutine's `MoveNext` to use the full array length. The countdown, host check, modifier/map pairing, submission, and RPCs remain in the original method. |
| P3 | The last shotgun firing sound and last pellet-hit sound never play. | `Shotgun.cs:139`, `ShootClientRpc`, and `DamageOnParticle.cs:27`, `OnParticleCollision`, repeat the same exclusive-bound error. The shipped Shotgun prefab has four pellet-hit sounds. | The same narrowly matched transpiler corrects both sound bounds. Each peer continues to play its own cosmetic sound through the existing callbacks. |
| P2 | Return to lobby does nothing if the connecting view is hidden but its GameObject is still active. | `HudController.cs:445`, `OpenLobby(bool)`, uses `IsActive()` as a visibility test. That reports component/GameObject activation, not whether the Doozy view is displayed. | `ReturnToLobbyFix` bypasses only that guard when the view is neither visible, showing, nor hiding. It preserves the original disconnection and cleanup path and the caller's explicit bypass option. |

The random-bound patch requires exactly one matching array-length subtraction in each target. It throws if the game instruction pattern changes, rather than silently rewriting an unrelated random call. Replacing the subtraction instructions with no-ops preserves branch labels and exception boundaries.

The particle-hit owner dereference was investigated but is not included: dropping a weapon retains its owner reference, so that action alone is not evidence of a null-owner bug.

## Validation

- Release build: passed, zero warnings and errors. MinVer read the existing tags with a process-local Git safe-directory setting; no version or release tag was changed.
- Steam startup: all 13 Game fixes patches loaded, with no patch errors.
- A temporary harness in Unity's Mono runtime checked all three actual game method bodies: only the two subtraction instructions changed, other operands and control-flow metadata were preserved, and an absent pattern was rejected.
- The actual survival timer coroutine was exercised on the local host with RNG seed 12345. The harness advanced its final countdown tick, intercepted submission to avoid applying perks, verified the chosen modifier/map pair, and restored arrays, timer, RNG state, and temporary patches afterward.

| Offered cards | Original counts | Patched counts |
| --- | --- | --- |
| 1, 100 trials | Not sampled | 100 |
| 2, 200 trials | Not sampled | 104, 96 |
| 3, 200 trials | 104, 96, **0** | 71, 64, **65** |
| 4, 200 trials | Not sampled | 57, 48, 47, 48 |

- Lobby guard: a visible connecting view blocked `OpenLobby(false)`; a hidden view allowed a lobby load. Explicitly activating the hidden view produced `isActiveAndEnabled=true` with all three visibility/transition flags false; the patched call still loaded the lobby. The ordinary show/hide cycle deactivated the view in this test, so the hidden-but-active state was constructed rather than reproduced through an online connection failure.
- Steam/Xbox source comparison: `Shotgun.cs` and `DamageOnParticle.cs` are identical; the perk coroutine and `OpenLobby(bool)` bodies also match.

## Gameplay follow-up (2026-10-26)

Tested commit `efd79e875aa82d1dc7e54e016e1eeb937d7a7ae1` in both installed builds, using SpiderInspector 0.5.0 and one virtual controller per process. These were separate local games, not connected peers. Release build and non-public access scan passed (zero non-public references).

| Check | Result and evidence |
| --- | --- |
| Real perk timeout, Steam | Completed three plain-survival waves and let the normal 30-second timer expire. Offered modifiers `[18,5,28]`, map indices `[26,32,26]`; selected index 0, modifier 18 reached level 1, and wave 4 loaded its paired map `32.Ant Domain`. Submission was not intercepted in this follow-up. |
| Real perk timeout, Xbox | Offered modifiers `[3,20,17]`, map indices `[33,27,19]`; selected index 1, modifier 20 reached level 1, and wave 4 loaded its paired map `33.Rock Loop`. |
| Return to lobby, both builds | Used Pause > Return to lobby > YES after constructing the hidden-but-active connecting view. Both loaded Lobby. The three visibility/transition flags were false while `isActiveAndEnabled` was true. |
| Shotgun audio, both builds | Fired through the player input path at level geometry in `2.Smash`. Steam fired 3 volleys; Xbox fired 15. Both selected and played `shotgun_shot5` (index 4, exclusive bound 5) and `shotgun_hit4` (index 3, bound 4). A temporary observer called the original RNG and `AudioSource.Play` and recorded `isPlaying=true`; it did not force the choices. Audio remained muted, so this verifies Unity playback, not listening quality. |
| Disable and restart, Steam | Switched Game fixes off through Mods > SpiderSurge > Mod parts. `Fixes.Running` remained true until restart, then became false with the log `Game fixes is turned off`. Actual firing returned to bounds 4 and 3, excluding the last variants. The same hidden-active Return to lobby confirmation failed to load Lobby within 4 seconds; the view flags still matched the enabled test. |

Immunity and frozen enemies kept the test players alive. Waves ran normally for perk timeouts; waves were frozen for audio testing only. Virtual-pad creation with `join=false`, followed by a separate join request, worked on both builds. The earlier combined creation/join crash was not diagnosed or fixed.

Raw requests and responses are retained locally under `%TEMP%\qa-base-game-fixes-followup\evidence`. The audio captures end in `211406-745405-21712.json` and `211825-648821-21713.json`; the disabled baseline is `212007-002613-21712.json`, and the blocked lobby wait is `212031-779431-21712.json`.

The logs were not entirely clean: scene transitions reported duplicate global lights and inactive-view coroutine warnings, Xbox achievement requests failed, and cleanup with Game fixes disabled reproduced the existing colour-unlock RPC exception. The tested firing actions returned no warnings. No new patch exception was observed.

Both test processes were closed. Steam snapshot `base-game-fixes-followup-20261026` restored and verified the original mod/config/save files, with no new match logs. The Xbox backup restored all 13 original local files with zero SHA-256 mismatches; added Inspector files and test history files were moved into the local test archive. Telemetry was disabled during testing. Xbox cloud storage was not snapshotted: the test set its online/offline preference to offline (matching the backed-up local save), but its previous cloud value was not recorded. No cloud-save restoration is claimed.

## Remaining draft checks

Steam's normal multiplayer login failed repeatedly: `PlatformInitializer.Initialized` stayed false and the game announced that login to multiplayer services failed. Xbox logged in successfully, but no cross-platform lobby could be established. No production authentication checks were bypassed.

Before merging, exercise a timed-out perk choice with a remote client and confirm that both peers receive the same selected perk/map. Exercise return-to-lobby, connection-in-progress rejection, and remote disconnect behavior on both peers, and verify shotgun audio on the remote client. Reproducing the hidden-but-active view through an actual failed connection also remains pending. The local gameplay checks above passed; they do not establish multiplayer synchronization.
