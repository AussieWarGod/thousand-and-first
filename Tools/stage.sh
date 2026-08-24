#!/usr/bin/env bash
# Canonical runtime staging for The Thousand and First.
#
# One source of truth for "what the game loads". Build gate, deploy, and Workshop
# packaging all consume this set, so the gate stops asking a different question
# from the one that matters (COORDINATION.md, open question answered 2026-08-20).
#
#   Tools/stage.sh manifest            print sorted relative-path + sha256 manifest
#   Tools/stage.sh list                print sorted relative paths only
#   Tools/stage.sh list-head [commit]  print the tracked runtime paths selected from a commit
#   Tools/stage.sh copy <dir>          materialise the runtime set into <dir>
#   Tools/stage.sh verify              prove a fresh cold-install tree matches byte-for-byte
#   Tools/stage.sh verify <dir>        prove an existing tree is exactly the runtime set
#   Tools/stage.sh diff                inventory diff: staged set vs the live mod folder
#   Tools/stage.sh deploy              DRY RUN: what deploy would add/update/delete
#   Tools/stage.sh deploy --apply      back up, mirror into the live folder, verify, receipt
#
# Nothing is written to the live folder without --apply, and --apply always
# takes a full backup of the existing live folder first.

set -euo pipefail

REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
LIVE_DEFAULT="/mnt/c/Users/Reegan/AppData/LocalLow/Freehold Games/CavesOfQud/Mods/ThousandAndFirst"
LIVE="${TAF_LIVE_MOD:-$LIVE_DEFAULT}"

# The runtime set, stated as an EXCLUSION rather than a list of blessed directories.
#
# It used to name the code directories positively, and Experience/ was missing from that
# list for a whole wave: three finished features compiled in the gate and then silently
# never shipped to the game. A positive list fails in the worst direction - new work
# disappears without a word. An exclusion list fails safe: something unwanted ships and is
# obvious, instead of something wanted vanishing and being invisible.
#
# Everything is staged EXCEPT these, which are development-only and must never reach the
# live folder or the Workshop.
EXCLUDE_DIRS=(.git _notes DevTests Art docs Tools .ruff_cache __pycache__ .agent-handoff)
# Metadata that is harmless in a mod folder and wanted on the Workshop.
ROOT_META=(README.md LICENSE CHANGELOG.md manifest.json modconfig.json preview.png workshop.json)
# Asset trees copied whole.
ASSET_DIRS=(Textures)

stage_list0() {
	cd "$REPO"
	for f in "${ROOT_META[@]}"; do
		if [ -e "$f" ] || [ -L "$f" ]; then
			[ -f "$f" ] && [ ! -L "$f" ] || {
				echo "runtime metadata is not a regular non-link file: $f" >&2; return 3; }
			printf '%s\0' "$f"
		fi
	done
	# Every .cs and .xml the game could load, wherever it lives, minus the excluded trees.
	local args=( . )
	local d
	for d in "${EXCLUDE_DIRS[@]}"; do
		args+=( -path "./$d" -prune -o )
	done
	local linked
	linked="$(find "${args[@]}" -type l \( -name '*.cs' -o -name '*.xml' \
		-o -xtype d -o -xtype l \) -print -quit)"
	[ -z "$linked" ] || {
		echo "runtime discovery contains a link: $linked" >&2; return 3; }
	args+=( -type f '(' -name '*.cs' -o -name '*.xml' ')' -print0 )
	find "${args[@]}"
	for d in "${ASSET_DIRS[@]}"; do
		if [ -e "$d" ] || [ -L "$d" ]; then
			[ -d "$d" ] && [ ! -L "$d" ] || {
				echo "runtime asset tree is not an ordinary directory: $d" >&2; return 3; }
			linked="$(find "$d" -type l -print -quit)"
			[ -z "$linked" ] || {
				echo "runtime asset tree contains a link: $linked" >&2; return 3; }
			find "$d" -type f -print0
		fi
	done
}

validate_sort_paths() {
	local label="$1"
	python3 -c '
import posixpath
import re
import sys
import unicodedata

label = sys.argv[1]
payload = sys.stdin.buffer.read()
if payload and not payload.endswith(b"\0"):
    raise SystemExit(label + " inventory ended without a NUL record boundary")
raw_paths = payload[:-1].split(b"\0") if payload else []
validated = []
seen = set()
windows_seen = {}
reserved = re.compile(
    r"^(?:CON|PRN|AUX|NUL|CONIN\$|CONOUT\$|"
    r"COM[1-9\u00b9\u00b2\u00b3]|LPT[1-9\u00b9\u00b2\u00b3])$",
    re.IGNORECASE,
)
for raw in raw_paths:
    if raw.startswith(b"./"):
        raw = raw[2:]
    try:
        path = raw.decode("utf-8", "strict")
    except UnicodeDecodeError:
        raise SystemExit(label + " inventory contains a non-UTF-8 path")
    components = path.split("/")
    unsafe_character = any(
        character == "\\" or unicodedata.category(character).startswith("C")
        for character in path
    )
    if (not path or path.startswith("/") or unsafe_character
            or any(component in ("", ".", "..") for component in components)
            or posixpath.normpath(path) != path):
        raise SystemExit(label + " inventory contains an unsafe relative path: " + repr(path))
    for component in components:
        if (any(character in "<>:\"|?*" for character in component)
                or component.endswith((".", " "))):
            raise SystemExit(
                label + " inventory contains a Windows-incompatible path: " + repr(path)
            )
        device_name = component.rstrip(". ").split(".", 1)[0]
        if reserved.fullmatch(device_name):
            raise SystemExit(
                label + " inventory contains a Windows-reserved path: " + repr(path)
            )
    encoded = path.encode("utf-8")
    if encoded in seen:
        raise SystemExit(label + " inventory contains a duplicate path: " + repr(path))
    seen.add(encoded)
    windows_key = path.casefold()
    if windows_key in windows_seen:
        raise SystemExit(
            label + " inventory contains a Windows case-fold collision: "
            + repr(windows_seen[windows_key]) + " and " + repr(path)
        )
    windows_seen[windows_key] = path
    validated.append((encoded, path))
for encoded, _path in sorted(validated):
    sys.stdout.buffer.write(encoded + b"\n")
' "$label"
}

sorted_list() { stage_list0 | validate_sort_paths "runtime stage"; }

cmd_list() { sorted_list; }

stage_list_head0() {
	local commit="$1" path include d f excluded
	cd "$REPO"
	git cat-file -e "$commit^{commit}" 2>/dev/null || {
		echo "cannot resolve stage commit: $commit" >&2; return 2; }
	git ls-tree -r -z --name-only "$commit" | while IFS= read -r -d '' path; do
		include=0
		for f in "${ROOT_META[@]}"; do
			[ "$path" != "$f" ] || { include=1; break; }
		done
		if [ "$include" -eq 0 ]; then
			for d in "${ASSET_DIRS[@]}"; do
				case "$path" in "$d"/*) include=1; break ;; esac
			done
		fi
		if [ "$include" -eq 0 ]; then
			case "$path" in
				*.cs|*.xml)
					excluded=0
					for d in "${EXCLUDE_DIRS[@]}"; do
						case "$path" in "$d"|"$d"/*) excluded=1; break ;; esac
					done
					[ "$excluded" -ne 0 ] || include=1 ;;
			esac
		fi
		[ "$include" -eq 0 ] || printf '%s\0' "$path"
	done
}

cmd_list_head() { stage_list_head0 "${1:-HEAD}" | validate_sort_paths "commit stage"; }

cmd_manifest() {
	cd "$REPO"
	sorted_list | while IFS= read -r f; do
		printf '%s  %s\n' "$(sha256sum -- "$f" | cut -d' ' -f1)" "$f"
	done
}

cmd_copy() (
	local dest="$1" dest_lex dest_real parent parent_real repo_real overlap
	local created=0 created_id="" boundary_ready=0
	cleanup_created_copy_dir() {
		local status=$?
		trap - EXIT
		set +e
		if [ "$created" -eq 1 ] && [ "$boundary_ready" -eq 0 ] \
				&& [ -d "$dest_lex" ] && [ ! -L "$dest_lex" ] \
				&& [ "$(stat -Lc '%d:%i' -- "$dest_lex" 2>/dev/null)" = "$created_id" ] \
				&& [ -z "$(find -P "$dest_lex" -mindepth 1 -print -quit 2>/dev/null)" ]; then
			rmdir -- "$dest_lex"
		fi
		exit "$status"
	}
	trap cleanup_created_copy_dir EXIT
	[ -n "$dest" ] || { echo "copy needs a destination" >&2; exit 2; }
	dest_lex="$(realpath -ms -- "$dest")"
	repo_real="$(realpath -e -- "$REPO")"
	[ "$dest_lex" != "/" ] || { echo "refusing broad stage copy target: /" >&2; exit 2; }
	case "$dest_lex" in
		"$repo_real"|"$repo_real"/*)
			echo "refusing stage copy target inside repository: $dest_lex" >&2; exit 2 ;;
	esac
	case "$repo_real" in
		"$dest_lex"|"$dest_lex"/*)
			echo "refusing stage copy target containing repository: $dest_lex" >&2; exit 2 ;;
	esac
	parent="$(dirname -- "$dest_lex")"
	[ -d "$parent" ] || { echo "stage copy parent does not exist: $parent" >&2; exit 2; }
	parent_real="$(realpath -e -- "$parent")"
	[ "$parent" = "$parent_real" ] || {
		echo "refusing linked stage copy parent: $parent -> $parent_real" >&2; exit 2; }
	if [ -e "$dest_lex" ] || [ -L "$dest_lex" ]; then
		[ ! -L "$dest_lex" ] || {
			echo "refusing linked stage copy target: $dest_lex" >&2; exit 2; }
		[ -d "$dest_lex" ] && [ ! -L "$dest_lex" ] || {
			echo "stage copy target is not an ordinary directory: $dest_lex" >&2; exit 2; }
		[ -z "$(find -P "$dest_lex" -mindepth 1 -print -quit)" ] || {
			echo "stage copy target is not empty: $dest_lex" >&2; exit 2; }
	else
		mkdir -- "$dest_lex"
		created=1
		created_id="$(stat -Lc '%d:%i' -- "$dest_lex")"
	fi
	dest_real="$(realpath -e -- "$dest_lex")"
	[ "$dest_lex" = "$dest_real" ] || {
		echo "refusing linked stage copy target: $dest_lex -> $dest_real" >&2; exit 2; }
	overlap="$(directory_overlap "$repo_real" "$dest_real")"
	[ -z "$overlap" ] || {
		echo "refusing stage copy alias into repository: $overlap" >&2; exit 2; }
	boundary_ready=1
	trap - EXIT
	dest="$dest_real"
	cd "$REPO"
	sorted_list | while IFS= read -r f; do
		[ -f "$f" ] && [ ! -L "$f" ] || {
			echo "staged source is not a regular non-link file: $f" >&2; exit 3; }
		mkdir -p "$dest/$(dirname "$f")"
		cp -p -- "$f" "$dest/$f"
	done
)

tree_list() {
	local tree="$1"
	[ -d "$tree" ] || return 0
	(
		cd "$tree"
		find . -type f -print0 | validate_sort_paths "verification tree"
	)
}

verify_tree() (
	local tree="$1"
	local scratch; scratch="$(mktemp -d)"
	trap 'rm -rf -- "$scratch"' EXIT

	sorted_list > "$scratch/expected"
	tree_list "$tree" > "$scratch/actual"
	if ! cmp -s "$scratch/expected" "$scratch/actual"; then
		echo "COLD-INSTALL INVENTORY MISMATCH: $tree" >&2
		echo "=== MISSING ===" >&2
		comm -23 "$scratch/expected" "$scratch/actual" >&2
		echo "=== UNEXPECTED ===" >&2
		comm -13 "$scratch/expected" "$scratch/actual" >&2
		return 1
	fi

	local failed=0
	while IFS= read -r f; do
		cmp -s "$REPO/$f" "$tree/$f" || {
			echo "COLD-INSTALL CONTENT MISMATCH: $f" >&2
			failed=1
		}
	done < "$scratch/expected"
	if find "$tree" -type l -print -quit | grep -q .; then
		echo "COLD-INSTALL CONTAINS A SYMBOLIC LINK" >&2
		failed=1
	fi
	local special; special="$(find -P "$tree" -mindepth 1 ! -type f ! -type d -print -quit)"
	if [ -n "$special" ]; then
		echo "COLD-INSTALL CONTAINS A SPECIAL FILE: $special" >&2
		failed=1
	fi
	manifest_is_ours "$tree/manifest.json" || {
		echo "COLD-INSTALL MANIFEST IDENTITY MISMATCH" >&2
		failed=1
	}
	[ "$failed" -eq 0 ] || return 1
	echo "COLD-INSTALL CLEAN ($(wc -l < "$scratch/expected") files)"
)

manifest_is_ours() {
	python3 - "$1" <<'PY' >/dev/null 2>&1
import json
import sys

with open(sys.argv[1], encoding="utf-8-sig") as stream:
    data = json.load(stream)
if not isinstance(data, dict) or data.get("id") != "r_ThousandAndFirst":
    raise SystemExit(1)
PY
}

directory_overlap() {
	python3 - "$1" "$2" <<'PY'
import os
import sys

left, right = sys.argv[1:]

def walk(root):
    def fail(error):
        raise error
    for current, _directories, _files in os.walk(
            root, topdown=True, followlinks=False, onerror=fail):
        stat = os.stat(current, follow_symlinks=False)
        yield current, (stat.st_dev, stat.st_ino)

known = {identity: path for path, identity in walk(left)}
for path, identity in walk(right):
    if identity in known:
        print(path + " -> " + known[identity])
        break
PY
}

# A bind mount is an alias without a symlink or necessarily a different device/inode in either
# tree. Never walk a mount rooted at or below a tree we intend to mirror, delete, back up, or
# restore. /proc/self/mountinfo exposes same-device bind mounts as distinct mount records.
mount_boundary() {
	python3 - "$1" <<'PY'
import os
import re
import sys

root = os.path.realpath(sys.argv[1])

def unescape_mount_field(value):
    return re.sub(r"\\([0-7]{3})", lambda match: chr(int(match.group(1), 8)), value)

with open("/proc/self/mountinfo", encoding="utf-8") as stream:
    for line in stream:
        fields = line.split(" - ", 1)[0].split()
        if len(fields) < 5:
            raise RuntimeError("malformed /proc/self/mountinfo record")
        mountpoint = os.path.normpath(unescape_mount_field(fields[4]))
        try:
            inside = os.path.commonpath((root, mountpoint)) == root
        except ValueError:
            inside = False
        if inside:
            print(mountpoint)
            break
PY
}

cmd_verify() (
	local supplied="${1:-}"
	if [ -n "$supplied" ]; then
		verify_tree "$(cd "$supplied" && pwd)"
		return
	fi
	local cold; cold="$(mktemp -d)"
	trap 'rm -rf -- "$cold"' EXIT
	cmd_copy "$cold"
	verify_tree "$cold"
)

# Every relative path currently in the live folder, excluding the dev worktree
# metadata that a git-based sync leaves behind.
live_list() {
	[ -d "$LIVE" ] || return 0
	cd "$LIVE"
	find . -type f -not -path './.git/*' -print0 | validate_sort_paths "live mod"
}

# Prove that the mirror destination is one ordinary, separate directory before even a dry-run
# inventory walks it.  A mod-shaped symlink back into the checkout would otherwise make the
# later copy/delete loop operate on source files.  Interior links are equally unsafe: copying
# Core/Foo.cs through a linked Core/ would leave the live root while still writing elsewhere.
validate_live_target() {
	local live_lex live_real repo_real linked special hardlink boundary overlap
	[ -n "$LIVE" ] || { echo "live mod folder is empty" >&2; exit 2; }
	live_lex="$(realpath -ms -- "$LIVE")"
	repo_real="$(realpath -e -- "$REPO")"
	[ "$live_lex" != "/" ] || { echo "refusing broad live mod folder: /" >&2; exit 2; }
	[ -d "$live_lex" ] || { echo "live mod folder not found: $live_lex" >&2; exit 2; }
	live_real="$(realpath -e -- "$live_lex")"
	[ "$live_lex" = "$live_real" ] || {
		echo "refusing linked live mod path: $live_lex -> $live_real" >&2; exit 2; }

	case "$live_real" in
		"$repo_real"|"$repo_real"/*)
			echo "refusing live mod folder inside repository: $live_real" >&2; exit 2 ;;
	esac
	case "$repo_real" in
		"$live_real"|"$live_real"/*)
			echo "refusing live mod folder containing repository: $live_real" >&2; exit 2 ;;
	esac

	# Refuse mount aliases before any recursive inventory enters them.
	boundary="$(mount_boundary "$live_real")"
	[ -z "$boundary" ] || {
		echo "refusing mount boundary inside live mod folder: $boundary" >&2; exit 2; }
	linked="$(find -P "$live_real" -type l -print -quit)"
	[ -z "$linked" ] || {
		echo "refusing linked path inside live mod folder: $linked" >&2; exit 2; }
	special="$(find -P "$live_real" -mindepth 1 ! -type f ! -type d -print -quit)"
	[ -z "$special" ] || {
		echo "refusing special file inside live mod folder: $special" >&2; exit 2; }
	hardlink="$(find -P "$live_real" -path "$live_real/.git" -prune -o \
		-type f -links +1 -print -quit)"
	[ -z "$hardlink" ] || {
		echo "refusing hard-linked file inside live mod folder: $hardlink" >&2; exit 2; }
	if [ -L "$live_real/.git" ] || { [ -e "$live_real/.git" ] && [ ! -d "$live_real/.git" ]; }; then
		echo "refusing linked-worktree metadata in live mod folder: $live_real" >&2
		exit 2
	fi

	# Path spelling does not expose bind mounts.  Compare every directory identity in both trees;
	# an interior alias such as live/Core -> repo/Core is as unsafe as aliased roots.
	overlap="$(directory_overlap "$REPO" "$live_real")"
	[ -z "$overlap" ] || {
		echo "refusing directory alias between live mod and repository: $overlap" >&2; exit 2; }

	manifest_is_ours "$live_real/manifest.json" || {
		echo "refusing: $live_real/manifest.json does not declare r_ThousandAndFirst" >&2; exit 3; }
	LIVE="$live_real"
}

prepare_backup_root() (
	local proposed root_lex root_real repo_real live_parent parent parent_real boundary overlap
	local created=0 created_id="" boundary_ready=0
	cleanup_created_backup_root() {
		local status=$?
		trap - EXIT
		set +e
		if [ "$created" -eq 1 ] && [ "$boundary_ready" -eq 0 ] \
				&& [ -d "$root_lex" ] && [ ! -L "$root_lex" ] \
				&& [ "$(stat -Lc '%d:%i' -- "$root_lex" 2>/dev/null)" = "$created_id" ] \
				&& [ -z "$(find -P "$root_lex" -mindepth 1 -print -quit 2>/dev/null)" ]; then
			rmdir -- "$root_lex"
		fi
		exit "$status"
	}
	trap cleanup_created_backup_root EXIT
	live_parent="$(dirname "$LIVE")"
	if [ -n "${TAF_DEPLOY_BACKUP_ROOT:-}" ]; then
		proposed="$TAF_DEPLOY_BACKUP_ROOT"
	else
		case "$(basename "$live_parent")" in
			Mods|mods|MODS) proposed="$(dirname "$live_parent")/TAF-ModBackups" ;;
			*) echo "custom live path requires TAF_DEPLOY_BACKUP_ROOT" >&2; exit 2 ;;
		esac
	fi
	[ -n "$proposed" ] || { echo "deployment backup root is empty" >&2; exit 2; }
	root_lex="$(realpath -ms -- "$proposed")"
	repo_real="$(realpath -e -- "$REPO")"
	case "$root_lex" in
		/|"$live_parent"|"$live_parent"/*)
			echo "refusing broad or scanned deployment backup root: $root_lex" >&2; exit 2 ;;
		"$REPO"|"$REPO"/*|"$LIVE"|"$LIVE"/*)
			echo "refusing deployment backup root inside source or live tree: $root_lex" >&2; exit 2 ;;
	esac
	case "$repo_real" in
		"$root_lex"|"$root_lex"/*)
			echo "refusing deployment backup root containing repository: $root_lex" >&2; exit 2 ;;
	esac
	if [ -e "$root_lex" ] || [ -L "$root_lex" ]; then
		[ -d "$root_lex" ] && [ ! -L "$root_lex" ] || {
			echo "deployment backup root is not an ordinary directory: $root_lex" >&2; exit 2; }
	else
		parent="$(dirname -- "$root_lex")"
		[ -d "$parent" ] || {
			echo "deployment backup parent does not exist: $parent" >&2; exit 2; }
		parent_real="$(realpath -e -- "$parent")"
		[ "$parent" = "$parent_real" ] || {
			echo "refusing linked deployment backup parent: $parent -> $parent_real" >&2; exit 2; }
		mkdir -- "$root_lex"
		created=1
		created_id="$(stat -Lc '%d:%i' -- "$root_lex")"
	fi
	root_real="$(realpath -e -- "$root_lex")"
	[ "$root_lex" = "$root_real" ] || {
		echo "refusing linked deployment backup root: $root_lex -> $root_real" >&2; exit 2; }
	boundary="$(mount_boundary "$root_real")"
	[ -z "$boundary" ] || {
		echo "refusing mount boundary inside deployment backup root: $boundary" >&2; exit 2; }
	overlap="$(directory_overlap "$REPO" "$root_real")"
	[ -z "$overlap" ] || {
		echo "refusing deployment backup alias into repository: $overlap" >&2; exit 2; }
	overlap="$(directory_overlap "$LIVE" "$root_real")"
	[ -z "$overlap" ] || {
		echo "refusing deployment backup alias into live tree: $overlap" >&2; exit 2; }
	boundary_ready=1
	trap - EXIT
	printf '%s\n' "$root_real"
)

cmd_diff() (
	# Keep cleanup scoped to this diff invocation.  A RETURN trap installed in
	# a function leaks into its caller on this Bash path; once cmd_diff returns,
	# its local tmp is gone and the caller's RETURN trips `set -u`.
	local tmp; tmp="$(mktemp -d)"
	trap 'rm -rf -- "$tmp"' EXIT
	sorted_list > "$tmp/staged"
	live_list > "$tmp/live"

	echo "=== ADD (staged, absent live) ==="
	comm -23 "$tmp/staged" "$tmp/live"
	echo "=== UPDATE (present both, content differs) ==="
	comm -12 "$tmp/staged" "$tmp/live" | while IFS= read -r f; do
		cmp -s "$REPO/$f" "$LIVE/$f" || printf '%s\n' "$f"
	done
	echo "=== DELETE (live, not in runtime set) ==="
	comm -13 "$tmp/staged" "$tmp/live"
)

cmd_deploy() (
	local apply="${1:-}"
	local backup="" tmp="" receipt_tmp="" rollback_armed=0 source_commit source_dirty
	validate_live_target
	source_commit="$(git -C "$REPO" rev-parse HEAD)"
	source_dirty="$(git -C "$REPO" status --porcelain=v1 --untracked-files=all | wc -l)"

	echo "repo:   $REPO (${source_commit:0:7}, $source_dirty dirty)"
	echo "live:   $LIVE"
	echo "staged: $(sorted_list | wc -l) files"
	echo
	cmd_diff

	if [ "$apply" != "--apply" ]; then
		echo
		echo "DRY RUN. Nothing written. Re-run with --apply to back up and mirror."
		exit 0
	fi

	local stamp backup_root
	stamp="$(date +%Y%m%d-%H%M%S)"
	backup_root="$(prepare_backup_root)"
	backup="$backup_root/ThousandAndFirst-${stamp}-$$"
	[ ! -e "$backup" ] && [ ! -L "$backup" ] || {
		echo "refusing existing deployment backup: $backup" >&2; exit 3; }
	echo
	echo "backup: $backup"
	cp -a -- "$LIVE" "$backup"
	# Recheck after the backup and immediately before the first write.  This also proves the copy
	# did not expose a link hidden by the host filesystem.
	validate_live_target

	rollback_deploy() {
		local original_status=$? restore_failed=0
		trap - EXIT
		set +e
		if [ -n "$tmp" ] && [ -e "$tmp" ]; then
			case "$tmp" in /tmp/tmp.*) find -P "$tmp" -depth -delete || true ;; esac
		fi
		if [ -n "$receipt_tmp" ] && [ -e "$receipt_tmp" ]; then
			case "$receipt_tmp" in
				"$REPO/Tools/.last-deploy-receipt.tmp."*) unlink -- "$receipt_tmp" || true ;;
			esac
		fi
		if [ "$rollback_armed" -eq 1 ]; then
			if [ -e "$LIVE" ] || [ -L "$LIVE" ]; then
				find -P "$LIVE" -depth -delete || restore_failed=1
			fi
			cp -a -- "$backup" "$LIVE" || restore_failed=1
			if [ "$restore_failed" -eq 0 ]; then
				echo "DEPLOY FAILED — live folder restored from $backup" >&2
			else
				echo "DEPLOY FAILED — automatic restore also failed; preserve $backup" >&2
				original_status=5
			fi
		fi
		exit "$original_status"
	}
	rollback_armed=1
	trap rollback_deploy EXIT

	# Mirror: copy the runtime set, then remove live files outside it.
	cd "$REPO"
	sorted_list | while IFS= read -r f; do
		target_dir="$LIVE/$(dirname "$f")"
		mkdir -p "$target_dir"
		copy_tmp="$(mktemp "$target_dir/.taf-stage-copy.XXXXXX")"
		cp -p -- "$f" "$copy_tmp"
		mv -T -- "$copy_tmp" "$LIVE/$f"
	done
	tmp="$(mktemp -d)"
	sorted_list > "$tmp/staged"
	live_list > "$tmp/live"
	comm -13 "$tmp/staged" "$tmp/live" | while IFS= read -r f; do
		rm -f "$LIVE/$f"
	done
	find "$LIVE" -mindepth 1 -type d -empty -not -path "$LIVE/.git/*" -delete 2>/dev/null || true
	find -P "$tmp" -depth -delete
	tmp=""

	# Verify: the live tree must now equal the staged set, byte for byte.
	local fail=0
	while IFS= read -r f; do
		cmp -s "$REPO/$f" "$LIVE/$f" || { echo "MISMATCH $f" >&2; fail=1; }
	done < <(sorted_list)
	local extra; extra="$(live_list | comm -13 <(sorted_list) -)"
	[ -z "$extra" ] || { echo "EXTRA FILES REMAIN:" >&2; echo "$extra" >&2; fail=1; }
	[ "$fail" -eq 0 ] || { echo "DEPLOY VERIFY FAILED" >&2; exit 4; }

	# Receipt: build beside its ignored destination, then atomically replace only an ordinary file.
	local receipt_path="$REPO/Tools/last-deploy-receipt.txt"
	if [ -L "$receipt_path" ] || { [ -e "$receipt_path" ] && [ ! -f "$receipt_path" ]; }; then
		echo "refusing unsafe deployment receipt path: $receipt_path" >&2
		exit 4
	fi
	receipt_tmp="$(mktemp "$REPO/Tools/.last-deploy-receipt.tmp.XXXXXX")"
	{
		echo "# Deploy receipt"
		echo "date:   $(date -Iseconds)"
		echo "repo:   $source_commit"
		echo "dirty:  $source_dirty worktree entries before deploy"
		echo "live:   $LIVE"
		echo "backup: $backup"
		echo "files:  $(sorted_list | wc -l)"
		echo
		cmd_manifest
	} > "$receipt_tmp"
	if [ -L "$receipt_path" ] || { [ -e "$receipt_path" ] && [ ! -f "$receipt_path" ]; }; then
		echo "deployment receipt path changed during deploy: $receipt_path" >&2
		exit 4
	fi
	mv -T -- "$receipt_tmp" "$receipt_path"
	receipt_tmp=""

	rollback_armed=0
	echo "DEPLOY OK — $(sorted_list | wc -l) files, receipt at Tools/last-deploy-receipt.txt"
)

case "${1:-}" in
	manifest) cmd_manifest ;;
	list)     cmd_list ;;
	list-head) cmd_list_head "${2:-HEAD}" ;;
	copy)     cmd_copy "${2:-}" ;;
	verify)   cmd_verify "${2:-}" ;;
	diff)     cmd_diff ;;
	deploy)   cmd_deploy "${2:-}" ;;
	*) sed -n '2,20p' "${BASH_SOURCE[0]}"; exit 2 ;;
esac
