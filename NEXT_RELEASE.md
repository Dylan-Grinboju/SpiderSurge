> **Not released yet.** This page describes what `main` holds now, which is not in a release yet. The [README](README.md) describes the latest release. When the next version is released, this file replaces `README.md`.

# SpiderSurge

**Abilities, live stats and team versus for SpiderHeck, all in one mod.**

[![Latest release](https://img.shields.io/github/v/release/Dylan-Grinboju/SpiderSurge?label=release&color=ff0061)](https://github.com/Dylan-Grinboju/SpiderSurge/releases)
[![Downloads](https://img.shields.io/github/downloads/Dylan-Grinboju/SpiderSurge/total?color=ff0061)](https://github.com/Dylan-Grinboju/SpiderSurge/releases)
[![Silk](https://img.shields.io/badge/Silk-0.7.0-ff0061)](https://github.com/SilkModding/Silk)

<img width="2559" height="1439" alt="Choosing a Surge ability" src="https://github.com/user-attachments/assets/74943e59-c808-42a0-84f2-6da99aca94d3" />

SpiderSurge is made of four parts. Surge is played from its own lobby platform; the other parts can be turned on or off from the game's Mods menu.

| Part | What it does | On after installing |
|---|---|---|
| [Stats](stats/README.md) | Tracks kills, deaths, streaks and more in Survival and Versus, with live stats windows and fun titles after Survival runs | Yes |
| [Surge](surge/README.md) | A separate Surge Survival platform above and left of Wave Survival: new abilities, ultimates, perks and custom enemies | Yes |
| [Teams](teams/README.md) | Team versus: pick teams in the lobby, friendly fire rules, team colors and spawns, and Surge abilities in versus | No |
| [Game fixes](#game-fixes) | Fixes bugs in the base game | Yes |

Stats and Teams used to be separate mods, SpiderStats and SpiderTeams. They are now only released as parts of SpiderSurge.

## A look around

### Stats

A small live window sits in a corner of the screen while you play: each spider's kills and deaths, next to its color. It fades a spider's row while it's dead. Drag it anywhere with the mouse.

<img width="680" alt="The small stats window next to the game's HUD" src="docs/images/stats-small-window-closeup.png" />

The big window is a full scoreboard: run time, enemies killed and a table with the columns you pick (totals, streaks, wave clutches, alive time, kills per enemy and per weapon, and more). Green is good, pink is a hit you took, amber is friendly fire.

<img width="1600" alt="The big stats window during a Survival run" src="docs/images/stats-big-window.jpg" />

When a run with two or more players ends, everyone gets fun titles based on how they played. The cards are revealed one by one in the lobby.

<img width="1600" alt="End-of-run titles" src="docs/images/stats-titles.jpg" />

### Surge

New abilities and ultimates for Survival, perks that combine with the game's own, and new enemies. The in-game guide explains all of it; press the backtick key to open it at any time.

<img width="1600" alt="The Surge guide" src="docs/images/surge-guide.jpg" />

### Teams

Split the lobby into teams and fight until one team is left standing. Spiders take their team's color, teammates spawn together, and the win screen shows each team's score.

<img width="1600" alt="A team versus match" src="docs/images/teams-versus.jpg" />

### Settings in the game

Open **Mods** > **SpiderSurge** from the pause menu to turn parts on or off and configure Stats and Surge. Settings use the game's normal menu, with larger text and separate category pages. Navigate with the stick or D-pad, press A/Cross to select, and use left/right to adjust sliders. B/Circle, Start, Escape and the Back button return one page at a time, restoring your selection and scroll position.

<img width="1280" alt="The native SpiderSurge settings menu with controller selection" src="docs/images/mods-menu-native.png" />

Team versus, team count and friendly fire are on the normal **Versus Battle** setup page, above the versus presets. Scroll down for the other team options. They are also available on the custom versus page.

## Controls

| What | Keyboard | Controller |
|---|---|---|
| Small stats window | **F1** | D-pad (cycles the windows) |
| Big stats window | **F2** | |
| Titles | **F3** (also skips the reveal) | |
| Surge ability | **Q** | **L1**, or any button you pick: hold **Start** and double-tap it |
| Surge ultimate | **C** | **L3 + R3** together, or any D-pad direction if you turn that on |
| Surge guide | **`** (backtick) | |
| Switch team in the lobby | **T** | **Y / Triangle** |

On a controller you can switch the stats windows with a stick press instead of the D-pad. While Surge's ultimate is on the D-pad, only the stick press switches them.

## Installation

1. Install Silk Mod Loader: https://github.com/SilkModding/Silk
2. Download the `SpiderSurge.dll` from the [releases page](https://github.com/Dylan-Grinboju/SpiderSurge/releases) or build it from the source.
3. Place the `SpiderSurge.dll` into your Silk `mods` folder.
4. Launch the game.
5. Play Surge from its platform above and left of Wave Survival in the lobby. To turn on Teams, open the pause menu, pick **Mods** > **SpiderSurge** > **Enable or disable parts**, check **Teams**, and restart the game.

## Updating from SpiderStats or SpiderTeams

1. Delete `StatsMod.dll` and `SpiderTeams.dll` from the `Silk/Mods` folder, and replace an older `SpiderSurge.dll` with the new one.
2. Launch the game.

On the first launch, SpiderSurge moves your settings from `SpiderSurge.yaml`, `Stats_Mod.yaml` and `SpiderTeams.yaml` into the new `SpiderSurge.yaml`, and renames the old files to `.yaml.old`. Every part you had installed stays on, except Stats if you had turned its tracking off.

If `StatsMod.dll` or `SpiderTeams.dll` is still in the Mods folder, its part stays off, because both copies would change the game at once, and its settings file stays where it is until you delete the DLL. The same goes for Surge and an older `SpiderSurge.dll` left next to the new one. With two copies of SpiderSurge 1.0.0, only the first one runs. A popup lists the files to delete.

## Settings

### From the Mods menu:

Open the pause menu and pick **Mods** > **SpiderSurge**. The main page has:

- **Enable or disable parts**: a switch for each optional part. Turning a part on or off takes effect after restarting the game. Surge is always available from its lobby platform.
- Settings pages for Stats and Surge. Teams settings are on the versus setup page. Changes apply right away and are saved to the file.
- **General**: check for updates and reload edits to the settings file without restarting the game.
- **About SpiderSurge**: version and credits.

Stats columns are grouped by totals, enemy kills, weapon kills, shooting accuracy and player stats. Surge's indicator and telemetry settings have their own pages. Every reset asks for confirmation first.

### Using the Yaml file:

All the settings are in one file: `...\Silk\Config\Mods\SpiderSurge.yaml`. It has a section for each part (`stats`, `surge` and `teams`), plus:

`parts.stats`, `parts.teams`, `parts.fixes`: Turns each optional part on or off. Takes effect after restarting the game. Surge is always available through its lobby platform; older `parts.surge` and `surge.surgeMode` values are ignored.

`general.checkForUpdates`: If true, the mod checks for a new version when the game starts. Default: `true`.

Each part's page explains its own section. Edit the file while the game is closed, or use **Reload the settings file** afterwards. If a value is invalid, the mod uses the default instead and writes a warning to the Silk log. If the file itself can't be read, for example after a wrong indent, **Reload the settings file** keeps your current settings and shows the error; at launch, the mod starts with the defaults and keeps a copy of your file as `SpiderSurge.yaml.broken`. You can reset everything by deleting the file; it will be recreated with default values on the next launch.

## Game fixes

- Reconnecting a controller keeps its original spider instead of adding a second player controlled by the same gamepad.
- Whisps no longer shoot the moment they spawn.
- Off-screen enemy arrows no longer throw an error every frame after their enemy dies in lava.
- Pain-level colour unlocks no longer throw an error on every lobby load, and every earned colour unlocks instead of only the first.
- Starting the game no longer throws the custom-maps warning error twice.

## Other Mods

- **[InfiniteFriends](https://github.com/Dylan-Grinboju/InfiniteFriends)**: Raises the player limit to 32. Teams works with it.
