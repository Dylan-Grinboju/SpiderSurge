# SpiderTeams

A team versus mod for Spiderheck. Split the lobby into teams and fight it out: a round ends when only one team is left standing, not one spider.


## Features

- **Team Versus**: A versus round ends when one team is left standing. Every member of the winning team scores, alive or dead, and the first team to reach the winning score takes the match.
- **Any Number of Teams**: Play with 2 to 32 teams. With as many teams as players, everyone is on their own team, which plays exactly like normal free-for-all.
- **More Than 4 Players**: Works with [InfiniteFriends](https://github.com/Dylan-Grinboju/InfiniteFriends) for up to 32 players.
- **Pick Teams in the Lobby**: Press **Y / Triangle** on a controller or **T** on the keyboard to switch to the next team. New players join the smallest team automatically.
- **Configurable Friendly Fire**: Teammates' hits can push you (the default), do nothing, or work as normal.
- **Team Visuals**: Recolor spiders in their team's color, show a team marker above each spider, both, or neither.
- **Team Spawns**: Teammates spawn close to each other, and the teams spawn apart.
- **Team Win Screen**: The win screen and the lobby summary show each team's score and members.
- **In-Game Settings**: Every setting can be changed from the Mods menu, including moving players between teams.
- **Automation**: Includes an integrated mod updater to notify you when a new version is available.

## Installation

1. Install Silk Mod Loader: https://github.com/SilkModding/Silk
2. Download the `SpiderTeams.dll` from the [releases page](https://github.com/Dylan-Grinboju/SpiderTeams/releases) or build it from the source.
3. Place the `SpiderTeams.dll` into your Silk `mods` folder.
4. Launch the game.

Team versus is on as soon as the mod is installed. You can turn it off from the Mods menu at any time.

## How to Play

1. Gather everyone in the lobby. While team versus is on, the versus platform reads **TEAM VERSUS**.
2. Each player presses **Y / Triangle** (controller) or **T** (keyboard) to switch to the next team. Every switch is announced, for example "P2 joined Blue Team".
3. Start versus as usual. Teams are locked until the match ends.

- A round ends when only one team has spiders alive. If the last spiders all die during the slow motion at the end of a round, the round is a draw.
- Every member of the winning team gets a point, alive or dead, so everyone on a team has the same score on the scoreboard.
- The first 8 teams are Red, Blue, Green, Yellow, Purple, Orange, Cyan and Pink. From the 9th team on, teams are numbered and get generated colors.
- Teams are kept between matches until someone changes them or leaves. They are not saved when you quit the game.
- Only players on the host's PC can switch with the team button. The host can move anyone, including online players, from the Mods menu.

## Friendly Fire

| Mode | What a teammate's hit does |
|---|---|
| `push` (default) | Knocks you back and stuns you, but never kills you or breaks your shield |
| `ignore` | Nothing at all |
| `damage` | Works as normal and can kill |

- Hitting yourself always works as normal, so your own grenade can still kill you.
- If the mod can't tell who caused a hit, the hit works as normal. A gap in the tracking will never save someone from a real kill.

## How to configure the Mod

### From the Mods menu:

Open the **Mods** menu and pick **SpiderTeams**. The page has every setting from the Yaml file, plus:

- The team list, where the host can move each player to the next team, shuffle everyone, or balance the team sizes.
- **Reload from file**, to pick up edits to the Yaml file without restarting the game.
- **Reset to defaults**.

Changes apply right away and are saved to the Yaml file. Team versus, the team count and the team list are locked while a match is running.

### Using the Yaml file:

The first time you launch the game with the mod installed, a configuration file will be created at: `...\Silk\Config\Mods\SpiderTeams.yaml`

Changes to this file take effect after a game restart, or right away with **Reload from file** on the mod's page. If a value is invalid, the mod uses the default instead (or the nearest allowed team count) and writes a warning to the Silk log.

<details>
<summary>Explanation of the fields</summary>

### Teams:
`teams.enabled`: Toggles team versus. If false, versus plays exactly like the base game. Default: `true`.

`teams.count`: How many teams to split the players into, from 2 to 32. New players join the smallest team, so with at least as many teams as players everyone starts on their own team. Default: `2`.

### Friendly fire:
`friendlyFire.mode`: What a teammate's hit does: `push`, `ignore` or `damage`. See [Friendly Fire](#friendly-fire). Default: `push`.

### Visuals:
`visuals.recolorSpiders`: During a match, each spider's main color becomes a shade of its team's color. Teammates get slightly different shades, so you can still tell them apart. Second colors and hats are kept, and the original colors come back when the match ends. Your saved colors are never changed. Default: `true`.

`visuals.showMarker`: Shows a team-colored marker above each spider, in the lobby and during matches. For now it only shows on the host's PC. Default: `false`.

You can use both, one, or neither. With both off, teams only show in the announcements and on the mod's page.

### Spawns:
`spawns.groupTeammates`: Spawns teammates close to each other, with the teams spread apart. Works with any number of spawn points, including the extra ones from InfiniteFriends. Default: `true`.

### Input:
`input.cycleTeamGamepad`: The controller button that switches team in the lobby. Default: `<Gamepad>/buttonNorth` (Y / Triangle).

`input.cycleTeamKeyboard`: The keyboard key that switches team in the lobby. Default: `<Keyboard>/t`.

The Mods menu cycles through a few presets: Y, L3, R3 or Select on a controller, and T, G, R or Tab on the keyboard. In the file you can use any Unity Input System path, such as `<Keyboard>/y`, or `none` to turn the button off.

### Updater:
`updater.checkForUpdates`: If true, the mod checks for a new version when the game starts. Default: `true`.

</details>

## Online Play

SpiderTeams runs on the host. Couch games on one PC always count as hosting, so they just work.

- **You host with the mod**: The team rules, friendly fire and team spawns apply to everyone. Players without the mod still see the team colors, the announcements and the right scores, but their win screen may name only one player from the winning team. For now, the team marker only shows on the host's PC.
- **Someone else hosts**: The mod does nothing. When a match starts, it shows a one-time message that teams only apply when you host.
- Online players can't use the team button yet. The host picks their team from the Mods menu.
- Like other gameplay mods, SpiderTeams makes Silk turn off leaderboard score uploads for the session.

## Compatibility

- **[InfiniteFriends](https://github.com/Dylan-Grinboju/InfiniteFriends)**: Supported. Nothing in SpiderTeams assumes 4 players, and team spawns use the extra spawn points it adds.
- **[SpiderSurge](https://github.com/Dylan-Grinboju/SpiderSurge)**: Can be installed alongside. SpiderTeams only changes versus and SpiderSurge only changes survival, so they stay out of each other's way. Proper support is planned.
- **[SpiderStats](https://github.com/Dylan-Grinboju/spiderheck_stats_mod)**: Can be installed alongside. SpiderStats doesn't know about teams yet, so it counts every kill on another player as a friendly kill, and it may count a teammate's hit as a kill even when SpiderTeams turned it into a push.

## Notes and disclaimers

- This mod only changes versus. Survival is untouched.
- With team versus off, versus plays exactly like the base game. With one player per team, the rules are the same as normal versus, with team colors and announcements on top.
- A player who joins in the middle of a match starts at 0 points, even if their team is ahead. The match is still decided by the team's points.
- Team colors use the game's temporary color sync, so your saved spider colors are never touched.
- You can reset your configuration at any time with **Reset to defaults**, or by deleting the YAML file; it will be recreated with default values on the next launch.

## The Future

- SpiderSurge support
- Sharing the teams with online players who have the mod, so they can pick their own team and see the markers and the team win screen
- Giving a player who joins in the middle of a match their team's score
- SpiderStats integration, so it can tell teammate kills from enemy kills

## Other Mods

If you enjoy SpiderTeams, check out my other mods:

- **SpiderSurge**: A gameplay overhaul for survival mode with new abilities, custom enemies and perk synergies. You can find it here: https://github.com/Dylan-Grinboju/SpiderSurge
- **SpiderStats**: A statistics tracking mod for Spiderheck that monitors player performance, game events, and awards fun titles at the end of each game. You can find it here: https://github.com/Dylan-Grinboju/spiderheck_stats_mod
