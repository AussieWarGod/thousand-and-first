#!/usr/bin/env python3
"""Descriptor-bound content-and-mode snapshots and Linux atomic entry publication.

The stage script uses this helper only after its product-specific path and ownership checks.
Tree writes stay in a newly created sibling directory. Directory and receipt-file publication use
one renameat2 operation: RENAME_NOREPLACE for an absent destination or RENAME_EXCHANGE otherwise.

Every parent command requires an inherited directory fd holding flock(LOCK_EX); callers retain the
same locked open-file description for the whole multi-command transaction. This serializes
cooperating stage processes. Advisory locks cannot constrain an uncooperative same-UID process, so
all same-UID processes remain inside the trusted computing base. Snapshots preserve regular-file
bytes and every file/directory permission bit, including special bits; ownership, timestamps,
xattrs, ACLs, sparse layout, and hard-link topology are deliberately outside this helper's claim.
"""

from __future__ import annotations

import argparse
from contextlib import contextmanager
import ctypes
import errno
import fcntl
import hashlib
import os
from pathlib import Path, PurePosixPath
import secrets
import select
import signal
import stat
import sys
from typing import Sequence
import unicodedata


RENAME_NOREPLACE = 1
RENAME_EXCHANGE = 2
UNSUPPORTED_ERRNOS = {
    errno.ENOSYS,
    errno.EINVAL,
    errno.EXDEV,
    errno.EOPNOTSUPP,
    getattr(errno, "ENOTSUP", errno.EOPNOTSUPP),
}
OPEN_DIRECTORY = os.O_RDONLY | os.O_DIRECTORY | os.O_CLOEXEC | os.O_NOFOLLOW
OPEN_FILE = os.O_RDONLY | os.O_CLOEXEC | os.O_NOFOLLOW
MAX_INVENTORY_BYTES = 16 * 1024 * 1024
MAX_BUFFERED_FILE_BYTES = 16 * 1024 * 1024

# Deterministic test-only signal cuts. Set TAF_ATOMIC_TEST_SIGNAL_AT to one label and,
# optionally, TAF_ATOMIC_TEST_SIGNAL_SCOPE=process-group when the caller runs under setsid(1).
# Production behavior is unchanged while the environment variable is absent.
TEST_SIGNAL_LABELS = (
    "materialize-before-mkdir",
    "materialize-after-mkdir",
    "materialize-after-directory-create",
    "materialize-after-directories",
    "materialize-file-before-create",
    "materialize-file-after-create",
    "materialize-file-after-copy",
    "materialize-file-after-chmod",
    "materialize-file-after-fsync",
    "materialize-after-file",
    "materialize-after-directory-mode",
    "materialize-after-tree-fsync",
    "materialize-before-parent-fsync",
    "materialize-after-parent-fsync",
    "probe-after-create",
    "probe-before-parent-fsync",
    "probe-after-parent-fsync",
    "probe-exchange-forward-after-rename",
    "probe-exchange-forward-after-fsync",
    "probe-exchange-reverse-after-rename",
    "probe-exchange-reverse-after-fsync",
    "probe-publish-after-rename",
    "probe-publish-after-fsync",
    "publish-after-rename",
    "publish-after-fsync",
    "exchange-after-rename",
    "exchange-after-fsync",
    "remove-sequester-after-rename",
    "remove-sequester-after-fsync",
    "remove-before-recurse",
    "remove-before-child",
    "remove-after-child",
    "remove-before-final",
    "write-file-after-create",
    "write-file-after-write",
    "write-file-after-fsync",
)

_PENDING_SIGNAL: int | None = None
_SIGNAL_DEFER_DEPTH = 0
_TRIGGERED_TEST_SIGNALS: set[str] = set()


class PublishError(RuntimeError):
    pass


class UnsupportedPublish(PublishError):
    pass


class RetainedEntry(PublishError):
    pass


class OperationInterrupted(BaseException):
    def __init__(self, signum: int) -> None:
        self.signum = signum
        super().__init__(f"interrupted by {signal.Signals(signum).name}")


def _record_signal(signum: int, _frame: object) -> None:
    global _PENDING_SIGNAL
    if _PENDING_SIGNAL is None:
        _PENDING_SIGNAL = signum


def _signal_checkpoint() -> None:
    if _PENDING_SIGNAL is not None and _SIGNAL_DEFER_DEPTH == 0:
        raise OperationInterrupted(_PENDING_SIGNAL)


@contextmanager
def _defer_signals(*, deliver_on_exit: bool = True):
    global _SIGNAL_DEFER_DEPTH
    _SIGNAL_DEFER_DEPTH += 1
    completed = False
    try:
        yield
        completed = True
    finally:
        _SIGNAL_DEFER_DEPTH -= 1
        if deliver_on_exit and completed:
            _signal_checkpoint()


@contextmanager
def _installed_signal_protocol():
    global _PENDING_SIGNAL, _SIGNAL_DEFER_DEPTH
    previous = {
        signum: signal.getsignal(signum)
        for signum in (signal.SIGHUP, signal.SIGINT, signal.SIGTERM)
    }
    _PENDING_SIGNAL = None
    _SIGNAL_DEFER_DEPTH = 0
    for signum in previous:
        signal.signal(signum, _record_signal)
    try:
        yield
        _signal_checkpoint()
    finally:
        for signum, handler in previous.items():
            signal.signal(signum, handler)


def _test_signal(label: str) -> None:
    requested = os.environ.get("TAF_ATOMIC_TEST_SIGNAL_AT")
    if requested != label or label in _TRIGGERED_TEST_SIGNALS:
        return
    if label not in TEST_SIGNAL_LABELS:
        raise PublishError(f"unknown atomic test signal label: {label}")
    marker = os.environ.get("TAF_ATOMIC_TEST_SIGNAL_MARKER")
    if marker:
        try:
            marker_fd = os.open(
                marker,
                os.O_WRONLY | os.O_CREAT | os.O_EXCL | os.O_CLOEXEC | os.O_NOFOLLOW,
                0o600,
            )
        except FileExistsError:
            return
        else:
            os.close(marker_fd)
    _TRIGGERED_TEST_SIGNALS.add(label)
    if os.environ.get("TAF_ATOMIC_TEST_SIGNAL_SCOPE") == "process-group":
        os.killpg(os.getpgrp(), signal.SIGTERM)
    else:
        os.kill(os.getpid(), signal.SIGTERM)


def _validate_test_signal_configuration() -> None:
    requested = os.environ.get("TAF_ATOMIC_TEST_SIGNAL_AT")
    if requested is not None and requested not in TEST_SIGNAL_LABELS:
        raise PublishError(f"unknown atomic test signal label: {requested}")
    scope = os.environ.get("TAF_ATOMIC_TEST_SIGNAL_SCOPE")
    if scope not in (None, "self", "process-group"):
        raise PublishError(f"unknown atomic test signal scope: {scope}")


def identity(status: os.stat_result) -> str:
    return f"{status.st_dev}:{status.st_ino}"


def _safe_name(value: str) -> str:
    if (
        not value
        or value in (".", "..")
        or "/" in value
        or "\0" in value
        or any(_forbidden_unicode(character) for character in value)
    ):
        raise PublishError(f"unsafe sibling entry name: {value!r}")
    return value


def _forbidden_unicode(character: str) -> bool:
    # Cc includes ASCII/C1 controls; Cf includes bidi overrides, isolates, joiners, and BOM;
    # Cs/Cn/Co complete Unicode's Other (C*) categories. Filesystem-facing names reject all.
    return unicodedata.category(character).startswith("C")


def _safe_relative(value: str) -> tuple[str, ...]:
    pure = PurePosixPath(value)
    if (
        not value
        or pure.is_absolute()
        or "\\" in value
        or any(part in ("", ".", "..") for part in pure.parts)
        or pure.as_posix() != value
        or any(_forbidden_unicode(character) for character in value)
    ):
        raise PublishError(f"unsafe tree inventory path: {value!r}")
    return pure.parts


def _open_bound_directory(path: Path, expected: str) -> int:
    absolute = Path(os.path.abspath(path))
    for component_name in absolute.parts[1:]:
        _safe_name(component_name)
    try:
        lexical = os.lstat(absolute)
    except OSError as error:
        raise PublishError(f"cannot inspect directory {absolute}: {error}") from error
    if not stat.S_ISDIR(lexical.st_mode):
        raise PublishError(f"directory is linked or non-directory: {absolute}")
    try:
        descriptor = os.open(absolute, OPEN_DIRECTORY)
    except OSError as error:
        raise PublishError(f"cannot bind directory {absolute}: {error}") from error
    status = os.fstat(descriptor)
    if identity(lexical) != expected or identity(status) != expected:
        os.close(descriptor)
        raise PublishError(f"directory identity changed: {absolute}")
    return descriptor


def _require_trusted_ancestors(path: Path) -> None:
    """Enforce the caller's path trust boundary.

    Same-UID processes are deliberately inside the trusted computing base: flock serializes
    cooperating stage transactions but cannot stop a malicious peer that ignores advisory locks.
    """

    absolute = Path(os.path.abspath(path))
    allowed_owners = {os.geteuid(), 0}
    components = (Path(absolute.root), *absolute.parents[::-1][1:], absolute)
    seen: set[Path] = set()
    for component in components:
        if component in seen:
            continue
        seen.add(component)
        try:
            status = os.lstat(component)
        except OSError as error:
            raise PublishError(
                f"cannot inspect trusted parent ancestor {component}: {error}"
            ) from error
        if not stat.S_ISDIR(status.st_mode):
            raise PublishError(
                f"trusted parent ancestor is linked or non-directory: {component}"
            )
        if status.st_uid not in allowed_owners:
            raise PublishError(
                f"trusted parent ancestor has untrusted owner uid {status.st_uid}: {component}"
            )
        shared_writable = bool(status.st_mode & (stat.S_IWGRP | stat.S_IWOTH))
        if shared_writable and not status.st_mode & stat.S_ISVTX:
            raise PublishError(
                f"trusted parent ancestor is shared-writable without sticky bit: {component}"
            )


def _require_parent_lock(parent_fd: int, lock_fd: int) -> None:
    try:
        lock_status = os.fstat(lock_fd)
    except OSError as error:
        raise PublishError(
            f"cannot inspect inherited parent lock fd {lock_fd}: {error}"
        ) from error
    parent_status = os.fstat(parent_fd)
    if not stat.S_ISDIR(lock_status.st_mode) or identity(lock_status) != identity(
        parent_status
    ):
        raise PublishError(
            "inherited lock fd is not the exact publication parent directory"
        )

    try:
        fcntl.flock(parent_fd, fcntl.LOCK_EX | fcntl.LOCK_NB)
    except BlockingIOError:
        pass
    except OSError as error:
        if error.errno in (errno.ENOSYS, errno.EOPNOTSUPP, errno.ENOTSUP):
            raise UnsupportedPublish(
                "atomic publication is unsupported: parent directory flock unavailable"
            ) from error
        raise PublishError(f"cannot verify parent transaction lock: {error}") from error
    else:
        fcntl.flock(parent_fd, fcntl.LOCK_UN)
        raise PublishError("caller does not hold the required parent transaction lock")

    try:
        # flock is attached to the open file description. Re-locking the inherited descriptor is
        # a no-op only when this exact descriptor owns the lock detected through parent_fd above.
        fcntl.flock(lock_fd, fcntl.LOCK_EX | fcntl.LOCK_NB)
    except OSError as error:
        raise PublishError(
            "inherited parent lock fd does not own the exclusive flock"
        ) from error


def _open_locked_parent(args: argparse.Namespace) -> int:
    _require_trusted_ancestors(args.parent)
    descriptor = _open_bound_directory(args.parent, args.parent_id)
    try:
        _require_parent_lock(descriptor, args.lock_fd)
    except BaseException:
        os.close(descriptor)
        raise
    return descriptor


def _entry_status(parent_fd: int, name: str) -> os.stat_result | None:
    try:
        return os.stat(name, dir_fd=parent_fd, follow_symlinks=False)
    except FileNotFoundError:
        return None


def _require_directory_entry(
    parent_fd: int, name: str, expected: str
) -> os.stat_result:
    status = _entry_status(parent_fd, name)
    if (
        status is None
        or not stat.S_ISDIR(status.st_mode)
        or identity(status) != expected
    ):
        raise PublishError(f"sibling directory identity changed: {name}")
    return status


def _require_regular_entry(parent_fd: int, name: str, expected: str) -> os.stat_result:
    status = _entry_status(parent_fd, name)
    if (
        status is None
        or not stat.S_ISREG(status.st_mode)
        or identity(status) != expected
    ):
        raise PublishError(f"sibling regular-file identity changed: {name}")
    return status


def _require_entry(
    parent_fd: int, name: str, expected: str, kind: str
) -> os.stat_result:
    if kind == "directory":
        return _require_directory_entry(parent_fd, name, expected)
    if kind == "file":
        return _require_regular_entry(parent_fd, name, expected)
    raise PublishError(f"unsupported sibling entry kind: {kind}")


def _entry_matches(status: os.stat_result | None, expected: str, kind: str) -> bool:
    if status is None or identity(status) != expected:
        return False
    if kind == "directory":
        return stat.S_ISDIR(status.st_mode)
    if kind == "file":
        return stat.S_ISREG(status.st_mode)
    raise PublishError(f"unsupported sibling entry kind: {kind}")


def _entry_kind(status: os.stat_result) -> str:
    if stat.S_ISDIR(status.st_mode):
        return "directory"
    if stat.S_ISREG(status.st_mode):
        return "file"
    if stat.S_ISLNK(status.st_mode):
        return "symlink"
    return "other"


def _ordered_entries(directory_fd: int, *, reverse: bool = False):
    with os.scandir(directory_fd) as scanned:
        entries = list(scanned)
    for entry in entries:
        _safe_name(entry.name)
    return sorted(
        entries,
        key=lambda entry: entry.name.encode("utf-8"),
        reverse=reverse,
    )


def _fsync_directory(descriptor: int) -> None:
    try:
        os.fsync(descriptor)
    except OSError as error:
        if error.errno in (errno.EINVAL, errno.EOPNOTSUPP):
            raise UnsupportedPublish(
                "durable atomic publication is unsupported: directory fsync unavailable"
            ) from error
        raise


def _open_child_directory(
    parent_fd: int,
    name: str,
    root_device: int,
    expected: str | None = None,
) -> int:
    descriptor = os.open(name, OPEN_DIRECTORY, dir_fd=parent_fd)
    status = os.fstat(descriptor)
    if status.st_dev != root_device:
        os.close(descriptor)
        raise PublishError(f"tree contains a mount boundary: {name}")
    if expected is not None and identity(status) != expected:
        os.close(descriptor)
        raise PublishError(f"directory identity changed while binding child: {name}")
    return descriptor


def _read_inventory_descriptor(descriptor: int) -> tuple[str, ...]:
    status = os.fstat(descriptor)
    if not (stat.S_ISREG(status.st_mode) or stat.S_ISFIFO(status.st_mode)):
        raise PublishError("tree inventory fd is not a regular file or pipe")
    if stat.S_ISREG(status.st_mode) and status.st_size > MAX_INVENTORY_BYTES:
        raise PublishError("tree inventory exceeds the bounded input limit")
    payload = _read_bounded_descriptor(
        descriptor,
        MAX_INVENTORY_BYTES,
        "tree inventory exceeds the bounded input limit",
    )
    try:
        rows = bytes(payload).decode("utf-8").splitlines()
    except UnicodeDecodeError as error:
        raise PublishError("tree inventory is not UTF-8") from error
    if not rows:
        raise PublishError("tree inventory is empty")
    for row in rows:
        _safe_relative(row)
    encoded = [row.encode("utf-8") for row in rows]
    if encoded != sorted(encoded) or len(rows) != len(set(rows)):
        raise PublishError("tree inventory must be unique and bytewise sorted")
    return tuple(rows)


def _read_inventory(path: Path) -> tuple[str, ...]:
    descriptor = os.open(path, OPEN_FILE)
    try:
        return _read_inventory_descriptor(descriptor)
    finally:
        os.close(descriptor)


def _walk_tree(root_fd: int) -> tuple[tuple[str, ...], tuple[str, ...]]:
    root_device = os.fstat(root_fd).st_dev
    directories: list[str] = []
    files: list[str] = []

    def walk(directory_fd: int, prefix: tuple[str, ...]) -> None:
        ordered = _ordered_entries(directory_fd)
        for entry in ordered:
            relative = "/".join((*prefix, entry.name))
            status = entry.stat(follow_symlinks=False)
            if status.st_dev != root_device:
                raise PublishError(f"tree contains a mount boundary: {relative}")
            if stat.S_ISDIR(status.st_mode):
                directories.append(relative)
                child_fd = _open_child_directory(
                    directory_fd, entry.name, root_device, identity(status)
                )
                try:
                    walk(child_fd, (*prefix, entry.name))
                finally:
                    os.close(child_fd)
            elif stat.S_ISREG(status.st_mode):
                files.append(relative)
            else:
                raise PublishError(f"tree contains a link or special file: {relative}")

    walk(root_fd, ())
    return tuple(directories), tuple(files)


def _open_relative_file(
    root_fd: int, parts: tuple[str, ...]
) -> tuple[int, os.stat_result]:
    directory_fd = os.dup(root_fd)
    root_device = os.fstat(root_fd).st_dev
    try:
        for component in parts[:-1]:
            child_fd = _open_child_directory(directory_fd, component, root_device)
            os.close(directory_fd)
            directory_fd = child_fd
        descriptor = os.open(parts[-1], OPEN_FILE, dir_fd=directory_fd)
    finally:
        os.close(directory_fd)
    status = os.fstat(descriptor)
    if not stat.S_ISREG(status.st_mode) or status.st_dev != root_device:
        os.close(descriptor)
        raise PublishError(f"source is linked, special, or mounted: {'/'.join(parts)}")
    return descriptor, status


def _open_relative_directory(
    root_fd: int, parts: tuple[str, ...]
) -> tuple[int, os.stat_result]:
    directory_fd = os.dup(root_fd)
    root_device = os.fstat(root_fd).st_dev
    try:
        for component in parts:
            child_fd = _open_child_directory(directory_fd, component, root_device)
            os.close(directory_fd)
            directory_fd = child_fd
        status = os.fstat(directory_fd)
        return directory_fd, status
    except BaseException:
        os.close(directory_fd)
        raise


def _ensure_destination_directory(root_fd: int, parts: tuple[str, ...]) -> int:
    directory_fd = os.dup(root_fd)
    root_device = os.fstat(root_fd).st_dev
    try:
        for component in parts:
            try:
                os.mkdir(component, 0o755, dir_fd=directory_fd)
                _test_signal("materialize-after-directory-create")
                _signal_checkpoint()
            except FileExistsError:
                pass
            child_fd = _open_child_directory(directory_fd, component, root_device)
            os.close(directory_fd)
            directory_fd = child_fd
        return directory_fd
    except BaseException:
        os.close(directory_fd)
        raise


def _copy_file(source_fd: int, destination_fd: int) -> str:
    digest = hashlib.sha256()
    while block := os.read(source_fd, 1024 * 1024):
        digest.update(block)
        view = memoryview(block)
        while view:
            written = os.write(destination_fd, view)
            view = view[written:]
        _signal_checkpoint()
    return digest.hexdigest()


def _fsync_tree(root_fd: int) -> None:
    root_device = os.fstat(root_fd).st_dev

    def sync_directory(directory_fd: int) -> None:
        ordered = _ordered_entries(directory_fd)
        for entry in ordered:
            status = entry.stat(follow_symlinks=False)
            if status.st_dev != root_device:
                raise PublishError(
                    f"tree contains a mount boundary while syncing: {entry.name}"
                )
            if stat.S_ISDIR(status.st_mode):
                child_fd = _open_child_directory(
                    directory_fd, entry.name, root_device, identity(status)
                )
                try:
                    sync_directory(child_fd)
                finally:
                    os.close(child_fd)
            elif not stat.S_ISREG(status.st_mode):
                raise PublishError(
                    f"tree contains a link or special file while syncing: {entry.name}"
                )
        _fsync_directory(directory_fd)

    sync_directory(root_fd)


def _directory_mode_map(source_fd: int, directories: Sequence[str]) -> dict[str, int]:
    result = {"": stat.S_IMODE(os.fstat(source_fd).st_mode)}
    for relative in directories:
        descriptor, status = _open_relative_directory(
            source_fd, _safe_relative(relative)
        )
        try:
            result[relative] = stat.S_IMODE(status.st_mode)
        finally:
            os.close(descriptor)
    return result


def _finalize_tree_modes(root_fd: int, modes: dict[str, int]) -> None:
    root_device = os.fstat(root_fd).st_dev
    visited: set[str] = set()

    def finalize(directory_fd: int, prefix: tuple[str, ...]) -> None:
        ordered = _ordered_entries(directory_fd)
        for entry in ordered:
            status = entry.stat(follow_symlinks=False)
            relative_parts = (*prefix, entry.name)
            relative = "/".join(relative_parts)
            if status.st_dev != root_device:
                raise PublishError(
                    f"tree contains a mount boundary while finalizing: {relative}"
                )
            if stat.S_ISDIR(status.st_mode):
                child_fd = _open_child_directory(
                    directory_fd, entry.name, root_device, identity(status)
                )
                try:
                    finalize(child_fd, relative_parts)
                finally:
                    os.close(child_fd)
            elif not stat.S_ISREG(status.st_mode):
                raise PublishError(
                    f"tree contains a link or special file while finalizing: {relative}"
                )
        relative = "/".join(prefix)
        if relative not in modes:
            raise PublishError(
                f"destination directory lacks source mode: {relative or '.'}"
            )
        os.fchmod(directory_fd, modes[relative])
        if stat.S_IMODE(os.fstat(directory_fd).st_mode) != modes[relative]:
            raise PublishError(
                f"destination directory mode could not be preserved: {relative or '.'}"
            )
        _test_signal("materialize-after-directory-mode")
        _signal_checkpoint()
        _fsync_directory(directory_fd)
        visited.add(relative)

    finalize(root_fd, ())
    missing = set(modes) - visited
    if missing:
        raise PublishError(
            "source directories missing from destination: "
            + ", ".join(sorted(missing, key=lambda value: value.encode("utf-8")))
        )


def _materialize(args: argparse.Namespace) -> None:
    source_fd = _open_bound_directory(args.source, args.source_id)
    parent_fd = _open_locked_parent(args)
    name = _safe_name(args.name)
    created_id = ""
    tree_fd = -1
    try:
        if args.inventory is None and args.inventory_fd is None:
            directories, files = _walk_tree(source_fd)
        elif args.inventory_fd is not None:
            files = _read_inventory_descriptor(args.inventory_fd)
            directories = ()
        else:
            files = _read_inventory(args.inventory)
            directories = ()

        directory_names = set(directories)
        for relative in files:
            parts = _safe_relative(relative)
            for count in range(1, len(parts)):
                directory_names.add("/".join(parts[:count]))
        ordered_directories = tuple(
            sorted(
                directory_names,
                key=lambda value: (value.count("/"), value.encode("utf-8")),
            )
        )
        directory_modes = _directory_mode_map(source_fd, ordered_directories)

        _test_signal("materialize-before-mkdir")
        _signal_checkpoint()
        with _defer_signals():
            if _entry_status(parent_fd, name) is not None:
                raise PublishError(f"private sibling already exists: {name}")
            os.mkdir(name, 0o700, dir_fd=parent_fd)
            created_status = _entry_status(parent_fd, name)
            if created_status is None or not stat.S_ISDIR(created_status.st_mode):
                raise PublishError(
                    f"private sibling creation did not bind a directory: {name}"
                )
            created_id = identity(created_status)
            _test_signal("materialize-after-mkdir")
            tree_fd = _open_child_directory(
                parent_fd,
                name,
                os.fstat(parent_fd).st_dev,
                created_id,
            )
        try:
            for relative in ordered_directories:
                directory_fd = _ensure_destination_directory(
                    tree_fd, _safe_relative(relative)
                )
                os.close(directory_fd)
            _test_signal("materialize-after-directories")
            _signal_checkpoint()
            for relative in files:
                parts = _safe_relative(relative)
                source_file, source_status = _open_relative_file(source_fd, parts)
                destination_parent = _ensure_destination_directory(tree_fd, parts[:-1])
                try:
                    _test_signal("materialize-file-before-create")
                    _signal_checkpoint()
                    destination_file = os.open(
                        parts[-1],
                        os.O_WRONLY
                        | os.O_CREAT
                        | os.O_EXCL
                        | os.O_CLOEXEC
                        | os.O_NOFOLLOW,
                        0o600,
                        dir_fd=destination_parent,
                    )
                    try:
                        _test_signal("materialize-file-after-create")
                        _signal_checkpoint()
                        _copy_file(source_file, destination_file)
                        _test_signal("materialize-file-after-copy")
                        _signal_checkpoint()
                        source_after = os.fstat(source_file)
                        if (
                            source_after.st_size != source_status.st_size
                            or source_after.st_mtime_ns != source_status.st_mtime_ns
                            or source_after.st_ctime_ns != source_status.st_ctime_ns
                            or stat.S_IMODE(source_after.st_mode)
                            != stat.S_IMODE(source_status.st_mode)
                        ):
                            raise PublishError(
                                f"source changed while copying: {relative}"
                            )
                        source_mode = stat.S_IMODE(source_status.st_mode)
                        os.fchmod(destination_file, source_mode)
                        if (
                            stat.S_IMODE(os.fstat(destination_file).st_mode)
                            != source_mode
                        ):
                            raise PublishError(
                                f"destination file mode could not be preserved: {relative}"
                            )
                        _test_signal("materialize-file-after-chmod")
                        _signal_checkpoint()
                        os.fsync(destination_file)
                        _test_signal("materialize-file-after-fsync")
                        _signal_checkpoint()
                    finally:
                        os.close(destination_file)
                finally:
                    os.close(source_file)
                    os.close(destination_parent)
                _test_signal("materialize-after-file")
                _signal_checkpoint()
            _finalize_tree_modes(tree_fd, directory_modes)
            _test_signal("materialize-after-tree-fsync")
            _signal_checkpoint()
        finally:
            os.close(tree_fd)
            tree_fd = -1
        # Persist the new private sibling name as well as every entry below it.  Backups are
        # returned directly under this name; publication candidates are renamed only later.
        _test_signal("materialize-before-parent-fsync")
        _signal_checkpoint()
        _fsync_directory(parent_fd)
        _test_signal("materialize-after-parent-fsync")
        _signal_checkpoint()
        print(f"{name}\t{created_id}")
    except BaseException as operation_error:
        if tree_fd >= 0:
            os.close(tree_fd)
            tree_fd = -1
        if created_id:
            try:
                with _defer_signals(deliver_on_exit=False):
                    _remove_named_tree(parent_fd, name, created_id)
            except BaseException as cleanup_error:
                raise RetainedEntry(
                    "materialization failed and exact cleanup failed; "
                    f"identity {created_id} may be retained from {name}: {cleanup_error}"
                ) from operation_error
        raise
    finally:
        os.close(source_fd)
        os.close(parent_fd)


def _renameat2(
    directory_fd: int,
    old: str,
    new: str,
    flags: int,
    *,
    test_label: str | None = None,
) -> None:
    libc = ctypes.CDLL(None, use_errno=True)
    function = getattr(libc, "renameat2", None)
    if function is None:
        raise UnsupportedPublish(
            "atomic directory publication is unsupported: renameat2 unavailable"
        )
    function.argtypes = [
        ctypes.c_int,
        ctypes.c_char_p,
        ctypes.c_int,
        ctypes.c_char_p,
        ctypes.c_uint,
    ]
    function.restype = ctypes.c_int
    with _defer_signals():
        if (
            function(
                directory_fd,
                os.fsencode(old),
                directory_fd,
                os.fsencode(new),
                flags,
            )
            == 0
        ):
            # Namespace mutation already committed if fsync fails. Callers classify identities;
            # this helper never guesses from status and never performs a blind reverse rename.
            if test_label is not None:
                _test_signal(f"{test_label}-after-rename")
            _fsync_directory(directory_fd)
            if test_label is not None:
                _test_signal(f"{test_label}-after-fsync")
            return
    error_number = ctypes.get_errno()
    if error_number in UNSUPPORTED_ERRNOS:
        raise UnsupportedPublish(
            "atomic directory publication is unsupported by this filesystem: "
            + os.strerror(error_number)
        )
    raise PublishError(
        f"atomic directory publication failed: {os.strerror(error_number)}"
    )


def _probe(args: argparse.Namespace) -> None:
    parent_fd = _open_locked_parent(args)
    prefix = ".taf-atomic-probe-" + secrets.token_bytes(10).hex()
    left = prefix + "-left"
    right = prefix + "-right"
    source = prefix + "-source"
    destination = prefix + "-destination"
    created: dict[str, str] = {}
    operation_error: BaseException | None = None
    try:
        for name in (left, right, source):
            with _defer_signals():
                os.mkdir(name, 0o700, dir_fd=parent_fd)
                status = os.stat(name, dir_fd=parent_fd, follow_symlinks=False)
                if not stat.S_ISDIR(status.st_mode):
                    raise PublishError(f"atomic probe entry is not a directory: {name}")
                created[name] = identity(status)
                _test_signal("probe-after-create")
        # Reject filesystems without durable directory operations before the first rename.
        _test_signal("probe-before-parent-fsync")
        _signal_checkpoint()
        _fsync_directory(parent_fd)
        _test_signal("probe-after-parent-fsync")
        _signal_checkpoint()
        _renameat2(
            parent_fd,
            left,
            right,
            RENAME_EXCHANGE,
            test_label="probe-exchange-forward",
        )
        if not _entry_matches(
            _entry_status(parent_fd, left), created[right], "directory"
        ) or not _entry_matches(
            _entry_status(parent_fd, right), created[left], "directory"
        ):
            raise PublishError(
                "atomic directory exchange did not preserve both identities"
            )
        _renameat2(
            parent_fd,
            left,
            right,
            RENAME_EXCHANGE,
            test_label="probe-exchange-reverse",
        )
        if not _entry_matches(
            _entry_status(parent_fd, left), created[left], "directory"
        ) or not _entry_matches(
            _entry_status(parent_fd, right), created[right], "directory"
        ):
            raise PublishError(
                "atomic directory reverse exchange did not preserve both identities"
            )
        _renameat2(
            parent_fd,
            source,
            destination,
            RENAME_NOREPLACE,
            test_label="probe-publish",
        )
        created[destination] = created.pop(source)
        if _entry_status(parent_fd, source) is not None or not _entry_matches(
            _entry_status(parent_fd, destination), created[destination], "directory"
        ):
            raise PublishError("atomic no-replace publication lost its exact identity")
    except BaseException as error:
        operation_error = error

    cleanup_errors: list[str] = []
    cleanup_durability_error: BaseException | None = None
    remaining = set(created.values())
    with _defer_signals(deliver_on_exit=False):
        # Fsync can fail after a rename has committed. Find every probe identity under either
        # possible name instead of trusting pre-rename bookkeeping.
        for name in (left, right, source, destination):
            try:
                status = _entry_status(parent_fd, name)
                if (
                    status is not None
                    and identity(status) in remaining
                    and stat.S_ISDIR(status.st_mode)
                ):
                    expected = identity(status)
                    descriptor = _open_child_directory(
                        parent_fd,
                        name,
                        os.fstat(parent_fd).st_dev,
                        expected,
                    )
                    try:
                        _require_directory_entry(parent_fd, name, expected)
                        os.rmdir(name, dir_fd=parent_fd)
                    finally:
                        os.close(descriptor)
                    remaining.remove(identity(status))
            except BaseException as cleanup_error:
                cleanup_errors.append(f"{name}: {cleanup_error}")
        if remaining:
            cleanup_errors.append(
                "probe identities no longer occupy their owned names: "
                + ", ".join(sorted(remaining))
            )
        try:
            _fsync_directory(parent_fd)
        except BaseException as cleanup_error:
            cleanup_durability_error = cleanup_error
    os.close(parent_fd)
    if cleanup_errors:
        error = RetainedEntry(
            "atomic probe cleanup failed: " + "; ".join(cleanup_errors)
        )
        if operation_error is not None:
            raise error from operation_error
        raise error
    if cleanup_durability_error is not None:
        if isinstance(operation_error, UnsupportedPublish) and isinstance(
            cleanup_durability_error, UnsupportedPublish
        ):
            raise UnsupportedPublish(
                f"{operation_error}; probe names were removed, but cleanup durability "
                "is likewise unsupported"
            ) from operation_error
        error = PublishError(
            "atomic probe names were removed, but cleanup durability failed: "
            f"{cleanup_durability_error}"
        )
        if operation_error is not None:
            raise error from operation_error
        raise error
    if operation_error is not None:
        raise operation_error
    _signal_checkpoint()
    print("ATOMIC TREE PUBLICATION SUPPORTED")


def _publication_state_value(
    parent_fd: int,
    source: str,
    source_id: str,
    destination: str,
    destination_id: str,
    kind: str,
) -> str:
    source_status = _entry_status(parent_fd, source)
    destination_status = _entry_status(parent_fd, destination)
    source_is_new = _entry_matches(source_status, source_id, kind)
    destination_is_new = _entry_matches(destination_status, source_id, kind)

    if destination_id == "absent":
        if source_is_new and destination_status is None:
            return "before"
        if source_status is None and destination_is_new:
            return "after"
        if source_status is None and destination_status is None:
            return "rolled-back"
    else:
        source_is_old = _entry_matches(source_status, destination_id, kind)
        destination_is_old = _entry_matches(destination_status, destination_id, kind)
        if source_is_new and destination_is_old:
            return "before"
        if source_is_old and destination_is_new:
            return "after"
        if source_status is None and destination_is_old:
            return "rolled-back"
        if source_status is None and destination_is_new:
            return "accepted"
    raise PublishError("publication identity state is ambiguous")


def _state(args: argparse.Namespace) -> None:
    parent_fd = _open_locked_parent(args)
    try:
        print(
            _publication_state_value(
                parent_fd,
                _safe_name(args.source),
                args.source_id,
                _safe_name(args.destination),
                args.destination_id,
                args.kind,
            )
        )
    finally:
        os.close(parent_fd)


def _exchange(args: argparse.Namespace) -> None:
    parent_fd = _open_locked_parent(args)
    try:
        left = _safe_name(args.left)
        right = _safe_name(args.right)
        left_status = _require_entry(parent_fd, left, args.left_id, args.kind)
        right_status = _require_entry(parent_fd, right, args.right_id, args.kind)
        if left_status.st_dev != right_status.st_dev:
            raise UnsupportedPublish("atomic entry exchange requires one filesystem")
        _renameat2(
            parent_fd,
            left,
            right,
            RENAME_EXCHANGE,
            test_label="exchange",
        )
        if (
            _publication_state_value(
                parent_fd, left, args.left_id, right, args.right_id, args.kind
            )
            != "after"
        ):
            raise PublishError("atomic entry exchange postcondition failed")
    finally:
        os.close(parent_fd)


def _publish(args: argparse.Namespace) -> None:
    parent_fd = _open_locked_parent(args)
    try:
        source = _safe_name(args.source)
        destination = _safe_name(args.destination)
        _require_entry(parent_fd, source, args.source_id, args.kind)
        if args.destination_id == "absent":
            if _entry_status(parent_fd, destination) is not None:
                raise PublishError(f"publication destination appeared: {destination}")
            _renameat2(
                parent_fd,
                source,
                destination,
                RENAME_NOREPLACE,
                test_label="publish",
            )
        else:
            _require_entry(parent_fd, destination, args.destination_id, args.kind)
            _renameat2(
                parent_fd,
                source,
                destination,
                RENAME_EXCHANGE,
                test_label="publish",
            )
        if (
            _publication_state_value(
                parent_fd,
                source,
                args.source_id,
                destination,
                args.destination_id,
                args.kind,
            )
            != "after"
        ):
            raise PublishError("atomic publication postcondition failed")
    finally:
        os.close(parent_fd)


def _sequester_entry(parent_fd: int, name: str, expected: str, kind: str) -> str:
    _require_entry(parent_fd, name, expected, kind)
    quarantine = ""
    for _attempt in range(128):
        proposed = ".taf-remove-" + secrets.token_bytes(16).hex()
        if _entry_status(parent_fd, proposed) is None:
            quarantine = proposed
            break
    if not quarantine:
        raise PublishError("cannot allocate a private cleanup quarantine name")
    try:
        _renameat2(
            parent_fd,
            name,
            quarantine,
            RENAME_NOREPLACE,
            test_label="remove-sequester",
        )
    except BaseException as operation_error:
        source_status = _entry_status(parent_fd, name)
        quarantine_status = _entry_status(parent_fd, quarantine)
        if source_status is None and _entry_matches(quarantine_status, expected, kind):
            raise RetainedEntry(
                f"cleanup sequestered {kind} as {quarantine} identity {expected}, "
                "but durable rename confirmation failed"
            ) from operation_error
        if _entry_matches(source_status, expected, kind) and quarantine_status is None:
            raise
        raise RetainedEntry(
            f"cleanup rename left ambiguous identity {expected} between {name} and {quarantine}"
        ) from operation_error
    if _entry_status(parent_fd, name) is not None or not _entry_matches(
        _entry_status(parent_fd, quarantine), expected, kind
    ):
        raise RetainedEntry(
            f"cleanup sequester postcondition is ambiguous for identity {expected}: {quarantine}"
        )
    return quarantine


def _open_bound_regular(parent_fd: int, name: str, expected: str) -> int:
    flags = getattr(os, "O_PATH", os.O_RDONLY) | os.O_CLOEXEC | os.O_NOFOLLOW
    descriptor = os.open(name, flags, dir_fd=parent_fd)
    status = os.fstat(descriptor)
    if not stat.S_ISREG(status.st_mode) or identity(status) != expected:
        os.close(descriptor)
        raise PublishError(f"regular-file identity changed while binding child: {name}")
    return descriptor


def _remove_named_tree(parent_fd: int, name: str, expected: str) -> None:
    with _defer_signals():
        # Seal exact owned root before namespace mutation. If untrappable death lands immediately
        # after sequester rename, other UIDs still cannot enter and race child names.
        seal_fd = -1
        try:
            seal_status = _require_directory_entry(parent_fd, name, expected)
            seal_fd = _open_child_directory(
                parent_fd, name, seal_status.st_dev, expected
            )
            os.fchmod(seal_fd, 0o700)
            if stat.S_IMODE(os.fstat(seal_fd).st_mode) != 0o700:
                raise PublishError("cleanup root could not be permission-sealed")
            _fsync_directory(seal_fd)
        except BaseException as seal_error:
            current = _entry_status(parent_fd, name)
            if _entry_matches(current, expected, "directory"):
                raise RetainedEntry(
                    f"cleanup retained directory {name} identity {expected} while sealing: "
                    f"{seal_error}"
                ) from seal_error
            raise
        finally:
            if seal_fd >= 0:
                os.close(seal_fd)

        quarantine = _sequester_entry(parent_fd, name, expected, "directory")
        root_fd = -1
        try:
            status = _require_directory_entry(parent_fd, quarantine, expected)
            root_device = status.st_dev
            root_fd = _open_child_directory(
                parent_fd, quarantine, root_device, expected
            )
            if stat.S_IMODE(os.fstat(root_fd).st_mode) != 0o700:
                raise PublishError("cleanup root lost its permission seal")

            def remove_contents(directory_fd: int, relative: str) -> None:
                ordered = _ordered_entries(directory_fd, reverse=True)
                for entry in ordered:
                    _test_signal("remove-before-child")
                    child_relative = (
                        f"{relative}/{entry.name}" if relative else entry.name
                    )
                    child_status = entry.stat(follow_symlinks=False)
                    child_id = identity(child_status)
                    if child_status.st_dev != root_device:
                        raise PublishError(
                            f"refusing cleanup across mount boundary: {child_relative}"
                        )
                    if stat.S_ISDIR(child_status.st_mode):
                        child_fd = _open_child_directory(
                            directory_fd,
                            entry.name,
                            root_device,
                            child_id,
                        )
                        try:
                            remove_contents(child_fd, child_relative)
                            if identity(os.fstat(child_fd)) != child_id:
                                raise PublishError(
                                    f"bound directory identity changed: {child_relative}"
                                )
                            _fsync_directory(child_fd)
                            _require_directory_entry(directory_fd, entry.name, child_id)
                            os.rmdir(entry.name, dir_fd=directory_fd)
                        finally:
                            os.close(child_fd)
                    elif stat.S_ISREG(child_status.st_mode):
                        child_fd = _open_bound_regular(
                            directory_fd, entry.name, child_id
                        )
                        try:
                            if identity(os.fstat(child_fd)) != child_id:
                                raise PublishError(
                                    f"bound regular-file identity changed: {child_relative}"
                                )
                            _require_regular_entry(directory_fd, entry.name, child_id)
                            os.unlink(entry.name, dir_fd=directory_fd)
                        finally:
                            os.close(child_fd)
                    else:
                        raise PublishError(
                            f"refusing cleanup of link or special file: {child_relative}"
                        )
                    _test_signal("remove-after-child")
                _fsync_directory(directory_fd)

            _test_signal("remove-before-recurse")
            remove_contents(root_fd, "")
            _test_signal("remove-before-final")
            if identity(os.fstat(root_fd)) != expected:
                raise PublishError("bound cleanup-root identity changed")
            _require_directory_entry(parent_fd, quarantine, expected)
            os.rmdir(quarantine, dir_fd=parent_fd)
        except BaseException as cleanup_error:
            current = _entry_status(parent_fd, quarantine)
            if _entry_matches(current, expected, "directory"):
                raise RetainedEntry(
                    f"partial cleanup retained directory {quarantine} identity {expected}: "
                    f"{cleanup_error}"
                ) from cleanup_error
            raise
        finally:
            if root_fd >= 0:
                os.close(root_fd)
        _fsync_directory(parent_fd)


def _remove_named_file(parent_fd: int, name: str, expected: str) -> None:
    with _defer_signals():
        quarantine = _sequester_entry(parent_fd, name, expected, "file")
        descriptor = -1
        try:
            descriptor = _open_bound_regular(parent_fd, quarantine, expected)
            _require_regular_entry(parent_fd, quarantine, expected)
            os.unlink(quarantine, dir_fd=parent_fd)
        except BaseException as cleanup_error:
            current = _entry_status(parent_fd, quarantine)
            if _entry_matches(current, expected, "file"):
                raise RetainedEntry(
                    f"cleanup retained file {quarantine} identity {expected}: {cleanup_error}"
                ) from cleanup_error
            raise
        finally:
            if descriptor >= 0:
                os.close(descriptor)
        _fsync_directory(parent_fd)


def _remove(args: argparse.Namespace) -> None:
    parent_fd = _open_locked_parent(args)
    try:
        name = _safe_name(args.name)
        if args.kind == "directory":
            _remove_named_tree(parent_fd, name, args.expected_id)
        else:
            _remove_named_file(parent_fd, name, args.expected_id)
    finally:
        os.close(parent_fd)


def _sync(args: argparse.Namespace) -> None:
    parent_fd = _open_locked_parent(args)
    try:
        _fsync_directory(parent_fd)
    finally:
        os.close(parent_fd)


def _sync_file(args: argparse.Namespace) -> None:
    parent_fd = _open_locked_parent(args)
    descriptor = -1
    try:
        name = _safe_name(args.name)
        _require_regular_entry(parent_fd, name, args.expected_id)
        descriptor = os.open(name, OPEN_FILE, dir_fd=parent_fd)
        if identity(os.fstat(descriptor)) != args.expected_id:
            raise PublishError(f"sibling regular-file identity changed: {name}")
        os.fsync(descriptor)
        _fsync_directory(parent_fd)
    finally:
        if descriptor >= 0:
            os.close(descriptor)
        os.close(parent_fd)


def _inspect(args: argparse.Namespace) -> None:
    parent_fd = _open_locked_parent(args)
    try:
        status = _entry_status(parent_fd, _safe_name(args.name))
        if status is None:
            print("absent")
        else:
            print(f"{_entry_kind(status)}\t{identity(status)}")
    finally:
        os.close(parent_fd)


def _list_prefix(args: argparse.Namespace) -> None:
    parent_fd = _open_locked_parent(args)
    try:
        prefix = _safe_name(args.prefix)
        matches: list[tuple[bytes, str, str, str]] = []
        with os.scandir(parent_fd) as scanned:
            for entry in scanned:
                if not entry.name.startswith(prefix):
                    continue
                _safe_name(entry.name)
                status = entry.stat(follow_symlinks=False)
                matches.append(
                    (
                        entry.name.encode("utf-8"),
                        entry.name,
                        _entry_kind(status),
                        identity(status),
                    )
                )
        for _encoded, name, kind, entry_id in sorted(matches):
            print(f"{name}\t{kind}\t{entry_id}")
    finally:
        os.close(parent_fd)


def _locate(args: argparse.Namespace) -> None:
    parent_fd = _open_locked_parent(args)
    try:
        matches: list[tuple[bytes, str, str]] = []
        with os.scandir(parent_fd) as scanned:
            for entry in scanned:
                status = entry.stat(follow_symlinks=False)
                if not _entry_matches(status, args.expected_id, args.kind):
                    continue
                _safe_name(entry.name)
                matches.append(
                    (entry.name.encode("utf-8"), entry.name, identity(status))
                )
        for _encoded, name, entry_id in sorted(matches):
            print(f"{name}\t{args.kind}\t{entry_id}")
    finally:
        os.close(parent_fd)


def _read_bounded_stdin() -> bytes:
    return _read_bounded_descriptor(
        sys.stdin.fileno(),
        MAX_BUFFERED_FILE_BYTES,
        "buffered file input exceeds the bounded input limit",
    )


def _read_bounded_descriptor(descriptor: int, limit: int, too_large: str) -> bytes:
    payload = bytearray()
    original_flags = fcntl.fcntl(descriptor, fcntl.F_GETFL)
    poller = select.poll()
    poller.register(
        descriptor,
        select.POLLIN | select.POLLHUP | select.POLLERR | select.POLLNVAL,
    )
    fcntl.fcntl(descriptor, fcntl.F_SETFL, original_flags | os.O_NONBLOCK)
    try:
        wait_marker = os.environ.get("TAF_ATOMIC_TEST_INPUT_WAIT_MARKER")
        if wait_marker:
            try:
                marker_fd = os.open(
                    wait_marker,
                    os.O_WRONLY | os.O_CREAT | os.O_EXCL | os.O_CLOEXEC | os.O_NOFOLLOW,
                    0o600,
                )
            except FileExistsError:
                pass
            else:
                os.close(marker_fd)
        while True:
            _signal_checkpoint()
            events = poller.poll(100)
            _signal_checkpoint()
            if not events:
                continue
            if events[0][1] & select.POLLNVAL:
                raise PublishError("buffered input descriptor became invalid")
            try:
                block = os.read(
                    descriptor,
                    min(1024 * 1024, limit + 1 - len(payload)),
                )
            except BlockingIOError:
                continue
            if not block:
                return bytes(payload)
            payload.extend(block)
            if len(payload) > limit:
                raise PublishError(too_large)
    finally:
        fcntl.fcntl(descriptor, fcntl.F_SETFL, original_flags)


def _parse_mode(value: str) -> int:
    try:
        mode = int(value, 8)
    except ValueError as error:
        raise argparse.ArgumentTypeError(
            "mode must be an octal permission value"
        ) from error
    if mode < 0 or mode > 0o7777:
        raise argparse.ArgumentTypeError("mode must be between 0000 and 7777")
    return mode


def _write_file(args: argparse.Namespace) -> None:
    # Buffer before allocating/truncating any name. Receipt-sized payloads are bounded so a signal
    # or producer failure cannot expose a half-streamed private file.
    payload = _read_bounded_stdin()
    _signal_checkpoint()
    parent_fd = _open_locked_parent(args)
    name = _safe_name(args.name)
    descriptor = -1
    written_id = ""
    created = False
    try:
        with _defer_signals():
            if args.expected_id == "absent":
                if _entry_status(parent_fd, name) is not None:
                    raise PublishError(f"private file already exists: {name}")
                descriptor = os.open(
                    name,
                    os.O_WRONLY | os.O_CREAT | os.O_EXCL | os.O_CLOEXEC | os.O_NOFOLLOW,
                    0o600,
                    dir_fd=parent_fd,
                )
                created = True
            else:
                _require_regular_entry(parent_fd, name, args.expected_id)
                descriptor = os.open(
                    name,
                    os.O_WRONLY | os.O_CLOEXEC | os.O_NOFOLLOW,
                    dir_fd=parent_fd,
                )
            opened = os.fstat(descriptor)
            written_id = identity(opened)
            if not stat.S_ISREG(opened.st_mode) or (
                args.expected_id != "absent" and written_id != args.expected_id
            ):
                raise PublishError(f"private regular-file identity changed: {name}")
            if opened.st_nlink != 1:
                raise PublishError(
                    f"refusing to truncate multiply-linked private file: {name}"
                )
            named = _require_regular_entry(parent_fd, name, written_id)
            if named.st_nlink != 1 or identity(os.fstat(descriptor)) != written_id:
                raise PublishError(f"private regular-file identity changed: {name}")
            _test_signal("write-file-after-create")
            os.ftruncate(descriptor, 0)
            view = memoryview(payload)
            while view:
                count = os.write(descriptor, view)
                view = view[count:]
            _test_signal("write-file-after-write")
            os.fchmod(descriptor, args.mode)
            if stat.S_IMODE(os.fstat(descriptor).st_mode) != args.mode:
                raise PublishError(
                    f"private file mode could not be set exactly: {name}"
                )
            os.fsync(descriptor)
            _test_signal("write-file-after-fsync")
            _fsync_directory(parent_fd)
            print(f"{name}\t{written_id}")
    except BaseException as operation_error:
        if descriptor >= 0:
            os.close(descriptor)
            descriptor = -1
        if created and written_id:
            try:
                with _defer_signals(deliver_on_exit=False):
                    _remove_named_file(parent_fd, name, written_id)
            except BaseException as cleanup_error:
                raise RetainedEntry(
                    "private-file write failed and exact cleanup failed; "
                    f"identity {written_id} may be retained from {name}: {cleanup_error}"
                ) from operation_error
        elif written_id:
            raise RetainedEntry(
                f"private-file write failed; exact identity {written_id} retained as {name}"
            ) from operation_error
        raise
    finally:
        if descriptor >= 0:
            os.close(descriptor)
        os.close(parent_fd)


def _compare(args: argparse.Namespace) -> None:
    left_fd = _open_bound_directory(args.left, args.left_id)
    right_fd = _open_bound_directory(args.right, args.right_id)
    try:
        left_dirs, left_files = _walk_tree(left_fd)
        right_dirs, right_files = _walk_tree(right_fd)
        if left_dirs != right_dirs or left_files != right_files:
            raise PublishError("tree inventories differ")
        if stat.S_IMODE(os.fstat(left_fd).st_mode) != stat.S_IMODE(
            os.fstat(right_fd).st_mode
        ):
            raise PublishError("tree root modes differ")
        for relative in left_dirs:
            parts = _safe_relative(relative)
            left_directory, left_status = _open_relative_directory(left_fd, parts)
            right_directory, right_status = _open_relative_directory(right_fd, parts)
            try:
                if stat.S_IMODE(left_status.st_mode) != stat.S_IMODE(
                    right_status.st_mode
                ):
                    raise PublishError(f"tree directory modes differ: {relative}")
            finally:
                os.close(left_directory)
                os.close(right_directory)
        for relative in left_files:
            parts = _safe_relative(relative)
            left_file, left_status = _open_relative_file(left_fd, parts)
            right_file, right_status = _open_relative_file(right_fd, parts)
            try:
                if stat.S_IMODE(left_status.st_mode) != stat.S_IMODE(
                    right_status.st_mode
                ):
                    raise PublishError(f"tree modes differ: {relative}")
                while True:
                    left_block = os.read(left_file, 1024 * 1024)
                    right_block = os.read(right_file, 1024 * 1024)
                    if left_block != right_block:
                        raise PublishError(f"tree bytes differ: {relative}")
                    if not left_block:
                        break
            finally:
                os.close(left_file)
                os.close(right_file)
        print(f"ATOMIC TREE COPY VERIFIED ({len(left_files)} files)")
    finally:
        os.close(left_fd)
        os.close(right_fd)


def parser() -> argparse.ArgumentParser:
    result = argparse.ArgumentParser(description=__doc__)
    commands = result.add_subparsers(dest="command", required=True)

    def parent_arguments(command: argparse.ArgumentParser) -> None:
        command.add_argument("--parent", type=Path, required=True)
        command.add_argument("--parent-id", required=True)
        command.add_argument("--lock-fd", type=int, required=True)

    def kind_argument(command: argparse.ArgumentParser) -> None:
        command.add_argument(
            "--kind", choices=("directory", "file"), default="directory"
        )

    def publication_arguments(command: argparse.ArgumentParser) -> None:
        parent_arguments(command)
        kind_argument(command)
        command.add_argument("--source", required=True)
        command.add_argument("--source-id", required=True)
        command.add_argument("--destination", required=True)
        command.add_argument("--destination-id", required=True)

    materialize = commands.add_parser("materialize")
    materialize.add_argument("--source", type=Path, required=True)
    materialize.add_argument("--source-id", required=True)
    parent_arguments(materialize)
    inventories = materialize.add_mutually_exclusive_group()
    inventories.add_argument("--inventory", type=Path)
    inventories.add_argument("--inventory-fd", type=int)
    materialize.add_argument(
        "--name",
        required=True,
        help="caller-preallocated cryptorandom sibling basename",
    )

    probe = commands.add_parser("probe")
    parent_arguments(probe)

    exchange = commands.add_parser("exchange")
    parent_arguments(exchange)
    exchange.add_argument("--left", required=True)
    exchange.add_argument("--left-id", required=True)
    exchange.add_argument("--right", required=True)
    exchange.add_argument("--right-id", required=True)
    kind_argument(exchange)

    publish = commands.add_parser("publish")
    publication_arguments(publish)

    state = commands.add_parser("state")
    publication_arguments(state)

    remove = commands.add_parser("remove")
    parent_arguments(remove)
    remove.add_argument("--name", required=True)
    remove.add_argument("--expected-id", required=True)
    kind_argument(remove)

    sync = commands.add_parser("sync")
    parent_arguments(sync)

    sync_file = commands.add_parser("sync-file")
    parent_arguments(sync_file)
    sync_file.add_argument("--name", required=True)
    sync_file.add_argument("--expected-id", required=True)

    inspect = commands.add_parser("inspect")
    parent_arguments(inspect)
    inspect.add_argument("--name", required=True)

    list_prefix = commands.add_parser("list-prefix")
    parent_arguments(list_prefix)
    list_prefix.add_argument("--prefix", required=True)

    locate = commands.add_parser("locate")
    parent_arguments(locate)
    locate.add_argument("--expected-id", required=True)
    kind_argument(locate)

    write_file = commands.add_parser("write-file")
    parent_arguments(write_file)
    write_file.add_argument("--name", required=True)
    write_file.add_argument("--expected-id", required=True)
    write_file.add_argument("--mode", type=_parse_mode, required=True)

    compare = commands.add_parser("compare")
    compare.add_argument("--left", type=Path, required=True)
    compare.add_argument("--left-id", required=True)
    compare.add_argument("--right", type=Path, required=True)
    compare.add_argument("--right-id", required=True)
    return result


def main(argv: Sequence[str] | None = None) -> int:
    args = parser().parse_args(argv)
    try:
        with _installed_signal_protocol():
            _validate_test_signal_configuration()
            if args.command == "materialize":
                _materialize(args)
            elif args.command == "probe":
                _probe(args)
            elif args.command == "exchange":
                _exchange(args)
            elif args.command == "publish":
                _publish(args)
            elif args.command == "state":
                _state(args)
            elif args.command == "remove":
                _remove(args)
            elif args.command == "sync":
                _sync(args)
            elif args.command == "sync-file":
                _sync_file(args)
            elif args.command == "inspect":
                _inspect(args)
            elif args.command == "list-prefix":
                _list_prefix(args)
            elif args.command == "locate":
                _locate(args)
            elif args.command == "write-file":
                _write_file(args)
            elif args.command == "compare":
                _compare(args)
    except OperationInterrupted as error:
        print(
            f"atomic tree operation interrupted by {signal.Signals(error.signum).name}",
            file=sys.stderr,
        )
        return 128 + error.signum
    except UnsupportedPublish as error:
        print(str(error), file=sys.stderr)
        return 75
    except RetainedEntry as error:
        print(
            f"atomic tree operation retained an exact recovery entry: {error}",
            file=sys.stderr,
        )
        return 5
    except (OSError, PublishError) as error:
        print(f"atomic tree operation failed: {error}", file=sys.stderr)
        return 1
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
