# Controller pause regression

Checked on 2026-10-20 against `origin/main` at `7a76656`, using the Steam game and SpiderInspector virtual gamepads.

## Cause and scope

`HudController.Update` polls Escape and `Gamepad.current.startButton.wasPressedThisFrame`. `Gamepad.current` is one device, not the set of local players. Another controller's input can make it current before the HUD checks Start, losing the first controller's press. `PauseController.OnPause` is not attached to the shipped player prefab and does not provide a per-player fallback.

The recent reconnect fix preserves player/device pairing, but does not change this pause check. Neither the input remapping changes nor the settings menu changes fix it.

`ControllerPauseFix` replaces the two `Gamepad.current` reads inside `HudController.Update` with a lookup for any gamepad whose Start button was pressed this frame. It leaves the global current device unchanged and keeps the game's keyboard handling, pause restrictions, single toggle per update, and existing `HudController.Pause` patches. Its transpiler refuses an unexpected method shape instead of applying a partial replacement. It belongs to the existing Game fixes part.

## Reproduce before the fix

With the game ready in the lobby and SpiderInspector's game lock held:

1. Add two joined virtual pads with `/api/input/add`.
2. Confirm `/api/call?type=Gamepad&get=current.name` is the second pad.
3. Run these requests in one `/api/batch`:
   - `/api/input/set?pad=1&hold=pause`
   - `/api/input/set?pad=2&move=0.7,0`
   - `/api/wait?until=real:0.15&timeout=1`
   - `/api/call?type=HudController&get=paused,canPauseMenu`
4. Before the fix, `paused=false` despite `canPauseMenu=true`. Release pad 1 and press Start on pad 2: `paused=true`.

Virtual pad state changes do not reliably make that pad current; check `current.name` explicitly. This allows the missed non-current Start press to be reproduced deterministically. With physical controllers, keep moving another controller's stick while pressing Start.

## Checks after the fix

| Check | Result |
|---|---|
| Pad 1 presses Start while pad 4 is current and sends stick input | Opens pause; current remains pad 4 |
| Each of four joined pads presses Start | Each opens pause |
| A different pad presses Start while paused | Resumes, tested for all four pads |
| Start stays held across multiple updates | No repeated toggle |
| All four pads press Start in the same update | One pause toggle |
| `canPauseMenu=false` and a non-current pad presses Start | Pause stays closed |
| Escape with gamepads connected | Opens pause |
| Escape and Start pressed in the same update | One pause toggle |
| Escape with no gamepads connected | Opens pause |
| Non-current pad in a four-player Surge run on `1.Launch` | `paused=true`, `gamePaused=true`, `Time.timeScale=0` |
| Start from SpiderSurge's settings root | Returns to Mods with SpiderSurge selected; game stays paused |
| Dead player's non-current controller presses Start | Pauses the surviving players; `Time.timeScale=0` |
| Release build | Succeeds with zero warnings or errors |
| Runtime error log excluding known base-game noise | No errors |

Release Start and allow an update before pressing it again. SpiderInspector queues keyboard events but applies virtual pad state immediately; for the simultaneous Escape/Start case, process the queued Escape with `/api/call?type=UnityEngine.InputSystem.InputSystem&method=Update` before setting Start in the same batch. Confirm both `wasPressedThisFrame` values are true before waiting for the HUD update. Without this, the test sends two presses in different updates and correctly toggles twice.

The live test used a temporary DLL. The game was closed afterward and the install snapshot restored and hash-verified, including the original mod, configuration, and saves. No physical-controller hardware test was performed.
