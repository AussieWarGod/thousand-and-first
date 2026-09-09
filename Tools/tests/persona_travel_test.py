"""Travel witness negatives execute the same checker used by the persona verdict."""
import importlib.util
from pathlib import Path
import unittest

ROOT = Path(__file__).resolve().parents[2]
SPEC = importlib.util.spec_from_file_location("persona_travel", ROOT / "Tools/personas/persona_travel.py")
travel = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(travel)


def rows(mode="away", **changes):
    data = dict(mode=mode, seed="#123", home="JoppaWorld.8.22.0.0.10")
    data.update({"observed-tick": "20000", "wait-turns": "1200", "travel-turns": "82" if mode == "away" else "0",
                 "containers": "4", "envelope": "252", "drain-turns": "3", "peak-thirds": "24", "peak-heavy": "4",
                 "measured-demand": "12", "demand-observed": "True", "processed": "19900", "semantic": "19800",
                 "growth-mirror": "19900", "schedule-observations": "100", "remaining-demand": "0",
                 "pause-effects-proved": "false", "full-envelope-stress": "false", "ordinary-acceptance": "false"})
    data.update(changes)
    result = [("stagedigest", "OK", "digest"), ("realize", "OK", "founded"),
              ("advance-complete", "OK", "1 turn(s) elapsed of 1 requested"), ("beta-" + mode, "OK", "began")]
    if mode == "away":
        result.append(("travel-out-complete", "OK", "normal-walk=true; steps=41"))
    result.extend([("advance-complete", "OK", "1200 turn(s) elapsed of 1200 requested"), ("beta-return", "OK", "return")])
    if mode == "away":
        result.append(("travel-return-complete", "OK", "normal-walk=true; steps=41"))
    result.extend([("advance-complete", "OK", "39 turn(s) elapsed of 39 requested"),
                   ("yield-frames-complete", "OK", "frame"),
                   ("beta-check", "OK", "taf-travel-checked " + "; ".join(f"{k}={v}" for k, v in data.items())),
                   ("status", "OK", "status"), ("SCRIPT-COMPLETE", "OK", "done")])
    return result


class TravelTests(unittest.TestCase):
    def test_valid_pair_does_not_claim_equal_total_elapsed_or_release(self):
        result = travel.compare(rows("present"), rows())
        self.assertEqual(result["awayTravelTurns"], 82)
        self.assertFalse(result["equalTotalElapsedClaimed"])
        self.assertFalse(result["fullEnvelopeStress"])
        self.assertFalse(result["releaseAcceptance"])

    def test_envelope_clock_and_claim_negatives(self):
        for key, value in (("containers", "253"), ("drain-turns", "40"), ("peak-thirds", "25"),
                           ("peak-heavy", "5"), ("measured-demand", "937"), ("envelope", "251"),
                           ("wait-turns", "1199"), ("growth-mirror", "19899"), ("semantic", "20001"),
                           ("demand-observed", "False"), ("remaining-demand", "1"), ("schedule-observations", "0"),
                           ("pause-effects-proved", "true"), ("ordinary-acceptance", "true"),
                           ("full-envelope-stress", "true"), ("seed", "random"), ("containers", "04")):
            with self.subTest(key=key, value=value):
                self.assertTrue(travel.assess(rows(**{key: value}), "away"))

    def test_missing_and_duplicate_milestones(self):
        for name in ("beta-away", "beta-return", "beta-check", "travel-out-complete", "travel-return-complete",
                     "advance-complete", "yield-frames-complete", "SCRIPT-COMPLETE"):
            original = rows()
            self.assertTrue(travel.assess([r for r in original if r[0] != name], "away"), name)
            duplicate = next(r for r in original if r[0] == name)
            self.assertTrue(travel.assess(original + [duplicate], "away"), name)

    def test_refusal_cannot_be_hidden(self):
        self.assertTrue(travel.assess(rows() + [("travel-refused", "REFUSED", "blocked")], "away"))

    def test_movement_order_matters(self):
        original = rows()
        movement = next(r for r in original if r[0] == "travel-return-complete")
        original.remove(movement)
        self.assertTrue(travel.assess([movement] + original, "away"))

    def test_advances_must_be_inside_their_phase(self):
        original = rows()
        advance = next(r for r in original if r[0] == "advance-complete" and r[2].startswith("1200"))
        original.remove(advance)
        self.assertTrue(travel.assess([advance] + original, "away"))

    def test_movement_requires_equal_bounded_real_steps(self):
        for message in ("teleport=true; steps=41", "normal-walk=true; steps=241", "normal-walk=true; steps=40"):
            original = [(v, o, message if v == "travel-return-complete" else m) for v, o, m in rows()]
            self.assertTrue(travel.assess(original, "away"))

    def test_present_cannot_move(self):
        self.assertTrue(travel.assess(rows("present", **{"travel-turns": "1"}), "present"))
        self.assertTrue(travel.assess(rows("present") + [("travel-out-complete", "OK", "moved")], "present"))

    def test_pair_requires_same_seed_home_and_mode(self):
        for changed in ({"seed": "#124"}, {"home": "JoppaWorld.9.22.0.0.10"}, {"mode": "present"}):
            with self.assertRaises(ValueError):
                travel.compare(rows("present"), rows(**changed))

    def test_schema_cannot_drop_add_or_duplicate_fields(self):
        for alter in (lambda text: text.replace("; remaining-demand=0", ""),
                      lambda text: text + "; fake=0", lambda text: text + "; mode=away"):
            original = rows()
            original = [(v, o, alter(m) if v == "beta-check" else m) for v, o, m in original]
            self.assertTrue(travel.assess(original, "away"))


if __name__ == "__main__":
    unittest.main()
