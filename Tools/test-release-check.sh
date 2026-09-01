#!/usr/bin/env bash
# Destructive only inside one mktemp-owned release-check fixture tree.

set -euo pipefail

SOURCE_REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd -P)"
FIXTURE_ROOT="$(mktemp -d /tmp/taf-release-check.XXXXXX)"
FIXTURE_REPO="$FIXTURE_ROOT/repo"
QUD_ROOT="$FIXTURE_ROOT/qud"

cleanup() {
	local status=$?
	trap - EXIT
	case "$FIXTURE_ROOT" in
		/tmp/taf-release-check.*) find -P "$FIXTURE_ROOT" -depth -delete ;;
		*) echo "refusing unexpected release-check fixture cleanup path: $FIXTURE_ROOT" >&2; status=1 ;;
	esac
	exit "$status"
}
trap cleanup EXIT

expect_fail() {
	local label="$1" needle="$2"
	shift 2
	local output status
	set +e
	output="$("$@" 2>&1)"
	status=$?
	set -e
	[ "$status" -ne 0 ] || { echo "$label unexpectedly succeeded" >&2; exit 1; }
	case "$output" in
		*"$needle"*) ;;
		*) echo "$label failed for wrong reason:" >&2; printf '%s\n' "$output" >&2; exit 1 ;;
	esac
}

bash -n "$SOURCE_REPO/Tools/release-check.sh"
bash -n "$SOURCE_REPO/Tools/release-head-guard.sh"
grep -Fq 'HEAD_COMMIT="$(bash "$HEAD_GUARD" capture "$REPO")"' \
	"$SOURCE_REPO/Tools/release-check.sh"
grep -Fq 'python3 Tools/workshop_metadata.py workshop "$MODE" manifest.json workshop.json' \
	"$SOURCE_REPO/Tools/release-check.sh"
grep -Fq 'python3 Tools/workshop_metadata.py alpha-candidate' \
	"$SOURCE_REPO/Tools/release-check.sh"
grep -Fq 'python3 Tools/workshop_metadata.py evidence' \
	"$SOURCE_REPO/Tools/release-check.sh"
grep -Fq 'bash "$HEAD_GUARD" verify "$REPO" "$HEAD_COMMIT"' \
	"$SOURCE_REPO/Tools/release-check.sh"

mkdir -p "$FIXTURE_REPO/Tools" "$QUD_ROOT"
cp -- "$SOURCE_REPO/Tools/release-check.sh" "$FIXTURE_REPO/Tools/release-check.sh"
cp -- "$SOURCE_REPO/Tools/release-head-guard.sh" "$FIXTURE_REPO/Tools/release-head-guard.sh"
printf '%s\n' 'fixture' > "$FIXTURE_REPO/tracked.txt"
git -C "$FIXTURE_REPO" init -q
git -C "$FIXTURE_REPO" config user.name "TAF release-check harness"
git -C "$FIXTURE_REPO" config user.email "fixture@example.invalid"
git -C "$FIXTURE_REPO" add --all
git -C "$FIXTURE_REPO" commit -q -m "fixture base"

HEAD_GUARD="$FIXTURE_REPO/Tools/release-head-guard.sh"
PINNED_HEAD="$(bash "$HEAD_GUARD" capture "$FIXTURE_REPO")"
bash "$HEAD_GUARD" verify "$FIXTURE_REPO" "$PINNED_HEAD"

printf '%s\n' 'dirty' >> "$FIXTURE_REPO/tracked.txt"
expect_fail "dirty tracked worktree" "requires a clean worktree" \
	bash "$HEAD_GUARD" capture "$FIXTURE_REPO"
git -C "$FIXTURE_REPO" restore --source=HEAD --worktree -- tracked.txt

printf '%s\n' 'staged' >> "$FIXTURE_REPO/tracked.txt"
git -C "$FIXTURE_REPO" add -- tracked.txt
expect_fail "staged tracked worktree" "requires a clean worktree" \
	bash "$HEAD_GUARD" capture "$FIXTURE_REPO"
git -C "$FIXTURE_REPO" restore --source=HEAD --staged --worktree -- tracked.txt

printf '%s\n' 'untracked' > "$FIXTURE_REPO/untracked.txt"
expect_fail "untracked worktree" "requires a clean worktree" \
	bash "$HEAD_GUARD" capture "$FIXTURE_REPO"
unlink -- "$FIXTURE_REPO/untracked.txt"

printf '%s\n' 'next' >> "$FIXTURE_REPO/tracked.txt"
git -C "$FIXTURE_REPO" add -- tracked.txt
git -C "$FIXTURE_REPO" commit -q -m "advance HEAD"
expect_fail "changed HEAD" "HEAD changed during release check" \
	bash "$HEAD_GUARD" verify "$FIXTURE_REPO" "$PINNED_HEAD"

RELEASE_CHECK="$FIXTURE_REPO/Tools/release-check.sh"
"$RELEASE_CHECK" --help | grep -Fq -- '--test|--alpha|--release'
expect_fail "missing lane" "Usage:" "$RELEASE_CHECK"
expect_fail "unknown lane" "Usage:" "$RELEASE_CHECK" --unknown
expect_fail "multiple lanes" "Usage:" "$RELEASE_CHECK" --test --alpha
for lane in --test --alpha --release; do
	expect_fail "$lane reaches post-guard Qud check" "configured Qud root is incomplete" \
		env TAF_QUD_ROOT="$QUD_ROOT" "$RELEASE_CHECK" "$lane"
done

echo "RELEASE CHECK HARNESS CLEAN"
