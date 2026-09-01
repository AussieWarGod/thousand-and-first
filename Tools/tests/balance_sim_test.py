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
        self.assertIn(
            "F1  Physical food and explicit-provider acts (author ruling 2026-09-01)",
            completed.stdout,
        )
        self.assertIn("passive ration/forage/hunger rate = 0", completed.stdout)
        self.assertIn(
            "population equilibrium/binding = water + roof only; food cannot cause subsidence",
            completed.stdout,
        )
        self.assertIn(
            "mill conversion = 2 crops x3 -> net 4, matching food:4",
            completed.stdout,
        )
        self.assertIn(
            "market: current physical provider + exact held office; first service Village/tier 3",
            completed.stdout,
        )
        self.assertIn(
            "market stock: native TradeUI physical _stock only; generated output/restock = 0",
            completed.stdout,
        )
        self.assertIn(
            "market custody: detached/personal/foreign goods stay physical; TAF marks retire only",
            completed.stdout,
        )
        self.assertIn(
            "market succession: only an open prepared handoff endpoint is temporarily unavailable",
            completed.stdout,
        )
        self.assertIn("C3  Fully-grafted founder structural stress case", completed.stdout)
        self.assertIn("total bill: 305 drams, 66 staffed days, 12 kept parts", completed.stdout)
        self.assertIn("structural verdict: PASS", completed.stdout)
        self.assertIn("sub-adventuring invariant: UNSIGNED", completed.stdout)
        self.assertIn("Q13 Purpose portfolio: five pairs, ten exact directions, one live operation",
                      completed.stdout)
        self.assertIn("ten-direction local bill: water=134, food=24", completed.stdout)
        self.assertIn("operation-boundary cargo: carried-food metadata=12", completed.stdout)
        self.assertIn("food conservation: 24 debited = 12 landed + 4 carriage loss + 8 local process sink",
                      completed.stdout)
        self.assertIn("active Harvest edge: 8 debited = 6 landed + 2 carriage loss",
                      completed.stdout)
        self.assertIn("cap case: all 10 unique directions resolve; cap+1 duplicate/non-edge/self refuses",
                      completed.stdout)
        self.assertIn("live-edge cap: min(3 cities // 2, one pair register) = 1; edge 2 refuses",
                      completed.stdout)
        self.assertIn("route standing demand: 2 gates x 12000 = 24000", completed.stdout)
        self.assertIn("sensitivity: 31 scalar/material -1 boundaries", completed.stdout)
        self.assertIn("native ten-direction/save/interruption/appearance evidence: UNSIGNED",
                      completed.stdout)
        self.assertIn("Q14 Hosted arcology: one shell, bounded lots, physical water-gated food",
                      completed.stdout)
        self.assertIn("exact bill: water=116; build ticks=16800", completed.stdout)
        self.assertIn("both paid lots active; exact shell-effectiveness sensitivity:",
                      completed.stdout)
        self.assertIn("14 growbeds x 2 rows = 28 rows -> food:14", completed.stdout)
        self.assertIn("StoredWater=0 -> food:0; StoredWater>0 -> food:14", completed.stdout)
        self.assertIn("2 paid + 1 read-only + 13 extension slots = 16; slot 17 refuses",
                      completed.stdout)
        self.assertIn("staffing: shell 4; one Working lot adds 2 -> transient peak 6",
                      completed.stdout)
        self.assertIn("master pause: physical labour freezes; MasterOptionTick discards pre-resume",
                      completed.stdout)
        self.assertIn("native traversal/save/cardinality/water/appearance evidence: UNSIGNED",
                      completed.stdout)
        self.assertIn("Q15 Routed construction inputs: itinerary, custody, debit, rollback, recovery",
                      completed.stdout)
        self.assertIn("4096 drams = 64 x 64; 4097 drams needs 65 and refuses",
                      completed.stdout)
        self.assertIn("64 same-holder cargo -> 6 porters; 16 endpoint groups fit; 17 refuse",
                      completed.stdout)
        self.assertIn("global job boundary: 16 endpoint children fill all 16 rows",
                      completed.stdout)
        self.assertIn("G1 reuse: rung-floor daily upkeep 4/6/18/45/110", completed.stdout)
        self.assertIn("step sensitivity at 64 lines: whole material 979/6 passes; split material 1299/7; water 1043/6",
                      completed.stdout)
        self.assertIn("before+paused waits; exact after acknowledges; third state quarantines",
                      completed.stdout)
        self.assertIn("master pause: global physical recovery freezes; PausedTicks shifts arrival",
                      completed.stdout)
        self.assertIn("source + flight + landed + spent + compensating + quarantine + loss = expected",
                      completed.stdout)
        self.assertIn("native cold-save/obstruction/carrier/custody/debit/recovery evidence: UNSIGNED",
                      completed.stdout)
        self.assertIn("C4  Activated v1 authority capacity and concurrency boundaries",
                      completed.stdout)
        self.assertIn("C18 framed envelope          840,048", completed.stdout)
        self.assertIn("ACTIVE SOURCE                864,624", completed.stdout)
        self.assertIn("MaxCivicMemorySectionArchiveChars = 321,848", completed.stdout)
        self.assertIn("known section; CivicMemoryChunkChars absent", completed.stdout)
        self.assertIn("C18 base64 content         1,119,828", completed.stdout)
        self.assertIn("C18 note metadata             22,077", completed.stdout)
        self.assertIn("experience note total         86,811", completed.stdout)
        self.assertIn("NATIVE RETIREMENT          1,228,716", completed.stdout)
        self.assertIn("ACTIVE + RETIREMENT        2,093,340", completed.stdout)
        self.assertIn("4 MiB contract headroom    2,100,964", completed.stdout)
        self.assertIn("worst-lawful share: 49.91%", completed.stdout)
        self.assertIn("vocation-service 48 (16/city)", completed.stdout)
        self.assertIn("native notes: 9 C18 sections + 12 Experience civic rows = 21",
                      completed.stdout)
        self.assertIn("uncompressed UTF-8 structural content + conservative per-note framing",
                      completed.stdout)
        self.assertIn("covenant authors <=4094 and accepts <=4096", completed.stdout)
        self.assertIn("generated output/restock = 0", completed.stdout)
        self.assertIn("native save size/p50/p95/max: UNSIGNED -- no native distribution is inferred",
                      completed.stdout)


if __name__ == "__main__":
    unittest.main()
