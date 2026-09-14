"""Fail missing, duplicated or contradictory housing observations; no native claims."""
import sys
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parents[1] / 'personas'))
import persona_housing as housing
import persona_matrix as matrix


def observations(cold=False):
    result = []
    for stage, turns in [('startup', 4), ('grown', 16804)] + ([('loaded', 16806)] if cold else []):
        grown = stage != 'startup'
        message = (f'stage={stage}; citizens=4; housed={4 if grown else 0}; '
                   f'shelters={2 if grown else 0}; beds={6 if grown else 0}; rooms={2 if grown else 0}; '
                   f'clear-floor={34 if grown else 0}; turns={turns}; '
                   'synthetic-residents=false; forced-housing=false')
        result += [('quickstart-settlement', 'OK', message),
                   ('lifecycle-' + {'startup': 'open', 'grown': 'grown', 'loaded': 'loaded'}[stage], 'OK', 'step')]
    return result


class HousingWitnessTests(unittest.TestCase):
    def test_complete_warm_and_separate_cold_observations_pass(self):
        self.assertEqual([], housing.assess(observations()))
        self.assertEqual([], housing.assess(observations(True), True))

    def test_each_required_observation_is_mandatory_and_unique(self):
        source = observations(True)
        for index in (0, 2, 4):
            with self.subTest(index=index):
                self.assertTrue(housing.assess(source[:index] + source[index + 1:], True))
                self.assertTrue(housing.assess(source[:index] + [source[index]] + source[index:], True))

    def test_wrong_founders_rooms_floor_capacity_or_synthetic_flags_fail(self):
        for before, after in [('citizens=4', 'citizens=3'), ('housed=4', 'housed=3'),
                              ('shelters=2', 'shelters=1'), ('beds=6', 'beds=5'),
                              ('rooms=2', 'rooms=0'), ('clear-floor=34', 'clear-floor=48'),
                              ('synthetic-residents=false', 'synthetic-residents=true'),
                              ('forced-housing=false', 'forced-housing=true')]:
            rows = observations()
            rows[2] = (rows[2][0], 'OK', rows[2][2].replace(before, after))
            self.assertTrue(housing.assess(rows), (before, after))

    def test_refused_and_malformed_fields_never_pass(self):
        for message in [observations()[2][2] + '; rooms=2',
                        observations()[2][2].replace('; rooms=2', ''),
                        observations()[2][2].replace('beds=6', 'beds=①'),
                        observations()[2][2].replace('turns=16804', 'turns=nan')]:
            rows = observations(); rows[2] = (rows[2][0], 'OK', message)
            self.assertTrue(housing.assess(rows))
        rows = observations(); rows[2] = (rows[2][0], 'REFUSED', rows[2][2])
        self.assertTrue(housing.assess(rows))

    def test_observations_must_precede_their_unique_successful_enclosing_steps(self):
        rows = observations(); rows[2], rows[3] = rows[3], rows[2]
        self.assertTrue(housing.assess(rows))
        rows = observations(); rows.append(rows[3])
        self.assertTrue(housing.assess(rows))
        rows = observations(); rows[3] = (rows[3][0], 'REFUSED', rows[3][2])
        self.assertTrue(housing.assess(rows))

    def test_short_wait_or_reversed_load_clock_fails(self):
        rows = observations(); rows[2] = (rows[2][0], 'OK', rows[2][2].replace('16804', '10004'))
        self.assertTrue(housing.assess(rows))
        rows = observations(True); rows[4] = (rows[4][0], 'OK', rows[4][2].replace('16806', '16803'))
        self.assertTrue(housing.assess(rows, True))

    def test_housing_check_remains_mandatory_when_bookkeeping_rows_are_filtered(self):
        manifest = {'VERBS': 'lifecycle-open,lifecycle-grown', 'CHECK': 'quickstart-housing',
                    'EXPECT': 'lifecycle-open:OK,lifecycle-grown:OK,COMPLETE'}
        rows = observations() + [('SCRIPT-COMPLETE', 'OK', 'complete')]
        journal = lambda values: ''.join('2026-09-14T00:00:00.000Z\t' + '\t'.join(row) + '\n' for row in values)
        self.assertEqual([], matrix.assess(manifest, journal(rows), 'housing-test'))
        self.assertTrue(matrix.assess(manifest, journal([r for r in rows if r[0] != 'quickstart-settlement']), 'housing-test'))


if __name__ == '__main__':
    unittest.main()
