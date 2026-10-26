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
- Steam/Xbox source comparison: `Shotgun.cs` and `DamageOnParticle.cs` are identical; the perk coroutine and `OpenLobby(bool)` bodies also match. This is source compatibility evidence, not an Xbox runtime test.
- Test telemetry was disabled. The Steam process was closed and the snapshot tool verified restoration of the original mod, config, and saves, with no new match logs.

## Remaining draft checks

The virtual-gamepad join operation crashed the test process twice (Windows exit code `0xC0000005`); the menu and coroutine checks above were subsequently completed without it. The cause has not been established. Two-instance Steam-host/Xbox-client gameplay, remote disconnect behavior, and audible verification of the last sound variants remain pending.

Before merging, exercise a real timed-out perk choice with a remote client and confirm that both peers receive the same selected perk/map. Exercise normal return-to-lobby and connection-in-progress rejection on both peers. Check shotgun firing and pellet-hit audio on both peers, including the final sound variants. Also verify that disabling Game fixes restores the original behavior after restart.
