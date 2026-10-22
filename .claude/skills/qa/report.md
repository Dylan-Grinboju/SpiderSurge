# Report

Write the report in the chat, in this order. Keep each line to one fact, with the numbers that prove it.

## 1. Results

One row per test from the plan, in plan order:

| # | Kind | Change, bug or feature | Test | Result | Evidence |
|---|---|---|---|---|---|
| A | Change | Storage keeps the stored weapon across a death | Shotgun stored with LB, then a kill | Pass | `GetStoredWeapons(0)` = Shotgun after the kill; no inactive Weapon left in the level |
| B | Suspected bug | Pulse ignores frozen enemies | Freeze Time, then a pulse next to 3 wasps | Fail, fixed in `abc1234` | Before: wasps moved 14 units. After: 0 |
| C | Neighbour | Null Field (shares `SpiderHealthSystem.Damage`) | Friendly-fire hit inside Null Field | Pass | `act/kill` reply says immune; no death event |
| D | Random | Team win screen | Win a 2v1 match | Pass | Win screen lists Red 3, Blue 1 (screenshot) |
| E | Change | Titles with a tie | 2 players with equal kills | Not reproduced | No title shown; the tie path wasn't reached live (confirmed in code: `stats/Titles.cs:88`) |

- **Kind** is `Change`, `Suspected bug`, `Neighbour` (with what it shares), `Random`, or in a general check `Feature`, `Session`, `Startup`, `Performance` or `Hot spot`.
- **Result** is `Pass`, `Fail`, `Fail, fixed in <hash>`, `Fail, also in <tag>` (the release DLL fails the same way), or `Not reproduced`.
- **Evidence** is what you saw: an event, a field before and after, a log line, a count, a distance, or a screenshot you looked at.
- Label findings that weren't seen live as *confirmed in code*, with `file:line`.
- **General check:** group the rows by part, and start with one line per part: how many features were checked and how many passed. End the section with the garbage per frame and fps of each measured state, next to the last general check's numbers from history.md.

## 2. Fixed

One line per fix: the bug, the cause (`file:line`), the fix, the commit hash, and the retest that passed.

## 3. Not fixed

Each confirmed bug left open and why: out of scope, needs the user's decision, or too risky without more testing. Say what you would change.

- Keep bugs that were already in the last release under their own heading.
- Write each one so the user can answer in one line. Say what a player sees, when it happens, and what you'd change. Don't use internal names alone: "the caster can revive themselves" needed a follow-up "what? how??".
- Don't put anything under "by design" without the source that says so (SKILL.md, "Rules").

## 4. Out of scope

Bugs found in other repos (SpiderInspector, SpiderHeck_cheats), base-game bugs SpiderSurge could fix, and online-only issues only if the user asked for them. Offer a fix for each; don't make it.

## 5. Missing tools

SpiderInspector requests that would have made a test possible or simpler: what you needed, what you did instead, and the request you'd add.

## 6. State

One line: the game is closed, and the DLLs, config and save files are restored and hash-checked, and the titles folder is back to how it was. Or say exactly what isn't. Name the match logs the run left in `Silk\Logs\SpiderSurge`, and say that history.md has the run's rows.

## 7. Offer

Ask whether to push the fixes and add the results to the PR. Don't do either unprompted. A general check has no PR: offer to open one for its fixes.

When writing commit messages and PR bodies, claim only what you checked. Write "reproduced before the fix" or "already in v0.4.0" only for bugs where you saw it.

## The PR section

When the user says yes, add or update a "Tested in game" section in the PR body (`gh pr edit <n> --body-file <file>`). Keep the rest of the body as it is.

```markdown
## Tested in game

Steam build, SpiderInspector 0.5, 2 virtual pads, 2.Smash.

- **Storage across a death:** stored a Shotgun with LB, then a kill. The respawn restored it, and no hidden weapon was left in the level.
- **Pulse on frozen enemies:** with Freeze Time on, a pulse next to 3 wasps moved them 0 units (14 before the fix).
- **Settings page at 1920x1080 and 2560x1440:** the text fits, and controller navigation reaches every row.
```

One bullet per test: a bold name, what you did, and what happened, with the numbers. Leave out tests that weren't run, and say "not tested live" for changes covered only by the code review.
