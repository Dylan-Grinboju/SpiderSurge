# In-game player removal

While playing, open the pause menu and choose **Remove Players**. Pick the numbered player and confirm **Remove player**. Cancel or Back leaves the players unchanged. The game keeps at least one player; use **Return to lobby** to end the game.

The host can remove any player. A remote client can remove its own local players. Removing one slot does not disconnect that machine's other players. Dead players and disconnected controller slots can also be removed.

The host validates ownership, the player slot and its network object identity, loading state, and the number of remaining players. A stale selection cannot remove a replacement occupying the same slot. The host despawns the character and controller through Netcode; the owner releases only the matching local input. Native player destruction clears scores, assigned spawn points and controller reconnect tracking. The lobby's existing remove-all action stays available in the lobby.

Validation: `pwsh -NoProfile -File tests/PlayerRemoval.Tests.ps1` exercises request permissions, stale identity, missing players, loading, editor/lobby restrictions, individual cleanup, and consecutive requests protecting the last player. Live checks must cover the picker, cancellation, controller navigation, live/dead/disconnected removal, round continuation, slot reuse, and a Steam host with an Xbox client running the same build.

## Verification on 2026-11-05

- Release build: no warnings or errors; runtime access scan: zero non-public references; 18 automated checks passed.
- Local plain Survival: cancellation, controller navigation, individual removal, released input, last-player protection, Escape/Start closing the picker, Escape cancelling confirmation, dead-player removal, and rejoining the released slot in the lobby passed.
- Steam host and Xbox client, two players each: host removal of one remote slot preserved its sibling and input; client UI offered only its local slot; the client's removal request reached the host; both peers agreed on the remaining players; removing the client's final local slot preserved its network connection and host players. Both ran DLL SHA-256 `F1328AD79063D8D2A226A99CA677FB3F49A376435A7534ED96A7D1EB7F6676DF`.
- Physical controller disconnection was not verified: the installed Steam Inspector lacks the disconnect endpoint. Versus- and Surge-specific transitions were not exercised. Known base-game scene/input log noise occurred; no new removal errors were observed on either peer.
- Temporary test DLLs, settings and saves were restored and hash-checked on both installs, including the Xbox cloud-save container.
