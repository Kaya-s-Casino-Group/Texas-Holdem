"""Real disposable Git repos + recorded GitHub fixtures; never update the working project.
Python 3 + Git for tests. Bash cases use jq to evaluate the script's actual gh --jq
query. PowerShell cases can use Windows PowerShell 5.1 or SYNC_POWERSHELL.
"""
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import unittest

SOURCE = Path(__file__).resolve().parents[3]
URL = 'https://github.com/Kaya-s-Casino-Group/BattleToolkit.git'
POWERSHELL = os.environ.get('SYNC_POWERSHELL') or shutil.which('powershell') or shutil.which('pwsh')
JOB_NAMES = ['rules-and-host (windows-latest)', 'rules-and-host (macos-latest)',
             'first-launch (windows-latest)', 'first-launch (macos-latest)']
FAKE_GH = r'''import json, os, pathlib, subprocess, sys
args=sys.argv[1:]; path=pathlib.Path(os.environ['SYNC_TEST_STATE']); state=json.loads(path.read_text())
with open(os.environ['SYNC_TEST_CALLS'],'a') as log: log.write(json.dumps(args)+'\n')
if args[0]=='auth': sys.exit(state.get('auth_code',0))
if state.get('api_error'): print('fixture network failure',file=sys.stderr);sys.exit(1)
endpoint=next(a for a in args if a.startswith('repos/'))
if '/workflows/' in endpoint:
    runs=state['runs']
    if '&head_sha=' in endpoint:
        sha=endpoint.split('&head_sha=')[1]
        runs=[r for r in runs if r['head_sha']==sha]
        if state.get('stale'):
            runs=[dict(r,run_attempt=r['run_attempt']+1,status='in_progress',conclusion=None) for r in runs]
        if state.get('dirty_during_recheck'):
            pathlib.Path(state['dirty_during_recheck']).write_text('new local work')
    data={'workflow_runs':runs}
else:
    parts=endpoint.split('/'); key=parts[parts.index('runs')+1]+':'+parts[parts.index('attempts')+1]
    jobs=state['jobs'].get(key,[]);data={'jobs':jobs,'total_count':len(jobs)}
if '--jq' in args:
    query=args[args.index('--jq')+1]
    result=subprocess.run([os.environ['SYNC_TEST_JQ'],'-r',query],input=json.dumps(data),text=True,capture_output=True)
    sys.stdout.write(result.stdout);sys.stderr.write(result.stderr);sys.exit(result.returncode)
print(json.dumps(data))
'''


class RepositoryFixture:
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix='battle sync 策划 ')
        self.base = Path(self.temp.name).resolve()
        self.root = self.base / 'Unity 主工程'
        self.seed = self.base / 'seed'
        self.remote = self.base / 'remote.git'
        self.state_path = self.base / 'github.json'
        self.calls = self.base / 'calls.jsonl'
        self.env = dict(os.environ, GIT_CONFIG_NOSYSTEM='1', GIT_CONFIG_GLOBAL=str(self.base / 'gitconfig'),
                        GIT_TERMINAL_PROMPT='0', SYNC_TEST_STATE=str(self.state_path), SYNC_TEST_CALLS=str(self.calls),
                        SYNC_TEST_JQ=shutil.which('jq') or '')
        self.git(self.base, 'config', '--global', 'user.name', 'Sync Tests')
        self.git(self.base, 'config', '--global', 'user.email', 'sync-tests@example.invalid')
        self.git(self.base, 'config', '--global', 'protocol.file.allow', 'always')
        self.git(self.base, 'config', '--global', 'url.' + self.remote.as_uri() + '.insteadOf', URL)
        self.git(self.base, 'init', '--bare', '-b', 'main', str(self.remote))
        self.git(self.base, 'init', '-b', 'main', str(self.seed))
        (self.seed / 'rules.txt').write_text('before')
        (self.seed / '.gitignore').write_text('local-only/\n')
        self.git(self.seed, 'add', '.')
        self.git(self.seed, 'commit', '-m', '旧规则')
        self.before = self.git(self.seed, 'rev-parse', 'HEAD')
        (self.seed / 'rules.txt').write_text('after')
        (self.seed / 'local-only').mkdir()
        (self.seed / 'local-only' / 'cache.txt').write_text('tracked in target')
        self.git(self.seed, 'add', '-f', 'rules.txt', 'local-only/cache.txt')
        self.git(self.seed, 'commit', '-m', '新规则 中文')
        self.after = self.git(self.seed, 'rev-parse', 'HEAD')
        self.git(self.seed, 'remote', 'add', 'origin', URL)
        self.git(self.seed, 'push', 'origin', 'main')
        self.git(self.base, 'init', '-b', 'dev', str(self.root))
        self.git(self.root, 'submodule', 'add', URL, 'External/BattleToolkit')
        self.child = self.root / 'External/BattleToolkit'
        self.git(self.child, 'switch', '--detach', self.before)
        script_dir = self.root / 'Tools/BattleToolkitSync'
        script_dir.mkdir(parents=True)
        for name in ['sync.sh', 'sync.ps1']:
            shutil.copy2(SOURCE / 'Tools/BattleToolkitSync' / name, script_dir / name)
        for name in ['sync-battle-toolkit.command', 'sync-battle-toolkit.cmd']:
            shutil.copy2(SOURCE / name, self.root / name)
        (self.root / 'scene.txt').write_text('scene')
        self.git(self.root, 'add', '.')
        self.git(self.root, 'commit', '-m', '使用旧规则')
        self.parent_head = self.git(self.root, 'rev-parse', 'HEAD')
        fake_bin = self.base / 'bin'; fake_bin.mkdir()
        (fake_bin / 'fake_gh.py').write_text(FAKE_GH)
        if os.name == 'nt':
            (fake_bin / 'gh.cmd').write_text('@echo off\n"' + sys.executable + '" "%~dp0fake_gh.py" %*\n')
        else:
            (fake_bin / 'gh').write_text('#!' + sys.executable + '\n' + FAKE_GH)
            (fake_bin / 'gh').chmod(0o755)
        self.env['PATH'] = str(fake_bin) + os.pathsep + self.env['PATH']
        self.state = {'runs': [self.run_record(self.after, 100), self.run_record(self.before, 90)],
                      'jobs': {'100:1': self.jobs(), '90:1': self.jobs()}}
        self.save_state()

    def tearDown(self):
        self.temp.cleanup()

    def git(self, directory, *args):
        p = subprocess.run(['git', '-C', str(directory), *args], env=self.env, text=True, encoding='utf-8', capture_output=True)
        if p.returncode: raise AssertionError(p.stderr)
        return p.stdout.strip()

    @staticmethod
    def jobs():
        return [dict(name=name,status='completed',conclusion='success') for name in JOB_NAMES]

    @staticmethod
    def run_record(sha, number, **changes):
        return dict(dict(head_sha=sha,id=number,run_number=number,run_attempt=1,status='completed',conclusion='success'),**changes)

    def save_state(self):
        self.state_path.write_text(json.dumps(self.state))

    def execute(self, *args, stdin=''):
        self.save_state()
        if self.flavor == 'bash':
            command = ['/bin/bash', str(self.root / 'sync-battle-toolkit.command'), *args]
        else:
            command = [POWERSHELL, '-NoLogo', '-NoProfile', '-NonInteractive', '-File',
                       str(self.root / 'Tools/BattleToolkitSync/sync.ps1'), *args]
            # Read-Host is intentionally exercised with stdin in normal interactive mode.
            command.remove('-NonInteractive')
        return subprocess.run(command, input=stdin, env=self.env, text=True, encoding='utf-8', capture_output=True, timeout=45)

    def unchanged(self):
        self.assertEqual(self.git(self.child, 'rev-parse', 'HEAD'), self.before)
        self.assertEqual(self.git(self.root, 'rev-parse', 'HEAD'), self.parent_head)

    def test_clean_check_does_not_contact_github(self):
        result = self.execute('--check')
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
        self.assertFalse(self.calls.exists())
        self.unchanged()

    def test_update_leaves_only_unstaged_pointer_and_no_commit(self):
        result = self.execute(stdin='1\nUPDATE\n')
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
        self.assertEqual(self.git(self.child, 'rev-parse', 'HEAD'), self.after)
        self.assertEqual(self.git(self.root, 'rev-parse', 'HEAD'), self.parent_head)
        self.assertEqual(self.git(self.root, 'diff', '--cached', '--name-only'), '')
        self.assertEqual(self.git(self.root, 'diff', '--name-only'), 'External/BattleToolkit')
        self.assertEqual(self.git(self.child, 'status', '--porcelain'), '')

    def test_parent_unstaged_edit_blocks_before_network(self):
        (self.root / 'scene.txt').write_text('my scene')
        result = self.execute(stdin='1\nUPDATE\n')
        self.assertNotEqual(result.returncode, 0)
        self.assertEqual((self.root / 'scene.txt').read_text(), 'my scene')
        self.assertFalse(self.calls.exists()); self.unchanged()

    def test_parent_staged_edit_is_preserved(self):
        (self.root / 'scene.txt').write_text('staged scene')
        self.git(self.root, 'add', 'scene.txt')
        staged = self.git(self.root, 'diff', '--cached')
        self.assertNotEqual(self.execute('--list').returncode, 0)
        self.assertEqual(self.git(self.root, 'diff', '--cached'), staged); self.unchanged()

    def test_parent_untracked_file_blocks(self):
        (self.root / 'new scene.txt').write_text('keep')
        self.assertNotEqual(self.execute('--list').returncode, 0)
        self.assertTrue((self.root / 'new scene.txt').exists()); self.unchanged()

    def test_child_dirty_and_untracked_are_preserved(self):
        (self.child / 'rules.txt').write_text('local rules')
        (self.child / 'new.xml').write_text('local xml')
        self.assertNotEqual(self.execute('--list').returncode, 0)
        self.assertEqual((self.child / 'rules.txt').read_text(), 'local rules')
        self.assertEqual((self.child / 'new.xml').read_text(), 'local xml'); self.unchanged()

    def test_existing_uncommitted_pointer_blocks(self):
        self.git(self.child, 'switch', '--detach', self.after)
        self.assertNotEqual(self.execute('--list').returncode, 0)
        self.assertEqual(self.git(self.child, 'rev-parse', 'HEAD'), self.after)
        self.assertFalse(self.calls.exists())

    def test_merge_marker_blocks_even_with_clean_status(self):
        gitdir = Path(self.git(self.child, 'rev-parse', '--absolute-git-dir'))
        (gitdir / 'MERGE_HEAD').write_text(self.after + '\n')
        self.assertNotEqual(self.execute('--check').returncode, 0); self.unchanged()

    def test_assume_unchanged_edit_blocks(self):
        self.git(self.child, 'update-index', '--assume-unchanged', 'rules.txt')
        (self.child / 'rules.txt').write_text('hidden local edit')
        self.assertNotEqual(self.execute('--list').returncode, 0)
        self.assertEqual((self.child / 'rules.txt').read_text(), 'hidden local edit'); self.unchanged()

    def test_ignored_file_collision_is_not_overwritten(self):
        (self.child / 'local-only').mkdir()
        (self.child / 'local-only/cache.txt').write_text('precious ignored work')
        result = self.execute(stdin='1\nUPDATE\n')
        self.assertNotEqual(result.returncode, 0, result.stdout + result.stderr)
        self.assertEqual((self.child / 'local-only/cache.txt').read_text(), 'precious ignored work')
        self.unchanged()

    def test_cancel_selection(self):
        self.assertEqual(self.execute(stdin='0\n').returncode, 0); self.unchanged()

    def test_cancel_confirmation(self):
        self.assertEqual(self.execute(stdin='1\nno\n').returncode, 0); self.unchanged()

    def test_list_does_not_switch(self):
        result = self.execute('--list')
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
        self.assertIn(self.after[:12], result.stdout); self.unchanged()

    def test_newer_failed_run_blocks_older_success_for_same_sha(self):
        self.state['runs'].append(self.run_record(self.after,101,conclusion='failure'))
        result = self.execute('--list')
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
        self.assertNotIn(self.after[:12],result.stdout); self.unchanged()

    def test_newer_running_run_blocks_older_success_for_same_sha(self):
        self.state['runs'].append(self.run_record(self.after,101,status='in_progress',conclusion=None))
        result = self.execute('--list')
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
        self.assertNotIn(self.after[:12],result.stdout); self.unchanged()

    def test_skipped_or_missing_jobs_exclude_candidate(self):
        self.state['jobs']['100:1'][0]['conclusion'] = 'skipped'
        self.state['jobs']['90:1'] = self.jobs()[:-1]
        result = self.execute('--list')
        self.assertNotEqual(result.returncode, 0); self.unchanged()

    def test_ci_rerun_during_confirmation_aborts(self):
        self.state['stale'] = True
        result = self.execute(stdin='1\nUPDATE\n')
        self.assertNotEqual(result.returncode, 0); self.unchanged()

    def test_new_local_edit_during_ci_recheck_aborts(self):
        self.state['dirty_during_recheck'] = str(self.root / 'scene.txt')
        result = self.execute(stdin='1\nUPDATE\n')
        self.assertNotEqual(result.returncode, 0)
        self.assertEqual((self.root / 'scene.txt').read_text(), 'new local work'); self.unchanged()

    def test_no_green_run_does_not_update(self):
        self.state['runs'] = []
        self.assertNotEqual(self.execute('--list').returncode, 0); self.unchanged()

    def test_unreachable_sha_not_offered(self):
        self.state['runs'].insert(0,self.run_record('e'*40,200))
        result = self.execute('--list')
        self.assertEqual(result.returncode, 0, result.stdout + result.stderr)
        self.assertNotIn('e'*12,result.stdout); self.unchanged()

    def test_wrong_remote_blocks(self):
        self.git(self.child, 'remote', 'set-url', 'origin', 'https://example.invalid/other.git')
        self.assertNotEqual(self.execute('--list').returncode, 0)
        self.assertFalse(self.calls.exists()); self.unchanged()

    def test_api_error_does_not_switch(self):
        self.state['api_error'] = True
        self.assertNotEqual(self.execute('--list').returncode, 0); self.unchanged()

    def test_auth_error_does_not_fetch(self):
        self.state['auth_code'] = 1
        self.assertNotEqual(self.execute('--list').returncode, 0); self.unchanged()
        self.assertEqual(len(self.calls.read_text().splitlines()),1)

    def test_uninitialized_submodule_gives_instruction(self):
        self.git(self.root, 'submodule', 'deinit', '-f', 'External/BattleToolkit')
        result = self.execute('--check')
        self.assertNotEqual(result.returncode, 0)
        self.assertIn('git submodule update --init --recursive',result.stdout+result.stderr)
        self.assertFalse((self.child / '.git').exists())


@unittest.skipUnless(os.name != 'nt' and shutil.which('jq'), 'Bash tests need macOS/Linux + jq')
class BashSyncTests(RepositoryFixture, unittest.TestCase):
    flavor = 'bash'


@unittest.skipUnless(POWERSHELL, 'Set SYNC_POWERSHELL or install PowerShell to test ps1')
class PowerShellSyncTests(RepositoryFixture, unittest.TestCase):
    flavor = 'powershell'


if __name__ == '__main__':
    unittest.main()
