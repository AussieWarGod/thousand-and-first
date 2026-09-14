import unittest
import sys
import json
import subprocess
import tempfile
from pathlib import Path
sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import scenario_advance_check as check


class AdvanceCheckTests(unittest.TestCase):
    def fixture(self, requested=(1200, 3600, 1200), extra=1):
        rows = []
        for index, count in enumerate(requested):
            guard = 'founderCell=22,12; zone=JoppaWorld.8.22.1.1.10; '
            suffix = '; walk=none; scope=every-scripted-advance'
            rows += [('advance-guard', 'OK', 'start; ' + guard + 'guard=ignoreme-armed; ignoreMe=True' + suffix),
                     ('advance', 'OK', f'Advancing {count} game turn(s) with no player input. A advance-progress row lands every 100 turns and the script resumes at its next verb when the wait completes.')]
            rows += [('advance-progress', 'OK', f'{n} of {count} turn(s) elapsed') for n in range(100, count + 1, 100)]
            rows += [('advance-guard', 'OK', 'end; ' + guard + 'guard=ignoreme-released; ignoreMe=False' + suffix),
                     ('advance-complete', 'OK', f'{count + (extra if index == 2 else 0)} turn(s) elapsed of {count} requested')]
        return rows

    def test_real_delayed_action_shape_preserves_1201_for_1200(self):
        for extra in (0, 1, 7):
            waits = check.judge(self.fixture(extra=extra), (1200, 3600, 1200))
            self.assertEqual(6000 + extra, sum(w['elapsed'] for w in waits))
            self.assertEqual([1200, 3600, 1200], [w['requested'] for w in waits])

    def test_every_missing_repeated_refused_or_reordered_wait_row_fails(self):
        original = self.fixture()
        for index in range(len(original)):
            for mode in ('remove', 'duplicate', 'refuse', 'swap'):
                rows = list(original)
                if mode == 'remove': del rows[index]
                elif mode == 'duplicate': rows.insert(index, rows[index])
                elif mode == 'refuse': rows[index] = rows[index][0], 'REFUSED', rows[index][2]
                elif index == 0: continue
                else: rows[index - 1], rows[index] = rows[index], rows[index - 1]
                with self.subTest(index=index, mode=mode), self.assertRaises(ValueError):
                    check.judge(rows, (1200, 3600, 1200))

    def test_shortened_wrong_requested_or_malformed_actual_wait_fails(self):
        for detail in ('1199 turn(s) elapsed of 1200 requested', '1201 turn(s) elapsed of 1201 requested',
                       '01201 turn(s) elapsed of 1200 requested', '-1 turn(s) elapsed of 1200 requested'):
            rows = self.fixture()
            rows[-1] = 'advance-complete', 'OK', detail
            with self.subTest(detail=detail), self.assertRaises(ValueError):
                check.judge(rows, (1200, 3600, 1200))

    def test_empty_zero_negative_or_excessive_expected_waits_fail(self):
        for requested in ((), (0,), (-1,), (10001,), (True,)):
            with self.subTest(requested=requested), self.assertRaises(ValueError):
                check.judge([], requested)

    def test_guard_cell_zone_and_restoration_drift_fail(self):
        for old, new in (('22,12', '23,12'), ('JoppaWorld.8.22.1.1.10', 'elsewhere'),
                         ('ignoreMe=False', 'ignoreMe=True'), ('walk=none', 'walk=west')):
            rows = self.fixture()
            rows[-2] = rows[-2][0], 'OK', rows[-2][2].replace(old, new)
            with self.subTest(old=old), self.assertRaises(ValueError): check.judge(rows, (1200, 3600, 1200))

    def test_cli_records_true_elapsed_and_refuses_incomplete_or_reused_output(self):
        for complete in (True, False):
            with tempfile.TemporaryDirectory() as folder:
                journal, output = Path(folder) / 'journal.tsv', Path(folder) / 'result.json'
                rows = self.fixture()
                if complete: rows.append(('SCRIPT-COMPLETE', 'OK', 'done'))
                journal.write_text(''.join('2026-09-14T00:00:00Z\t' + '\t'.join(row) + '\n' for row in rows))
                command = [sys.executable, str(Path(check.__file__)), str(journal), '--requested',
                           '1200', '3600', '1200', '--results', str(output)]
                run = subprocess.run(command, capture_output=True, text=True)
                self.assertEqual(0 if complete else 1, run.returncode, run.stderr)
                result = json.loads(output.read_text())
                self.assertEqual('PASS' if complete else 'FAIL', result['verdict'])
                if complete: self.assertEqual(1201, result['waits'][-1]['elapsed'])
                original = output.read_bytes()
                self.assertNotEqual(0, subprocess.run(command, capture_output=True).returncode)
                self.assertEqual(original, output.read_bytes())

    def test_actual_clock_binding_including_two_waits_and_zero_wait_supply(self):
        counts = (1200, 1200, 7200, 1200, 6600, 6600, 1200)
        rows = [('camp-heart-chain-setup', 'OK', 'turns=6003')]
        clock = 6003
        for i, count in enumerate(counts):
            rows.extend(self.fixture((count,), extra=0))
            clock += count
            if i == 4: continue
            rows.append(('camp-heart-chain-supply' if i == 0 else 'camp-heart-chain-check', 'OK', f'turns={clock}'))
            if i == 2: rows.append(('camp-heart-chain-supply', 'OK', f'turns={clock}'))
        waits = check.judge(rows, counts)
        self.assertEqual(clock, check.bind_chain_clocks(rows, waits)['chainEndTurns'])
        for delta in (-1, 1):
            changed = list(rows)
            changed[-1] = changed[-1][0], 'OK', f'turns={clock + delta}'
            with self.assertRaises(ValueError): check.bind_chain_clocks(changed, waits)


if __name__ == '__main__': unittest.main()
