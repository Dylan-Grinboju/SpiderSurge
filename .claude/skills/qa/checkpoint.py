import argparse
import contextlib
import datetime
import hashlib
import json
import pathlib
import os
import re
import subprocess

HERE = pathlib.Path(__file__).resolve().parent


@contextlib.contextmanager
def locked(path):
    with path.with_suffix('.lock').open('a+b') as handle:
        handle.write(b'\0')
        handle.flush()
        handle.seek(0)
        if os.name == 'nt':
            import msvcrt
            msvcrt.locking(handle.fileno(), msvcrt.LK_LOCK, 1)
        else:
            import fcntl
            fcntl.flock(handle, fcntl.LOCK_EX)
        try:
            yield
        finally:
            handle.seek(0)
            if os.name == 'nt':
                msvcrt.locking(handle.fileno(), msvcrt.LK_UNLCK, 1)
            else:
                fcntl.flock(handle, fcntl.LOCK_UN)


def git(repo, *args):
    root = str(pathlib.Path(repo).resolve()).replace('\\', '/')
    return subprocess.check_output(['git', '-c', f'safe.directory={root}', '-C', root, *args], text=True, encoding='utf-8').strip()


def default_store(repo):
    root = pathlib.Path(repo).resolve()
    try:
        common = pathlib.Path(git(root, 'rev-parse', '--path-format=absolute', '--git-common-dir'))
        if common.name == '.git':
            root = common.parent
    except (OSError, subprocess.CalledProcessError):
        pass
    return root / '.claude' / 'qa' / 'checkpoints.json'


STORE = default_store(HERE.parents[2])


def ancestor(repo, old, new):
    try:
        git(repo, 'merge-base', '--is-ancestor', old, new)
        return True
    except subprocess.CalledProcessError:
        return False


def read_store(path=STORE):
    path = pathlib.Path(path)
    if not path.exists():
        return {'schema': 1, 'runs': []}
    value = json.loads(path.read_text(encoding='utf-8'))
    if value.get('schema') != 1 or not isinstance(value.get('runs'), list):
        raise ValueError('Unsupported checkpoint store')
    if any(not isinstance(run, dict) or not isinstance(run.get('commit'), str) or not re.fullmatch('[0-9a-fA-F]{40,64}', run['commit']) for run in value['runs']):
        raise ValueError('Checkpoint runs must name a full Git commit hash')
    return value


def baseline(repo, target, store, full=False):
    if not full:
        valid = [run for run in store['runs'] if run.get('complete') and run.get('reviewComplete') and run.get('mode') in ('incremental', 'full') and ancestor(repo, run['commit'], target)]
        if valid:
            latest = min(valid, key=lambda run: int(git(repo, 'rev-list', '--count', run['commit'] + '..' + target)))
            return latest['commit'], 'completed QA checkpoint'
    return git(repo, 'describe', '--tags', '--abbrev=0', '--match', 'v*', target), 'release fallback'


def fingerprint(row):
    return hashlib.sha256(json.dumps({k: row[k] for k in ('name', 'check', 'code', 'shares')}, sort_keys=True).encode()).hexdigest()


def save_run(repo, plan, results, store_path, reviewed_files):
    commit = git(repo, 'rev-parse', 'HEAD')
    if plan.get('commit') != commit or git(repo, 'status', '--porcelain', '--untracked-files=no'):
        raise ValueError('Checkpoint requires the planned commit and a clean tracked working tree; commit fixes, regenerate the plan and retest')
    selected = {r['name']: r for r in plan['selected']}
    required = set(selected) | set(plan.get('requiredChecks', []))
    if not required or plan.get('mode') not in ('incremental', 'general', 'full', 'selection') or plan.get('unmappedFiles'):
        raise ValueError('A completed plan needs required checks, a supported mode and no unresolved unmapped files')
    checks = results.get('checks', [])
    if len({c['feature'] for c in checks}) != len(checks):
        raise ValueError('Duplicate check results')
    by_name = {c['feature']: c for c in checks}
    if any(c.get('result') != 'pass' for c in checks):
        raise ValueError('A failed check cannot advance coverage')
    for name in required:
        check = by_name.get(name, {})
        if check.get('result') != 'pass' or check.get('evidenceKind') != 'live' or check.get('commit') != commit:
            raise ValueError(f'Missing live pass at the planned commit: {name}')
        evidence = check.get('evidence', [])
        if not evidence or not all(pathlib.Path(p).is_absolute() and pathlib.Path(p).is_file() for p in evidence):
            raise ValueError(f'Check needs existing absolute evidence files: {name}')
    if results.get('restored') is not True or not results.get('environment'):
        raise ValueError('Record successful install restoration and the tested build/environment')
    changed = set(plan.get('changedFiles', []))
    reviewed = set(reviewed_files)
    review_complete = results.get('reviewComplete') is True
    if plan['mode'] in ('incremental', 'full', 'selection') and (not review_complete or not changed.issubset(reviewed)):
        raise ValueError('Review must cover every changed file before advancing the baseline')
    record = {'date': datetime.datetime.now(datetime.timezone.utc).isoformat(), 'commit': commit, 'mode': plan['mode'], 'baseline': plan.get('baseline'), 'complete': True, 'reviewComplete': review_complete, 'reviewedFiles': sorted(reviewed), 'environment': results['environment'], 'checks': checks, 'features': {name: fingerprint(row) for name, row in selected.items()}}
    path = pathlib.Path(store_path)
    path.parent.mkdir(parents=True, exist_ok=True)
    with locked(path):
        store = read_store(path)
        store['runs'].append(record)
        temporary = path.with_suffix('.tmp')
        temporary.write_text(json.dumps(store, indent=2), encoding='utf-8')
        temporary.replace(path)
    return record


def main():
    p = argparse.ArgumentParser(description='Record completed, evidenced QA coverage without treating failed or partial runs as a baseline.')
    p.add_argument('--repo', required=True)
    p.add_argument('--plan', type=pathlib.Path, required=True)
    p.add_argument('--results', type=pathlib.Path, required=True)
    p.add_argument('--store', type=pathlib.Path, default=STORE)
    p.add_argument('--reviewed-files', type=pathlib.Path, required=True, help='JSON array of reviewed repository-relative paths')
    a = p.parse_args()
    try:
        record = save_run(a.repo, json.loads(a.plan.read_text(encoding='utf-8')), json.loads(a.results.read_text(encoding='utf-8')), a.store, json.loads(a.reviewed_files.read_text(encoding='utf-8')))
        print(json.dumps({'commit': record['commit'], 'checks': len(record['checks']), 'store': str(a.store)}))
    except (ValueError, KeyError, OSError, subprocess.CalledProcessError) as error:
        p.exit(1, f'{error}\n')


if __name__ == '__main__':
    main()
