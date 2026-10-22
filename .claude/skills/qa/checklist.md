# Review checklist

Ask each question of every change. The examples are real bugs this mod has had.

## Game state

- **Paused, a menu open, the perk screen, or a level loading or ending:**
  - Does the feature still act? It shouldn't. Ability casts once went off while the game was paused.
  - Does its UI hide under menus? The stats windows once showed over the pause and lobby menus.
  - Does input in a menu leak into the game? The D-pad once switched the stats windows inside menus, and the spiders moved while the Surge guide was open.
- **Each survival phase:** before the first wave, between waves, during a wave, a lost wave, game over, restart, a cancelled restart, the Tiers of Heck win screen and its Restart, and returning to the lobby.
  - Cocoons once kept hatching after a lost wave.
  - A cancelled restart once restarted the session.
  - The restart flag once stayed on after the win screen.
- **Modes:** Surge, plain survival, versus and custom maps. Does the feature run only where it should, and switch off cleanly when the mode changes?
- **Wave edges:** wave 1, waves 30 and 60 (the ultimate phases), and a starting wave of 0 in a custom config. The wave budget log once skipped wave 1.

## Spider lifecycle

- **Every death cause:** `Damage`, `Disintegrate` (lava), `FellOffMap`, `StuckInWall`, a shield absorbing the hit, and immunity blocking it.
- **Astral Heckhound:**
  - A hit makes a ghost (`astralDead`, not `dead`). The ghost can be revived (`DisableDeathEffect`), or time out after 7.5 s; at the timeout `dead` is set and the spider's root is switched off in the same frame.
  - A disintegrated ghost is never marked dead and is destroyed after 1 s. While it dissolves it reads `dying` and `astralDead` with `isAlive` still true; Neural Backup once revived one.
  - Count a death only when the spider really dies. Storage once saved a death at the ghost hit, and Stats had to take back kill credit and streaks on a revive. The kill and solo streaks the ghost hit ended must come back on a revive too.
- **Dying in the middle of something:**
  - an ability's duration, or a weapon swap (storage left a hidden weapon in the level);
  - your own cast: Neural Backup's 10 s cast finished for a caster who had really died and could pick them as the player to bring back;
  - a Banish pull (an astral death during the pull had to dissolve the spider);
  - an enemy's telegraph.
- **Respawn:** is per-spider state rebuilt, and is anything left over from the old spider?
- **A spider removed without dying** (a pad removed, round end, level change): do the `OnDestroy` and `OnDisable` paths run, and do they save or clean up correctly?
- **All players dead, or one left:** the airborne and web swing timers once kept running on death and while astral.

## Players

- Test with 2 to 4 pads. P1 is playerID 0. Anything keyed by playerID, or by pad, needs a P2 test.
  - P2 to P4 once lost the BiggestBoom, Oof and LivingMine achievements because the mod overwrote `Weapon.ownerWeaponClientId`.
- Per-player settings such as the ability remap:
  - Do they work in the lobby, in plain modes and while the spider is dead? The remap gesture once worked only in Surge runs.
  - Are they forgotten when the player leaves? They are kept by slot (`playerIndex`), so the next player to join that slot once inherited the remap.
- Players are listed in ID order, not join order.
- A keyboard and mouse player: holding a key in the lobby joins one.

## Ownership and credit (Stats)

- **The weapon owner** after a throw, a drop, a pickup, a launched blade, or storing and restoring a weapon.
- **Explosions:** grenades and mines, armed or not, chain explosions, self kills and friendly kills.
- **Kills:** credit, streaks, and the best streak after a kill is taken back; enemy kills against player kills; titles need 2+ players and no tie.
- Never write into game fields the game reads itself.

## Harmony patches

- **A prefix that returns false skips the original, not the other prefixes or the postfixes.** Every other patch on that method must check `__runOriginal` (or the flag the cancelling patch sets) before it records anything. Surge once counted hits Teams friendly fire had cancelled as Null Field blocks, and Stats recorded knockbacks Teams had cancelled.
- **A hit reaches a spider two ways.** `SpiderHealthSystem.Damage` is the lethal one. `Impact` alone knocks back, stuns and cuts the web: blasts outside their kill range, saws, zaps and energy balls. A gate on one path must cover the other. Friendly fire on Ignore once blocked teammates' kills but still pushed and stunned them.
- **A patch that never runs:** is its target ever called, and does its type exist in a scene (`find_assets.py <type> --scenes`)? Teams had a `LobbyScreen` patch for a type no scene uses.
- **Exceptions:** a patch that throws can cancel the game method it watches. An achievement check must not be able to break a revive; wrap it in `try`/`catch`.

## Achievements and telemetry

- Unlock only once the outcome is final. Unstoppable Force once unlocked while the Biggest Grenade blast was still hitting, before the spider had survived it.
- A choice the game makes for the player is not the player's choice. Yay I Guess? once unlocked on a card the perk timer picked.
- Self-cases: does reviving, killing or banishing yourself count? Healer doesn't count reviving yourself.
- Counts per wave or round: a Null Field save once went to the wave after the hit, and counted even when the spider died a moment later.
- What telemetry sends for a plain Survival run, a run with no abilities, and a run that ends on a perk screen.

## Objects and Netcode

The local host still runs Netcode.

- `SetActive(false)` doesn't despawn a network object, and an inactive network object under a spider outlives the spider. Despawn it on the host.
- A destroyed object can still be referenced: check `== null` the Unity way.
- A coroutine stops when its object is disabled, and anything it would have finished never happens.

## Timers

- `WaitForSeconds` and `Time.time` stop when the game is paused or in slow motion. `WaitForSecondsRealtime` and `Time.unscaledTime` don't. Which one does the feature need? `game-rules.md` lists the game's own. The remap gesture needed unscaled timers.
- Cooldowns and durations across a death, a respawn, a level change and an ultimate swap.
- Records matched by time across a pause must match on game time. Teams once matched a blast to its owner within 2 real seconds, so a pause between the blast and its first knockback left the hit unowned.

## Enemies (Surge)

- Frozen by Freeze Time (don't combine it with SpiderInspector's freeze), shielded, struts damaged (PowerRoller, ExplodingRoller), spawned during a telegraph, cleared, lava deaths.
  - A frozen enemy once lost colour changes the game made while it was frozen.
  - A cocoon's shield breaking during the telegraph must hatch a plain Wasp.
- An enemy dying during its own effect: laser rings once stayed on after their wasp died.
- Pulse and other pushes on frozen or shielded enemies: Kinetic Pulse once moved frozen enemies.

## Input

- Default buttons, the remap gesture and its reset.
- Conflicts: Surge's ultimate on the D-pad against the stats window switching, and stick presses.
- One button, two features: Y picks a team in the Teams lobby and can also be a remap target. While Start is held it must only remap.
- Controller navigation in mod menus: up and down, submit, back.
- Keyboard shortcuts such as the backtick.
- Fire reacts to presses, not to holding.

## UI

- Resolutions 1024x768, 1920x1080 and 2560x1440, and changing between them.
  - Windows must rescale only after the size settles, and stay on screen.
  - Saved positions are stored as a percentage of the screen.
- Text: wrapping, scaling, and other languages. Find I2 labels by path or `mTerm`, not by English text.
- Feedback text: the remap feedback once didn't scale.
- Colours from the config: an invalid value falls back to the default with one warning.

## Config and settings

- A fresh config gets defaults.
- An invalid value falls back with one warning, not one per frame.
- Numbers on German, Spanish or French Windows (decimal comma).
- Migrating an old layout keeps the values.
- "Reload the settings file" works, and live settings change live while restart-only ones say so.
- Reset buttons ask first. Click by exact text.

## Logs and performance

- No new warnings or errors, and no log line every frame.
- Remove leftover debug logs; a per-spawn log line once had to go.
- No `FindObjectsOfType`, LINQ or allocations every frame in hot paths. Cache what's measured each frame (display snapshots, title sizes).
- OnGUI runs several times a frame, so its garbage adds up. These allocate:
  - string concatenation and `int.ToString()`;
  - passing an instance method as a delegate (`GUI.Window(id, rect, DrawWindow, ...)`), which creates a new delegate each call; keep it in a field;
  - settings reads that build their key or parser on every call.
  The Stats big window once made 14 KB of garbage a frame. Measure it (recipes.md, "Performance").

## Docs

- Check `main` against `NEXT_RELEASE.md` and the part READMEs, never against `README.md` (the release).
- Check every number in a docs table against `Consts`, and every claim against the code. A README row once said frozen enemies do no harm, but their blades, saws and poison clouds still hurt.
- Links between docs: a part README link to `../README.md#<section>` breaks when that section only exists in one of `README.md` and `NEXT_RELEASE.md`.

## Integration

- Turning parts on and off ("Enable or disable parts").
- A legacy `StatsMod.dll` or `SpiderTeams.dll` disables the matching part.
- Stats, Teams and Surge together, such as friendly fire with Pulse.
- Telemetry stays off during tests.
- The update check.
