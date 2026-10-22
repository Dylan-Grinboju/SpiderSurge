import contextlib
import copy
import io
import json
import pathlib
import random
import subprocess
import tempfile
import unittest

import checkpoint
import pick
import run_recipe


class CoverageTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.root = pathlib.Path(self.temp.name)
        subprocess.run(['git', 'init', '-q', str(self.root)], check=True)
        self.write('feature.cs', 'old')
        self.write('shared.cs', 'old')
        self.first = self.commit()
        checkpoint.git(self.root, 'tag', 'v0.1.0')
        self.row = {'name': 'Feature', 'part': 'surge', 'code': '`feature.cs`', 'shares': '`Hot.Run`', 'check': 'Real input'}

    def tearDown(self):
        self.temp.cleanup()

    def write(self, name, value):
        (self.root / name).write_text(value, encoding='utf-8')

    def commit(self):
        checkpoint.git(self.root, 'add', '.')
        checkpoint.git(self.root, '-c', 'user.name=QA Test', '-c', 'user.email=qa@example.invalid', 'commit', '-qm', 'fixture')
        return checkpoint.git(self.root, 'rev-parse', 'HEAD')

    def plan(self):
        return {'commit': self.first, 'mode': 'incremental', 'selected': [self.row], 'changedFiles': [], 'requiredChecks': ['Restoration'], 'unmappedFiles': []}

    def results(self):
        evidence = self.root / 'evidence.json'
        evidence.write_text('{}')
        return {'restored': True, 'reviewComplete': True, 'environment': {'build': 'fixture'}, 'checks': [{'feature': name, 'result': 'pass', 'commit': self.first, 'evidenceKind': 'live', 'evidence': [str(evidence)]} for name in ('Feature', 'Restoration')]}

    def test_missing_failed_and_code_only_checks_do_not_create_checkpoint(self):
        for mutate in [lambda r: r['checks'].pop(), lambda r: r['checks'][0].update(result='fail'), lambda r: r['checks'][0].update(evidenceKind='code'), lambda r: r.update(restored=False)]:
            results = self.results()
            mutate(results)
            with self.assertRaises(ValueError):
                checkpoint.save_run(self.root, self.plan(), results, self.root / 'store.json', [])
            self.assertFalse((self.root / 'store.json').exists())

    def test_dirty_code_cannot_be_recorded(self):
        self.write('feature.cs', 'dirty')
        with self.assertRaises(ValueError):
            checkpoint.save_run(self.root, self.plan(), self.results(), self.root / 'store.json', [])

    def test_completed_coverage_is_reused_and_shared_patch_changes_invalidate_it(self):
        plan, result = self.plan(), self.results()
        checkpoint.save_run(self.root, plan, result, self.root / '.qa/store.json', [])
        store = checkpoint.read_store(self.root / '.qa/store.json')
        hot = [{'method': '`Hot.Run`', 'paths': ['shared.cs']}]
        self.assertIn('Feature', pick.last_verified(self.root, self.first, store, [self.row], hot))
        self.write('shared.cs', 'changed')
        second = self.commit()
        self.assertNotIn('Feature', pick.last_verified(self.root, second, store, [self.row], hot))
        self.assertEqual(checkpoint.baseline(self.root, second, store)[0], self.first)

    def test_general_sample_is_not_a_global_baseline(self):
        self.write('feature.cs', 'new')
        second = self.commit()
        store = {'runs': [{'commit': second, 'complete': True, 'reviewComplete': True, 'mode': 'general'}]}
        self.assertEqual(checkpoint.baseline(self.root, second, store)[0], 'v0.1.0')

    def test_explicit_feature_selection_records_coverage_without_a_global_baseline(self):
        plan = self.plan()
        plan['mode'] = 'selection'
        checkpoint.save_run(self.root, plan, self.results(), self.root / '.qa/store.json', [])
        store = checkpoint.read_store(self.root / '.qa/store.json')
        self.assertEqual(checkpoint.baseline(self.root, self.first, store)[0], 'v0.1.0')
        self.assertIn('Feature', pick.last_verified(self.root, self.first, store, [self.row], []))

    def test_late_record_of_older_run_does_not_move_baseline_backwards(self):
        self.write('feature.cs', 'new')
        second = self.commit()
        store = {'runs': [dict(commit=c, complete=True, reviewComplete=True, mode='incremental') for c in [second, self.first]]}
        self.assertEqual(checkpoint.baseline(self.root, second, store)[0], second)

    def test_picker_includes_shared_patch_neighbour_and_code_locations(self):
        rows = [dict(self.row, code='`surge/feature.cs`'), dict(self.row, name='Neighbour', code='`stats/shared.cs`')]
        hot = [{'method': '`Hot.Run`', 'paths': ['surge/feature.cs', 'stats/shared.cs']}]
        selected, spots = pick.choose(rows, hot, ['surge/feature.cs'], {}, 0, 0, [], random.Random(1))
        self.assertEqual([r['reason'] for r in selected], ['changed', 'neighbour'])
        self.assertEqual(len(spots), 1)
        self.assertIn('code', selected[1])

    def test_catalog_cells_keep_conditions_with_pipes_in_one_column(self):
        self.assertEqual(pick.cells('| Feature | `dead:0|ticks:60` | `core/Test.cs` | Shared |'), ['Feature', '`dead:0|ticks:60`', '`core/Test.cs`', 'Shared'])

    def test_linked_worktree_uses_main_checkout_coverage_store(self):
        worktree = self.root / 'linked'
        checkpoint.git(self.root, 'worktree', 'add', '--detach', str(worktree), self.first)
        self.assertEqual(checkpoint.default_store(worktree), self.root / '.claude/qa/checkpoints.json')


class FakeClient:
    owner = 'test-owner'

    def __init__(self, responses=None, owner='test-owner', log_lines=None, capabilities=()):
        self.responses = responses or {}
        self.lock_owner, self.log_lines, self.capabilities = owner, log_lines or [], capabilities
        self.calls = []

    def get(self, request):
        self.calls.append(request)
        if request == '/api/ping':
            return 200, {'lock': {'owner': self.lock_owner}, 'game': {'ready': True}, 'capabilities': self.capabilities}
        if request.startswith('/api/log'):
            return 200, {'next': 8, 'lines': self.log_lines if 'since=' in request else []}
        return self.responses.get(request, (200, {'value': 3}))


class RecipeTests(unittest.TestCase):
    def execute(self, client, steps=None):
        recipe = {'tests': [{'feature': 'Feature', 'steps': steps or [{'request': '/api/value', 'assert': [{'path': 'value', 'value': 3}]}]}]}
        with tempfile.TemporaryDirectory() as temp, contextlib.redirect_stdout(io.StringIO()):
            ok = run_recipe.run(recipe, client, pathlib.Path(temp) / 'evidence', 'fixture')
            result = json.loads((pathlib.Path(temp) / 'evidence/results.json').read_text())
        return ok, result

    def test_success_saves_assertions_but_does_not_certify_restoration(self):
        ok, result = self.execute(FakeClient())
        self.assertTrue(ok)
        self.assertFalse(result['restored'])
        self.assertEqual(result['checks'][0]['assertions'][0]['actual'], 3)

    def test_other_owner_refuses_before_actions(self):
        client = FakeClient(owner='other')
        with self.assertRaises(ValueError):
            self.execute(client)
        self.assertEqual(client.calls, ['/api/ping'])

    def test_nested_unmet_condition_is_failure(self):
        client = FakeClient({'/api/value': (200, {'results': [{'status': 200, 'body': {'met': False}}]})})
        ok, result = self.execute(client)
        self.assertFalse(ok)
        self.assertEqual(result['checks'][0]['result'], 'fail')

    def test_new_log_warning_fails_successful_assertion(self):
        ok, _ = self.execute(FakeClient(log_lines=[{'message': 'warning'}]))
        self.assertFalse(ok)

    def test_unsupported_filter_is_not_silently_ignored(self):
        client = FakeClient()
        with self.assertRaisesRegex(ValueError, 'player-fields'):
            self.execute(client, [{'request': '/api/players?fields=velocity', 'assert': [{'path': 'value', 'value': 3}]}])
        self.assertEqual(client.calls, ['/api/ping'])

    def test_capture_is_url_encoded_and_assertions_keep_numeric_values(self):
        client = FakeClient({'/api/first': (200, {'id': 'a,b'}), '/api/second?id=a%2Cb': (200, {'value': 3})})
        ok, _ = self.execute(client, [{'request': '/api/first', 'capture': {'id': 'id'}}, {'request': '/api/second?id=${id}', 'assert': [{'path': 'value', 'value': 3}]}])
        self.assertTrue(ok)
        self.assertIn('/api/second?id=a%2Cb', client.calls)


if __name__ == '__main__':
    unittest.main()
