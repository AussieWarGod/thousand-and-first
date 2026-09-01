#!/usr/bin/env bash
# Build one immutable, root-level directory for Qud's in-game Workshop uploader.
# This script never authenticates, creates a Workshop item, or uploads content.

set -euo pipefail

usage() {
	cat <<'EOF'
Usage:
  Tools/workshop-package.sh --copy
  Tools/workshop-package.sh [--test|--alpha|--release] DESTINATION

  --copy     print the canonical Qud Workshop fields; write nothing
  --test     build a private-test package (default); workshop.json may be absent
  --alpha    build a tagged public v1.0.x Alpha package; require public metadata,
             exact private-candidate binding, final preview, and structural review,
             but no final human release-evidence record
  --release  require an annotated v<version> tag at HEAD, public workshop.json,
             release evidence, the committed private-package receipt copy, and
             the exact-inventory structural release review

DESTINATION and its sibling DESTINATION.sha256 must not exist. Packaging accepts
only clean, regular staged files whose bytes are tracked by HEAD.
EOF
}

MODE="test"
case "${1:-}" in
	--copy) MODE="copy"; shift ;;
	--test) MODE="test"; shift ;;
	--alpha) MODE="alpha"; shift ;;
	--release) MODE="release"; shift ;;
	-h|--help) usage; exit 0 ;;
esac

REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd -P)"
METADATA="$REPO/Tools/workshop_metadata.py"

if [ "$MODE" = "copy" ]; then
	[ "$#" -eq 0 ] || { usage >&2; exit 2; }
	exec python3 "$METADATA" copy "$REPO/manifest.json"
fi

[ "$#" -eq 1 ] || { usage >&2; exit 2; }
DEST_INPUT="$1"
[ -n "$DEST_INPUT" ] || { echo "destination is empty" >&2; exit 2; }

# Resolve the nonexistent destination through one already-existing, physically ordinary
# parent. A symlink in any parent component would make lexical containment checks lie.
DEST="$(realpath -ms -- "$DEST_INPUT")"
PARENT="$(dirname -- "$DEST")"
BASENAME="$(basename -- "$DEST")"
[ "$DEST" != "/" ] || { echo "refusing broad package destination: /" >&2; exit 2; }
[ -d "$PARENT" ] || { echo "destination parent does not exist: $PARENT" >&2; exit 2; }
PARENT_REAL="$(realpath -e -- "$PARENT")"
[ "$PARENT" = "$PARENT_REAL" ] || {
	echo "refusing linked destination parent: $PARENT -> $PARENT_REAL" >&2; exit 2; }
case "$DEST" in
	"$REPO"|"$REPO"/*)
		echo "refusing package destination inside repository: $DEST" >&2; exit 2 ;;
esac
case "$REPO" in
	"$DEST"|"$DEST"/*)
		echo "refusing package destination containing repository: $DEST" >&2; exit 2 ;;
esac

RECEIPT="$DEST.sha256"
for absent in "$DEST" "$RECEIPT"; do
	if [ -e "$absent" ] || [ -L "$absent" ]; then
		echo "destination artifact already exists: $absent" >&2
		exit 2
	fi
done

cd "$REPO"
HEAD_COMMIT="$(git rev-parse --verify HEAD)"
require_clean_head() {
	local dirty current
	dirty="$(git status --porcelain=v1 --untracked-files=all)"
	[ -z "$dirty" ] || {
		echo "refusing to package a dirty worktree:" >&2
		printf '%s\n' "$dirty" >&2
		return 1
	}
	current="$(git rev-parse --verify HEAD)"
	[ "$current" = "$HEAD_COMMIT" ] || {
		echo "HEAD changed while packaging: $HEAD_COMMIT -> $current" >&2
		return 1
	}
}
require_clean_head

require_release_tag() {
	TAG_REF="refs/tags/v$VERSION"
	git show-ref --verify --quiet "$TAG_REF" || {
		echo "public package requires annotated tag v$VERSION" >&2; exit 3; }
	[ "$(git cat-file -t "$TAG_REF")" = "tag" ] || {
		echo "public tag v$VERSION must be annotated, not lightweight" >&2; exit 3; }
	[ "$(git rev-parse "$TAG_REF^{commit}")" = "$HEAD_COMMIT" ] || {
		echo "public package requires annotated tag v$VERSION at HEAD" >&2; exit 3; }
}

require_alpha_lineage() {
	[ "$VERSION" != "1.0.0" ] || return 0
	local first_ref="refs/tags/v1.0.0" first_commit first_version
	git show-ref --verify --quiet "$first_ref" || {
		echo "later v1.0 Alpha patch requires preserved annotated tag v1.0.0" >&2
		return 1
	}
	[ "$(git cat-file -t "$first_ref")" = "tag" ] || {
		echo "first Alpha tag v1.0.0 must be annotated, not lightweight" >&2
		return 1
	}
	first_commit="$(git rev-parse "$first_ref^{commit}")"
	[ "$first_commit" != "$HEAD_COMMIT" ] \
		&& git merge-base --is-ancestor "$first_commit" "$HEAD_COMMIT" || {
		echo "later v1.0 Alpha patch requires v1.0.0 on an earlier ancestor" >&2
		return 1
	}
	first_version="$(git show "$first_ref:manifest.json" | python3 -c \
		'import json, sys; print(json.load(sys.stdin).get("version", ""))')" || {
		echo "cannot read manifest version from first Alpha tag v1.0.0" >&2
		return 1
	}
	[ "$first_version" = "1.0.0" ] || {
		echo "first Alpha tag v1.0.0 does not bind manifest version 1.0.0" >&2
		return 1
	}
}

extract_head_blob() {
	local path="$1" output="$2" label="$3" mode_policy="$4"
	local entry metadata tracked_path mode kind blob
	entry="$(git -c core.quotePath=false ls-tree "$HEAD_COMMIT" -- ":(literal)$path")"
	[ -n "$entry" ] || {
		echo "public package requires $label in HEAD: $path" >&2; return 1; }
	metadata="${entry%%$'\t'*}"
	tracked_path="${entry#*$'\t'}"
	read -r mode kind blob <<< "$metadata"
	[ "$tracked_path" = "$path" ] && [ "$kind" = "blob" ] || {
		echo "$label is not an ordinary file in HEAD: $path" >&2; return 1; }
	case "$mode_policy:$mode" in
		ordinary:100644|ordinary:100755|nonexec:100644) ;;
		*) echo "$label has an unsafe Git mode in HEAD: $path ($mode)" >&2; return 1 ;;
	esac
	git cat-file blob "$blob" > "$output"
}

require_release_structure() {
	local path
	extract_head_blob "Tools/check-structure.py" "$STRUCTURE_GATE_FILE" \
		"structural release gate" ordinary
	extract_head_blob "docs/STRUCTURE_REVIEW.json" "$STRUCTURE_LEDGER_FILE" \
		"exact-inventory semantic review" nonexec
	: > "$STRUCTURE_INVENTORY_FILE"
	while IFS= read -r path; do
		case "$path" in *.cs) printf '%s\n' "$path" >> "$STRUCTURE_INVENTORY_FILE" ;; esac
	done < "$LIST_FILE"
	[ -s "$STRUCTURE_INVENTORY_FILE" ] || {
		echo "structural release inventory contains no production C#" >&2; return 1; }
	assert_scratch_workspace "before immutable structural release gate"
	PYTHONDONTWRITEBYTECODE=1 python3 "$STRUCTURE_GATE_FILE" \
		--repo-root "$BUILD_DIR" --inventory-file "$STRUCTURE_INVENTORY_FILE" \
		--review-ledger "$STRUCTURE_LEDGER_FILE" --release
	assert_scratch_workspace "after immutable structural release gate"
}

SCRATCH_DIR=""
SCRATCH_ID=""
SCRATCH_UID="$(id -u)"
BUILD_DIR=""
BUILD_ID=""
RECEIPT_TMP=""
LIST_FILE=""
CURRENT_LIST=""
PROOF_FILE=""
ACTUAL_LIST=""
EVIDENCE_FILE=""
ALPHA_CANDIDATE_FILE=""
CANDIDATE_RECEIPT_FILE=""
CANDIDATE_LIST=""
CANDIDATE_PROOF_FILE=""
CANDIDATE_MANIFEST=""
CANDIDATE_WORKSHOP=""
ARTIFACT_LIST=""
TESTING_FILE=""
EVIDENCE_ROOT=""
STRUCTURE_GATE_FILE=""
STRUCTURE_LEDGER_FILE=""
STRUCTURE_INVENTORY_FILE=""
PRIVATE_COMMIT=""
PUBLISHED_DEST_ID=""
PUBLISHED_RECEIPT_ID=""
RECEIPT_PAYLOAD_SHA=""
PUBLICATION_ACCEPTED=0
declare -a SCRATCH_FILES=()
declare -a SCRATCH_FILE_IDS=()

identity() { stat -Lc '%d:%i' -- "$1"; }

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

known = {directory_identity: path for path, directory_identity in walk(left)}
for path, directory_identity in walk(right):
    if directory_identity in known:
        print(path + " -> " + known[directory_identity])
        break
PY
}

ancestor_repository_alias() {
	python3 - "$1" "$2" <<'PY'
import os
import sys

repository, path = sys.argv[1:]
repository_directories = set()
for current, _directories, _files in os.walk(repository, topdown=True, followlinks=False):
    status = os.stat(current, follow_symlinks=False)
    repository_directories.add((status.st_dev, status.st_ino))

while True:
    status = os.stat(path, follow_symlinks=False)
    if (status.st_dev, status.st_ino) in repository_directories:
        print(path)
        break
    parent = os.path.dirname(path)
    if parent == path:
        break
    path = parent
PY
}

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

remove_owned_tree() {
	local path="$1" expected="$2" overlap boundary
	if [ -n "$expected" ] && [ -d "$path" ] && [ ! -L "$path" ] \
			&& [ "$(identity "$path" 2>/dev/null || true)" = "$expected" ]; then
		overlap="$(directory_overlap "$REPO" "$path" 2>/dev/null)" || return
		[ -z "$overlap" ] || {
			echo "refusing temporary cleanup through repository alias: $overlap" >&2; return; }
		boundary="$(mount_boundary "$path" 2>/dev/null)" || return
		[ -z "$boundary" ] || {
			echo "refusing temporary cleanup through mount boundary: $boundary" >&2; return; }
		find -P "$path" -depth -delete
	fi
}

remove_owned_scratch_tree() {
	local path="$1" expected="$2" overlap boundary
	[ -n "$path" ] && [ -n "$expected" ] || return 0
	case "$path" in
		"$PARENT/.${BASENAME}.scratch."*) ;;
		*) echo "refusing unexpected scratch cleanup path: $path" >&2; return 1 ;;
	esac
	[ -d "$PARENT" ] && [ ! -L "$PARENT" ] \
		&& [ "$(identity "$PARENT" 2>/dev/null || true)" = "$PARENT_ID" ] || {
		echo "refusing scratch cleanup through changed parent: $PARENT" >&2; return 1; }
	[ -d "$path" ] && [ ! -L "$path" ] \
		&& [ "$(identity "$path" 2>/dev/null || true)" = "$expected" ] \
		&& [ "$(stat -Lc '%a:%u' -- "$path" 2>/dev/null || true)" = "700:$SCRATCH_UID" ] || {
		[ ! -e "$path" ] && [ ! -L "$path" ] && return 0
		echo "refusing scratch cleanup through changed identity or mode: $path" >&2
		return 1
	}
	overlap="$(directory_overlap "$REPO" "$path" 2>/dev/null)" || return 1
	[ -z "$overlap" ] || {
		echo "refusing scratch cleanup through repository alias: $overlap" >&2; return 1; }
	boundary="$(mount_boundary "$path" 2>/dev/null)" || return 1
	[ -z "$boundary" ] || {
		echo "refusing scratch cleanup through mount boundary: $boundary" >&2; return 1; }
	find -P "$path" -xdev -depth -delete
}

remove_owned_file() {
	local path="$1" expected="$2"
	if [ -n "$expected" ] && [ -f "$path" ] && [ ! -L "$path" ] \
			&& [ "$(identity "$path" 2>/dev/null || true)" = "$expected" ]; then
		unlink -- "$path"
	fi
}

cleanup() {
	local status=$?
	trap - EXIT HUP INT TERM
	set +e
	if [ "$PUBLICATION_ACCEPTED" -eq 0 ]; then
		remove_owned_file "$RECEIPT" "$PUBLISHED_RECEIPT_ID"
		remove_owned_tree "$DEST" "$PUBLISHED_DEST_ID"
	fi
	remove_owned_scratch_tree "$SCRATCH_DIR" "$SCRATCH_ID"
	exit "$status"
}

assert_scratch_boundary() {
	local phase="$1" build_state="${2:-inside}" overlap boundary special
	[ -d "$PARENT" ] && [ ! -L "$PARENT" ] \
			&& [ "$(identity "$PARENT" 2>/dev/null || true)" = "$PARENT_ID" ] || {
		echo "destination parent identity changed $phase: $PARENT" >&2; return 1; }
	[ -d "$SCRATCH_DIR" ] && [ ! -L "$SCRATCH_DIR" ] \
			&& [ "$(identity "$SCRATCH_DIR" 2>/dev/null || true)" = "$SCRATCH_ID" ] \
			&& [ "$(dirname -- "$SCRATCH_DIR")" = "$PARENT" ] \
			&& [ "$(stat -Lc '%a:%u' -- "$SCRATCH_DIR" 2>/dev/null || true)" = \
				"700:$SCRATCH_UID" ] || {
		echo "private scratch identity or mode changed $phase: $SCRATCH_DIR" >&2; return 1; }
	overlap="$(directory_overlap "$REPO" "$SCRATCH_DIR")" || {
		echo "cannot prove temporary package separation $phase" >&2; return 1; }
	[ -z "$overlap" ] || {
		echo "temporary package aliases repository $phase: $overlap" >&2; return 1; }
	boundary="$(mount_boundary "$SCRATCH_DIR")" || {
		echo "cannot prove temporary package mount boundary $phase" >&2; return 1; }
	[ -z "$boundary" ] || {
		echo "temporary package contains a mount boundary $phase: $boundary" >&2; return 1; }
	special="$(find -P "$SCRATCH_DIR" -xdev -mindepth 1 ! -type f ! -type d -print -quit)" || {
		echo "cannot inspect private scratch file types $phase" >&2; return 1; }
	[ -z "$special" ] || {
		echo "private scratch contains a link or special file $phase: $special" >&2; return 1; }
	case "$build_state" in
		inside)
			[ -d "$BUILD_DIR" ] && [ ! -L "$BUILD_DIR" ] \
					&& [ "$(identity "$BUILD_DIR" 2>/dev/null || true)" = "$BUILD_ID" ] || {
				echo "temporary package identity changed $phase: $BUILD_DIR" >&2; return 1; } ;;
		published)
			[ ! -e "$BUILD_DIR" ] && [ ! -L "$BUILD_DIR" ] \
					&& [ -d "$DEST" ] && [ ! -L "$DEST" ] \
					&& [ "$(identity "$DEST" 2>/dev/null || true)" = "$BUILD_ID" ] || {
				echo "published package identity changed $phase: $DEST" >&2; return 1; } ;;
		*) echo "invalid scratch build state: $build_state" >&2; return 1 ;;
	esac
}

register_scratch_file() {
	local path="$1"
	[ -f "$path" ] && [ ! -L "$path" ] \
		&& [ "$(stat -Lc '%a:%u:%h' -- "$path")" = "600:$SCRATCH_UID:1" ] || {
		echo "cannot bind private scratch file: $path" >&2; return 1; }
	SCRATCH_FILES+=("$path")
	SCRATCH_FILE_IDS+=("$(identity "$path")")
}

new_scratch_file() {
	local variable="$1" stem="$2" path
	path="$(mktemp -- "$SCRATCH_DIR/$stem.XXXXXX")"
	chmod 600 -- "$path"
	register_scratch_file "$path"
	printf -v "$variable" '%s' "$path"
}

forget_scratch_file() {
	local wanted="$1" index
	for index in "${!SCRATCH_FILES[@]}"; do
		[ "${SCRATCH_FILES[$index]}" != "$wanted" ] || {
			SCRATCH_FILES[$index]=""
			SCRATCH_FILE_IDS[$index]=""
			return 0
		}
	done
	return 1
}

assert_scratch_files() {
	local phase="$1" index path expected
	for index in "${!SCRATCH_FILES[@]}"; do
		path="${SCRATCH_FILES[$index]}"
		[ -n "$path" ] || continue
		expected="${SCRATCH_FILE_IDS[$index]}"
		[ -f "$path" ] && [ ! -L "$path" ] \
				&& [ "$(identity "$path" 2>/dev/null || true)" = "$expected" ] \
				&& [ "$(stat -Lc '%a:%u:%h' -- "$path" 2>/dev/null || true)" = \
					"600:$SCRATCH_UID:1" ] || {
			echo "private scratch file identity changed $phase: $path" >&2
			return 1
		}
	done
}

assert_scratch_workspace() {
	local phase="$1" build_state="${2:-inside}"
	assert_scratch_boundary "$phase" "$build_state"
	assert_scratch_files "$phase"
}

assert_published_state() {
	local special expected_permissions
	assert_scratch_workspace "before post-publication validation" published
	[ -d "$PARENT" ] && [ ! -L "$PARENT" ] \
		&& [ "$(identity "$PARENT" 2>/dev/null || true)" = "$PARENT_ID" ] || {
		echo "destination parent identity changed after receipt publication: $PARENT" >&2
		return 1
	}
	[ -d "$DEST" ] && [ ! -L "$DEST" ] \
		&& [ "$(identity "$DEST" 2>/dev/null || true)" = "$BUILD_ID" ] || {
		echo "published package identity changed after receipt publication: $DEST" >&2
		return 1
	}
	[ -f "$RECEIPT" ] && [ ! -L "$RECEIPT" ] \
		&& [ "$(identity "$RECEIPT" 2>/dev/null || true)" = "$RECEIPT_ID" ] || {
		echo "published receipt identity changed after receipt publication: $RECEIPT" >&2
		return 1
	}
	[ "$(sha256sum "$RECEIPT" | cut -d' ' -f1)" = "$RECEIPT_PAYLOAD_SHA" ] || {
		echo "published receipt bytes changed after receipt publication: $RECEIPT" >&2
		return 1
	}
	special="$(find -P "$DEST" -mindepth 1 ! -type f ! -type d -print -quit)" || {
		echo "cannot inspect published package file types: $DEST" >&2
		return 1
	}
	[ -z "$special" ] || {
		echo "published package contains a link or special file: ${special#"$DEST/"}" >&2
		return 1
	}
	assert_scratch_workspace "before published inventory capture" published
	(
		cd "$DEST"
		find -P . -type f -printf '%P\n' | LC_ALL=C sort
	) > "$ACTUAL_LIST" || {
		echo "cannot inspect published package inventory: $DEST" >&2
		return 1
	}
	assert_scratch_workspace "after published inventory capture" published
	cmp -s "$LIST_FILE" "$ACTUAL_LIST" || {
		echo "published package inventory changed after receipt publication" >&2
		return 1
	}
	while IFS=$'\t' read -r path head_blob head_mode; do
		[ -f "$DEST/$path" ] && [ ! -L "$DEST/$path" ] || {
			echo "published package file type changed after receipt publication: $path" >&2
			return 1
		}
		[ "$(git hash-object --no-filters -- "$DEST/$path")" = "$head_blob" ] || {
			echo "published package bytes changed after receipt publication: $path" >&2
			return 1
		}
		expected_permissions=644
		[ "$head_mode" != "100755" ] || expected_permissions=755
		[ "$(stat -Lc '%a' -- "$DEST/$path")" = "$expected_permissions" ] || {
			echo "published package mode changed after receipt publication: $path" >&2
			return 1
		}
	done < "$PROOF_FILE"
	assert_scratch_workspace "after published blob proof" published
	(
		cd "$DEST"
		sha256sum -c "$RECEIPT" >/dev/null
	) || {
		echo "published receipt hashes do not match package bytes" >&2
		return 1
	}
	# Close checks with stable ownership and receipt bytes. A racing replacement after either
	# content pass must not make cleanup ownership transfer to an unproved path.
	[ -d "$PARENT" ] && [ ! -L "$PARENT" ] \
		&& [ "$(identity "$PARENT" 2>/dev/null || true)" = "$PARENT_ID" ] \
		&& [ -d "$DEST" ] && [ ! -L "$DEST" ] \
		&& [ "$(identity "$DEST" 2>/dev/null || true)" = "$BUILD_ID" ] || {
		echo "published package boundary changed during post-publication validation" >&2
		return 1
	}
	[ -f "$RECEIPT" ] && [ ! -L "$RECEIPT" ] \
		&& [ "$(identity "$RECEIPT" 2>/dev/null || true)" = "$RECEIPT_ID" ] \
		&& [ "$(sha256sum "$RECEIPT" | cut -d' ' -f1)" = "$RECEIPT_PAYLOAD_SHA" ] || {
		echo "published receipt changed during post-publication validation: $RECEIPT" >&2
		return 1
	}
}

PARENT_ID="$(identity "$PARENT")"
UNSAFE_PARENT_ANCESTOR="$(python3 - "$PARENT" <<'PY'
import os
import stat
import sys

path = os.path.abspath(sys.argv[1])
allowed_owners = {os.getuid(), 0}
while True:
    status = os.stat(path, follow_symlinks=False)
    if not stat.S_ISDIR(status.st_mode):
        raise RuntimeError(f"destination ancestor is not an ordinary directory: {path}")
    shared_writable = bool(status.st_mode & (stat.S_IWGRP | stat.S_IWOTH))
    sticky = bool(status.st_mode & stat.S_ISVTX)
    owner_is_trusted = status.st_uid in allowed_owners
    if not owner_is_trusted:
        print(
            f"{path} (owner uid {status.st_uid} is neither current uid "
            f"{os.getuid()} nor root uid 0)"
        )
        break
    if shared_writable and not sticky:
        print(f"{path} (group/world-writable without sticky-bit protection)")
        break
    parent = os.path.dirname(path)
    if parent == path:
        break
    path = parent
PY
)" || {
	echo "cannot inspect destination ancestor protection: $PARENT" >&2; exit 2; }
[ -z "$UNSAFE_PARENT_ANCESTOR" ] || {
	echo "unsafe destination ancestor for scratch names: $UNSAFE_PARENT_ANCESTOR" >&2
	echo "every ancestor requires owner uid $SCRATCH_UID or 0; shared-writable ancestors also require sticky" >&2
	exit 2
}
PARENT_ALIAS="$(ancestor_repository_alias "$REPO" "$PARENT")" || {
	echo "cannot prove destination parent separation: $PARENT" >&2; exit 2; }
[ -z "$PARENT_ALIAS" ] || {
	echo "destination parent aliases repository: $PARENT_ALIAS" >&2; exit 2; }
umask 077
trap cleanup EXIT
trap 'exit 129' HUP
trap 'exit 130' INT
trap 'exit 143' TERM
SCRATCH_DIR="$(mktemp -d -- "$PARENT/.${BASENAME}.scratch.XXXXXX")"
chmod 700 -- "$SCRATCH_DIR"
SCRATCH_ID="$(identity "$SCRATCH_DIR")"
BUILD_DIR="$SCRATCH_DIR/package"
mkdir -- "$BUILD_DIR"
chmod 755 -- "$BUILD_DIR"
BUILD_ID="$(identity "$BUILD_DIR")"
assert_scratch_boundary "immediately after creation"
new_scratch_file RECEIPT_TMP receipt
new_scratch_file LIST_FILE list
new_scratch_file CURRENT_LIST current-list
new_scratch_file PROOF_FILE proof
new_scratch_file ACTUAL_LIST actual-list
if [ "$MODE" = "alpha" ] || [ "$MODE" = "release" ]; then
	new_scratch_file STRUCTURE_GATE_FILE structure-gate
	new_scratch_file STRUCTURE_LEDGER_FILE structure-ledger
	new_scratch_file STRUCTURE_INVENTORY_FILE structure-inventory
	new_scratch_file CANDIDATE_RECEIPT_FILE candidate-receipt
	new_scratch_file CANDIDATE_LIST candidate-list
	new_scratch_file CANDIDATE_PROOF_FILE candidate-proof
	new_scratch_file CANDIDATE_MANIFEST candidate-manifest
	new_scratch_file CANDIDATE_WORKSHOP candidate-workshop
fi
if [ "$MODE" = "alpha" ]; then
	new_scratch_file ALPHA_CANDIDATE_FILE alpha-candidate
fi
if [ "$MODE" = "release" ]; then
	new_scratch_file EVIDENCE_FILE evidence
	new_scratch_file ARTIFACT_LIST artifact-list
	new_scratch_file TESTING_FILE testing
	EVIDENCE_ROOT="$SCRATCH_DIR/evidence-root"
	mkdir -- "$EVIDENCE_ROOT"
	chmod 700 -- "$EVIDENCE_ROOT"
fi
assert_scratch_workspace "after private workspace creation"

require_candidate_binding() {
	local expected_receipt_sha="$1"
	local path candidate_sha candidate_entry head_entry candidate_metadata head_metadata
	local candidate_path head_path candidate_mode candidate_kind candidate_blob
	local head_mode head_kind head_blob candidate_blob_sha
	local receipt_path candidate_receipt_entry candidate_receipt_metadata
	local candidate_receipt_path candidate_receipt_mode candidate_receipt_kind candidate_receipt_blob
	local head_receipt_entry head_receipt_metadata head_receipt_path head_receipt_mode
	local head_receipt_kind head_receipt_blob
	local candidate_testing_entry candidate_testing_metadata candidate_testing_path
	local candidate_testing_mode candidate_testing_kind candidate_testing_blob
	local head_testing_entry head_testing_metadata head_testing_path head_testing_mode
	local head_testing_kind head_testing_blob
	assert_scratch_workspace "before candidate receipt binding"
	path="TESTING.md"
	candidate_testing_entry="$(git -c core.quotePath=false ls-tree "$PRIVATE_COMMIT" -- \
		":(literal)$path")"
	head_testing_entry="$(git -c core.quotePath=false ls-tree "$HEAD_COMMIT" -- \
		":(literal)$path")"
	[ -n "$candidate_testing_entry" ] && [ -n "$head_testing_entry" ] || {
		echo "candidate and release must both contain TESTING.md" >&2; return 1; }
	candidate_testing_metadata="${candidate_testing_entry%%$'\t'*}"
	candidate_testing_path="${candidate_testing_entry#*$'\t'}"
	head_testing_metadata="${head_testing_entry%%$'\t'*}"
	head_testing_path="${head_testing_entry#*$'\t'}"
	read -r candidate_testing_mode candidate_testing_kind candidate_testing_blob \
		<<< "$candidate_testing_metadata"
	read -r head_testing_mode head_testing_kind head_testing_blob <<< "$head_testing_metadata"
	[ "$candidate_testing_path" = "$path" ] && [ "$candidate_testing_kind" = "blob" ] \
		&& [ "$candidate_testing_mode" = "100644" ] || {
		echo "private candidate TESTING.md is not an ordinary non-executable file" >&2
		return 1
	}
	[ "$head_testing_path" = "$path" ] && [ "$head_testing_kind" = "blob" ] \
		&& [ "$head_testing_mode" = "100644" ] || {
		echo "release TESTING.md is not an ordinary non-executable file" >&2
		return 1
	}
	[ "$candidate_testing_blob" = "$head_testing_blob" ] || {
		echo "release TESTING.md differs from subscribed private candidate" >&2
		return 1
	}
	receipt_path="docs/PRIVATE_PACKAGE_RECEIPT.sha256"
	candidate_receipt_entry="$(git -c core.quotePath=false ls-tree "$PRIVATE_COMMIT" -- \
		":(literal)$receipt_path")"
	[ -n "$candidate_receipt_entry" ] || {
		echo "private candidate commit does not contain $receipt_path" >&2; return 1; }
	candidate_receipt_metadata="${candidate_receipt_entry%%$'\t'*}"
	candidate_receipt_path="${candidate_receipt_entry#*$'\t'}"
	read -r candidate_receipt_mode candidate_receipt_kind candidate_receipt_blob \
		<<< "$candidate_receipt_metadata"
	[ "$candidate_receipt_path" = "$receipt_path" ] \
		&& [ "$candidate_receipt_kind" = "blob" ] \
		&& [ "$candidate_receipt_mode" = "100644" ] || {
		echo "private candidate receipt is not an ordinary non-executable file: $receipt_path" >&2
		return 1
	}
	head_receipt_entry="$(git -c core.quotePath=false ls-tree "$HEAD_COMMIT" -- \
		":(literal)$receipt_path")"
	[ -n "$head_receipt_entry" ] || {
		echo "release package requires $receipt_path in HEAD" >&2; return 1; }
	head_receipt_metadata="${head_receipt_entry%%$'\t'*}"
	head_receipt_path="${head_receipt_entry#*$'\t'}"
	read -r head_receipt_mode head_receipt_kind head_receipt_blob <<< "$head_receipt_metadata"
	[ "$head_receipt_path" = "$receipt_path" ] && [ "$head_receipt_kind" = "blob" ] \
		&& [ "$head_receipt_mode" = "100644" ] || {
		echo "release receipt is not an ordinary non-executable file: $receipt_path" >&2
		return 1
	}
	[ "$head_receipt_mode" = "$candidate_receipt_mode" ] \
		&& [ "$head_receipt_blob" = "$candidate_receipt_blob" ] || {
		echo "release HEAD private package receipt differs from candidate commit" >&2
		return 1
	}
	assert_scratch_workspace "before candidate receipt extraction"
	git cat-file blob "$candidate_receipt_blob" > "$CANDIDATE_RECEIPT_FILE"
	assert_scratch_workspace "after candidate receipt extraction"
	[ "$(sha256sum "$CANDIDATE_RECEIPT_FILE" | cut -d' ' -f1)" = \
		"$expected_receipt_sha" ] || {
		echo "committed private package receipt does not match public candidate record" >&2
		return 1
	}
	assert_scratch_workspace "before candidate receipt parsing"
	python3 - "$CANDIDATE_RECEIPT_FILE" "$CANDIDATE_LIST" "$CANDIDATE_PROOF_FILE" <<'PY'
import posixpath
import re
import sys
import unicodedata
from pathlib import Path

receipt_path, list_path, proof_path = map(Path, sys.argv[1:])
try:
    payload = receipt_path.read_bytes()
except OSError as error:
    raise SystemExit(f"cannot read private package receipt: {error}")
if not payload or not payload.endswith(b"\n") or b"\r" in payload:
    raise SystemExit("private package receipt must be nonempty LF-terminated text")
try:
    lines = payload[:-1].decode("utf-8", "strict").split("\n")
except UnicodeDecodeError:
    raise SystemExit("private package receipt is not UTF-8")

record = re.compile(r"([0-9a-f]{64})  \./(.+)")
reserved = re.compile(
    r"^(?:CON|PRN|AUX|NUL|CONIN\$|CONOUT\$|"
    r"COM[1-9\u00b9\u00b2\u00b3]|LPT[1-9\u00b9\u00b2\u00b3])$",
    re.IGNORECASE,
)
entries = []
windows_seen = {}
for line in lines:
    match = record.fullmatch(line)
    if match is None:
        raise SystemExit("private package receipt contains a malformed record")
    digest, path = match.groups()
    components = path.split("/")
    if (not path or path.startswith("/") or posixpath.normpath(path) != path
            or any(component in ("", ".", "..") for component in components)
            or any(character == "\\" or unicodedata.category(character).startswith("C")
                   for character in path)):
        raise SystemExit(f"private package receipt contains an unsafe path: {path!r}")
    for component in components:
        if (any(character in '<>:"|?*' for character in component)
                or component.endswith((".", " "))
                or reserved.fullmatch(component.rstrip(". ").split(".", 1)[0])):
            raise SystemExit(
                f"private package receipt contains a Windows-incompatible path: {path!r}"
            )
    windows_key = path.casefold()
    if windows_key in windows_seen:
        raise SystemExit(
            "private package receipt contains a Windows case-fold collision: "
            f"{windows_seen[windows_key]!r} and {path!r}"
        )
    windows_seen[windows_key] = path
    entries.append((path.encode("utf-8"), path, digest))
if entries != sorted(entries):
    raise SystemExit("private package receipt inventory is not bytewise path-sorted")

list_path.write_bytes(b"".join(encoded + b"\n" for encoded, _path, _digest in entries))
proof_path.write_text(
    "".join(f"{path}\t{digest}\n" for _encoded, path, digest in entries),
    encoding="utf-8",
)
PY
	assert_scratch_workspace "after candidate receipt parsing"
	cmp -s "$CANDIDATE_LIST" "$LIST_FILE" || {
		echo "release stage inventory differs from subscribed private package receipt" >&2
		return 1
	}
	while IFS=$'\t' read -r path candidate_sha; do
		candidate_entry="$(git -c core.quotePath=false ls-tree "$PRIVATE_COMMIT" -- \
			":(literal)$path")"
		head_entry="$(git -c core.quotePath=false ls-tree "$HEAD_COMMIT" -- \
			":(literal)$path")"
		[ -n "$candidate_entry" ] && [ -n "$head_entry" ] || {
			echo "private package receipt path is absent from candidate or release: $path" >&2
			return 1
		}
		candidate_metadata="${candidate_entry%%$'\t'*}"
		candidate_path="${candidate_entry#*$'\t'}"
		head_metadata="${head_entry%%$'\t'*}"
		head_path="${head_entry#*$'\t'}"
		read -r candidate_mode candidate_kind candidate_blob <<< "$candidate_metadata"
		read -r head_mode head_kind head_blob <<< "$head_metadata"
		[ "$candidate_path" = "$path" ] && [ "$candidate_kind" = "blob" ] \
			&& { [ "$candidate_mode" = "100644" ] || [ "$candidate_mode" = "100755" ]; } || {
			echo "private package receipt path is not an ordinary candidate file: $path" >&2
			return 1
		}
		[ "$head_path" = "$path" ] && [ "$head_kind" = "blob" ] \
			&& { [ "$head_mode" = "100644" ] || [ "$head_mode" = "100755" ]; } || {
			echo "private package receipt path is not an ordinary release file: $path" >&2
			return 1
		}
		candidate_blob_sha="$(git cat-file blob "$candidate_blob" | sha256sum | cut -d' ' -f1)"
		[ "$candidate_blob_sha" = "$candidate_sha" ] || {
			echo "private package receipt differs from candidate commit: $path" >&2
			return 1
		}
		[ "$candidate_mode" = "$head_mode" ] || {
			echo "release runtime mode differs from subscribed private candidate: $path" >&2
			return 1
		}
		case "$path" in
			README.md|CHANGELOG.md|workshop.json) ;;
			*) [ "$candidate_blob" = "$head_blob" ] || {
				echo "release runtime differs from subscribed private candidate: $path" >&2
				return 1
			} ;;
		esac
	done < "$CANDIDATE_PROOF_FILE"
	assert_scratch_workspace "after candidate blob proof"
	assert_scratch_workspace "before candidate manifest extraction"
	git cat-file blob "$PRIVATE_COMMIT:manifest.json" > "$CANDIDATE_MANIFEST"
	assert_scratch_workspace "after candidate manifest extraction"
	git cat-file blob "$PRIVATE_COMMIT:workshop.json" > "$CANDIDATE_WORKSHOP"
	assert_scratch_workspace "after candidate Workshop extraction"
	python3 "$METADATA" workshop test "$CANDIDATE_MANIFEST" "$CANDIDATE_WORKSHOP"
	[ "$(python3 "$METADATA" workshop-id "$CANDIDATE_WORKSHOP")" = \
		"$(python3 "$METADATA" workshop-id "$BUILD_DIR/workshop.json")" ] || {
		echo "release Workshop ID differs from subscribed private candidate" >&2; return 1; }
	assert_scratch_workspace "after candidate metadata validation"
}

assert_scratch_workspace "before committed inventory capture"
./Tools/stage.sh list-head "$HEAD_COMMIT" > "$LIST_FILE"
assert_scratch_workspace "after committed inventory capture"
./Tools/stage.sh list > "$CURRENT_LIST"
assert_scratch_workspace "after worktree inventory capture"
[ -s "$LIST_FILE" ] || { echo "runtime stage is empty" >&2; exit 4; }
[ -s "$CURRENT_LIST" ] || { echo "worktree runtime stage is empty" >&2; exit 4; }
[ "$(LC_ALL=C sort -u "$CURRENT_LIST" | wc -l)" -eq "$(wc -l < "$CURRENT_LIST")" ] || {
	echo "worktree runtime stage contains duplicate paths" >&2; exit 4; }
[ "$(LC_ALL=C sort -u "$LIST_FILE" | wc -l)" -eq "$(wc -l < "$LIST_FILE")" ] || {
	echo "runtime stage contains duplicate paths" >&2; exit 4; }
cmp -s "$LIST_FILE" "$CURRENT_LIST" || {
	echo "worktree runtime inventory differs from HEAD" >&2; exit 4; }
assert_scratch_workspace "before committed source proof"

# A clean status is insufficient: ignored files can still match stage.sh's .cs/.xml selection.
# Bind every selected source byte to one ordinary blob in the commit being packaged, then
# materialise from those blobs rather than rediscovering a mutable worktree.
while IFS= read -r path; do
	case "$path" in
		""|/*|./*|../*|*/../*|*/..|*//*|*$'\t'*|*$'\r'*)
			echo "unsafe runtime stage path: $path" >&2; exit 4 ;;
	esac
	[ -f "$path" ] && [ ! -L "$path" ] || {
		echo "staged source is not a regular non-link file: $path" >&2; exit 4; }
	[ "$(git cat-file -t "$HEAD_COMMIT:$path" 2>/dev/null || true)" = "blob" ] || {
		echo "staged source is not a blob in HEAD: $path" >&2; exit 4; }
	head_blob="$(git rev-parse "$HEAD_COMMIT:$path")"
	entry="$(git ls-tree "$HEAD_COMMIT" -- "$path")"
	metadata="${entry%%$'\t'*}"
	tracked_path="${entry#*$'\t'}"
	read -r head_mode head_kind tree_blob <<< "$metadata"
	[ "$tracked_path" = "$path" ] && [ "$head_kind" = "blob" ] \
		&& [ "$tree_blob" = "$head_blob" ] \
		&& { [ "$head_mode" = "100644" ] || [ "$head_mode" = "100755" ]; } || {
		echo "staged source is not an ordinary committed file: $path" >&2; exit 4; }
	worktree_blob="$(git hash-object --no-filters -- "$path")"
	[ "$head_blob" = "$worktree_blob" ] || {
		echo "staged source bytes differ from HEAD: $path" >&2; exit 4; }
	printf '%s\t%s\t%s\n' "$path" "$head_blob" "$head_mode" >> "$PROOF_FILE"
done < "$LIST_FILE"
assert_scratch_workspace "after committed source proof"

while IFS=$'\t' read -r path head_blob head_mode; do
	mkdir -p -- "$BUILD_DIR/$(dirname -- "$path")"
	git cat-file blob "$head_blob" > "$BUILD_DIR/$path"
	if [ "$head_mode" = "100755" ]; then
		chmod 755 -- "$BUILD_DIR/$path"
	else
		chmod 644 -- "$BUILD_DIR/$path"
	fi
done < "$PROOF_FILE"
assert_scratch_workspace "after package materialisation"

assert_scratch_workspace "before materialised inventory capture"
(
	cd "$BUILD_DIR"
	find -P . -type f -printf '%P\n' | LC_ALL=C sort
) > "$ACTUAL_LIST"
assert_scratch_workspace "after materialised inventory capture"
cmp -s "$LIST_FILE" "$ACTUAL_LIST" || {
	echo "materialised package inventory differs from committed stage" >&2; exit 4; }
while IFS=$'\t' read -r path head_blob _head_mode; do
	[ "$(git hash-object --no-filters -- "$BUILD_DIR/$path")" = "$head_blob" ] || {
		echo "materialised package bytes differ from HEAD: $path" >&2; exit 4; }
done < "$PROOF_FILE"
assert_scratch_workspace "after materialised blob proof"

[ "$MODE" = "test" ] || require_release_structure

for forbidden in .git .github _notes DevTests Art docs Tools; do
	[ ! -e "$BUILD_DIR/$forbidden" ] && [ ! -L "$BUILD_DIR/$forbidden" ] || {
		echo "forbidden package path: $forbidden" >&2; exit 4; }
done
SPECIAL="$(find -P "$BUILD_DIR" -mindepth 1 ! -type f ! -type d -print -quit)"
[ -z "$SPECIAL" ] || {
	echo "package contains a link or special file: ${SPECIAL#"$BUILD_DIR/"}" >&2; exit 4; }
FORBIDDEN_FILE="$(find -P "$BUILD_DIR" -type f \( \
	-iname '*.dll' -o -iname '*.pdb' -o -iname '*.mdb' -o -iname '*.csproj' \
	-o -iname '*.sln' -o -iname '*.ps1' -o -iname '*.sh' -o -iname '*.py' \
	-o -iname '*.pyc' -o -iname '*.log' -o -iname '*.sav' \
	\) -print -quit)"
[ -z "$FORBIDDEN_FILE" ] || {
	echo "forbidden package file: ${FORBIDDEN_FILE#"$BUILD_DIR/"}" >&2; exit 4; }

PYTHONDONTWRITEBYTECODE=1 python3 - "$BUILD_DIR" <<'PY'
import os
import sys

from Art import check_wiring

build = os.path.abspath(sys.argv[1])
records, problems = check_wiring.runtime_asset_records()
if problems:
    raise SystemExit("runtime asset provenance failed: " + "; ".join(problems))
allowed = {"preview.png"}
allowed.update(row["path"] for row in records.values())
extensions = check_wiring.RASTER_EXTENSIONS
found = set()
for root, _dirs, files in os.walk(build):
    for name in files:
        path = os.path.join(root, name)
        relative = os.path.relpath(path, build).replace(os.sep, "/")
        if name.lower().endswith(extensions):
            found.add(relative)
extras = sorted(found - allowed, key=str.casefold)
missing = sorted((allowed - {"preview.png"}) - found, key=str.casefold)
if extras:
    raise SystemExit("forbidden runtime raster: " + extras[0])
if missing:
    raise SystemExit("allowlisted runtime raster missing from package: " + missing[0])
PY

# Metadata is authoritative only after HEAD blobs have been materialised. Never accept a mutable
# worktree manifest, preview, Workshop record, release claim, or evidence file as release proof.
MANIFEST_OUTPUT="$(python3 "$METADATA" fields "$BUILD_DIR/manifest.json")"
readarray -t MANIFEST_FIELDS <<< "$MANIFEST_OUTPUT"
[ "${#MANIFEST_FIELDS[@]}" -eq 3 ] || {
	echo "manifest metadata helper returned an incomplete result" >&2; exit 3; }
VERSION="${MANIFEST_FIELDS[0]}"
TITLE="${MANIFEST_FIELDS[1]}"
PREVIEW="${MANIFEST_FIELDS[2]}"
python3 "$METADATA" preview "$BUILD_DIR/$PREVIEW"
python3 "$METADATA" workshop "$MODE" "$BUILD_DIR/manifest.json" \
	"$BUILD_DIR/workshop.json"
assert_scratch_workspace "after package metadata validation"

if [ "$MODE" = "alpha" ]; then
	alpha_path="docs/ALPHA_CANDIDATE.json"
	assert_scratch_workspace "before Alpha candidate extraction"
	extract_head_blob "$alpha_path" "$ALPHA_CANDIDATE_FILE" \
		"Alpha candidate record" nonexec
	assert_scratch_workspace "after Alpha candidate extraction"
	ALPHA_OUTPUT="$(python3 "$METADATA" alpha-candidate \
		"$BUILD_DIR/manifest.json" "$BUILD_DIR/$PREVIEW" \
		"$BUILD_DIR/workshop.json" "$ALPHA_CANDIDATE_FILE" \
		"$BUILD_DIR/README.md" "$BUILD_DIR/CHANGELOG.md")"
	readarray -t ALPHA_FIELDS <<< "$ALPHA_OUTPUT"
	[ "${#ALPHA_FIELDS[@]}" -eq 2 ] || {
		echo "Alpha metadata helper returned an incomplete result" >&2; exit 3; }
	PRIVATE_COMMIT="${ALPHA_FIELDS[0]}"
	CANDIDATE_RECEIPT_SHA="${ALPHA_FIELDS[1]}"
	assert_scratch_workspace "after Alpha candidate validation"
	require_alpha_lineage
	[ "$PRIVATE_COMMIT" != "$HEAD_COMMIT" ] \
		&& git cat-file -e "$PRIVATE_COMMIT^{commit}" 2>/dev/null \
		&& git merge-base --is-ancestor "$PRIVATE_COMMIT" "$HEAD_COMMIT" || {
		echo "Alpha candidateCommit must be a private-candidate ancestor of HEAD" >&2
		exit 3
	}
	require_candidate_binding "$CANDIDATE_RECEIPT_SHA"
	require_release_tag
fi

if [ "$MODE" = "release" ]; then
	evidence_path="docs/RELEASE_EVIDENCE.json"
	assert_scratch_workspace "before release evidence extraction"
	extract_head_blob "$evidence_path" "$EVIDENCE_FILE" "release evidence" nonexec
	extract_head_blob "TESTING.md" "$TESTING_FILE" "authoritative numbered protocol" nonexec
	assert_scratch_workspace "after release evidence extraction"
	python3 "$METADATA" evidence-artifact-refs "$EVIDENCE_FILE" > "$ARTIFACT_LIST"
	assert_scratch_workspace "after release artifact inventory"
	while IFS= read -r artifact_path; do
		artifact_entry="$(git -c core.quotePath=false ls-tree "$HEAD_COMMIT" -- \
			":(literal)$artifact_path")"
		[ -n "$artifact_entry" ] || {
			echo "release evidence artifact is absent from HEAD: $artifact_path" >&2; exit 3; }
		artifact_metadata="${artifact_entry%%$'\t'*}"
		artifact_tracked_path="${artifact_entry#*$'\t'}"
		read -r artifact_mode artifact_kind artifact_blob <<< "$artifact_metadata"
		[ "$artifact_tracked_path" = "$artifact_path" ] \
			&& [ "$artifact_kind" = "blob" ] && [ "$artifact_mode" = "100644" ] || {
			echo "release evidence artifact is not an ordinary non-executable HEAD blob: $artifact_path" >&2
			exit 3
		}
		[ "$(git cat-file -s "$artifact_blob")" -le 536870912 ] || {
			echo "release evidence artifact exceeds the evidence size cap: $artifact_path" >&2
			exit 3
		}
		artifact_output="$EVIDENCE_ROOT/$artifact_path"
		mkdir -p -- "$(dirname -- "$artifact_output")"
		git cat-file blob "$artifact_blob" > "$artifact_output"
		chmod 600 -- "$artifact_output"
	done < "$ARTIFACT_LIST"
	assert_scratch_workspace "after immutable release artifact extraction"
	PRIVATE_COMMIT="$(python3 "$METADATA" evidence "$BUILD_DIR/manifest.json" \
		"$BUILD_DIR/$PREVIEW" "$BUILD_DIR/workshop.json" "$EVIDENCE_FILE" \
		"$BUILD_DIR/README.md" "$BUILD_DIR/CHANGELOG.md" \
		--repository-root "$EVIDENCE_ROOT" --testing "$TESTING_FILE")"
	assert_scratch_workspace "after release evidence validation"
	[ "$PRIVATE_COMMIT" != "$HEAD_COMMIT" ] \
		&& git cat-file -e "$PRIVATE_COMMIT^{commit}" 2>/dev/null \
		&& git merge-base --is-ancestor "$PRIVATE_COMMIT" "$HEAD_COMMIT" || {
		echo "release evidence candidateCommit must be a pre-evidence ancestor of HEAD" >&2
		exit 3
	}
	CANDIDATE_RECEIPT_SHA="$(python3 - "$EVIDENCE_FILE" <<'PY'
import json
import sys
from pathlib import Path

print(json.loads(Path(sys.argv[1]).read_text(encoding="utf-8"))["privatePackageReceiptSha256"])
PY
)"
	require_candidate_binding "$CANDIDATE_RECEIPT_SHA"
	require_release_tag
fi

assert_scratch_workspace "before package receipt capture"
(
	cd "$BUILD_DIR"
	find -P . -type f -print0 | LC_ALL=C sort -z | xargs -0 sha256sum
) > "$RECEIPT_TMP"
assert_scratch_workspace "after package receipt capture"
[ -s "$RECEIPT_TMP" ] || { echo "package receipt is empty" >&2; exit 4; }
require_clean_head
[ "$MODE" = "test" ] || require_release_tag

# Rebuild the inventory and mode/blob proof after all validators and immediately before the
# physical boundary check. This catches any late change inside the private temporary directory.
assert_scratch_workspace "before final inventory capture"
(
	cd "$BUILD_DIR"
	find -P . -type f -printf '%P\n' | LC_ALL=C sort
) > "$ACTUAL_LIST"
assert_scratch_workspace "after final inventory capture"
cmp -s "$LIST_FILE" "$ACTUAL_LIST" || {
	echo "temporary package inventory changed before publication" >&2; exit 4; }
while IFS=$'\t' read -r path head_blob head_mode; do
	[ -f "$BUILD_DIR/$path" ] && [ ! -L "$BUILD_DIR/$path" ] || {
		echo "temporary package file type changed before publication: $path" >&2; exit 4; }
	[ "$(git hash-object --no-filters -- "$BUILD_DIR/$path")" = "$head_blob" ] || {
		echo "temporary package bytes changed before publication: $path" >&2; exit 4; }
	expected_permissions=644
	[ "$head_mode" != "100755" ] || expected_permissions=755
	[ "$(stat -Lc '%a' -- "$BUILD_DIR/$path")" = "$expected_permissions" ] || {
		echo "temporary package mode changed before publication: $path" >&2; exit 4; }
done < "$PROOF_FILE"
assert_scratch_workspace "immediately before publication"

# Arm both rollback identities before either move. A signal after a successful rename therefore
# cannot strand an unowned artifact. GNU mv --no-clobber can report success after declining a
# racing target, so compare identities and prove each source vanished.
RECEIPT_ID="$(identity "$RECEIPT_TMP")"
RECEIPT_PAYLOAD_SHA="$(sha256sum "$RECEIPT_TMP" | cut -d' ' -f1)"
PUBLISHED_DEST_ID="$BUILD_ID"
PUBLISHED_RECEIPT_ID="$RECEIPT_ID"
if ! mv -T --no-clobber -- "$BUILD_DIR" "$DEST"; then
	if [ -e "$DEST" ] || [ -L "$DEST" ]; then
		echo "package destination appeared during publication: $DEST" >&2
	else
		echo "package destination publication failed: $DEST" >&2
	fi
	exit 5
fi
[ ! -e "$BUILD_DIR" ] && [ ! -L "$BUILD_DIR" ] || {
	echo "package destination appeared during publication: $DEST" >&2; exit 5; }
[ -d "$DEST" ] && [ ! -L "$DEST" ] && [ "$(identity "$DEST")" = "$BUILD_ID" ] || {
	echo "published package identity mismatch: $DEST" >&2; exit 5; }
assert_scratch_workspace "after package publication" published

if ! mv -T --no-clobber -- "$RECEIPT_TMP" "$RECEIPT"; then
	if [ -e "$RECEIPT" ] || [ -L "$RECEIPT" ]; then
		echo "package receipt appeared during publication: $RECEIPT" >&2
	else
		echo "package receipt publication failed: $RECEIPT" >&2
	fi
	exit 5
fi
[ ! -e "$RECEIPT_TMP" ] && [ ! -L "$RECEIPT_TMP" ] || {
	echo "package receipt appeared during publication: $RECEIPT" >&2; exit 5; }
[ -f "$RECEIPT" ] && [ ! -L "$RECEIPT" ] && [ "$(identity "$RECEIPT")" = "$RECEIPT_ID" ] || {
	echo "published receipt identity mismatch: $RECEIPT" >&2; exit 5; }
forget_scratch_file "$RECEIPT_TMP"
assert_scratch_workspace "after receipt publication" published
assert_published_state || exit 5

# Destroy only the still-owned private scratch tree. One assignment then hands both validated
# published artifacts to the caller; cleanup never observes one-sided ownership clearing.
remove_owned_scratch_tree "$SCRATCH_DIR" "$SCRATCH_ID" || {
	echo "cannot remove private package scratch tree" >&2; exit 5; }
[ ! -e "$SCRATCH_DIR" ] && [ ! -L "$SCRATCH_DIR" ] || {
	echo "private package scratch tree survived cleanup" >&2; exit 5; }
PUBLICATION_ACCEPTED=1

echo "WORKSHOP PACKAGE CLEAN"
echo "mode:    $MODE"
echo "version: $VERSION"
echo "title:   $TITLE"
[ "$MODE" != "alpha" ] || echo "channel: v1.0 Alpha (final human release evidence deferred)"
echo "root:    $DEST"
echo "files:   $(find -P "$DEST" -type f | wc -l)"
echo "hashes:  $RECEIPT"
echo "No Steam item was created or uploaded."
