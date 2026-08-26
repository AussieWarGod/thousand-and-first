#!/usr/bin/env python3
"""Validate the exact Qud/Steam metadata used by the Workshop package lane."""

from __future__ import annotations

import argparse
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
TITLE = "The Thousand and First"
AUTHOR = "AussieWarGod"
TAGS = ("Beta", "Faction", "Settlement", "Script")
PREVIEW = "preview.png"
GAME_MARKETING_VERSION = "1.0.5"
GAME_CORE_BUILD = "2.0.211.51"
RELEASE_EVIDENCE_SCHEMA = 3
VERIFICATION_PASS_IDS = {
    "nativeCompileLoad": "native-compile-load",
    "architectureGallery": "architecture-gallery",
    "controllerAndColor": "controller-color-accessibility",
    "denseCityPerformance": "dense-city-performance",
    "oneSurveyReceipt": "one-survey-receipt",
    "compatibilityMatrix": "compatibility-matrix",
}
MAX_WORKSHOP_ID = (1 << 64) - 1
MAX_PREVIEW_BYTES = 1_000_000  # Steam says under 1 MB; use the conservative decimal bound.
EVIDENCE_ARTIFACT_ROOT = "docs/release-evidence"
MAX_EVIDENCE_ARTIFACT_BYTES = 512 * 1024 * 1024


class ValidationError(ValueError):
    pass


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


def _load_json(path: Path) -> dict:
    try:
        with path.open(encoding="utf-8-sig") as stream:
            value = json.load(stream)
    except (OSError, UnicodeError, json.JSONDecodeError) as error:
        raise ValidationError(f"cannot read {path.name}: {error}") from error
    if not isinstance(value, dict):
        raise ValidationError(f"{path.name} must contain one JSON object")
    return value


def load_manifest(path: Path, require_preview: bool = True) -> dict:
    data = _load_json(path)
    errors: list[str] = []
    if data.get("id") != MOD_ID:
        errors.append(f"manifest id must be {MOD_ID}")
    if data.get("title") != TITLE:
        errors.append(f"manifest title must be {TITLE}")
    description = data.get("description")
    if not isinstance(description, str) or description != description.strip() or len(description) < 80:
        errors.append("manifest description must be a trimmed, current feature summary")
    elif "slice 0.1" in description.lower() or "debug wish" in description.lower():
        errors.append("manifest description still describes the 0.1 debug slice")
    elif "optionally" not in description.lower() or "legacy across worlds" not in description.lower():
        errors.append("manifest description must disclose that cross-world legacy is optional")
    elif (reason := _qud_text_error(description)) is not None:
        errors.append(f"manifest description {reason}")
    version = data.get("version")
    if not isinstance(version, str) or re.fullmatch(r"[0-9]+\.[0-9]+\.[0-9]+", version) is None:
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
    if isinstance(description, str):
        errors.extend(_text_limits(TITLE, canonical_description(data), TAGS))
    if errors:
        raise ValidationError("; ".join(errors))
    return data


def canonical_description(manifest: dict) -> str:
    return (
        f"Pre-release playtest build for Caves of Qud v{GAME_MARKETING_VERSION} "
        f"(core {GAME_CORE_BUILD}).\n\n"
        + manifest["description"]
        + "\n\nBack up your saves before testing. Report issues at "
        "https://github.com/AussieWarGod/thousand-and-first/issues. "
        "This is an unofficial community mod, not affiliated with or endorsed by Freehold Games."
    )


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
        errors.append("Workshop Description must be nonempty and under 8000 UTF-8 bytes")
    for tag in tags:
        reason = _qud_text_error(tag)
        encoded = tag.encode("utf-8") if reason is None else b""
        if (reason is not None or not tag or len(encoded) > 255 or "," in tag
                or "\x00" in tag or not tag.isprintable()):
            errors.append(f"Workshop tag is outside Steam limits: {tag!r}")
    if all(_qud_text_error(tag) is None for tag in tags) and len(",".join(tags).encode("utf-8")) >= 1025:
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
    if (not isinstance(value, int) or isinstance(value, bool)
            or not 0 < value <= MAX_WORKSHOP_ID):
        raise ValidationError("WorkshopId must be an unsigned 64-bit positive integer")
    return value


def canonicalize_workshop(path: Path, manifest: dict, mode: str) -> None:
    """Atomically rewrite Qud's public fields while preserving its published-file ID."""
    if path.is_symlink() or not path.is_file():
        raise ValidationError("workshop.json canonicalization requires an existing regular non-link file")
    existing = _load_json(path)
    allowed = {"WorkshopId", "Title", "Description", "Tags", "Visibility", "ImagePath"}
    unknown = set(existing) - allowed
    if unknown:
        raise ValidationError("workshop.json has unknown fields: " + ", ".join(sorted(unknown)))
    workshop_id = _workshop_id(existing)
    visibility = "0" if mode == "test" else "2"
    payload = canonical_workshop_bytes(canonical_workshop_data(manifest, workshop_id, visibility))
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
        raise ValidationError(f"cannot atomically canonicalize workshop.json: {error}") from error
    finally:
        if temporary_name is not None:
            try:
                os.unlink(temporary_name)
            except FileNotFoundError:
                pass


def validate_release_claims(readme_path: Path, changelog_path: Path) -> None:
    markers = (
        "not once run in the live game",
        "not yet run in a live game",
        "the playtest protocol in [testing.md](testing.md) is the outstanding gate",
        "nothing here is stable until the first playtest passes",
    )
    errors: list[str] = []
    for path in (readme_path, changelog_path):
        try:
            text = path.read_text(encoding="utf-8-sig").lower()
        except (OSError, UnicodeError) as error:
            raise ValidationError(f"cannot read release evidence document {path.name}: {error}") from error
        for marker in markers:
            if marker in text:
                errors.append(f"{path.name} still says: {marker}")
        if path == changelog_path:
            for line in text.splitlines():
                if "[unreleased]" in line and "in progress" in line:
                    errors.append(f"{path.name} still declares an in-progress unreleased version")
                    break
    if errors:
        raise ValidationError("release evidence is still pending; " + "; ".join(errors))


def validate_release_evidence(manifest: dict, preview_path: Path, workshop_path: Path,
                              evidence_path: Path, readme_path: Path,
                              changelog_path: Path) -> str:
    validate_release_claims(readme_path, changelog_path)
    evidence = _load_json(evidence_path)
    top_keys = {
        "schemaVersion", "releaseVersion", "candidateCommit", "gameMarketingVersion",
        "gameCoreBuild", "gameAssemblySha256", "workshopId", "previewSha256",
        "privatePackageReceiptSha256", "privateSubscription", "verification",
    }
    errors: list[str] = []
    if set(evidence) != top_keys:
        errors.append(
            f"release evidence fields must exactly match schema version {RELEASE_EVIDENCE_SCHEMA}"
        )
    if (type(evidence.get("schemaVersion")) is not int
            or evidence.get("schemaVersion") != RELEASE_EVIDENCE_SCHEMA):
        errors.append(
            f"release evidence schemaVersion must be {RELEASE_EVIDENCE_SCHEMA}"
        )
    if evidence.get("releaseVersion") != manifest.get("version"):
        errors.append("release evidence version must match manifest version")
    candidate = evidence.get("candidateCommit")
    if not isinstance(candidate, str) or re.fullmatch(r"[0-9a-f]{40}", candidate) is None:
        errors.append("release evidence candidateCommit must be a lowercase full Git commit")
        candidate = ""
    if evidence.get("gameMarketingVersion") != GAME_MARKETING_VERSION:
        errors.append(
            f"release evidence gameMarketingVersion must be {GAME_MARKETING_VERSION}"
        )
    if evidence.get("gameCoreBuild") != GAME_CORE_BUILD:
        errors.append(f"release evidence gameCoreBuild must be {GAME_CORE_BUILD}")
    assembly_hash = evidence.get("gameAssemblySha256")
    if (not isinstance(assembly_hash, str)
            or re.fullmatch(r"[0-9a-f]{64}", assembly_hash) is None
            or assembly_hash == "0" * 64):
        errors.append("release evidence gameAssemblySha256 must be a nonzero lowercase SHA-256")

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
    receipt_hash = evidence.get("privatePackageReceiptSha256")
    if (not isinstance(receipt_hash, str)
            or re.fullmatch(r"[0-9a-f]{64}", receipt_hash) is None
            or receipt_hash == "0" * 64):
        errors.append("release evidence privatePackageReceiptSha256 must be a nonzero lowercase SHA-256")

    verification = evidence.get("verification")
    repository_root = Path(__file__).resolve().parent.parent
    verification_keys = set(VERIFICATION_PASS_IDS) | {"numberedProtocols"}
    if not isinstance(verification, dict) or set(verification) != verification_keys:
        errors.append(
            "release evidence verification fields must exactly match schema version "
            f"{RELEASE_EVIDENCE_SCHEMA}"
        )
    else:
        for lane, pass_id in VERIFICATION_PASS_IDS.items():
            _validate_artifact_binding(verification.get(lane),
                                       f"verification.{lane}", errors,
                                       repository_root,
                                       expected_pass_id=pass_id)
        _validate_assembly_receipt(verification.get("nativeCompileLoad"),
                                   assembly_hash, errors, repository_root)
        protocols = verification.get("numberedProtocols")
        if not isinstance(protocols, dict) or set(protocols) != {
                "artifactRef", "artifactSha256", "passIds"}:
            errors.append(
                "release evidence verification.numberedProtocols fields must be "
                "artifactRef, artifactSha256, and passIds"
            )
        else:
            _validate_artifact_binding(protocols,
                                       "verification.numberedProtocols", errors,
                                       repository_root,
                                       include_pass_id=False,
                                       extra_keys={"passIds"})
            pass_ids = protocols.get("passIds")
            if (not isinstance(pass_ids, list) or not pass_ids
                    or len(pass_ids) > 1024):
                errors.append(
                    "release evidence verification.numberedProtocols.passIds must be "
                    "a nonempty bounded list"
                )
            else:
                seen: set[str] = set()
                for pass_id in pass_ids:
                    if (not isinstance(pass_id, str)
                            or re.fullmatch(r"[0-9]+[a-z]*[0-9]*", pass_id) is None):
                        errors.append(
                            "release evidence verification.numberedProtocols.passIds "
                            "must contain exact individual TESTING.md IDs"
                        )
                        break
                    if pass_id in seen:
                        errors.append(
                            "release evidence verification.numberedProtocols.passIds "
                            "must not contain duplicates"
                        )
                        break
                    seen.add(pass_id)
                if len(seen) == len(pass_ids):
                    protocol_path = Path(__file__).resolve().parent.parent / "TESTING.md"
                    try:
                        protocol_text = protocol_path.read_text(encoding="utf-8-sig")
                    except (OSError, UnicodeError) as error:
                        errors.append(
                            "release evidence cannot read the authoritative TESTING.md: "
                            f"{error}"
                        )
                    else:
                        defined_rows = re.findall(
                            r"^\| ([0-9]+[a-z]*[0-9]*) \|", protocol_text, re.MULTILINE
                        )
                        defined = set(defined_rows)
                        missing = sorted(seen - defined)
                        if missing:
                            errors.append(
                                "release evidence verification.numberedProtocols.passIds "
                                "are absent from TESTING.md: " + ", ".join(missing)
                            )
                        ambiguous = sorted(
                            pass_id for pass_id in seen
                            if defined_rows.count(pass_id) != 1
                        )
                        if ambiguous:
                            errors.append(
                                "release evidence verification.numberedProtocols.passIds "
                                "are not unique in TESTING.md: " + ", ".join(ambiguous)
                            )

    private = evidence.get("privateSubscription")
    private_keys = {
        "source", "inventory", "receipt", "loader", "newGame", "saveReload", "oldSave",
        "representativeFeatures", "playerLog", "localDuplicatesRemoved", "uploadHiddenFiles",
        "testedBy", "completedUtc",
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
                errors.append(f"release evidence privateSubscription.{key} must be {value!r}")
        tester = private.get("testedBy")
        if (not isinstance(tester, str) or tester != tester.strip() or not 2 <= len(tester) <= 80
                or tester.casefold() in {"todo", "tbd", "unknown", "n/a"}
                or _qud_text_error(tester) is not None):
            errors.append("release evidence testedBy must name the human tester")
        completed = private.get("completedUtc")
        if (not isinstance(completed, str)
                or re.fullmatch(r"[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}:[0-9]{2}Z", completed) is None):
            errors.append("release evidence completedUtc must be second-precision UTC")
        else:
            try:
                from datetime import datetime
                datetime.strptime(completed, "%Y-%m-%dT%H:%M:%SZ")
            except ValueError:
                errors.append("release evidence completedUtc is not a real UTC date")
    if errors:
        raise ValidationError("release evidence is invalid; " + "; ".join(errors))
    return candidate


def _validate_artifact_binding(value: object, label: str, errors: list[str],
                               repository_root: Path,
                               expected_pass_id: str | None = None,
                               include_pass_id: bool = True,
                               extra_keys: set[str] | None = None) -> None:
    keys = {"artifactRef", "artifactSha256"}
    if include_pass_id:
        keys.add("passId")
    if extra_keys:
        keys.update(extra_keys)
    if not isinstance(value, dict) or set(value) != keys:
        errors.append(f"release evidence {label} fields must be "
                      + ", ".join(sorted(keys)))
        return
    if include_pass_id and value.get("passId") != expected_pass_id:
        errors.append(f"release evidence {label}.passId must be {expected_pass_id!r}")
    artifact_ref = value.get("artifactRef")
    if (not isinstance(artifact_ref, str) or artifact_ref != artifact_ref.strip()
            or not 3 <= len(artifact_ref) <= 512
            or artifact_ref.casefold() in {"todo", "tbd", "unknown", "n/a"}
            or "placeholder" in artifact_ref.casefold()
            or "artifact_reference" in artifact_ref.casefold()
            or _qud_text_error(artifact_ref) is not None
            or not _safe_evidence_artifact_ref(artifact_ref)):
        errors.append(f"release evidence {label}.artifactRef must identify the retained artifact")
        artifact_ref = None
    artifact_hash = value.get("artifactSha256")
    if (not isinstance(artifact_hash, str)
            or re.fullmatch(r"[0-9a-f]{64}", artifact_hash) is None
            or artifact_hash == "0" * 64):
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


def _validate_assembly_receipt(value: object, expected_hash: object,
                               errors: list[str], repository_root: Path) -> None:
    """Bind the declared licensed game binary to the retained native transcript."""
    if (not isinstance(value, dict) or not isinstance(value.get("artifactRef"), str)
            or not isinstance(expected_hash, str)
            or re.fullmatch(r"[0-9a-f]{64}", expected_hash) is None):
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
    if (posixpath.normpath(value) != value
            or any(component in ("", ".", "..") for component in components)
            or any(unicodedata.category(character).startswith("C") for character in value)):
        return False
    reserved = re.compile(
        r"^(?:CON|PRN|AUX|NUL|CONIN\$|CONOUT\$|"
        r"COM[1-9\u00b9\u00b2\u00b3]|LPT[1-9\u00b9\u00b2\u00b3])$",
        re.IGNORECASE,
    )
    for component in components:
        if (any(character in '<>:"|?*' for character in component)
                or component.endswith((".", " "))
                or reserved.fullmatch(component.rstrip(". ").split(".", 1)[0])):
            return False
    return True


def validate_workshop(path: Path, manifest: dict, mode: str) -> dict | None:
    if not path.exists():
        if mode == "test":
            return None
        raise ValidationError("release package requires workshop.json created by Qud's private-item flow")
    data = _load_json(path)
    expected_keys = (
        "WorkshopId", "Title", "Description", "Tags", "Visibility", "ImagePath"
    )
    errors: list[str] = []
    if tuple(data.keys()) != expected_keys:
        errors.append("workshop.json fields/order must match Qud's completed serializer shape")
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
    errors.extend(_text_limits(
        data.get("Title") if isinstance(data.get("Title"), str) else "",
        data.get("Description") if isinstance(data.get("Description"), str) else "",
        tuple(data.get("Tags", "").split(",")) if isinstance(data.get("Tags"), str) else (),
    ))
    if not errors and path.read_bytes() != canonical_workshop_bytes(expected):
        errors.append("workshop.json bytes are not Qud's canonical Windows serializer output")
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
        length = struct.unpack(">I", payload[offset:offset + 4])[0]
        kind = payload[offset + 4:offset + 8]
        if len(kind) != 4 or not kind.isalpha() or kind[2] & 0x20:
            raise ValidationError("Workshop preview has an invalid PNG chunk type")
        if not kind[0] & 0x20 and kind not in (b"IHDR", b"PLTE", b"IDAT", b"IEND"):
            raise ValidationError("Workshop preview has an unknown critical PNG chunk")
        end = offset + 12 + length
        if end > len(payload):
            raise ValidationError("Workshop preview has a truncated PNG chunk body")
        body = payload[offset + 8:offset + 8 + length]
        expected_crc = struct.unpack(">I", payload[offset + 8 + length:end])[0]
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
    if sum(kind == b"IHDR" for kind, _ in chunks) != 1 or sum(kind == b"IEND" for kind, _ in chunks) != 1:
        raise ValidationError("Workshop preview has duplicate structural PNG chunks")

    width, height, depth, color, compression, filtering, interlace = struct.unpack(
        ">IIBBBBB", chunks[0][1]
    )
    if (width, height) != (512, 512):
        raise ValidationError(f"Workshop preview must be 512x512, got {width}x{height}")
    if depth != 8 or color not in (2, 6) or compression != 0 or filtering != 0 or interlace != 0:
        raise ValidationError("Workshop preview must be non-interlaced 8-bit RGB or RGBA PNG")
    idat_indexes = [index for index, (kind, _) in enumerate(chunks) if kind == b"IDAT"]
    if not idat_indexes or idat_indexes != list(range(idat_indexes[0], idat_indexes[-1] + 1)):
        raise ValidationError("Workshop preview must contain contiguous image-data chunks")
    plte_indexes = [index for index, (kind, _) in enumerate(chunks) if kind == b"PLTE"]
    if len(plte_indexes) > 1:
        raise ValidationError("Workshop preview has duplicate palette chunks")
    if plte_indexes:
        palette = chunks[plte_indexes[0]][1]
        if len(palette) < 3 or len(palette) > 768 or len(palette) % 3:
            raise ValidationError("Workshop preview has an invalid palette chunk length")
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
            raise ValidationError("Workshop preview decoded image exceeds its declared size")
        raw += decoder.flush(expected_decoded + 1 - len(raw))
    except zlib.error as error:
        raise ValidationError(f"Workshop preview image data cannot be decoded: {error}") from error
    if len(raw) > expected_decoded:
        raise ValidationError("Workshop preview decoded image exceeds its declared size")
    if not decoder.eof or decoder.unused_data or decoder.unconsumed_tail:
        raise ValidationError("Workshop preview image data is incomplete or has trailing bytes")
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
    workshop.add_argument("mode", choices=("test", "release"))
    workshop.add_argument("manifest", type=Path)
    workshop.add_argument("path", type=Path)
    canonicalize = subparsers.add_parser("canonicalize")
    canonicalize.add_argument("mode", choices=("test", "release"))
    canonicalize.add_argument("manifest", type=Path)
    canonicalize.add_argument("path", type=Path)
    evidence = subparsers.add_parser("evidence")
    evidence.add_argument("manifest", type=Path)
    evidence.add_argument("preview", type=Path)
    evidence.add_argument("workshop", type=Path)
    evidence.add_argument("record", type=Path)
    evidence.add_argument("readme", type=Path)
    evidence.add_argument("changelog", type=Path)
    workshop_id = subparsers.add_parser("workshop-id")
    workshop_id.add_argument("path", type=Path)
    args = parser.parse_args(argv)
    try:
        if args.command == "fields":
            manifest = load_manifest(args.manifest, require_preview=True)
            for value in (manifest["version"], manifest["title"], manifest["PreviewImage"]):
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
            print(validate_release_evidence(manifest, args.preview, args.workshop,
                                            args.record, args.readme, args.changelog))
        elif args.command == "workshop-id":
            print(_workshop_id(_load_json(args.path)))
    except ValidationError as error:
        print(str(error), file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
