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
              ("advance-complete", "OK", "1200 turn(s) elapsed of 1200 requested"), ("beta-" + mode, "OK", "began")]
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


def economic_rows(mode="away", **changes):
    values = {"containers": "252", "pause-effects-proved": "true", "full-envelope-stress": "true",
              "pause-disabled": "100", "pause-resumed": "15000", "paused-ticks": "14930",
              "resume-arrival": "16200", "resume-applications": "1", "pause-local-start": "80",
              "pause-prior": "10", "arrival-interval": "1200", "stress-initial-thirds": "756",
              "stress-residents": "0", "synthetic-fixture": "true"}
    values.update(changes)
    result = rows(mode, **values)
    result[3:3] = [("beta-local-pause", "OK", "set"),
                   ("advance-complete", "OK", "1 turn(s) elapsed of 1 requested"),
                   ("beta-master-pause", "OK", "set"),
                   ("advance-complete", "OK", "1 turn(s) elapsed of 1 requested"),
                   ("beta-stress", "OK", "ready")]
    return result


class TravelTests(unittest.TestCase):
    def test_warmup_requires_a_real_day_before_observation(self):
        for fixture in (rows, economic_rows):
            for elapsed, requested, valid in ((1200, 1200, True), (1201, 1200, True),
                                               (1, 1, False), (1199, 1200, False),
                                               (1200, 1, False)):
                with self.subTest(fixture=fixture.__name__, elapsed=elapsed, requested=requested):
                    journal = fixture()
                    journal[2] = ("advance-complete", "OK", f"{elapsed} turn(s) elapsed of {requested} requested")
                    self.assertEqual(not travel.assess(journal, "away", fixture is economic_rows), valid)

    def test_drain_completion_observation_can_overshoot_without_extending_deadline(self):
        for fixture in (rows, economic_rows):
            for mode in ("away", "present"):
                with self.subTest(fixture=fixture.__name__, mode=mode):
                    journal = fixture(mode, **{"drain-turns": "39"})
                    journal = [(verb, outcome, "40 turn(s) elapsed of 39 requested"
                                if verb == "advance-complete" and message.startswith("39 ") else message)
                               for verb, outcome, message in journal]
                    self.assertEqual(travel.assess(journal, mode, fixture is economic_rows), [])
                    for key, value in (("drain-turns", "40"), ("remaining-demand", "1"),
                                       ("demand-observed", "False")):
                        bad = [(verb, outcome, message.replace(key + "=" +
                               {"drain-turns": "39", "remaining-demand": "0", "demand-observed": "True"}[key],
                               key + "=" + value)) for verb, outcome, message in journal]
                        self.assertTrue(travel.assess(bad, mode, fixture is economic_rows))

    def test_drain_completion_still_requires_requested_count_and_canonical_elapsed(self):
        for message in ("38 turn(s) elapsed of 39 requested", "40 turn(s) elapsed of 40 requested",
                        "040 turn(s) elapsed of 39 requested", "40 turn(s) elapsed of 39 requested extra",
                        "9223372036854775808 turn(s) elapsed of 39 requested"):
            with self.subTest(message=message):
                journal = [(verb, outcome, message if verb == "advance-complete" and text.startswith("39 ")
                            else text) for verb, outcome, text in rows()]
                self.assertTrue(travel.assess(journal, "away"))

    def test_provider_admission_refusal_is_not_a_travel_failure(self):
        for fixture in (rows, economic_rows):
            for mode in ("away", "present"):
                with self.subTest(fixture=fixture.__name__, mode=mode):
                    journal = [("VERB-REFUSED", "REFUSED", "unrelated provider admission")]
                    journal += fixture(mode)
                    self.assertEqual(travel.assess(journal, mode, fixture is economic_rows), [])

    def test_provider_admission_refusals_do_not_break_a_matched_pair(self):
        warning = ("VERB-REFUSED", "REFUSED", "unrelated provider admission")
        for fixture in (rows, economic_rows):
            with self.subTest(fixture=fixture.__name__):
                try:
                    result = travel.compare([warning] + fixture("present"),
                                            [warning] + fixture("away"))
                except ValueError as error:
                    self.fail("provider admission warning refused a valid pair: " + str(error))
                self.assertFalse(result["releaseAcceptance"])

    def test_admission_exception_does_not_hide_other_failed_outcomes(self):
        warning = ("VERB-REFUSED", "REFUSED", "unrelated provider admission")
        for verb, outcome in (("VERB-REFUSED", "ERROR"), ("VERB-REFUSED", "FAILED"),
                              ("AUTOSTART", "REFUSED"), ("advance-progress", "REFUSED"),
                              ("travel-refused", "REFUSED"), ("mod-observer", "ERROR")):
            with self.subTest(verb=verb, outcome=outcome):
                self.assertTrue(travel.assess([warning] + rows()
                                             + [(verb, outcome, "actual failure")], "away"))

    def test_economic_pair_proves_only_declared_scope(self):
        self.assertEqual(travel.assess(economic_rows(), "away", True), [])
        self.assertEqual(travel.assess(economic_rows("present"), "present", True), [])
        result = travel.compare(economic_rows("present"), economic_rows())
        self.assertTrue(result["fullEnvelopeStress"])
        self.assertFalse(result["residentStress"])
        self.assertFalse(result["releaseAcceptance"])

    def test_economic_oracle_rejects_double_pause_and_shifted_deadline(self):
        for key, value in (("paused-ticks", "29830"), ("resume-arrival", "31100"),
                           ("resume-applications", "2"), ("pause-local-start", "101"),
                           ("pause-resumed", "100"), ("arrival-interval", "0"),
                           ("containers", "251"), ("stress-initial-thirds", "753"),
                           ("stress-residents", "60"), ("synthetic-fixture", "false"),
                           ("pause-prior", "11"), ("full-envelope-stress", "false")):
            with self.subTest(key=key):
                self.assertTrue(travel.assess(economic_rows(**{key: value}), "away", True))

    def test_economic_recipe_cannot_accept_old_or_mixed_pair(self):
        self.assertTrue(travel.assess(rows(), "away", True))
        with self.assertRaises(ValueError):
            travel.compare(rows("present"), economic_rows())

    def test_economic_setup_steps_are_required_once_in_order(self):
        for name in ("beta-local-pause", "beta-master-pause", "beta-stress"):
            original = economic_rows()
            selected = next(row for row in original if row[0] == name)
            original.remove(selected)
            self.assertTrue(travel.assess(original, "away", True))
            self.assertTrue(travel.assess([selected] + original, "away", True))
            self.assertTrue(travel.assess(economic_rows() + [selected], "away", True))
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
