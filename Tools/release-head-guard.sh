#!/usr/bin/env bash
# Pin or reprove one clean release worktree without changing repository state.

set -euo pipefail

usage() {
	cat <<'EOF'
Usage:
  Tools/release-head-guard.sh capture REPOSITORY
  Tools/release-head-guard.sh verify REPOSITORY EXPECTED_HEAD
EOF
}

[ "$#" -ge 2 ] || { usage >&2; exit 2; }
ACTION="$1"
REPO_INPUT="$2"
shift 2
case "$ACTION:$#" in
	capture:0|verify:1) ;;
	*) usage >&2; exit 2 ;;
esac

[ -d "$REPO_INPUT" ] || {
	echo "release repository does not exist: $REPO_INPUT" >&2
	exit 2
}
REPO="$(cd "$REPO_INPUT" && pwd -P)"
TOP="$(git -C "$REPO" rev-parse --show-toplevel 2>/dev/null)" || {
	echo "release repository is not a Git worktree: $REPO" >&2
	exit 2
}
TOP="$(cd "$TOP" && pwd -P)"
[ "$TOP" = "$REPO" ] || {
	echo "release repository must be the Git worktree root: $REPO" >&2
	exit 2
}

read_head() {
	git -C "$REPO" rev-parse --verify 'HEAD^{commit}'
}

require_clean() {
	local dirty
	dirty="$(git -C "$REPO" status --porcelain=v1 --untracked-files=all --ignore-submodules=none)"
	[ -z "$dirty" ] || {
		echo "release check requires a clean worktree (tracked, staged, and untracked):" >&2
		printf '%s\n' "$dirty" >&2
		return 1
	}
}

if [ "$ACTION" = "capture" ]; then
	HEAD_BEFORE="$(read_head)"
	require_clean
	HEAD_AFTER="$(read_head)"
	[ "$HEAD_BEFORE" = "$HEAD_AFTER" ] || {
		echo "HEAD changed while release state was captured: $HEAD_BEFORE -> $HEAD_AFTER" >&2
		exit 1
	}
	printf '%s\n' "$HEAD_AFTER"
	exit 0
fi

EXPECTED_HEAD="$1"
case "$EXPECTED_HEAD" in
	''|*[!0-9a-f]*) echo "expected release HEAD is not a lowercase object ID" >&2; exit 2 ;;
esac
HEAD_BEFORE="$(read_head)"
[ "$HEAD_BEFORE" = "$EXPECTED_HEAD" ] || {
	echo "HEAD changed during release check: $EXPECTED_HEAD -> $HEAD_BEFORE" >&2
	exit 1
}
require_clean
HEAD_AFTER="$(read_head)"
[ "$HEAD_AFTER" = "$EXPECTED_HEAD" ] || {
	echo "HEAD changed during release check: $EXPECTED_HEAD -> $HEAD_AFTER" >&2
	exit 1
}
