# Recipes

Requests are written as `/api/...` paths after `http://127.0.0.1:21712`. Each one has an MCP tool (`start_mode`, `step_game`, `call_game`, ...); fall back to `curl -s` when a tool is missing. Pass the same `owner=<session name>` on curl requests as on `lock_game`.

Paths used below:

| Name | Path |
|---|---|
| Game | `C:\Program Files (x86)\Steam\steamapps\common\SpiderHeck` |
| Mods | `<Game>\Silk\Mods` |
| Config | `<Game>\Silk\Config\Mods\SpiderSurge.yaml` |
| Build | `<worktree>\bin\Release\net472\SpiderSurge.dll` |
| Save | `%USERPROFILE%\AppData\LocalLow\Neverjam Studio\SpiderHeckApp` |
| Skill | `C:\Users\dylan\My_Data\Coding\Spiderheck\SpiderSurge\.claude\skills\qa` |

## Setup

1. **Is the game free?** `ping_game`. If another session holds the lock or `SpiderHeckApp.exe` is starting, wait or message the holder. Don't close a game you didn't launch.
2. **Build:** `dotnet build -c Release` in the worktree. A build outside git history has version `0.0.0-dev`, which the update check counts as old.
3. **Access scan:** `powershell -NoProfile -ExecutionPolicy Bypass -File <Skill>\access-scan.ps1 -Dll <Build>`. Bash can't run the script without `-ExecutionPolicy Bypass`. It must print `Non-public references: 0`; the unresolved scopes (0Harmony, Silk, ...) are normal. Use `AccessTools` for anything it lists.
4. **Check what is installed:** list `<Mods>` recursively. Only `SpiderSurge.dll` and `SpiderInspector.dll` belong at the top. A `StatsMod.dll` or `SpiderTeams.dll` there turns the matching part off.
5. **Back up** into `%TEMP%\qa-<topic>\`, and note every SHA-256 hash (`Get-FileHash`) in your notes:
   - `<Mods>\SpiderSurge.dll`, copied to `<Mods>\Disabled\SpiderSurge-before-<topic>\` as well (never another subfolder of `Mods`: Silk loads it twice);
   - the config;
   - `<Save>\SaveFile.es3`, `SaveFile.es3.bac` and `RemoteConfigCache.json`. Achievements are saved in `SaveFile.es3` under the key `SpiderSurge-Achievements`;
   - whether `<Save>\SpiderSurge\` exists. It holds `titles.dat` (the title collection) and its `.bak`, which the first run with titles creates;
   - the newest file in `<Game>\Silk\Logs\SpiderSurge\`, so you know which match logs are yours.
   - `snapshot_install` (name `qa-<topic>`) does all of this but the `Disabled` copy, with the game closed and before you change anything: see "SpiderInspector QA tools".
6. **Telemetry off:** with the game closed, set `surge.telemetry: false` in the config. The user's config has it on, a Surge test run would upload a report, and reports that failed to send earlier go out at startup. In a running game, `/api/call?type=SpiderSurge.ModConfig&method=SetTelemetryEnabled&args=false` does the same. The mod rewrites the YAML when it saves (it moves keys and adds missing ones), so restore it by copying the backup, never by editing the value back.
7. **Install the build:** with the game closed, `Copy-Item <Build> <Mods>\SpiderSurge.dll -Force`. Straight after `Stop-Process` it fails with "user-mapped section open"; retry for a few seconds.
8. **Launch:** `launch_game` with your session name and a note. It waits for `game.ready`, mutes the game and takes the lock.
9. **Check the log:** `/api/log?level=warning` once after launch, and keep its `next` for later reads. Startup errors from the mod belong in the report. The update check logs `Latest version: X, current version: Y`.

## Starting a run

- **Two players:** `/api/input/add` twice, then `/api/time/step?ticks=30` so both join. P1 is playerID 0. Add 4 for tests like Total Destruction (3 teammates in one blast).
- **Surge survival:** `/api/act/start?mode=survival&map=2.Smash&surge=1` (`surge=0` for plain survival).
- **Then:**
  1. `/api/act/freeze?waves=1&enemies=1`;
  2. `/api/time/step?until=loaded,scene:2.Smash`;
  3. freeze again, since the level load resets it;
  4. `/api/act/immune?player=all&seconds=9999` when deaths would get in the way.
- **Versus:** `/api/act/start?mode=versus&map=<name>`. Names come from `/api/act/list`.
- **Teams:** turn the part on in Mods > SpiderSurge > Enable or disable parts (it needs a restart), or set `parts.teams: true` in the config before launch. Team options are on the Versus Battle setup page.
- **Back to the lobby:** `/api/act/lobby`, then `step?until=loaded,scene:Lobby`.

**2.Smash positions:**
- Safe standing spots: (-228,-175) and (-20,-95).
- The lava's top is at y ≈ -327. To kill a spider in lava, teleport it to about y=-340.
- Open sky above (-20,-55), for measuring knockback.

## Waves and perks

- **End a wave:** `/api/act/complete-wave`.
- **Reach a perk choice:**
  - The game offers one only when `CurrentWave == _nextPerkChoiceWave` (`/api/call?type=SurvivalMode&get=CurrentWave,_nextPerkChoiceWave`).
  - Unfreeze waves, then repeat `complete-wave` + `step?until=perk` until the wave gets there.
- **Ultimate phases:** set `SurvivalMode` `CurrentWave` to 30 or 60 (`call ... set=CurrentWave&value=30`). That runs Surge's phase setter.
- **Grant a perk:** `/api/call?type=PerksManager&method=OnSelected&args=<key>`.
  - Keys: `immune`, `ammo`, `pulse`, `storage`, `freeze`, `<ability>Ultimate` (for example `pulseUltimate`), `abilityCooldown`, `abilityDuration`, `shortTermInvestment`, `longTermInvestment`, `perkLuck`.
  - A level: `type=PerksManager&method=SetPerkLevel&args=<key>,2`.
- **Game modifiers by list index** (works on any SpiderInspector):
  - Read: `/api/call?type=ModifierManager&get=_modifiers.17.data.key,_modifiers.17.levelInSurvival`.
  - Set: `set=_modifiers.17.levelInSurvival&value=2`.
  - `moreGuns` is index 17 and `astralHeckhound` is 45. Check the key before setting; versus uses `levelInVersus`.
  - SpiderInspector 0.5 does it by name: `/api/act/modifier?name=astralHeckhound&level=1`.

## Surge abilities

- **Buttons on a virtual pad:**
  - Ability: `/api/input/tap?pad=N&button=LB`.
  - Ultimate: `button=LeftStick,RightStick` (not `L3`).
  - With `surge.dpadUltimate: true` in the config, the ultimate is `DpadUp`.
- **Abilities exist only in a Surge run.** In the lobby, `/api/call?type=SpiderSurge.SurgeGameModeManager&method=SelectMode&args=true`, or `act/start ... surge=1`.
- **Skip a cooldown:** `/api/call?type=SpiderSurge.<Name>Ability&id=<spider id>&method=SetCooldownToZero`.
  - The spider ids come from `/api/game-objects?type=SpiderSurge.<Name>Ability`. Read the owner from each entry's `playerID`; the positions are spider roots, not bodies.
  - After granting a new ability, or after a respawn, its ultimate can still be on cooldown. When a press does nothing, read `get=isActive,onCooldown,isUltimateActive,cooldownEndTime` on the component before suspecting the code.
- **Neural Backup** (`immuneUltimate`): the cast takes 10 s and a ghost lasts about 7.5 s (452 ticks), so cast first, then make the ghost 2.5 s or more into the cast. A ghost can't start a cast. With nobody to bring back, 3 friendly wasps spawn.
- **The ability-button remap:** while paused, `/api/input/set?pad=N&hold=Start`, step 5 ticks, `input/tap?button=RB` twice 8 ticks apart (stepping between), release, then `/api/call?type=SpiderSurge.PlayerControlSettings&method=GetPlayerAbilityButton&args=<playerIndex>`. A spider's live binding is `get=ActivationButtons` on its ability.
  - Only shoulders, triggers, face buttons and stick presses can be mapped; the D-pad can't.
  - Holding Start opens the pause menu. In the lobby, Y in that menu opens the invite-code box; close it with `ui/click` on its Back.
  - Test it in the lobby, in a run, in a Teams round and while dead, then remove the pad and join a new one: the new player must start on L1.
- **Fire a pulse without the perk:** `type=SpiderSurge.PulseAbility&id=<id>&method=TriggerPulse&args=false` (`true` is the ultimate).
- **Don't combine `act/freeze?enemies=1` with Freeze Time.** The ability saves the inspector's locked constraints as the originals, so enemies stay locked after the thaw. Unfreeze with `enemies=0` first.

## Hits, deaths and respawns

- **An owned hit** (credit goes to a player):
  1. `/api/act/give?player=0&weapon=Grenade`;
  2. arm it: `/api/call?type=Grenade&id=<id from give>&method=Use&args=true`;
  3. `method=ExplodeClientRpc&args=x;y;0`.
  - Without `Use`, the blast has no owner.
  - Blasts linger about 0.75 s and break shields and struts nearby. Move to fresh ground between tests.
- **Shooting:**
  - `/api/act/fire?player=0&x=&y=`, standing 20+ units back: a Shotgun's muzzle is about 15 units out.
  - Pad aiming is unreliable; use `act/aim` or `act/fire` with x/y.
  - Shotgun pellets spread, so one volley at 3 frozen wasps may kill only one. Use a Railvolver for a sure kill per shot.
- **A kill:** `/api/act/kill?player=0`.
  - The spider must not be immune: `/api/act/immune?player=0&seconds=0` first.
  - Spawn immunity is about 0.33 s.
  - A shield absorbs the hit unless `ignoreShield=1`.
- **Lava:** teleport into it, or `call type=EnemyHealthSystem&id=<enemy>&method=Disintegrate` for an enemy.
- **Respawn:** `/api/act/respawn?player=0`. Without a player in survival it uses the end-of-wave respawn and undoes a wave lost to everyone dying.
- **Enemies:**
  - `/api/act/spawn-enemy?enemy=<name>&x=&y=&frozen=1`. Names come from `/api/act/list` and match loosely.
  - ExplodingRoller fuse: `call type=ExplodingRoller&id=<id>&method=TriggerExplosive`; it waits while frozen.
- **Astral Heckhound:**
  1. Set the modifier to level 1, then respawn the spiders: they read it when they spawn.
  2. A kill makes a ghost: `/api/call?type=SpiderHealthSystem&player=0&get=astralDead,dead`.
  3. `method=DisableDeathEffect` revives the ghost; doing nothing times it out after about 452 ticks.
  4. A lava death of a ghost (`Disintegrate`) destroys it without `dead` ever being set.
  - `step?until=astral:P` waits for a ghost, and the events show `astralDeath` and `astralReturn` (with `astralTicks`). While paused, a kill's `astralDeath` event shows up after the next tick, though `astral:P` is true at once.

## Reading the mod's state

- **Stats per player:** `/api/call?type=StatsMod.PlayerTracker&method=GetActivePlayers&depth=3` (Kills, KillStreak, IsAlive, ...).
- **Statics:** `/api/call?type=<Type>&get=<field>`, for example `type=StatsMod.HitLogic&get=lastEnemyHits`. `get=*` lists every member.
- **Members:** `/api/members?type=<Type>&filter=<text>` before guessing a name.
- **Leftover objects:** `/api/game-objects?type=Weapon&inactive=1` shows hidden weapons a feature left behind.
- **Storage:** `type=SpiderSurge.StoragePersistenceManager&method=GetStoredWeapons&args=<playerID>` is what the next respawn restores.
- **The game clock the mods store:** `/api/call?type=UnityEngine.Time&get=time`.
- **Harmony patches:** `type=HarmonyLib.Harmony&method=GetAllPatchedMethods` (a huge reply; grep it).
- **Achievements:** the unlocks are in `SaveFile.es3` under `SpiderSurge-Achievements`; the Achievements page under Mods > SpiderSurge lists them with who and when.
- **Titles:** `type=StatsMod.TitlesManager&get=Instance.CalculationCount,Instance.LastTitlesShown,Instance.LastNewlyUnlocked` after a run ends, and `type=StatsMod.TitleCollection&get=FilePath,Readable` for the collection file.
- **What telemetry would send, without sending it:** with telemetry off, a Surge run's match report still goes to `<Game>\Silk\Logs\SpiderSurge\SpiderSurge_Match_*.txt`, and `type=SpiderSurge.Logging.SpiderSurgeStatsManager&method=BuildTitlesReport&depth=3` builds the titles part. A plain Survival run writes no local report.

## Menus and UI

- **Open the pause menu:**
  1. `/api/time/release` first: while time is held paused, the menu view never animates in and `ui/buttons` lists nothing.
  2. `/api/call?type=HudController&method=Pause`.
  3. `/api/ui/buttons`.
- **The mod's pages:** `ui/click?text=Mods&list=1`, then `text=SpiderSurge&list=1`, then the part's button.
  - Read the page with `ui/controls`; change it with `ui/set?id=&value=` and `list=1`.
  - Pages rebuild after every change, so ids go stale.
- **Click by exact text only.** A partial "Stats" match once hit "Reset Stats settings to defaults". The Teams reset has no confirmation page.
- **Controller navigation:** `/api/ui/nav?dir=down&times=N`, `dir=submit`, `dir=cancel`. Check `selected` after each.
- **Resolution:**
  - Change: `/api/call?type=UnityEngine.Screen&method=SetResolution&args=[1920,1080,"FullScreenWindow"]`.
  - Restore the user's: `args=[2560,1440,"FullScreenWindow"]`.
  - Step or wait a second for the size to settle before judging the layout.
- **Stats windows:**
  - The small window's rect: `/api/call?type=StatsMod.SmallUI&get=windowRect`.
  - The saved position: `type=StatsMod.ModConfig&get=DisplayPosition`.
- **Screenshots:** `/api/screenshot?width=960`. Crop with `id=` of a UI element, `x=&y=&w=&h=`, or `player=N&radius=30`.
  - Read the PNG after saving it with the MCP tool's `saveTo` or `curl -s -o`.
- **Popups** (achievement unlocks, Teams toasts): Doozy popups run on real time, so while time is held paused they open and close before you can look.
  - The `popupShown` and `popupHidden` events record every popup with its texts, `/api/ui/popups` lists the visible and queued ones, and `step?until=popup:<text>` or `wait?until=popup:<text>` waits for one.
  - To see one on screen: run time (`/api/time/run?scale=1`), trigger it, `wait?until=real:0.5` (or `popup:<text>`), then take the screenshot.
  - On an older SpiderInspector, read `type=Doozy.Engine.UI.UIPopupManager&get=VisiblePopups,PopupQueue&brief=1`.
  - Don't call `UIPopup.GetPopup` to look: it creates a new popup each time.
  - A test toast: `/api/call?type=SpiderTeams.Toast&method=Show&args=["text","#FFFFFF",false]`. Doozy shows one popup at a time; the rest wait in the queue for about 2.6 s each.

## Logs

- **The game's own errors**, seen on the release build too:
  - two `Coroutine couldn't be started because the the game object 'Canvas - Hud'` (or `'View - LoadingScreen'`) `is inactive!` on each scene load;
  - `Cannot switch to actions 'Spider'; input is not enabled`, at the start of a run;
  - `prev: N next: N`, a debug line from `SurvivalModeHud`;
  - `Just show console` (`EngagementScreen`) and `Starting game!` (`VersusMode`), logged as errors;
  - `Serialization depth limit 10 exceeded at 'SerializedMapElement.associatedObjects'` warnings at startup.
  - SpiderInspector leaves these out of the `error` condition and of step and wait warnings, and counts them in `knownNoise`. `/api/log?noise=0` hides them too.
- **SpiderSurge's own warnings,** seen on 2026-10-19 with `main`. Report them only if they are new or repeat more than this:
  - `Attempting to stop Versus session when none is active` (`GameSessionManager.StopSession`), twice at each survival start;
  - `Surge mode not active - skipping ability initialization`, at each spawn outside a Surge run;
  - `SpiderTeams: <Gamepad>/buttonNorth is also the game's 'ShowNames' button`, at startup with Teams on.
- **DOTween `Target or field is missing/null`** warnings when a run starts from the lobby: a tween on a lobby object that was destroyed. Not yet checked against the release build.
- **Anything else:** to tell whether it is new, install the backed-up release DLL, repeat the steps, and compare.
- **Match logs:** a match log in `<Game>\Silk\Logs\SpiderSurge\` written before your run came from another build. Read only your own, by the time you noted in Setup.

## Performance

- **Garbage per frame:** put the state you want to measure on screen, then `/api/performance?gc=5` (`get_performance_stats` with `gc: 5`) . It forces a collection, adds up each frame's growth in managed memory for 5 real seconds, and skips frames in which a collection ran. Read `bytesPerFrame` and `maxFrameBytes`. Frames keep running while time is paused, so this works paused too.
  - On an older SpiderInspector, run `python <Skill>\gc-measure.py <label> --owner "<session name>"` instead: it compares managed memory over 2 s with no collection in between, 4 times.
  - Both include SpiderInspector's own per-frame work: about 3.9 to 5.6 KB per frame in the lobby. The game itself makes about 1.3 KB per frame at 166 fps in a 2-player Surge run.
- **Compare:** measure each state (for example the Stats windows hidden, small, big: `call type=StatsMod.SmallUI&id=<id>&method=ShowDisplay` or `HideDisplay`, ids from `game-objects`), then install the release DLL and measure the same states.

## SpiderInspector 0.5

SpiderInspector 0.5 is installed (2026-10-19). On top of 0.4 it has:

| Request | Does |
|---|---|
| `/api/input/key?key=Backquote&frames=3` | Taps keys (Unity Input System names: `Backquote`, `F1`, `Q`, `C`, `T`, `Escape`). `hold=`/`release=` (`release=all`) keep them down |
| `/api/input/mouse?x=&y=&click=left` | Moves the cursor (screen pixels from the top-left) and clicks; also `press=`/`release=` |
| `/api/input/drag?from=x,y&to=x,y&steps=20` | Drags with the button held, for OnGUI windows and sliders |
| `/api/act/modifier?name=astralHeckhound&level=1[&mode=versus]` | Sets a game modifier by name; without `level` it reads it |
| Condition `field:<Type>[@<id>\|@p<playerID>]:<member>[<op><value>]` | Waits on any field, for example `field:SpiderHealthSystem@p1:astralDead`. Ops: `= != > >= < <= ~` |
| Condition `astral:P` and events `astralDeath`, `astralReturn` | Astral Heckhound ghosts. A ghost's death has `astral: true` and `cause` `AstralTimeout`, `Removed` or the hit |
| `body:<playerID>` call argument | The spider's body GameObject, the one that moves (`player:` gives the root) |

- Check `ping` for `version` 0.5.0 before relying on these. A copy of 0.4 is in `<Mods>\Disabled\SpiderInspector-0.4-backup\`.
- Their MCP tools (`press_key`, `mouse`, `drag_mouse`, `set_modifier`) only exist in sessions started after 0.5 was installed. An older session reaches the paths through `batch`, for example `batch?requests=["/api/input/key?key=Escape&frames=3"]`, or with curl.
- A key works only if the installed SpiderSurge has its shortcut. The backtick opens the guide only in builds with the `SpiderSurge.SurgeGuide` type (`/api/members?type=SpiderSurge.SurgeGuide` fails without it), which no release has yet. Escape (the pause menu) proves the keyboard path itself.

## SpiderInspector QA tools

SpiderInspector PR #4 adds the tools these QA runs were missing. It was installed on 2026-10-19 (DLL hash `A442B3DD`), and the registered MCP server was rebuilt from it. `ping` still reports 0.5.0, so check that `/api/ui/popups` answers (a 404 means an older DLL) before relying on these.

| Tool or request | Does |
|---|---|
| `snapshot_install` (MCP) | Copies every file under `<Mods>` (not `Disabled`), `Silk\Config`, the save files and `<Save>\SpiderSurge\` to `%TEMP%\spiderinspector\snapshots\<name>\`, with SHA-256 hashes, and the list of match logs. Take it with the game closed |
| `check_install` (MCP) | What changed, was added or removed since the snapshot, and the match logs written since. `launch_game` reports the same as `sinceSnapshot` |
| `restore_install` (MCP) | With the game closed: copies back changed or missing files, moves added ones (a new `<Save>\SpiderSurge\` folder too) into the snapshot's `removed-<time>\` folder, and checks every hash |
| `/api/performance?gc=S` | Garbage per frame over S real seconds ("Performance") |
| `/api/ui/popups`, events `popupShown` and `popupHidden`, condition `popup[:<text>]` | Popups ("Menus and UI") |
| `/api/log?noise=0`, `knownNoise` in step and wait replies | The game's own errors left out ("Logs") |

Its MCP tools exist only in sessions started after the rebuild. An older session can call them through a small MCP client: `node <script>` with `@modelcontextprotocol/sdk`'s `Client` and `StdioClientTransport` on `SpiderInspector\mcp-server\dist\index.js`, with `SPIDERINSPECTOR_OWNER` set to your session name.

## Restore

1. `/api/time/release`, `/api/act/lobby`, `/api/input/remove?pad=all`.
2. `close_game`, only when you hold the lock. It releases the lock and waits for the process to exit.
   - With a snapshot from `snapshot_install`, `restore_install` does steps 3 to 6 and reports `verified`. Read its `moved` list: a file the run added on purpose belongs in the report.
3. Copy the backed-up `SpiderSurge.dll` back into `<Mods>`, retrying the copy until the file is free.
4. Copy the config and the save files back. If `<Save>\SpiderSurge\` didn't exist before, look at what it holds, then remove it.
5. Run copies and deletions as separate commands: the permission check once blocked a restore that did both.
6. Compare every hash with the ones you noted. Leave the backup folders in place, and list the match logs your run added in the report.
