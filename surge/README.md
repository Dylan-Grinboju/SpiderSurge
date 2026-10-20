# Surge

The Surge part of [SpiderSurge](../README.md): a gameplay overhaul for Survival that introduces "Surge Mode", powerful new abilities, and fearsome custom enemies.

To play, stand on the **Surge Survival** platform above and to the left of **Wave Survival** in the lobby. Everyone joins the platform for the usual countdown, then chooses a pain level. The original Wave Survival platform still starts normal Survival. No settings toggle is needed.


<img width="2559" height="1439" alt="image" src="https://github.com/user-attachments/assets/74943e59-c808-42a0-84f2-6da99aca94d3" />

## Features

- **Surge Mode**: A high-intensity survival mode overhaul that increases enemy spawn rates and introduces unique wave-based progression.
- **New Abilities**:
    - **Kinetic Pulse**: Release a kinetic pulse that destroys projectiles and repels enemies.
    - **Bottomless Clip**: Grants a brief period of unlimited ammunition for equipped weapons.
    - **Pocket Dimension**: Store and recall weapons at will, enabling advanced inventory tactics.
    - **Null Field**: Become immune to all damage for a short time. Its Ultimate, **Neural Backup**, brings back a random dead player after a 10 second cast, or summons 3 friendly Wasps when nobody is dead. An Astral Heckhound ghost counts as dead and comes back where it floats, even when it is the caster's own ghost.
    - **Freeze Time**: Freeze every enemy, shot and effect inside the ice ring for 5 seconds. Frozen enemies don't move, shoot or explode, but their blades, saws and poison clouds still hurt on contact. Frozen shots, from enemies or players, hang in the air and still hit whatever touches them, then fly on when they thaw. Spiders and the weapons they hold aren't frozen, so a Shotgun fired into the ring still hits. Casting it on enemies another player already froze doesn't add time, it only keeps the longer freeze. **Deep Freeze** (Ability Duration) freezes for longer and farther away. Its Ultimate, **Rewind**, turns every nearby enemy back into a plain Wasp; shielded enemies become shielded Wasps.
- **Custom Enemies**:
    - **Twin Whisp**: A Whisp with two cores that fires two shots at once.
    - **Missile Whisp**: Uses the RocketTurret's body while flying and launching rockets. Cyan when unshielded and purple when shielded.
    - **Twin Blade Whisp**: Elite melee enemies equipped with dual-ended particle blades.
    - **Wasp Cocoon**: A stationary cocoon that starts hatching its first Wasp a second after it appears, then one every 10 seconds until it's destroyed, with at most 2 of its Wasps alive at once. Its Wasps don't count toward the wave's limit on live enemies, but the wave still ends only once they're dead. A shielded cocoon hatches Shielded Wasps while its shield holds.
    - **Laser enemies**: Red-shielded versions of most enemies (not the Exploding Roller or already shielded ones). Every 5 seconds a faint ring and the Death Ray's charge sound warn for 1 second, then a Laser Cannon ring around the enemy burns for 1 second. Breaking the red shield removes the ring for good.
- **Perk Synergy System**: Gain unique bonuses when combining specific mod abilities with vanilla perks (e.g., Bottomless Clip + Efficiency).
- **Ultimate Upgrades**: Your ability's Ultimate is guaranteed as a choice at wave 30 and can appear in regular perk choices afterwards until unlocked. At wave 60, you can unlock it or switch to another ability with its Ultimate.
- **In-Game Guide**: Explains the new mechanics, controls and perk synergies. It is a page of the Mods menu, so it works with a controller and the spiders stay still while you read: open it from **Mods** > **SpiderSurge** > **Surge** > **Surge guide**, or with its key at any time, backtick (**`**) by default. It also opens on the first launch.
- **Achievements**: 12 achievements for specific feats with the abilities, listed in **Mods** > **SpiderSurge** > **Achievements** > **Surge achievements**. See [Achievements](#achievements).
- **Luck Perk**: A new perk that provides a chance to skip perk levels and jump straight to higher-tier upgrades.
- **Custom Controls**:
    - **Ability**: **Q** or **L1**. **Ultimate**: **C**, or **L3 + R3** together. Change them in **Mods** > **SpiderSurge** > **Controls**, where the controller ultimate can also be another pair of buttons, one button or the whole D-pad.
    - **Per-Player Remapping**: While holding **Start/Menu**, **double-tap** any button to set it as your ability key. It works in the lobby, in a run or Team Versus match, and while your spider is dead, and lasts until you leave the game.
    - **Reset**: Hold **Start/Menu** for 3 seconds to go back to the controller ability button of the **Controls** page (L1 by default).
- **Pause menu confirmations**: **Restart** and **Main Menu** in the pause menu ask "Are you sure?" before acting.
- **Team Versus**: With the [Teams](../teams/README.md) part on, everyone can get the same Surge ability in versus.

<img width="2559" height="1439" alt="image" src="https://github.com/user-attachments/assets/a92e59fd-22f3-4ab9-922c-22e4b57ad999" />

<img width="1600" alt="The Surge guide" src="../docs/images/surge-guide.jpg" />

## Achievements

SpiderSurge has its own achievements for specific feats with the Surge abilities. See them, with the date each one was unlocked and who did it, in **Mods** > **SpiderSurge** > **Achievements** > **Surge achievements**. A locked achievement shows only its name there; click it (or press A on it) to see how to get it. They are saved on your PC in `SpiderSurge\progress.dat` in the game's own save folder (`%USERPROFILE%\AppData\LocalLow\Neverjam Studio\SpiderHeckApp` for Steam), next to the Stats title collection and the Surge highscores, and a popup announces each new one. Older versions kept them in the game's save file; they move on the first launch. "Teammate" means any other player in Survival, and a player of your team in [Team Versus](../teams/README.md).

| Achievement | How to unlock it |
|---|---|
| Black Hole | Banish a Hornet Shaman |
| Total Destruction | Kill 3 teammates with one Thermal Detonation |
| I Will Keep You Safe | Banish a teammate |
| Unlimited Power | Use Bottomless Clip while holding a Particle Blade that appeared with Size Matters and Thick Blades both at level 2 |
| Doomsday Scenario | Send an armed Biggest Grenade into the Pocket Dimension |
| Begone | Knock an enemy into lava with Kinetic Pulse, so Stats credits you the kill. Needs Stats on |
| Unstoppable Force Against Immovable Object | Survive a Biggest Grenade blast inside Null Field |
| Healer | Revive the same teammate twice in one wave (or Team Versus round) with Neural Backup |
| Yay I Guess? | Pick the Nothing perk when Luck made it a Lucky Upgrade |
| You Are Not Done Yet | Revive an Astral Heckhound ghost with Neural Backup, your own included |
| Kiss of Death | Hit a Power Twin Blade Whisp with a Particle Blade below 15% durability |
| Not On My Watch | Block a Laser Cannon or Laser Cube beam headed for a teammate while Null Field makes you immune. A teammate who is immune at that moment doesn't count |

## How to configure Surge

### From the Mods menu:

Open the pause menu and pick **Mods** > **SpiderSurge** > **Surge**. Changes apply right away, except **Play without abilities**, which applies from the next run. The ability, ultimate and guide keys are on the **Controls** page. **Reset Surge settings to defaults** at the bottom leaves the telemetry settings alone; they are on the **General** page, since plain Survival runs use them too.

### Using the Yaml file:

Surge's settings are the `surge` section of `...\Silk\Config\Mods\SpiderSurge.yaml`. See [Settings](../README.md#settings) for how the file works.

<details>
<summary>Explanation of the fields</summary>

### Gameplay:
The lobby platform selects the mode for each run. Older `surge.surgeMode` and `parts.surge` settings are ignored.

The ability, ultimate and Surge guide keys are in the `controls` section, which **Mods** > **SpiderSurge** > **Controls** sets. An older `surge.dpadUltimate: true` becomes `controls.ultimate.controller: <Gamepad>/dpad` on the first launch.

`surge.unlimitedPerkTime`: Disables the timer on the perk selection screen. Default: `true`.

`surge.noAbilities`: If true, Surge Survival runs have no abilities, ultimates or ability perks (Ability Cooldown, Ability Duration, Short and Long Term Investment). The first perk choice is a normal one, waves 30 and 60 bring no ultimate choice, and the ability keys keep their normal game actions. Luck still appears. Applies from the next run, and doesn't change Team Versus abilities. Default: `false`.

### Sound:
`surge.abilityVolume`: The volume of every Surge sound (abilities, ultimates and the perk choice sounds), from 0 to 100, on top of the game's SFX volume. 80 is the original volume and 100 is a quarter louder. Default: `50`.

### UI:
`surge.showTutorial`: Opens the Surge guide in the lobby on the next launch, then turns itself off. Resetting the Surge settings leaves it as it is. Default: `true`.

`surge.indicator.radius`: The size of the ability indicator, from 0.5 to 10. Default: `1.5`.

`surge.indicator.offset.x`, `surge.indicator.offset.y`: Where the indicator sits, relative to the spider. Defaults: `3` and `8`.

`surge.indicator.availableColor`, `surge.indicator.cooldownColor`, `surge.indicator.activeColor`: The indicator's color when the ability is ready, on cooldown, and active, as hex codes. The Mods menu cycles through 8 colors; in the file you can use any hex code. Defaults: `#00FF00`, `#FF0000` and `#87CEEB`.

`surge.indicator.showOnlyWhenReady`: If true, the indicator only appears when the ability is off cooldown. Default: `false`.

### Logging:
`surge.statsLogging`: **Record each run** on the General page. Builds a report of every Survival run (waves, perks, deaths, ability and ultimate use). Surge runs save it to a local file and feed it to the Stats titles. Telemetry sends this report, so nothing is uploaded while it's off. Default: `true`.

`surge.telemetry`: **Share anonymous run reports** on the General page. Uploads the report of each finished Survival run, with or without Surge mode. See [Telemetry](#telemetry). Default: `false`.

`surge.showTelemetryPrompt`: Asks whether to share run stats a few seconds after the game starts. Answering the question turns it off. Default: `true`.

</details>

## Telemetry

SpiderSurge supports optional telemetry to help track balancing and gameplay trends in Surge matches.

### How to enable

Telemetry is off until you agree. The first time the game starts with Surge on, a popup asks whether to share anonymous run stats. You can change your answer at any time with **Share anonymous run reports** in **Mods** > **SpiderSurge** > **General**, or `surge.telemetry` in the file. Uploads also need **Record each run** (`surge.statsLogging`) on.

### What is sent

One report per finished Survival run, with or without Surge mode, sent by the host. A plain Survival run sends the same report with no ability or ultimate use, and every perk choice counted as a plain perk:

- **The run:** whether it was a Surge run, mod version, length, player count, waves survived, pain level, how it ended (lives ran out, restart or quit), lives left and the most lives, the final perks with their levels, whether the perk choice timer is unlimited, and whether the run had abilities.
- **Each perk choice:** the wave, the type of choice (ability, ultimate, ultimate swap or perk), the cards offered and which of them were lucky upgrades, the card picked and its new level, whether the timer ran out, and how long the choice took. A perk screen still open when the run ends is sent with no card picked.
- **Each wave attempt:** the wave number, the map, whether it was won, lost or left, how long it took in game time, lives at its start, how many spiders died in it, how many abilities and ultimates were used in it, and how many times Null Field blocked a hit that would have killed a spider without a shield.
- **Each player, by slot number:** ability and ultimate presses, and how many of their deaths Null Field blocked. When the [Stats](../stats/README.md) part is on, also deaths (and lava deaths), kills, kills while their ability or ultimate was on, kills by enemy type, kills and shields broken by each weapon (Kinetic Pulse counts as a weapon), friendly kills, shots fired and hit, and time alive.

- **Titles**, when the Stats part is on and the run had 2 or more players: every title earned at the end of the run, with the slot number of the player who earned it and whether it was one of the cards on the end screen, whether titles were shown at all, how many titles the run newly unlocked in the [title collection](../stats/README.md#title-collection), how many are unlocked on this PC in total, and how many titles exist. The collection itself, with its dates, is never sent.

A very long run's report leaves out its oldest perk choices and waves, so it stays within the 32 KB the relay accepts.

### Privacy and identity

- Telemetry uses an anonymous random ID, not player names.
- The anonymous ID is generated locally and stored in:
    `...\Silk\Logs\SpiderSurge\telemetry_anonymous_id.txt`
- The **General** page shows the ID, and **Reset telemetry ID** replaces it with a new one.


### Local log files (non-telemetry)

When `surge.statsLogging` is on, per-match text reports are written to:

`...\Silk\Logs\SpiderSurge\SpiderSurge_Match_YYYY-MM-DD_HH-mm-ss.txt`


## Stats Integration

When the [Stats](../stats/README.md) part is also on, Surge adds its ability and ultimate data to the stats log and registers custom player titles.

### Custom Stats

At the end of each Surge Survival match, the following per-player stats are appended to the Stats log file:
- **Ability Activations** — how many times each player activated their ability
- **Ultimate Activations** — how many times each player activated their ultimate

### Custom Titles

Surge registers the following titles through the Stats title system. Titles require **2+ players** and a **clear leader** (no ties):

| Title | Earned by |
|---|---|
| Enhanced | Most ability activations |
| Ultimate Form | Most ultimate activations |
| Powered Up | Most ultimate and most ability activations |
| Maximum Power | Most ultimate and fewest ability activations |
| Simple is Better | Most ability and fewest ultimate activations |
| Vanilla | Fewest ultimate and fewest ability activations |
| Ability Destroyer | Most ability activations and most offence |
| Wasn't Fast Enough | Most Null Field activations and most damage taken |
| Down with the Ship | Most Kinetic Pulse activations and most lava deaths |
| Boom Boom | Most Thermal Detonation activations and most explosion kills |
| Self Sacrifice | Most Neural Backup activations and most damage taken |

The last five combine Surge data with Stats requirements, creating **cross-part titles** that reflect both parts' tracked stats.

## Notes and disclaimers

- This mod overrides the highscores displayed in the survival menu. This is intentional, as it allows you to see your highscores for the modded game. The global position in the leaderboards will NOT be tracked, as this requires changes to the backend. Surge Survival keeps its own highscores in `SpiderSurge\progress.dat`, and Wave Survival still shows your normal ones from the game's save.
- Surge Mode is designed for Survival Mode. Its abilities only reach versus through the [Teams](../teams/README.md) part.

## The Future

- More abilities and ults
- More enemies
- Even harder mode?
