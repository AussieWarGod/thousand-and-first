#!/usr/bin/env python3
"""Regression coverage for the source-pinned deterministic balance model."""

from __future__ import annotations

import subprocess
import sys
import unittest
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]


class BalanceSimulationTests(unittest.TestCase):
    def test_current_split_source_families_satisfy_every_balance_pin(self) -> None:
        completed = subprocess.run(
            [sys.executable, str(ROOT / "_notes" / "balance-sim.py")],
            cwd=ROOT,
            capture_output=True,
            text=True,
            timeout=30,
            check=False,
        )
        self.assertEqual(0, completed.returncode, completed.stderr or completed.stdout)
        self.assertIn("Constants read from source:", completed.stdout)
        self.assertIn("THE MILL CONSERVES", completed.stdout)


if __name__ == "__main__":
    unittest.main()
