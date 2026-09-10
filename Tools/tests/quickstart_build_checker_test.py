"""Committed regression for the quickstart-build acceptance-oracle defect: journal() must
never silently accept a well-formed but REFUSED build (verify() would then emit verdict PASS
for a build that never actually succeeded), and must never accept a refused BOOT row or a
step row whose disclosed content contradicts what it claims. These are the same 14 shapes as
the independent scratch probe (taf-scratch/hotfix142-checker-probe.py), committed here so CI
catches a regression of the defect it found. Synthetic journals only; no native evidence."""

import importlib.util
import sys
import unittest
from pathlib import Path

TOOLS = Path(__file__).resolve().parents[1]
_SPEC = importlib.util.spec_from_file_location(
    "quickstart_build_checker", TOOLS / "check-quickstart-results.py"
)
checker = importlib.util.module_from_spec(_SPEC)
sys.path.insert(0, str(TOOLS))
try:
    _SPEC.loader.exec_module(checker)
finally:
    sys.path.pop(0)

COMMAND = "quickstart-build marsh no"
SEED = "#43101"
GENUINE_SUCCESS_ROWS = [
    (
        "QUICKSTART-BOOT-BEGIN",
        "OK",
        COMMAND + "; seed=" + SEED + "; genuine-production-boot=true",
    ),
    ("QUICKSTART-BOOT-OBSERVED", "OK", checker.OBSERVED),
    (
        "QUICKSTART-BOOT-COMPLETE",
        "OK",
        COMMAND + "; boot-only=true; save-load=false; ordinary-acceptance=false",
    ),
    (
        "QUICKSTART-BUILD-BEGIN",
        "OK",
        COMMAND + "; boot-only=false; genuine-production-commission=true",
    ),
    ("QUICKSTART-BUILD-QUOTE", "OK", "boot-only=false; waterDrams=2"),
    ("QUICKSTART-BUILD-CANPAY", "OK", "boot-only=false; blocked=false"),
    ("QUICKSTART-BUILD-COMMISSION", "OK", "boot-only=false; commissioned=true"),
    (
        "QUICKSTART-BUILD-COMPLETE",
        "OK",
        COMMAND
        + "; commissioned=true; timber-debit=exact; water-debit=exact; job-projected=true"
        "; survey-scope-clear=True; boot-only=false; build-refused=false",
    ),
]


def accepted(rows, suffix="\n"):
    """True when journal() returns without raising -- i.e. would let verify() proceed toward
    a PASS verdict. False when it raises (the required outcome for anything but a genuine,
    fully-successful build)."""
    raw = (
        "\n".join("2026-09-10T22:08:00.000Z\t" + "\t".join(row) for row in rows)
        + suffix
    ).encode()
    try:
        checker.journal(raw, "build", COMMAND, SEED, None)
        return True
    except (ValueError, SystemExit):
        return False


class QuickstartBuildAcceptanceOracleTest(unittest.TestCase):
    def test_genuine_closed_success_is_accepted(self):
        self.assertTrue(accepted(GENUINE_SUCCESS_ROWS))

    def test_each_refused_boot_row_is_rejected(self):
        for index in range(3):
            with self.subTest(boot_row=index):
                rows = GENUINE_SUCCESS_ROWS.copy()
                rows[index] = (rows[index][0], "REFUSED", rows[index][2])
                self.assertFalse(accepted(rows))

    def test_contradictory_step_disclosure_is_rejected(self):
        for index in (4, 5, 6):
            genuine_message = GENUINE_SUCCESS_ROWS[index][2]
            for message in (
                genuine_message.replace("boot-only=false; ", ""),
                "boot-only=false; nonsense=true",
            ):
                with self.subTest(step_row=index, message=message):
                    rows = GENUINE_SUCCESS_ROWS.copy()
                    rows[index] = (rows[index][0], "OK", message)
                    self.assertFalse(accepted(rows))

    def test_well_formed_production_refusal_is_never_accepted(self):
        # The exact defect Codex reproduced: QUOTE OK, CANPAY REFUSED, terminal REFUSED with a
        # correctly-attributed step -- a completely well-formed refusal journal that must still
        # never let verify() reach PASS.
        rows = GENUINE_SUCCESS_ROWS[:5] + [
            (
                "QUICKSTART-BUILD-CANPAY",
                "REFUSED",
                "boot-only=false; blocked=true; reason=materials missing",
            ),
            (
                "QUICKSTART-BUILD-COMPLETE",
                "REFUSED",
                COMMAND + "; build-refused=true; boot-only=false; step=canpay"
                "; survey-scope-clear=True; refusal=materials missing",
            ),
        ]
        self.assertFalse(accepted(rows))

    def test_torn_terminal_row_is_rejected(self):
        self.assertFalse(accepted(GENUINE_SUCCESS_ROWS, suffix=""))

    def test_duplicate_terminal_row_is_rejected(self):
        self.assertFalse(accepted(GENUINE_SUCCESS_ROWS + [GENUINE_SUCCESS_ROWS[-1]]))

    def test_missing_commission_row_is_rejected(self):
        self.assertFalse(accepted(GENUINE_SUCCESS_ROWS[:6] + GENUINE_SUCCESS_ROWS[7:]))

    def test_post_success_scope_leak_is_never_accepted(self):
        # All three production steps genuinely succeeded, but the live re-check after the five
        # census-after reads found a leaked survey scope -- also a well-formed refusal that must
        # never be accepted.
        rows = GENUINE_SUCCESS_ROWS[:7] + [
            (
                "QUICKSTART-BUILD-COMPLETE",
                "REFUSED",
                COMMAND + "; build-refused=true; boot-only=false; step=post-success"
                "; survey-scope-clear=False; refusal=a survey scope leaked after success",
            )
        ]
        self.assertFalse(accepted(rows))


if __name__ == "__main__":
    unittest.main()
