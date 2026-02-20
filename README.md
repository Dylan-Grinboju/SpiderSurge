# SpiderSurge

A comprehensive gameplay overhaul mod for Spiderheck that introduces "Surge Mode", powerful new abilities, and fearsome custom enemies.


<img width="2559" height="1439" alt="Pasted image 20260219121801" src="https://github.com/user-attachments/assets/a7c04ff6-16f2-49c2-a91a-5453d4e8b546" />

## Features

- **Surge Mode**: A high-intensity survival mode overhaul that increases enemy spawn rates and introduces unique wave-based progression.
- **New Abilities**:
    - **Kinetic Pulse**: Release a kinetic pulse that destroys projectiles and repels enemies.
    - **Bottomless Clip**: Grants a brief period of unlimited ammunition for equipped weapons.
    - **Pocket Dimension**: Store and recall weapons at will, enabling advanced inventory tactics.
    - **Null Field**: Deploys a temporary protective energy barrier.
- **Custom Enemies**:
    - **Twin Whisp**: Fires dual projectiles simultaneously.
    - **Missile Whisp**: Launches rockets that deal devastating damage.
    - **Twin Blade Whisp**: Elite melee enemies equipped with dual-ended particle blades.
- **Perk Synergy System**: Gain unique bonuses when combining specific mod abilities with vanilla perks (e.g., Bottomless Clip + Efficiency).
- **Ultimate Upgrades**: Special perk selection phases at waves 30 and 60 for powerful "Ultimate" variants and ability swaps.
- **In-Game Tutorial**: A dedicated UI to guide players through new mechanics and ability controls.
- **Luck Perk**: A new perk that provides a chance to skip perk levels and jump straight to higher-tier upgrades.
- **Custom Controls**:
    - **Per-Player Remapping**: While holding **Start/Menu**, **double-tap** any button to set it as your ability key.
    - **Reset**: Hold **Start/Menu** for 3 seconds to reset to default (L1).
- **Automation**: Includes an integrated mod updater to notify you when a new version is available.

<img width="2559" height="1439" alt="image" src="https://github.com/user-attachments/assets/a92e59fd-22f3-4ab9-922c-22e4b57ad999" />

## Installation

1. Install Silk Mod Loader: https://github.com/SilkModding/Silk
2. Download the `SpiderSurge.dll` from the [releases page](https://github.com/Dylan-Grinboju/SpiderSurge/releases) or build it from the source.
3. Place the `SpiderSurge.dll` into your Silk `mods` folder.
4. Launch the game.

## How to configure the Mod using the Yaml file:

The first time you launch the game with the mod installed, a configuration file will be created at: `...\Silk\Config\Mods\SpiderSurge_Mod.yaml`

Changes to this file require a game restart to take effect. If invalid values are entered, the mod may default to its original settings or report errors in the log.

<details>
<summary>Explanation of the fields</summary>

### Gameplay:
`EnableSurgeMode`: Toggles the Surge Mode overhaul features.

`UseDpadForUltimate`: If true, pressing the D-pad on a controller will trigger ultimate abilities.

`UnlimitedPerkChoosingTime`: Disables the timer on the perk selection screen.

### UI:
`display.showTutorial`: Toggles the visibility of the instruction overlay.

`indicator.radius`: Adjusts the size of the ability indicator UI.

`indicator.showOnlyWhenReady`: If true, the ability indicator will only appear when the ability is off cooldown.

`indicator.availableColor`, `indicator.cooldownColor`, `indicator.activeColor`: Customizes the colors for different ability states using hex codes.

### Logging:
`EnableStatsLogging`: Enables or disables the recording of mod-specific events to the Silk logs.

`TelemetryEnabled`: Enables or disables anonymous telemetry uploads for completed SpiderSurge Survival matches.

</details>

## Telemetry

SpiderSurge supports optional telemetry to help track balancing and gameplay trends in Surge matches.

### How to enable

In `...\Silk\Config\Mods\SpiderSurge_Mod.yaml`:

- Set `EnableStatsLogging: true` to keep local match stat files.
- Set `TelemetryEnabled: true` to allow telemetry upload.

**Telemetry is disabled by default.**

### What is sent

A compact JSON event is sent at the end of each tracked SpiderSurge Survival match with:

- Event metadata: `eventType`, `schemaVersion` (now `2`), `timestampUtc`, `modVersion`
- Match summary: `matchDurationSeconds`, `playerCount`, `wavesSurvived`, `painLevel`
- Ability usage: per-player `abilityActivationCount`, `ultimateActivationCount`
- Perks snapshot: active `globalPerks`
- Truncation flags: `globalPerksTruncated`, `playerStatsTruncated` when payload is shortened to fit Discord limits
- Anonymous identifier: `anonId`

### Privacy and identity

- Telemetry uses an anonymous random ID, not player names.
- The anonymous ID is generated locally and stored in:
    `...\Silk\Logs\SpiderSurge\telemetry_anonymous_id.txt`

### Delivery behavior

- Telemetry is sent through a Discord webhook.
- In multiplayer, upload is only attempted by host/server to avoid duplicate reports.
- Failed uploads are queued locally in:
    `...\Silk\Logs\SpiderSurge\TelemetryPending`
- Queued payloads are retried automatically later.

### Local log files (non-telemetry)

When `EnableStatsLogging` is enabled, per-match text reports are written to:

`...\Silk\Logs\SpiderSurge\SpiderSurge_Match_YYYY-MM-DD_HH-mm-ss.txt`


## Notes and disclaimers

- This mod overrides the highscores displayed in the survival menu. This is intentional, as it allows you to see your highscores for the modded game. The global position in the leaderboards will NOT be tracked, as this requires changes to the backend. Turning off the mod will restore your highscores. BACK UP YOUR SAVE FILES JUST IN CASE.
- This mod is designed for Survival Mode. While it may load in other modes, it won't do anything in them.
- You can reset your configuration at any time by deleting the YAML file; it will be recreated with default values on the next launch.

## The Future

- More abilities and ults
- More enemies
- Even harder mode?

## Other Mods

If you enjoy SpiderSurge, check out my other mod:

- **SpiderStats**: A statistics tracking mod for Spiderheck that monitors player performance, game events, and awards fun titles at the end of each game. You can find it here: https://github.com/Dylan-Grinboju/spiderheck_stats_mod
