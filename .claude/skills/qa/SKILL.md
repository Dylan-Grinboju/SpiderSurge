---
name: qa
description: Review and test SpiderSurge changes with SpiderInspector, reuse completed QA coverage, fix confirmed in-scope bugs, and report evidence. Supports incremental change checks, general health checks and full release checks.
disable-model-invocation: true
argument-hint: "[PR | branch | feature | general [part] | full | release; empty = incremental]"
---

# QA SpiderSurge

Target: $ARGUMENTS

Use the shared Claude workspace rules. Work in a task worktree, never the main checkout with other sessions' edits. The skill and coverage store live in the canonical `SpiderSurge\.claude\` folder, shared by Codex and Claude. `<Skill>` below is this file's folder.

| Request | Scope |
|---|---|
| Empty | Changes since the last completed, applicable QA checkpoint; fall back to the latest `v*` release if none exists |
| PR, branch or feature | That change and affected neighbours; preserve the user's named scope |
| `general [part]` | Rotating health check: six stale/currently unverified features and two random features, plus the fixed session checks |
| `full` or `release` | All changes since the latest release, with the full session and performance checks |

Do not silently narrow an explicitly requested whole-mod or release review. Do not treat old history dates as verification of changed code. No checkpoint is seeded from the historical reports: the first incremental run establishes evidenced coverage.

## Scope and references

Resolve the requested commit and find/create its worktree. For a PR, read its title, body and file list first; fetch the diff once. For default/full runs:

```text
python <Skill>\pick.py --repo <worktree> --mode incremental --target HEAD --json --output <run>\plan.json
```

Use `--mode general [--part surge|stats|teams|fixes|core]` or `--mode full` as requested. Explicit changes can use `--files <paths> --random 3 --outside <touched-parts> --repo <worktree>`. Record the seed. The output contains selected rows, their code, relevant shared patch targets and unmapped changed files. Resolve unmapped files and update their feature rows. Never load the entire feature catalog or history just to select tests.

Keep `<run>` under `%TEMP%\qa-<topic>\`. Save the plan, commit/build, backup hashes, pending checks and log/event cursors in a small current-state file. Put full responses in evidence files, not in that recovery file. Discard object IDs across scene reloads. Reuse only checkpoint coverage whose dependency code and feature definition remain unchanged.

Read references on demand. `python <Skill>\reference.py recipes.md` lists headings; add an exact heading to print only that section. Use the same helper for `checklist.md`.

- Before judging a change, read the relevant game behaviour/timing sections and touched part READMEs. Trace the game methods needed to resolve the triggering path; use source and scene data before experimenting.
- Apply relevant checklist items to changed code and shared patch interactions. Do not restart a full review of unchanged features already covered by a completed checkpoint.
- `README.md` describes the published release. `NEXT_RELEASE.md` and part READMEs describe main; never rewrite the release README to match unreleased work.
- Keep `features.md` current when features change; edit the affected rows only.

## Review and evidence

For a small or focused change, review directly. When a large change benefits from parallel review, use at most two reviewers by default, with disjoint assignments, exact files/diffs, relevant game methods and questions. Add another only for a specific uncovered question and record why in the plan. Give each reviewer only the needed context when supported. Avoid overlapping whole-part audits and repeated reference reads. Each report must contain a triggering state, wrong player-visible result and `file:line`, or a concise no-finding result. Verify suspected bugs before adding them to the plan.

Plan one observable check per changed feature and suspected bug, plus selected neighbours/random rows. Include P2 for player-keyed behaviour. A shared patch ordering change needs a case where both features act together. Distinguish new regressions from bugs already present in the release. Label findings *confirmed live*, *confirmed in code*, or *not reproduced*. “By design” needs a README, code comment, memory or user instruction; otherwise ask as a question in the final report.

This skill tests **local multiplayer only**; do not report untested online behaviour. For `general`, `full` or `release`, also read [full-check.md](full-check.md) and the performance recipe. Do not run the fixed full-mod journey for every narrow incremental change.

## Live work

Read the Setup recipe before installing a test build. Preserve these invariants:

- One game owner. Ping first; do not take or close another session's game, or force a lock.
- Build the task worktree and run `access-scan.ps1`; require zero non-public references. Publicized libraries compiling a call does not prove Mono permits it at runtime.
- Snapshot the installed DLLs, config and save/title files while the game is closed. Keep the original DLL under `Silk\Mods\Disabled\`; Silk loads other subfolders. Turn telemetry off before launching, including previously queued reports. Mute on launch.
- Use real input, menus and hits for test actions. Direct methods may set up state. Use condition waits; while paused, step with `until=`. Freeze, immunity and pinning must not mask the feature under test.
- Use targeted fields/components and explicit evidence captures. Steps should use `players=0` when their full player state is unnecessary; new inspector builds also support it on waits. Do not assume a parameter works on older installed builds.
- Setup batches can use `bodies=errors`; use `last` only when its final observation is sufficient. On supporting builds, `bodies=compact&capture=0,3` retains selected bodies, failures, warnings and unmet conditions. Never discard intermediate measurements needed by the test.
- Keep incremental log/event cursors. After each test, check for new warnings/errors with known noise hidden. Compare unexpected failures with the release build before blaming the change. Retain failures and raw evidence even when compact output is used.
- Use cropped, readable screenshots for visual evidence. Field assertions cannot prove layout or colours; do not repeatedly send unchanged full screens.

For repeatable checks, use [runner.md](runner.md) and `run_recipe.py`: it executes requests and assertions under your existing lock, writes complete evidence, checks incremental logs and prints a compact result per feature. Investigate failures instead of manually repeating successful setup. It does not install builds, take locks or restore the game for you.

Fix confirmed in-scope bugs in the task worktree. Reproduce on the old build when possible; rebuild and rerun the failed test and affected neighbours. A SpiderInspector bug belongs in that repo: report it, do not silently work around it. Before relaunching, compare config/save hashes and preserve newer user state if another session used the game. No code comments except one line on major methods or complicated logic.

## Restore, checkpoint and report

Release time, return to the lobby, remove your pads and close only your owned game. Restore original DLLs/config/saves/title files and verify hashes. No version bumps, tags, releases or uploads. Commit in-scope QA fixes to the tested branch. Push, open PRs or change PR descriptions only when asked.

Append live results to `history.md` as before. Also record a completed checkpoint using [runner.md](runner.md): every planned feature and required smoke check needs a live pass at the tested commit with an evidence file; review must cover all changed files and restoration must pass. Failed, partial, dirty or code-only runs must not advance the baseline. General samples never advance the global change-review baseline. After fixes, regenerate the plan at the new clean commit and retest before recording it.

Use [report.md](report.md). Include results for neighbours and random picks, actual numbers and evidence, fixed commits, old/out-of-scope findings, missing tools and restoration status. State reused coverage and untested work honestly. Offer publishing only after the concrete result exists; no automatic push or release.
