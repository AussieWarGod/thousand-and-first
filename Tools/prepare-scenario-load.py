#!/usr/bin/env python3
"""Copy one stopped, sealed developer save into a fresh isolated load profile.

Never launches Qud, changes the source, or treats evidence hashes as process authority.
Refusals retain any partial destination; there is no cleanup or overwrite mode.
"""
from __future__ import annotations

import concurrent.futures
import hashlib
import json
import os
from datetime import datetime, timezone
from pathlib import Path
import re
import stat
import subprocess
import sys
import time
from typing import Callable

import scenario_profile

ROOT_NAME = re.compile(r"taf-scenario\.[A-Za-z0-9]+\Z")
CLI_ROOT = re.compile(r"/mnt/c/taf-scenario\.[A-Za-z0-9]+\Z")
GUID = re.compile(r"[0-9a-f]{8}(?:-[0-9a-f]{4}){3}-[0-9a-f]{12}\Z")
SHA = re.compile(r"[0-9a-f]{64}\Z")
TOKEN = re.compile(r"[a-z0-9.-]{1,96}\Z")
MAX_FILE = 512 * 1024 * 1024
MAX_LOCAL_FILE = 32 * 1024 * 1024
MAX_SNAPSHOT = 4 * 1024 * 1024
MAX_FILES = 16384
MAX_TREE_BYTES = 2 * 1024 * 1024 * 1024
SAVE_FILES = ("Primary.sav.gz", "Primary.json", "Cache.db")
MAX_COPY_WORKERS = 4  # Bounded local-only fan-out; never spawns a process, never touches Windows.


def _iso(moment: float) -> str:
    return datetime.fromtimestamp(moment, timezone.utc).strftime("%Y-%m-%dT%H:%M:%S.%f")[:-3] + "Z"


class PhaseTimer:
    """Diagnostic-only wall-clock phase durations for the load-profile copier.

    Recorded phases are pure observation: they never gate sealing, never change which bytes are
    read or written, and are not consulted by any guard in this module or by
    check-quickstart-results.py. The list is written once, after every safety predicate below has
    already passed, as an additive JSON evidence file beside load-source-evidence.json.
    """

    def __init__(self) -> None:
        self.phases: list[dict] = []

    def measure(self, name: str, counters: dict | None = None) -> "_PhaseScope":
        return _PhaseScope(self, name, counters if counters is not None else {})


class _PhaseScope:
    def __init__(self, timer: PhaseTimer, name: str, counters: dict) -> None:
        self._timer, self._name, self.counters = timer, name, counters

    def __enter__(self) -> dict:
        self._began = time.time()
        return self.counters

    def __exit__(self, exc_type, exc, tb) -> bool:
        ended = time.time()
        self._timer.phases.append({
            "phase": self._name,
            "beginISO": _iso(self._began),
            "endISO": _iso(ended),
            "seconds": round(ended - self._began, 6),
            **self.counters,
        })
        return False


def require(condition: bool, message: str) -> None:
    if not condition:
        raise ValueError(message)


def directory(path: Path) -> None:
    """Check every ancestor, including the starting root; never resolve away an alias."""
    require(path.is_absolute(), "directory must be absolute")
    for part in reversed((path, *path.parents)):
        status = part.lstat()
        require(stat.S_ISDIR(status.st_mode) and not stat.S_ISLNK(status.st_mode)
                and not getattr(status, "st_file_attributes", 0) & scenario_profile.REPARSE_POINT,
                "linked/reparse or non-directory ancestor: " + str(part))
    require(os.path.realpath(path) == str(path), "aliased directory: " + str(path))


def file_status(path: Path, limit: int = MAX_FILE):
    directory(path.parent)
    status = path.lstat()
    require(stat.S_ISREG(status.st_mode) and status.st_nlink == 1
            and not getattr(status, "st_file_attributes", 0) & scenario_profile.REPARSE_POINT,
            "file is linked, reparse, or non-regular: " + str(path))
    require(0 <= status.st_size <= limit, "file exceeds finite bound: " + str(path))
    return status


def stamp(status) -> tuple:
    return (status.st_dev, status.st_ino, status.st_size, status.st_mtime_ns,
            status.st_ctime_ns, status.st_nlink, status.st_mode)


def read_stream(path: Path, limit: int, consume: Callable[[bytes], None]) -> str:
    before = file_status(path, limit)
    descriptor = os.open(path, os.O_RDONLY | getattr(os, "O_NOFOLLOW", 0))
    with os.fdopen(descriptor, "rb") as source:
        require(stamp(os.fstat(source.fileno())) == stamp(before), "source identity changed: " + str(path))
        digest = hashlib.sha256()
        length = 0
        for block in iter(lambda: source.read(65536), b""):
            length += len(block)
            require(length <= limit, "source grew beyond finite bound: " + str(path))
            digest.update(block)
            consume(block)
        require(stamp(os.fstat(source.fileno())) == stamp(before), "source changed while reading: " + str(path))
    require(stamp(file_status(path, limit)) == stamp(before), "source path changed: " + str(path))
    return digest.hexdigest()


def read_bytes(path: Path, limit: int) -> bytes:
    blocks: list[bytes] = []
    read_stream(path, limit, blocks.append)
    return b"".join(blocks)


def digest(path: Path, limit: int = MAX_FILE) -> str:
    return read_stream(path, limit, lambda block: None)


def tree_files(root: Path, limit: int) -> list[Path]:
    directory(root)
    found: list[Path] = []
    total = 0
    directories = 0
    for current, children, files in os.walk(root, followlinks=False):
        directory(Path(current))
        directories += len(children)
        require(directories <= MAX_FILES, "too many source directories")
        for child in children:
            directory(Path(current) / child)
        for name in files:
            path = Path(current) / name
            total += file_status(path, limit).st_size
            found.append(path)
            require(len(found) <= MAX_FILES and total <= MAX_TREE_BYTES, "source tree exceeds finite bound")
    return sorted(found)


def empty_destination(path: Path) -> None:
    directory(path.parent)
    if os.path.lexists(path):
        directory(path)
        require(path.stat().st_uid == os.getuid(), "destination is not owned by this user")
        require(not any(path.iterdir()), "destination is occupied: " + str(path))


def write_new(path: Path, data: bytes) -> None:
    directory(path.parent)
    with path.open("xb") as target:
        target.write(data)
        target.flush()
        os.fsync(target.fileno())
    require(digest(path, max(len(data), 1)) == hashlib.sha256(data).hexdigest(), "destination readback changed")


DIR_FD_SUPPORTED = (os.open in os.supports_dir_fd and os.mkdir in os.supports_dir_fd
                    and hasattr(os, "O_DIRECTORY") and hasattr(os, "O_NOFOLLOW"))
DIR_FLAGS = os.O_RDONLY | os.O_DIRECTORY | os.O_NOFOLLOW


def open_validated_root(path: Path) -> int:
    """Anchor `path` as a directory file descriptor by walking EVERY component of its absolute
    path, starting from the filesystem root -- never a by-name open of `path` itself.

    An earlier version of this function opened `path` directly (a multi-component path) on the
    theory that every ancestor had just been proven by directory() with "no other actor able to
    run in between". That theory is false against another PROCESS: nothing stops a second actor
    from swapping any ancestor of `path` -- including `path`'s own parent -- for a symlink in the
    instant between that proof and this open, and a bare os.open(path, O_NOFOLLOW) only guards
    the FINAL component; every ancestor above it is still resolved by name and would silently
    follow such a swap.

    The fix: open "/" (which cannot be a symlink or swapped by any local actor -- it is the root
    of the filesystem namespace) and then walk path.parts[1:] one component at a time via
    open_directory_chain, which opens each name relative to the fd the PREVIOUS component's open
    returned. Every single component, including what used to be "just an ancestor", now gets its
    own atomic O_NOFOLLOW|O_DIRECTORY check; there is no remaining path string, at any depth, for
    a swap at any point before or during this call to poison. If any component is, or has become,
    a symlink or non-directory, this raises instead of silently resolving through it.

    Every directory BELOW this root is opened the same way, one component at a time, via
    open_directory_chain (see make_directory_tree), so this function is the ONLY remaining place
    this module resolves more than one path component per syscall, and it does so entirely via
    single-component dir_fd-relative opens chained from "/" -- never a multi-component name.
    """
    require(path.is_absolute(), "root anchor path must be absolute: " + str(path))
    root_fd = os.open(os.sep, DIR_FLAGS)
    try:
        return open_directory_chain(root_fd, path.parts[1:])
    finally:
        os.close(root_fd)


def open_directory_chain(root_fd: int, components: tuple[str, ...]) -> int:
    """From an already-open, already-anchored `root_fd`, open each of `components` one at a time,
    every step relative to the fd the PREVIOUS step returned -- never a multi-component path.

    O_NOFOLLOW|O_DIRECTORY makes each single-component open atomic: it fails immediately if that
    one name is, or has become, a symlink or non-directory. Because every step's fd is bound to
    the inode the previous step already opened, swapping an INTERMEDIATE ancestor's NAME for a
    symlink after this walk has passed it changes nothing: there is no remaining path string for
    that swap to poison, only fds already bound to real inodes. Always returns a fd this caller
    owns and must close, even when `components` is empty (a dup of `root_fd`, so callers never need
    to special-case "no fd was opened, don't close root_fd twice").
    """
    if not components:
        return os.dup(root_fd)
    fd = root_fd
    owned = False
    try:
        for name in components:
            next_fd = os.open(name, DIR_FLAGS, dir_fd=fd)
            if owned:
                os.close(fd)
            fd, owned = next_fd, True
        return fd
    except BaseException:
        if owned:
            os.close(fd)
        raise


def make_directory_tree(root: Path, source_root: Path) -> dict[Path, int]:
    """Anchor `root` (already just created by the caller) and create/open every subdirectory of
    `source_root`'s tree beneath it via dir_fd-relative mkdir+open, one component at a time.

    Returns every directory's fd keyed by its path relative to `root` (the root itself keyed by
    Path(".")). No directory below `root` is ever created or opened by resolving a multi-component
    path: each is created with os.mkdir(name, dir_fd=parent_fd) and opened with
    os.open(name, ..., dir_fd=parent_fd), where parent_fd is the fd this same walk already opened
    for its immediate parent -- see open_directory_chain's docstring for why that closes the gap a
    bare per-file `directory()` walk-by-name cannot. Callers must close every returned fd.
    """
    require(DIR_FD_SUPPORTED, "this platform cannot anchor directory custody with O_NOFOLLOW|O_DIRECTORY+dir_fd")
    directory_fds: dict[Path, int] = {Path("."): open_validated_root(root)}
    try:
        for current, directories, _ in os.walk(source_root, followlinks=False):
            relative_current = Path(current).relative_to(source_root)
            parent_fd = directory_fds[relative_current]
            for child in sorted(directories):
                os.mkdir(child, dir_fd=parent_fd)
                directory_fds[relative_current / child] = open_directory_chain(parent_fd, (child,))
        return directory_fds
    except BaseException:
        for fd in directory_fds.values():
            os.close(fd)
        raise


def copy_new(source: Path, destination: Path, expected: str, limit: int, dir_fd: int | None = None) -> None:
    """Exclusive create, flush, fsync, then an independent readback hash proof.

    `dir_fd`, when given, anchors the create to an already-open, already fully path-anchored
    directory file descriptor (see make_directory_tree/open_directory_chain) instead of resolving
    `destination`'s parent by name: the create can then never be redirected by a directory-name
    swap -- of the leaf OR any intermediate ancestor -- because dir_fd-relative opens operate on
    the fd's inode, not any path string. Every caller without a dir_fd keeps the original per-file
    `directory(destination.parent)` walk-by-name.
    """
    flags = os.O_WRONLY | os.O_CREAT | os.O_EXCL | os.O_NOFOLLOW
    if dir_fd is None:
        directory(destination.parent)
        handle = os.open(destination, flags)
    else:
        handle = os.open(destination.name, flags, dir_fd=dir_fd)
    with os.fdopen(handle, "wb") as target:
        copied = read_stream(source, limit, target.write)
        target.flush()
        os.fsync(target.fileno())
    require(copied == expected and digest(destination, limit) == expected, "copy differs from frozen source: " + str(source))


def copy_new_files(pairs: list[tuple[Path, Path, str, int]], limit: int, workers: int = MAX_COPY_WORKERS) -> None:
    """Bounded local-only parallel fan-out of copy_new, one worker thread per file at a time.

    Every guard copy_new performs -- exclusive create, flush, fsync, independent readback hash --
    runs unchanged for every file. Each pair already carries the dir_fd of its fully path-anchored
    parent directory (see make_directory_tree): this function performs no directory validation or
    opening of its own, it only fans copy_new out across workers and joins them. This never
    launches a process and never touches a network path.

    Every future is joined (ThreadPoolExecutor.shutdown waits for all of them) before this function
    returns or raises, so a failure in one worker can never leave another worker still running when
    the caller moves on. On any failure, every file's outcome is still awaited, the first error is
    re-raised, and prepare() therefore never reaches the sealing phase: whatever partial
    destination bytes exist stay exactly as ordinary serial failure would have left them.
    """
    if not pairs:
        return
    errors: list[BaseException] = []
    with concurrent.futures.ThreadPoolExecutor(max_workers=min(workers, len(pairs))) as pool:
        futures = [pool.submit(copy_new, source, destination, expected, limit, dir_fd)
                   for source, destination, expected, dir_fd in pairs]
        for future in concurrent.futures.as_completed(futures):
            try:
                future.result()
            except BaseException as error:
                errors.append(error)
    if errors:
        raise errors[0]


def request_text(raw: bytes) -> str:
    text = raw.decode("utf-8")
    require(text.endswith("\n") and text.count("\n") == 1 and "\r" not in text,
            "request.txt must be one exact LF-terminated line")
    request = text[:-1]
    parts = request.split(";")
    require(len(request) <= 512 and 2 <= len(parts) <= 16 and TOKEN.fullmatch(parts[0]), "malformed sealed request")
    seen: set[str] = set()
    for part in parts[1:]:
        name, equals, value = part.partition("=")
        require(equals and TOKEN.fullmatch(name) and name not in seen, "malformed/duplicate request parameter")
        seen.add(name)
        if name == "seed":
            scenario_profile.validate_seed(value)
        else:
            require(TOKEN.fullmatch(value), "malformed request value")
    require(parts[-1].startswith("seed=") and "seed" in seen, "request lacks its final exact frozen seed")
    return request


def ownership_shape(raw: bytes, root: Path) -> None:
    text = raw.decode("utf-8")
    value = json.loads(text)
    names = ("schema", "root", "pid", "startTicks", "executable", "arguments")
    require(isinstance(value, dict) and tuple(value) == names
            and text == json.dumps(value, ensure_ascii=False, separators=(",", ":")), "noncanonical process ownership receipt")
    require(value["schema"] == "taf-scenario-process-v1" and isinstance(value["root"], str)
            and value["root"].lower() == ("C:\\" + root.name).lower()
            and type(value["pid"]) is int and 0 < value["pid"] <= 2147483647
            and isinstance(value["startTicks"], str) and re.fullmatch(r"[1-9][0-9]{0,18}", value["startTicks"])
            and int(value["startTicks"]) <= 3155378975999999999
            and isinstance(value["executable"], str) and isinstance(value["arguments"], list)
            and len(value["arguments"]) == 12 and all(isinstance(arg, str) for arg in value["arguments"]),
            "malformed process ownership receipt")
    # The Windows helper, not this shape check, proves exact process policy and absence.


def prepare(source: Path, destination: Path, assert_stopped: Callable[[Path], None]) -> dict:
    """Filesystem core; tests use canonical-name roots under TemporaryDirectory.

    Production main additionally requires the exact /mnt/c root domain and always supplies
    the real Windows verifier. There is no CLI skip flag or alternate process authority.

    Wall-clock phase timings are recorded via PhaseTimer purely for diagnosis (see its
    docstring): every guard, flush, readback and inventory below runs exactly as before.
    """
    timer = PhaseTimer()
    require(ROOT_NAME.fullmatch(source.name) and ROOT_NAME.fullmatch(destination.name)
            and source.parent == destination.parent and source.name.lower() != destination.name.lower(),
            "source/destination must be distinct canonical sibling scenario roots")
    with timer.measure("preflight-and-stop-authority"):
        directory(source)
        source_seal, destination_seal = Path(str(source) + ".seal"), Path(str(destination) + ".seal")
        directory(source_seal)
        empty_destination(destination)
        empty_destination(destination_seal)
        ownership = read_bytes(source / "process-ownership.json", 16384)
        ownership_shape(ownership, source)
        assert_stopped(source)  # Before reading save/cache bytes or creating ANY output.
    with timer.measure("source-validation") as counters:
        tree_files(source, MAX_FILE)
        profile_seal = source_seal / "profile.sha256"
        seal_bytes = read_bytes(profile_seal, 4 * 1024 * 1024)
        request_bytes = read_bytes(source_seal / "request.txt", 1024)
        request = request_text(request_bytes)
        local = source / "Local"
        files = tree_files(local, MAX_LOCAL_FILE)
        expected = scenario_profile.read_seal(str(profile_seal))
        require(scenario_profile.inventory(str(local)) == expected, "source Local differs from its closed seal")
        require(read_bytes(local / "scenario-script.txt", MAX_LOCAL_FILE), "source has no sealed script")
        embark = read_bytes(local / "Mods/ThousandAndFirst/Harness/EmbarkModules.xml", MAX_LOCAL_FILE).decode("utf-8")
        marker = 'Name="r_TAF_ScenarioRequest_v1" Value="'
        require(embark.count(marker) == 1 and embark.split(marker)[1].split('"', 1)[0] == request,
                "sealed request and source embark overlay disagree")
        require(not os.path.lexists(local / "scenario-load.txt") and not os.path.lexists(local / "scenario-load-snapshot.txt"),
                "source already carries a load request")
        receipt = read_bytes(source / "scenario-save-receipt.txt", 512)
        lines = receipt.decode("ascii").split("\n")
        require(len(lines) == 6 and lines[-1] == "" and lines[0] == "taf-scenario-save-v1"
                and GUID.fullmatch(lines[1]) and all(SHA.fullmatch(line) for line in lines[2:5]), "malformed five-line save receipt")
        game_id = lines[1]
        snapshot = read_bytes(source / "scenario-save-snapshot.txt", MAX_SNAPSHOT)
        require(snapshot and hashlib.sha256(snapshot).hexdigest() == lines[4], "save snapshot hash mismatch")
        saves = source / "Synced/Saves"
        directory(saves)
        require(sorted(path.name for path in saves.iterdir()) == [game_id], "source must contain exactly the named save directory")
        save = saves / game_id
        directory(save)
        children = {path.name for path in save.iterdir()}
        require(set(SAVE_FILES) <= children <= set(SAVE_FILES) | {"Primary.sav.gz.bak"}, "save has missing files, WAL/SHM, or unexpected artifacts")
        hashes = {name: digest(save / name) for name in children}
        require(all((save / name).stat().st_size > 0 for name in SAVE_FILES), "save artifacts must not be empty")
        require(hashes[SAVE_FILES[0]] == lines[2] and hashes[SAVE_FILES[1]] == lines[3], "primary/info save hash mismatch")
        load_request = ("taf-scenario-load-v1\n" + game_id + "\n" + lines[2] + "\n" + lines[3]
                        + "\n" + hashes["Cache.db"] + "\n" + lines[4] + "\n").encode("ascii")
        frozen = {source / "process-ownership.json": ownership, profile_seal: seal_bytes,
                  source_seal / "request.txt": request_bytes, source / "scenario-save-receipt.txt": receipt,
                  source / "scenario-save-snapshot.txt": snapshot}
        evidence = {"schema": "taf-scenario-load-source-v1", "sourceRoot": str(source), "gameId": game_id,
                    "processAuthority": False, "sourceHashes": {str(path.relative_to(source)) if path.is_relative_to(source)
                        else ".seal/" + path.name: hashlib.sha256(data).hexdigest() for path, data in frozen.items()},
                    "saveHashes": {name: hashes[name] for name in SAVE_FILES}}
        counters["localFiles"] = len(files)
    with timer.measure("destination-setup"):
        empty_destination(destination); empty_destination(destination_seal)
        if not destination.exists(): destination.mkdir()
        if not destination_seal.exists(): destination_seal.mkdir()
        target_local = destination / "Local"
        target_local.mkdir()
        # Every subdirectory below target_local is created AND opened one path component at a time,
        # relative to its own already-anchored parent fd -- never by resolving a multi-component
        # path -- so a swap of any intermediate ancestor's NAME during the copy phase below cannot
        # redirect a worker's write (see make_directory_tree/open_directory_chain docstrings).
        directory_fds = make_directory_tree(target_local, local)
    try:
        with timer.measure("local-copy", {"files": len(files), "bytes": sum(os.path.getsize(path) for path in files)}):
            pairs = [(path, target_local / path.relative_to(local),
                      expected[scenario_profile.normalize(str(path.relative_to(local)))],
                      directory_fds[path.relative_to(local).parent]) for path in files]
            copy_new_files(pairs, MAX_LOCAL_FILE)
    finally:
        for fd in directory_fds.values():
            os.close(fd)
    with timer.measure("post-copy-target-inventory"):
        require(scenario_profile.inventory(str(target_local)) == expected, "copied Local differs from original closed inventory")
        write_new(target_local / "scenario-load.txt", load_request)
        write_new(target_local / "scenario-load-snapshot.txt", snapshot)
    with timer.measure("save-copy"):
        (destination / "Save").mkdir()
        (destination / "Synced").mkdir()
        (destination / "Synced/Saves").mkdir()
        target_save = destination / "Synced/Saves" / game_id
        target_save.mkdir()
        for name in SAVE_FILES:
            copy_new(save / name, target_save / name, hashes[name], MAX_FILE)
    with timer.measure("source-reproof"):
        for path, data in frozen.items():
            require(read_bytes(path, max(len(data), 1)) == data, "source receipt/seal/snapshot changed during copy")
        require(scenario_profile.inventory(str(local)) == expected, "source Local changed during copy")
        tree_files(source, MAX_FILE)
        require(sorted(path.name for path in saves.iterdir()) == [game_id] and {path.name for path in save.iterdir()} == children,
                "source save inventory changed during copy")
        for name, before in hashes.items():
            require(digest(save / name) == before, "source save file changed during copy: " + name)
    with timer.measure("seal-computation"):
        inventory = scenario_profile.inventory(str(target_local))
        target_expected = dict(expected)
        target_expected["scenario-load.txt"] = hashlib.sha256(load_request).hexdigest()
        target_expected["scenario-load-snapshot.txt"] = hashlib.sha256(snapshot).hexdigest()
        require(inventory == target_expected, "destination Local acquired unproved content during copy")
        seal = scenario_profile.SEAL_HEADER + "\n" + "".join(inventory[key] + "  " + key + "\n" for key in sorted(inventory))
    # Written BEFORE the seal/request/evidence below, deliberately: this file's own write can fail
    # (disk error, readback mismatch) like any write_new call, and when it does, prepare() must
    # raise with NO seal on disk yet -- exactly like every other pre-seal refusal already tested --
    # rather than leave an already-sealed profile behind a refusal that looks like the load failed.
    timings = {"schema": "taf-scenario-load-phase-timings-v1", "phases": timer.phases}
    write_new(destination / "load-phase-timings.json", (json.dumps(timings, sort_keys=True, separators=(",", ":")) + "\n").encode("utf-8"))
    write_new(destination_seal / "profile.sha256", seal.encode("utf-8"))
    write_new(destination_seal / "request.txt", request_bytes)
    write_new(destination / "load-source-evidence.json", (json.dumps(evidence, sort_keys=True, separators=(",", ":")) + "\n").encode("utf-8"))
    return evidence


def windows_path(path: Path) -> str:
    return subprocess.run(["wslpath", "-w", str(path)], check=True, capture_output=True, text=True).stdout.strip()


def assert_source_stopped(source: Path) -> None:
    tools = Path(__file__).resolve().parent
    configured = os.environ.get("TAF_QUD_ROOT")
    if configured:
        game = windows_path(Path(configured) / "CoQ.exe")
    elif os.environ.get("TAF_QUD_BASE"):
        game = windows_path((Path(os.environ["TAF_QUD_BASE"]) / "../../..").resolve() / "CoQ.exe")
    else:
        game = r"F:\SteamLibrary\steamapps\common\Caves of Qud\CoQ.exe"
    subprocess.run(["powershell.exe", "-NoProfile", "-ExecutionPolicy", "Bypass", "-File",
                    windows_path(tools / "assert-scenario-source-stopped.ps1"), "-Root", windows_path(source),
                    "-Game", game], check=True, capture_output=True, text=True)


def main(argv: list[str]) -> int:
    if len(argv) != 3:
        print("usage: prepare-scenario-load.py sourceRoot destRoot", file=sys.stderr)
        return 2
    try:
        require(all(CLI_ROOT.fullmatch(root) for root in argv[1:]), "CLI roots must be exact /mnt/c/taf-scenario.<alnum>")
        prepare(Path(argv[1]), Path(argv[2]), assert_source_stopped)
    except (OSError, ValueError, SystemExit, subprocess.SubprocessError) as error:
        print("scenario load preparation refused: " + str(error), file=sys.stderr)
        return 2
    print("SCENARIO LOAD PROFILE READY: " + argv[2] + "; source and partial evidence retained")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv))
