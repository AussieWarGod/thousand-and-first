import importlib.util
from pathlib import Path
import unittest

spec = importlib.util.spec_from_file_location('founding_regard_check',
    Path(__file__).resolve().parents[1] / 'founding_regard_check.py')
check_module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(check_module)


class FoundingRegardCheckTests(unittest.TestCase):
    def setUp(self):
        self.freeze = ('founding regard freeze boundary: result=True; version=2; count=80; '
                       'inanimate=0; personal=0; eligible=True; identity=True; polity=True;\n')
        self.change = ('founding regard native change: baseline=0; city-delta=73; personal-before=0; '
                       'personal-after=40; standing=93; carry=0; compared=80; '
                       'synthetic-reputation=true; synthetic-residents=false\n')
        self.witness = lambda stage: (f'founding regard native witness: stage={stage}; entries=80; '
                                     f'sha256={"a" * 64}; preserved=true\n')
        self.source = self.freeze + self.change + self.witness('startup') + self.witness('grown')
        self.loaded = self.witness('loaded')

    def test_complete_chain(self):
        self.assertEqual(check_module.check(self.source, self.loaded)['standing'], 93)

    def test_missing_duplicate_or_reordered_observations(self):
        cases = [self.source.replace(self.freeze, ''), self.source.replace(self.change, ''),
                 self.source.replace(self.witness('grown'), ''), self.source + self.witness('startup'),
                 self.source.replace('stage=startup', 'stage=loaded'),
                 self.freeze + self.change + self.witness('grown') + self.witness('startup')]
        for source in cases:
            with self.subTest(source=source), self.assertRaises(ValueError):
                check_module.check(source, self.loaded)

    def test_bad_counterexamples_and_different_loaded_snapshot(self):
        for before, after in [('standing=93', 'standing=73'), ('city-delta=73', 'city-delta=0'),
                              ('personal-after=40', 'personal-after=0'), ('count=80', 'count=0'),
                              ('compared=80', 'compared=513'), ('preserved=true', 'preserved=false')]:
            with self.subTest(after=after), self.assertRaises(ValueError):
                check_module.check(self.source.replace(before, after), self.loaded)
        for loaded in ['', self.loaded * 2, self.loaded.replace('a' * 64, 'b' * 64),
                       self.change + self.loaded, self.freeze + self.loaded]:
            with self.subTest(loaded=loaded), self.assertRaises(ValueError):
                check_module.check(self.source, loaded)
