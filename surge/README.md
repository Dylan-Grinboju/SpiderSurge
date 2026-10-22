# Surge

The Surge part of [SpiderSurge](../README.md): a gameplay overhaul for Survival that introduces "Surge Mode", powerful new abilities, and fearsome custom enemies.

To play, stand on the **Surge Survival** platform above and to the left of **Wave Survival** in the lobby. Everyone joins the platform for the usual countdown, then chooses a pain level. The original Wave Survival platform still starts normal Survival. No settings toggle is needed.


<img width="2559" height="1439" alt="image" src="https://github.com/user-attachments/assets/74943e59-c808-42a0-84f2-6da99aca94d3" />

## Features

- **Surge Mode**: A high-intensity survival mode overhaul that increases enemy spawn rates and introduces unique wave-based progression.
- **New Abilities**:
    - **Kinetic Pulse**: Release a kinetic pulse that destroys projectiles and repels enemies.
    - **Bottomless Clip**: Grants a brief period of unlimited ammunition for equipped weapons.
    - **Pocket Dimension**: Store and recall weapons at will, enabling advanced inventory tactics. Its Ultimate, **Black Hole**, opens a field 150 units around the caster that moves with them for 3 seconds. The enemies inside it when it opens stop where they are and can't move, shoot or explode while it lasts, though their stingers, blades and struts still hurt other spiders on contact and a Hornet Shaman's poison cloud keeps chasing. The field pulls them toward the caster, Wasp Cocoons included, and an enemy with any part inside its small inner circle (20 units) is swallowed for good (no death, no drops). The inner circle is a buffer: an enemy is swallowed before any part of it can touch the caster. From open air, an enemy that starts within about three quarters of the range makes it in. One farther out, or held up by a wall, stops where the field leaves it and goes free. Enemies that come in later aren't pulled. The field also pulls every spider inside it the way a Gravity Grenade does, cutting its web, more weakly toward the edge. A spider whose body reaches the inner circle is swallowed too, teammates included, unless it is immune. A spider close in can't outrun the pull, one farther out can, and the caster can walk away to save them. **Ability Duration** level 2 and **Long Term Investment** each add 50 to the range, **Short Term Investment** takes 50 away, and the cooldown perks change its 20 second cooldown as for every ultimate.
    - **Null Field**: Become immune to all damage for a short time. Its Ultimate, **Neural Backup**, brings back a random dead player after a 10 second cast, or summons 3 friendly Wasps when nobody is dead. An Astral Heckhound ghost counts as dead and comes back where it floats, even when it is the caster's own ghost.
    - **Freeze Time**: Freeze every enemy, shot and effect inside the ice ring for 5 seconds. Frozen enemies don't move, shoot or explode, but their blades, saws and poison clouds still hurt on contact. A Hornet Shaman's poison cloud freezes with its shaman, and on its own once the ring reaches any of it: the whole cloud stops where it is. Frozen shots, from enemies or players, hang in the air and still hit whatever touches them, then fly on when they thaw. A frozen Gravity Saw stops pulling, and one that hasn't opened yet opens that much later. Spiders and the weapons they hold aren't frozen, so a Shotgun fired into the ring still hits. Casting it on enemies another player already froze doesn't add time, it only keeps the longer freeze. **Deep Freeze** (Ability Duration) freezes for longer and farther away. Its Ultimate, **Rewind**, turns every nearby enemy back into a plain Wasp; shielded enemies become shielded Wasps.
- **Custom Enemies**:
    - **Twin Whisp**: A Whisp with two cores that fires two shots at once.
    - **Missile Whisp**: Uses the RocketTurret's body while flying and launching rockets. Blue, with yellow lights like the game's Whisps.
    - **Twin Blade Whisp**: Elite melee enemies equipped with dual-ended particle blades.
    - **Wasp Cocoon**: A stationary cocoon that starts hatching its first Wasp a second after it appears. Each hatch takes as long as an enemy's spawn warning (2.5 seconds, longer with Interference), and the next one starts 10 seconds after a Wasp comes out, until the cocoon is destroyed, with at most 2 of its Wasps alive at once. Its Wasps don't count toward the wave's limit on live enemies, but the wave still ends only once they're dead. A shielded cocoon hatches Shielded Wasps while its shield holds.
    - **Shielded versions**: The shielded Twin Whisp, Missile Whisp and Wasp Cocoon wear the game's pink shield bubble, which breaks on the first hit like every shielded enemy's.
    - **Laser enemies**: Red-shielded versions of most enemies (not the Exploding Roller or already shielded ones). Every 5 seconds a faint ring and the Death Ray's charge sound warn for 1 second, then a Laser Cannon ring around the enemy burns for 1 second. Breaking the red shield removes the ring for good.
- **Surge Meter and Shop**: The team shares a meter, shown under the lives counter, that fills from what abilities do during a wave: enemies launched by Kinetic Pulse, kills while Bottomless Clip or Null Field runs, hits Null Field blocked, Pocket Dimension giving back a stored weapon, enemies frozen by Freeze Time and frozen enemies that die, and more from the ultimates (enemies hit by Thermal Detonation, kills with Care Package weapons, Neural Backup revives and its Wasps' kills, Black Hole and Rewind). A chaos point takes more charge in a bigger team, as much more as its waves have more enemies. A full meter is one chaos point, up to one ability-earned point per wave for the team; unfinished charge carries between waves. Chaos points are spent in a shop beside every perk choice after the starting ability pick, reachable with the controller by pressing right from the last card. Each perk screen puts two of the five items on sale: **1** reroll the cards, **2** an extra life (never above the max), **3** a random perk the team doesn't have yet, **5** +1 max lives without a life, and the **Blood Pact**, which costs no chaos points but a life and gives one chaos point. See [Surge meter](#surge-meter).
- **Perk Synergy System**: Gain unique bonuses when combining specific mod abilities with vanilla perks (e.g., Bottomless Clip + Efficiency).
- **Ultimate Upgrades**: Your ability's Ultimate is guaranteed as a choice at wave 30 and can appear in regular perk choices afterwards until unlocked. At wave 60, you can unlock it or switch to another ability with its Ultimate.
- **In-Game Guide**: Explains the new mechanics, controls and perk synergies. Open **Mods** > **SpiderSurge** > **How to play** for a first-run walkthrough and illustrated topics about abilities, enemies, controls, progression and multiplayer. Navigate with a controller or mouse.
- **Achievements**: 13 achievements for specific feats with the abilities, listed in **Mods** > **SpiderSurge** > **Achievements** > **Surge achievements**. See [Achievements](#achievements).
- **Luck Perk**: A new perk that provides a chance to skip perk levels and jump straight to higher-tier upgrades.
- **Custom Controls**:
    - **Ability**: **Q** or **L1**. **Ultimate**: **C**, or **L3 + R3** together. Change them in **Mods** > **SpiderSurge** > **Controls**, where the controller ultimate can also be another pair of buttons, one button or the whole D-pad.
    - **Per-Player Remapping**: While holding **Select**, **double-tap** any button to set it as your ability key. It works in the lobby, in a run or Team Versus match, and while your spider is dead, and lasts until you leave the game.
    - **Reset**: Hold **Select** for 3 seconds to go back to the controller ability button of the **Controls** page (L1 by default).
- **Pause menu confirmations**: **Restart** and **Main Menu** in the pause menu ask "Are you sure?" before acting.
- **Team Versus**: With the [Teams](../teams/README.md) part on, everyone can get the same Surge ability in versus.

<img width="2559" height="1439" alt="image" src="https://github.com/user-attachments/assets/a92e59fd-22f3-4ab9-922c-22e4b57ad999" />

<img width="1280" alt="The first-run walkthrough in How to play" src="../docs/images/guide-first-run.png" />

## Achievements

SpiderSurge has its own achievements for specific feats with the Surge abilities. See them, with the date each one was unlocked and who did it, in **Mods** > **SpiderSurge** > **Achievements** > **Surge achievements**. A locked achievement shows only its name there; click it (or press A on it) to see how to get it. They are saved on your PC in `SpiderSurge\progress.dat` in the game's own save folder (`%USERPROFILE%\AppData\LocalLow\Neverjam Studio\SpiderHeckApp` for Steam), next to the Stats title collection and the Surge highscores, and a popup announces each new one. Older versions kept them in the game's save file; they move on the first launch. "Teammate" means any other player in Survival, and a player of your team in [Team Versus](../teams/README.md).

| Achievement | How to unlock it |
|---|---|
| Event Horizon | Feed a Hornet Shaman to your Black Hole |
| Total Destruction | Kill 3 teammates with one Thermal Detonation |
| I Will Keep You Safe | Feed a teammate to your Black Hole |
| Unlimited Power | Use Bottomless Clip while holding a Particle Blade that appeared with Size Matters and Thick Blades both at level 2 |
| Doomsday Scenario | Send an armed Biggest Grenade into the Pocket Dimension |
| Begone | Knock an enemy into lava with Kinetic Pulse, so Stats credits you the kill. Needs Stats on |
| Unstoppable Force Against Immovable Object | Survive a Biggest Grenade blast inside Null Field |
| Healer | Revive the same teammate twice in one wave (or Team Versus round) with Neural Backup |
| Yay I Guess? | Pick the Nothing perk when Luck made it a Lucky Upgrade |
| You Are Not Done Yet | Revive an Astral Heckhound ghost with Neural Backup, your own included |
| Kiss of Death | Hit a Power Twin Blade Whisp with a Particle Blade below 15% durability |
| Not On My Watch | Block a Laser Cannon or Laser Cube beam headed for a teammate while Null Field makes you immune. A teammate who is immune at that moment doesn't count |
| Max Capacity | Buy a Bigger Heart (+1 max lives) in the Surge shop |

## Surge meter

The team can earn **one chaos point per wave from ability charge**. Once earned, charging stops until a later wave, including when retrying the same wave. Excess charge is discarded; unfinished charge carries between waves. **Blood Pact remains separate** and can still grant its point after the ability cap is reached.

Focusing a synergized perk shows a short explanation beneath the cards, using the offered level (including lucky upgrades). **Freeze Time + Adrenaline, Blademaster or Cool Explosions** removes an extra **0.2 seconds** from Freeze Time / Rewind's shared cooldown for every real second of their slow motion. Overlapping effects do not multiply the refund, and pauses, menus and ordinary wave-end slow motion do not count.

The meter is the team's, and only the host counts it. It fills while a wave runs, from these:

| Ability | Charges the meter when | Charge |
|---|---|---|
| Kinetic Pulse | an enemy is launched | 1 each |
| Thermal Detonation | an enemy is in the death zone or launched | 2 each |
| Bottomless Clip | the caster kills an enemy while it runs | 2 each |
| Care Package | a weapon it dropped kills an enemy, whoever holds it | 1 each |
| Null Field | a hit is blocked (lava counts as one hit per cast, and at most twice a second and 3 hits per cast) | 5 each |
| Null Field | the caster kills an enemy while it runs | 2 each |
| Neural Backup | a player is revived | 3 |
| Neural Backup | a Wasp it summoned kills an enemy | 1 each |
| Pocket Dimension | it gives back a stored weapon | 1 |
| Black Hole | an enemy is swallowed | 3 each |
| Freeze Time | an enemy is frozen, and again when a frozen enemy dies | 1 each, then 2 more |
| Rewind | an enemy is turned into a Wasp | 1 each |

The kills come from the [Stats](../stats/README.md) part's kill credit, which knows who made each kill and with which weapon, so Bottomless Clip, Null Field and Care Package only charge for kills while Stats is on. Turning Stats off in the parts page warns about this; to only hide the stats, keep it on and turn off `stats.display.showStatsWindow` and `stats.titles.enabled`. A kill counts toward an ability that was running when the killing hit landed: an enemy shot into lava during Bottomless Clip counts even when it dies after the ability ends. Freeze Time's kill charge doesn't need Stats: it counts every death of an enemy it holds, and goes to the last player who froze it. Neither do Neural Backup's Wasps, whose stings count for the player who summoned them.

A full meter is one chaos point. For one player, a chaos point takes `charge per player per wave × waves per chaos point`: 3 charge with the defaults (1.5 and 2). A bigger team pays that times how many more enemies each wave sends at it than at one player, from the game's enemy budget (`startingBudget + wave × budgetPerWave + players × budgetPerPlayer`), so a point costs the same per enemy whatever the team size. With four players that is 7 charge at wave 1, 5 at wave 6 and 4 at wave 20; with two, 4 at first and 3 from wave 9. A player casting Kinetic Pulse, Freeze Time or Bottomless Clip on cooldown made 2 to 4 charge a wave in testing, so using the ability half as often still buys a point every 2 waves. It changes at every wave and as soon as a player joins or leaves. Both numbers can be changed in **Mods** > **SpiderSurge** > **Surge** (`surge.meter.chargePerWave`, `surge.meter.wavesPerPoint`); the host's values apply online and a change applies at the next wave.

Every perk screen in a Surge run has a column beside the cards with the team's chaos points, the meter and the charge toward the next point. On the starting ability pick, which has no shop, the column explains the meter and chaos points. A line under the cards says how the focused ability or ultimate card charges the meter, or explains the focused perk's synergy for its offered level (the locked card's, while the shop or **Confirm** has the focus). The cards keep their normal size next to the column and shrink only when they wouldn't fit.

The shop fills that column on every perk choice after the starting ability pick, including the ultimate choices at waves 30 and 60. Each screen puts two of the five items on sale, drawn at random when the screen opens (items that can be bought right now are drawn first); a reroll changes the cards, not the items on sale. Press right from the last card to enter it, up and down to move inside it, A on an item to select it and A on the **Buy** button under the items to pay for it (a reroll happens the moment it is bought), left to go back to the cards (from **Buy**, to **Confirm** when the perk timer is off). Any player can buy, and the mouse works too. A reroll keeps the kind of choice (the ultimate screen offers ultimates again) and the perk timer, if it is on, keeps counting. The extra life is refused when lives are full, the Blood Pact needs a spare life (it never takes the last one, since losing a wave with it ends the run), and the random perk is never an ability, an ultimate or a pain-level handicap. The match log and telemetry record the charge per player and wave, what was bought and rerolled offers, and the Stats title **Power Broker** goes to the player with the most charge.

## How to configure Surge

### From the Mods menu:

Open the pause menu and pick **Mods** > **SpiderSurge** > **Surge**. Hover over a setting or select it with a controller to read its explanation in the side panel. Use the right stick or mouse wheel to scroll that explanation. Indicator controls are on the Surge page. Changes apply right away, except **Play without abilities**, which applies from the next run. The ability and ultimate keys are on the **Controls** page. **Reset Surge settings to defaults** at the bottom leaves the telemetry settings alone; they are on the **General** page, since plain Survival runs use them too.

### Using the Yaml file:

Surge's settings are the `surge` section of `...\Silk\Config\Mods\SpiderSurge.yaml`. See the [upcoming release's Settings](../NEXT_RELEASE.md#settings) for how the current file works.

<details>
<summary>Explanation of the fields</summary>

### Gameplay:
The lobby platform selects the mode for each run. Older `surge.surgeMode` and `parts.surge` settings are ignored.

The ability and ultimate keys are in the `controls` section, which **Mods** > **SpiderSurge** > **Controls** sets. An older `surge.dpadUltimate: true` becomes `controls.ultimate.controller: <Gamepad>/dpad` on the first launch.

`surge.unlimitedPerkTime`: Disables the timer on the perk selection screen. With it on, clicking a card only locks it, and a **Confirm** button under the cards (press down from a card) makes the pick, so a stray press can't choose a perk. With the timer on, a click picks at once as in the normal game. The host's setting applies online. Default: `true`.

`surge.noAbilities`: If true, Surge Survival runs have no abilities, ultimates or ability perks (Ability Cooldown, Ability Duration, Short and Long Term Investment). The first perk choice is a normal one, waves 30 and 60 bring no ultimate choice, and the ability keys keep their normal game actions. Luck still appears, the Surge meter and shop don't. Applies from the next run, and doesn't change Team Versus abilities. Default: `false`.

`surge.meter.chargePerWave`: The meter charge one player is expected to make per wave, used to size a chaos point; a bigger team's point grows with the extra enemies its waves bring. From 0.5 to 20. Default: `1.5`.

`surge.meter.wavesPerPoint`: How many waves of that charge one chaos point takes. From 0.5 to 10. Default: `2`. See [Surge meter](#surge-meter) for the formula.

With abilities enabled, a dedicated choice screen offers as many random shared abilities as a normal perk choice has cards (3, or 2 from pain level 3, plus Picky) before wave 1. The arena is paused until an ability is selected; this does not consume an earned perk choice or change the map. The first earned perk choice offers normal upgrades.

`surge.indicator.compactHud`: Replaces the world rings with a compact panel at the bottom right, with a named row for each player. Active effects have a draining bar and remaining seconds; cooldowns have a filling bar and remaining seconds. Both use game time. Pocket Dimension also shows its stored weapon. Change this immediately under Surge > Use compact ability HUD. The existing colors and Only show when ready setting apply to both displays; size and position settings apply to the world rings. Default: `false`.

The world ring drains in the active color over a timed ability or ultimate's duration, then fills clockwise in the cooldown color until ready. Active effects without a duration keep a full ring. Both timers stop while game time is paused.

### Sound:
`surge.abilityVolume`: The volume of every Surge sound (abilities, ultimates and the perk choice sounds), from 0 to 100, on top of the game's SFX volume. 80 is the original volume and 100 is a quarter louder. Default: `50`.

### UI:
The guide is available from **How to play** in settings. The old `surge.showTutorial` and `controls.surgeGuide.keyboard` values are no longer used.

`surge.indicator.radius`: The size of the ability indicator, from 0.5 to 10. Default: `1.5`.

`surge.reducedEffects`: Softens the decorative Surge effects: ability particles are cut to a quarter with no trails, flashes and lights to a quarter, and rings and bubbles stop pulsing. Danger warnings stay as they are. Default: `false`.

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
- **The Surge meter**, in Surge runs: the charge a chaos point took, the total charge, chaos points earned and spent, the charge from each ability, and each purchase with its wave and what it gave. A perk choice that was rerolled is sent as its own record, marked rerolled, before the new offer. Each wave also carries the charge made in it.
- **Each player, by slot number:** ability and ultimate presses, how many of their deaths Null Field blocked, and the meter charge they made. When the [Stats](../stats/README.md) part is on, also deaths (and lava deaths), kills, kills while their ability or ultimate was on, kills by enemy type, kills and shields broken by each weapon (Kinetic Pulse counts as a weapon), friendly kills, shots fired and hit, and time alive.

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
| Power Broker | Most Surge meter charge |
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
