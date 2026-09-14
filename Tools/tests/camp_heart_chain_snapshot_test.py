import base64
import sys
from pathlib import Path
import unittest
sys.path.insert(0, str(Path(__file__).resolve().parents[1]))
import camp_heart_chain_snapshot as codec
from heart_chain_fixture import FIXTURES, facts, number, saved, sha


class HeartChainSnapshotTests(unittest.TestCase):
    def test_shared_native_snapshot_and_unicode_fact_fixtures(self):
        wire = (FIXTURES / "heart-chain-save-v1.wire").read_bytes()
        self.assertEqual(saved(), wire)
        decoded = codec.decode_snapshot(wire)
        self.assertEqual((4, 50, 4848, 1728, 31204, 445200),
                         tuple(decoded[key] for key in ("rung", "population", "water", "food", "turns", "time_ticks")))
        wire = (FIXTURES / "heart-chain-facts-v1.wire").read_bytes()
        self.assertEqual({"id": [None, "", "1:a"], "λ": ["a|b", "🌳", "-"]},
                         codec.decode_facts(wire, "custody", sha(wire)))
        self.assertEqual(wire, facts("custody", [["id", None, "", "1:a"], ["λ", "a|b", "🌳", "-"]]))

    def test_every_payload_truncation_and_trailing_byte_refuses(self):
        for wire, prefix, decode in ((saved(), codec.PREFIX, codec.decode_snapshot),
                (facts("jobs", [["id", "value"]]), codec.FACT_PREFIX,
                 lambda raw: codec.decode_facts(raw, "jobs", sha(raw)))):
            body = base64.b64decode(wire[len(prefix):])
            for length in range(len(body)):
                with self.subTest(prefix=prefix, length=length), self.assertRaises(ValueError):
                    decode(prefix + base64.b64encode(body[:length]))
            with self.assertRaises(ValueError):
                decode(prefix + base64.b64encode(body + b"\0"))

    def test_invalid_version_magic_lengths_utf8_and_noncanonical_encoding(self):
        body = base64.b64decode(saved()[len(codec.PREFIX):])
        cases = [codec.PREFIX + base64.b64encode(body[:offset] + number(value) + body[offset + 4:])
                 for offset, value in ((0, 0), (4, 2), (8, -1), (8, 0), (8, 4097), (8, 2147483647))]
        cases += [saved().replace(b"save-v1:", b"save-v2:"), saved() + b"\n", saved() + b"=",
                  codec.PREFIX + base64.b64encode(body[:12] + b"\xff" + body[13:])]
        for wire in cases:
            with self.subTest(wire=wire[:40]), self.assertRaises(ValueError): codec.decode_snapshot(wire)

    def test_identity_counter_and_utf16_bounds(self):
        for changes in ({"game_id": "01234567-89AB-cdef-0123-456789abcdef"}, {"jobs_digest": "A" * 64},
                {"rung": 5}, {"population": 0}, {"water": -1}, {"food": -1}, {"turns": -1}, {"time_ticks": -1},
                {"realm_id": "a\nb"}, {"realm_id": "🌳" * 513}, {"resident_id": "heart"},
                {"basin": dict(id="heart", x=1, y=2)}, {"heart": dict(id="heart", x=4096, y=0)}):
            with self.subTest(changes=changes), self.assertRaises(ValueError): codec.decode_snapshot(saved(**changes))
        self.assertEqual("🌳" * 512, codec.decode_snapshot(saved(realm_id="🌳" * 512))["realm_id"])
        self.assertEqual(3, codec.decode_snapshot(saved(rung=3))["rung"])

    def test_fact_identity_order_null_framing_and_field_bounds(self):
        for rows in ([[None]], [[""]], [["a"], ["a"]], [["b"], ["a"]], [["a\n"]],
                     [["🌳" * 513]], [["a", "x" * 524289]], [["a"] * 65], [[]]):
            wire = facts("jobs", rows)
            with self.subTest(rows=str(rows)[:40]), self.assertRaises(ValueError):
                codec.decode_facts(wire, "jobs", sha(wire))
        # UTF-16 ordinal ordering differs from Python code point ordering here.
        wire = facts("jobs", [["🌳"], ["\ue000"]])
        self.assertEqual(["🌳", "\ue000"], list(codec.decode_facts(wire, "jobs", sha(wire))))
        wire = facts("jobs", [["a", None, "", "x|y"]])
        self.assertEqual([None, "", "x|y"], codec.decode_facts(wire, "jobs", sha(wire))["a"])

    def test_fact_digest_domain_version_counts_and_aggregate_bound(self):
        wire = facts("jobs", [["a", "value"]])
        for domain, digest in (("support", sha(wire)), ("jobs", "a" * 64), ("unknown", sha(wire))):
            with self.assertRaises(ValueError): codec.decode_facts(wire, domain, digest)
        body = base64.b64decode(wire[len(codec.FACT_PREFIX):])
        for offset, value in ((0, 2), (12, -1), (12, 4097), (16, 0), (16, 65)):
            raw = codec.FACT_PREFIX + base64.b64encode(body[:offset] + number(value) + body[offset + 4:])
            with self.subTest(offset=offset, value=value), self.assertRaises(ValueError):
                codec.decode_facts(raw, "jobs", sha(raw))
        raw = facts("jobs", [[str(i), "x" * 524288] for i in range(8)])
        with self.assertRaises(ValueError): codec.decode_facts(raw, "jobs", sha(raw))


if __name__ == "__main__": unittest.main()
