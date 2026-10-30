# Surge

The Surge part of [SpiderSurge](../README.md): a gameplay overhaul for Survival that introduces "Surge Mode", powerful new abilities, and fearsome custom enemies.

To play, stand on the **Surge Survival** platform above and to the left of **Wave Survival** in the lobby. Everyone joins the platform for the usual countdown, then chooses a pain level. The original Wave Survival platform still starts normal Survival. No settings toggle is needed.


<img width="2559" height="1439" alt="image" src="https://github.com/user-attachments/assets/74943e59-c808-42a0-84f2-6da99aca94d3" />

## Features

- **Surge Mode**: A survival overhaul with 2.1 times the normal enemy budget and 1.1 times the movement force on affected enemy brains. Spawn attempts are 2.5 seconds apart, plus Interference's usual extra delay; each enemy keeps its full 2.5-second spawn warning. The live-enemy gate gains a full slot every 8 waves and gradually admits that slot between milestones: while the normal slots are full, the next slot's extra wait shrinks as the next milestone approaches. At wave 4 it is 2.5 seconds, at wave 7 about 0.36 seconds, and at wave 8 the third slot is fully available. Interference lengthens that extra wait too. Empty arenas refill at the same paced interval instead of receiving growing bursts. Only the host controls these spawns, and normal Wave Survival keeps its original pacing and limits.
- **New Abilities**:
    - **Kinetic Pulse**: Release a kinetic pulse that repels enemies, including Wasp Cocoons, and knocks back large projectiles such as rockets; small shots fly on. Cocoons slide away briefly, stop at solid terrain and settle in their new position. Launch speed and knockback are reduced by 25%; zero-gravity maps apply a further 35% reduction and shorten the high-speed launch window from 0.35 to 0.2 seconds.
    - **Overcharge**: The held weapon spends no ammo for 7 seconds, then burns out. Dropping it ends the burst early; picking up another weapon does not transfer the effect. Efficiency level 1 / 2 gives a 25% / 50% chance to salvage it with up to one shot, or 10% durability for Particle Blades, never exceeding its ammo before activation. Each burst resolves once on the host; a green flash marks salvage. Grenades, mines, Boomsticks, Laser Cubes and Death Cubes retain normal behavior. The base cooldown is 20 seconds and starts after the burst. Ability Duration level 1 adds 2.5 seconds; level 2 keeps Care Package's upgrade to all weapon spawn points. Short/Long Term Investment and Ability Cooldown apply normally and do not change salvage chance.
    - **Pocket Dimension**: Store and recall weapons at will, enabling advanced inventory tactics. The independently selectable ultimate, **Black Hole**, opens a field 150 units around the caster that moves with them for 3 seconds. The enemies inside it when it opens stop where they are and can't move, shoot or explode while it lasts, though their stingers, blades and struts still hurt other spiders on contact and a Hornet Shaman's poison cloud keeps chasing. The field pulls them toward the caster, Wasp Cocoons included, and an enemy with any part inside its small inner circle (20 units) is swallowed for good (no death, no drops). The inner circle is a buffer: an enemy is swallowed before any part of it can touch the caster. The pull on enemies and spiders is four times stronger. In open air, enemies across the range can reach the inner circle before it closes. An enemy held up by a wall stops where the field leaves it and goes free. Enemies that come in later aren't pulled. The field also pulls every spider inside it the way a Gravity Grenade does, cutting its web, more weakly toward the edge. A spider whose body reaches the inner circle is swallowed too, teammates included, unless it is immune. A spider close in can't outrun the pull, one farther out can, and the caster can walk away to save them. **Ability Duration** level 2 and **Long Term Investment** each add 50 to the range, **Short Term Investment** takes 50 away, and the cooldown perks change its 20 second cooldown as for every ultimate.
    - **Null Field**: Become immune to all damage for a short time. The independently selectable ultimate, **Neural Backup**, brings back a random dead player after a 10 second cast, or summons 3 friendly Wasps when nobody is dead. An Astral Heckhound ghost counts as dead and comes back where it floats, even when it is the caster's own ghost.
    - **Freeze Time**: Freeze enemies, other players, shots and effects inside the ice ring for 5 seconds. Walls and solid platforms block the freeze. Frozen enemies don't move, shoot or explode, but their blades, saws and poison clouds still hurt on contact. A Hornet Shaman's poison cloud freezes with its shaman when no wall separates them, or on its own when the ring reaches it with a clear path: the whole cloud stops where it is. Frozen shots, from enemies or players, hang in the air and still hit whatever touches them, then fly on when they thaw. A frozen Gravity Saw stops pulling, and one that hasn't opened yet opens that much later. The caster stays free. Other players, including teammates, cannot move, jump, shoot, swap weapons or activate abilities while frozen; their held weapons stop with them. Casting it on enemies another player already froze doesn't add time, it only keeps the longer freeze. **Ability Duration** freezes for longer and farther away. The independently selectable ultimate, **Rewind**, turns every nearby enemy back into plain Wasps; shielded enemies become shielded Wasps.
- **Custom Enemies**:
    - **Twin Whisp**: A Whisp with two cores that fires two shots at once.
    - **Missile Whisp**: Uses the RocketTurret's body while flying and launching rockets. Its charcoal body and bright yellow eye and thrusters stay recognizable with or without a shield. Its pink or red shield bubble is more transparent so the body remains visible.
    - **Twin Blade Whisp**: Elite melee enemies equipped with dual-ended particle blades.
    - **Wasp Cocoon**: A stationary cocoon that starts hatching its first Wasp a second after it appears. Each hatch takes as long as an enemy's spawn warning (2.5 seconds, longer with Interference), and the next one starts 7 seconds after a Wasp comes out, until the cocoon is destroyed, with at most 3 of its Wasps alive at once. Its Wasps don't count toward the wave's limit on live enemies, but the wave still ends only once they're dead. A shielded cocoon hatches Shielded Wasps and emits purple pulses every 2 seconds within 40 units while its shield holds. The pulses gently repel spiders and loose objects without stunning spiders or breaking their shields. Freeze Time pauses the pulses; breaking the cocoon's shield stops them.
    - **Shielded versions**: The shielded Twin Whisp, Missile Whisp and Wasp Cocoon wear the game's pink shield bubble, which breaks on the first hit like every shielded enemy's. Off-screen indicators use the vanilla Whisp/Roller orange without a shield, purple with an ordinary shield, and red with a laser shield. Exploding Rollers always use red. Breaking a shield changes its indicator to orange.
    - **Laser enemies**: Red-shielded versions of most enemies (not Khepris, the Exploding Roller or already shielded ones). Every 4 seconds a faint ring and the Death Ray's charge sound warn for 1 second, then a Laser Cannon ring with radius 35 units around the enemy burns for 1 second. Breaking the red shield removes the ring for good.
- **Chaos points, shop and challenges**: Shared points come from marked map choices, optional challenges and Blood Pact. Each shop item can be purchased once per visit. Bigger Heart grants both maximum capacity and a life. There is no ability-charge meter.
- **Perk Synergy System**: Gain unique bonuses when combining specific mod abilities with vanilla perks (e.g., Overcharge + Efficiency).
- **Ultimate Upgrades**: After wave 30, everyone sees the same random ultimate offers and chooses their own. Any ultimate works with any regular ability, and duplicate choices are allowed. The normal shared perk/map choice follows. No later ultimate swap is offered. [Pain 8](#pain-level-8) makes the separate ultimate choice before wave 1. Each player retains one shared cooldown for their regular ability and ultimate.
- **In-Game Guide**: Explains the new mechanics, controls and perk synergies. Open **Mods** > **SpiderSurge** > **How to play** for a first-run walkthrough and illustrated topics about abilities, enemies, controls, progression and multiplayer. Navigate with a controller or mouse.
- **Achievements**: 25 achievements for ability and combat feats, listed in **Mods** > **SpiderSurge** > **Collection** > **Surge achievements**. See [Achievements](#achievements).
- **Luck Perk**: A new perk that provides a chance to skip perk levels and jump straight to higher-tier upgrades.
- **Custom Controls**:
    - **Ability**: **Q** or **L2**. **Ultimate**: **C**, or **L3 + R3** together. These are fixed. The controller ability button replaces that button's game action, so L2 no longer jumps (A and L1 still do).
    - **Per-Player Remapping**: While holding **Select**, **double-tap** any button to set it as your ability key. It works in the lobby, in a run or Team Versus match, and while your spider is dead, and lasts until you leave the game.
    - **Reset**: Release Select after remapping, then hold it for 3 seconds to go back to L2. Keeping Select held after a successful remap does not undo it. The controls guide lists each local controller's current ability button.
- **Pause menu confirmations**: **Restart** and **Main Menu** in the pause menu ask "Are you sure?" before acting.
- **Team Versus**: Everyone can get the same Surge ability in versus, set up with [Teams](../teams/README.md).

<img width="2559" height="1439" alt="image" src="https://github.com/user-attachments/assets/a92e59fd-22f3-4ab9-922c-22e4b57ad999" />

<img width="1280" alt="The first-run walkthrough in How to play" src="../docs/images/guide-first-run.png" />

## Pain Level 8

Surge Survival has a Pain 8 after Pain 7. It unlocks at the same wave Pain 7 needs, reached on Surge Pain 7. Pain 8 keeps all of Pain 7's modifiers and gives separate personal ability and ultimate choices before wave 1, so wave 30 brings a normal perk choice. Every player may pair any offered ultimate with their ability. Every normal wave enemy spawns twice and the enemy capacity is doubled. Friendly summons and Cocoon hatchlings are not doubled. The No abilities setting does not apply to Pain 8. Its high scores are separate from Pain 7 and from normal Survival, which keeps its seven pain levels.

## Achievements

The Collection menu shows the number of unseen unlocks. In Surge achievements, cycle **Show** to **New** to see just those entries; select one to mark it seen, or use **Mark all seen**. Earned progress is kept.

SpiderSurge has its own achievements for ability and combat feats. See them, with the date each one was unlocked and who did it, in **Mods** > **SpiderSurge** > **Collection** > **Surge achievements**. A locked achievement shows only its name there; click it (or press A on it) to see how to get it. They are saved on your PC in `SpiderSurge\progress.dat` in the game's own save folder (`%USERPROFILE%\AppData\LocalLow\Neverjam Studio\SpiderHeckApp` for Steam), next to the Stats title collection and the Surge highscores, and a popup announces each new one. Older versions kept them in the game's save file; they move on the first launch. "Teammate" means any other player in Survival, and a player of your team in [Team Versus](../teams/README.md).

| Achievement | How to unlock it |
|---|---|
| Event Horizon | Feed a Hornet Shaman to your Black Hole |
| Total Destruction | Kill 3 teammates with one Thermal Detonation |
| I Will Keep You Safe | Feed a teammate to your Black Hole |
| Unlimited Power | Use Overcharge while holding a Particle Blade that appeared with Size Matters and Thick Blades both at level 2 |
| Doomsday Scenario | Send an armed Biggest Grenade into the Pocket Dimension |
| Begone | Knock an enemy into lava with Kinetic Pulse, so Stats credits you the kill. Needs Stats on |
| Unstoppable Force Against Immovable Object | Survive a Biggest Grenade blast inside Null Field |
| Healer | Revive the same teammate twice in one wave (or Team Versus round) with Neural Backup |
| Yay I Guess? | Pick the Nothing perk when Luck made it a Lucky Upgrade |
| You Are Not Done Yet | Revive an Astral Heckhound ghost with Neural Backup, your own included |
| Kiss of Death | Hit a Power Twin Blade Whisp with a Particle Blade below 15% durability |
| Not On My Watch | Block a Laser Cannon or Laser Cube beam headed for a teammate while Null Field makes you immune. A teammate who is immune at that moment doesn't count |
| Max Capacity | Buy a Bigger Heart (+1 max lives) in the Surge shop |
| Factory Reset | Turn a Wasp Cocoon into a Wasp with Rewind |
| Hold That Thought | Freeze a laser enemy while its laser ring is firing |
| Cold Cuts | Kill 3 frozen enemies with a Particle Blade during one Freeze Time activation |
| Still Sharp | Die to any frozen enemy, including its stinger, blade, struts, saw or poison |
| Return to Sender | Push a Missile Whisp's rocket back with Kinetic Pulse and kill its original shooter with the blast |
| Eviction Notice | Push a Wasp Cocoon into lava with Kinetic Pulse |
| Personal Space | Knock 5 enemies away with one Kinetic Pulse |
| Family Heirloom | Carry the same stored weapon through 3 map changes; deaths and retries on the same map do not add to the count |
| Light Snack | Swallow 3 different enemy types with one Black Hole; shield and laser variants of the same type count once |
| Overkill | Use Thermal Detonation to kill the wave's last enemy: a single plain Wasp |
| We Brought Our Own | Have a friendly Wasp summoned by Neural Backup kill a Wasp hatched by a Cocoon |
| Wasn't Me | Have an enemy trigger a sticky grenade and die in that same grenade's explosion |

## Chaos points, shop and challenges

The team shares a chaos-point balance, shown with a gold spark inside a broken ring. Ability use never earns points. At each map choice there is a 75% chance to mark exactly one offered card with **+1 Chaos**, paid when that map is chosen. Rerolling keeps the bonus on the same card slot, without another chance roll.

The shop offers two items per visit: Reroll (1 point), Healing (2), Mystery Perk (3), Bigger Heart (5; +1 maximum life and +1 current life), or Blood Pact (one life for one point, leaving at least one life). Each item can be bought once by the whole team per visit, including rerolls and Blood Pact. Sold-out items remain visible. Buying Mystery Perk replaces its shop slot with the acquired perk's icon, name and description for everyone until the next visit. Rebuilds, rerolls and reconnects do not restock them. Press right from the last perk card to enter the shop, select an item and then Buy. Any player can purchase.

After choosing a map, the team has a 50% chance of seeing one optional challenge before the map loads. The host rolls this chance once for the team; otherwise the map loads immediately. The host holds either Accept or Skip for 0.8 seconds; a bar shows hold progress on both buttons. Recent challenge offers are avoided when alternatives are eligible. Release the previous button press before starting a hold. Releasing early, moving away, switching buttons, opening the pause menu or losing focus cancels the hold. Remote players see that the host is deciding; only the host can accept or skip. The game waits without a countdown, and the pause menu stays usable meanwhile. There is no challenge after the starting ability pick. Restrictions last through wave retries until the next map-choice boundary, where rewards and borrowed lives return before shopping. Game over or quitting pays nothing.

| Challenge | Effect | Reward |
|---|---|---:|
| Poisoned Heart | Reduce current lives to one; return withheld lives upon completion. Losing the last life ends the run. Requires two current lives. | 3 |
| Double Cooldown | Double regular and ultimate cooldowns after perk adjustments. | 1 |
| No Abilities | Abilities disabled; ultimates remain usable. | 2 |
| Life Wager | First lost team life costs one point, floored at zero, and forfeits the reward. Further losses cost no points. | 1 if no life lost |
| No Ultimate | Ultimates disabled; regular abilities work. Only offered with an unlocked ultimate. | 1 |
| Overrun | Larger enemy waves: 50% more budget, with unchanged spawn pacing and simultaneous-enemy limits. | 2 |
| Clean Hands | With two or more players, finish without a teammate kill or teammate shield break credited to a player. Failure costs 1 Chaos point once, floored at zero, and forfeits the reward. Reversing all friendly-fire credit refunds the points taken without counting them as earnings. | 1 |

The HUD uses a small currency icon and balance beneath the lives display, with a quieter challenge status line. First encounters with laser enemies and cocoons show a brief local hint without pausing; the guide remains available afterward. Surge results add a compact recap above the existing titles and retain the full scoreboard separately. Map bonuses, completed challenges, purchases and penalties are logged. The economy also works with abilities disabled; inapplicable challenges are excluded. Existing perk-synergy hints remain below the cards.

## How to configure Surge

### From the Mods menu:

Open the pause menu and pick **Mods** > **SpiderSurge** > **Settings**. Brightness is under Display, and **Unlimited time to choose perks** is under Survival; it applies immediately to both normal Wave Survival and Surge, including an open perk choice. Select **Surge** for ability settings and **Advanced run rules** for **Play without abilities**, which applies from the next run. Hover over a setting or select it with a controller to read its explanation in the side panel. Use the right stick or mouse wheel to scroll that explanation. Choose Beside spider or Corner panel on the Surge page; detailed controls are in Customize ability display. **Reset Surge settings to defaults** leaves the shared perk timer and telemetry settings alone; telemetry is on **Settings** > **Records and privacy**.

**Brightness** adjusts the local game's brightness from 50% to 200%, with 100% preserving the original appearance. Changes apply immediately and persist across restarts under `display.brightness`. Each online player controls their own brightness.

### Using the Yaml file:

Surge's settings are the `surge` section of `...\Silk\Config\Mods\SpiderSurge.yaml`. See the [upcoming release's Settings](../NEXT_RELEASE.md#settings) for how the current file works.

<details>
<summary>Explanation of the fields</summary>

### Gameplay:
The lobby platform selects the mode for each run. Older `surge.surgeMode` and `parts.surge` settings are ignored.

The ability and ultimate keys are fixed, so they are not in the file. An older `surge.dpadUltimate` is ignored.

`surge.unlimitedPerkTime`: Disables the timer on perk selection screens in normal Wave Survival and Surge, including an open choice. Turning it off resumes the countdown. Its toggle is on the main Settings page, separate from Surge's no-abilities rule. The existing config key and saved preference are retained. With it on, clicking a card only locks it, and a **Confirm** button under the cards (press down from a card) makes the pick, so a stray press can't choose a perk. With the timer on, a click picks at once as in the normal game. Personal ability and ultimate choices always wait for every player to lock their own card. A card keeps the controller focus while the mouse passes over a disabled Confirm or Buy button, or when a menu or click clears the selection. The host's setting applies online. Default: `true`.

`surge.noAbilities`: If true, Surge Survival runs have no abilities, ultimates or ability perks (Ability Cooldown, Ability Duration, Short and Long Term Investment). The first perk choice is a normal one, waves 30 and 60 bring no ultimate choice, and the ability keys keep their normal game actions. Luck and the chaos economy remain available; ability challenges are excluded. Applies from the next run, and doesn't change Team Versus abilities. Pain 8 ignores it and keeps abilities. Default: `false`.

With abilities enabled, a shared screen offers as many random abilities as a normal perk choice has cards (3, or 2 from pain level 3, plus Picky) before wave 1. Each player browses and confirms independently; repeated picks are allowed. Controllers use the D-pad or left stick, A to ready and B to undo. Keyboard uses arrows, Enter and Backspace. The arena waits for everyone. Colored player markers wrap into rows on each card for large lobbies. After wave 30, everyone chooses an independent ultimate before the normal team perk/map screen. Pain 8 makes the ultimate choice immediately after the starting ability choice. New arrivals choose at the next wave boundary. Ordinary perks remain shared.

`surge.indicator.compactHud`: Replaces the world rings with a compact panel at the bottom right, with a named row for each player. Active effects have a draining bar and remaining seconds; cooldowns have a filling bar and remaining seconds. Both use game time. Pocket Dimension also shows its stored weapon. Change this immediately under Surge > Use compact ability HUD. The existing colors and Only show when ready setting apply to both displays; size and position settings apply to the world rings. Default: `false`.

The world ring drains in the active color over a timed ability or ultimate's duration, then fills clockwise in the cooldown color until ready. Active effects without a duration keep a full ring. Both timers stop while game time is paused.

### Sound:
`surge.abilityVolume`: **Surge sound effects volume** controls abilities, ultimates, perks, shop purchases, choices, challenges, achievements and title reveals together, from 0 to 100, on top of the game's SFX volume. Set it to 0 to mute all Surge sounds. 80 is the original volume and 100 is a quarter louder. Default: `50`. The existing config key is retained. Source audio and license details are in [Sound credits](../docs/SOUND_CREDITS.md).

### UI:
The guide is available from **How to play** in settings. The old `surge.showTutorial` and `controls.surgeGuide.keyboard` values are no longer used.

`surge.indicator.radius`: The size of the ability indicator, from 0.5 to 10. Default: `1.5`.

`surge.reducedEffects`: Softens the decorative Surge effects: ability particles are cut to a quarter with no trails, flashes and lights to a quarter, Kinetic Pulse's screen shake and colour fringe to a quarter, and rings and bubbles stop pulsing. Danger warnings stay as they are. Default: `false`.

`surge.indicator.offset.x`, `surge.indicator.offset.y`: Where the indicator sits, relative to the spider. Defaults: `3` and `8`.

`surge.indicator.availableColor`, `surge.indicator.cooldownColor`, `surge.indicator.activeColor`: The indicator's color when the ability is ready, on cooldown, and active, as hex codes. The Mods menu cycles through 8 colors; in the file you can use any hex code. Defaults: `#00FF00`, `#FF0000` and `#87CEEB`.

`surge.indicator.showOnlyWhenReady`: If true, the indicator only appears when the ability is off cooldown. Default: `false`.

### Logging:
`surge.statsLogging`: The Surge part of **Export detailed reports** under Settings > Records and privacy. Writes a local text report of Surge runs. Match history and anonymous sharing independently prepare the data they need. Default: `true`.

`surge.telemetry`: **Share anonymous run reports** under Settings > Records and privacy. Uploads the report of each finished Survival run, with or without Surge mode. See [Telemetry](#telemetry). Default: `false`.

`surge.showTelemetryPrompt`: Asks whether to share run stats a few seconds after the game starts. Answering the question turns it off. Default: `true`.

</details>

## Telemetry

SpiderSurge supports optional telemetry to help track balancing and gameplay trends in Surge matches.

### How to enable

Telemetry is off until you agree. The first time the game starts with Surge on, a popup asks whether to share anonymous run stats. You can change your answer at any time with **Share anonymous run reports** in **Mods** > **SpiderSurge** > **Settings** > **Records and privacy**, or `surge.telemetry` in the file. Sharing does not require local text exports.

### What is sent

One report per finished Survival run, with or without Surge mode, sent by the host. A plain Survival run sends the same report with no ability or ultimate use, and every perk choice counted as a plain perk:

- **The run:** whether it was a Surge run, mod version, length, player count, waves survived, pain level, how it ended (lives ran out, restart or quit), lives left and the most lives, the final perks with their levels, whether the perk choice timer is unlimited, and whether the run had abilities.
- **Each perk choice:** the wave, the type of choice (ability, ultimate, ultimate swap or perk), the cards offered and which of them were lucky upgrades, the card picked and its new level, whether the timer ran out, and how long the choice took. A perk screen still open when the run ends is sent with no card picked.
- **Each wave attempt:** the wave number, the map, whether it was won, lost or left, how long it took in game time, lives at its start, how many spiders died in it, how many abilities and ultimates were used in it, and how many times Null Field blocked a hit that would have killed a spider without a shield.
- **Chaos economy**, in Surge runs: points earned and spent, point sources, challenge offers and outcomes, penalties, and purchases, plus a log of these events by wave. When a long run's report gets too big, the oldest waves, perk choices and events are left out first. Historical charge fields remain zero in new telemetry.
- **Each player, by slot number:** ability and ultimate presses, how many of their deaths Null Field blocked, and, from [Stats](../stats/README.md), deaths (and lava deaths), kills, kills while their ability or ultimate was on, kills by enemy type, kills and shields broken by each weapon (Kinetic Pulse counts as a weapon), friendly kills, shots fired and hit, and time alive.

- **Titles**, when the run had 2 or more players: every title earned at the end of the run, with the slot number of the player who earned it and whether it was one of the cards on the end screen, whether titles were shown at all, how many titles the run newly unlocked in the [title collection](../stats/README.md#title-collection), how many are unlocked on this PC in total, and how many titles exist. The collection itself, with its dates, is never sent.

A very long run's report leaves out its oldest perk choices and waves, so it stays within the 32 KB the relay accepts.

### Privacy and identity

- Telemetry uses an anonymous random ID, not player names.
- The anonymous ID is generated locally and stored in:
    `...\Silk\Logs\SpiderSurge\telemetry_anonymous_id.txt`
- The **Settings** > **Records and privacy** page shows the ID, and **Reset telemetry ID** replaces it with a new one.


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

- This mod overrides the highscores displayed in the survival menu. This is intentional, as it allows you to see your highscores for the modded game. The global position in the leaderboards will NOT be tracked, as this requires changes to the backend. Surge Survival keeps its own highscores in `SpiderSurge\progress.dat`, and Wave Survival still shows your normal ones from the game's save. Joining an online Surge Survival game checks the pain levels you unlocked in Surge Survival.
- Surge Mode is designed for Survival Mode. Its abilities only reach versus through the [Teams](../teams/README.md) part.

## The Future

- More abilities and ults
- More enemies

Ability indicators respect challenge restrictions: the compact HUD names the allowed action or shows that abilities are disabled, and blocked input plays the normal rate-limited unavailable sound.
