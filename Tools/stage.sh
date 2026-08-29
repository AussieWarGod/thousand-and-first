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
# Nothing is written to the live folder without --apply. Apply first takes an exact regular-file
# content and file/directory permission-mode snapshot. Ownership, timestamps, xattrs, ACLs, sparse
# layout, and hard-link topology are intentionally outside this cross-platform backup contract.

set -euo pipefail

REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
ATOMIC_TREE="$REPO/Tools/atomic_tree_publish.py"
default_live_mod() {
	# Deployment is WSL/Windows-only, but inventory checks also run on Linux CI. Derive the
	# interactive Windows account when interop exists; never bake a maintainer name into the tool.
	command -v powershell.exe >/dev/null 2>&1 || return 0
	command -v wslpath >/dev/null 2>&1 || return 0
	local windows_profile wsl_profile
	windows_profile="$(powershell.exe -NoProfile -Command \
		"[Environment]::GetFolderPath('UserProfile')" 2>/dev/null | tr -d '\r' | tail -n 1)"
	[ -n "$windows_profile" ] || return 0
	wsl_profile="$(wslpath -u "$windows_profile" 2>/dev/null)" || return 0
	[ -n "$wsl_profile" ] || return 0
	printf '%s\n' "$wsl_profile/AppData/LocalLow/Freehold Games/CavesOfQud/Mods/ThousandAndFirst"
}

LIVE="${TAF_LIVE_MOD:-}"

resolve_live_mod() {
	[ -z "$LIVE" ] || return 0
	LIVE="$(default_live_mod)"
	[ -n "$LIVE" ] || {
		echo "cannot derive the live mod folder; set TAF_LIVE_MOD explicitly" >&2
		return 2
	}
}

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
EXCLUDE_DIRS=(.git .nuget _notes DevTests Harness Art docs Tools .ruff_cache __pycache__ .agent-handoff)
# Metadata that is harmless in a mod folder and wanted on the Workshop.
ROOT_META=(README.md LICENSE NOTICE CHANGELOG.md manifest.json modconfig.json preview.png workshop.json)
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
    portable_casefold_key = path.casefold()
    if portable_casefold_key in windows_seen:
        raise SystemExit(
            label + " inventory contains a Windows case-fold collision: "
            + repr(windows_seen[portable_casefold_key]) + " and " + repr(path)
        )
    windows_seen[portable_casefold_key] = path
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

tree_manifest_from_inventory() {
	local tree="$1" inventory="${2:-}" f
	if [ -n "$inventory" ]; then
		while IFS= read -r f; do
			printf '%s  %s\n' "$(sha256sum -- "$tree/$f" | cut -d' ' -f1)" "$f"
		done < "$inventory"
	else
		while IFS= read -r f; do
			printf '%s  %s\n' "$(sha256sum -- "$tree/$f" | cut -d' ' -f1)" "$f"
		done
	fi
}

identity() { stat -Lc '%d:%i' -- "$1"; }

random_token() {
	python3 -c 'import secrets; print(secrets.token_bytes(16).hex())'
}

# All cooperating stage transactions lock the exact parent-directory inode for their whole
# namespace transaction. The helper verifies this inherited descriptor before every name lookup or
# mutation. Cryptorandom private names and sticky-directory ownership protect against other UIDs.
# A malicious same-UID process can ignore advisory flock and mutate any same-UID deployment tree;
# Linux renameat2 has no inode-CAS primitive, so that actor is outside this tool's integrity boundary.
acquire_parent_lock() {
	local path="$1" expected_id="$2" output_variable="$3" descriptor actual_id
	command -v flock >/dev/null 2>&1 || {
		echo "atomic publication is unsupported: flock is unavailable" >&2
		return 75
	}
	exec {descriptor}<"$path" || {
		echo "cannot open transaction-lock directory: $path" >&2
		return 75
	}
	actual_id="$(stat -Lc '%d:%i' -- "/proc/$BASHPID/fd/$descriptor" 2>/dev/null)" || {
		exec {descriptor}<&-
		echo "cannot bind transaction-lock directory: $path" >&2
		return 75
	}
	[ "$actual_id" = "$expected_id" ] || {
		exec {descriptor}<&-
		echo "transaction-lock directory identity changed: $path" >&2
		return 4
	}
	flock -n -x "$descriptor" || {
		exec {descriptor}<&-
		echo "another atomic publication owns the parent transaction lock: $path" >&2
		return 75
	}
	printf -v "$output_variable" '%s' "$descriptor"
}

trusted_ancestor_issue() {
	python3 - "$1" <<'PY'
import os
import stat
import sys

path = os.path.abspath(sys.argv[1])
allowed_owners = {os.getuid(), 0}
while True:
    status = os.stat(path, follow_symlinks=False)
    if not stat.S_ISDIR(status.st_mode):
        print(f"{path} is linked or not a directory")
        break
    if status.st_uid not in allowed_owners:
        print(f"{path} has untrusted owner uid {status.st_uid}")
        break
    shared_writable = bool(status.st_mode & (stat.S_IWGRP | stat.S_IWOTH))
    if shared_writable and not status.st_mode & stat.S_ISVTX:
        print(f"{path} is shared-writable without sticky-bit protection")
        break
    parent = os.path.dirname(path)
    if parent == path:
        break
    path = parent
PY
}

require_atomic_helper() {
	[ -f "$ATOMIC_TREE" ] && [ ! -L "$ATOMIC_TREE" ] || {
		echo "atomic tree publication helper is unavailable: $ATOMIC_TREE" >&2; return 75; }
}

cleanup_private_entry() {
	local parent="$1" parent_id="$2" lock_fd="$3" name="$4" expected_kind="$5"
	local expected_id="${6:-}" inspection="" actual_kind="" actual_id="" after="" locations=""
	local remove_status=0
	inspection="$(python3 "$ATOMIC_TREE" inspect --parent "$parent" --parent-id "$parent_id" \
		--lock-fd "$lock_fd" --name "$name")" || {
		echo "cleanup inspection failed; retained named entry: $parent/$name expected-id=${expected_id:-unknown}" >&2
		return 5
	}
	[ "$inspection" != "absent" ] || return 0
	IFS=$'\t' read -r actual_kind actual_id <<< "$inspection"
	if [ -z "$expected_id" ]; then
		echo "cleanup ownership is unknown; retained $parent/$name id=${actual_id:-unknown} kind=${actual_kind:-unknown}" >&2
		return 5
	fi
	if [ "$actual_kind" != "$expected_kind" ] \
			|| [ "$actual_id" != "$expected_id" ]; then
		echo "cleanup identity ambiguous; retained $parent/$name id=${actual_id:-unknown} kind=${actual_kind:-unknown}" >&2
		return 5
	fi
	python3 "$ATOMIC_TREE" remove --kind "$expected_kind" --parent "$parent" \
		--parent-id "$parent_id" --lock-fd "$lock_fd" --name "$name" \
		--expected-id "$actual_id" || remove_status=$?
	after="$(python3 "$ATOMIC_TREE" inspect --parent "$parent" --parent-id "$parent_id" \
		--lock-fd "$lock_fd" --name "$name")" || {
		echo "cleanup post-inspection failed for $parent/$name id=$actual_id" >&2
		return 5
	}
	locations="$(python3 "$ATOMIC_TREE" locate --kind "$expected_kind" \
		--parent "$parent" --parent-id "$parent_id" --lock-fd "$lock_fd" \
		--expected-id "$actual_id")" || {
		echo "cleanup identity search failed; inspect $parent for id=$actual_id" >&2
		return 5
	}
	if [ -n "$locations" ]; then
		echo "cleanup retained exact identities under $parent:" >&2
		printf '%s\n' "$locations" >&2
		return 5
	fi
	[ "$after" = "absent" ] || {
		echo "cleanup postcondition ambiguous; retained $parent/$name id=$actual_id" >&2
		return 5
	}
	if [ "$remove_status" -ne 0 ]; then
		echo "cleanup removed exact identity $actual_id, but helper returned status $remove_status" >&2
		return "$remove_status"
	fi
}

refuse_recovery_entries() {
	local parent="$1" parent_id="$2" lock_fd="$3" prefix="$4" label="$5" entries
	entries="$(python3 "$ATOMIC_TREE" list-prefix --parent "$parent" \
		--parent-id "$parent_id" --lock-fd "$lock_fd" --prefix "$prefix")"
	[ -z "$entries" ] || {
		echo "$label; inspect exact retained entries under $parent:" >&2
		printf '%s\n' "$entries" >&2
		return 5
	}
}

cmd_copy() (
	local dest="$1" dest_lex parent parent_real parent_id parent_lock_fd=""
	local repo_real repo_id overlap unsafe destination_name="" destination_id="absent"
	local inventory_payload="" inventory_fd="" result="" frozen_manifest=""
	local sibling_name="" sibling_id="" candidate_armed=0 candidate_id_known=0
	local publication_pending=0 publication_accepted=0
	cleanup_copy() {
		local status=$? state="" after_state="" exchange_status=0 recovery_failed=0
		record_copy_cleanup_failure() {
			local cleanup_status=$?
			if [ "$cleanup_status" -eq 5 ]; then
				recovery_failed=1
			elif [ "$status" -eq 0 ]; then
				status="$cleanup_status"
			fi
		}
		trap - EXIT HUP INT TERM
		set +e
		if [ "$publication_pending" -eq 1 ]; then
			state="$(python3 "$ATOMIC_TREE" state --parent "$parent_real" \
				--parent-id "$parent_id" --lock-fd "$parent_lock_fd" \
				--source "$sibling_name" --source-id "$sibling_id" \
				--destination "$destination_name" --destination-id "$destination_id")" \
				|| state="ambiguous"
			if [ "$publication_accepted" -eq 1 ]; then
				case "$state" in
					after)
						if [ "$destination_id" != "absent" ]; then
							cleanup_private_entry "$parent_real" "$parent_id" "$parent_lock_fd" \
								"$sibling_name" directory "$destination_id" \
								|| record_copy_cleanup_failure
						fi ;;
					accepted) ;;
					*) recovery_failed=1 ;;
				esac
			else
				case "$state" in
					before)
						cleanup_private_entry "$parent_real" "$parent_id" "$parent_lock_fd" \
							"$sibling_name" directory "$sibling_id" \
							|| record_copy_cleanup_failure
						after_state="$(python3 "$ATOMIC_TREE" state --parent "$parent_real" \
							--parent-id "$parent_id" --lock-fd "$parent_lock_fd" \
							--source "$sibling_name" --source-id "$sibling_id" \
							--destination "$destination_name" --destination-id "$destination_id")" \
							|| after_state="ambiguous"
						[ "$after_state" = "rolled-back" ] || recovery_failed=1 ;;
					after)
						if [ "$destination_id" = "absent" ]; then
							cleanup_private_entry "$parent_real" "$parent_id" "$parent_lock_fd" \
								"$destination_name" directory "$sibling_id" \
								|| record_copy_cleanup_failure
						else
							python3 "$ATOMIC_TREE" exchange --parent "$parent_real" \
								--parent-id "$parent_id" --lock-fd "$parent_lock_fd" \
								--left "$sibling_name" --left-id "$destination_id" \
								--right "$destination_name" --right-id "$sibling_id" \
								|| exchange_status=$?
						fi
						after_state="$(python3 "$ATOMIC_TREE" state --parent "$parent_real" \
							--parent-id "$parent_id" --lock-fd "$parent_lock_fd" \
							--source "$sibling_name" --source-id "$sibling_id" \
							--destination "$destination_name" --destination-id "$destination_id")" \
							|| after_state="ambiguous"
						if [ "$destination_id" != "absent" ] && [ "$after_state" = "before" ]; then
							if [ "$exchange_status" -eq 0 ]; then
								cleanup_private_entry "$parent_real" "$parent_id" "$parent_lock_fd" \
									"$sibling_name" directory "$sibling_id" \
									|| record_copy_cleanup_failure
								after_state="$(python3 "$ATOMIC_TREE" state --parent "$parent_real" \
									--parent-id "$parent_id" --lock-fd "$parent_lock_fd" \
									--source "$sibling_name" --source-id "$sibling_id" \
									--destination "$destination_name" --destination-id "$destination_id")" \
									|| after_state="ambiguous"
							else
								recovery_failed=1
							fi
						fi
						[ "$after_state" = "rolled-back" ] || recovery_failed=1 ;;
					rolled-back) ;;
					*) recovery_failed=1 ;;
				esac
				[ "$recovery_failed" -ne 0 ] \
					|| echo "STAGE COPY FAILED — atomic publication rolled back" >&2
			fi
		elif [ "$candidate_armed" -eq 1 ]; then
			cleanup_private_entry "$parent_real" "$parent_id" "$parent_lock_fd" \
				"$sibling_name" directory \
				"$([ "$candidate_id_known" -eq 1 ] && printf '%s' "$sibling_id")" \
				|| record_copy_cleanup_failure
		fi
		if [ "$recovery_failed" -ne 0 ]; then
			echo "STAGE COPY FAILED — no ambiguous identity was deleted; inspect $parent_real/$sibling_name and $dest_lex" >&2
			status=5
		fi
		exit "$status"
	}
	trap cleanup_copy EXIT
	trap 'exit 129' HUP
	trap 'exit 130' INT
	trap 'exit 143' TERM
	[ -n "$dest" ] || { echo "copy needs a destination" >&2; exit 2; }
	require_atomic_helper
	dest_lex="$(realpath -ms -- "$dest")"
	destination_name="$(basename -- "$dest_lex")"
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
	parent_id="$(identity "$parent_real")"
	unsafe="$(trusted_ancestor_issue "$parent_real")" || {
		echo "cannot inspect stage copy parent protection" >&2; exit 2; }
	[ -z "$unsafe" ] || {
		echo "atomic stage copy is unsupported at this parent: $unsafe" >&2; exit 75; }
	acquire_parent_lock "$parent_real" "$parent_id" parent_lock_fd
	refuse_recovery_entries "$parent_real" "$parent_id" "$parent_lock_fd" \
		".${destination_name}.taf-copy-" "prior interrupted stage copy requires recovery"
	refuse_recovery_entries "$parent_real" "$parent_id" "$parent_lock_fd" \
		".taf-remove-" "prior quarantined cleanup requires recovery"
	if [ -e "$dest_lex" ] || [ -L "$dest_lex" ]; then
		[ ! -L "$dest_lex" ] || {
			echo "refusing linked stage copy target: $dest_lex" >&2; exit 2; }
		[ -d "$dest_lex" ] || {
			echo "stage copy target is not an ordinary directory: $dest_lex" >&2; exit 2; }
		[ -z "$(find -P "$dest_lex" -mindepth 1 -print -quit)" ] || {
			echo "stage copy target is not empty: $dest_lex" >&2; exit 2; }
		destination_id="$(identity "$dest_lex")"
	fi
	overlap="$(directory_identity_alias "$repo_real" "$parent_real")"
	[ -z "$overlap" ] || {
		echo "refusing stage copy alias into repository: $overlap" >&2; exit 2; }
	python3 "$ATOMIC_TREE" probe --parent "$parent_real" --parent-id "$parent_id" \
		--lock-fd "$parent_lock_fd"
	repo_id="$(identity "$repo_real")"
	inventory_payload="$(sorted_list)"
	sibling_name=".${destination_name}.taf-copy-$(random_token)"
	[ "$(python3 "$ATOMIC_TREE" inspect --parent "$parent_real" --parent-id "$parent_id" \
		--lock-fd "$parent_lock_fd" --name "$sibling_name")" = "absent" ] || {
		echo "cryptorandom stage candidate name already exists: $parent_real/$sibling_name" >&2
		exit 5
	}
	candidate_armed=1
	exec {inventory_fd}< <(printf '%s\n' "$inventory_payload")
	result="$(python3 "$ATOMIC_TREE" materialize --source "$repo_real" \
		--source-id "$repo_id" --parent "$parent_real" --parent-id "$parent_id" \
		--lock-fd "$parent_lock_fd" --inventory-fd "$inventory_fd" --name "$sibling_name")"
	exec {inventory_fd}<&-
	IFS=$'\t' read -r result_name sibling_id <<< "$result"
	[ "$result_name" = "$sibling_name" ] && [ -n "$sibling_id" ] || {
		echo "atomic stage copy helper returned no exact private sibling identity" >&2; exit 4; }
	candidate_id_known=1
	frozen_manifest="$(tree_manifest_from_inventory "$parent_real/$sibling_name" \
		< <(printf '%s\n' "$inventory_payload"))"
	verify_tree_frozen "$parent_real/$sibling_name" "$inventory_payload" \
		"$frozen_manifest" >/dev/null
	[ "$(identity "$parent_real")" = "$parent_id" ] || {
		echo "stage copy parent identity changed before publication" >&2; exit 4; }
	if [ "$destination_id" = "absent" ]; then
		[ ! -e "$dest_lex" ] && [ ! -L "$dest_lex" ] || {
			echo "stage copy destination appeared before publication: $dest_lex" >&2; exit 4; }
	else
		[ -d "$dest_lex" ] && [ ! -L "$dest_lex" ] \
			&& [ "$(identity "$dest_lex")" = "$destination_id" ] \
			&& [ -z "$(find -P "$dest_lex" -mindepth 1 -print -quit)" ] || {
			echo "stage copy destination changed before publication: $dest_lex" >&2; exit 4; }
	fi
	publication_pending=1
	local publish_status=0 state=""
	python3 "$ATOMIC_TREE" publish --parent "$parent_real" --parent-id "$parent_id" \
		--lock-fd "$parent_lock_fd" --source "$sibling_name" --source-id "$sibling_id" \
		--destination "$destination_name" --destination-id "$destination_id" \
		|| publish_status=$?
	[ "$publish_status" -eq 0 ] || exit "$publish_status"
	state="$(python3 "$ATOMIC_TREE" state --parent "$parent_real" --parent-id "$parent_id" \
		--lock-fd "$parent_lock_fd" --source "$sibling_name" --source-id "$sibling_id" \
		--destination "$destination_name" --destination-id "$destination_id")"
	[ "$state" = "after" ] || {
		echo "stage copy publication identity state is not complete" >&2; exit 4; }
	verify_tree_frozen "$dest_lex" "$inventory_payload" "$frozen_manifest" >/dev/null
	publication_accepted=1
	if [ "$destination_id" != "absent" ]; then
		cleanup_private_entry "$parent_real" "$parent_id" "$parent_lock_fd" \
			"$sibling_name" directory "$destination_id"
		state="$(python3 "$ATOMIC_TREE" state --parent "$parent_real" --parent-id "$parent_id" \
			--lock-fd "$parent_lock_fd" --source "$sibling_name" --source-id "$sibling_id" \
			--destination "$destination_name" --destination-id "$destination_id")"
		[ "$state" = "accepted" ] || {
			echo "stage copy old destination cleanup state is ambiguous" >&2; exit 5; }
	fi
	publication_pending=0
	candidate_armed=0
	trap - EXIT HUP INT TERM
	echo "STAGE COPY CLEAN ($(printf '%s\n' "$inventory_payload" | wc -l) files): $dest_lex"
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

# After private-candidate verification, publication validation and the receipt use only this frozen
# inventory+manifest snapshot. They never reread a worktree that may continue changing.
verify_tree_frozen() (
	local tree="$1" expected_inventory="$2" expected_manifest="$3"
	local actual_inventory actual_manifest failed=0 special
	actual_inventory="$(tree_list "$tree")"
	if [ "$actual_inventory" != "$expected_inventory" ]; then
		echo "FROZEN CANDIDATE INVENTORY MISMATCH: $tree" >&2
		failed=1
	fi
	actual_manifest="$(tree_manifest_from_inventory "$tree" < <(printf '%s\n' "$expected_inventory"))"
	if [ "$actual_manifest" != "$expected_manifest" ]; then
		echo "FROZEN CANDIDATE CONTENT MISMATCH: $tree" >&2
		failed=1
	fi
	if find "$tree" -type l -print -quit | grep -q .; then
		echo "FROZEN CANDIDATE CONTAINS A SYMBOLIC LINK" >&2
		failed=1
	fi
	special="$(find -P "$tree" -mindepth 1 ! -type f ! -type d -print -quit)"
	if [ -n "$special" ]; then
		echo "FROZEN CANDIDATE CONTAINS A SPECIAL FILE: $special" >&2
		failed=1
	fi
	manifest_is_ours "$tree/manifest.json" || {
		echo "FROZEN CANDIDATE MANIFEST IDENTITY MISMATCH" >&2
		failed=1
	}
	[ "$failed" -eq 0 ] || return 1
	echo "FROZEN CANDIDATE CLEAN ($(printf '%s\n' "$expected_inventory" | wc -l) files)"
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

directory_identity_alias() {
	python3 - "$1" "$2" <<'PY'
import os
import sys

tree, candidate = sys.argv[1:]
candidate_status = os.stat(candidate, follow_symlinks=False)
candidate_identity = (candidate_status.st_dev, candidate_status.st_ino)
for current, _directories, _files in os.walk(tree, topdown=True, followlinks=False):
    status = os.stat(current, follow_symlinks=False)
    if (status.st_dev, status.st_ino) == candidate_identity:
        print(f"{candidate} -> {current}")
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
	resolve_live_mod || exit $?
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
	[ ! -d "$live_real/.git" ] || {
		echo "atomic deployment does not support .git metadata in the live mod folder" >&2
		exit 75
	}

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
	local proposed root_lex root_real repo_real live_parent parent parent_real boundary overlap unsafe
	local created=0 created_id="" boundary_ready=0 parent_id="" parent_lock_fd=""
	cleanup_created_backup_root() {
		local status=$? cleanup_status=0
		trap - EXIT
		set +e
		if [ "$created" -eq 1 ] && [ "$boundary_ready" -eq 0 ] \
				&& [ -d "$root_lex" ] && [ ! -L "$root_lex" ] \
				&& [ "$(stat -Lc '%d:%i' -- "$root_lex" 2>/dev/null)" = "$created_id" ] \
				&& [ -z "$(find -P "$root_lex" -mindepth 1 -print -quit 2>/dev/null)" ]; then
			cleanup_private_entry "$parent_real" "$parent_id" "$parent_lock_fd" \
				"$(basename -- "$root_lex")" directory "$created_id" || {
				cleanup_status=$?
				if [ "$cleanup_status" -eq 5 ]; then
					status=5
				elif [ "$status" -eq 0 ]; then
					status="$cleanup_status"
				fi
			}
		elif [ "$created" -eq 1 ] && [ "$boundary_ready" -eq 0 ]; then
			echo "backup-root cleanup ambiguous; retained $root_lex id=${created_id:-unknown}" >&2
			status=5
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
		parent_id="$(identity "$parent_real")"
		unsafe="$(trusted_ancestor_issue "$parent_real")" || {
			echo "cannot inspect deployment backup parent protection" >&2; exit 2; }
		[ -z "$unsafe" ] || {
			echo "atomic deployment backup is unsupported at this parent: $unsafe" >&2; exit 75; }
		if [ "$parent_id" = "${live_parent_id:-}" ]; then
			parent_lock_fd="${live_parent_lock_fd:-}"
		elif [ "$parent_id" = "${receipt_parent_id:-}" ]; then
			parent_lock_fd="${receipt_parent_lock_fd:-}"
		else
			acquire_parent_lock "$parent_real" "$parent_id" parent_lock_fd
		fi
		[ ! -e "$root_lex" ] && [ ! -L "$root_lex" ] || {
			echo "deployment backup root appeared before creation: $root_lex" >&2; exit 4; }
		created=1
		mkdir -- "$root_lex"
		created_id="$(stat -Lc '%d:%i' -- "$root_lex")"
		python3 "$ATOMIC_TREE" sync --parent "$parent_real" --parent-id "$parent_id" \
			--lock-fd "$parent_lock_fd"
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
	unsafe="$(trusted_ancestor_issue "$root_real")" || {
		echo "cannot inspect deployment backup root protection" >&2; exit 2; }
	[ -z "$unsafe" ] || {
		echo "atomic deployment backup is unsupported at this root: $unsafe" >&2; exit 75; }
	boundary_ready=1
	trap - EXIT
	printf '%s\n' "$root_real"
)

cmd_diff() (
	# Keep cleanup scoped to this diff invocation.  A RETURN trap installed in
	# a function leaks into its caller on this Bash path; once cmd_diff returns,
	# its local tmp is gone and the caller's RETURN trips `set -u`.
	resolve_live_mod || exit $?
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
	local backup="" source_commit source_dirty
	local inventory_payload="" inventory_fd="" frozen_manifest=""
	local live_parent="" live_parent_id="" live_parent_lock_fd="" live_name="" live_id=""
	local next_name="" next_id="" candidate_armed=0 candidate_id_known=0
	local publication_pending=0 live_committed=0
	local backup_root="" backup_root_id="" backup_name="" backup_id=""
	local backup_parent="" backup_parent_id="" backup_parent_lock_fd="" backup_root_lock_fd=""
	local backup_armed=0 backup_id_known=0 backup_complete=0
	local result="" result_name="" unsafe="" repo_id="" stamp=""
	local receipt_path="$REPO/Tools/last-deploy-receipt.txt"
	local receipt_parent="$REPO/Tools" receipt_parent_id="" receipt_parent_lock_fd=""
	local receipt_tmp_name="" receipt_tmp_id="" receipt_tmp_armed=0 receipt_tmp_id_known=0
	local receipt_previous_id="absent" receipt_pending=0 receipt_write_status=0
	cleanup_deploy() {
		local original_status=$? state="" after_state="" receipt_state="" expected_id=""
		local sync_status=0 receipt_exchange_status=0 recovery_failed=0 live_is_old=1
		local halt_recovery_mutation=0
		retain_deploy_state() {
			recovery_failed=1
			halt_recovery_mutation=1
		}
		record_deploy_cleanup_failure() {
			local cleanup_status=$?
			if [ "$cleanup_status" -eq 5 ]; then
				retain_deploy_state
			elif [ "$original_status" -eq 0 ]; then
				original_status="$cleanup_status"
			fi
		}
		trap - EXIT HUP INT TERM
		set +e
		if [ "$publication_pending" -eq 1 ]; then
			state="$(python3 "$ATOMIC_TREE" state --parent "$live_parent" \
				--parent-id "$live_parent_id" --lock-fd "$live_parent_lock_fd" \
				--source "$next_name" --source-id "$next_id" \
				--destination "$live_name" --destination-id "$live_id")" \
				|| state="ambiguous"
			case "$state" in
				before)
					live_is_old=1
					cleanup_private_entry "$live_parent" "$live_parent_id" \
						"$live_parent_lock_fd" "$next_name" directory "$next_id" \
						|| record_deploy_cleanup_failure
					after_state="$(python3 "$ATOMIC_TREE" state --parent "$live_parent" \
						--parent-id "$live_parent_id" --lock-fd "$live_parent_lock_fd" \
						--source "$next_name" --source-id "$next_id" \
						--destination "$live_name" --destination-id "$live_id")" \
						|| after_state="ambiguous"
					[ "$after_state" = "rolled-back" ] || retain_deploy_state ;;
				after)
					live_is_old=0
					if [ "$live_committed" -eq 0 ]; then
						python3 "$ATOMIC_TREE" sync --parent "$live_parent" \
							--parent-id "$live_parent_id" --lock-fd "$live_parent_lock_fd" \
							|| sync_status=$?
						after_state="$(python3 "$ATOMIC_TREE" state --parent "$live_parent" \
							--parent-id "$live_parent_id" --lock-fd "$live_parent_lock_fd" \
							--source "$next_name" --source-id "$next_id" \
							--destination "$live_name" --destination-id "$live_id")" \
							|| after_state="ambiguous"
						if [ "$sync_status" -eq 0 ] && [ "$after_state" = "after" ]; then
							live_committed=1
						else
							retain_deploy_state
						fi
					fi
					if [ "$live_committed" -eq 1 ]; then
						cleanup_private_entry "$live_parent" "$live_parent_id" \
							"$live_parent_lock_fd" "$next_name" directory "$live_id" \
							|| record_deploy_cleanup_failure
					fi ;;
				accepted) live_is_old=0; live_committed=1 ;;
				rolled-back) live_is_old=1 ;;
				*) retain_deploy_state ;;
			esac
		elif [ "$candidate_armed" -eq 1 ]; then
			expected_id=""
			[ "$candidate_id_known" -eq 0 ] || expected_id="$next_id"
			cleanup_private_entry "$live_parent" "$live_parent_id" "$live_parent_lock_fd" \
				"$next_name" directory "$expected_id" || record_deploy_cleanup_failure
		fi

		# Live exchange is the only deployment commit. Receipt is informational and independently
		# reconciled to either its exact old or exact new state; never mutate an ambiguous mapping.
		if [ "$halt_recovery_mutation" -eq 0 ] && [ "$receipt_pending" -eq 1 ]; then
			receipt_state="$(python3 "$ATOMIC_TREE" state --kind file \
				--parent "$receipt_parent" --parent-id "$receipt_parent_id" \
				--lock-fd "$receipt_parent_lock_fd" --source "$receipt_tmp_name" \
				--source-id "$receipt_tmp_id" --destination "$(basename -- "$receipt_path")" \
				--destination-id "$receipt_previous_id")" || receipt_state="ambiguous"
			if [ "$live_committed" -eq 1 ]; then
				case "$receipt_state" in
					before)
						cleanup_private_entry "$receipt_parent" "$receipt_parent_id" \
							"$receipt_parent_lock_fd" "$receipt_tmp_name" file \
							"$receipt_tmp_id" || record_deploy_cleanup_failure ;;
					after)
						if [ "$receipt_previous_id" != "absent" ]; then
							cleanup_private_entry "$receipt_parent" "$receipt_parent_id" \
								"$receipt_parent_lock_fd" "$receipt_tmp_name" file \
								"$receipt_previous_id" || record_deploy_cleanup_failure
						fi ;;
					accepted|rolled-back) ;;
					*) retain_deploy_state ;;
				esac
			elif [ "$live_is_old" -eq 1 ]; then
				case "$receipt_state" in
					before)
						cleanup_private_entry "$receipt_parent" "$receipt_parent_id" \
							"$receipt_parent_lock_fd" "$receipt_tmp_name" file \
							"$receipt_tmp_id" || record_deploy_cleanup_failure ;;
					after)
						if [ "$receipt_previous_id" = "absent" ]; then
							cleanup_private_entry "$receipt_parent" "$receipt_parent_id" \
								"$receipt_parent_lock_fd" "$(basename -- "$receipt_path")" file \
								"$receipt_tmp_id" || record_deploy_cleanup_failure
						else
							python3 "$ATOMIC_TREE" exchange --kind file \
								--parent "$receipt_parent" --parent-id "$receipt_parent_id" \
								--lock-fd "$receipt_parent_lock_fd" --left "$receipt_tmp_name" \
								--left-id "$receipt_previous_id" \
								--right "$(basename -- "$receipt_path")" \
								--right-id "$receipt_tmp_id" || receipt_exchange_status=$?
						fi
						receipt_state="$(python3 "$ATOMIC_TREE" state --kind file \
							--parent "$receipt_parent" --parent-id "$receipt_parent_id" \
							--lock-fd "$receipt_parent_lock_fd" --source "$receipt_tmp_name" \
							--source-id "$receipt_tmp_id" \
							--destination "$(basename -- "$receipt_path")" \
							--destination-id "$receipt_previous_id")" || receipt_state="ambiguous"
						if [ "$receipt_previous_id" != "absent" ] \
								&& [ "$receipt_state" = "before" ]; then
							if [ "$receipt_exchange_status" -eq 0 ]; then
								cleanup_private_entry "$receipt_parent" "$receipt_parent_id" \
									"$receipt_parent_lock_fd" "$receipt_tmp_name" file \
									"$receipt_tmp_id" || record_deploy_cleanup_failure
							else
								retain_deploy_state
							fi
						fi ;;
					rolled-back) ;;
					*) retain_deploy_state ;;
				esac
				receipt_state="$(python3 "$ATOMIC_TREE" state --kind file \
					--parent "$receipt_parent" --parent-id "$receipt_parent_id" \
					--lock-fd "$receipt_parent_lock_fd" --source "$receipt_tmp_name" \
					--source-id "$receipt_tmp_id" --destination "$(basename -- "$receipt_path")" \
					--destination-id "$receipt_previous_id")" || receipt_state="ambiguous"
				[ "$receipt_state" = "rolled-back" ] || retain_deploy_state
			else
				echo "receipt identities retained because live-tree state is not exactly old: $receipt_parent/$receipt_tmp_name id=$receipt_tmp_id" >&2
				retain_deploy_state
			fi
		elif [ "$halt_recovery_mutation" -eq 0 ] && [ "$receipt_tmp_armed" -eq 1 ]; then
			expected_id=""
			[ "$receipt_tmp_id_known" -eq 0 ] || expected_id="$receipt_tmp_id"
			cleanup_private_entry "$receipt_parent" "$receipt_parent_id" \
				"$receipt_parent_lock_fd" "$receipt_tmp_name" file "$expected_id" \
				|| record_deploy_cleanup_failure
		fi

		if [ "$halt_recovery_mutation" -eq 0 ] \
				&& [ "$backup_armed" -eq 1 ] && [ "$backup_complete" -eq 0 ]; then
			expected_id=""
			[ "$backup_id_known" -eq 0 ] || expected_id="$backup_id"
			cleanup_private_entry "$backup_root" "$backup_root_id" "$backup_root_lock_fd" \
				"$backup_name" directory "$expected_id" || record_deploy_cleanup_failure
		fi
		if [ "$halt_recovery_mutation" -eq 1 ]; then
			[ "$publication_pending" -eq 0 ] || echo \
				"live identities require recovery inspection: $live_parent/$live_name expected-id=$live_id; $live_parent/$next_name expected-id=${next_id:-unknown}" >&2
			[ "$receipt_tmp_armed" -eq 0 ] || echo \
				"receipt identity requires recovery inspection: $receipt_parent/$receipt_tmp_name expected-id=${receipt_tmp_id:-unknown}" >&2
			[ "$backup_armed" -eq 0 ] || [ "$backup_complete" -eq 1 ] || echo \
				"backup identity requires recovery inspection: $backup_root/$backup_name expected-id=${backup_id:-unknown}" >&2
		fi
		if [ "$live_committed" -eq 1 ] && [ "$original_status" -ne 0 ]; then
			echo "DEPLOY INCOMPLETE — new live tree is committed; informational receipt may be old or new; content+mode backup retained at $backup" >&2
		fi
		if [ "$recovery_failed" -ne 0 ]; then
			echo "DEPLOY FAILED — ambiguous or failed cleanup retained named identities; no further deletion attempted" >&2
			original_status=5
		fi
		exit "$original_status"
	}
	trap cleanup_deploy EXIT
	trap 'exit 129' HUP
	trap 'exit 130' INT
	trap 'exit 143' TERM
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

	require_atomic_helper
	live_parent="$(dirname -- "$LIVE")"
	live_name="$(basename -- "$LIVE")"
	live_parent_id="$(identity "$live_parent")"
	live_id="$(identity "$LIVE")"
	unsafe="$(trusted_ancestor_issue "$live_parent")" || {
		echo "cannot inspect live parent protection" >&2; exit 2; }
	[ -z "$unsafe" ] || {
		echo "atomic deployment is unsupported at this live parent: $unsafe" >&2; exit 75; }
	acquire_parent_lock "$live_parent" "$live_parent_id" live_parent_lock_fd
	refuse_recovery_entries "$live_parent" "$live_parent_id" "$live_parent_lock_fd" \
		".${live_name}.taf-next-" "prior interrupted deployment requires recovery"
	refuse_recovery_entries "$live_parent" "$live_parent_id" "$live_parent_lock_fd" \
		".taf-remove-" "prior quarantined cleanup requires recovery"
	receipt_parent_id="$(identity "$receipt_parent")"
	unsafe="$(trusted_ancestor_issue "$receipt_parent")" || {
		echo "cannot inspect deployment receipt parent protection" >&2; exit 2; }
	[ -z "$unsafe" ] || {
		echo "atomic deployment receipt is unsupported at this parent: $unsafe" >&2; exit 75; }
	acquire_parent_lock "$receipt_parent" "$receipt_parent_id" receipt_parent_lock_fd
	refuse_recovery_entries "$receipt_parent" "$receipt_parent_id" \
		"$receipt_parent_lock_fd" ".last-deploy-receipt.tmp." \
		"prior interrupted deployment receipt requires recovery"
	refuse_recovery_entries "$receipt_parent" "$receipt_parent_id" \
		"$receipt_parent_lock_fd" ".taf-remove-" \
		"prior quarantined receipt cleanup requires recovery"
	# Probe both exchange and no-replace in disposable siblings. Unsupported filesystems stop here;
	# the live entry has not been modified.
	python3 "$ATOMIC_TREE" probe --parent "$live_parent" --parent-id "$live_parent_id" \
		--lock-fd "$live_parent_lock_fd"
	# Receipt publication is a separate informational transaction and may live on a different
	# filesystem. Prove its directory durability and rename primitives before the live exchange too.
	python3 "$ATOMIC_TREE" probe --parent "$receipt_parent" --parent-id "$receipt_parent_id" \
		--lock-fd "$receipt_parent_lock_fd"
	if [ -L "$receipt_path" ] || { [ -e "$receipt_path" ] && [ ! -f "$receipt_path" ]; }; then
		echo "refusing unsafe deployment receipt path: $receipt_path" >&2
		exit 4
	fi
	if [ -e "$receipt_path" ]; then
		[ "$(stat -Lc '%h' -- "$receipt_path")" -eq 1 ] || {
			echo "refusing hard-linked deployment receipt path: $receipt_path" >&2
			exit 4
		}
		receipt_previous_id="$(identity "$receipt_path")"
	fi
	inventory_payload="$(sorted_list)"
	repo_id="$(identity "$REPO")"
	next_name=".${live_name}.taf-next-$(random_token)"
	[ "$(python3 "$ATOMIC_TREE" inspect --parent "$live_parent" --parent-id "$live_parent_id" \
		--lock-fd "$live_parent_lock_fd" --name "$next_name")" = "absent" ] || {
		echo "cryptorandom deployment candidate name already exists: $live_parent/$next_name" >&2
		exit 5
	}
	candidate_armed=1
	exec {inventory_fd}< <(printf '%s\n' "$inventory_payload")
	result="$(python3 "$ATOMIC_TREE" materialize --source "$REPO" --source-id "$repo_id" \
		--parent "$live_parent" --parent-id "$live_parent_id" \
		--lock-fd "$live_parent_lock_fd" --inventory-fd "$inventory_fd" --name "$next_name")"
	exec {inventory_fd}<&-
	IFS=$'\t' read -r result_name next_id <<< "$result"
	[ "$result_name" = "$next_name" ] && [ -n "$next_id" ] || {
		echo "atomic deployment helper returned no exact complete sibling identity" >&2; exit 4; }
	candidate_id_known=1
	frozen_manifest="$(tree_manifest_from_inventory "$live_parent/$next_name" \
		< <(printf '%s\n' "$inventory_payload"))"
	verify_tree_frozen "$live_parent/$next_name" "$inventory_payload" \
		"$frozen_manifest" >/dev/null

	stamp="$(date +%Y%m%d-%H%M%S)"
	backup_root="$(prepare_backup_root)"
	backup_root_id="$(identity "$backup_root")"
	backup_parent="$(dirname -- "$backup_root")"
	backup_parent_id="$(identity "$backup_parent")"
	if [ "$backup_parent_id" = "$live_parent_id" ]; then
		backup_parent_lock_fd="$live_parent_lock_fd"
	elif [ "$backup_parent_id" = "$receipt_parent_id" ]; then
		backup_parent_lock_fd="$receipt_parent_lock_fd"
	else
		acquire_parent_lock "$backup_parent" "$backup_parent_id" backup_parent_lock_fd
	fi
	acquire_parent_lock "$backup_root" "$backup_root_id" backup_root_lock_fd
	# Persist a newly created backup-root name, then independently prove this filesystem's durable
	# exchange/no-replace support before creating the backup or touching live.
	python3 "$ATOMIC_TREE" sync --parent "$backup_parent" --parent-id "$backup_parent_id" \
		--lock-fd "$backup_parent_lock_fd"
	python3 "$ATOMIC_TREE" probe --parent "$backup_root" --parent-id "$backup_root_id" \
		--lock-fd "$backup_root_lock_fd"
	backup_name="ThousandAndFirst-${stamp}-$$-$(random_token)"
	[ "$(python3 "$ATOMIC_TREE" inspect --parent "$backup_root" --parent-id "$backup_root_id" \
		--lock-fd "$backup_root_lock_fd" --name "$backup_name")" = "absent" ] || {
		echo "cryptorandom backup candidate name already exists: $backup_root/$backup_name" >&2
		exit 5
	}
	backup_armed=1
	result="$(python3 "$ATOMIC_TREE" materialize --source "$LIVE" --source-id "$live_id" \
		--parent "$backup_root" --parent-id "$backup_root_id" \
		--lock-fd "$backup_root_lock_fd" --name "$backup_name")"
	IFS=$'\t' read -r result_name backup_id <<< "$result"
	[ "$result_name" = "$backup_name" ] && [ -n "$backup_id" ] || {
		echo "atomic deployment helper returned no exact complete backup identity" >&2; exit 4; }
	backup_id_known=1
	backup="$backup_root/$backup_name"
	echo
	echo "content+mode backup (excludes ownership/timestamps/xattrs/ACLs/sparse layout/hard-link topology): $backup"
	python3 "$ATOMIC_TREE" compare --left "$LIVE" --left-id "$live_id" \
		--right "$backup" --right-id "$backup_id" >/dev/null
	backup_complete=1

	# Buffer the receipt payload from the verified private candidate, then create/write its exact
	# cryptorandom temp name through one O_NOFOLLOW descriptor before any live mutation.
	receipt_tmp_name=".last-deploy-receipt.tmp.$(random_token)"
	[ "$(python3 "$ATOMIC_TREE" inspect --parent "$receipt_parent" \
		--parent-id "$receipt_parent_id" --lock-fd "$receipt_parent_lock_fd" \
		--name "$receipt_tmp_name")" = "absent" ] || {
		echo "cryptorandom receipt temp name already exists: $receipt_parent/$receipt_tmp_name" >&2
		exit 5
	}
	receipt_tmp_armed=1
	set +e
	result="$({
		echo "# Deploy receipt"
		echo "date:   $(date -Iseconds)"
		echo "repo:   $source_commit"
		echo "dirty:  $source_dirty worktree entries before deploy"
		echo "live:   $LIVE"
		echo "backup: $backup"
			echo "backup-contract: exact regular-file content and file/directory permission modes"
			echo "backup-excludes: ownership, timestamps, xattrs, ACLs, sparse layout, hard-link topology"
			echo "receipt-authority: informational; live directory exchange is the deployment commit"
		echo "files:  $(printf '%s\n' "$inventory_payload" | wc -l)"
		echo
		printf '%s\n' "$frozen_manifest"
	} | python3 "$ATOMIC_TREE" write-file --parent "$receipt_parent" \
		--parent-id "$receipt_parent_id" --lock-fd "$receipt_parent_lock_fd" \
		--name "$receipt_tmp_name" --expected-id absent --mode 0600)"
	receipt_write_status=$?
	set -e
	[ "$receipt_write_status" -eq 0 ] || exit "$receipt_write_status"
	IFS=$'\t' read -r result_name receipt_tmp_id <<< "$result"
	[ "$result_name" = "$receipt_tmp_name" ] && [ -n "$receipt_tmp_id" ] || {
		echo "atomic receipt writer returned no exact temp identity" >&2; exit 4; }
	receipt_tmp_id_known=1

	# Rebind the live root, complete sibling, and parent immediately before the only live mutation.
	validate_live_target
	[ "$(identity "$LIVE")" = "$live_id" ] \
		&& [ "$(identity "$live_parent")" = "$live_parent_id" ] \
		&& [ "$(identity "$live_parent/$next_name")" = "$next_id" ] || {
		echo "deployment boundary changed before atomic exchange" >&2; exit 4; }
	python3 "$ATOMIC_TREE" compare --left "$LIVE" --left-id "$live_id" \
		--right "$backup" --right-id "$backup_id" >/dev/null
	[ "$(identity "$receipt_parent")" = "$receipt_parent_id" ] || {
		echo "deployment receipt parent changed before atomic exchange" >&2; exit 4; }
	if [ "$receipt_previous_id" = "absent" ]; then
		[ ! -e "$receipt_path" ] && [ ! -L "$receipt_path" ] || {
			echo "deployment receipt path appeared before atomic exchange: $receipt_path" >&2
			exit 4
		}
	elif [ ! -L "$receipt_path" ] && [ -f "$receipt_path" ] \
			&& [ "$(identity "$receipt_path")" = "$receipt_previous_id" ]; then
		:
	else
		echo "deployment receipt path changed before atomic exchange: $receipt_path" >&2
		exit 4
	fi
	publication_pending=1
	local exchange_status=0 state="" receipt_publish_status=0
	python3 "$ATOMIC_TREE" exchange --parent "$live_parent" --parent-id "$live_parent_id" \
		--lock-fd "$live_parent_lock_fd" --left "$next_name" --left-id "$next_id" \
		--right "$live_name" --right-id "$live_id" \
		|| exchange_status=$?
	[ "$exchange_status" -eq 0 ] || exit "$exchange_status"
	state="$(python3 "$ATOMIC_TREE" state --parent "$live_parent" \
		--parent-id "$live_parent_id" --lock-fd "$live_parent_lock_fd" \
		--source "$next_name" --source-id "$next_id" \
		--destination "$live_name" --destination-id "$live_id")"
	[ "$state" = "after" ] || {
		echo "deployment publication identity state is not complete" >&2; exit 4; }
	# One durable exact live-directory exchange is the deployment commit. Receipt lives in another
	# parent and may be old, new, or absent after untrappable process death; it is non-authoritative
	# and reconstructed on the next clean deployment.
	live_committed=1
	verify_tree_frozen "$LIVE" "$inventory_payload" "$frozen_manifest" >/dev/null

	# Receipt state is classified independently. Once live commits, keep whichever exact receipt
	# generation publication reached.
	if [ "$receipt_previous_id" = "absent" ]; then
		[ ! -e "$receipt_path" ] && [ ! -L "$receipt_path" ] || {
			echo "deployment receipt path appeared during deploy: $receipt_path" >&2; exit 4; }
	elif [ ! -L "$receipt_path" ] && [ -f "$receipt_path" ] \
			&& [ "$(identity "$receipt_path")" = "$receipt_previous_id" ]; then
		:
	else
		echo "deployment receipt path changed during deploy: $receipt_path" >&2
		exit 4
	fi
	receipt_pending=1
	python3 "$ATOMIC_TREE" publish --kind file --parent "$receipt_parent" \
		--parent-id "$receipt_parent_id" --lock-fd "$receipt_parent_lock_fd" \
		--source "$receipt_tmp_name" \
		--source-id "$receipt_tmp_id" --destination "$(basename -- "$receipt_path")" \
		--destination-id "$receipt_previous_id" || receipt_publish_status=$?
	[ "$receipt_publish_status" -eq 0 ] || exit "$receipt_publish_status"
	state="$(python3 "$ATOMIC_TREE" state --kind file --parent "$receipt_parent" \
		--parent-id "$receipt_parent_id" --lock-fd "$receipt_parent_lock_fd" \
		--source "$receipt_tmp_name" \
		--source-id "$receipt_tmp_id" --destination "$(basename -- "$receipt_path")" \
		--destination-id "$receipt_previous_id")"
	[ "$state" = "after" ] || {
		echo "deployment receipt publication identity state is not complete" >&2; exit 4; }
	python3 "$ATOMIC_TREE" sync-file --parent "$receipt_parent" \
		--parent-id "$receipt_parent_id" --lock-fd "$receipt_parent_lock_fd" \
		--name "$(basename -- "$receipt_path")" \
		--expected-id "$receipt_tmp_id"

	# Commit already happened at live exchange. These deletions retire exact rollback siblings only;
	# content+mode backup remains independently recoverable.
	cleanup_private_entry "$live_parent" "$live_parent_id" "$live_parent_lock_fd" \
		"$next_name" directory "$live_id"
	state="$(python3 "$ATOMIC_TREE" state --parent "$live_parent" \
		--parent-id "$live_parent_id" --lock-fd "$live_parent_lock_fd" \
		--source "$next_name" --source-id "$next_id" \
		--destination "$live_name" --destination-id "$live_id")"
	[ "$state" = "accepted" ] || {
		echo "deployment old-tree cleanup state is ambiguous" >&2; exit 4; }
	if [ "$receipt_previous_id" != "absent" ]; then
		cleanup_private_entry "$receipt_parent" "$receipt_parent_id" "$receipt_parent_lock_fd" \
			"$receipt_tmp_name" file "$receipt_previous_id"
		state="$(python3 "$ATOMIC_TREE" state --kind file --parent "$receipt_parent" \
			--parent-id "$receipt_parent_id" --lock-fd "$receipt_parent_lock_fd" \
			--source "$receipt_tmp_name" \
			--source-id "$receipt_tmp_id" --destination "$(basename -- "$receipt_path")" \
			--destination-id "$receipt_previous_id")"
		[ "$state" = "accepted" ] || {
			echo "deployment previous-receipt cleanup state is ambiguous" >&2; exit 4; }
	fi
	publication_pending=0
	receipt_pending=0
	candidate_armed=0
	receipt_tmp_armed=0
	receipt_tmp_name=""
	receipt_tmp_id=""
	trap - EXIT HUP INT TERM
	echo "DEPLOY OK — $(printf '%s\n' "$inventory_payload" | wc -l) files, receipt at Tools/last-deploy-receipt.txt"
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
