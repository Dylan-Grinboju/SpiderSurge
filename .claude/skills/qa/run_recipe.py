import argparse
import json
import pathlib
import re
import urllib.error
import urllib.parse
import urllib.request


def read_path(value, path):
    for part in path.removeprefix('$.').split('.') if path not in ('', '$') else []:
        value = value[int(part)] if isinstance(value, list) else value[part]
    return value


def problem(value, warnings=True):
    if not isinstance(value, dict):
        return None
    for key in ('error', 'listError'):
        if value.get(key):
            return {key: value[key]}
    for key in ('ok', 'met', 'untilMet'):
        if value.get(key) is False:
            return {key: False}
    if value.get('interrupted'):
        return {'interrupted': True}
    if warnings and value.get('warnings'):
        return {'warnings': value['warnings']}
    for entry in value.get('results', []):
        if entry.get('status', 200) >= 400:
            return entry
        failure = problem(entry.get('body'), warnings)
        if failure:
            return {'request': entry.get('request', entry.get('index')), 'failure': failure}
    return None


def assertion(value, spec):
    actual = read_path(value, spec['path'])
    expected, op = spec.get('value'), spec.get('op', 'eq')
    operations = {'eq': lambda: actual == expected, 'ne': lambda: actual != expected, 'gt': lambda: actual > expected, 'gte': lambda: actual >= expected, 'lt': lambda: actual < expected, 'lte': lambda: actual <= expected, 'contains': lambda: expected in actual, 'length': lambda: len(actual) == expected}
    if op not in operations:
        raise ValueError(f'Unknown assertion operation: {op}')
    if not operations[op]():
        raise ValueError(f"{spec['path']}: expected {op} {expected!r}; actual {actual!r}")
    return {'path': spec['path'], 'op': op, 'expected': expected, 'actual': actual}


class Client:
    def __init__(self, url, owner, timeout=60):
        parsed = urllib.parse.urlsplit(url)
        if parsed.scheme != 'http' or parsed.hostname not in ('127.0.0.1', 'localhost', '::1') or parsed.path not in ('', '/') or parsed.query:
            raise ValueError('Use a loopback HTTP inspector URL')
        self.url, self.owner, self.timeout = url.rstrip('/'), owner, timeout

    def get(self, path):
        parsed = urllib.parse.urlsplit(path)
        if not path.startswith('/api/') or parsed.scheme or parsed.netloc or parsed.fragment:
            raise ValueError('Requests must be /api/ paths on the selected inspector')
        query = urllib.parse.parse_qsl(parsed.query, keep_blank_values=True)
        if any(k.lower() == 'owner' for k, _ in query):
            raise ValueError('Recipe requests cannot override the owner')
        target = self.url + parsed.path + '?' + urllib.parse.urlencode(query + [('owner', self.owner)])
        try:
            with urllib.request.urlopen(target, timeout=self.timeout) as response:
                return response.status, json.loads(response.read())
        except urllib.error.HTTPError as error:
            return error.code, json.loads(error.read())


def run(recipe, client, output, commit):
    tests = recipe.get('tests', [])
    if not tests or len({t['feature'] for t in tests}) != len(tests):
        raise ValueError('Provide tests with unique feature names')
    for test in tests:
        if not test.get('steps') or not any(s.get('assert') for s in test['steps']):
            raise ValueError(f"Test needs observable assertions: {test['feature']}")
    output = pathlib.Path(output).resolve()
    output.mkdir(parents=True, exist_ok=False)
    status, ping = client.get('/api/ping')
    (output / 'ping.json').write_text(json.dumps(ping, indent=2), encoding='utf-8')
    if status != 200 or (ping.get('lock') or {}).get('owner') != client.owner:
        raise ValueError('Acquire the game lock as this owner before running recipes')
    capabilities = set(ping.get('capabilities', []))
    for test in tests:
        for step in test['steps']:
            parsed = urllib.parse.urlsplit(step['request'])
            query = urllib.parse.parse_qs(parsed.query)
            required = []
            if parsed.path == '/api/players' and any(k in query for k in ('player', 'fields')):
                required.append('player-fields')
            if parsed.path in ('/api/wait', '/api/time/wait') and 'players' in query:
                required.append('wait-players')
            if parsed.path == '/api/batch' and (query.get('bodies') == ['compact'] or 'capture' in query):
                required.append('compact-batch')
            if parsed.path.startswith('/api/ui/') and ('compact' in query or 'listMode' in query):
                required.append('ui-selected')
            if set(required) - capabilities:
                raise ValueError('Installed inspector does not support: ' + ', '.join(sorted(set(required) - capabilities)))
    _, logs = client.get('/api/log?level=warning&noise=0&limit=1')
    cursor = logs.get('next')
    if not isinstance(cursor, int):
        raise ValueError('Inspector did not return a log cursor')
    checks = []
    for index, test in enumerate(tests):
        variables, proofs, evidence = {}, [], []
        failure = None
        for step_index, step in enumerate(test['steps']):
            file = output / f'{index:03}-{step_index:03}.json'
            try:
                def substitute(match):
                    value = variables[match.group(1)]
                    text = json.dumps(value, separators=(',', ':')) if not isinstance(value, str) else value
                    return urllib.parse.quote(text, safe='')
                request = re.sub(r'\$\{([A-Za-z][A-Za-z0-9_]*)\}', substitute, step['request'])
                status, value = client.get(request)
                file.write_text(json.dumps({'request': request, 'status': status, 'body': value}, indent=2), encoding='utf-8')
                evidence.append(str(file))
                failure = problem(value, not step.get('allowWarnings', False))
                if status >= 400 or failure:
                    raise ValueError(f'Inspector failed: {status}; {failure}')
                proofs.extend(assertion(value, a) for a in step.get('assert', []))
                if step.get('clearVariables'):
                    variables.clear()
                variables.update({name: read_path(value, path) for name, path in step.get('capture', {}).items()})
            except (ValueError, KeyError, IndexError, TypeError, OSError) as error:
                failure = str(error)
                break
        log_file = output / f'{index:03}-log.json'
        try:
            status, logs = client.get(f'/api/log?level=warning&noise=0&since={cursor}&limit=1000')
            log_file.write_text(json.dumps(logs, indent=2), encoding='utf-8')
            evidence.append(str(log_file))
            if status >= 400 or problem(logs) or logs.get('lines') or logs.get('truncated') or not isinstance(logs.get('next'), int):
                failure = failure or 'New warning/error log entries, truncated logs, or failed log check; see evidence'
            cursor = logs.get('next', cursor)
        except (OSError, ValueError) as error:
            failure = failure or str(error)
        check = {'feature': test['feature'], 'result': 'fail' if failure else 'pass', 'evidenceKind': 'live', 'commit': commit, 'evidence': evidence, 'assertions': proofs, 'failure': failure}
        checks.append(check)
        (output / 'state.json').write_text(json.dumps({'commit': commit, 'owner': client.owner, 'completed': len(checks), 'pending': [t['feature'] for t in tests[index + 1:]], 'logCursor': cursor, 'results': 'results.json'}, indent=2), encoding='utf-8')
        print(json.dumps({'feature': test['feature'], 'result': check['result'], 'assertions': len(proofs), 'failure': str(failure)[:700] if failure else None, 'evidence': str(output)}))
        if failure:
            break
    result = {'commit': commit, 'checks': checks, 'environment': {'inspector': ping, 'recipe': recipe.get('name')}, 'restored': False, 'reviewComplete': False}
    (output / 'results.json').write_text(json.dumps(result, indent=2), encoding='utf-8')
    return len(checks) == len(tests) and all(c['result'] == 'pass' for c in checks)


def main():
    p = argparse.ArgumentParser(description='Run observable inspector checks under an existing lock; keep full evidence on disk and print compact results.')
    p.add_argument('recipe', type=pathlib.Path)
    p.add_argument('--owner', required=True)
    p.add_argument('--commit', required=True)
    p.add_argument('--output', type=pathlib.Path, required=True)
    p.add_argument('--url', default='http://127.0.0.1:21712')
    a = p.parse_args()
    try:
        ok = run(json.loads(a.recipe.read_text(encoding='utf-8')), Client(a.url, a.owner), a.output, a.commit)
        raise SystemExit(0 if ok else 1)
    except (ValueError, OSError, KeyError) as error:
        p.exit(1, f'{error}\n')


if __name__ == '__main__':
    main()
