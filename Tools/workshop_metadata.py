#!/usr/bin/env python3
"""Validate the exact Qud/Steam metadata used by the Workshop package lane."""

from __future__ import annotations

import argparse
from datetime import datetime
import hashlib
import json
import os
import posixpath
import re
import struct
import sys
import tempfile
import unicodedata
import zlib
from pathlib import Path


MOD_ID = "r_ThousandAndFirst"
TITLE = "The Thousand and First [ALPHA]"
AUTHOR = "AussieWarGod"
TAGS = ("Building", "Faction", "Settlement", "World", "Script", "Lore")
PREVIEW = "preview.png"
GAME_MARKETING_VERSION = "1.0.5"
GAME_CORE_BUILD = "2.0.211.51"
RELEASE_EVIDENCE_SCHEMA = 4
ALPHA_CANDIDATE_SCHEMA = 2
LEGACY_ALPHA_CANDIDATE_SCHEMA = 1
FIRST_ALPHA_RELEASE_VERSION = "0.3.0"
ALPHA_RELEASE_VERSION_PATTERN = re.compile(r"^0\.3\.(?:0|[1-9][0-9]*)$")
ALPHA_RELEASE_CHANNEL = "v0.3 Alpha"
RELEASE_MODES = ("test", "alpha", "release")
VERIFICATION_PASS_IDS = {
    "nativeCompileLoad": "native-compile-load",
    "architectureGallery": "architecture-gallery",
    "controllerAndColor": "controller-color-accessibility",
    "denseCityPerformance": "dense-city-performance",
    "oneSurveyReceipt": "one-survey-receipt",
    "compatibilityMatrix": "compatibility-matrix",
}
MAX_WORKSHOP_ID = (1 << 64) - 1
MAX_PREVIEW_BYTES = (
    1_000_000  # Steam says under 1 MB; use the conservative decimal bound.
)
EVIDENCE_ARTIFACT_ROOT = "docs/release-evidence"
MAX_EVIDENCE_ARTIFACT_BYTES = 512 * 1024 * 1024
MAX_NUMBERED_PROTOCOLS = 1024
MAX_PROTOCOL_WAIVERS = 64
TESTING_PASS_ID_TEXT = r"[0-9]+[a-z0-9]*(?:\.[0-9]+)?"
TESTING_PASS_ID = re.compile(rf"^{TESTING_PASS_ID_TEXT}$")
INTERIM_PREVIEW_SHA256 = (
    "498e85d0f6aba0024845bccece31a427b7b84f680087abd1d6588b8b30e00bad"
)
PREVIEW_REVIEW_PASS_ID = "final-native-preview-review"
HUMAN_SENTINEL = re.compile(
    r"(?:^|[^a-z0-9])(?:placeholder|example|todo|tbd|unknown|n\s*/\s*a)"
    r"(?:$|[^a-z0-9])|human[_ -]*(?:reviewer|tester)|name[_ -]*the|"
    r"replace[_ -]*with|your[_ -]*name",
    re.IGNORECASE,
)

# Keep this grammar deliberately small and bounded. It admits equivalent ways to say that
# durable state may cross a world boundary only by player choice, without treating three
# unrelated appearances of "optional", "legacy", and "world" as a disclosure.
_WORLD_SCOPE_TEXT = (
    r"(?:(?:across|between)\s+worlds"
    r"|into\s+(?:(?:the|your|a)\s+)?(?:next|new|later)\s+world)"
)
_LEGACY_TOPIC_TEXT = r"(?:legacy|inheritance|history|layout|chronicle)"
_LEGACY_ACTION_TEXT = (
    r"(?:leave|leaves|leaving|left|carry|carries|carried|carrying|"
    r"preserve|preserves|preserved|preserving|import|imports|imported|importing)"
)
_LEGACY_CONTINUITY_TEXT = (
    rf"(?:{_LEGACY_ACTION_TEXT}|persist|persists|persisted|"
    r"pass|passes|passed|transfer|transfers|transferred)"
)
_CROSS_WORLD_LEGACY_NOUN_TEXT = (
    r"(?:cross-world\s+(?:legacy|inheritance)"
    r"|(?:legacy|inheritance)\s+(?:across|between)\s+worlds)"
)
_FORWARD_LEGACY_CARRY_TEXT = (
    rf"(?:{_LEGACY_ACTION_TEXT}\b"
    rf"[^.!?\n]{{0,80}}\b{_LEGACY_TOPIC_TEXT}\b"
    rf"[^.!?\n]{{0,80}}\b{_WORLD_SCOPE_TEXT})"
)
_REVERSE_LEGACY_CARRY_TEXT = (
    rf"(?:\b{_LEGACY_TOPIC_TEXT}\b"
    rf"[^.!?\n]{{0,64}}\b{_LEGACY_CONTINUITY_TEXT}\b"
    rf"[^.!?\n]{{0,64}}\b{_WORLD_SCOPE_TEXT})"
)
_CROSS_WORLD_LEGACY_PROPOSITION_TEXT = (
    rf"(?:{_CROSS_WORLD_LEGACY_NOUN_TEXT}"
    rf"|{_FORWARD_LEGACY_CARRY_TEXT}"
    rf"|{_REVERSE_LEGACY_CARRY_TEXT})"
)
_OPTIONAL_CROSS_WORLD_LEGACY = tuple(
    re.compile(pattern, re.IGNORECASE)
    for pattern in (
        rf"\boptional\s+{_CROSS_WORLD_LEGACY_PROPOSITION_TEXT}\b",
        rf"\b(?:an?\s+)?opt[- ]in\s+{_CROSS_WORLD_LEGACY_NOUN_TEXT}\b",
        rf"\boptionally\s+(?:a\s+)?{_CROSS_WORLD_LEGACY_NOUN_TEXT}\b",
        rf"\boptionally\s+{_FORWARD_LEGACY_CARRY_TEXT}\b",
        rf"{_CROSS_WORLD_LEGACY_PROPOSITION_TEXT}[^.!?\n]{{0,32}}"
        rf"\b(?:is|remains|stays)\s+(?:optional|opt[- ]in)\b",
        rf"{_CROSS_WORLD_LEGACY_PROPOSITION_TEXT}[^.!?\n]{{0,48}}"
        rf"\b(?:if\s+you\s+choose|only\s+when\s+(?:you\s+)?enable(?:d)?)\b",
        rf"\bchoose\s+whether\b"
        rf"[^.!?\n]{{0,96}}{_CROSS_WORLD_LEGACY_PROPOSITION_TEXT}\b",
        rf"\bchoose\s+to\s+{_FORWARD_LEGACY_CARRY_TEXT}\b",
        rf"\bopt[- ]in\s+to\s+(?:{_CROSS_WORLD_LEGACY_NOUN_TEXT}"
        rf"|{_FORWARD_LEGACY_CARRY_TEXT})\b",
        rf"\b(?:only\s+)?when\s+(?:you\s+)?enable(?:d)?\b"
        rf"[^.!?\n]{{0,96}}{_CROSS_WORLD_LEGACY_PROPOSITION_TEXT}\b",
    )
)
_NEGATED_CROSS_WORLD_OPTIONALITY = re.compile(
    rf"(?:{_CROSS_WORLD_LEGACY_PROPOSITION_TEXT}[^.!?;\n]{{0,32}}"
    r"\b(?:(?:is|remains|stays)\s+(?:not|never)|isn't)\s+"
    r"(?:optional|opt[- ]in)\b"
    r"|\b(?:not|never)\s+(?:an?\s+)?(?:optional\s+|opt[- ]in\s+)?"
    rf"{_CROSS_WORLD_LEGACY_PROPOSITION_TEXT}\b)",
    re.IGNORECASE,
)
_PREFIXED_LEGACY_CONTRADICTION = re.compile(
    r"\b(?:mandatory|required|automatic|always[- ]on|non[- ]optional)\s+"
    rf"{_CROSS_WORLD_LEGACY_PROPOSITION_TEXT}\b",
    re.IGNORECASE,
)
_MANDATORY_CROSS_WORLD_LEGACY = re.compile(
    rf"{_CROSS_WORLD_LEGACY_PROPOSITION_TEXT}[^.!?\n]{{0,32}}"
    r"\b(?:is|remains|stays)\s+(?:automatic|mandatory|required)\b",
    re.IGNORECASE,
)
_CONTRADICTORY_LEGACY_CARRY = re.compile(
    r"\b(?:the\s+)?(?:cross-world\s+)?(?:legacy|inheritance)\s+"
    r"(?:is\s+)?(?:not\s+optional|mandatory|required|"
    r"(?:always|automatically)\s+(?:carried|preserved|imported|transferred))\b",
    re.IGNORECASE,
)
_NEGATED_CROSS_WORLD_CARRY = re.compile(
    rf"\b(?:do\s+not|don't|never|cannot|can't|will\s+not|choose\s+not\s+to)\s+"
    rf"{_FORWARD_LEGACY_CARRY_TEXT}\b",
    re.IGNORECASE,
)
_NEGATED_CROSS_WORLD_CHOICE = re.compile(
    rf"\b(?:choose\s+(?:whether|to)|opt[- ]in\s+to)\b"
    rf"[^.!?\n]{{0,80}}\b(?:prevent|delete|erase|avoid|stop)\b"
    rf"[^.!?\n]{{0,128}}{_CROSS_WORLD_LEGACY_PROPOSITION_TEXT}\b",
    re.IGNORECASE,
)
_LEGACY_CONTROL_CONTRADICTION = re.compile(
    rf"{_CROSS_WORLD_LEGACY_PROPOSITION_TEXT}[^.!?\n]{{0,64}}(?:"
    r"\b(?:cannot|can't|can\s+not)\s+be\s+(?:disabled|turned\s+off)\b|"
    r"\b(?:has|allows?)\s+no\s+(?:opt[- ]out|off\s+switch)\b|"
    r"\b(?:with\s+no|without(?:\s+an?)?)\s+"
    r"(?:opt[- ]out|off\s+switch)\b|"
    r"\byou\s+(?:cannot|can't|can\s+not)\s+turn\s+it\s+off\b|"
    r"\bthere\s+is\s+no\s+(?:opt[- ]out|off\s+switch)\b|"
    r"\b(?:but|yet)\s+(?:(?:it\s+)?is\s+)?"
    r"(?:mandatory|required|(?:always|automatically)\s+enabled|always[- ]on)\b|"
    r"\b(?:is|stays|remains)\s+(?:always[- ]on|"
    r"(?:always|automatically)\s+enabled)\b)",
    re.IGNORECASE,
)
_LATER_LEGACY_CONTRADICTION = re.compile(
    r"(?:\b(?:it|the\s+legacy|this\s+(?:feature|option|legacy))\s+(?:"
    r"(?:cannot|can't|can\s+not)\s+be\s+(?:disabled|turned\s+off)|"
    r"(?:has|allows?)\s+no\s+(?:opt[- ]out|off\s+switch)|"
    r"(?:is|stays|remains)\s+(?:always[- ]on|"
    r"(?:always|automatically)\s+enabled)|"
    r"(?:is\s+)?(?:not\s+optional|mandatory|required|"
    r"(?:always|automatically)\s+(?:carried|preserved|imported|transferred)))\b"
    r"|\byou\s+(?:cannot|can't|can\s+not)\s+turn\s+it\s+off\b"
    r"|\bthere\s+is\s+no\s+(?:opt[- ]out|off\s+switch)\b)",
    re.IGNORECASE,
)


class ValidationError(ValueError):
    pass


def _require_release_mode(mode: str) -> None:
    if mode not in RELEASE_MODES:
        raise ValidationError("release mode must be test, alpha, or release")


def _discloses_optional_cross_world_legacy(value: str) -> bool:
    normalized = unicodedata.normalize("NFKC", value).casefold()
    normalized = normalized.translate(
        str.maketrans({character: "-" for character in "‐‑‒–—―"})
    )
    normalized = normalized.translate(str.maketrans({"‘": "'", "’": "'"}))
    normalized = re.sub(r"[ \t\f\v]+", " ", normalized)
    if _NEGATED_CROSS_WORLD_OPTIONALITY.search(normalized):
        return False
    if _PREFIXED_LEGACY_CONTRADICTION.search(normalized):
        return False
    if _MANDATORY_CROSS_WORLD_LEGACY.search(normalized):
        return False
    if _CONTRADICTORY_LEGACY_CARRY.search(normalized):
        return False
    if _NEGATED_CROSS_WORLD_CARRY.search(normalized):
        return False
    if _NEGATED_CROSS_WORLD_CHOICE.search(normalized):
        return False
    if _LEGACY_CONTROL_CONTRADICTION.search(normalized):
        return False
    disclosed = False
    for sentence in re.split(r"[.!?\r\n]+", normalized):
        if disclosed and _LATER_LEGACY_CONTRADICTION.search(sentence):
            return False
        if any(pattern.search(sentence) for pattern in _OPTIONAL_CROSS_WORLD_LEGACY):
            disclosed = True
    return disclosed


def _qud_text_error(value: str) -> str | None:
    """Return why Python cannot reproduce Json.NET's default UTF-8 spelling exactly."""
    for character in value:
        codepoint = ord(character)
        if 0xD800 <= codepoint <= 0xDFFF:
            return "contains an unpaired UTF-16 surrogate"
        if codepoint in (0x0085, 0x2028, 0x2029):
            return f"contains U+{codepoint:04X}, whose Qud JSON escaping differs"
    try:
        value.encode("utf-8")
    except UnicodeEncodeError:
        return "cannot be encoded as UTF-8"
    return None


def _load_json(path: Path, *, reject_duplicates: bool = False) -> dict:
    def unique_fields(pairs: list[tuple[str, object]]) -> dict:
        result: dict = {}
        for key, item in pairs:
            if key in result:
                raise ValidationError(f"{path.name} contains duplicate JSON field {key!r}")
            result[key] = item
        return result

    try:
        with path.open(encoding="utf-8-sig") as stream:
            value = json.load(
                stream, object_pairs_hook=unique_fields if reject_duplicates else None
            )
    except (OSError, UnicodeError, json.JSONDecodeError) as error:
        raise ValidationError(f"cannot read {path.name}: {error}") from error
    if not isinstance(value, dict):
        raise ValidationError(f"{path.name} must contain one JSON object")
    return value


def load_manifest(path: Path, require_preview: bool = True) -> dict:
    data = _load_json(path)
    errors: list[str] = []
    description_safe_for_canonical = False
    if data.get("id") != MOD_ID:
        errors.append(f"manifest id must be {MOD_ID}")
    if data.get("title") != TITLE:
        errors.append(f"manifest title must be {TITLE}")
    description = data.get("description")
    if (
        not isinstance(description, str)
        or description != description.strip()
        or len(description) < 80
    ):
        errors.append("manifest description must be a trimmed, current feature summary")
    elif (reason := _qud_text_error(description)) is not None:
        errors.append(f"manifest description {reason}")
    elif len(description.encode("utf-8")) >= 8000:
        errors.append("Workshop Description must be nonempty and under 8000 UTF-8 bytes")
    else:
        description_safe_for_canonical = True
        if "slice 0.1" in description.lower() or "debug wish" in description.lower():
            errors.append("manifest description still describes the 0.1 debug slice")
        elif not _discloses_optional_cross_world_legacy(description):
            errors.append(
                "manifest description must disclose that cross-world legacy is optional"
            )
    version = data.get("version")
    if (
        not isinstance(version, str)
        or re.fullmatch(r"[0-9]+\.[0-9]+\.[0-9]+", version) is None
    ):
        errors.append("manifest version must be numeric major.minor.patch")
    if data.get("author") != AUTHOR:
        errors.append(f"manifest author must be {AUTHOR}")
    if data.get("tags") != ",".join(TAGS):
        errors.append("manifest tags must be exactly " + ",".join(TAGS))
    preview = data.get("PreviewImage")
    if require_preview and preview != PREVIEW:
        errors.append(f"manifest PreviewImage must be {PREVIEW}")
    if not require_preview and preview not in (None, PREVIEW):
        errors.append(f"manifest PreviewImage must be absent or {PREVIEW}")
    if description_safe_for_canonical:
        errors.extend(_text_limits(TITLE, canonical_description(data), TAGS))
    if errors:
        raise ValidationError("; ".join(errors))
    return data


def canonical_description(manifest: dict) -> str:
    paragraphs = (
        "[b]Found a faction. Raise settlements. Leave a history behind.[/b]",
        "The Thousand and First turns settlement-building into part of a normal Caves of "
        "Qud adventure. Pour fresh water into a founder's basin—or choose Kingdom "
        "Quickstart—claim ground, raise a civic heart, and decide what kind of place grows "
        "there. Leave to explore. When you return, your settlements report what happened "
        "while you were away.",
        "This is not a detached city-builder or a daily chore. Water sits in vessels, food "
        "in larders, materials in stores, and citizens live on the ground. You set intent; "
        "your people do the work.",
        "[b]Build a living realm[/b]\n"
        "- Found through a water rite or start quickly through the optional Kingdom Quickstart\n"
        "- Establish a seat and up to two other cities across surface and underground claims\n"
        "- Reserve typed plots in S, M, L, and XL sizes, then choose authored buildings that "
        "renovate, expand, or give way as the settlement grows\n"
        "- Join districts with roads, shafts, utilities, porters, construction routes, and trade\n"
        "- Build homes, workshops, farms, larders, markets, offices, shrines, fortifications, "
        "civic works, and late-game projects\n"
        "- Manage physical fresh water, crops, meals, materials, power, wear, repair, and cargo\n"
        "- Meet named settlers with origins, homes, work, creeds, conversations, petitions, "
        "rites, and funerals\n"
        "- Answer raids by paying, fighting, fortifying, or talking; face diplomacy, dissent, "
        "rivals, exile, return, and succession\n"
        "- Pursue research, certified machinery, laboratories, grafts, a becoming annexe, "
        "a crown, mirror-gates, and a hosted arcology\n"
        "- Read a dated Chronicle and homecoming reports that remember what your realm became",
        "[b]Shape each settlement[/b]\n"
        "Site and circumstance matter. Ground, materials, technology, culture, creed, skills, "
        "staffing, and infrastructure affect which plans open and how the city looks and "
        "works. Choose plots and plans, set civic policy and water detail, commission "
        "upgrades, decide how to answer threats, and invest from hand-carried beginnings "
        "toward carts, conduits, and advanced works.",
        "Affiliations and optional covenants are not cosmetic labels. They can change "
        "architecture, civic practices, relationships, and available projects without "
        "forcing every city in one realm to look alike.",
        "Tune the experience with separate options for growth, water scarcity, raids, trade, "
        "research, civic stories, ambient activity, creed, roads, wear, and more.",
        "Cross-world legacy is opt-in and must be enabled before world creation. It may "
        "carry bounded layout and history into a later world. It never carries items, "
        "liquids, charge, or old actor identity.",
        "[b]Alpha, compatibility, and support[/b]\n"
        "This is a public Alpha playtest. Expect bugs, rough edges, balance changes, and "
        "incomplete visual or compatibility coverage. This listing stays Alpha; Beta and "
        "Release will be separate Workshop items.",
        f"Built for Caves of Qud v{GAME_MARKETING_VERSION}, core build {GAME_CORE_BUILD}. "
        "Later game builds are unverified. No dependency is required. Optional exact-version "
        "Hearthpyre 2.2.3 integration is included when Hearthpyre loads first; native "
        "compatibility remains unverified. Single-player only.",
        "Back up saves before every Alpha install or update. Keep only one enabled copy of "
        "the mod; a local install plus a Workshop subscription can load the wrong one.",
        "Bugs and playtest feedback:\n"
        "[url=https://github.com/AussieWarGod/thousand-and-first/issues/new/choose]"
        "GitHub issue forms[/url]",
        "Install, save, and test guidance:\n"
        "[url=https://github.com/AussieWarGod/thousand-and-first/blob/main/PLAYTESTING.md]"
        "Alpha Playtesting Guide[/url]",
        "Open source under MIT:\n"
        "[url=https://github.com/AussieWarGod/thousand-and-first]"
        "Source and contributor docs[/url]",
    )
    return "\n\n".join(paragraphs)


def _text_limits(title: str, description: str, tags: tuple[str, ...]) -> list[str]:
    errors: list[str] = []
    title_error = _qud_text_error(title)
    description_error = _qud_text_error(description)
    if title_error is not None:
        errors.append(f"Workshop Title {title_error}")
    elif not title or len(title.encode("utf-8")) >= 129:
        errors.append("Workshop Title must be nonempty and under 129 UTF-8 bytes")
    if description_error is not None:
        errors.append(f"Workshop Description {description_error}")
    elif not description or len(description.encode("utf-8")) >= 8000:
        errors.append(
            "Workshop Description must be nonempty and under 8000 UTF-8 bytes"
        )
    for tag in tags:
        reason = _qud_text_error(tag)
        encoded = tag.encode("utf-8") if reason is None else b""
        if (
            reason is not None
            or not tag
            or len(encoded) > 255
            or "," in tag
            or "\x00" in tag
            or not tag.isprintable()
        ):
            errors.append(f"Workshop tag is outside Steam limits: {tag!r}")
    if (
        all(_qud_text_error(tag) is None for tag in tags)
        and len(",".join(tags).encode("utf-8")) >= 1025
    ):
        errors.append("Workshop tag list must be under 1025 UTF-8 bytes")
    return errors


def canonical_workshop_data(manifest: dict, workshop_id: int, visibility: str) -> dict:
    return {
        "WorkshopId": workshop_id,
        "Title": TITLE,
        "Description": canonical_description(manifest),
        "Tags": ",".join(TAGS),
        "Visibility": visibility,
        "ImagePath": PREVIEW,
    }


def canonical_workshop_bytes(data: dict) -> bytes:
    # Qud's ModManager serializer uses Newtonsoft Formatting.Indented on Windows and writes the
    # public fields in SteamWorkshopInfo declaration order, with no terminal newline.
    text = json.dumps(data, ensure_ascii=False, indent=2, separators=(",", ": "))
    return text.replace("\n", "\r\n").encode("utf-8")


def _workshop_id(data: dict) -> int:
    value = data.get("WorkshopId")
    if (
        not isinstance(value, int)
        or isinstance(value, bool)
        or not 0 < value <= MAX_WORKSHOP_ID
    ):
        raise ValidationError("WorkshopId must be an unsigned 64-bit positive integer")
    return value


def canonicalize_workshop(path: Path, manifest: dict, mode: str) -> None:
    """Atomically rewrite Qud's public fields while preserving its published-file ID."""
    _require_release_mode(mode)
    if path.is_symlink() or not path.is_file():
        raise ValidationError(
            "workshop.json canonicalization requires an existing regular non-link file"
        )
    existing = _load_json(path)
    allowed = {"WorkshopId", "Title", "Description", "Tags", "Visibility", "ImagePath"}
    unknown = set(existing) - allowed
    if unknown:
        raise ValidationError(
            "workshop.json has unknown fields: " + ", ".join(sorted(unknown))
        )
    workshop_id = _workshop_id(existing)
    visibility = "0" if mode == "test" else "2"
    payload = canonical_workshop_bytes(
        canonical_workshop_data(manifest, workshop_id, visibility)
    )
    temporary_name: str | None = None
    try:
        descriptor, temporary_name = tempfile.mkstemp(
            prefix=".workshop.json.tmp.", dir=str(path.parent)
        )
        with os.fdopen(descriptor, "wb") as stream:
            stream.write(payload)
            stream.flush()
            os.fsync(stream.fileno())
        os.chmod(temporary_name, path.stat().st_mode & 0o777)
        os.replace(temporary_name, path)
        temporary_name = None
    except OSError as error:
        raise ValidationError(
            f"cannot atomically canonicalize workshop.json: {error}"
        ) from error
    finally:
        if temporary_name is not None:
            try:
                os.unlink(temporary_name)
            except FileNotFoundError:
                pass


def _human_text_valid(value: object, minimum: int, maximum: int) -> bool:
    return (
        isinstance(value, str)
        and value == value.strip()
        and minimum <= len(value) <= maximum
        and value.isprintable()
        and HUMAN_SENTINEL.search(value) is None
        and _qud_text_error(value) is None
    )


def _second_precision_utc(value: object) -> bool:
    if (
        not isinstance(value, str)
        or re.fullmatch(
            r"[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}:[0-9]{2}Z",
            value,
        )
        is None
    ):
        return False
    try:
        datetime.strptime(value, "%Y-%m-%dT%H:%M:%SZ")
    except ValueError:
        return False
    return True


def testing_pass_ids(path: Path) -> tuple[str, ...]:
    """Parse the authoritative individual pass IDs from TESTING.md.

    An ID is a numeric stem followed by lowercase alphanumerics and at most one
    dotted numeric suffix. Any numeric-looking first table
    cell must satisfy that grammar so malformed rows cannot disappear from evidence.
    """
    try:
        lines = path.read_text(encoding="utf-8-sig").splitlines()
    except (OSError, UnicodeError) as error:
        raise ValidationError(
            f"cannot read the authoritative TESTING.md: {error}"
        ) from error
    rows: list[tuple[str, int]] = []
    for line_number, line in enumerate(lines, 1):
        stripped = line.lstrip()
        if not stripped.startswith("|"):
            continue
        fields = stripped.split("|", 2)
        if len(fields) < 3:
            continue
        cell = fields[1].strip()
        if not cell or not cell[0].isdigit():
            continue
        if TESTING_PASS_ID.fullmatch(cell) is None:
            raise ValidationError(
                f"TESTING.md line {line_number} has an invalid individual pass ID: {cell!r}"
            )
        rows.append((cell, line_number))
    if not rows:
        raise ValidationError(
            "authoritative TESTING.md contains no numbered protocol rows"
        )
    locations: dict[str, list[int]] = {}
    for pass_id, line_number in rows:
        locations.setdefault(pass_id, []).append(line_number)
    duplicates = [
        f"{pass_id} (lines {', '.join(map(str, line_numbers))})"
        for pass_id, line_numbers in locations.items()
        if len(line_numbers) != 1
    ]
    if duplicates:
        raise ValidationError(
            "TESTING.md contains ambiguous duplicate pass IDs: " + "; ".join(duplicates)
        )
    return tuple(pass_id for pass_id, _line_number in rows)


def validate_release_claims(
    manifest: dict, readme_path: Path, changelog_path: Path
) -> None:
    version = manifest.get("version")
    if not isinstance(version, str):
        raise ValidationError("release claims require a valid manifest version")
    errors: list[str] = []
    documents: dict[Path, str] = {}
    for path in (readme_path, changelog_path):
        try:
            documents[path] = path.read_text(encoding="utf-8-sig")
        except (OSError, UnicodeError) as error:
            raise ValidationError(
                f"cannot read release evidence document {path.name}: {error}"
            ) from error

    expected_status = f"**Status: {version} public playtest release.**"
    readme_lines = documents[readme_path].splitlines()
    status_indexes = [
        index
        for index, line in enumerate(readme_lines)
        if line.strip().casefold().startswith("**status:")
    ]
    status_lines = [readme_lines[index].strip() for index in status_indexes]
    status_claim = ""
    if len(status_indexes) == 1:
        status_end = status_indexes[0] + 1
        while status_end < len(readme_lines) and readme_lines[status_end].strip():
            status_end += 1
        status_claim = " ".join(
            line.strip() for line in readme_lines[status_indexes[0] : status_end]
        )
    if status_lines != [expected_status]:
        errors.append(
            f"{readme_path.name} must contain exactly one version-bound release status: "
            + expected_status
        )

    changelog_lines = documents[changelog_path].splitlines()
    changelog_indexes = [
        index
        for index, line in enumerate(changelog_lines)
        if line.startswith("## ") and not line.startswith("### ")
    ]
    changelog_headings = [changelog_lines[index].strip() for index in changelog_indexes]
    expected_heading = re.compile(
        rf"^## \[{re.escape(version)}\] — ([0-9]{{4}}-[0-9]{{2}}-[0-9]{{2}})$"
    )
    if (
        not changelog_headings
        or (match := expected_heading.fullmatch(changelog_headings[0])) is None
    ):
        errors.append(
            f"{changelog_path.name} first version heading must bind {version} to a release date"
        )
    else:
        try:
            datetime.strptime(match.group(1), "%Y-%m-%d")
        except ValueError:
            errors.append(
                f"{changelog_path.name} release heading contains an invalid date"
            )

    first_changelog_end = (
        changelog_indexes[1] if len(changelog_indexes) > 1 else len(changelog_lines)
    )
    current_changelog_claim = " ".join(
        line.strip()
        for line in changelog_lines[
            changelog_indexes[0] if changelog_indexes else 0 : first_changelog_end
        ]
    )
    blocked_claims = (
        (r"\bwork in progress\b", "work in progress"),
        (r"\bnot a release candidate\b", "not a release candidate"),
        (r"\bnot (?:once|yet) run in (?:the )?live game\b", "not live-tested"),
        (r"\bno replacement native receipt(?: yet)?\b", "no current native receipt"),
        (r"\b(?:remain|remains|remaining) (?:open|pending)\b", "gates remain open"),
        (
            r"\b(?:outstanding|pending) (?:release |native |human |playtest |testing |steam |structural )?(?:gate|gates|pass|passes)\b",
            "pending gate",
        ),
    )
    for label, claim in (
        (readme_path.name, status_claim),
        (changelog_path.name, current_changelog_claim),
    ):
        for pattern, description in blocked_claims:
            if re.search(pattern, claim, re.IGNORECASE):
                errors.append(f"{label} current release claim still says {description}")
    if errors:
        raise ValidationError("release evidence is still pending; " + "; ".join(errors))


def validate_alpha_claims(
    manifest: dict, readme_path: Path, changelog_path: Path
) -> None:
    """Require public-Alpha wording without claiming final human release evidence."""
    version = manifest.get("version")
    if not isinstance(version, str):
        raise ValidationError("Alpha claims require a valid manifest version")
    errors: list[str] = []
    documents: dict[Path, str] = {}
    for path in (readme_path, changelog_path):
        try:
            documents[path] = path.read_text(encoding="utf-8-sig")
        except (OSError, UnicodeError) as error:
            raise ValidationError(
                f"cannot read Alpha candidate document {path.name}: {error}"
            ) from error

    expected_status = f"**Status: {version} public Alpha playtest.**"
    status_lines = [
        line.strip()
        for line in documents[readme_path].splitlines()
        if line.strip().casefold().startswith("**status:")
    ]
    if status_lines != [expected_status]:
        errors.append(
            f"{readme_path.name} must contain exactly one Alpha-bound status: "
            + expected_status
        )

    changelog_lines = documents[changelog_path].splitlines()
    heading_indexes = [
        index
        for index, line in enumerate(changelog_lines)
        if line.startswith("## ") and not line.startswith("### ")
    ]
    headings = [changelog_lines[index].strip() for index in heading_indexes]
    expected_heading = re.compile(
        rf"^## \[{re.escape(version)}\] — ([0-9]{{4}}-[0-9]{{2}}-[0-9]{{2}}) \(Alpha\)$"
    )
    if not headings or (match := expected_heading.fullmatch(headings[0])) is None:
        errors.append(
            f"{changelog_path.name} first version heading must bind {version} to an Alpha release date"
        )
    else:
        try:
            datetime.strptime(match.group(1), "%Y-%m-%d")
        except ValueError:
            errors.append(
                f"{changelog_path.name} Alpha heading contains an invalid date"
            )

    current_start = heading_indexes[0] if heading_indexes else 0
    current_end = (
        heading_indexes[1] if len(heading_indexes) > 1 else len(changelog_lines)
    )
    current_claim = " ".join(
        line.strip() for line in changelog_lines[current_start:current_end]
    )
    for pattern, description in (
        (r"\bwork in progress\b", "work in progress"),
        (r"\bno Alpha has shipped\b", "Alpha has not shipped"),
        (r"\bnot release-ready\b", "not release-ready"),
        (r"\bnot a release candidate\b", "not a release candidate"),
    ):
        if re.search(pattern, current_claim, re.IGNORECASE):
            errors.append(
                f"{changelog_path.name} current Alpha claim still says {description}"
            )
    if errors:
        raise ValidationError(
            "Alpha candidate claims are invalid; " + "; ".join(errors)
        )


def _validated_alpha_record(record_path: Path) -> dict:
    """Validate intrinsic provenance; external files and subscribed bytes remain separate proofs."""
    record = _load_json(record_path, reject_duplicates=True)
    schema = record.get("schemaVersion")
    keys = {
        "schemaVersion",
        "releaseChannel",
        "releaseVersion",
        "candidateCommit",
        "gameMarketingVersion",
        "gameCoreBuild",
        "workshopId",
        "previewSha256",
        "privatePackageReceiptSha256",
    }
    if schema == ALPHA_CANDIDATE_SCHEMA:
        keys.add("privateWorkshopId")
    errors: list[str] = []
    if set(record) != keys:
        errors.append(
            f"Alpha candidate fields must exactly match schema version {schema}; "
            f"missing={sorted(keys - set(record))}, extra={sorted(set(record) - keys)}"
        )
    if (
        type(schema) is not int
        or schema not in (LEGACY_ALPHA_CANDIDATE_SCHEMA, ALPHA_CANDIDATE_SCHEMA)
    ):
        errors.append("Alpha candidate schemaVersion must be 1 (historical 0.3.0 only) or 2")
    alpha_version = record.get("releaseVersion")
    if (
        not isinstance(alpha_version, str)
        or ALPHA_RELEASE_VERSION_PATTERN.fullmatch(alpha_version) is None
    ):
        errors.append(
            f"Alpha candidate releaseVersion must be {FIRST_ALPHA_RELEASE_VERSION} "
            "or a later canonical 0.3.x patch"
        )
    if schema == LEGACY_ALPHA_CANDIDATE_SCHEMA and alpha_version != FIRST_ALPHA_RELEASE_VERSION:
        errors.append("Alpha candidate schema 1 is historical 0.3.0 only; later patches require schema 2")
    if record.get("releaseChannel") != ALPHA_RELEASE_CHANNEL:
        errors.append(
            f"Alpha candidate releaseChannel must be {ALPHA_RELEASE_CHANNEL!r}"
        )
    candidate = record.get("candidateCommit")
    if (
        not isinstance(candidate, str)
        or re.fullmatch(r"[0-9a-f]{40}", candidate) is None
    ):
        errors.append("Alpha candidateCommit must be a lowercase full Git commit")
    if record.get("gameMarketingVersion") != GAME_MARKETING_VERSION:
        errors.append(
            f"Alpha candidate gameMarketingVersion must be {GAME_MARKETING_VERSION}"
        )
    if record.get("gameCoreBuild") != GAME_CORE_BUILD:
        errors.append(f"Alpha candidate gameCoreBuild must be {GAME_CORE_BUILD}")
    id_fields = ("workshopId", "privateWorkshopId") if schema == ALPHA_CANDIDATE_SCHEMA else ("workshopId",)
    for field in id_fields:
        try:
            _workshop_id({"WorkshopId": record.get(field)})
        except ValidationError as error:
            errors.append(f"Alpha candidate {field}: {error}")
    if schema == ALPHA_CANDIDATE_SCHEMA and record.get("privateWorkshopId") == record.get("workshopId"):
        errors.append("Alpha candidate privateWorkshopId must differ from public workshopId")
    for field in ("previewSha256", "privatePackageReceiptSha256"):
        value = record.get(field)
        if not isinstance(value, str) or re.fullmatch(r"[0-9a-f]{64}", value) is None or value == "0" * 64:
            errors.append(f"Alpha candidate {field} must be a nonzero lowercase SHA-256")
    if record.get("previewSha256") == INTERIM_PREVIEW_SHA256:
        errors.append("Alpha candidate refuses the known interim preview; capture the final native preview")
    if errors:
        raise ValidationError("Alpha candidate is invalid; " + "; ".join(errors))
    return record


def validate_alpha_workshop_binding(
    record_path: Path, private_workshop_path: Path, public_workshop_path: Path
) -> tuple[int, int]:
    """Bind two item identities without writing either; full candidate/file validation is still required."""
    record = _validated_alpha_record(record_path)
    private = _load_json(private_workshop_path, reject_duplicates=True)
    public = _load_json(public_workshop_path, reject_duplicates=True)
    private_id, public_id = _workshop_id(private), _workshop_id(public)
    expected_private = (
        record["workshopId"] if record["schemaVersion"] == LEGACY_ALPHA_CANDIDATE_SCHEMA
        else record["privateWorkshopId"]
    )
    if private.get("Visibility") != "0" or public.get("Visibility") != "2":
        raise ValidationError("Alpha Workshop binding requires exact private Visibility '0' and public Visibility '2'")
    if private_id != expected_private or public_id != record["workshopId"]:
        raise ValidationError("Alpha Workshop binding IDs do not match the validated candidate record")
    return private_id, public_id


def validate_alpha_candidate(
    manifest: dict,
    preview_path: Path,
    workshop_path: Path,
    record_path: Path,
    readme_path: Path,
    changelog_path: Path,
) -> tuple[str, str]:
    """Validate machine provenance for public Alpha without inventing human receipts."""
    validate_alpha_claims(manifest, readme_path, changelog_path)
    validate_preview(preview_path)
    validate_workshop(workshop_path, manifest, "alpha")
    record = _validated_alpha_record(record_path)
    errors: list[str] = []
    if record["releaseVersion"] != manifest.get("version"):
        errors.append("Alpha candidate version must match manifest version")
    workshop_id = _workshop_id(_load_json(workshop_path, reject_duplicates=True))
    if record["workshopId"] != workshop_id:
        errors.append("Alpha candidate workshopId must match workshop.json")
    try:
        preview_hash = hashlib.sha256(preview_path.read_bytes()).hexdigest()
    except OSError as error:
        raise ValidationError(f"cannot hash Alpha preview: {error}") from error
    if record.get("previewSha256") != preview_hash:
        errors.append("Alpha candidate previewSha256 must match preview.png")
    if preview_hash == INTERIM_PREVIEW_SHA256:
        errors.append(
            "Alpha candidate refuses the known interim preview; capture the final native preview"
        )
    if errors:
        raise ValidationError("Alpha candidate is invalid; " + "; ".join(errors))
    return record["candidateCommit"], record["privatePackageReceiptSha256"]


def validate_release_evidence(
    manifest: dict,
    preview_path: Path,
    workshop_path: Path,
    evidence_path: Path,
    readme_path: Path,
    changelog_path: Path,
    *,
    repository_root: Path | None = None,
    testing_path: Path | None = None,
) -> str:
    validate_release_claims(manifest, readme_path, changelog_path)
    evidence = _load_json(evidence_path)
    if repository_root is None:
        repository_root = Path(__file__).resolve().parent.parent
    try:
        repository_root = repository_root.resolve(strict=True)
    except OSError as error:
        raise ValidationError(
            f"cannot resolve release evidence root: {error}"
        ) from error
    if not repository_root.is_dir() or repository_root.is_symlink():
        raise ValidationError("release evidence root must be an ordinary directory")
    if testing_path is None:
        testing_path = repository_root / "TESTING.md"
    top_keys = {
        "schemaVersion",
        "releaseVersion",
        "candidateCommit",
        "gameMarketingVersion",
        "gameCoreBuild",
        "gameAssemblySha256",
        "workshopId",
        "previewSha256",
        "privatePackageReceiptSha256",
        "privateSubscription",
        "verification",
    }
    errors: list[str] = []
    if set(evidence) != top_keys:
        errors.append(
            f"release evidence fields must exactly match schema version {RELEASE_EVIDENCE_SCHEMA}"
        )
    if (
        type(evidence.get("schemaVersion")) is not int
        or evidence.get("schemaVersion") != RELEASE_EVIDENCE_SCHEMA
    ):
        errors.append(
            f"release evidence schemaVersion must be {RELEASE_EVIDENCE_SCHEMA}"
        )
    if evidence.get("releaseVersion") != manifest.get("version"):
        errors.append("release evidence version must match manifest version")
    candidate = evidence.get("candidateCommit")
    if (
        not isinstance(candidate, str)
        or re.fullmatch(r"[0-9a-f]{40}", candidate) is None
    ):
        errors.append(
            "release evidence candidateCommit must be a lowercase full Git commit"
        )
        candidate = ""
    if evidence.get("gameMarketingVersion") != GAME_MARKETING_VERSION:
        errors.append(
            f"release evidence gameMarketingVersion must be {GAME_MARKETING_VERSION}"
        )
    if evidence.get("gameCoreBuild") != GAME_CORE_BUILD:
        errors.append(f"release evidence gameCoreBuild must be {GAME_CORE_BUILD}")
    assembly_hash = evidence.get("gameAssemblySha256")
    if (
        not isinstance(assembly_hash, str)
        or re.fullmatch(r"[0-9a-f]{64}", assembly_hash) is None
        or assembly_hash == "0" * 64
    ):
        errors.append(
            "release evidence gameAssemblySha256 must be a nonzero lowercase SHA-256"
        )

    try:
        workshop_id = _workshop_id(_load_json(workshop_path))
    except ValidationError as error:
        errors.append(str(error))
        workshop_id = None
    evidence_workshop_id = evidence.get("workshopId")
    if type(evidence_workshop_id) is not int or evidence_workshop_id != workshop_id:
        errors.append("release evidence workshopId must match workshop.json")

    try:
        preview_hash = hashlib.sha256(preview_path.read_bytes()).hexdigest()
    except OSError as error:
        raise ValidationError(f"cannot hash release preview: {error}") from error
    if evidence.get("previewSha256") != preview_hash:
        errors.append("release evidence previewSha256 must match preview.png")
    if preview_hash == INTERIM_PREVIEW_SHA256:
        errors.append(
            "release evidence refuses the known interim preview; capture the final native preview"
        )
    receipt_hash = evidence.get("privatePackageReceiptSha256")
    if (
        not isinstance(receipt_hash, str)
        or re.fullmatch(r"[0-9a-f]{64}", receipt_hash) is None
        or receipt_hash == "0" * 64
    ):
        errors.append(
            "release evidence privatePackageReceiptSha256 must be a nonzero lowercase SHA-256"
        )

    verification = evidence.get("verification")
    verification_keys = set(VERIFICATION_PASS_IDS) | {
        "previewReview",
        "numberedProtocols",
    }
    if not isinstance(verification, dict) or set(verification) != verification_keys:
        errors.append(
            "release evidence verification fields must exactly match schema version "
            f"{RELEASE_EVIDENCE_SCHEMA}"
        )
    else:
        for lane, pass_id in VERIFICATION_PASS_IDS.items():
            _validate_artifact_binding(
                verification.get(lane),
                f"verification.{lane}",
                errors,
                repository_root,
                expected_pass_id=pass_id,
            )
        _validate_assembly_receipt(
            verification.get("nativeCompileLoad"),
            assembly_hash,
            errors,
            repository_root,
        )
        preview_review = verification.get("previewReview")
        _validate_artifact_binding(
            preview_review,
            "verification.previewReview",
            errors,
            repository_root,
            expected_pass_id=PREVIEW_REVIEW_PASS_ID,
            extra_keys={
                "source",
                "generativeAssistance",
                "previewSha256",
                "capturedBy",
                "captureUtc",
                "sourceSave",
                "editSummary",
                "reviewedBy",
                "completedUtc",
            },
        )
        if isinstance(preview_review, dict):
            if preview_review.get("source") != "native-game-screenshot":
                errors.append(
                    "release evidence verification.previewReview.source must be 'native-game-screenshot'"
                )
            if (
                type(preview_review.get("generativeAssistance")) is not bool
                or preview_review.get("generativeAssistance") is not False
            ):
                errors.append(
                    "release evidence verification.previewReview.generativeAssistance must be False"
                )
            if preview_review.get("previewSha256") != preview_hash:
                errors.append(
                    "release evidence verification.previewReview.previewSha256 must match preview.png"
                )
            if not _human_text_valid(preview_review.get("capturedBy"), 2, 80):
                errors.append(
                    "release evidence verification.previewReview.capturedBy must name the human capturer"
                )
            if not _second_precision_utc(preview_review.get("captureUtc")):
                errors.append(
                    "release evidence verification.previewReview.captureUtc must be a real second-precision UTC date"
                )
            if not _human_text_valid(preview_review.get("sourceSave"), 5, 200):
                errors.append(
                    "release evidence verification.previewReview.sourceSave must identify the native source save"
                )
            if not _human_text_valid(preview_review.get("editSummary"), 10, 500):
                errors.append(
                    "release evidence verification.previewReview.editSummary must describe the crop and edits"
                )
            if not _human_text_valid(preview_review.get("reviewedBy"), 2, 80):
                errors.append(
                    "release evidence verification.previewReview.reviewedBy must name the human reviewer"
                )
            if not _second_precision_utc(preview_review.get("completedUtc")):
                errors.append(
                    "release evidence verification.previewReview.completedUtc must be a real second-precision UTC date"
                )
        protocols = verification.get("numberedProtocols")
        if not isinstance(protocols, dict) or set(protocols) != {
            "artifactRef",
            "artifactSha256",
            "passIds",
            "waivers",
        }:
            errors.append(
                "release evidence verification.numberedProtocols fields must be "
                "artifactRef, artifactSha256, passIds, and waivers"
            )
        else:
            _validate_artifact_binding(
                protocols,
                "verification.numberedProtocols",
                errors,
                repository_root,
                include_pass_id=False,
                extra_keys={"passIds", "waivers"},
            )
            pass_ids = protocols.get("passIds")
            if (
                not isinstance(pass_ids, list)
                or not pass_ids
                or len(pass_ids) > MAX_NUMBERED_PROTOCOLS
            ):
                errors.append(
                    "release evidence verification.numberedProtocols.passIds must be "
                    "a nonempty bounded list"
                )
                valid_pass_ids = False
                seen: set[str] = set()
            else:
                seen: set[str] = set()
                valid_pass_ids = True
                for pass_id in pass_ids:
                    if (
                        not isinstance(pass_id, str)
                        or TESTING_PASS_ID.fullmatch(pass_id) is None
                    ):
                        errors.append(
                            "release evidence verification.numberedProtocols.passIds "
                            "must contain exact individual TESTING.md IDs"
                        )
                        valid_pass_ids = False
                        break
                    if pass_id in seen:
                        errors.append(
                            "release evidence verification.numberedProtocols.passIds "
                            "must not contain duplicates"
                        )
                        valid_pass_ids = False
                        break
                    seen.add(pass_id)
            waivers = protocols.get("waivers")
            waiver_ids: list[str] = []
            valid_waivers = True
            if not isinstance(waivers, list) or len(waivers) > MAX_PROTOCOL_WAIVERS:
                errors.append(
                    "release evidence verification.numberedProtocols.waivers must be a bounded list"
                )
                valid_waivers = False
            else:
                waiver_seen: set[str] = set()
                waiver_keys = {"passId", "reason", "reviewedBy", "completedUtc"}
                for index, waiver in enumerate(waivers):
                    label = f"verification.numberedProtocols.waivers[{index}]"
                    if not isinstance(waiver, dict) or set(waiver) != waiver_keys:
                        errors.append(
                            f"release evidence {label} fields must be completedUtc, passId, reason, and reviewedBy"
                        )
                        valid_waivers = False
                        continue
                    waiver_id = waiver.get("passId")
                    if (
                        not isinstance(waiver_id, str)
                        or TESTING_PASS_ID.fullmatch(waiver_id) is None
                    ):
                        errors.append(
                            f"release evidence {label}.passId must be one exact TESTING.md ID"
                        )
                        valid_waivers = False
                    elif waiver_id in waiver_seen:
                        errors.append(
                            "release evidence verification.numberedProtocols.waivers must not contain duplicate passIds"
                        )
                        valid_waivers = False
                    else:
                        waiver_seen.add(waiver_id)
                        waiver_ids.append(waiver_id)
                    if not _human_text_valid(waiver.get("reason"), 20, 500):
                        errors.append(
                            f"release evidence {label}.reason must be a bounded human-reviewed reason"
                        )
                        valid_waivers = False
                    if not _human_text_valid(waiver.get("reviewedBy"), 2, 80):
                        errors.append(
                            f"release evidence {label}.reviewedBy must name the human reviewer"
                        )
                        valid_waivers = False
                    if not _second_precision_utc(waiver.get("completedUtc")):
                        errors.append(
                            f"release evidence {label}.completedUtc must be a real second-precision UTC date"
                        )
                        valid_waivers = False

            try:
                defined_rows = testing_pass_ids(testing_path)
            except ValidationError as error:
                errors.append("release evidence " + str(error))
            else:
                defined = set(defined_rows)
                if valid_pass_ids:
                    unknown_passes = sorted(seen - defined)
                    if unknown_passes:
                        errors.append(
                            "release evidence verification.numberedProtocols.passIds "
                            "are absent from TESTING.md: " + ", ".join(unknown_passes)
                        )
                if valid_waivers:
                    unknown_waivers = sorted(set(waiver_ids) - defined)
                    if unknown_waivers:
                        errors.append(
                            "release evidence verification.numberedProtocols.waivers "
                            "name IDs absent from TESTING.md: "
                            + ", ".join(unknown_waivers)
                        )
                if valid_pass_ids and valid_waivers:
                    overlap = sorted(seen & set(waiver_ids))
                    if overlap:
                        errors.append(
                            "release evidence numbered protocol IDs cannot be both passed and waived: "
                            + ", ".join(overlap)
                        )
                    missing = [
                        pass_id
                        for pass_id in defined_rows
                        if pass_id not in seen and pass_id not in set(waiver_ids)
                    ]
                    if missing:
                        errors.append(
                            "release evidence is missing TESTING.md IDs without a human-reviewed waiver: "
                            + ", ".join(missing)
                        )
                    expected_passes = [
                        pass_id
                        for pass_id in defined_rows
                        if pass_id not in set(waiver_ids)
                    ]
                    expected_waivers = [
                        pass_id
                        for pass_id in defined_rows
                        if pass_id in set(waiver_ids)
                    ]
                    if pass_ids != expected_passes:
                        errors.append(
                            "release evidence numbered protocol passIds must follow authoritative TESTING.md order"
                        )
                    if waiver_ids != expected_waivers:
                        errors.append(
                            "release evidence numbered protocol waivers must follow authoritative TESTING.md order"
                        )

    private = evidence.get("privateSubscription")
    private_keys = {
        "source",
        "inventory",
        "receipt",
        "loader",
        "newGame",
        "saveReload",
        "oldSave",
        "representativeFeatures",
        "playerLog",
        "localDuplicatesRemoved",
        "uploadHiddenFiles",
        "testedBy",
        "completedUtc",
    }
    if not isinstance(private, dict) or set(private) != private_keys:
        errors.append(
            "release evidence privateSubscription fields must exactly match schema version "
            f"{RELEASE_EVIDENCE_SCHEMA}"
        )
    else:
        expected = {
            "source": "steam-subscription",
            "inventory": "clean",
            "receipt": "clean",
            "loader": "passed",
            "newGame": "passed",
            "saveReload": "passed",
            "oldSave": "passed",
            "representativeFeatures": "passed",
            "playerLog": "clean",
            "localDuplicatesRemoved": True,
            "uploadHiddenFiles": True,
        }
        for key, value in expected.items():
            actual = private.get(key)
            if type(actual) is not type(value) or actual != value:
                errors.append(
                    f"release evidence privateSubscription.{key} must be {value!r}"
                )
        tester = private.get("testedBy")
        if not _human_text_valid(tester, 2, 80):
            errors.append("release evidence testedBy must name the human tester")
        completed = private.get("completedUtc")
        if not _second_precision_utc(completed):
            errors.append(
                "release evidence completedUtc must be a real second-precision UTC date"
            )
    if errors:
        raise ValidationError("release evidence is invalid; " + "; ".join(errors))
    return candidate


def _validate_artifact_binding(
    value: object,
    label: str,
    errors: list[str],
    repository_root: Path,
    expected_pass_id: str | None = None,
    include_pass_id: bool = True,
    extra_keys: set[str] | None = None,
) -> None:
    keys = {"artifactRef", "artifactSha256"}
    if include_pass_id:
        keys.add("passId")
    if extra_keys:
        keys.update(extra_keys)
    if not isinstance(value, dict) or set(value) != keys:
        errors.append(
            f"release evidence {label} fields must be " + ", ".join(sorted(keys))
        )
        return
    if include_pass_id and value.get("passId") != expected_pass_id:
        errors.append(f"release evidence {label}.passId must be {expected_pass_id!r}")
    artifact_ref = value.get("artifactRef")
    if (
        not isinstance(artifact_ref, str)
        or artifact_ref != artifact_ref.strip()
        or not 3 <= len(artifact_ref) <= 512
        or artifact_ref.casefold() in {"todo", "tbd", "unknown", "n/a"}
        or "placeholder" in artifact_ref.casefold()
        or "artifact_reference" in artifact_ref.casefold()
        or _qud_text_error(artifact_ref) is not None
        or not _safe_evidence_artifact_ref(artifact_ref)
    ):
        errors.append(
            f"release evidence {label}.artifactRef must identify the retained artifact"
        )
        artifact_ref = None
    artifact_hash = value.get("artifactSha256")
    if (
        not isinstance(artifact_hash, str)
        or re.fullmatch(r"[0-9a-f]{64}", artifact_hash) is None
        or artifact_hash == "0" * 64
    ):
        errors.append(
            f"release evidence {label}.artifactSha256 must be a nonzero lowercase SHA-256"
        )
        artifact_hash = None
    if artifact_ref is not None and artifact_hash is not None:
        try:
            artifact_path = repository_root.joinpath(*artifact_ref.split("/"))
            resolved = artifact_path.resolve(strict=True)
            resolved.relative_to(repository_root)
            current = repository_root
            for component in artifact_ref.split("/"):
                current = current / component
                if current.is_symlink():
                    raise OSError("artifact path contains a symbolic link")
            if not resolved.is_file():
                raise OSError("artifact is not a regular file")
            if resolved.stat().st_size > MAX_EVIDENCE_ARTIFACT_BYTES:
                raise OSError("artifact exceeds the evidence size cap")
            digest = hashlib.sha256()
            with resolved.open("rb") as stream:
                for block in iter(lambda: stream.read(1024 * 1024), b""):
                    digest.update(block)
        except (OSError, RuntimeError, ValueError) as error:
            errors.append(
                f"release evidence {label}.artifactRef cannot read retained artifact: {error}"
            )
        else:
            if digest.hexdigest() != artifact_hash:
                errors.append(
                    f"release evidence {label}.artifactSha256 must match retained artifact"
                )


def _validate_assembly_receipt(
    value: object, expected_hash: object, errors: list[str], repository_root: Path
) -> None:
    """Bind the declared licensed game binary to the retained native transcript."""
    if (
        not isinstance(value, dict)
        or not isinstance(value.get("artifactRef"), str)
        or not isinstance(expected_hash, str)
        or re.fullmatch(r"[0-9a-f]{64}", expected_hash) is None
    ):
        return
    artifact_ref = value["artifactRef"]
    if not _safe_evidence_artifact_ref(artifact_ref):
        return
    try:
        path = repository_root.joinpath(*artifact_ref.split("/"))
        resolved = path.resolve(strict=True)
        resolved.relative_to(repository_root)
        if path.is_symlink() or not resolved.is_file():
            raise OSError("artifact is not a regular file")
        text = resolved.read_text(encoding="utf-8-sig")
    except (OSError, UnicodeError, ValueError) as error:
        errors.append(
            "release evidence verification.nativeCompileLoad cannot read assembly receipt: "
            + str(error)
        )
        return
    receipts = []
    for line in text.splitlines():
        match = re.fullmatch(r"Assembly-CSharp SHA-256: ([0-9a-f]{64})", line)
        if match is not None:
            receipts.append(match.group(1))
    if receipts != [expected_hash]:
        errors.append(
            "release evidence gameAssemblySha256 must match the unique "
            "Assembly-CSharp SHA-256 receipt in verification.nativeCompileLoad"
        )


def _safe_evidence_artifact_ref(value: str) -> bool:
    prefix = EVIDENCE_ARTIFACT_ROOT + "/"
    if not value.startswith(prefix) or value.startswith("/") or "\\" in value:
        return False
    components = value.split("/")
    if (
        posixpath.normpath(value) != value
        or any(component in ("", ".", "..") for component in components)
        or any(unicodedata.category(character).startswith("C") for character in value)
    ):
        return False
    reserved = re.compile(
        r"^(?:CON|PRN|AUX|NUL|CONIN\$|CONOUT\$|"
        r"COM[1-9\u00b9\u00b2\u00b3]|LPT[1-9\u00b9\u00b2\u00b3])$",
        re.IGNORECASE,
    )
    for component in components:
        if (
            any(character in '<>:"|?*' for character in component)
            or component.endswith((".", " "))
            or reserved.fullmatch(component.rstrip(". ").split(".", 1)[0])
        ):
            return False
    return True


def release_evidence_artifact_refs(path: Path) -> tuple[str, ...]:
    """List every safe retained-artifact path before immutable extraction.

    Full schema and hash validation happens after extraction. This first pass is deliberately
    narrow: it discovers every artifactRef anywhere in the record, refuses unsafe spellings,
    and returns one bytewise-sorted path per committed blob.
    """
    evidence = _load_json(path)
    refs: list[str] = []

    def visit(value: object) -> None:
        if isinstance(value, dict):
            if "artifactRef" in value:
                artifact_ref = value["artifactRef"]
                if not isinstance(artifact_ref, str) or not _safe_evidence_artifact_ref(
                    artifact_ref
                ):
                    raise ValidationError(
                        "release evidence contains an unsafe artifactRef before extraction"
                    )
                refs.append(artifact_ref)
            for child in value.values():
                visit(child)
        elif isinstance(value, list):
            for child in value:
                visit(child)

    visit(evidence)
    if not refs:
        raise ValidationError("release evidence contains no retained artifactRef")
    duplicates = sorted(
        {artifact_ref for artifact_ref in refs if refs.count(artifact_ref) > 1},
        key=lambda value: value.encode("utf-8"),
    )
    if duplicates:
        raise ValidationError(
            "release evidence reuses retained artifactRef: " + ", ".join(duplicates)
        )
    return tuple(sorted(refs, key=lambda value: value.encode("utf-8")))


def validate_workshop(path: Path, manifest: dict, mode: str) -> dict | None:
    _require_release_mode(mode)
    if not path.exists():
        if mode == "test":
            return None
        raise ValidationError(
            "release package requires workshop.json created by Qud's private-item flow"
        )
    data = _load_json(path)
    expected_keys = (
        "WorkshopId",
        "Title",
        "Description",
        "Tags",
        "Visibility",
        "ImagePath",
    )
    errors: list[str] = []
    if tuple(data.keys()) != expected_keys:
        errors.append(
            "workshop.json fields/order must match Qud's completed serializer shape"
        )
    workshop_id = data.get("WorkshopId")
    try:
        workshop_id = _workshop_id(data)
    except ValidationError as error:
        errors.append(str(error))
        workshop_id = 1
    expected_visibility = "0" if mode == "test" else "2"
    expected = canonical_workshop_data(manifest, workshop_id, expected_visibility)
    for key, value in expected.items():
        if data.get(key) != value:
            errors.append(f"{key} must exactly match the {mode} release metadata")
    errors.extend(
        _text_limits(
            data.get("Title") if isinstance(data.get("Title"), str) else "",
            data.get("Description") if isinstance(data.get("Description"), str) else "",
            tuple(data.get("Tags", "").split(","))
            if isinstance(data.get("Tags"), str)
            else (),
        )
    )
    if not errors and path.read_bytes() != canonical_workshop_bytes(expected):
        errors.append(
            "workshop.json bytes are not Qud's canonical Windows serializer output"
        )
    if errors:
        raise ValidationError("; ".join(errors))
    return data


def validate_preview(path: Path) -> None:
    try:
        payload = path.read_bytes()
    except OSError as error:
        raise ValidationError(f"Workshop preview is missing: {path.name}") from error
    if len(payload) >= MAX_PREVIEW_BYTES:
        raise ValidationError("Workshop preview must be under 1,000,000 bytes")
    if not payload.startswith(b"\x89PNG\r\n\x1a\n"):
        raise ValidationError("Workshop preview is not a PNG")

    offset = 8
    chunks: list[tuple[bytes, bytes]] = []
    while offset < len(payload):
        if len(payload) - offset < 12:
            raise ValidationError("Workshop preview has a truncated PNG chunk")
        length = struct.unpack(">I", payload[offset : offset + 4])[0]
        kind = payload[offset + 4 : offset + 8]
        if len(kind) != 4 or not kind.isalpha() or kind[2] & 0x20:
            raise ValidationError("Workshop preview has an invalid PNG chunk type")
        if not kind[0] & 0x20 and kind not in (b"IHDR", b"PLTE", b"IDAT", b"IEND"):
            raise ValidationError("Workshop preview has an unknown critical PNG chunk")
        end = offset + 12 + length
        if end > len(payload):
            raise ValidationError("Workshop preview has a truncated PNG chunk body")
        body = payload[offset + 8 : offset + 8 + length]
        expected_crc = struct.unpack(">I", payload[offset + 8 + length : end])[0]
        if zlib.crc32(kind + body) & 0xFFFFFFFF != expected_crc:
            raise ValidationError("Workshop preview has a bad PNG chunk checksum")
        chunks.append((kind, body))
        offset = end
        if kind == b"IEND":
            break
    if offset != len(payload) or not chunks or chunks[-1] != (b"IEND", b""):
        raise ValidationError("Workshop preview must end with one complete IEND chunk")
    if chunks[0][0] != b"IHDR" or len(chunks[0][1]) != 13:
        raise ValidationError("Workshop preview must begin with a complete IHDR chunk")
    if (
        sum(kind == b"IHDR" for kind, _ in chunks) != 1
        or sum(kind == b"IEND" for kind, _ in chunks) != 1
    ):
        raise ValidationError("Workshop preview has duplicate structural PNG chunks")

    width, height, depth, color, compression, filtering, interlace = struct.unpack(
        ">IIBBBBB", chunks[0][1]
    )
    if (width, height) != (512, 512):
        raise ValidationError(f"Workshop preview must be 512x512, got {width}x{height}")
    if (
        depth != 8
        or color not in (2, 6)
        or compression != 0
        or filtering != 0
        or interlace != 0
    ):
        raise ValidationError(
            "Workshop preview must be non-interlaced 8-bit RGB or RGBA PNG"
        )
    idat_indexes = [index for index, (kind, _) in enumerate(chunks) if kind == b"IDAT"]
    if not idat_indexes or idat_indexes != list(
        range(idat_indexes[0], idat_indexes[-1] + 1)
    ):
        raise ValidationError(
            "Workshop preview must contain contiguous image-data chunks"
        )
    plte_indexes = [index for index, (kind, _) in enumerate(chunks) if kind == b"PLTE"]
    if len(plte_indexes) > 1:
        raise ValidationError("Workshop preview has duplicate palette chunks")
    if plte_indexes:
        palette = chunks[plte_indexes[0]][1]
        if len(palette) < 3 or len(palette) > 768 or len(palette) % 3:
            raise ValidationError(
                "Workshop preview has an invalid palette chunk length"
            )
        if plte_indexes[0] > idat_indexes[0]:
            raise ValidationError("Workshop preview palette must precede image data")
    compressed = b"".join(chunks[index][1] for index in idat_indexes)
    decoder = zlib.decompressobj()
    channels = 3 if color == 2 else 4
    row_bytes = width * channels
    expected_decoded = height * (row_bytes + 1)
    try:
        raw = decoder.decompress(compressed, expected_decoded + 1)
        if len(raw) > expected_decoded or decoder.unconsumed_tail:
            raise ValidationError(
                "Workshop preview decoded image exceeds its declared size"
            )
        raw += decoder.flush(expected_decoded + 1 - len(raw))
    except zlib.error as error:
        raise ValidationError(
            f"Workshop preview image data cannot be decoded: {error}"
        ) from error
    if len(raw) > expected_decoded:
        raise ValidationError(
            "Workshop preview decoded image exceeds its declared size"
        )
    if not decoder.eof or decoder.unused_data or decoder.unconsumed_tail:
        raise ValidationError(
            "Workshop preview image data is incomplete or has trailing bytes"
        )
    if len(raw) != expected_decoded:
        raise ValidationError("Workshop preview decoded image size is inconsistent")
    if any(raw[row * (row_bytes + 1)] > 4 for row in range(height)):
        raise ValidationError("Workshop preview uses an invalid PNG row filter")


def _copy(manifest: dict) -> str:
    return (
        f"Title:\n{TITLE}\n\nDescription:\n{canonical_description(manifest)}\n\n"
        f"Tags:\n{','.join(TAGS)}"
    )


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser()
    subparsers = parser.add_subparsers(dest="command", required=True)
    fields = subparsers.add_parser("fields")
    fields.add_argument("manifest", type=Path)
    copy = subparsers.add_parser("copy")
    copy.add_argument("manifest", type=Path)
    preview = subparsers.add_parser("preview")
    preview.add_argument("path", type=Path)
    workshop = subparsers.add_parser("workshop")
    workshop.add_argument("mode", choices=RELEASE_MODES)
    workshop.add_argument("manifest", type=Path)
    workshop.add_argument("path", type=Path)
    canonicalize = subparsers.add_parser("canonicalize")
    canonicalize.add_argument("mode", choices=RELEASE_MODES)
    canonicalize.add_argument("manifest", type=Path)
    canonicalize.add_argument("path", type=Path)
    evidence = subparsers.add_parser("evidence")
    evidence.add_argument("manifest", type=Path)
    evidence.add_argument("preview", type=Path)
    evidence.add_argument("workshop", type=Path)
    evidence.add_argument("record", type=Path)
    evidence.add_argument("readme", type=Path)
    evidence.add_argument("changelog", type=Path)
    evidence.add_argument("--repository-root", type=Path)
    evidence.add_argument("--testing", type=Path)
    alpha_candidate = subparsers.add_parser("alpha-candidate")
    alpha_candidate.add_argument("manifest", type=Path)
    alpha_candidate.add_argument("preview", type=Path)
    alpha_candidate.add_argument("workshop", type=Path)
    alpha_candidate.add_argument("record", type=Path)
    alpha_candidate.add_argument("readme", type=Path)
    alpha_candidate.add_argument("changelog", type=Path)
    alpha_binding = subparsers.add_parser("alpha-workshop-binding")
    alpha_binding.add_argument("record", type=Path)
    alpha_binding.add_argument("private_workshop", type=Path)
    alpha_binding.add_argument("public_workshop", type=Path)
    artifact_refs = subparsers.add_parser("evidence-artifact-refs")
    artifact_refs.add_argument("record", type=Path)
    workshop_id = subparsers.add_parser("workshop-id")
    workshop_id.add_argument("path", type=Path)
    testing_ids = subparsers.add_parser("testing-pass-ids")
    testing_ids.add_argument("path", type=Path)
    args = parser.parse_args(argv)
    try:
        if args.command == "fields":
            manifest = load_manifest(args.manifest, require_preview=True)
            for value in (
                manifest["version"],
                manifest["title"],
                manifest["PreviewImage"],
            ):
                print(value)
        elif args.command == "copy":
            print(_copy(load_manifest(args.manifest, require_preview=False)))
        elif args.command == "preview":
            validate_preview(args.path)
        elif args.command == "workshop":
            manifest = load_manifest(args.manifest, require_preview=True)
            validate_workshop(args.path, manifest, args.mode)
        elif args.command == "canonicalize":
            manifest = load_manifest(args.manifest, require_preview=True)
            canonicalize_workshop(args.path, manifest, args.mode)
            validate_workshop(args.path, manifest, args.mode)
        elif args.command == "evidence":
            manifest = load_manifest(args.manifest, require_preview=True)
            print(
                validate_release_evidence(
                    manifest,
                    args.preview,
                    args.workshop,
                    args.record,
                    args.readme,
                    args.changelog,
                    repository_root=args.repository_root,
                    testing_path=args.testing,
                )
            )
        elif args.command == "alpha-candidate":
            manifest = load_manifest(args.manifest, require_preview=True)
            for value in validate_alpha_candidate(
                manifest,
                args.preview,
                args.workshop,
                args.record,
                args.readme,
                args.changelog,
            ):
                print(value)
        elif args.command == "evidence-artifact-refs":
            for artifact_ref in release_evidence_artifact_refs(args.record):
                print(artifact_ref)
        elif args.command == "alpha-workshop-binding":
            validate_alpha_workshop_binding(args.record, args.private_workshop, args.public_workshop)
        elif args.command == "workshop-id":
            print(_workshop_id(_load_json(args.path)))
        elif args.command == "testing-pass-ids":
            print(json.dumps(testing_pass_ids(args.path), indent=2))
    except ValidationError as error:
        print(str(error), file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
