import sys
from pathlib import Path
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import guest_save_check


class GuestSaveCheckTests(unittest.TestCase):
    def fixture(self):
        stamp = "guest=123; population=5; receipt-sha256=" + "a" * 64 + "; world-repair=false"
        source = [("guest-actions-check", "OK", "passed"), ("guest-save-witness", "OK", stamp),
                  ("lifecycle-save", "OK", "saved"), ("SCRIPT-COMPLETE", "OK", "done")]
        loaded = [("guest-load-preactivation", "OK", "exact-guest-authority=true; before-AfterGameLoaded=true; " + stamp),
                  ("guest-load-verified", "OK", "same-citizen=true; stale-choice-refused=2; duplicate-enrollment=false; extra-water-debit=false; " + stamp),
                  ("lifecycle-loaded", "OK", "loaded"), ("lifecycle-next", "OK", "paid"),
                  ("SCRIPT-COMPLETE", "OK", "done")]
        return source, loaded

    def test_exact_two_session_witness(self):
        result = guest_save_check.judge(*self.fixture())
        self.assertEqual(("PASS", "123", 5), (result["verdict"], result["guestId"], result["population"]))

    def test_missing_preactivation_refuses(self):
        source, loaded = self.fixture()
        with self.assertRaises(ValueError): guest_save_check.judge(source, loaded[1:])

    def test_changed_identity_population_digest_or_retry_refuses(self):
        for old, new in (("guest=123", "guest=124"), ("population=5", "population=6"),
                         ("a" * 64, "b" * 64), ("stale-choice-refused=2", "stale-choice-refused=1"),
                         ("duplicate-enrollment=false", "duplicate-enrollment=true"),
                         ("world-repair=false", "world-repair=true")):
            with self.subTest(old=old):
                source, loaded = self.fixture()
                event, outcome, detail = loaded[1]
                loaded[1] = event, outcome, detail.replace(old, new)
                with self.assertRaises(ValueError): guest_save_check.judge(source, loaded)

    def test_duplicate_or_out_of_order_witness_refuses(self):
        for duplicate in (True, False):
            source, loaded = self.fixture()
            if duplicate: loaded.insert(1, loaded[0])
            else: loaded[0], loaded[1] = loaded[1], loaded[0]
            with self.assertRaises(ValueError): guest_save_check.judge(source, loaded)

    def test_source_refusal_or_stopped_load_refuses(self):
        source, loaded = self.fixture()
        source[0] = "guest-actions-check", "REFUSED", "failed"
        with self.assertRaises(ValueError): guest_save_check.judge(source, loaded)
        source, loaded = self.fixture()
        loaded[-1] = "SCRIPT-STOPPED", "REFUSED", "failed"
        with self.assertRaises(ValueError): guest_save_check.judge(source, loaded)


if __name__ == "__main__": unittest.main()
