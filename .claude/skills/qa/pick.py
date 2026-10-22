import argparse
import fnmatch
import json
import pathlib
import random
import re
import subprocess

from checkpoint import STORE, ancestor, baseline, fingerprint, git, read_store

HERE = pathlib.Path(__file__).resolve().parent
PARTS = {'Surge': 'surge', 'Stats': 'stats', 'Teams': 'teams', 'Game fixes': 'fixes', 'Core, settings and menus': 'core'}


def cells(line):
    result, current, quoted, escaped = [], [], False, False
    for char in line.strip().strip('|'):
        if char == '`' and not escaped:
            quoted = not quoted
        if char == '|' and not quoted and not escaped:
            result.append(''.join(current).strip())
            current = []
        else:
            current.append(char)
        escaped = char == '\\' and not escaped
    return result + [''.join(current).strip()]


def paths(text):
    return [p.replace('\\', '/') for p in re.findall(r'`([^`]+)`', text) if ('/' in p or p.endswith(('.cs', '.csproj', '.props'))) and not p.startswith('/api/')]


def matches(file, patterns):
    return any(file == p or (p.endswith('/') and file.startswith(p)) or fnmatch.fnmatchcase(file, p) for p in patterns)


def catalog():
    rows, hot, part, hotspots = [], [], None, False
    for line in (HERE / 'features.md').read_text(encoding='utf-8').splitlines():
        if line.startswith('## '):
            part = PARTS.get(line[3:].strip())
            hotspots = line[3:].strip() == 'Patch hot spots'
        elif line.startswith('| ') and not line.startswith(('| Feature', '| Method', '|---')):
            c = cells(line)
            if part and len(c) >= 4:
                rows.append(dict(part=part, name=c[0], check=c[1], code=c[2], shares=c[3]))
            elif hotspots and len(c) >= 2:
                hot.append({'method': c[0], 'patches': c[1], 'paths': paths(c[1])})
    return rows, hot


def last_verified(repo, target, store, rows, hot):
    result = {}
    for run in store['runs']:
        if not run.get('complete') or not ancestor(repo, run['commit'], target):
            continue
        changed = git(repo, 'diff', '--name-only', run['commit'], target).splitlines()
        for row in rows:
            check = next((c for c in run.get('checks', []) if c.get('feature') == row['name']), {})
            if check.get('result') != 'pass' or check.get('evidenceKind') != 'live':
                continue
            related = paths(row['code'])
            for spot in hot:
                method = spot['method'].split('`')[1]
                if method in row['shares'] or any(matches(p, related) for p in spot['paths']):
                    related += spot['paths']
            if run.get('features', {}).get(row['name']) == fingerprint(row) and not any(matches(f, related) for f in changed):
                result[row['name']] = run['date']
    return result


def choose(rows, hot, files, last, stale, random_count, outside, rng):
    touched = [r for r in rows if any(matches(f, paths(r['code'])) for f in files)]
    selected = {r['name']: dict(r, reason='changed') for r in touched}
    relevant_hot = [s for s in hot if any(matches(f, s['paths']) for f in files) or any(s['method'].split('`')[1] in r['shares'] for r in touched)]
    for row in rows:
        named = any(row['name'].lower() in r['shares'].lower() for r in touched)
        shared = any(set(paths(row['code'])) & set(paths(r['code'])) for r in touched)
        hotspot = any(any(matches(p, paths(row['code'])) for p in s['paths']) or s['method'].split('`')[1] in row['shares'] for s in relevant_hot)
        if row['name'] not in selected and (named or shared or hotspot):
            selected[row['name']] = dict(row, reason='neighbour')
    pool = [r for r in rows if r['name'] not in selected]
    rng.shuffle(pool)
    pool.sort(key=lambda r: last.get(r['name'], ''))
    for _ in range(stale):
        candidates = [r for r in pool if r['name'] not in selected]
        if not candidates:
            break
        part_counts = {part: sum(r['part'] == part and r['reason'] == 'stale' for r in selected.values()) for part in PARTS.values()}
        row = min(candidates, key=lambda r: (bool(last.get(r['name'])), part_counts[r['part']], last.get(r['name'], '')))
        selected[row['name']] = dict(row, reason='stale')
    pool = [r for r in rows if r['name'] not in selected]
    picks = rng.sample(pool, min(random_count, len(pool)))
    other = [r for r in pool if r['part'] not in outside and r not in picks]
    if outside and picks and all(r['part'] in outside for r in picks) and other:
        picks[-1] = rng.choice(other)
    for row in picks:
        selected[row['name']] = dict(row, reason='random')
    relevant_hot = [s for s in hot if s in relevant_hot or any(s['method'].split('`')[1] in r['shares'] or any(matches(p, paths(r['code'])) for p in s['paths']) for r in selected.values())]
    return [dict(r, lastVerified=last.get(r['name'])) for r in selected.values()], relevant_hot


def main():
    p = argparse.ArgumentParser(description='Return selected checks, code paths and patch targets; avoid loading the full catalog.')
    p.add_argument('--files', nargs='*', default=[])
    p.add_argument('--stale', type=int, default=None)
    p.add_argument('--random', type=int, default=None)
    p.add_argument('--outside', default='')
    p.add_argument('--part', default='')
    p.add_argument('--seed', type=int)
    p.add_argument('--mode', choices=['incremental', 'general', 'full'])
    p.add_argument('--repo')
    p.add_argument('--target', default='HEAD')
    p.add_argument('--store', type=pathlib.Path, default=STORE)
    p.add_argument('--json', action='store_true')
    p.add_argument('--output', type=pathlib.Path)
    a = p.parse_args()
    try:
        if a.mode and not a.repo:
            p.error('--mode requires --repo')
        if a.part and a.mode in ('incremental', 'full'):
            p.error('--part is for general or explicit selections; a partial review cannot advance the global change baseline')
        seed = a.seed if a.seed is not None else random.randrange(1_000_000)
        rows, hot = catalog()
        all_rows = rows
        if a.part:
            unknown = set(a.part.split(',')) - set(PARTS.values())
            if unknown:
                p.error('Unknown parts: ' + ', '.join(sorted(unknown)))
            rows = [r for r in rows if r['part'] in a.part.split(',')]
        files, base, source, commit, last = [f.replace('\\', '/') for f in a.files], None, None, None, {}
        if a.repo:
            commit = git(a.repo, 'rev-parse', a.target)
            store = read_store(a.store)
            last = last_verified(a.repo, commit, store, rows, hot)
            if a.mode in ('incremental', 'full'):
                base, source = baseline(a.repo, commit, store, a.mode == 'full')
                files = git(a.repo, 'diff', '--name-only', base, commit).splitlines()
        stale = a.stale if a.stale is not None else (6 if a.mode == 'general' else 0)
        count = a.random if a.random is not None else (2 if a.mode == 'general' else 3 if files else 0)
        if min(stale, count) < 0:
            p.error('Pick counts cannot be negative')
        outside = a.outside.split(',') if a.outside else sorted({r['part'] for r in rows if any(matches(f, paths(r['code'])) for f in files)})
        selected, spots = choose(rows, hot, files, last, stale, count, outside, random.Random(seed))
        tracked = set(git(a.repo, 'ls-tree', '-r', '--name-only', commit).splitlines()) if a.repo and a.mode in ('incremental', 'full') else None
        missing = [f for f in files if f.endswith('.cs') and (tracked is None or f in tracked) and not any(matches(f, paths(r['code'])) for r in all_rows)]
        plan = {'schema': 1, 'mode': a.mode or 'selection', 'commit': commit, 'baseline': base, 'baselineSource': source, 'seed': seed, 'changedFiles': files, 'unmappedFiles': missing, 'selected': selected, 'hotspots': spots, 'requiredChecks': ['Startup', 'Session smoke', 'Performance', 'Restoration'] if a.mode in ('general', 'full') else ['Changed-scope smoke', 'Restoration'] if selected else []}
        if a.output:
            a.output.parent.mkdir(parents=True, exist_ok=True)
            a.output.write_text(json.dumps(plan, indent=2), encoding='utf-8')
        if a.json:
            print(json.dumps(plan, ensure_ascii=True))
        else:
            print(f'Baseline: {base or "explicit files"} ({source or "selection"}); seed {seed}')
            for row in selected:
                print(f"- [{row['part']}; {row['reason']}] {row['name']}\n  Check: {row['check']}\n  Code: {row['code']}\n  Shares: {row['shares']}\n  Verified: {row['lastVerified'] or 'not verified at this code'}")
            if missing:
                print('Unmapped changed files: ' + ', '.join(missing))
            if spots:
                print('Shared patch targets: ' + json.dumps(spots))
    except (ValueError, OSError, KeyError, subprocess.CalledProcessError) as error:
        p.exit(1, f'{error}\n')


if __name__ == '__main__':
    main()
