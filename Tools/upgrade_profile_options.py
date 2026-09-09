"""Read-only proof of the exact scripted option writes against the original birth seal."""
from __future__ import annotations

import json

from upgrade_profile_inputs import PROFILE_V1, json_bytes, parse_config, require

MAX_BYTES = 1024 * 1024
MAX_OPTIONS = 4096
IMPORT = "r_TAF_OptionLegacyImport"
SEAL = "r_TAF_OptionSeal"
GROWTH = "r_TAF_OptionGrowth"
RAIDS = "r_TAF_OptionRaids"


def _unique(pairs: list[tuple[str, object]]) -> dict:
    result = {}
    for key, value in pairs:
        require(key not in result, "options JSON repeats a key")
        result[key] = value
    return result


def _read(raw: bytes) -> dict[str, str]:
    require(type(raw) is bytes and 0 < len(raw) <= MAX_BYTES, "options bytes exceed bounds")
    try:
        value = json.loads(raw.decode("utf-8", errors="strict"), object_pairs_hook=_unique)
    except (UnicodeError, ValueError, RecursionError) as error:
        raise ValueError("options JSON is invalid or ambiguous") from error
    require(isinstance(value, dict) and 0 < len(value) <= MAX_OPTIONS, "options must be a bounded object")
    for key, item in value.items():
        require(isinstance(key, str) and 0 < len(key) <= 128
                and isinstance(item, str) and len(item) <= 4096, "option name/value is not a bounded string")
        # NameValueBag.Flush performs no JSON escaping. These pinned profiles contain only
        # printable ASCII without quote/backslash; do not pretend its writer supports more.
        require(all(32 <= ord(c) <= 126 and c not in '\\"' for c in key + item),
                "option name/value is outside the native unescaped vocabulary")
    return value


def validate_options(config: dict, birth: bytes, observed: bytes) -> None:
    """Reject every difference except the exact final map and native wire of known V2 writes.

    Caller must independently derive birth from the pinned recipe, retain its original seal,
    and bind the observed byte hash across final reproof. This function writes nothing.
    """
    config = parse_config(json_bytes(config))
    initial, actual = _read(birth), _read(observed)
    require(birth == json_bytes(initial), "birth options are not the canonical pinned recipe")
    mode = config["mode"]
    if config["schema"] == PROFILE_V1 or mode in ("upgrade", "downgrade"):
        require(observed == birth, "this recipe permits no operational option writes")
        return
    expected = dict(initial)
    if mode == "source":
        # The v2 inheritor is born opted in and writes no option at all: 0.3.1's own boot must arm
        # the reservation before its roster marker is committed (upgrade_profile_inputs.local_inputs).
        require(initial.get(IMPORT) == "Yes", "the v2 inheritor must be born with legacy import enabled")
    elif mode == "source-donor":
        require(initial.get(IMPORT) == "No", "old donor must be born with legacy import disabled")
        expected[SEAL] = "Yes"
    elif mode == "stage-source":
        expected[GROWTH] = "No"
        expected[RAIDS] = "No"
    else:
        raise ValueError("unknown operational options recipe")
    require(actual == expected, "operational options differ from the exact scripted final map")
    if expected == initial and observed == birth:
        return
    # Pinned Options.cs:1070 -> NameValueBag.SetValue:36 -> Flush:67. Existing keys retain
    # position; new keys append in script order. Manual LF wire, no indent or trailing LF.
    native = ('{\n' + ',\n'.join('"' + key + '":"' + value + '"'
                                for key, value in expected.items()) + '\n}').encode("ascii")
    require(observed == native, "operational options do not match the exact native writer bytes")
