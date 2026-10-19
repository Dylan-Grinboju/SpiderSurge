# SpiderSurge

**New abilities, ultimates, perks and enemies for SpiderHeck's Endless Survival.**

[![Latest release](https://img.shields.io/github/v/release/Dylan-Grinboju/SpiderSurge?label=release&color=ff0061)](https://github.com/Dylan-Grinboju/SpiderSurge/releases)
[![Downloads](https://img.shields.io/github/downloads/Dylan-Grinboju/SpiderSurge/total?color=ff0061)](https://github.com/Dylan-Grinboju/SpiderSurge/releases)
[![Silk](https://img.shields.io/badge/Silk-0.7.0-ff0061)](https://github.com/SilkModding/Silk)

<img width="2559" height="1439" alt="Choosing a Surge ability" src="https://github.com/user-attachments/assets/74943e59-c808-42a0-84f2-6da99aca94d3" />

This page describes **SpiderSurge 0.4.0**, the latest release. The next version, with live stats, team versus and game fixes built in, is still in development; [NEXT_RELEASE.md](NEXT_RELEASE.md) describes it.

## Surge Mode

Surge Mode is on after installing. The survival platform in the lobby then reads **Surge Survival**, and every Endless Survival run is a Surge run. Tiers of Heck and Parkour are unchanged. To play normal survival, turn Surge Mode off in the Mods menu (see [Settings](#settings)); the platform then reads **Wave Survival** again.

In a Surge run:

- Waves have 75% more enemies, and they move 10% faster.
- Every enemy type shows up much earlier, and the early ones stop coming sooner. On pain level 2 and higher, every enemy can show up from wave 1.
- Six new enemies join the waves.
- The first perk choice is an ability. From wave 30 you can take its ultimate, and from wave 60 you can switch to another ability's ultimate.
- Ability and ultimate cooldowns reset at the start of every wave. Null Field's cooldown drops by 12 seconds instead.
- The perk choice has no time limit.
- High scores are kept apart from your normal survival scores, and Surge runs aren't sent to the online leaderboards.

<details>
<summary>Which waves each enemy appears in</summary>

| Enemy | Normal survival | Surge |
|---|---|---|
| Wasp | 1-22 | 1-7 |
| Roller | 2-26 | 1-9 |
| Whisp | 4-30 | 2-10 |
| Melee Whisp | 6-34 | 3-10 |
| Khepri | 8-36 | 4-24 |
| Exploding Roller | 10+ | 5+ |
| Power Whisp | 12+ | 6+ |
| Power Wasp | 14+ | 7-16 |
| Power Roller | 16+ | 9+ |
| Power Khepri | 18+ | 10+ |
| Power Melee Whisp | 20+ | 11-20 |
| Shielded Wasp | 22+ | 13-42 |
| Hornet Shaman | 24+ | 14+ |
| Shielded Power Wasp | 26+ | 16+ |
| Hornet | 28+ | 18-25 |
| Shielded Hornet | 32+ | 20+ |

</details>

## Abilities and ultimates

Abilities are perks: the one you pick is shared by every player, and each spider has its own cooldown. An ability and its ultimate share that cooldown.

| Ability | Ultimate |
|---|---|
| **Null Field**: immune to all damage for 3 s. Cooldown 12 s. While it's active a spider can't die stuck in a wall either. A shield you had when it started breaks at the end if you were hit during it | **Neural Backup**: after a 10 s cast, a random dead player comes back at a spawn point. An Astral Heckhound ghost counts as dead and comes back where it floats. If nobody is dead, 3 friendly wasps spawn instead. Cooldown 75 s |
| **Bottomless Clip**: for 5 s, shooting doesn't use ammo. When it ends, or you drop or throw the weapon, the weapon disintegrates; grenades, mines, the BoomStick, the Laser Cube and the Death Cube are exempt. Cooldown 20 s | **Care Package**: random weapons appear at half of the map's weapon spawns. Cooldown 30 s |
| **Kinetic Pulse**: a pulse that pushes away enemies, other players, items and projectiles around you. Enemies are pushed twice as hard. Cooldown 12 s | **Thermal Detonation**: a bigger pulse whose center kills everything it reaches: enemies, projectiles and other players. Only you are spared. Cooldown 20 s |
| **Pocket Dimension**: stores the weapon you hold. The one stored before comes back 2.5 s later and equips itself. Cooldown 12 s | **Pocket Dimension^2**: a second storage slot on the ultimate button. Weapons come back after 4 s. Cooldown 15 s |

### Upgrade perks

- **Ability Cooldown:** level 1 shortens the ability's cooldown and level 2 the ultimate's.
- **Ability Duration:** level 1 lengthens the ability and level 2 the ultimate. With Kinetic Pulse it is **Bigger Pulse** (a larger radius); with Pocket Dimension it is **Faster Retrieval** (weapons come back 1 s sooner).
- **Short Term Investment:** the ability gets better and the ultimate worse. **Long Term Investment** does the opposite. Both need an ultimate.
- **Lucky:** each card for a two-level perk you don't have yet has a 15% chance (30% at Lucky level 2) of being a gold **Lucky Upgrade** that jumps straight to level 2. This works on the game's own perks too.

At wave 60, picking another ability's ultimate swaps your ability and ultimate for that pair. Those cards say **Swap Ability!**

### Synergies

Some of the game's perks improve an ability. Their cards are tagged **Synergized**.

| Ability | Perk | What it adds |
|---|---|---|
| Null Field | Start Shields, Positive Encouragement, Safety Net | If you have a shield when Neural Backup starts, the revived player has a 50% chance of a shield, or the wasps a 50% chance of a 4th wasp |
| Bottomless Clip | Efficiency | Level 1: the weapon keeps its ammo instead of disintegrating. Level 2: it keeps at least half its ammo |
| Kinetic Pulse | Bigger Boom | A stronger pulse; level 2 also strengthens Thermal Detonation |
| Kinetic Pulse | Too Cool | A larger lethal center for Thermal Detonation |
| Pocket Dimension | More Guns, More Boom or More Particles | Level 1: stored weapons of that kind stay through a map change. Level 2: they also stay through death |

## Achievements

SpiderSurge has its own achievements for specific feats with the Surge abilities. See them, with the date each one was unlocked and who did it, in **Mods** > **SpiderSurge** > **Achievements**. They are kept in the game's save file, and a popup announces each new one. "Teammate" means any other player in Survival, and a player of your team in [Team Versus](teams/README.md).

| Achievement | How to unlock it |
|---|---|
| Black Hole | Banish a Hornet Shaman |
| Total Destruction | Kill 3 teammates with one Thermal Detonation |
| I Will Keep You Safe | Banish a teammate |
| Unlimited Power | Use Bottomless Clip while holding a Particle Blade that appeared with Size Matters and Thick Blades both at level 2 |
| Doomsday Scenario | Send an armed Biggest Grenade into the Pocket Dimension |
| Begone | Knock an enemy into lava with Kinetic Pulse, so Stats credits you the kill. Needs Stats on |
| Unstoppable Force Against Immovable Object | Survive a Biggest Grenade blast inside Null Field |
| Healer | Revive the same player twice in one wave (or Team Versus round) with Neural Backup |
| Yay I Guess? | Pick the Nothing perk when Luck made it a Lucky Upgrade |
| You Are Not Done Yet | Revive an Astral Heckhound ghost with Neural Backup |
| Kiss of Death | Hit a Power Twin Blade Whisp with a Particle Blade below 15% durability |

## Enemies

These appear only in Surge runs.

<img width="2559" height="1439" alt="A Surge run with a Twin Blade Whisp, a Twin Whisp and a Missile Whisp" src="https://github.com/user-attachments/assets/a92e59fd-22f3-4ab9-922c-22e4b57ad999" />

| Enemy | What it does | Waves |
|---|---|---|
| Twin Blade Whisp | A Melee Whisp with a blade on each end | 8-16 |
| Twin Blade Power Melee Whisp | A Power Melee Whisp with the same second blade | 15+ |
| Twin Whisp | Yellow. Fires two rail shots side by side | 12-20 |
| Shielded Twin Whisp | A Twin Whisp with a shield | 17+ |
| Missile Whisp | Dark red. Fires rockets that explode on impact | 19-25 |
| Shielded Missile Whisp | A Missile Whisp with a shield | 21+ |

## Controls

| What | Keyboard | Controller |
|---|---|---|
| Ability | **Q** | **L1**, or a button you pick (below) |
| Ultimate | **C** | **L3 + R3** together, or any D-pad direction if `UseDpadForUltimate` is on |
| Guide | **`** (backtick) | |

- Once a spider has an ability, its ability button no longer does what it did in the game, so by default **L1 stops jumping**. Jump with A, or move the ability to another button.
- **Change the ability button:** during a Surge run, hold **Start** or **Select** and double-tap L1, L2, R1, R2, A, B, X, Y, L3 or R3. Hold Start or Select for 3 seconds to go back to L1. Each player picks their own, and the choice lasts until the game closes.
- **The guide** opens by itself the first time you launch the game. It explains the abilities, the controls, the ability indicator and the synergies. Press the backtick key to close it or open it again; it has no close button.
- **The ability indicator** is a dot above each spider: green when the ability is ready, blue while it's active and red while it recharges.

## Installation

1. Install the Silk mod loader: https://github.com/SilkModding/Silk
2. Download `SpiderSurge.dll` from the [releases page](https://github.com/Dylan-Grinboju/SpiderSurge/releases).
3. Put it in the `Silk\Mods` folder in the game folder.
4. Launch the game.

Building `main` from source gives the unreleased next version, not 0.4.0.

## Settings

### In the game

Open the pause menu and pick **Mods** > **SpiderSurge**. The page has:

- **Enable Surge Mode** / **Disable Surge Mode**: takes effect from the next run. It is locked during a run.
- **Enable Telemetry** / **Disable Telemetry**, your anonymous ID and **Reset Telemetry ID** (see [Telemetry](#telemetry)).
- Credits.

### The settings file

Everything else is in `...\Silk\Config\Mods\SpiderSurge.yaml`, which the mod creates on the first launch. Edit it while the game is closed; the mod reads it only at startup.

```yaml
EnableSurgeMode: true
UseDpadForUltimate: false
UnlimitedPerkChoosingTime: true
EnableStatsLogging: true
TelemetryEnabled: false
indicator:
  radius: 1.5
  offset:
    x: 3
    y: 8
  availableColor: '#00FF00'
  cooldownColor: '#FF0000'
  activeColor: '#87CEEB'
  showOnlyWhenReady: false
display:
  showTutorial: true
  showTelemetryConsentPrompt: true
```

| Setting | What it does | Default |
|---|---|---|
| `EnableSurgeMode` | Makes Endless Survival runs Surge runs | `true` |
| `UseDpadForUltimate` | Any D-pad direction fires the ultimate, and L3 + R3 no longer does. The D-pad still moves the spider | `false` |
| `UnlimitedPerkChoosingTime` | Removes the perk choice's countdown in Surge runs; the timer shows ∞ | `true` |
| `EnableStatsLogging` | Writes a report of each Surge run to `...\Silk\Logs\SpiderSurge\`. SpiderStats data, the Surge titles and telemetry all need it | `true` |
| `TelemetryEnabled` | Sends anonymous run data at the end of each Surge run (see [Telemetry](#telemetry)) | `false` |
| `indicator.radius` | The size of the ability dot, from 0.5 to 10 | `1.5` |
| `indicator.offset.x`, `indicator.offset.y` | Where the dot sits relative to the spider | `3`, `8` |
| `indicator.availableColor`, `cooldownColor`, `activeColor` | The dot's colors: `#RRGGBB`, `#RRGGBBAA`, a color name, or `r,g,b` from 0 to 1 | green, red, light blue |
| `indicator.showOnlyWhenReady` | Shows the dot only while the ability is ready | `false` |
| `display.showTutorial` | Opens the guide at the next launch. The mod sets it back to `false` after showing it | `true` |
| `display.showTelemetryConsentPrompt` | Asks about telemetry at the next launch. The mod sets it to `false` once you answer | `true` |

If a value is invalid, the mod uses its default and writes a warning to the Silk log. Write decimals with a dot (`1.5`) on every Windows language. To reset everything, delete the file; it is recreated with the defaults on the next launch.

Updating from 0.3.3 or older: on the first launch, the `indicator` and `display` settings move into their sections and keep your values. Before 0.4.0 those settings had no effect.

## Telemetry

Telemetry is off until you agree to it. On the first launch, a popup asks whether to send anonymized match data; you can change your answer in the Mods menu at any time.

When it's on, the end of each Surge run sends the date and time, the mod version, the run's length, the number of players, waves survived, pain level, the perks taken and each player's ability and ultimate counts. It uses an anonymous random ID, stored in `...\Silk\Logs\SpiderSurge\telemetry_anonymous_id.txt`, and never player names. Reports that fail to send are kept and sent at the next launch. Telemetry also needs `EnableStatsLogging`.

## SpiderStats

With [SpiderStats](https://github.com/Dylan-Grinboju/spiderheck_stats_mod/releases) 2.3.0 or newer installed (and `EnableStatsLogging` on), each Surge run adds every player's ability and ultimate counts to the SpiderStats log, and the end-of-run titles gain Surge titles. Like all titles, they need 2 or more players and a clear leader, with no tie.

| Title | Goes to the player with |
|---|---|
| Enhanced | The most abilities |
| Ultimate Form | The most ultimates |
| Powered Up | The most ultimates and the most abilities |
| Maximum Power | The most ultimates and the fewest abilities |
| Simple is Better | The most abilities and the fewest ultimates |
| Vanilla | The fewest ultimates and the fewest abilities |
| Ability Destroyer | The most abilities and the most offense (kills plus enemy shields broken) |
| Wasn't Fast Enough | The most Null Fields and the most damage taken |
| Down with the Ship | The most Kinetic Pulses and the most lava deaths |
| Boom Boom | The most Thermal Detonations and the most explosion kills |
| Self Sacrifice | The most Neural Backups and the most damage taken |

## Good to know

- **High scores:** Surge runs keep their own high scores. Your normal survival scores are untouched, and they show again when Surge Mode is off or the mod is removed. Back up your saves anyway.
- **Pause menu:** in every mode, **Restart** and returning to the lobby or main menu ask "Are you sure?" first.
- **Sounds** follow the game's SFX volume.
- **Updates:** 15 seconds after the game starts, the mod checks for a newer version and offers to open its download page. 0.4.0 has no setting to turn this off.

## Other mods

- **[SpiderStats](https://github.com/Dylan-Grinboju/spiderheck_stats_mod)**: tracks kills, deaths and more, with live stats windows and end-of-run titles.
- **[InfiniteFriends](https://github.com/Dylan-Grinboju/InfiniteFriends)**: raises the player limit to 32.
