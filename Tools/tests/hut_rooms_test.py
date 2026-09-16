"""Enclosed conversion destinations: deliberate room programmes and clear circulation."""
import unittest
import canvas_homes_test as canvas


class HutRoomTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        canvas.CanvasHomeTests.setUpClass()
        cls.studio = canvas.CanvasHomeTests.studio
        shape = cls.studio.checker.BlueprintShape
        # Expected native contracts for engine-free topology. The installed-blueprint
        # audit and native scenarios separately prove actual Qud parts and behavior.
        for name in ['r_KingdomHut', 'r_KingdomHutYard', 'r_KingdomMudHut',
                     'r_KingdomMudHutCourt', 'r_KingdomBlockHut', 'r_KingdomBlockYard',
                     'r_KingdomCivicCampfire', 'SaltPath', 'FungalTrailBrick', 'WoodFloor']:
            cls.studio.shapes[name] = shape(False, False)
        for name in ['r_KingdomStructureMudWall', 'r_KingdomStructureConcreteWall',
                     'r_KingdomStructureBrinestalkWall', 'r_KingdomStructurePetalWall',
                     'r_KingdomStructureMushroomWall']:
            cls.studio.shapes[name] = shape(True, False)
        cls.studio.shapes['r_KingdomFixtureGateBrinestalk'] = shape(False, True)
        cls.keys = {'hut', 'hutyard', 'mudhut', 'mudhutcourt', 'blockhut', 'blockyard'}

    def test_all_current_hut_variants_have_enclosed_usable_rooms_in_every_pose(self):
        checked = 0
        for case in self.studio.cases:
            if case['building'] not in self.keys or case['size'] == 'S':
                continue
            for pose in self.studio.checker.POSES:
                with self.subTest(case=case['label'], pose=pose):
                    r = self.studio.review(case['id'], pose=pose)
                    self.assertEqual(1 if case['building'] in ('hut', 'mudhut', 'blockhut') else 3,
                                     r['enclosed_beds'])
                    self.assertEqual({'M': 1, 'L': 3, 'XL': 4}[case['size']], r['rooms'])
                    self.assertEqual([], r['inaccessible_fixtures'])
                    self.assertEqual([], r['blocked_doorways'])
                    self.assertEqual([], r['unknown_blueprints'])
                    self.assertEqual([], r['issues'])
                    checked += 1
        self.assertEqual(192, checked)

    def test_new_huts_need_medium_ground_and_small_bindings_remain_historical(self):
        for key in self.keys:
            self.assertEqual('M', self.studio.buildings[key].plot)
            bindings = [t.binding for t in self.studio.model.tiers if t.build_key == key]
            self.assertEqual(['S'], [b.size for b in bindings if b.retained])
            self.assertEqual({'M', 'L', 'XL'}, {b.size for b in bindings if not b.retained})

    def test_conversions_keep_the_small_entrance_aisle_and_large_cross_court_clear(self):
        for case in self.studio.cases:
            if case['building'] not in self.keys or case['size'] not in ('M', 'XL'):
                continue
            r = self.studio.review(case['id'])
            cells = {(c['x'], c['y']): c for c in r['cells']}
            passage = {(3, y) for y in (2, 3, 4)} if case['size'] == 'M' else (
                {(x, y) for x in range(8, 12) for y in range(18)} |
                {(x, y) for y in range(7, 11) for x in range(20)})
            for point in passage:
                self.assertTrue(cells[point]['reached'], (case['label'], point))
                self.assertFalse(cells[point]['fixture'], (case['label'], point))


if __name__ == '__main__':
    unittest.main()
