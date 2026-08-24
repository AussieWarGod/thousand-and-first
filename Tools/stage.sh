#!/usr/bin/env bash
# Canonical runtime staging for The Thousand and First.
#
# One source of truth for "what the game loads". Build gate, deploy, and Workshop
# packaging all consume this set, so the gate stops asking a different question
# from the one that matters (COORDINATION.md, open question answered 2026-08-20).
#
#   Tools/stage.sh manifest            print sorted relative-path + sha256 manifest
#   Tools/stage.sh list                print sorted relative paths only
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
ROOT_META=(README.md LICENSE CHANGELOG.md manifest.json)
# Asset trees copied whole.
ASSET_DIRS=(Textures)

stage_list() {
	cd "$REPO"
	for f in "${ROOT_META[@]}"; do
		[ -f "$f" ] && printf '%s\n' "$f"
	done
	# Every .cs and .xml the game could load, wherever it lives, minus the excluded trees.
	local args=( . )
	local d
	for d in "${EXCLUDE_DIRS[@]}"; do
		args+=( -path "./$d" -prune -o )
	done
	args+=( -type f '(' -name '*.cs' -o -name '*.xml' ')' -print )
	find "${args[@]}"
	for d in "${ASSET_DIRS[@]}"; do
		[ -d "$d" ] && find "$d" -type f -print
	done
}

sorted_list() { stage_list | sed 's|^\./||' | LC_ALL=C sort; }

cmd_list() { sorted_list; }

cmd_manifest() {
	cd "$REPO"
	sorted_list | while IFS= read -r f; do
		printf '%s  %s\n' "$(sha256sum "$f" | cut -d' ' -f1)" "$f"
	done
}

cmd_copy() {
	local dest="$1"
	[ -n "$dest" ] || { echo "copy needs a destination" >&2; exit 2; }
	mkdir -p "$dest"
	cd "$REPO"
	sorted_list | while IFS= read -r f; do
		mkdir -p "$dest/$(dirname "$f")"
		cp -p "$f" "$dest/$f"
	done
}

tree_list() {
	local tree="$1"
	[ -d "$tree" ] || return 0
	(
		cd "$tree"
		find . -type f -print | sed 's|^\./||' | LC_ALL=C sort
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
	grep -q '"id": *"r_ThousandAndFirst"' "$tree/manifest.json" 2>/dev/null || {
		echo "COLD-INSTALL MANIFEST IDENTITY MISMATCH" >&2
		failed=1
	}
	[ "$failed" -eq 0 ] || return 1
	echo "COLD-INSTALL CLEAN ($(wc -l < "$scratch/expected") files)"
)

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
	find . -type f -not -path './.git/*' -print | sed 's|^\./||' | LC_ALL=C sort
}

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

cmd_deploy() {
	local apply="${1:-}"
	[ -d "$LIVE" ] || { echo "live mod folder not found: $LIVE" >&2; exit 2; }

	echo "repo:   $REPO ($(git -C "$REPO" rev-parse --short HEAD), $(git -C "$REPO" status --porcelain | wc -l) dirty)"
	echo "live:   $LIVE"
	echo "staged: $(sorted_list | wc -l) files"
	echo
	cmd_diff

	if [ "$apply" != "--apply" ]; then
		echo
		echo "DRY RUN. Nothing written. Re-run with --apply to back up and mirror."
		return 0
	fi

	# Target identity: refuse to touch anything that is not recognisably this mod.
	grep -q '"id": *"r_ThousandAndFirst"' "$LIVE/manifest.json" 2>/dev/null || {
		echo "refusing: $LIVE/manifest.json does not declare r_ThousandAndFirst" >&2; exit 3; }

	local stamp backup
	stamp="$(date +%Y%m%d-%H%M%S)"
	backup="${LIVE}.backup-${stamp}"
	echo
	echo "backup: $backup"
	cp -a "$LIVE" "$backup"

	# Mirror: copy the runtime set, then remove live files outside it.
	cd "$REPO"
	sorted_list | while IFS= read -r f; do
		mkdir -p "$LIVE/$(dirname "$f")"
		cp -p "$f" "$LIVE/$f"
	done
	local tmp; tmp="$(mktemp -d)"
	sorted_list > "$tmp/staged"
	live_list > "$tmp/live"
	comm -13 "$tmp/staged" "$tmp/live" | while IFS= read -r f; do
		rm -f "$LIVE/$f"
	done
	find "$LIVE" -mindepth 1 -type d -empty -not -path "$LIVE/.git/*" -delete 2>/dev/null || true
	rm -rf "$tmp"

	# Verify: the live tree must now equal the staged set, byte for byte.
	local fail=0
	while IFS= read -r f; do
		cmp -s "$REPO/$f" "$LIVE/$f" || { echo "MISMATCH $f" >&2; fail=1; }
	done < <(sorted_list)
	local extra; extra="$(live_list | comm -13 <(sorted_list) -)"
	[ -z "$extra" ] || { echo "EXTRA FILES REMAIN:" >&2; echo "$extra" >&2; fail=1; }
	[ "$fail" -eq 0 ] || { echo "DEPLOY VERIFY FAILED — live folder restored from $backup" >&2
		rm -rf "$LIVE"; cp -a "$backup" "$LIVE"; exit 4; }

	# Receipt: what was deployed, from what, and the hash of every file.
	{
		echo "# Deploy receipt"
		echo "date:   $(date -Iseconds)"
		echo "repo:   $(git -C "$REPO" rev-parse HEAD)"
		echo "dirty:  $(git -C "$REPO" status --porcelain | wc -l) tracked changes"
		echo "live:   $LIVE"
		echo "backup: $backup"
		echo "files:  $(sorted_list | wc -l)"
		echo
		cmd_manifest
	} > "$REPO/Tools/last-deploy-receipt.txt"

	echo "DEPLOY OK — $(sorted_list | wc -l) files, receipt at Tools/last-deploy-receipt.txt"
}

case "${1:-}" in
	manifest) cmd_manifest ;;
	list)     cmd_list ;;
	copy)     cmd_copy "${2:-}" ;;
	verify)   cmd_verify "${2:-}" ;;
	diff)     cmd_diff ;;
	deploy)   cmd_deploy "${2:-}" ;;
	*) sed -n '2,20p' "${BASH_SOURCE[0]}"; exit 2 ;;
esac
