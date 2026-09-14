"""Authored canvas-room programmes, clear routes and retained-reader boundaries."""
import sys
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / 'Tools'))
from layout_studio_data import Studio


class CanvasHomeTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.studio = Studio(ROOT)
        # Pure topology uses the native contracts; installed-blueprint and in-game gates
        # independently establish the real parts/physics. No native result is inferred here.
        shape = cls.studio.checker.BlueprintShape
        for name in ['DirtFloor', 'DirtPath', 'r_KingdomGroundTroddenPath',
                     'r_KingdomFixtureBedrollCanvas', 'r_KingdomFixtureBasketEmpty',
                     'r_KingdomFixtureCushionCanvas', 'r_KingdomFixtureTableTimber',
                     'r_KingdomTent', 'r_KingdomTentRow']:
            cls.studio.shapes[name] = shape(False, False)
        cls.studio.shapes['r_KingdomStructureCanvasWall'] = shape(True, False)
        cls.studio.shapes['r_KingdomFixtureDoorTimber'] = shape(False, True)

    def test_every_current_size_has_enclosed_accessible_beds_in_every_pose(self):
        checked = 0
        for case in self.studio.cases:
            if case['building'] not in ('tent', 'tentrow') or case['size'] == 'S':
                continue
            for pose in self.studio.checker.POSES:
                with self.subTest(build=case['building'], size=case['size'], pose=pose):
                    reading = self.studio.review(case['id'], pose=pose)
                    self.assertEqual(1 if case['building'] == 'tent' else 3, reading['enclosed_beds'])
                    self.assertEqual({'M': 1, 'L': 3, 'XL': 4}[case['size']], reading['rooms'])
                    self.assertEqual([], reading['inaccessible_fixtures'])
                    self.assertEqual([], reading['blocked_doorways'])
                    self.assertEqual([], reading['issues'])
                    self.assertEqual([], reading['unknown_blueprints'])
                    checked += 1
        self.assertEqual(24, checked)

    def test_shared_shelter_reserves_a_straight_entrance_aisle(self):
        for build in ('tent', 'tentrow'):
            case = next(c for c in self.studio.cases if c['building'] == build and c['size'] == 'M')
            reading = self.studio.review(case['id'])
            cells = {(c['x'], c['y']): c for c in reading['cells']}
            # The door at (3,5) opens along a clear aisle to the sleeping row.
            # Being able to detour around a chair would not satisfy this programme.
            for y in (2, 3, 4):
                self.assertTrue(cells[3, y]['reached'])
                self.assertFalse(cells[3, y]['fixture'])

    def test_larger_shelters_change_sleeping_rooms_not_just_yard_area(self):
        rooms = {}
        for case in self.studio.cases:
            if case['building'] == 'tentrow' and case['size'] != 'S':
                reading = self.studio.review(case['id'])
                rooms[case['size']] = sorted(space['beds'] for space in reading['spaces'] if space['beds'])
        self.assertEqual({'M': [3], 'L': [1, 2], 'XL': [1, 1, 1]}, rooms)

    def test_small_layouts_are_retained_below_the_new_minimum_only(self):
        for key in ['tent', 'tentrow']:
            self.assertEqual('M', self.studio.buildings[key].plot)
            bindings = [tier.binding for tier in self.studio.model.tiers if tier.build_key == key]
            self.assertEqual(['S'], [b.size for b in bindings if b.retained])
            self.assertEqual({'M', 'L', 'XL'}, {b.size for b in bindings if not b.retained})

    def test_all_cabin_thresholds_open_into_the_four_cell_court(self):
        case = next(c for c in self.studio.cases if c['building'] == 'tentrow' and c['size'] == 'XL')
        r = self.studio.review(case['id'])
        by_cell = {(c['x'], c['y']): c for c in r['cells']}
        for y in range(18):
            for x in range(8, 12):
                self.assertTrue(by_cell[x, y]['reached'])
                self.assertFalse(by_cell[x, y]['fixture'])
        for y in range(7, 11):
            for x in range(20):
                self.assertTrue(by_cell[x, y]['reached'])
                self.assertFalse(by_cell[x, y]['fixture'])


if __name__ == '__main__':
    unittest.main()
