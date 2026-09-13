"""Failure diagnostics only; fake effects do not establish native process ownership."""
from pathlib import Path
import sys
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
from persona_reload import execute


class CleanupTests(unittest.TestCase):
    def test_double_failure_keeps_original_cause_and_reports_both_errors(self):
        for original in (ValueError("launch refused"), KeyboardInterrupt("operator stopped")):
            with self.subTest(original=type(original).__name__):
                events = []

                class Backend:
                    def idle(self, phase): pass
                    def fresh(self, phase): return "source"
                    def prepare(self, *args): pass
                    def launch(self, *args): raise original
                    def stop(self, root, phase):
                        events.append((root, phase))
                        raise ValueError("receipt stop refused")

                with self.assertRaises(RuntimeError) as raised:
                    execute(Backend(), "marsh", "yes")
                self.assertIs(raised.exception.__cause__, original)
                self.assertIn(str(original), str(raised.exception))
                self.assertIn("receipt stop refused", str(raised.exception))
                self.assertEqual(events, [("source", "failure")])

    def test_successful_cleanup_preserves_original_exception_identity(self):
        original = ValueError("launch refused")
        events = []

        class Backend:
            def idle(self, phase): pass
            def fresh(self, phase): return "source"
            def prepare(self, *args): pass
            def launch(self, *args): raise original
            def stop(self, root, phase): events.append((root, phase))

        with self.assertRaises(ValueError) as raised:
            execute(Backend(), "marsh", "yes")
        self.assertIs(raised.exception, original)
        self.assertEqual(events, [("source", "failure")])


if __name__ == "__main__":
    unittest.main()
