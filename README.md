# SpiderSurge

A mod for Spiderheck that brings together four parts in one file. Each part can be turned on or off from the game's Mods menu.

| Part | What it does | On after installing |
|---|---|---|
| [Stats](stats/README.md) | Tracks kills, deaths, streaks and more in Survival, with live stats windows and fun titles at the end of each run | Yes |
| [Surge](surge/README.md) | Surge Mode for Survival: new abilities, ultimates, perks and custom enemies | No |
| [Teams](teams/README.md) | Team versus: pick teams in the lobby, friendly fire rules, team colors and spawns, and Surge abilities in versus | No |
| [Game fixes](#game-fixes) | Fixes two bugs in the base game | Yes |

Stats and Teams used to be separate mods, SpiderStats and SpiderTeams. They are now only released as parts of SpiderSurge.


<img width="2559" height="1439" alt="image" src="https://github.com/user-attachments/assets/74943e59-c808-42a0-84f2-6da99aca94d3" />

## Installation

1. Install Silk Mod Loader: https://github.com/SilkModding/Silk
2. Download the `SpiderSurge.dll` from the [releases page](https://github.com/Dylan-Grinboju/SpiderSurge/releases) or build it from the source.
3. Place the `SpiderSurge.dll` into your Silk `mods` folder.
4. Launch the game.
5. To turn on Surge or Teams, open the pause menu, pick **Mods** > **SpiderSurge**, check the part, and restart the game.

## Updating from SpiderStats or SpiderTeams

1. Delete `StatsMod.dll` and `SpiderTeams.dll` from the `Silk/Mods` folder, and replace an older `SpiderSurge.dll` with the new one.
2. Launch the game.

On the first launch, SpiderSurge moves your settings from `SpiderSurge.yaml`, `Stats_Mod.yaml` and `SpiderTeams.yaml` into the new `SpiderSurge.yaml`, and renames the old files to `.yaml.old`. Every part you had installed stays on, except Stats if you had turned its tracking off.

If `StatsMod.dll` or `SpiderTeams.dll` is still in the Mods folder, its part stays off, because both copies would change the game at once, and its settings file stays where it is until you delete the DLL. The same goes for Surge and an older `SpiderSurge.dll` left next to the new one. With two copies of SpiderSurge 1.0.0, only the first one runs. A popup lists the files to delete.

## Settings

### From the Mods menu:

Open the pause menu and pick **Mods** > **SpiderSurge**. The main page has:

- A switch for each part. Turning a part on or off takes effect after restarting the game.
- A settings page for each running part. Changes there apply right away and are saved to the file.
- **Check for updates**: when the game starts, checks whether a new version is out.
- **Reload the settings file**: picks up edits to the file without restarting the game.

### Using the Yaml file:

All the settings are in one file: `...\Silk\Config\Mods\SpiderSurge.yaml`. It has a section for each part (`stats`, `surge` and `teams`), plus:

`parts.stats`, `parts.surge`, `parts.teams`, `parts.fixes`: Turns each part on or off. Takes effect after restarting the game.

`general.checkForUpdates`: If true, the mod checks for a new version when the game starts. Default: `true`.

Each part's page explains its own section. Edit the file while the game is closed, or use **Reload the settings file** afterwards. If a value is invalid, the mod uses the default instead and writes a warning to the Silk log. If the file itself can't be read, for example after a wrong indent, **Reload the settings file** keeps your current settings and shows the error; at launch, the mod starts with the defaults and keeps a copy of your file as `SpiderSurge.yaml.broken`. Surge Mode can't change during a Survival run, so a reload or reset keeps it until the run ends. You can reset everything by deleting the file; it will be recreated with default values on the next launch.

## Game fixes

- Whisps no longer shoot the moment they spawn.
- Off-screen enemy arrows no longer throw an error every frame after their enemy dies in lava.

## Other Mods

- **[InfiniteFriends](https://github.com/Dylan-Grinboju/InfiniteFriends)**: Raises the player limit to 32. Teams works with it.
