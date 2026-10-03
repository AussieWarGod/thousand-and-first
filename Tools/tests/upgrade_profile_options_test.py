"""Synthetic option byte contracts only; no fixture here is executed native evidence."""
from __future__ import annotations

import copy
from pathlib import Path
import sys
import unittest

TOOLS = Path(__file__).resolve().parents[1]
sys.path.insert(0, str(TOOLS))
try:
    import upgrade_profile_inputs as inputs
    import upgrade_profile_options as options
finally:
    sys.path.pop(0)

PIN = "b" * 40
BASE = {
    "OptionDisplayFullscreen": "No", "OptionDisplayBrightness": "0", "OptionDisplayContrast": "0",
    "OptionDisplayScanlines": "Yes", "OptionMusicBackground": "No", "OptionAllowCSMods": "Yes",
    "OptionModernUI": "Yes", "OptionPrereleaseStageScale": "auto", "OptionPlayScale": "Fit",
    "OptionTileScale": "1", "OptionShowAdvancedOptions": "Yes", "OptionShowQuickstart": "Yes",
    "r_TAF_OptionDevLog": "Yes", "OptionEnableSeed": "Yes",
}
NATIVE_PREFIX = (b'{\n"OptionDisplayFullscreen":"No",\n"OptionDisplayBrightness":"0",\n'
    b'"OptionDisplayContrast":"0",\n"OptionDisplayScanlines":"Yes",\n"OptionMusicBackground":"No",\n'
    b'"OptionAllowCSMods":"Yes",\n"OptionModernUI":"Yes",\n"OptionPrereleaseStageScale":"auto",\n'
    b'"OptionPlayScale":"Fit",\n"OptionTileScale":"1",\n"OptionShowAdvancedOptions":"Yes",\n'
    b'"OptionShowQuickstart":"Yes",\n"r_TAF_OptionDevLog":"Yes",\n"OptionEnableSeed":"Yes"')


def config(mode: str, schema: str = inputs.PROFILE_V2) -> dict:
    return inputs.configuration(mode, PIN if mode in ("stage-source", "upgrade") else inputs.OLD_PIN,
        PIN, "reader" if mode in ("stage-source", "downgrade") else "inheritance", "#123", schema=schema,
        donor=dict(root="/mnt/c/taf-scenario.Donor", probe=PIN, receiptSha256="a" * 64)
        if mode == "source" and schema == inputs.PROFILE_V2 else None)


def fixture(mode: str) -> tuple[bytes, bytes]:
    initial = dict(BASE)
    if mode == "source":
        # Born opted in (issue #87): 0.3.1's own boot arms the reservation before the roster
        # marker is committed, so the reserved recipe writes no option at all.
        initial[options.IMPORT] = "Yes"
        birth = inputs.json_bytes(initial)
        return birth, birth
    if mode == "source-donor":
        initial[options.IMPORT] = "No"
        tail = b',\n"r_TAF_OptionLegacyImport":"No",\n"r_TAF_OptionSeal":"Yes"'
    else:
        tail = b',\n"r_TAF_OptionGrowth":"No",\n"r_TAF_OptionRaids":"No"'
    return inputs.json_bytes(initial), NATIVE_PREFIX + tail + b'\n}'


class UpgradeProfileOptionsTest(unittest.TestCase):
    def test_full_known_native_payload_for_each_scripted_mode(self):
        for mode in ("source", "source-donor", "stage-source"):
            with self.subTest(mode=mode):
                birth, native = fixture(mode)
                self.assertIsNone(options.validate_options(config(mode), birth, native))

    def test_source_and_donor_native_payloads_cannot_substitute(self):
        birth, source = fixture("source")
        _, donor = fixture("source-donor")
        for mode, wrong in (("source", donor), ("source-donor", source)):
            with self.subTest(mode=mode), self.assertRaises(ValueError):
                options.validate_options(config(mode), birth, wrong)

    def test_unknown_added_removed_and_changed_options_refuse(self):
        birth, native = fixture("stage-source")
        variants = (native.replace(b'\n}', b',\n"unknown":"Yes"\n}'),
                    native.replace(b'"OptionTileScale":"1",\n', b''),
                    native.replace(b'"OptionTileScale":"1"', b'"OptionTileScale":"2"'),
                    native.replace(b'"r_TAF_OptionRaids":"No"', b'"r_TAF_OptionRaids":"Yes"'))
        for wrong in variants:
            with self.subTest(wrong=wrong[-100:]), self.assertRaises(ValueError):
                options.validate_options(config("stage-source"), birth, wrong)

    def test_no_blanket_birth_byte_acceptance_before_required_mutation(self):
        for mode in ("source-donor", "stage-source"):
            birth, _ = fixture(mode)
            with self.subTest(mode=mode), self.assertRaises(ValueError):
                options.validate_options(config(mode), birth, birth)

    def test_exact_birth_only_when_final_map_already_equal(self):
        for mode, writes in (("source", {options.IMPORT: "Yes"}),
                             ("source-donor", {options.IMPORT: "No", options.SEAL: "Yes"}),
                             ("stage-source", {options.GROWTH: "No", options.RAIDS: "No"})):
            initial = dict(BASE, **writes)
            birth = inputs.json_bytes(initial)
            with self.subTest(mode=mode):
                self.assertIsNone(options.validate_options(config(mode), birth, birth))

    def test_existing_key_position_is_retained_not_reappended(self):
        initial = {options.RAIDS: "Yes", "OptionEnableSeed": "Yes", options.GROWTH: "Yes"}
        birth = inputs.json_bytes(initial)
        native = b'{\n"r_TAF_OptionRaids":"No",\n"OptionEnableSeed":"Yes",\n"r_TAF_OptionGrowth":"No"\n}'
        self.assertIsNone(options.validate_options(config("stage-source"), birth, native))
        wrong = b'{\n"OptionEnableSeed":"Yes",\n"r_TAF_OptionGrowth":"No",\n"r_TAF_OptionRaids":"No"\n}'
        with self.assertRaises(ValueError):
            options.validate_options(config("stage-source"), birth, wrong)

    def test_v1_and_readonly_modes_require_exact_birth_bytes(self):
        birth, native = fixture("stage-source")
        cases = [config(mode, inputs.PROFILE_V1) for mode in ("source", "stage-source", "upgrade", "downgrade")]
        cases += [config("upgrade"), config("downgrade")]
        for value in cases:
            with self.subTest(mode=value["mode"], schema=value["schema"]):
                self.assertIsNone(options.validate_options(value, birth, birth))
                with self.assertRaises(ValueError):
                    options.validate_options(value, birth, native)

    def test_duplicate_keys_and_nonstring_values_refuse(self):
        birth, native = fixture("source-donor")
        variants = [native.replace(b'\n}', b',\n"r_TAF_OptionSeal":"Yes"\n}')]
        for token in (b'null', b'false', b'1', b'[]', b'{}'):
            variants.append(native.replace(b'"r_TAF_OptionSeal":"Yes"', b'"r_TAF_OptionSeal":' + token))
        for wrong in variants:
            with self.subTest(wrong=wrong[-100:]), self.assertRaises(ValueError):
                options.validate_options(config("source-donor"), birth, wrong)

    def test_native_framing_escaping_and_encoding_are_exact(self):
        birth, native = fixture("source-donor")
        variants = (native + b'\n', native.replace(b'\n', b'\r\n'), b'\xef\xbb\xbf' + native,
                    native.replace(b'":"', b'": "'), native.replace(b'"Yes"', b'"\\u0059es"'),
                    native.replace(b'"Yes"', b'"\xff"'), native + native)
        for wrong in variants:
            with self.subTest(wrong=wrong[:50]), self.assertRaises(ValueError):
                options.validate_options(config("source-donor"), birth, wrong)

    def test_unsafe_native_vocabulary_and_bad_birth_refuse(self):
        for value in ('quoted"', 'back\\slash', '\n', '\u00e9', '\ud800'):
            birth = inputs.json_bytes(dict(BASE, unsafe=value))
            with self.subTest(value=repr(value)), self.assertRaises(ValueError):
                options.validate_options(config("upgrade"), birth, birth)
        for birth in (b'{}', b'[]', b'null', b'{"x":"1","x":"1"}\n', b'{"x":"1"}', b'\xff'):
            with self.subTest(birth=birth), self.assertRaises(ValueError):
                options.validate_options(config("upgrade"), birth, birth)

    def test_each_old_source_pins_its_own_birth_import_option(self):
        for mode, wrong in (("source", "No"), ("source-donor", "Yes")):
            birth = inputs.json_bytes(dict(BASE, **{options.IMPORT: wrong}))
            with self.subTest(mode=mode), self.assertRaises(ValueError):
                options.validate_options(config(mode), birth, birth)

    def test_reserved_source_permits_no_runtime_option_write(self):
        birth, _ = fixture("source")
        for key, value in ((options.IMPORT, "No"), (options.SEAL, "Yes"), (options.GROWTH, "No")):
            wrong = inputs.json_bytes(dict(BASE, **{options.IMPORT: "Yes", key: value}))
            with self.subTest(key=key), self.assertRaises(ValueError):
                options.validate_options(config("source"), birth, wrong)

    def test_reserved_source_rejects_unchanged_map_in_native_writer_format(self):
        birth, _ = fixture("source")
        native = NATIVE_PREFIX + b',\n"r_TAF_OptionLegacyImport":"Yes"\n}'
        self.assertNotEqual(birth, native)
        self.assertEqual(options._read(birth), options._read(native))
        with self.assertRaisesRegex(ValueError, "no operational option writes"):
            options.validate_options(config("source"), birth, native)

    def test_no_input_mutation_and_bounds(self):
        value = config("source")
        prior = copy.deepcopy(value)
        birth, native = fixture("source")
        options.validate_options(value, birth, native)
        self.assertEqual(value, prior)
        for wrong in (b'', b' ' * (options.MAX_BYTES + 1)):
            with self.subTest(size=len(wrong)), self.assertRaises(ValueError):
                options.validate_options(value, birth, wrong)


if __name__ == "__main__":
    unittest.main()
