# Recipe execution and coverage

Create one JSON recipe for repeatable checks. Each test has the feature's exact catalog name, steps, and observable assertions. Requests are `/api/` paths. Capture returned IDs; `${variable}` inserts a URL-encoded capture. Captures are local to one test. Set `clearVariables: true` on a step after a scene transition; recapture scene object IDs. Do not replace actual test input with directly calling the method being tested.

```json
{
  "name": "owned-game smoke",
  "tests": [{
    "feature": "Inspector readiness (example only)",
    "steps": [{
      "request": "/api/ping",
      "assert": [{"path": "game.ready", "value": true}]
    }]
  }]
}
```

This example proves inspector readiness only. It does not satisfy the required changed-scope or full session smoke check; those need the real affected gameplay/menu journey and evidence.

Run with an existing game lock; it refuses another owner:

```text
python <Skill>\run_recipe.py <run>\recipe.json --owner <owner> --commit <tested-SHA> --output <run>\evidence
```

The output folder must not exist, so evidence from another build cannot be overwritten. `--url http://127.0.0.1:<port>` selects a second local inspector. Each step supports `capture: {"name": "body.path"}` and `assert: [{"path": "body.path", "op": "eq", "value": 1}]`. Dot paths index arrays with numbers. Operators: `eq`, `ne`, `gt`, `gte`, `lt`, `lte`, `contains`, `length`. Top-level inspector errors, failed/unmet/interrupted operations and new log warnings fail the test. The runner stops at the first failed test and preserves its responses. `allowWarnings: true` on a step permits its immediate warning payload, but the final log check still requires investigation; do not use it to certify a warning-free run.

Keep arbitrary screenshots and manual visual results as separate evidence. The runner cannot judge visual correctness. A successful recipe does not prove installation restoration or code review, and its manifest deliberately leaves both false.

To record coverage, combine automated/manual checks in a results JSON:

```json
{
  "reviewComplete": true,
  "restored": true,
  "environment": {"buildSha256": "actual-DLL-hash", "inspector": "actual-build-or-capabilities", "scope": "local"},
  "checks": [{
    "feature": "exact selected feature or required check name",
    "result": "pass",
    "evidenceKind": "live",
    "commit": "full-tested-commit-SHA",
    "evidence": ["C:/absolute/path/to/actual-evidence.json"]
  }]
}
```

Include every row in `plan.json.selected` and every `requiredChecks` entry. Add actual restoration hash evidence to the Restoration check. `reviewed-files.json` is a JSON array of all reviewed changed repository-relative files, including docs when changed. The tested checkout must be clean and still at the planned commit.

```text
python <Skill>\checkpoint.py --repo <worktree> --plan <run>\plan.json --results <run>\results.json --reviewed-files <run>\reviewed-files.json
```

The canonical store is `SpiderSurge\.claude\qa\checkpoints.json`, independent of task worktrees and shared by both clients. Explicit PR/feature selections can record per-feature coverage, but neither those selections nor general samples advance the global change-review baseline. `--store` selects an isolated store for tests. Never manufacture passing coverage from old transcripts, absent evidence, an automated recipe that did not run, or a test-only stub. Retain failed results in the run folder, not as a successful baseline. No claim of token savings is valid until the same test set has been compared before and after.
