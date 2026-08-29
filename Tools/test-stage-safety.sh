#!/usr/bin/env bash
# Regression fixtures for stage.sh's deploy target boundary.  Apply uses only an owned temp tree.

set -euo pipefail

REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
FIXTURE="$(mktemp -d /tmp/taf-stage-safety.XXXXXX)"
SECOND_FS_FIXTURE=""
cleanup() {
	status=$?
	trap - EXIT
	case "$FIXTURE" in
		/tmp/taf-stage-safety.*) find -P "$FIXTURE" -depth -delete ;;
		*) echo "refusing unexpected stage fixture cleanup path: $FIXTURE" >&2; status=1 ;;
	esac
	if [ -n "$SECOND_FS_FIXTURE" ]; then
		case "$SECOND_FS_FIXTURE" in
			/dev/shm/taf-stage-safety.*) find -P "$SECOND_FS_FIXTURE" -depth -delete ;;
			*) echo "refusing unexpected second-filesystem fixture path: $SECOND_FS_FIXTURE" >&2
				status=1 ;;
		esac
	fi
	exit "$status"
}
trap cleanup EXIT

expect_refusal() {
	local live="$1" expected="$2" output status
	set +e
	output="$(TAF_LIVE_MOD="$live" "$REPO/Tools/stage.sh" deploy 2>&1)"
	status=$?
	set -e
	[ "$status" -ne 0 ] || {
		echo "unsafe stage fixture was accepted: $live" >&2; return 1; }
	case "$output" in
		*"$expected"*) ;;
		*) echo "wrong refusal for $live:" >&2; printf '%s\n' "$output" >&2; return 1 ;;
	esac
}

expect_apply_refusal() {
	local live="$1" backup_root="$2" expected="$3" output status
	set +e
	output="$(TAF_LIVE_MOD="$live" TAF_DEPLOY_BACKUP_ROOT="$backup_root" \
		"$REPO/Tools/stage.sh" deploy --apply 2>&1)"
	status=$?
	set -e
	[ "$status" -ne 0 ] || {
		echo "unsafe apply fixture was accepted: $live" >&2; return 1; }
	case "$output" in
		*"$expected"*) ;;
		*) echo "wrong apply refusal for $live:" >&2; printf '%s\n' "$output" >&2; return 1 ;;
	esac
}

make_stage_repo() {
	local name="$1" root
	root="$FIXTURE/$name"
	mkdir -p "$root/Tools"
	cp "$REPO/Tools/stage.sh" "$root/Tools/stage.sh"
	cp "$REPO/Tools/atomic_tree_publish.py" "$root/Tools/atomic_tree_publish.py"
	cp "$REPO/manifest.json" "$root/manifest.json"
	printf '%s\n' "$root"
}

install_atomic_fault_wrapper() {
	local root="$1"
	mv -- "$root/Tools/atomic_tree_publish.py" \
		"$root/Tools/atomic_tree_publish.real.py"
	printf '%s\n' \
		'#!/usr/bin/env python3' \
		'import errno' \
		'import os' \
		'from pathlib import Path' \
		'import runpy' \
		'import signal' \
		'import sys' \
		'' \
		'mode = os.environ.get("TAF_ATOMIC_FAULT", "")' \
		'marker_value = os.environ.get("TAF_ATOMIC_FAULT_MARKER", "")' \
		'marker = Path(marker_value) if marker_value else None' \
		'fault_parent = os.environ.get("TAF_ATOMIC_FAULT_PARENT", "")' \
		'fault_external = os.environ.get("TAF_ATOMIC_FAULT_EXTERNAL", "")' \
		'command = sys.argv[1] if len(sys.argv) > 1 else ""' \
		'def option_value(option):' \
		'    try:' \
		'        return sys.argv[sys.argv.index(option) + 1]' \
		'    except (ValueError, IndexError):' \
		'        return ""' \
		'parent = option_value("--parent")' \
		'name = option_value("--name")' \
		'if mode in ("receipt-temp-symlink", "receipt-temp-hardlink") and command == "write-file":' \
		'    entry = Path(parent) / name' \
		'    if mode == "receipt-temp-symlink":' \
		'        entry.symlink_to(fault_external)' \
		'    else:' \
		'        os.link(fault_external, entry)' \
		'    if marker is not None:' \
		'        marker.touch()' \
		'if mode == "mutate-repo-on-receipt" and command == "write-file" and marker is not None and not marker.exists():' \
		'    Path(fault_external).write_text("repo changed after candidate verification\n", encoding="utf-8")' \
		'    marker.touch()' \
		'if mode == "state-ambiguous" and command == "state":' \
		'    if marker is not None:' \
		'        marker.touch()' \
		'    print("injected ambiguous publication state", file=sys.stderr)' \
		'    raise SystemExit(1)' \
		'inject = False' \
		'if (mode.endswith("probe-unsupported") and command == "probe"' \
		'        and marker is not None and not marker.exists() and fault_parent' \
		'        and os.path.realpath(parent) == os.path.realpath(fault_parent)):' \
		'    marker.touch()' \
		'    inject = True' \
		'elif marker is not None and not marker.exists():' \
		'    is_file_publication = command == "publish" and "file" in sys.argv' \
		'    if ((mode in ("deploy-exchange-term", "deploy-exchange-eio", "deploy-exchange-kill")' \
		'            and command == "exchange" and not is_file_publication)' \
		'            or (mode in ("copy-publish-term", "copy-publish-eio")' \
		'                and command == "publish"' \
		'                and not is_file_publication)' \
		'            or (mode in ("receipt-publish-term", "receipt-publish-kill")' \
		'                and is_file_publication)):' \
		'        marker.touch()' \
		'        inject = True' \
		'' \
		'real_fsync = os.fsync' \
		'if inject:' \
		'    def injected_fsync(descriptor):' \
		'        if mode.endswith("-term"):' \
		'            os.kill(os.getpid(), signal.SIGTERM)' \
		'            return real_fsync(descriptor)' \
		'        if mode.endswith("-kill"):' \
		'            if mode == "receipt-publish-kill":' \
		'                real_fsync(descriptor)' \
		'            os.killpg(os.getpgrp(), signal.SIGKILL)' \
		'        if mode.endswith("probe-unsupported"):' \
		'            raise OSError(errno.EINVAL, os.strerror(errno.EINVAL))' \
		'        raise OSError(errno.EIO, os.strerror(errno.EIO))' \
		'    os.fsync = injected_fsync' \
		'' \
		'if mode == "cleanup-refuse" and command == "materialize":' \
		'    def injected_unlink(_path, *args, **kwargs):' \
		'        raise OSError(errno.EBUSY, os.strerror(errno.EBUSY))' \
		'    os.unlink = injected_unlink' \
		'' \
		'if mode == "cleanup-replace-hardlink" and command == "remove":' \
		'    real_open = os.open' \
		'    replaced = False' \
		'    def injected_open(path, flags, *args, **kwargs):' \
		'        global replaced' \
		'        if not replaced and path == "victim" and kwargs.get("dir_fd") is not None:' \
		'            replaced = True' \
		'            os.unlink(path, dir_fd=kwargs["dir_fd"])' \
		'            os.link(fault_external, path, dst_dir_fd=kwargs["dir_fd"])' \
		'        return real_open(path, flags, *args, **kwargs)' \
		'    os.open = injected_open' \
		'' \
		'runpy.run_path(str(Path(__file__).with_name("atomic_tree_publish.real.py")), run_name="__main__")' \
		> "$root/Tools/atomic_tree_publish.py"
	chmod +x "$root/Tools/atomic_tree_publish.py"
}

expect_list_refusal() {
	local root="$1" expected="$2" output status
	set +e
	output="$("$root/Tools/stage.sh" list 2>&1)"
	status=$?
	set -e
	[ "$status" -ne 0 ] || {
		echo "unsafe stage inventory was accepted: $root" >&2; return 1; }
	case "$output" in
		*"$expected"*) ;;
		*) echo "wrong inventory refusal for $root:" >&2; printf '%s\n' "$output" >&2; return 1 ;;
	esac
}

# The checkout itself is mod-shaped and was the most dangerous formerly accepted target.
expect_refusal "$REPO" "inside repository"

# Qud's root mod configuration is runtime metadata, while Windows-ambiguous names must never
# reach a package that will be copied between Linux, WSL, Steam, and NTFS.
MODCONFIG_REPO="$(make_stage_repo modconfig-repo)"
printf '%s\n' '{}' > "$MODCONFIG_REPO/modconfig.json"
modconfig_list="$("$MODCONFIG_REPO/Tools/stage.sh" list)"
case $'\n'"$modconfig_list"$'\n' in
	*$'\nmodconfig.json\n'*) ;;
	*) echo "modconfig.json was omitted from runtime staging" >&2; exit 1 ;;
esac
git -C "$MODCONFIG_REPO" init -q
git -C "$MODCONFIG_REPO" config user.name "TAF stage harness"
git -C "$MODCONFIG_REPO" config user.email "fixture@example.invalid"
git -C "$MODCONFIG_REPO" add --all
git -C "$MODCONFIG_REPO" commit -q -m "modconfig fixture"
modconfig_head_list="$("$MODCONFIG_REPO/Tools/stage.sh" list-head HEAD)"
case $'\n'"$modconfig_head_list"$'\n' in
	*$'\nmodconfig.json\n'*) ;;
	*) echo "modconfig.json was omitted from committed runtime staging" >&2; exit 1 ;;
esac

CASE_REPO="$(make_stage_repo casefold-repo)"
printf '%s\n' '// first spelling' > "$CASE_REPO/Case.cs"
printf '%s\n' '// second spelling' > "$CASE_REPO/case.cs"
expect_list_refusal "$CASE_REPO" "Windows case-fold collision"

RESERVED_REPO="$(make_stage_repo reserved-repo)"
printf '%s\n' '// reserved device name' > "$RESERVED_REPO/CON.cs"
expect_list_refusal "$RESERVED_REPO" "Windows-reserved path"

INVALID_REPO="$(make_stage_repo invalid-windows-repo)"
printf '%s\n' '// alternate data stream spelling' > "$INVALID_REPO/bad:name.cs"
expect_list_refusal "$INVALID_REPO" "Windows-incompatible path"

TRAILING_REPO="$(make_stage_repo trailing-windows-repo)"
mkdir "$TRAILING_REPO/trailing."
printf '%s\n' '// trailing-dot directory' > "$TRAILING_REPO/trailing./Test.cs"
expect_list_refusal "$TRAILING_REPO" "Windows-incompatible path"

# Discovery stays NUL-delimited until every relative path is proven canonical. A newline followed
# by two dots must never split into a fake ../ record that escapes a copy/delete target.
MALICIOUS_REPO="$FIXTURE/malicious-repo"
mkdir -p "$MALICIOUS_REPO/Tools" "$MALICIOUS_REPO/x"$'\n'".."
cp "$REPO/Tools/stage.sh" "$MALICIOUS_REPO/Tools/stage.sh"
cp "$REPO/manifest.json" "$MALICIOUS_REPO/manifest.json"
printf '// hostile path fixture\n' > "$MALICIOUS_REPO/x"$'\n'"../victim.cs"
set +e
path_output="$("$MALICIOUS_REPO/Tools/stage.sh" list 2>&1)"
path_status=$?
set -e
[ "$path_status" -ne 0 ]
case "$path_output" in *"unsafe relative path"*) ;; *) printf '%s\n' "$path_output" >&2; exit 1 ;; esac
printf 'outside source\n' > "$FIXTURE/link-source"
ln -s "$FIXTURE/link-source" "$MALICIOUS_REPO/linked.cs"
set +e
link_output="$("$MALICIOUS_REPO/Tools/stage.sh" list 2>&1)"
link_status=$?
set -e
[ "$link_status" -ne 0 ]
case "$link_output" in *"runtime discovery contains a link"*) ;; *) printf '%s\n' "$link_output" >&2; exit 1 ;; esac

# The public copy command gets the same boundary: no checkout, links, or populated trees.
set +e
copy_output="$("$REPO/Tools/stage.sh" copy "$REPO" 2>&1)"
copy_status=$?
set -e
[ "$copy_status" -ne 0 ]
case "$copy_output" in *"inside repository"*) ;; *) printf '%s\n' "$copy_output" >&2; exit 1 ;; esac
mkdir "$FIXTURE/copy-real"
ln -s "$FIXTURE/copy-real" "$FIXTURE/copy-link"
set +e
copy_output="$("$REPO/Tools/stage.sh" copy "$FIXTURE/copy-link" 2>&1)"
copy_status=$?
set -e
[ "$copy_status" -ne 0 ]
case "$copy_output" in *"linked stage copy"*) ;; *) printf '%s\n' "$copy_output" >&2; exit 1 ;; esac
mkdir "$FIXTURE/copy-populated"
printf 'kept\n' > "$FIXTURE/copy-populated/sentinel"
set +e
copy_output="$("$REPO/Tools/stage.sh" copy "$FIXTURE/copy-populated" 2>&1)"
copy_status=$?
set -e
[ "$copy_status" -ne 0 ]
case "$copy_output" in *"not empty"*) ;; *) printf '%s\n' "$copy_output" >&2; exit 1 ;; esac
[ "$(<"$FIXTURE/copy-populated/sentinel")" = kept ]

# Materialisation uses directory descriptors inside one private sibling. A shell mkdir wrapper
# cannot redirect a nested staged path into a different tree.
COPY_RACE_REPO="$(make_stage_repo copy-path-replacement-repo)"
mkdir "$COPY_RACE_REPO/Core"
printf 'descriptor-bound bytes\n' > "$COPY_RACE_REPO/Core/Test.cs"
COPY_RACE_BIN="$FIXTURE/copy-race-bin"
mkdir "$COPY_RACE_BIN" "$FIXTURE/copy-race-parent" "$FIXTURE/copy-race-external"
printf 'external sentinel survives\n' > "$FIXTURE/copy-race-external/sentinel"
printf '%s\n' \
	'#!/usr/bin/env bash' \
	'set -euo pipefail' \
	'last="${!#}"' \
	'if [[ "${1:-}" == -p && "$last" == */Core ]]; then' \
	'  : > "$TAF_COPY_RACE_MARKER"' \
	'  destination="${last%/Core}"' \
	'  "$TAF_REAL_MV" -- "$destination" "$destination.displaced"' \
	'  ln -s -- "$TAF_COPY_RACE_EXTERNAL" "$destination"' \
	'fi' \
	'exec "$TAF_REAL_MKDIR" "$@"' > "$COPY_RACE_BIN/mkdir"
chmod +x "$COPY_RACE_BIN/mkdir"
PATH="$COPY_RACE_BIN:$PATH" TAF_REAL_MKDIR="$(command -v mkdir)" \
	TAF_REAL_MV="$(command -v mv)" TAF_COPY_RACE_MARKER="$FIXTURE/copy-race-marker" \
	TAF_COPY_RACE_EXTERNAL="$FIXTURE/copy-race-external" \
	"$COPY_RACE_REPO/Tools/stage.sh" copy "$FIXTURE/copy-race-parent/live" >/dev/null
[ ! -e "$FIXTURE/copy-race-marker" ] && [ ! -L "$FIXTURE/copy-race-marker" ]
[ "$(<"$FIXTURE/copy-race-external/sentinel")" = "external sentinel survives" ]
[ -z "$(find "$FIXTURE/copy-race-external" -mindepth 1 ! -name sentinel -print -quit)" ]
"$COPY_RACE_REPO/Tools/stage.sh" verify "$FIXTURE/copy-race-parent/live" >/dev/null

# An unsupported atomic primitive returns 75 before an absent copy destination is published.
UNSUPPORTED_REPO="$(make_stage_repo unsupported-atomic-repo)"
printf '%s\n' \
	'#!/usr/bin/env python3' \
	'import sys' \
	'print("atomic directory publication is unsupported by this filesystem", file=sys.stderr)' \
	'raise SystemExit(75)' > "$UNSUPPORTED_REPO/Tools/atomic_tree_publish.py"
printf 'adjacent sentinel survives\n' > "$FIXTURE/unsupported-sentinel"
set +e
unsupported_output="$("$UNSUPPORTED_REPO/Tools/stage.sh" copy \
	"$FIXTURE/unsupported-copy" 2>&1)"
unsupported_status=$?
set -e
[ "$unsupported_status" -eq 75 ]
case "$unsupported_output" in *"unsupported by this filesystem"*) ;;
	*) printf '%s\n' "$unsupported_output" >&2; exit 1 ;;
esac
[ ! -e "$FIXTURE/unsupported-copy" ] && [ ! -L "$FIXTURE/unsupported-copy" ]
[ "$(<"$FIXTURE/unsupported-sentinel")" = "adjacent sentinel survives" ]

# Materialisation chmods before file fsync, fsyncs every directory bottom-up, and finally fsyncs
# the parent that owns the private sibling name.
python3 - "$REPO/Tools/atomic_tree_publish.py" <<'PY'
import argparse
import contextlib
import fcntl
import importlib.util
import io
import os
from pathlib import Path
import stat
import tempfile
import sys

helper_path = Path(sys.argv[1])
spec = importlib.util.spec_from_file_location("taf_atomic_durability", helper_path)
module = importlib.util.module_from_spec(spec)
assert spec.loader is not None
spec.loader.exec_module(module)
with tempfile.TemporaryDirectory(prefix="taf-materialize-durability-") as raw:
    root = Path(raw)
    source = root / "source"
    parent = root / "parent"
    (source / "Core" / "Nested").mkdir(parents=True)
    parent.mkdir()
    staged = source / "Core" / "Nested" / "Mode.cs"
    staged.write_text("durable bytes\n", encoding="utf-8")
    staged.chmod(0o751)
    inventory = root / "inventory"
    inventory.write_text("Core/Nested/Mode.cs\n", encoding="utf-8")
    lock_fd = os.open(parent, os.O_RDONLY | os.O_DIRECTORY)
    fcntl.flock(lock_fd, fcntl.LOCK_EX)
    synced_directories = set()
    synced_file_modes = []
    real_fsync = module.os.fsync

    def recording_fsync(descriptor):
        nonlocal_file = os.fstat(descriptor)
        if stat.S_ISDIR(nonlocal_file.st_mode):
            synced_directories.add((nonlocal_file.st_dev, nonlocal_file.st_ino))
        elif stat.S_ISREG(nonlocal_file.st_mode):
            synced_file_modes.append(stat.S_IMODE(nonlocal_file.st_mode))
        return real_fsync(descriptor)

    module.os.fsync = recording_fsync
    output = io.StringIO()
    with contextlib.redirect_stdout(output):
        module._materialize(argparse.Namespace(
            source=source,
            source_id=module.identity(os.lstat(source)),
            parent=parent,
            parent_id=module.identity(os.lstat(parent)),
            inventory=inventory,
            inventory_fd=None,
            lock_fd=lock_fd,
            name=".durability-exact",
        ))
    sibling_name, _sibling_id = output.getvalue().strip().split("\t")
    sibling = parent / sibling_name
    expected_directories = {
        (status.st_dev, status.st_ino)
        for status in (
            os.lstat(parent),
            os.lstat(sibling),
            os.lstat(sibling / "Core"),
            os.lstat(sibling / "Core" / "Nested"),
        )
    }
    assert synced_file_modes == [0o751]
    assert expected_directories <= synced_directories
    os.close(lock_fd)
PY

# Shell and helper reject Unicode format controls identically.
FORMAT_CONTROL_REPO="$(make_stage_repo format-control-repo)"
printf 'bidi path\n' > "$FORMAT_CONTROL_REPO/unsafe"$'\u202e'".cs"
expect_list_refusal "$FORMAT_CONTROL_REPO" "unsafe relative path"

# Direct helper fixtures hold exact inherited parent flock and exercise every materialization and
# probe signal cut. Helper receives TERM itself, cleans owned identity, then returns 143.
HELPER_SOURCE="$FIXTURE/helper-source"
HELPER_PARENT="$FIXTURE/helper-parent"
mkdir -p "$HELPER_SOURCE/Core/Nested" "$HELPER_PARENT"
printf 'direct helper bytes\n' > "$HELPER_SOURCE/Core/Nested/Test.cs"
printf 'Core/Nested/Test.cs\n' > "$FIXTURE/helper-inventory"
chmod 2751 "$HELPER_SOURCE"
chmod 3711 "$HELPER_SOURCE/Core" "$HELPER_SOURCE/Core/Nested"
chmod 6751 "$HELPER_SOURCE/Core/Nested/Test.cs"
HELPER_SOURCE_ID="$(stat -Lc '%d:%i' -- "$HELPER_SOURCE")"
HELPER_PARENT_ID="$(stat -Lc '%d:%i' -- "$HELPER_PARENT")"
exec {HELPER_LOCK_FD}<"$HELPER_PARENT"
flock -x "$HELPER_LOCK_FD"

set +e
helper_output="$(python3 "$REPO/Tools/atomic_tree_publish.py" inspect \
	--parent "$HELPER_PARENT" --parent-id "$HELPER_PARENT_ID" \
	--lock-fd "$HELPER_LOCK_FD" --name $'.unsafe\u202e' 2>&1)"
helper_status=$?
set -e
[ "$helper_status" -ne 0 ]
case "$helper_output" in *"unsafe sibling entry name"*) ;;
	*) printf '%s\n' "$helper_output" >&2; exit 1 ;;
esac

materialize_cuts=(
	materialize-before-mkdir
	materialize-after-mkdir
	materialize-after-directory-create
	materialize-after-directories
	materialize-file-before-create
	materialize-file-after-create
	materialize-file-after-copy
	materialize-file-after-chmod
	materialize-file-after-fsync
	materialize-after-file
	materialize-after-directory-mode
	materialize-after-tree-fsync
	materialize-before-parent-fsync
	materialize-after-parent-fsync
)
cut_index=0
for cut in "${materialize_cuts[@]}"; do
	cut_index=$((cut_index + 1))
	cut_name=".direct-materialize-$cut_index"
	set +e
	helper_output="$(TAF_ATOMIC_TEST_SIGNAL_AT="$cut" \
		python3 "$REPO/Tools/atomic_tree_publish.py" materialize \
		--source "$HELPER_SOURCE" --source-id "$HELPER_SOURCE_ID" \
		--parent "$HELPER_PARENT" --parent-id "$HELPER_PARENT_ID" \
		--lock-fd "$HELPER_LOCK_FD" --inventory "$FIXTURE/helper-inventory" \
		--name "$cut_name" 2>&1)"
	helper_status=$?
	set -e
	[ "$helper_status" -eq 143 ] || {
		echo "wrong direct materialize status at $cut: $helper_status" >&2
		printf '%s\n' "$helper_output" >&2
		exit 1
	}
	[ "$(python3 "$REPO/Tools/atomic_tree_publish.py" inspect \
		--parent "$HELPER_PARENT" --parent-id "$HELPER_PARENT_ID" \
		--lock-fd "$HELPER_LOCK_FD" --name "$cut_name")" = absent ]
done

probe_cuts=(
	probe-after-create
	probe-before-parent-fsync
	probe-after-parent-fsync
	probe-exchange-forward-after-rename
	probe-exchange-forward-after-fsync
	probe-exchange-reverse-after-rename
	probe-exchange-reverse-after-fsync
	probe-publish-after-rename
	probe-publish-after-fsync
)
for cut in "${probe_cuts[@]}"; do
	set +e
	helper_output="$(TAF_ATOMIC_TEST_SIGNAL_AT="$cut" \
		python3 "$REPO/Tools/atomic_tree_publish.py" probe \
		--parent "$HELPER_PARENT" --parent-id "$HELPER_PARENT_ID" \
		--lock-fd "$HELPER_LOCK_FD" 2>&1)"
	helper_status=$?
	set -e
	[ "$helper_status" -eq 143 ] || {
		echo "wrong direct probe status at $cut: $helper_status" >&2
		printf '%s\n' "$helper_output" >&2
		exit 1
	}
	[ -z "$(find -P "$HELPER_PARENT" -mindepth 1 -maxdepth 1 \
		-name '.taf-atomic-probe-*' -print -quit)" ]
done

# Full permission modes include root/subdirectories and file special bits. Compare fails closed.
helper_result="$(python3 "$REPO/Tools/atomic_tree_publish.py" materialize \
	--source "$HELPER_SOURCE" --source-id "$HELPER_SOURCE_ID" \
	--parent "$HELPER_PARENT" --parent-id "$HELPER_PARENT_ID" \
	--lock-fd "$HELPER_LOCK_FD" --inventory "$FIXTURE/helper-inventory" \
	--name .mode-tree)"
IFS=$'\t' read -r helper_mode_name helper_mode_id <<< "$helper_result"
[ "$helper_mode_name" = .mode-tree ]
[ "$(stat -c %a -- "$HELPER_PARENT/.mode-tree")" = 2751 ]
[ "$(stat -c %a -- "$HELPER_PARENT/.mode-tree/Core")" = 3711 ]
[ "$(stat -c %a -- "$HELPER_PARENT/.mode-tree/Core/Nested")" = 3711 ]
[ "$(stat -c %a -- "$HELPER_PARENT/.mode-tree/Core/Nested/Test.cs")" = 6751 ]
python3 "$REPO/Tools/atomic_tree_publish.py" compare \
	--left "$HELPER_SOURCE" --left-id "$HELPER_SOURCE_ID" \
	--right "$HELPER_PARENT/.mode-tree" --right-id "$helper_mode_id" >/dev/null
chmod 0711 "$HELPER_PARENT/.mode-tree/Core/Nested"
set +e
python3 "$REPO/Tools/atomic_tree_publish.py" compare \
	--left "$HELPER_SOURCE" --left-id "$HELPER_SOURCE_ID" \
	--right "$HELPER_PARENT/.mode-tree" --right-id "$helper_mode_id" >/dev/null 2>&1
helper_status=$?
set -e
[ "$helper_status" -ne 0 ]
python3 "$REPO/Tools/atomic_tree_publish.py" remove \
	--parent "$HELPER_PARENT" --parent-id "$HELPER_PARENT_ID" \
	--lock-fd "$HELPER_LOCK_FD" --name .mode-tree --expected-id "$helper_mode_id"

# Receipt writer never follows or truncates replacement links, even with captured hardlink identity.
printf 'unrelated receipt target\n' > "$FIXTURE/receipt-unrelated"
ln -s "$FIXTURE/receipt-unrelated" "$HELPER_PARENT/.receipt-symlink"
set +e
printf 'new receipt\n' | python3 "$REPO/Tools/atomic_tree_publish.py" write-file \
	--parent "$HELPER_PARENT" --parent-id "$HELPER_PARENT_ID" \
	--lock-fd "$HELPER_LOCK_FD" --name .receipt-symlink \
	--expected-id absent --mode 0600 >/dev/null 2>&1
helper_status=$?
set -e
[ "$helper_status" -ne 0 ]
[ "$(<"$FIXTURE/receipt-unrelated")" = "unrelated receipt target" ]
ln "$FIXTURE/receipt-unrelated" "$HELPER_PARENT/.receipt-hardlink"
receipt_hardlink_id="$(stat -Lc '%d:%i' -- "$HELPER_PARENT/.receipt-hardlink")"
for expected_receipt_id in absent "$receipt_hardlink_id"; do
	set +e
	printf 'new receipt\n' | python3 "$REPO/Tools/atomic_tree_publish.py" write-file \
		--parent "$HELPER_PARENT" --parent-id "$HELPER_PARENT_ID" \
		--lock-fd "$HELPER_LOCK_FD" --name .receipt-hardlink \
		--expected-id "$expected_receipt_id" --mode 0600 >/dev/null 2>&1
	helper_status=$?
	set -e
	[ "$helper_status" -ne 0 ]
	[ "$(<"$FIXTURE/receipt-unrelated")" = "unrelated receipt target" ]
done
for receipt_write_cut in write-file-after-create write-file-after-write write-file-after-fsync; do
	receipt_cut_name=".${receipt_write_cut}"
	set +e
	helper_output="$(printf 'signal receipt\n' | \
		TAF_ATOMIC_TEST_SIGNAL_AT="$receipt_write_cut" \
		python3 "$REPO/Tools/atomic_tree_publish.py" write-file \
		--parent "$HELPER_PARENT" --parent-id "$HELPER_PARENT_ID" \
		--lock-fd "$HELPER_LOCK_FD" --name "$receipt_cut_name" \
		--expected-id absent --mode 0600 2>&1)"
	helper_status=$?
	set -e
	[ "$helper_status" -eq 143 ] || {
		printf '%s\n' "$helper_output" >&2; exit 1; }
	[ ! -e "$HELPER_PARENT/$receipt_cut_name" ] \
		&& [ ! -L "$HELPER_PARENT/$receipt_cut_name" ]
done

# Recorded TERM cannot be trapped behind PEP475-restarted FIFO/stdin reads.
wait_for_marker() {
	local marker="$1" process="$2" attempt
	for attempt in $(seq 1 250); do
		[ ! -e "$marker" ] || return 0
		kill -0 "$process" 2>/dev/null || return 1
		sleep 0.02
	done
	return 1
}
mkfifo "$FIXTURE/hung-inventory"
exec {HUNG_INVENTORY_FD}<>"$FIXTURE/hung-inventory"
TAF_ATOMIC_TEST_INPUT_WAIT_MARKER="$FIXTURE/hung-inventory-ready" \
	python3 "$REPO/Tools/atomic_tree_publish.py" materialize \
	--source "$HELPER_SOURCE" --source-id "$HELPER_SOURCE_ID" \
	--parent "$HELPER_PARENT" --parent-id "$HELPER_PARENT_ID" \
	--lock-fd "$HELPER_LOCK_FD" --inventory-fd "$HUNG_INVENTORY_FD" \
	--name .hung-inventory >"$FIXTURE/hung-inventory.log" 2>&1 &
hung_pid=$!
wait_for_marker "$FIXTURE/hung-inventory-ready" "$hung_pid"
kill -TERM "$hung_pid"
set +e
wait "$hung_pid"
helper_status=$?
set -e
[ "$helper_status" -eq 143 ]
[ ! -e "$HELPER_PARENT/.hung-inventory" ] && [ ! -L "$HELPER_PARENT/.hung-inventory" ]
exec {HUNG_INVENTORY_FD}>&-

mkfifo "$FIXTURE/hung-receipt"
exec {HUNG_RECEIPT_FD}<>"$FIXTURE/hung-receipt"
TAF_ATOMIC_TEST_INPUT_WAIT_MARKER="$FIXTURE/hung-receipt-ready" \
	python3 "$REPO/Tools/atomic_tree_publish.py" write-file \
	--parent "$HELPER_PARENT" --parent-id "$HELPER_PARENT_ID" \
	--lock-fd "$HELPER_LOCK_FD" --name .hung-receipt \
	--expected-id absent --mode 0600 <"$FIXTURE/hung-receipt" \
	>"$FIXTURE/hung-receipt.log" 2>&1 &
hung_pid=$!
wait_for_marker "$FIXTURE/hung-receipt-ready" "$hung_pid"
kill -TERM "$hung_pid"
set +e
wait "$hung_pid"
helper_status=$?
set -e
[ "$helper_status" -eq 143 ]
[ ! -e "$HELPER_PARENT/.hung-receipt" ] && [ ! -L "$HELPER_PARENT/.hung-receipt" ]
exec {HUNG_RECEIPT_FD}>&-

# Post-sequester child replacement never deletes through stale stat data. Failure returns 5,
# prints and retains exact 0700 quarantine, then clean re-entry removes only quarantined identity.
CLEANUP_REPO="$(make_stage_repo cleanup-race-repo)"
install_atomic_fault_wrapper "$CLEANUP_REPO"
CLEANUP_PARENT="$FIXTURE/cleanup-parent"
mkdir -p "$CLEANUP_PARENT/race-tree"
printf 'original child\n' > "$CLEANUP_PARENT/race-tree/victim"
printf 'external child survives\n' > "$FIXTURE/cleanup-external"
chmod 0777 "$CLEANUP_PARENT/race-tree"
CLEANUP_PARENT_ID="$(stat -Lc '%d:%i' -- "$CLEANUP_PARENT")"
CLEANUP_TREE_ID="$(stat -Lc '%d:%i' -- "$CLEANUP_PARENT/race-tree")"
exec {CLEANUP_LOCK_FD}<"$CLEANUP_PARENT"
flock -x "$CLEANUP_LOCK_FD"
set +e
cleanup_output="$(TAF_ATOMIC_FAULT=cleanup-replace-hardlink \
	TAF_ATOMIC_FAULT_EXTERNAL="$FIXTURE/cleanup-external" \
	python3 "$CLEANUP_REPO/Tools/atomic_tree_publish.py" remove \
	--parent "$CLEANUP_PARENT" --parent-id "$CLEANUP_PARENT_ID" \
	--lock-fd "$CLEANUP_LOCK_FD" --name race-tree \
	--expected-id "$CLEANUP_TREE_ID" 2>&1)"
cleanup_status=$?
set -e
[ "$cleanup_status" -eq 5 ]
case "$cleanup_output" in *"partial cleanup retained directory .taf-remove-"*"identity $CLEANUP_TREE_ID"*) ;;
	*) printf '%s\n' "$cleanup_output" >&2; exit 1 ;;
esac
[ "$(<"$FIXTURE/cleanup-external")" = "external child survives" ]
CLEANUP_QUARANTINE="$(find -P "$CLEANUP_PARENT" -mindepth 1 -maxdepth 1 \
	-name '.taf-remove-*' -type d -print -quit)"
[ -n "$CLEANUP_QUARANTINE" ]
[ "$(stat -Lc '%d:%i' -- "$CLEANUP_QUARANTINE")" = "$CLEANUP_TREE_ID" ]
[ "$(stat -c %a -- "$CLEANUP_QUARANTINE")" = 700 ]
python3 "$CLEANUP_REPO/Tools/atomic_tree_publish.py" remove \
	--parent "$CLEANUP_PARENT" --parent-id "$CLEANUP_PARENT_ID" \
	--lock-fd "$CLEANUP_LOCK_FD" --name "$(basename -- "$CLEANUP_QUARANTINE")" \
	--expected-id "$CLEANUP_TREE_ID"
[ "$(<"$FIXTURE/cleanup-external")" = "external child survives" ]

set +e
cleanup_output="$(TAF_ATOMIC_FAULT=cleanup-refuse \
	TAF_ATOMIC_TEST_SIGNAL_AT=materialize-file-after-create \
	python3 "$CLEANUP_REPO/Tools/atomic_tree_publish.py" materialize \
	--source "$HELPER_SOURCE" --source-id "$HELPER_SOURCE_ID" \
	--parent "$CLEANUP_PARENT" --parent-id "$CLEANUP_PARENT_ID" \
	--lock-fd "$CLEANUP_LOCK_FD" --inventory "$FIXTURE/helper-inventory" \
	--name .signal-retained 2>&1)"
cleanup_status=$?
set -e
[ "$cleanup_status" -eq 5 ]
case "$cleanup_output" in *"exact recovery entry"*".taf-remove-"*"identity"*) ;;
	*) printf '%s\n' "$cleanup_output" >&2; exit 1 ;;
esac
[ ! -e "$CLEANUP_PARENT/.signal-retained" ]
CLEANUP_QUARANTINE="$(find -P "$CLEANUP_PARENT" -mindepth 1 -maxdepth 1 \
	-name '.taf-remove-*' -type d -print -quit)"
[ -n "$CLEANUP_QUARANTINE" ]
cleanup_quarantine_id="$(stat -Lc '%d:%i' -- "$CLEANUP_QUARANTINE")"
python3 "$CLEANUP_REPO/Tools/atomic_tree_publish.py" remove \
	--parent "$CLEANUP_PARENT" --parent-id "$CLEANUP_PARENT_ID" \
	--lock-fd "$CLEANUP_LOCK_FD" --name "$(basename -- "$CLEANUP_QUARANTINE")" \
	--expected-id "$cleanup_quarantine_id"

# A validation failure after public rename still belongs to this invocation. Absent destinations
# are removed; existing empty destinations are exchanged back with their exact original identity.
COPY_VALIDATION_REPO="$(make_stage_repo copy-public-validation-repo)"
mkdir "$COPY_VALIDATION_REPO/Core"
printf 'validated candidate\n' > "$COPY_VALIDATION_REPO/Core/Test.cs"
COPY_VALIDATION_BIN="$FIXTURE/copy-validation-bin"
mkdir "$COPY_VALIDATION_BIN" "$FIXTURE/copy-validation-parent"
printf '%s\n' \
	'#!/usr/bin/env bash' \
	'set -euo pipefail' \
	'if [[ "$1" == */atomic_tree_publish.py && "${2:-}" == publish && "$*" != *"--kind file"* ]]; then' \
	'  "$TAF_REAL_PYTHON" "$@"' \
	'  status=$?' \
	'  parent=""; destination=""; previous=""' \
	'  for argument in "$@"; do' \
	'    if [ "$previous" = --parent ]; then parent="$argument"; fi' \
	'    if [ "$previous" = --destination ]; then destination="$argument"; fi' \
	'    previous="$argument"' \
	'  done' \
	'  cp -- "$TAF_COPY_MUTATION_SOURCE" "$parent/$destination/unexpected.txt"' \
	'  exit "$status"' \
	'fi' \
	'exec "$TAF_REAL_PYTHON" "$@"' > "$COPY_VALIDATION_BIN/python3"
chmod +x "$COPY_VALIDATION_BIN/python3"
for destination_state in absent existing; do
	copy_target="$FIXTURE/copy-validation-parent/$destination_state"
	copy_original_id="absent"
	if [ "$destination_state" = existing ]; then
		mkdir "$copy_target"
		copy_original_id="$(stat -Lc '%d:%i' -- "$copy_target")"
	fi
	set +e
	copy_output="$(PATH="$COPY_VALIDATION_BIN:$PATH" \
		TAF_REAL_PYTHON="$(command -v python3)" \
		TAF_COPY_MUTATION_SOURCE="$COPY_VALIDATION_REPO/manifest.json" \
		"$COPY_VALIDATION_REPO/Tools/stage.sh" copy "$copy_target" 2>&1)"
	copy_status=$?
	set -e
	[ "$copy_status" -ne 0 ]
	case "$copy_output" in *"atomic publication rolled back"*) ;;
		*) printf '%s\n' "$copy_output" >&2; exit 1 ;;
	 esac
	if [ "$destination_state" = absent ]; then
		[ ! -e "$copy_target" ] && [ ! -L "$copy_target" ]
	else
		[ -d "$copy_target" ] && [ ! -L "$copy_target" ]
		[ "$(stat -Lc '%d:%i' -- "$copy_target")" = "$copy_original_id" ]
		[ -z "$(find -P "$copy_target" -mindepth 1 -print -quit)" ]
	fi
	[ -z "$(find -P "$FIXTURE/copy-validation-parent" -mindepth 1 -maxdepth 1 \
		-name ".$destination_state.taf-copy-*" -print -quit)" ]
done

# Direct-child TERM immediately after rename lands before stage.sh sees helper return. Both
# no-replace and exchange copy publications still roll back to their exact pre-publication states.
COPY_SIGNAL_REPO="$(make_stage_repo copy-publication-signal-repo)"
mkdir "$COPY_SIGNAL_REPO/Core"
printf 'signal candidate\n' > "$COPY_SIGNAL_REPO/Core/Test.cs"
mkdir "$FIXTURE/copy-signal-parent"
for destination_state in absent existing; do
	copy_target="$FIXTURE/copy-signal-parent/$destination_state"
	copy_original_id="absent"
	if [ "$destination_state" = existing ]; then
		mkdir "$copy_target"
		copy_original_id="$(stat -Lc '%d:%i' -- "$copy_target")"
	fi
	set +e
	copy_output="$(TAF_ATOMIC_TEST_SIGNAL_AT=publish-after-rename \
		TAF_ATOMIC_TEST_SIGNAL_MARKER="$FIXTURE/copy-signal-$destination_state" \
		"$COPY_SIGNAL_REPO/Tools/stage.sh" copy "$copy_target" 2>&1)"
	copy_status=$?
	set -e
	[ "$copy_status" -eq 143 ]
	case "$copy_output" in *"atomic publication rolled back"*) ;;
		*) printf '%s\n' "$copy_output" >&2; exit 1 ;;
	esac
	if [ "$destination_state" = absent ]; then
		[ ! -e "$copy_target" ] && [ ! -L "$copy_target" ]
	else
		[ "$(stat -Lc '%d:%i' -- "$copy_target")" = "$copy_original_id" ]
		[ -z "$(find -P "$copy_target" -mindepth 1 -print -quit)" ]
	fi
	[ -z "$(find -P "$FIXTURE/copy-signal-parent" -mindepth 1 -maxdepth 1 \
		-name ".$destination_state.taf-copy-*" -print -quit)" ]
done

# Parent fsync EIO after copy rename is classified by exact identities. Both destination forms roll
# back; failed helper status is never used to guess rename outcome.
COPY_EIO_REPO="$(make_stage_repo copy-publication-eio-repo)"
mkdir "$COPY_EIO_REPO/Core" "$FIXTURE/copy-eio-parent"
printf 'EIO candidate\n' > "$COPY_EIO_REPO/Core/Test.cs"
install_atomic_fault_wrapper "$COPY_EIO_REPO"
for destination_state in absent existing; do
	copy_target="$FIXTURE/copy-eio-parent/$destination_state"
	copy_original_id=absent
	if [ "$destination_state" = existing ]; then
		mkdir "$copy_target"
		copy_original_id="$(stat -Lc '%d:%i' -- "$copy_target")"
	fi
	set +e
	copy_output="$(TAF_ATOMIC_FAULT=copy-publish-eio \
		TAF_ATOMIC_FAULT_MARKER="$FIXTURE/copy-eio-$destination_state.marker" \
		"$COPY_EIO_REPO/Tools/stage.sh" copy "$copy_target" 2>&1)"
	copy_status=$?
	set -e
	[ "$copy_status" -ne 0 ]
	case "$copy_output" in *"atomic publication rolled back"*) ;;
		*) printf '%s\n' "$copy_output" >&2; exit 1 ;;
	esac
	if [ "$destination_state" = absent ]; then
		[ ! -e "$copy_target" ] && [ ! -L "$copy_target" ]
	else
		[ "$(stat -Lc '%d:%i' -- "$copy_target")" = "$copy_original_id" ]
		[ -z "$(find -P "$copy_target" -mindepth 1 -print -quit)" ]
	fi
	[ -z "$(find -P "$FIXTURE/copy-eio-parent" -mindepth 1 -maxdepth 1 \
		-name ".$destination_state.taf-copy-*" -print -quit)" ]
done

# Process-group TERM reaches stage shell, helper, and inventory producer together. One probe cut and
# one mid-materialization cut still leave absent destination and no unknown private sibling.
if command -v setsid >/dev/null 2>&1; then
	PROCESS_SIGNAL_REPO="$(make_stage_repo process-group-signal-repo)"
	mkdir "$PROCESS_SIGNAL_REPO/Core" "$FIXTURE/process-signal-parent"
	printf 'process group candidate\n' > "$PROCESS_SIGNAL_REPO/Core/Test.cs"
	for process_cut in probe-after-create materialize-file-after-copy; do
		process_target="$FIXTURE/process-signal-parent/${process_cut//[^a-z]/-}"
		set +e
		process_output="$(setsid env TAF_ATOMIC_TEST_SIGNAL_AT="$process_cut" \
			TAF_ATOMIC_TEST_SIGNAL_SCOPE=process-group \
			TAF_ATOMIC_TEST_SIGNAL_MARKER="$FIXTURE/process-$process_cut.marker" \
			"$PROCESS_SIGNAL_REPO/Tools/stage.sh" copy "$process_target" 2>&1)"
		process_status=$?
		set -e
		[ "$process_status" -eq 143 ] || {
			echo "wrong process-group TERM status at $process_cut: $process_status" >&2
			printf '%s\n' "$process_output" >&2
			exit 1
		}
		[ ! -e "$process_target" ] && [ ! -L "$process_target" ]
		[ -z "$(find -P "$FIXTURE/process-signal-parent" -mindepth 1 -maxdepth 1 \
			-name ".$(basename -- "$process_target").taf-copy-*" -print -quit)" ]
	done
else
	echo "PROCESS-GROUP SIGNAL FIXTURES SKIPPED: setsid unavailable" >&2
fi

# If exact state inspection itself fails, cleanup must retain both exchanged identities and return
# recovery status 5 instead of guessing which one is safe to delete.
COPY_AMBIGUOUS_REPO="$(make_stage_repo copy-ambiguous-state-repo)"
mkdir "$COPY_AMBIGUOUS_REPO/Core"
printf 'ambiguous candidate\n' > "$COPY_AMBIGUOUS_REPO/Core/Test.cs"
install_atomic_fault_wrapper "$COPY_AMBIGUOUS_REPO"
mkdir -p "$FIXTURE/copy-ambiguous-parent/existing"
COPY_AMBIGUOUS_TARGET="$FIXTURE/copy-ambiguous-parent/existing"
COPY_AMBIGUOUS_OLD_ID="$(stat -Lc '%d:%i' -- "$COPY_AMBIGUOUS_TARGET")"
set +e
copy_output="$(TAF_ATOMIC_FAULT=state-ambiguous \
	TAF_ATOMIC_FAULT_MARKER="$FIXTURE/copy-ambiguous-marker" \
	"$COPY_AMBIGUOUS_REPO/Tools/stage.sh" copy "$COPY_AMBIGUOUS_TARGET" 2>&1)"
copy_status=$?
set -e
[ "$copy_status" -eq 5 ]
case "$copy_output" in *"no ambiguous identity was deleted"*) ;;
	*) printf '%s\n' "$copy_output" >&2; exit 1 ;;
esac
[ -f "$FIXTURE/copy-ambiguous-marker" ]
"$COPY_AMBIGUOUS_REPO/Tools/stage.sh" verify "$COPY_AMBIGUOUS_TARGET" >/dev/null
COPY_AMBIGUOUS_OLD="$(find -P "$FIXTURE/copy-ambiguous-parent" -mindepth 1 -maxdepth 1 \
	-name '.existing.taf-copy-*' -type d -print -quit)"
[ -n "$COPY_AMBIGUOUS_OLD" ]
[ "$(stat -Lc '%d:%i' -- "$COPY_AMBIGUOUS_OLD")" = "$COPY_AMBIGUOUS_OLD_ID" ]
[ -z "$(find -P "$COPY_AMBIGUOUS_OLD" -mindepth 1 -print -quit)" ]

# A bind mount can spell a directory inside the source without a lexical overlap. Where an
# unprivileged mount namespace is available, prove both creation paths reject the dev:inode alias
# and remove only the empty directory that they themselves created through it.
mkdir "$FIXTURE/mount-probe-source" "$FIXTURE/mount-probe-target"
if unshare -Urnm bash -c 'mount --bind "$1" "$2" && umount "$2"' \
		_ "$FIXTURE/mount-probe-source" "$FIXTURE/mount-probe-target" \
		>/dev/null 2>&1; then
	COPY_ALIAS_REPO="$(make_stage_repo copy-alias-repo)"
	mkdir "$FIXTURE/copy-alias-parent"
	set +e
	copy_output="$(unshare -Urnm bash -c '
		mount --bind "$1" "$2"
		"$1/Tools/stage.sh" copy "$2/created-copy"
	' _ "$COPY_ALIAS_REPO" "$FIXTURE/copy-alias-parent" 2>&1)"
	copy_status=$?
	set -e
	[ "$copy_status" -ne 0 ]
	case "$copy_output" in
		*"refusing stage copy alias into repository"*) ;;
		*"atomic stage copy is unsupported at this parent: /tmp has untrusted owner uid"*)
			echo "BIND-ALIAS COPY FIXTURE SKIPPED: /tmp is foreign-owned in user namespace" >&2 ;;
		*) printf '%s\n' "$copy_output" >&2; exit 1 ;;
	esac
	[ ! -e "$COPY_ALIAS_REPO/created-copy" ] && [ ! -L "$COPY_ALIAS_REPO/created-copy" ]

	BACKUP_ALIAS_REPO="$(make_stage_repo backup-alias-repo)"
	git -C "$BACKUP_ALIAS_REPO" init -q
	git -C "$BACKUP_ALIAS_REPO" config user.name "TAF stage harness"
	git -C "$BACKUP_ALIAS_REPO" config user.email "fixture@example.invalid"
	git -C "$BACKUP_ALIAS_REPO" add --all
	git -C "$BACKUP_ALIAS_REPO" commit -q -m "fixture source"
	mkdir "$FIXTURE/backup-alias-parent" "$FIXTURE/live-home" "$FIXTURE/live-home/Mods"
	mkdir "$FIXTURE/live-home/Mods/alias-live"
	cp "$BACKUP_ALIAS_REPO/manifest.json" "$FIXTURE/live-home/Mods/alias-live/manifest.json"
	set +e
	backup_output="$(unshare -Urnm bash -c '
		mount --bind "$1" "$2"
		TAF_LIVE_MOD="$3" TAF_DEPLOY_BACKUP_ROOT="$2/created-backups" \
			"$1/Tools/stage.sh" deploy --apply
	' _ "$BACKUP_ALIAS_REPO" "$FIXTURE/backup-alias-parent" \
		"$FIXTURE/live-home/Mods/alias-live" 2>&1)"
	backup_status=$?
	set -e
	[ "$backup_status" -ne 0 ]
	case "$backup_output" in
		*"refusing deployment backup alias into repository"*) ;;
		*"atomic deployment is unsupported at this live parent: /tmp has untrusted owner uid"*)
			echo "BIND-ALIAS BACKUP FIXTURE SKIPPED: /tmp is foreign-owned in user namespace" >&2 ;;
		*) printf '%s\n' "$backup_output" >&2; exit 1 ;;
	esac
	[ ! -e "$BACKUP_ALIAS_REPO/created-backups" ] \
		&& [ ! -L "$BACKUP_ALIAS_REPO/created-backups" ]
else
	echo "BIND-ALIAS STAGE FIXTURES SKIPPED: unprivileged mount namespace unavailable" >&2
fi

# Exact verification rejects non-file nodes even when the regular-file inventory still matches.
VERIFY_SPECIAL_REPO="$(make_stage_repo verify-special-repo)"
mkdir "$VERIFY_SPECIAL_REPO/Core"
printf 'verify special bytes\n' > "$VERIFY_SPECIAL_REPO/Core/Test.cs"
"$VERIFY_SPECIAL_REPO/Tools/stage.sh" copy "$FIXTURE/verify-special" >/dev/null
mkfifo "$FIXTURE/verify-special/unexpected.fifo"
set +e
verify_output="$("$VERIFY_SPECIAL_REPO/Tools/stage.sh" verify \
	"$FIXTURE/verify-special" 2>&1)"
verify_status=$?
set -e
[ "$verify_status" -ne 0 ]
case "$verify_output" in *"CONTAINS A SPECIAL FILE"*) ;; *) printf '%s\n' "$verify_output" >&2; exit 1 ;; esac

# A final-component link must be refused before inventory traversal.
ln -s -- "$REPO" "$FIXTURE/live-link"
expect_refusal "$FIXTURE/live-link" "linked live mod path"

# So must a normal live root containing a linked directory: a copy through it would escape.
mkdir "$FIXTURE/interior"
cp "$REPO/manifest.json" "$FIXTURE/interior/manifest.json"
ln -s -- "$REPO/Core" "$FIXTURE/interior/Core"
expect_refusal "$FIXTURE/interior" "linked path inside live mod folder"

# A misleading string inside the wrong JSON object is not target identity.
mkdir "$FIXTURE/wrong-id"
printf '%s\n' '{"id":"another_mod","note":"\"id\": \"r_ThousandAndFirst\""}' \
	> "$FIXTURE/wrong-id/manifest.json"
expect_refusal "$FIXTURE/wrong-id" "does not declare r_ThousandAndFirst"

mkdir "$FIXTURE/git-live"
cp "$REPO/manifest.json" "$FIXTURE/git-live/manifest.json"
printf 'gitdir: elsewhere\n' > "$FIXTURE/git-live/.git"
expect_refusal "$FIXTURE/git-live" "linked-worktree metadata"

mkdir "$FIXTURE/special-live"
cp "$REPO/manifest.json" "$FIXTURE/special-live/manifest.json"
mkfifo "$FIXTURE/special-live/untrusted.fifo"
expect_refusal "$FIXTURE/special-live" "special file inside live mod folder"

mkdir -p "$FIXTURE/hardlink-live/Core"
cp "$REPO/manifest.json" "$FIXTURE/hardlink-live/manifest.json"
printf 'external bytes survive\n' > "$FIXTURE/hardlink-target"
ln "$FIXTURE/hardlink-target" "$FIXTURE/hardlink-live/Core/KingdomRules.cs"
expect_refusal "$FIXTURE/hardlink-live" "hard-linked file inside live mod folder"
[ "$(<"$FIXTURE/hardlink-target")" = "external bytes survive" ]

mkdir -p "$FIXTURE/newline-live/x"$'\n'".."
cp "$REPO/manifest.json" "$FIXTURE/newline-live/manifest.json"
printf 'never touched\n' > "$FIXTURE/newline-live/x"$'\n'"../victim.txt"
expect_refusal "$FIXTURE/newline-live" "live mod inventory contains an unsafe relative path"
[ "$(<"$FIXTURE/newline-live/x"$'\n'"../victim.txt")" = "never touched" ]

# A separate, recognisable directory remains a valid dry-run target and is not mutated.
mkdir "$FIXTURE/ordinary"
cp "$REPO/manifest.json" "$FIXTURE/ordinary/manifest.json"
printf 'kept\n' > "$FIXTURE/ordinary/extra.txt"
TAF_LIVE_MOD="$FIXTURE/ordinary" "$REPO/Tools/stage.sh" deploy >/dev/null
[ "$(<"$FIXTURE/ordinary/extra.txt")" = "kept" ]
expect_apply_refusal "$FIXTURE/ordinary" "$FIXTURE/scanned-backups" \
	"scanned deployment backup root"
mkdir "$FIXTURE/backup-real"
ln -s "$FIXTURE/backup-real" "$FIXTURE/backup-link"
mkdir -p "$FIXTURE/custom-live-parent/ordinary"
cp "$REPO/manifest.json" "$FIXTURE/custom-live-parent/ordinary/manifest.json"
expect_apply_refusal "$FIXTURE/custom-live-parent/ordinary" "$FIXTURE/backup-link/nested" \
	"linked deployment backup parent"

snapshot() {
	local root="$1" output="$2"
	(
		cd "$root"
		find . -type f -print0 | LC_ALL=C sort -z | xargs -0 sha256sum
	) > "$output"
}

# Pre-armed reconciliation covers direct-child TERM and transient fsync EIO after live rename.
# Exact after-state is durably committed; informational receipt may remain old.
FAULT_REPO="$(make_stage_repo atomic-deployment-fault-repo)"
mkdir "$FAULT_REPO/Core"
printf 'new fault candidate\n' > "$FAULT_REPO/Core/Test.cs"
install_atomic_fault_wrapper "$FAULT_REPO"
git -C "$FAULT_REPO" init -q
git -C "$FAULT_REPO" config user.name "TAF stage harness"
git -C "$FAULT_REPO" config user.email "fixture@example.invalid"
git -C "$FAULT_REPO" add --all
git -C "$FAULT_REPO" commit -q -m "atomic fault source"
for fault_mode in deploy-exchange-term deploy-exchange-eio; do
	fault_slug="${fault_mode#deploy-exchange-}"
	fault_parent="$FIXTURE/$fault_slug-live-parent"
	fault_backups="$FIXTURE/$fault_slug-backups"
	fault_live="$fault_parent/live"
	mkdir -p "$fault_live/Core" "$fault_backups"
	cp "$FAULT_REPO/manifest.json" "$fault_live/manifest.json"
	printf 'old fault live\n' > "$fault_live/Core/Test.cs"
	printf 'old fault extra\n' > "$fault_live/extra.txt"
	fault_live_id="$(stat -Lc '%d:%i' -- "$fault_live")"
	snapshot "$fault_live" "$FIXTURE/$fault_slug-before.sha256"
	set +e
	failure="$(TAF_ATOMIC_FAULT="$fault_mode" \
		TAF_ATOMIC_FAULT_MARKER="$FIXTURE/$fault_slug-marker" \
		TAF_LIVE_MOD="$fault_live" TAF_DEPLOY_BACKUP_ROOT="$fault_backups" \
		"$FAULT_REPO/Tools/stage.sh" deploy --apply 2>&1)"
	status=$?
	set -e
	if [ "$fault_mode" = deploy-exchange-term ]; then
		[ "$status" -eq 143 ]
	else
		[ "$status" -ne 0 ]
		case "$failure" in *"Input/output error"*) ;;
			*) printf '%s\n' "$failure" >&2; exit 1 ;;
		esac
	fi
	case "$failure" in *"new live tree is committed"*) ;;
		*) printf '%s\n' "$failure" >&2; exit 1 ;;
	esac
	[ "$(<"$fault_live/Core/Test.cs")" = "new fault candidate" ]
	[ ! -e "$fault_live/extra.txt" ]
	[ "$(stat -Lc '%d:%i' -- "$fault_live")" != "$fault_live_id" ]
	[ "$(find "$fault_backups" -mindepth 1 -maxdepth 1 -type d | wc -l)" -eq 1 ]
	fault_backup="$(find "$fault_backups" -mindepth 1 -maxdepth 1 -type d -print -quit)"
	snapshot "$fault_backup" "$FIXTURE/$fault_slug-backup.sha256"
	cmp -s "$FIXTURE/$fault_slug-before.sha256" "$FIXTURE/$fault_slug-backup.sha256"
	[ -z "$(find -P "$fault_parent" -mindepth 1 -maxdepth 1 \
		-name '.live.taf-next-*' -print -quit)" ]
done

# Backup filesystem gets its own durable primitive probe. Unsupported directory fsync on that
# second filesystem stops before backup creation and before live exchange.
BACKUP_PROBE_PARENT="$FIXTURE/backup-probe-live-parent"
BACKUP_PROBE_LIVE="$BACKUP_PROBE_PARENT/live"
BACKUP_PROBE_ROOT="$FIXTURE/backup-probe-backups"
mkdir -p "$BACKUP_PROBE_LIVE/Core" "$BACKUP_PROBE_ROOT"
cp "$FAULT_REPO/manifest.json" "$BACKUP_PROBE_LIVE/manifest.json"
printf 'old backup-probe live\n' > "$BACKUP_PROBE_LIVE/Core/Test.cs"
printf 'old backup-probe extra\n' > "$BACKUP_PROBE_LIVE/extra.txt"
BACKUP_PROBE_LIVE_ID="$(stat -Lc '%d:%i' -- "$BACKUP_PROBE_LIVE")"
snapshot "$BACKUP_PROBE_LIVE" "$FIXTURE/backup-probe-before.sha256"
set +e
failure="$(TAF_ATOMIC_FAULT=backup-probe-unsupported \
	TAF_ATOMIC_FAULT_MARKER="$FIXTURE/backup-probe-count" \
	TAF_ATOMIC_FAULT_PARENT="$BACKUP_PROBE_ROOT" \
	TAF_LIVE_MOD="$BACKUP_PROBE_LIVE" TAF_DEPLOY_BACKUP_ROOT="$BACKUP_PROBE_ROOT" \
	"$FAULT_REPO/Tools/stage.sh" deploy --apply 2>&1)"
status=$?
set -e
[ "$status" -eq 75 ]
case "$failure" in *"directory fsync unavailable"*) ;;
	*) printf '%s\n' "$failure" >&2; exit 1 ;;
esac
[ -f "$FIXTURE/backup-probe-count" ]
snapshot "$BACKUP_PROBE_LIVE" "$FIXTURE/backup-probe-after.sha256"
cmp -s "$FIXTURE/backup-probe-before.sha256" "$FIXTURE/backup-probe-after.sha256"
[ "$(stat -Lc '%d:%i' -- "$BACKUP_PROBE_LIVE")" = "$BACKUP_PROBE_LIVE_ID" ]
[ -z "$(find -P "$BACKUP_PROBE_PARENT" -mindepth 1 -maxdepth 1 \
	-name '.live.taf-next-*' -print -quit)" ]
[ -z "$(find -P "$BACKUP_PROBE_ROOT" -mindepth 1 -print -quit)" ]

# Receipt filesystem durability is preflighted before candidate creation or live exchange.
RECEIPT_PROBE_PARENT="$FIXTURE/receipt-probe-live-parent"
RECEIPT_PROBE_LIVE="$RECEIPT_PROBE_PARENT/live"
RECEIPT_PROBE_BACKUPS="$FIXTURE/receipt-probe-backups"
mkdir -p "$RECEIPT_PROBE_LIVE/Core" "$RECEIPT_PROBE_BACKUPS"
cp "$FAULT_REPO/manifest.json" "$RECEIPT_PROBE_LIVE/manifest.json"
printf 'old receipt-probe live\n' > "$RECEIPT_PROBE_LIVE/Core/Test.cs"
printf 'old receipt-probe extra\n' > "$RECEIPT_PROBE_LIVE/extra.txt"
RECEIPT_PROBE_LIVE_ID="$(stat -Lc '%d:%i' -- "$RECEIPT_PROBE_LIVE")"
snapshot "$RECEIPT_PROBE_LIVE" "$FIXTURE/receipt-probe-before.sha256"
set +e
failure="$(TAF_ATOMIC_FAULT=receipt-probe-unsupported \
	TAF_ATOMIC_FAULT_MARKER="$FIXTURE/receipt-probe-marker" \
	TAF_ATOMIC_FAULT_PARENT="$FAULT_REPO/Tools" \
	TAF_LIVE_MOD="$RECEIPT_PROBE_LIVE" \
	TAF_DEPLOY_BACKUP_ROOT="$RECEIPT_PROBE_BACKUPS" \
	"$FAULT_REPO/Tools/stage.sh" deploy --apply 2>&1)"
status=$?
set -e
[ "$status" -eq 75 ]
case "$failure" in *"directory fsync unavailable"*) ;;
	*) printf '%s\n' "$failure" >&2; exit 1 ;;
esac
[ -f "$FIXTURE/receipt-probe-marker" ]
snapshot "$RECEIPT_PROBE_LIVE" "$FIXTURE/receipt-probe-after.sha256"
cmp -s "$FIXTURE/receipt-probe-before.sha256" "$FIXTURE/receipt-probe-after.sha256"
[ "$(stat -Lc '%d:%i' -- "$RECEIPT_PROBE_LIVE")" = "$RECEIPT_PROBE_LIVE_ID" ]
[ ! -e "$FAULT_REPO/Tools/last-deploy-receipt.txt" ] \
	&& [ ! -L "$FAULT_REPO/Tools/last-deploy-receipt.txt" ]
[ -z "$(find -P "$RECEIPT_PROBE_PARENT" -mindepth 1 -maxdepth 1 \
	-name '.live.taf-next-*' -print -quit)" ]
[ -z "$(find -P "$RECEIPT_PROBE_BACKUPS" -mindepth 1 -print -quit)" ]

# Receipt is non-authoritative. Direct-child TERM after its rename leaves exact committed new live
# and accepts exact new receipt; old live remains recoverable from content+mode backup.
for receipt_previous in absent existing; do
	receipt_path="$FAULT_REPO/Tools/last-deploy-receipt.txt"
	previous_receipt_id="absent"
	if [ "$receipt_previous" = existing ]; then
		printf 'previous exact receipt\n' > "$receipt_path"
		previous_receipt_id="$(stat -Lc '%d:%i' -- "$receipt_path")"
	else
		[ ! -e "$receipt_path" ] && [ ! -L "$receipt_path" ]
	fi
	RECEIPT_PARENT="$FIXTURE/receipt-$receipt_previous-live-parent"
	RECEIPT_LIVE="$RECEIPT_PARENT/live"
	RECEIPT_BACKUPS="$FIXTURE/receipt-$receipt_previous-backups"
	mkdir -p "$RECEIPT_LIVE/Core" "$RECEIPT_BACKUPS"
	cp "$FAULT_REPO/manifest.json" "$RECEIPT_LIVE/manifest.json"
	printf 'old receipt live\n' > "$RECEIPT_LIVE/Core/Test.cs"
	printf 'old receipt extra\n' > "$RECEIPT_LIVE/extra.txt"
	receipt_live_id="$(stat -Lc '%d:%i' -- "$RECEIPT_LIVE")"
	snapshot "$RECEIPT_LIVE" "$FIXTURE/receipt-$receipt_previous-before.sha256"
	set +e
	failure="$(TAF_ATOMIC_FAULT=receipt-publish-term \
		TAF_ATOMIC_FAULT_MARKER="$FIXTURE/receipt-$receipt_previous-publish-marker" \
		TAF_LIVE_MOD="$RECEIPT_LIVE" TAF_DEPLOY_BACKUP_ROOT="$RECEIPT_BACKUPS" \
		"$FAULT_REPO/Tools/stage.sh" deploy --apply 2>&1)"
	status=$?
	set -e
	[ "$status" -eq 143 ]
	case "$failure" in *"new live tree is committed"*) ;;
		*) printf '%s\n' "$failure" >&2; exit 1 ;;
	esac
	[ "$(<"$RECEIPT_LIVE/Core/Test.cs")" = "new fault candidate" ]
	[ ! -e "$RECEIPT_LIVE/extra.txt" ]
	[ "$(stat -Lc '%d:%i' -- "$RECEIPT_LIVE")" != "$receipt_live_id" ]
	[ -f "$receipt_path" ] && [ ! -L "$receipt_path" ]
	case "$(<"$receipt_path")" in *"receipt-authority: informational"*) ;;
		*) echo "new informational receipt was not retained" >&2; exit 1 ;;
	esac
	[ "$previous_receipt_id" = absent ] \
		|| [ "$(stat -Lc '%d:%i' -- "$receipt_path")" != "$previous_receipt_id" ]
	[ "$(find "$RECEIPT_BACKUPS" -mindepth 1 -maxdepth 1 -type d | wc -l)" -eq 1 ]
	receipt_backup="$(find "$RECEIPT_BACKUPS" -mindepth 1 -maxdepth 1 -type d -print -quit)"
	snapshot "$receipt_backup" "$FIXTURE/receipt-$receipt_previous-backup.sha256"
	cmp -s "$FIXTURE/receipt-$receipt_previous-before.sha256" \
		"$FIXTURE/receipt-$receipt_previous-backup.sha256"
	[ -z "$(find -P "$RECEIPT_PARENT" -mindepth 1 -maxdepth 1 \
		-name '.live.taf-next-*' -print -quit)" ]
	[ -z "$(find -P "$FAULT_REPO/Tools" -mindepth 1 -maxdepth 1 \
		-name '.last-deploy-receipt.tmp.*' -print -quit)" ]
done

# Replacement symlink/hardlink inserted after receipt-temp preinspection is never opened or
# truncated. Unknown identity is retained with status 5; unrelated target and old live stay exact.
for receipt_race_kind in symlink hardlink; do
	RECEIPT_RACE_REPO="$(make_stage_repo receipt-$receipt_race_kind-race-repo)"
	mkdir "$RECEIPT_RACE_REPO/Core"
	printf 'new receipt-race live\n' > "$RECEIPT_RACE_REPO/Core/Test.cs"
	install_atomic_fault_wrapper "$RECEIPT_RACE_REPO"
	git -C "$RECEIPT_RACE_REPO" init -q
	git -C "$RECEIPT_RACE_REPO" config user.name "TAF stage harness"
	git -C "$RECEIPT_RACE_REPO" config user.email "fixture@example.invalid"
	git -C "$RECEIPT_RACE_REPO" add --all
	git -C "$RECEIPT_RACE_REPO" commit -q -m "receipt race source"
	receipt_race_parent="$FIXTURE/receipt-$receipt_race_kind-race-live-parent"
	receipt_race_live="$receipt_race_parent/live"
	receipt_race_backups="$FIXTURE/receipt-$receipt_race_kind-race-backups"
	mkdir -p "$receipt_race_live/Core" "$receipt_race_backups"
	cp "$RECEIPT_RACE_REPO/manifest.json" "$receipt_race_live/manifest.json"
	printf 'old receipt-race live\n' > "$receipt_race_live/Core/Test.cs"
	printf 'old receipt-race extra\n' > "$receipt_race_live/extra.txt"
	receipt_race_live_id="$(stat -Lc '%d:%i' -- "$receipt_race_live")"
	printf 'unrelated receipt-race target\n' \
		> "$FIXTURE/receipt-$receipt_race_kind-unrelated"
	set +e
	failure="$(TAF_ATOMIC_FAULT="receipt-temp-$receipt_race_kind" \
		TAF_ATOMIC_FAULT_MARKER="$FIXTURE/receipt-$receipt_race_kind-race.marker" \
		TAF_ATOMIC_FAULT_EXTERNAL="$FIXTURE/receipt-$receipt_race_kind-unrelated" \
		TAF_LIVE_MOD="$receipt_race_live" \
		TAF_DEPLOY_BACKUP_ROOT="$receipt_race_backups" \
		"$RECEIPT_RACE_REPO/Tools/stage.sh" deploy --apply 2>&1)"
	status=$?
	set -e
	[ "$status" -eq 5 ]
	case "$failure" in *"cleanup ownership is unknown; retained"*) ;;
		*) printf '%s\n' "$failure" >&2; exit 1 ;;
	esac
	[ "$(<"$FIXTURE/receipt-$receipt_race_kind-unrelated")" \
		= "unrelated receipt-race target" ]
	[ "$(stat -Lc '%d:%i' -- "$receipt_race_live")" = "$receipt_race_live_id" ]
	[ "$(<"$receipt_race_live/Core/Test.cs")" = "old receipt-race live" ]
	[ -n "$(find -P "$RECEIPT_RACE_REPO/Tools" -mindepth 1 -maxdepth 1 \
		-name '.last-deploy-receipt.tmp.*' -print -quit)" ]
done

# Mutating repository after private verification cannot alter deployed bytes or receipt manifest.
FROZEN_RECEIPT_REPO="$(make_stage_repo frozen-receipt-repo)"
mkdir "$FROZEN_RECEIPT_REPO/Core"
printf 'immutable candidate bytes\n' > "$FROZEN_RECEIPT_REPO/Core/Test.cs"
install_atomic_fault_wrapper "$FROZEN_RECEIPT_REPO"
git -C "$FROZEN_RECEIPT_REPO" init -q
git -C "$FROZEN_RECEIPT_REPO" config user.name "TAF stage harness"
git -C "$FROZEN_RECEIPT_REPO" config user.email "fixture@example.invalid"
git -C "$FROZEN_RECEIPT_REPO" add --all
git -C "$FROZEN_RECEIPT_REPO" commit -q -m "frozen receipt source"
FROZEN_RECEIPT_LIVE="$FIXTURE/frozen-receipt-live-parent/live"
FROZEN_RECEIPT_BACKUPS="$FIXTURE/frozen-receipt-backups"
mkdir -p "$FROZEN_RECEIPT_LIVE/Core" "$FROZEN_RECEIPT_BACKUPS"
cp "$FROZEN_RECEIPT_REPO/manifest.json" "$FROZEN_RECEIPT_LIVE/manifest.json"
printf 'old frozen-receipt live\n' > "$FROZEN_RECEIPT_LIVE/Core/Test.cs"
TAF_ATOMIC_FAULT=mutate-repo-on-receipt \
	TAF_ATOMIC_FAULT_MARKER="$FIXTURE/frozen-receipt-mutation.marker" \
	TAF_ATOMIC_FAULT_EXTERNAL="$FROZEN_RECEIPT_REPO/Core/Test.cs" \
	TAF_LIVE_MOD="$FROZEN_RECEIPT_LIVE" \
	TAF_DEPLOY_BACKUP_ROOT="$FROZEN_RECEIPT_BACKUPS" \
	"$FROZEN_RECEIPT_REPO/Tools/stage.sh" deploy --apply >/dev/null
[ "$(<"$FROZEN_RECEIPT_LIVE/Core/Test.cs")" = "immutable candidate bytes" ]
[ "$(<"$FROZEN_RECEIPT_REPO/Core/Test.cs")" \
	= "repo changed after candidate verification" ]
frozen_live_hash="$(sha256sum -- "$FROZEN_RECEIPT_LIVE/Core/Test.cs" | cut -d' ' -f1)"
frozen_repo_hash="$(sha256sum -- "$FROZEN_RECEIPT_REPO/Core/Test.cs" | cut -d' ' -f1)"
grep -Fq "$frozen_live_hash  Core/Test.cs" \
	"$FROZEN_RECEIPT_REPO/Tools/last-deploy-receipt.txt"
! grep -Fq "$frozen_repo_hash  Core/Test.cs" \
	"$FROZEN_RECEIPT_REPO/Tools/last-deploy-receipt.txt"

# Untrappable process death cannot make cross-filesystem receipt authoritative. At either cold cut,
# new live is complete, exact old live remains under caller-known random sibling and in backup, and
# receipt is exact old/new. Next run reports retained identities and mutates nothing.
if command -v setsid >/dev/null 2>&1; then
	for cold_mode in deploy-exchange-kill receipt-publish-kill; do
		cold_slug="${cold_mode%-kill}"
		COLD_REPO="$(make_stage_repo cold-$cold_slug-repo)"
		mkdir "$COLD_REPO/Core"
		printf 'new cold live\n' > "$COLD_REPO/Core/Test.cs"
		install_atomic_fault_wrapper "$COLD_REPO"
		git -C "$COLD_REPO" init -q
		git -C "$COLD_REPO" config user.name "TAF stage harness"
		git -C "$COLD_REPO" config user.email "fixture@example.invalid"
		git -C "$COLD_REPO" add --all
		git -C "$COLD_REPO" commit -q -m "cold death source"
		cold_parent="$FIXTURE/cold-$cold_slug-live-parent"
		cold_live="$cold_parent/live"
		cold_backups="$FIXTURE/cold-$cold_slug-backups"
		mkdir -p "$cold_live/Core" "$cold_backups"
		cp "$COLD_REPO/manifest.json" "$cold_live/manifest.json"
		printf 'old cold live\n' > "$cold_live/Core/Test.cs"
		printf 'old cold extra\n' > "$cold_live/extra.txt"
		snapshot "$cold_live" "$FIXTURE/cold-$cold_slug-old.sha256"
		cold_previous_receipt_id=absent
		if [ "$cold_mode" = receipt-publish-kill ]; then
			printf 'old cold receipt\n' > "$COLD_REPO/Tools/last-deploy-receipt.txt"
			cold_previous_receipt_id="$(stat -Lc '%d:%i' \
				-- "$COLD_REPO/Tools/last-deploy-receipt.txt")"
		fi
		set +e
		failure="$(setsid env TAF_ATOMIC_FAULT="$cold_mode" \
			TAF_ATOMIC_FAULT_MARKER="$FIXTURE/cold-$cold_slug.marker" \
			TAF_LIVE_MOD="$cold_live" TAF_DEPLOY_BACKUP_ROOT="$cold_backups" \
			"$COLD_REPO/Tools/stage.sh" deploy --apply 2>&1)"
		status=$?
		set -e
		[ "$status" -eq 137 ]
		[ -f "$FIXTURE/cold-$cold_slug.marker" ]
		[ "$(<"$cold_live/Core/Test.cs")" = "new cold live" ]
		[ ! -e "$cold_live/extra.txt" ]
		cold_old_sibling="$(find -P "$cold_parent" -mindepth 1 -maxdepth 1 \
			-name '.live.taf-next-*' -type d -print -quit)"
		[ -n "$cold_old_sibling" ]
		snapshot "$cold_old_sibling" "$FIXTURE/cold-$cold_slug-sibling.sha256"
		cmp -s "$FIXTURE/cold-$cold_slug-old.sha256" \
			"$FIXTURE/cold-$cold_slug-sibling.sha256"
		cold_backup="$(find "$cold_backups" -mindepth 1 -maxdepth 1 -type d -print -quit)"
		[ -n "$cold_backup" ]
		snapshot "$cold_backup" "$FIXTURE/cold-$cold_slug-backup.sha256"
		cmp -s "$FIXTURE/cold-$cold_slug-old.sha256" \
			"$FIXTURE/cold-$cold_slug-backup.sha256"
		if [ "$cold_mode" = deploy-exchange-kill ]; then
			[ ! -e "$COLD_REPO/Tools/last-deploy-receipt.txt" ]
			[ -n "$(find -P "$COLD_REPO/Tools" -mindepth 1 -maxdepth 1 \
				-name '.last-deploy-receipt.tmp.*' -type f -print -quit)" ]
		else
			[ -f "$COLD_REPO/Tools/last-deploy-receipt.txt" ]
			case "$(<"$COLD_REPO/Tools/last-deploy-receipt.txt")" in
				*"receipt-authority: informational"*) ;;
				*) echo "cold new receipt missing" >&2; exit 1 ;;
			esac
			cold_old_receipt="$(find -P "$COLD_REPO/Tools" -mindepth 1 -maxdepth 1 \
				-name '.last-deploy-receipt.tmp.*' -type f -print -quit)"
			[ -n "$cold_old_receipt" ]
			[ "$(stat -Lc '%d:%i' -- "$cold_old_receipt")" \
				= "$cold_previous_receipt_id" ]
			[ "$(<"$cold_old_receipt")" = "old cold receipt" ]
		fi
		snapshot "$cold_live" "$FIXTURE/cold-$cold_slug-before-retry.sha256"
		set +e
		failure="$(TAF_LIVE_MOD="$cold_live" TAF_DEPLOY_BACKUP_ROOT="$cold_backups" \
			"$COLD_REPO/Tools/stage.sh" deploy --apply 2>&1)"
		status=$?
		set -e
		[ "$status" -eq 5 ]
		case "$failure" in *"prior interrupted deployment requires recovery"*) ;;
			*) printf '%s\n' "$failure" >&2; exit 1 ;;
		esac
		snapshot "$cold_live" "$FIXTURE/cold-$cold_slug-after-retry.sha256"
		cmp -s "$FIXTURE/cold-$cold_slug-before-retry.sha256" \
			"$FIXTURE/cold-$cold_slug-after-retry.sha256"
	done
else
	echo "COLD PROCESS-DEATH FIXTURES SKIPPED: setsid unavailable" >&2
fi

# Real second filesystem when available: backup materializes and verifies there independently.
fixture_device="$(stat -c %d -- "$FIXTURE")"
second_fs_device="unavailable"
if [ -d /dev/shm ] && [ -w /dev/shm ]; then
	second_fs_device="$(stat -c %d -- /dev/shm)"
fi
if [ "$second_fs_device" != unavailable ] && [ "$second_fs_device" != "$fixture_device" ]; then
	SECOND_FS_FIXTURE="$(mktemp -d /dev/shm/taf-stage-safety.XXXXXX)"
	CROSS_FS_REPO="$(make_stage_repo cross-filesystem-backup-repo)"
	mkdir "$CROSS_FS_REPO/Core"
	printf 'new cross-filesystem live\n' > "$CROSS_FS_REPO/Core/Test.cs"
	git -C "$CROSS_FS_REPO" init -q
	git -C "$CROSS_FS_REPO" config user.name "TAF stage harness"
	git -C "$CROSS_FS_REPO" config user.email "fixture@example.invalid"
	git -C "$CROSS_FS_REPO" add --all
	git -C "$CROSS_FS_REPO" commit -q -m "cross-filesystem source"
	CROSS_FS_LIVE="$FIXTURE/cross-filesystem-live-parent/live"
	CROSS_FS_BACKUPS="$SECOND_FS_FIXTURE/backups"
	mkdir -p "$CROSS_FS_LIVE/Core" "$CROSS_FS_BACKUPS"
	cp "$CROSS_FS_REPO/manifest.json" "$CROSS_FS_LIVE/manifest.json"
	printf 'old cross-filesystem live\n' > "$CROSS_FS_LIVE/Core/Test.cs"
	printf 'old cross-filesystem extra\n' > "$CROSS_FS_LIVE/extra.txt"
	TAF_LIVE_MOD="$CROSS_FS_LIVE" TAF_DEPLOY_BACKUP_ROOT="$CROSS_FS_BACKUPS" \
		"$CROSS_FS_REPO/Tools/stage.sh" deploy --apply >/dev/null
	cross_fs_backup="$(find "$CROSS_FS_BACKUPS" -mindepth 1 -maxdepth 1 \
		-type d -print -quit)"
	[ -n "$cross_fs_backup" ]
	[ "$(stat -c %d -- "$cross_fs_backup")" != "$(stat -c %d -- "$CROSS_FS_LIVE")" ]
	[ "$(<"$cross_fs_backup/Core/Test.cs")" = "old cross-filesystem live" ]
else
	echo "CROSS-FILESYSTEM BACKUP FIXTURE SKIPPED: /dev/shm device=$second_fs_device, fixture device=$fixture_device, writable=$([ -w /dev/shm ] 2>/dev/null && echo yes || echo no)" >&2
fi

# A successful apply exposes one complete new tree and retains one exact old-tree backup.
SUCCESS_REPO="$(make_stage_repo successful-deploy-repo)"
mkdir "$SUCCESS_REPO/Core"
printf 'new deployed bytes\n' > "$SUCCESS_REPO/Core/Test.cs"
git -C "$SUCCESS_REPO" init -q
git -C "$SUCCESS_REPO" config user.name "TAF stage harness"
git -C "$SUCCESS_REPO" config user.email "fixture@example.invalid"
git -C "$SUCCESS_REPO" add --all
git -C "$SUCCESS_REPO" commit -q -m "successful deploy source"
mkdir -p "$FIXTURE/success-live-parent/live/Core" "$FIXTURE/success-backups"
SUCCESS_LIVE="$FIXTURE/success-live-parent/live"
cp "$SUCCESS_REPO/manifest.json" "$SUCCESS_LIVE/manifest.json"
printf 'old deployed bytes\n' > "$SUCCESS_LIVE/Core/Test.cs"
printf 'old backup-only bytes\n' > "$SUCCESS_LIVE/extra.txt"
chmod 2751 "$SUCCESS_LIVE"
chmod 3711 "$SUCCESS_LIVE/Core"
chmod 6751 "$SUCCESS_LIVE/Core/Test.cs"
TAF_LIVE_MOD="$SUCCESS_LIVE" TAF_DEPLOY_BACKUP_ROOT="$FIXTURE/success-backups" \
	"$SUCCESS_REPO/Tools/stage.sh" deploy --apply >/dev/null
"$SUCCESS_REPO/Tools/stage.sh" verify "$SUCCESS_LIVE" >/dev/null
[ ! -e "$SUCCESS_LIVE/extra.txt" ] && [ ! -L "$SUCCESS_LIVE/extra.txt" ]
SUCCESS_BACKUP="$(find "$FIXTURE/success-backups" -mindepth 1 -maxdepth 1 -type d -print -quit)"
[ "$(<"$SUCCESS_BACKUP/Core/Test.cs")" = "old deployed bytes" ]
[ "$(<"$SUCCESS_BACKUP/extra.txt")" = "old backup-only bytes" ]
[ "$(stat -c %a -- "$SUCCESS_BACKUP")" = 2751 ]
[ "$(stat -c %a -- "$SUCCESS_BACKUP/Core")" = 3711 ]
[ "$(stat -c %a -- "$SUCCESS_BACKUP/Core/Test.cs")" = 6751 ]
grep -Fq "backup-contract: exact regular-file content and file/directory permission modes" \
	"$SUCCESS_REPO/Tools/last-deploy-receipt.txt"
grep -Fq "backup-excludes: ownership, timestamps, xattrs, ACLs, sparse layout, hard-link topology" \
	"$SUCCESS_REPO/Tools/last-deploy-receipt.txt"
! grep -Fqi "full backup" "$REPO/Tools/stage.sh" "$REPO/Tools/atomic_tree_publish.py"

echo "STAGE TARGET SAFETY CLEAN"
