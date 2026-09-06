#!/usr/bin/env python3
"""Observe a closed package and emit an upload plan; never authenticate or upload.

This is not a release gate. A trusted launcher must first run the clean-candidate
packager, then revalidate these bindings immediately before any authorized upload.
"""
from __future__ import annotations

import argparse
from contextlib import ExitStack
import hashlib
import io
import json
import os
from pathlib import Path
import re
import stat
import sys
import unicodedata

sys.dont_write_bytecode = True
if __package__:
    from . import workshop_metadata as metadata
else:
    import workshop_metadata as metadata

APP_ID = 333640
ALPHA_ITEM = 3794797472
MAX_FILES = 10_000
MAX_TOTAL_BYTES = 512 * 1024 * 1024
MAX_RECEIPT_BYTES = 1024 * 1024
REQUIRED = frozenset(("manifest.json", "workshop.json", metadata.PREVIEW))
ValidationError = metadata.ValidationError


def require(condition: bool, message: str) -> None:
    if not condition:
        raise ValidationError(message)


def _name(value: str) -> str:
    require(bool(value) and value.isprintable() and "\\" not in value and ":" not in value,
            "unsafe package or receipt name")
    value.encode("utf-8", errors="strict")
    parts = value.split("/")
    require(all(part not in ("", ".", "..") for part in parts),
            "absolute, empty, or traversing receipt entry")
    for part in parts:
        stem = part.split(".", 1)[0].rstrip(" ").casefold()
        require(not part.endswith((".", " ")) and not any(char in part for char in '<>"|?*')
                and re.fullmatch(r"(?:con|prn|aux|nul|conin\$|conout\$|(?:com|lpt)[1-9¹²³])", stem) is None,
                "Windows-unsafe package or receipt component")
    return value


def _path(value: str | Path) -> Path:
    raw = str(value)
    path = Path(raw)
    require(path.is_absolute() and str(path) == raw and raw.isprintable()
            and all(part != ".." for part in path.parts), "path must be canonical and absolute")
    roots = {Path(__file__).absolute().parents[1]}
    for parent in (Path.cwd(), *Path.cwd().parents):
        if (parent / ".git").exists():
            roots.add(parent)
            break
    require(len(path.parts) > 2 and path != Path.home(), "refusing broad root or home path")
    require(not any(path == root or root in path.parents or path in root.parents for root in roots),
            "refusing workspace path or ancestor")
    _name(raw[1:])
    return path


def _signature(status: os.stat_result) -> tuple:
    return (status.st_dev, status.st_ino, status.st_mode, status.st_nlink,
            status.st_uid, status.st_gid, status.st_size, status.st_mtime_ns, status.st_ctime_ns)


def _open(name: str, parent: int | None, directory: bool) -> int:
    flags = os.O_RDONLY | os.O_NOFOLLOW | os.O_NONBLOCK
    if directory:
        flags |= os.O_DIRECTORY
    return os.open(name, flags, dir_fd=parent)


def _anchor(path: Path, directory: bool, stack: ExitStack, anchors: dict) -> int:
    fd = _open(path.anchor, None, True)
    stack.callback(os.close, fd)
    current = Path(path.anchor)
    for index, part in enumerate(path.parts[1:]):
        fd = _open(part, fd, directory or index < len(path.parts) - 2)
        stack.callback(os.close, fd)
        current /= part
        status = os.fstat(fd)
        anchors[str(current)] = (status.st_dev, status.st_ino, status.st_mode)
    return fd


def _read(fd: int, limit: int, retain: bool = False) -> tuple:
    before = _signature(os.fstat(fd))
    require(stat.S_ISREG(before[2]) and before[3] == 1, "input must be regular and not hard-linked")
    require(0 <= before[6] <= limit, "input exceeds byte bound")
    os.lseek(fd, 0, os.SEEK_SET)
    digest, chunks, count = hashlib.sha256(), [], 0
    while True:
        chunk = os.read(fd, min(1024 * 1024, before[6] - count + 1))
        if not chunk:
            break
        count += len(chunk)
        require(count <= before[6], "input changed while hashing")
        digest.update(chunk)
        if retain:
            chunks.append(chunk)
    require(count == before[6] and before == _signature(os.fstat(fd)), "input changed while hashing")
    return before, digest.hexdigest(), b"".join(chunks) if retain else None


def _receipt(payload: bytes) -> dict[str, str]:
    require(bool(payload) and payload.endswith(b"\n"), "receipt requires complete sha256sum lines")
    lines = payload.decode("utf-8", errors="strict").split("\n")[:-1]
    require(0 < len(lines) <= MAX_FILES, "receipt file count exceeds bound")
    entries, folded = {}, set()
    for line in lines:
        match = re.fullmatch(r"([0-9a-f]{64}) [ *](.+)", line)
        require(match is not None, "invalid sha256sum receipt row")
        raw = match[2]
        name = _name(raw[2:] if raw.startswith("./") else raw)
        key = unicodedata.normalize("NFC", name).casefold()
        require(name not in entries and key not in folded, "duplicate or casefold-colliding receipt entry")
        entries[name] = match[1]
        folded.add(key)
    require(REQUIRED <= entries.keys(), "receipt lacks manifest/workshop/preview")
    return entries


def _inventory(root: int, hashes: bool = True) -> tuple[dict, dict, dict]:
    files, directories, payloads, folded = {}, {}, {}, set()
    total = 0

    def visit(fd: int, prefix: str, depth: int) -> None:
        nonlocal total
        require(depth <= 64 and len(directories) < MAX_FILES, "directory traversal exceeds bound")
        before = _signature(os.fstat(fd))
        directories[prefix] = before
        os.lseek(fd, 0, os.SEEK_SET)
        with os.scandir(fd) as entries:
            for entry in entries:
                leaf = entry.name
                name = _name(prefix + "/" + leaf if prefix else leaf)
                key = unicodedata.normalize("NFC", name).casefold()
                require(key not in folded, "casefold-colliding package path")
                folded.add(key)
                observed = _signature(os.stat(leaf, dir_fd=fd, follow_symlinks=False))
                is_directory = stat.S_ISDIR(observed[2])
                require(is_directory or stat.S_ISREG(observed[2]), "linked or nonregular package entry")
                child = _open(leaf, fd, is_directory)
                try:
                    require(observed == _signature(os.fstat(child)), "entry changed while opening")
                    if is_directory:
                        visit(child, name, depth + 1)
                    else:
                        require(len(files) < MAX_FILES, "package file count exceeds bound")
                        require(observed[3] == 1 and 0 <= observed[6] <= MAX_TOTAL_BYTES - total,
                                "hard-linked file or package exceeds byte bound")
                        record = (_read(child, MAX_TOTAL_BYTES - total, name in REQUIRED)
                                  if hashes else (observed, "", None))
                        require(record[0] == observed, "entry changed before hashing")
                        files[name] = record[:2]
                        total += record[0][6]
                        if name in REQUIRED:
                            payloads[name] = record[2]
                    require(observed == _signature(os.stat(leaf, dir_fd=fd, follow_symlinks=False)),
                            "entry changed during observation")
                finally:
                    os.close(child)
        require(before == _signature(os.fstat(fd)), "directory changed during observation")

    visit(root, "", 0)
    return files, directories, payloads


class _BytesFile:
    """Read-only path protocol for existing validators over already observed bytes."""
    def __init__(self, name: str, payload: bytes):
        self.name, self.payload = name, payload

    def open(self, *, encoding: str):
        return io.StringIO(self.payload.decode(encoding, errors="strict"))

    def exists(self) -> bool:
        return True

    def read_bytes(self) -> bytes:
        return self.payload


def build_plan(package: str | Path, receipt: str | Path, mode: str,
               expected_item: str, expected_version: str) -> dict:
    require(os.name == "posix" and hasattr(os, "O_NOFOLLOW"), "requires POSIX no-follow descriptor support")
    require(mode in ("test", "alpha"), "mode must be test or alpha")
    require(isinstance(expected_item, str) and re.fullmatch(r"[1-9][0-9]{0,19}", expected_item) is not None,
            "expected item must be canonical positive u64")
    item = int(expected_item)
    require(item <= metadata.MAX_WORKSHOP_ID, "expected item exceeds u64")
    require((item == ALPHA_ITEM) == (mode == "alpha"), "item does not belong to requested test/alpha lane")
    require(isinstance(expected_version, str) and re.fullmatch(r"(?:0|[1-9][0-9]*)\.(?:0|[1-9][0-9]*)\.(?:0|[1-9][0-9]*)",
            expected_version) is not None, "expected version must be canonical X.Y.Z")
    package, receipt = _path(package), _path(receipt)
    require(package not in receipt.parents, "receipt must be external to package")
    with ExitStack() as stack:
        anchors = {}
        root = _anchor(package, True, stack, anchors)
        receipt_fd = _anchor(receipt, False, stack, anchors)
        receipt_before = _read(receipt_fd, MAX_RECEIPT_BYTES, True)
        expected = _receipt(receipt_before[2])
        files, directories, payloads = _inventory(root)
        require(files.keys() == expected.keys(), "receipt is not a closed package inventory")
        require(all(files[name][1] == digest for name, digest in expected.items()), "package receipt hash mismatch")
        manifest = metadata.load_manifest(_BytesFile("manifest.json", payloads["manifest.json"]))
        workshop = metadata.validate_workshop(_BytesFile("workshop.json", payloads["workshop.json"]), manifest, mode)
        metadata.validate_preview(_BytesFile(metadata.PREVIEW, payloads[metadata.PREVIEW]))
        require(workshop is not None and workshop["WorkshopId"] == item, "WorkshopId differs from expected item")
        require(manifest["version"] == expected_version, "manifest version differs from expected version")
        require(workshop["Visibility"] == ("0" if mode == "test" else "2"), "unexpected Qud visibility")
        repeated = _inventory(root)
        require((files, directories) == repeated[:2], "package changed between observations")
        require(receipt_before == _read(receipt_fd, MAX_RECEIPT_BYTES, True), "receipt changed between observations")
        final_files, final_dirs, _ = _inventory(root, hashes=False)
        require(directories == final_dirs and {key: value[0] for key, value in files.items()}
                == {key: value[0] for key, value in final_files.items()}, "package changed after hashing")
        require(receipt_before[0] == _signature(os.fstat(receipt_fd)), "receipt changed after hashing")
        after = {}
        _anchor(package, True, stack, after)
        _anchor(receipt, False, stack, after)
        require(anchors == after, "input path authority changed")
        return {
            "schema": "taf-workshop-upload-plan-v1", "planOnly": True,
            "appId": APP_ID, "targetItem": expected_item, "mode": mode,
            "manifestId": manifest["id"], "version": manifest["version"],
            "title": workshop["Title"], "description": workshop["Description"],
            "tags": workshop["Tags"].split(","), "qudVisibility": workshop["Visibility"],
            "steamVisibility": {"0": 2, "2": 0}[workshop["Visibility"]],
            "contentPath": str(package), "previewPath": str(package / metadata.PREVIEW),
            "receiptPath": str(receipt), "receiptSHA": receipt_before[1],
            "manifestSHA": files["manifest.json"][1], "workshopSHA": files["workshop.json"][1],
            "files": [{"path": name, "sha256": files[name][1], "size": files[name][0][6]}
                      for name in sorted(files, key=lambda value: value.encode("utf-8"))],
        }


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--package", required=True)
    parser.add_argument("--receipt", required=True)
    parser.add_argument("--mode", choices=("test", "alpha"), required=True)
    parser.add_argument("--expected-item", required=True)
    parser.add_argument("--expected-version", required=True)
    args = parser.parse_args(argv)
    try:
        plan = build_plan(args.package, args.receipt, args.mode, args.expected_item, args.expected_version)
    except (ValidationError, OSError, UnicodeError) as error:
        print(f"Workshop upload plan refused: {error}", file=sys.stderr)
        return 2
    print(json.dumps(plan, ensure_ascii=False, indent=2, sort_keys=True))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
