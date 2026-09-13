import sys
from pathlib import Path
import unittest
sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import camp_heart_load_check as check


class CampHeartLoadCheckTests(unittest.TestCase):
    def fixture(self):
        game = '01234567-89ab-cdef-0123-456789abcdef'
        physical = 'heart=heart; store=store; fire=fire'
        digest = 'snapshot-sha256=' + 'a' * 64
        wait = lambda n: f'{n} turn(s) elapsed of {n} requested'
        source = [('camp-heart-setup', 'OK', 'setup')]
        for n in (1200, 3600, 1200):
            source += [('advance-complete', 'OK', wait(n)), ('camp-heart-check', 'OK', 'phase')]
        source[-1] = ('camp-heart-check', 'OK', 'native-camp-heart cases=1 passed=1 failed=0; next-day=true')
        source += [('camp-heart-save-custody', 'OK', 'store=store; before=r_KingdomBrush=21; after=r_KingdomBrush=21,r_KingdomTimber=1; added-timber=timber'),
                   ('camp-heart-save', 'OK', 'paid-camp-save=true; rung=2; synthetic-next-job-timber=1; brush=21; time-ticks=265527; save=' + game + '; tent-job=tent-job; ' + physical + '; ' + digest),
                   ('SCRIPT-COMPLETE', 'OK', 'done')]
        loaded = [('LOAD-BEGIN', 'OK', 'exact sealed save; game-id=' + game + '; new-game=false; mod-restore=false'),
                  ('camp-heart-preactivation', 'OK', 'before-AfterGameLoaded=true; tent-job=tent-job; time-ticks=265527; ' + physical + '; ' + digest),
                  ('camp-heart-loaded', 'OK', 'rung=2; basin=48; brush=21; timber=1; time-ticks=265527; tent-job=tent-job; ' + physical + '; ' + digest),
                  ('camp-heart-next', 'OK', 'new-job=next; upgrade-job=upgrade; water-debited=2; timber-debited=1; synthetic-materials-after-load=0'),
                  ('camp-heart-resume', 'OK', 'vanilla-Continue=true; saved-script-considered=true; requested-turns=3600'),
                  ('advance-complete', 'OK', wait(3600)),
                  ('camp-heart-completed', 'OK', 'new-job=next; phase=Complete; effects-settled=true; output=new-fire; ' + physical + '; brush=21; turns=9602'),
                  ('SCRIPT-COMPLETE', 'OK', 'native-camp-heart cold-load complete; real-save-quit-load=true; next-paid-job-complete=true; new-game-script-replayed=false; ordinary-acceptance=false')]
        return source, loaded

    def test_paid_loaded_job_really_completed(self):
        result = check.judge(*self.fixture())
        self.assertEqual(('PASS', 'next', 'new-fire', 6000, 3600, 21),
                         tuple(result[key] for key in ('verdict', 'newJobId', 'outputId', 'sourceTurns', 'loadedTurns', 'brush')))

    def test_missing_duplicate_reordered_or_refused_phase_fails(self):
        for side in (0, 1):
            for index in range(len(self.fixture()[side])):
                for mode in ('remove', 'repeat', 'refuse', 'reorder'):
                    rows = self.fixture()
                    if mode == 'remove': del rows[side][index]
                    elif mode == 'repeat': rows[side].insert(index, rows[side][index])
                    elif mode == 'refuse':
                        event, _, detail = rows[side][index]
                        rows[side][index] = event, 'REFUSED', detail
                    elif index == 0: continue
                    else: rows[side][index - 1], rows[side][index] = rows[side][index], rows[side][index - 1]
                    with self.subTest(side=side, index=index, mode=mode), self.assertRaises(ValueError):
                        check.judge(*rows)

    def test_loaded_identity_payment_completion_and_resume_drift_fail(self):
        changes = ((0, 'game-id=01234567', 'game-id=11234567'), (0, 'new-game=false', 'new-game=true'),
                   (1, 'heart=heart', 'heart=other'), (1, 'store=store', 'store=other'),
                   (1, 'fire=fire', 'fire=other'), (1, 'basin=48', 'basin=24'),
                   (1, 'brush=21', 'brush=22'), (1, 'a' * 64, 'b' * 64),
                   (1, 'tent-job=tent-job', 'tent-job=another-tent'),
                   (2, 'new-job=next', 'new-job=tent-job'),
                   (2, 'new-job=next', 'new-job=upgrade'), (2, 'water-debited=2', 'water-debited=0'),
                   (2, 'timber-debited=1', 'timber-debited=0'), (2, 'synthetic-materials-after-load=0', 'synthetic-materials-after-load=1'),
                   (3, 'vanilla-Continue=true', 'vanilla-Continue=false'), (3, 'saved-script-considered=true', 'saved-script-considered=false'),
                   (4, '3600 turn(s) elapsed', '3599 turn(s) elapsed'),
                   (5, 'phase=Complete', 'phase=Working'), (5, 'effects-settled=true', 'effects-settled=false'),
                   (5, 'output=new-fire', 'output=fire'), (5, 'new-job=next', 'new-job=old'),
                   (5, 'brush=21', 'brush=22'), (6, 'new-game-script-replayed=false', 'new-game-script-replayed=true'))
        for index, before, after in changes:
            source, loaded = self.fixture()
            if index >= 1: index += 1
            event, outcome, detail = loaded[index]
            self.assertIn(before, detail)
            loaded[index] = event, outcome, detail.replace(before, after)
            with self.subTest(before=before), self.assertRaises(ValueError): check.judge(source, loaded)

    def test_replay_duplicate_fields_and_rows_after_terminal_fail(self):
        for event in ('camp-heart-setup', 'camp-heart-check', 'camp-heart-save', 'stagedigest'):
            source, loaded = self.fixture()
            loaded.insert(0, (event, 'OK', 'replay'))
            with self.assertRaises(ValueError): check.judge(source, loaded)
        source, loaded = self.fixture()
        event, status, detail = loaded[2]
        loaded[2] = event, status, detail + '; brush=21'
        with self.assertRaises(ValueError): check.judge(source, loaded)
        source, loaded = self.fixture()
        loaded.append(('extra', 'OK', 'after terminal'))
        with self.assertRaises(ValueError): check.judge(source, loaded)

    def test_preactivation_must_match_saved_physical_state_and_clock(self):
        for index in (1, 2):
            for before, after in (('265527', '265528'), ('heart=heart', 'heart=other'),
                                  ('tent-job=tent-job', 'tent-job=other'), ('a' * 64, 'b' * 64)):
                source, loaded = self.fixture()
                event, status, detail = loaded[index]
                loaded[index] = event, status, detail.replace(before, after)
                with self.subTest(index=index, before=before), self.assertRaises(ValueError): check.judge(source, loaded)


if __name__ == '__main__': unittest.main()
