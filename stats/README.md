# Stats

The Stats part of [SpiderSurge](../README.md): statistics tracking that monitors player performance and game events. It used to be a separate mod, SpiderStats.

Stats always loads with SpiderSurge, because Surge depends on its tracking. To hide its windows and title cards, turn off **Show stats and titles** on its settings page.


## Features

- **Extensive Player Statistics**: Tracks a wide range of player stats including kills, deaths, friendly kills, shield interactions, web swings, airborne time, kill streaks, enemy-specific and weapon-specific kills.
- **Player Titles**: At the end of each Survival run, players are awarded fun titles based on their performance (e.g., "The Survivor", "Phantom Blade", "Lone Wolf"). Each title card shows a picture of the player's spider, hat included. Titles stay hidden during and after Versus matches, with or without teams.
- **Shooting accuracy**: Optional overall and per-weapon accuracy columns in F2, with successful/fired shot counts in saved stats, and accuracy titles such as "Bullseye", "Bullet Time" and "Stormtrooper". Each shot counts once, including pellet volleys and sustained beams. See [definitions, supported weapons and implementation research](../docs/weapon-accuracy.md).
- **In-Game Display**: 
  - Press **F1** to toggle the small stats display window. You can click and drag this window to reposition it
  - Press **F2** for the large stats overlay
  - Press **F3** to view player titles after a Survival run
  - Press **D-pad** on a controller to switch between the small window and Off during a match; in the lobby it also offers the big window and Survival titles. The D-pad leaves the windows alone while a menu is open or a menu button is selected
  - While the big window is open, **Page Up / Page Down**, **D-pad left / right**, its arrows or the mouse wheel move through pages and scoreboard presets
- **Log File**: All the stats are saved to a local txt file when the game ends 
- **Match history**: Browse saved Survival and Versus results in the Mods menu, with player statistics, accuracy, maps, perks and titles. Recording history is separate from text reports and telemetry
- **Configurable**: Every setting can be changed from the Mods menu or the Yaml file
- **Versus Mode Support**: Opponent kills count toward Kills, Total Offence, kill streaks and weapon stats. Breaking an opponent's shield counts toward Enemy Shields and Total Offence. With Teams on, only teammate kills and shield breaks count as friendly fire; without teams, every other spider is an opponent. Solo kills and streaks mean no living teammates, even while opponents are alive.


### Small window (F1)
Each spider's offence (kills plus enemy shield breaks) and deaths, next to its color. A dead spider's row fades until it respawns. Size, position and background are configurable; the two displayed stats are fixed. Turn on `stats.display.smallUIShowBackground` for a dark panel behind it.

<img width="680" alt="The small stats window next to the game's HUD" src="../docs/images/stats-small-window-closeup.png" />

<img width="1600" alt="The small stats window during a run" src="../docs/images/stats-small-window.jpg" />

### Big window (F2)
The run time, enemies killed and a table with a portrait for each spider. Green is good, pink is a hit you took, amber is friendly fire, and zeros are dimmed. In versus it shows the match time instead.

Choose the opening preset with **Settings** > **Stats** > **Starting scoreboard**. **My scoreboard** has editable columns in **Customize scoreboard**. **Combat**, **Enemy kills**, **Weapon kills**, **Accuracy** and **Movement** are fixed presets; hide any you do not want on the Customize scoreboard page. Each page uses the available width for its columns, and the window scales to fit the screen. Move through the pages and presets with **Page Up / Page Down**, **D-pad left / right**, the arrows at the bottom of the window or the mouse wheel over it.

<img width="1600" alt="The big stats window during a Survival run" src="../docs/images/stats-big-window.jpg" />

### Titles (F3)
Shown in the lobby when a run with two or more players ends. The cards are revealed one by one; press F3 to show them all at once.

<img width="1600" alt="End-of-run titles" src="../docs/images/stats-titles.jpg" />

### Title collection
Every title that shows on the end screen unlocks in your collection. Open it from **Mods** > **SpiderSurge** > **Collection** > **Titles** to see the titles you have unlocked, how many runs you earned each one in and when you first got it, and the ones still locked. Click a locked title (or press A on it) to see what it takes to earn it. Surge mode's titles are listed too. **Reset the title collection** at the bottom of the page locks every title again.

Use **Show** to cycle through All, Locked, Unlocked, Recently earned and New. New shows only unseen unlocks, and the Collection menu shows how many are waiting. Select an unlocked title or use **Mark all seen** to clear its marker. The end screen and saved match history still remember which titles were new that run.

The collection is saved on your PC in the game's own save folder, so updating or reinstalling the mod or the game keeps it: `%USERPROFILE%\AppData\LocalLow\Neverjam Studio\SpiderHeckApp\SpiderSurge\progress.dat` for Steam (`...\SPIDERHECK\SpiderSurge\progress.dat` for the Xbox app). That file also holds Surge's achievements and highscores; the collection is only title names, counts and dates. Each save writes a new file and swaps it in, keeping the previous one as `progress.dat.bak`, so a crash mid-save loses nothing. The file carries a checksum: if it is damaged or edited by hand, the mod loads the backup instead and keeps the bad file as `progress.dat.broken`. Older versions kept the collection in `titles.dat` in the same folder; it moves into `progress.dat` on the first launch, and `titles.dat` is removed once the move is saved.

### Match history

Open **Mods** > **SpiderSurge** > **Match history**, then choose Survival or Versus. Each record shows the outcome, duration and players, plus the wave and pain level for Survival. A Surge badge identifies Surge Survival; Surge abilities marks versus matches that used abilities. Open a match for player statistics, enemy and weapon kills, shooting accuracy, maps, perks and earned titles, including which titles appeared on the end screen and which were new that run.

Saving history is independent of text reports and telemetry. All recorded matches are kept on this PC, with 20 matches per list page, in the `SpiderSurge\History` folder beside `progress.dat`. History starts with this update; older text reports are available through **About SpiderSurge** > **Support** > **Open logs**. **Refresh history** rereads the files. **Clear match history** removes history after confirmation and keeps achievements, the title collection and highscores.

Use **Show** to filter either list to All, Surge or Without Surge. Results are newest first. Each player's result shows kills, deaths, best streak and time alive before you open their details. Time statistics use hours, minutes and seconds; Accuracy shows the overall hit rate and explains when no shots were recorded.

Surge results include a compact run recap above the title cards: build, wave reached, completed challenges, Chaos earned/spent, and new team achievements. The full scoreboard stays separate. With history enabled, the recap is also saved with that run.

## How to configure Stats

### From the Mods menu:

Open the pause menu and pick **Mods** > **SpiderSurge** > **Settings** > **Stats**. Changes apply right away. Display and reveal options holds secondary controls; Records and privacy holds file exports and match history. Drag the small window to place it. **Customize scoreboard** contains the preset switches and My scoreboard's column groups. **Reset Stats settings to defaults** keeps the small window's position.

### Using the Yaml file:

Stats' settings are the `stats` section of `...\Silk\Config\Mods\SpiderSurge.yaml`. See the [upcoming release's Settings](../NEXT_RELEASE.md#settings) for how the current file works.

<details>
<summary>Explanation of the fields</summary>

### UI:
`stats.display.showStatsWindow`: shows the mod UI. If false, stats and results windows stay hidden. Tracking continues.

`stats.display.showPlayers`, `stats.display.showPlayTime` and `stats.display.showEnemyDeaths`: Exactly what it sounds like. Will hide/show the corresponding parts of the Big UI.

`stats.display.autoScale`: Retained in old config files but ignored. Windows now always scale with the screen.

`stats.display.uiScale`: You can use this to manually scale the UI, from 0.5 to 3. 1 is normal size, 2 is double size, 0.5 is half size and so on. This adjusts the automatic screen scaling.

`stats.display.bigUIOpacity`: Controls the transparency of the Big UI overlay (F2). Values range from 0 (fully transparent) to 100 (fully opaque). Default is 100. 

`stats.display.smallUIShowBackground`: Shows a background behind the Small UI. Default is false.

`stats.display.position`: The position of the Small UI's top left corner as `xPercent` and `yPercent`, percentages of the screen's width and height from 0 to 100, with 0,0 being the top left corner of the screen. Being a share of the screen, it keeps the window in the same place when the resolution or window size changes. This value is saved every time you click and drag the UI, and a position saved in pixels by an older version is converted on the next launch. If the UI doesn't show up on your screen, set both values to 1. Default: `xPercent: 0.5`, `yPercent: 1`.

`stats.display.bigUI.columns`: Setting a column to true includes it in the My scoreboard preset. Columns are split across pages according to the available screen width.

`stats.display.startingPreset`: The scoreboard opened first: `default` (My scoreboard), `combat`, `enemies`, `weapons`, `accuracy`, or `movement`.

`stats.display.presets.combat`, `enemies`, `weapons`, `accuracy` and `movement`: Show or hide each fixed scoreboard preset. My scoreboard and the selected starting preset are always available. Each fixed preset uses its own columns, independent of the My scoreboard column settings. All are on by default.

### tracking:
`stats.tracking.saveStatsToFile`: If true, the stats will be saved to a local txt file.

`stats.tracking.saveHistory`: If true, results are saved in Match history. Default: `true`. It does not control text reports or telemetry.

Stats tracking always runs because Surge depends on it. Turn off **Show stats and titles** (`stats.display.showStatsWindow`) to hide all Stats windows and title cards without stopping tracking or title collection. Older disabled Stats settings become hidden Stats on upgrade; older disabled Teams settings keep team versus off.

### Titles:

`stats.titles.enabled`: If true, player titles will be calculated and displayed at the end of each Survival run. Press F3 to view them. Versus titles stay hidden regardless of this setting.

`stats.titles.revealDelaySeconds`: Controls the delay between revealing each title card in the titles screen animation, from 0 to 10. Default is 2.0 seconds. Set to 0 for instant reveal.

The title collection isn't a setting, so it isn't in this file; see [Title collection](#title-collection).

### Input:
The window keys and the controller switch are fixed, so they are not in the file. An older `stats.input.cycleInput` is ignored.

Page Up / Page Down and D-pad left / right move through the big window's pages and presets while it is open. During an active match, the controller window switch cycles only the small window and Off; in the lobby, it also offers the big window and Survival titles.

</details>


## Cross-Mod Integration API

Stats exposes a public API that other mods can call at runtime to inject custom stats and titles into the end-of-game screen and log files. The Surge part doesn't use it: Stats asks Surge for its titles when a run ends. Other mods are separate DLLs, so they use **reflection**: discover `StatsMod.StatsModApi` in the loaded `SpiderSurge` assembly and invoke its static methods.

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

#### `DescribeTitle(string titleName, string howToEarn)`

Lists a title in the title collection, so players see it and how to earn it while it is still locked. Call it once at startup, with the same name you pass to `RegisterCustomTitle`. A title that is earned without being described still unlocks; it shows in the collection with a generic description.

| Parameter | Description |
|---|---|
| `titleName` | The title's name, exactly as passed to `RegisterCustomTitle` |
| `howToEarn` | A short line on how to earn it (up to 200 characters) |

### Important Notes

- **Data is cleared** at the start of each game session — external mods should re-register their stats and titles after each game ends.
- **Titles require 2+ players** — they are meaningless in solo play and will not be displayed.
- **Unlocking** — a title unlocks in the collection when it shows on the end screen, so a title that is earned but loses its spot to higher-priority titles doesn't unlock that run.
- **Titles are Survival-only** — native and external titles are skipped for Versus matches, with or without teams.
- **Thread safety**: All API methods are synchronized internally. Safe to call from any thread.
- **Graceful degradation**: If SpiderSurge is not installed, calling mods should handle the case where the API type is not found. If the Stats part can't start (for example, because an old copy of the Stats mod is still in the Mods folder), registered stats and titles are never shown.

## Notes and disclaimers
- Stats reset when starting a new game. After the game ends, you can return to the lobby and the overlay will show the last game's stats.
- By default, the mod saves the stats in a txt file in this path: `.../Silk/Logs/SpiderStats/Spiderheck_stats_YYYY-MM-DD_HH-mm-ss.txt`
- Grenade and mine explosion kills belong to the player who armed the explosive, including arming it by hitting it with another weapon. Dropping an unarmed explosive does not give its last holder ownership
- Online play requires the same SpiderSurge version on every player and a P2P lobby, as described in the main documentation.
- Lobby combat does not change the last game's stats. Hit credit resets at the start of each session and versus round
- **Mods** > **SpiderSurge** > **About SpiderSurge** > **Support** opens logs, config and save folders, copies version and mod-part diagnostics, and opens the bug report form. These actions do not upload logs, player names or save data

## The Future
- I plan to continue expanding this mod with more fun titles and statistics
- I welcome any help or feedback! You can send me a message on Discord or Reddit, open a PR or try to send me a bug report using smoke signals. Some options are better than others
