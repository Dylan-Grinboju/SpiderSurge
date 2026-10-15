# Stats

The Stats part of [SpiderSurge](../README.md): statistics tracking that monitors player performance and game events. It used to be a separate mod, SpiderStats.

Stats is on after installing SpiderSurge. You can turn it off from the pause menu, under **Mods** > **SpiderSurge**.


## Features

- **Extensive Player Statistics**: Tracks a wide range of player stats including kills, deaths, friendly kills, shield interactions, web swings, airborne time, kill streaks, enemy-specific and weapon-specific kills.
- **Player Titles**: At the end of each game, players are awarded fun titles based on their performance (e.g., "The Survivor", "Phantom Blade", "Lone Wolf")
- **In-Game Display**: 
  - Press **F1** to toggle the small stats display window. You can click and drag this window to reposition it
  - Press **F2** for the large stats overlay
  - Press **F3** to view player titles (only after the game ends)
  - Press **D-pad** on a controller to cycle the UIs. You can switch to a stick press instead. While Surge's ultimate is on the D-pad, only the stick press works
- **Log File**: All the stats are saved to a local txt file when the game ends 
- **Configurable**: Every setting can be changed from the Mods menu or the Yaml file
- **Versus Mode Support**: The mod will track stats in versus mode, but as this was developed with the survival mode in mind, some features (such as the titles) may not work as intended


### Titles
<img width="2485" height="1288" alt="Pasted image 20260109102238" src="https://github.com/user-attachments/assets/ea9b7aa7-9b15-408d-88d9-dd35c2679eff" />

### Small version:
<img width="2559" height="1439" alt="image" src="https://github.com/user-attachments/assets/2fd4a739-d333-4173-9e68-1ee017032b86" />

### Big version:
<img width="2547" height="1422" alt="image" src="https://github.com/user-attachments/assets/42ea27ef-9450-4cae-ae46-3f1befbe0d6a" />

## How to configure Stats

### From the Mods menu:

Open the pause menu and pick **Mods** > **SpiderSurge** > **Stats settings**. Changes apply right away. The page has every setting below, the big window's columns on their own page, and **Reset Stats settings to defaults**, which keeps the small window's position.

### Using the Yaml file:

Stats' settings are the `stats` section of `...\Silk\Config\Mods\SpiderSurge.yaml`. See [Settings](../README.md#settings) for how the file works.

<details>
<summary>Explanation of the fields</summary>

### UI:
`stats.display.showStatsWindow`: shows the mod UI. If false then pressing F1, F2, or F3 will do nothing. Note that the tracking will still work, it will just not show you anything.

`stats.display.showPlayers`, `stats.display.showPlayTime` and `stats.display.showEnemyDeaths`: Exactly what this sounds. Will hide/show the corresponding parts of the Big UI.

`stats.display.autoScale`: If true, the mod will automatically scale the UI based on your screen resolution.

`stats.display.uiScale`: You can use this to manually scale the UI, from 0.5 to 3. 1 is normal size, 2 is double size, 0.5 is half size and so on. You can use this with conjunction with autoScale or by itself.

`stats.display.bigUIOpacity`: Controls the transparency of the Big UI overlay (F2). Values range from 0 (fully transparent) to 100 (fully opaque). Default is 100. 

`stats.display.smallUIShowBackground`: Shows a background behind the Small UI. Default is false.

`stats.display.position`: You can use this to manually set the position of the Small UI. The coordinates are in pixels, with 0,0 being the top left corner of the screen. This value will be saved every time you click and drag the UI. If the UI doesn't show up on your screen, you can try setting the x and y values to 10 again.

`stats.display.bigUI.columns`: Setting a column to true will display it in the Big UI. I don't limit you but the elements can be cut off if there are too many. 

### tracking:
`stats.tracking.saveStatsToFile`: If true, the stats will be saved to a local txt file.

To stop tracking altogether, turn off the Stats part (`parts.stats`).

### Titles:

`stats.titles.enabled`: If true, player titles will be calculated and displayed at the end of each game. Press F3 to view them.

`stats.titles.revealDelaySeconds`: Controls the delay between revealing each title card in the titles screen animation, from 0 to 10. Default is 2.0 seconds. Set to 0 for instant reveal.

### Input:
`stats.input.cycleInput`: Controls which input on a controller cycles the UI. Options are `dpad`, `joystick`, `both`, or `none`. The joystick input refers to pressing down either joystick. The default and recommended is `dpad`. While Surge's ultimate is on the D-pad (`surge.dpadUltimate`), `dpad` works like `none` and `both` like `joystick`.

</details>


## Cross-Mod Integration API

Stats exposes a public API that other mods can call at runtime to inject custom stats and titles into the end-of-game screen and log files. The Surge part calls it directly. Other mods are separate DLLs, so they use **reflection**: discover `StatsMod.StatsModApi` in the loaded `SpiderSurge` assembly and invoke its static methods.

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
- **Graceful degradation**: If SpiderSurge is not installed, calling mods should handle the case where the API type is not found. If the Stats part is off, registered stats and titles are never shown.

## Notes and disclaimers
- Stats reset when starting a new game. After the game ending you can return to the lobby and the overlay will show the last game's stats
- By default, the mod saves the stats in a txt file in this path: `.../Silk/Logs/SpiderStats/Spiderheck_stats_<Current_Date>.txt`
- There are some kills that won't register such as activating a weapon with another (hitting a grenade with a particle blade). This just complicates the logic and is up to interpretation of who gets the kill in difficult scenarios. Also, this is just very complicated to track, and I wasn't feeling like coding it
- This probably won't work with multiplayer, you are welcome to try and send me feedback, but I don't plan to support it anytime soon
- Stats doesn't know about teams yet. In team versus, it counts every kill on another spider as a friendly kill

## The Future
- I plan to continue expanding this mod with more fun titles and statistics
- I welcome any help or feedback! You can send me a message on Discord or Reddit, open a PR or try to send me a bug report using smoke signals. Some options are better than others
