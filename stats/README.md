# SpiderStats

A statistics tracking mod for Spiderheck that monitors player performance and game events.


## Features

- **Extensive Player Statistics**: Tracks a wide range of player stats including kills, deaths, friendly kills, shield interactions, web swings, airborne time, kill streaks, enemy-specific and weapon-specific kills.
- **Player Titles**: At the end of each game, players are awarded fun titles based on their performance (e.g., "The Survivor", "Phantom Blade", "Lone Wolf")
- **In-Game Display**: 
  - Press **F1** to toggle the small stats display window. You can click and drag this window to reposition it
  - Press **F2** for the large stats overlay
  - Press **F3** to view player titles (only after the game ends)
  - Press **D-pad** on a controller to cycle the UIs
- **Log File**: All the stats are saved to a local txt file when the game ends 
- **Configurable**: Using the Yaml file, various aspects of the mod can be changed
- **Updater**: The mod will automatically check for updates when the game starts
- **Versus Mode Support**: The mod will track stats in versus mode, but as this was developed with the survival mode in mind, some features (such as the titles) may not work as intended


### Titles
<img width="2485" height="1288" alt="Pasted image 20260109102238" src="https://github.com/user-attachments/assets/ea9b7aa7-9b15-408d-88d9-dd35c2679eff" />

### Small version:
<img width="2559" height="1439" alt="image" src="https://github.com/user-attachments/assets/2fd4a739-d333-4173-9e68-1ee017032b86" />

### Big version:
<img width="2547" height="1422" alt="image" src="https://github.com/user-attachments/assets/42ea27ef-9450-4cae-ae46-3f1befbe0d6a" />

## Installation

1. Install Silk Mod Loader: https://github.com/SilkModding/Silk
2. Download the mod dll file from [here](https://github.com/Dylan-Grinboju/spiderheck_stats_mod/releases) or from the sidebar in the Github page, or build the project yourself from this repo
3. Place the mod file in your Silk mods folder
4. Launch the game

## How to configure the Mod using the Yaml file:
The first time after launching the game with the mod installed, the following file will be created at this path: `...\Silk\Config\Mods\Stats_Mod.yaml`

After every change to the file, you need to relaunch the game for it to take effect. If you enter an incorrect value, for example 5 to a boolean field, the mod will throw a bunch of errors at you. You can change it back or just delete the file and it will be created again with the next game launch. 

<details>
<summary>Explanation of the fields</summary>

### UI:
`showStatsWindow`: shows the mod UI. If false then pressing F1, F2, or F3 will do nothing. Note that the tracking will still work, it will just not show you anything.

`showPlayers`, `showPlayTime` and `showEnemyDeaths`: Exactly what this sounds. Will hide/show the corresponding parts of the Big UI.

`autoScale`: If true, the mod will automatically scale the UI based on your screen resolution.

`uiScale`: You can use this to manually scale the UI. 1 is normal size, 2 is double size, 0.5 is half size and so on. You can use this with conjunction with autoScale or by itself.

`bigUIOpacity`: Controls the transparency of the Big UI overlay (F2). Values range from 0 (fully transparent) to 100 (fully opaque). Default is 100. 

`position`: You can use this to manually set the position of the Small UI. The coordinates are in pixels, with 0,0 being the top left corner of the screen. This value will be saved every time you click and drag the UI. If the UI doesn't show up on your screen, you can try setting the x and y values to 10 again.

`columns`: Setting a column to true will display it in the Big UI. I don't limit you but the elements can be cut off if there are too many. 

### tracking:
`enabled`: If false, the mod will not track any stats. This is as if the mod is not installed at all.

`saveStatsToFile`: If true, the stats will be saved to a local txt file.

### Updater:

`checkForUpdates`: If true, the mod will automatically check for updates when the game starts.

### Titles:

`enabled`: If true, player titles will be calculated and displayed at the end of each game. Press F3 to view them.

`revealDelaySeconds`: Controls the delay between revealing each title card in the titles screen animation. Default is 2.0 seconds. Set to 0 for instant reveal.

### Input:
`cycleInput`: Controls which input on a controller cycles the UI. Options are `dpad`, `joystick`, `both`, or `none`. The joystick input refers to pressing down either joystick. The default and recommended is `dpad`.

</details>


## Cross-Mod Integration API

SpiderStats exposes a public API that other mods can call at runtime to inject custom stats and titles into the end-of-game screen and log files. Since mods are separate DLLs, communication happens via **reflection** — the calling mod discovers `StatsMod.StatsModApi` from the loaded assemblies and invokes its static methods.

#### `RegisterCustomStats(List<string> lines)`

Appends raw text lines to the end-of-game stats log file under an "EXTERNAL MOD STATISTICS" section.

| Parameter | Description |
|---|---|
| `lines` | Lines of text to append to the stats file. Null or empty lists are ignored. |

#### `RegisterCustomTitle(string titleName, string description, string[] requirementNames, PlayerInput leader, bool leaderHasStat, int bonusPriority)`

Registers a custom title that participates in the native title pipeline (domination removal, priority balancing, display selection).

| Parameter | Description |
|---|---|
| `titleName` | Display name for the title |
| `description` | Subtitle text. Can be expanded with native stats if any native requirement names are included |
| `requirementNames` | Array of stat keys. Can be native names (e.g. `"MostOffense"`, `"MostDamageTaken"`) or entirely custom names |
| `leader` | The `PlayerInput` who earned this title. **If all requirements are custom, this must be non-null** — otherwise the title will be rejected since there's no native stat to resolve the player from |
| `leaderHasStat` | Set to `true` if the external mod already validated the stat. If `false` and `leader` is non-null, the title is rejected |
| `bonusPriority` | Additional priority added on top of the base (`requirementCount × 10`). Higher priority = more likely to be displayed |

### Important Notes

- **Data is cleared** at the start of each game session — external mods should re-register their stats and titles after each game ends.
- **Titles require 2+ players** — they are meaningless in solo play and will not be displayed.
- **Thread safety**: All API methods are synchronized internally. Safe to call from any thread.
- **Graceful degradation**: If SpiderStats is not installed, calling mods should handle the case where the API type is not found.

## Notes and disclaimers
- Stats reset when starting a new game. After the game ending you can return to the lobby and the overlay will show the last game's stats
- By default, the mod saves the stats in a txt file in this path: `.../Silk/Logs/Spiderheck_stats_<Current_Date>.txt`
- There are some kills that won't register such as activating a weapon with another (hitting a grenade with a particle blade). This just complicates the logic and is up to interpretation of who gets the kill in difficult scenarios. Also, this is just very complicated to track, and I wasn't feeling like coding it
- This probably won't work with multiplayer, you are welcome to try and send me feedback, but I don't plan to support it anytime soon

## The Future
- I plan to continue expanding this mod with more fun titles and statistics
- I welcome any help or feedback! You can send me a message on Discord or Reddit, open a PR or try to send me a bug report using smoke signals. Some options are better than others