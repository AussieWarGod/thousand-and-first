#!/usr/bin/env bash
# Regression fixtures for stage.sh's deploy target boundary.  Apply uses only an owned temp tree.

set -euo pipefail

REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
FIXTURE="$(mktemp -d /tmp/taf-stage-safety.XXXXXX)"
cleanup() {
	status=$?
	trap - EXIT
	case "$FIXTURE" in
		/tmp/taf-stage-safety.*) find -P "$FIXTURE" -depth -delete ;;
		*) echo "refusing unexpected stage fixture cleanup path: $FIXTURE" >&2; status=1 ;;
	esac
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
	cp "$REPO/manifest.json" "$root/manifest.json"
	printf '%s\n' "$root"
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
		*) printf '%s\n' "$backup_output" >&2; exit 1 ;;
	esac
	[ ! -e "$BACKUP_ALIAS_REPO/created-backups" ] \
		&& [ ! -L "$BACKUP_ALIAS_REPO/created-backups" ]
else
	echo "BIND-ALIAS STAGE FIXTURES SKIPPED: unprivileged mount namespace unavailable" >&2
fi

# Exact verification rejects non-file nodes even when the regular-file inventory still matches.
"$REPO/Tools/stage.sh" copy "$FIXTURE/verify-special" >/dev/null
mkfifo "$FIXTURE/verify-special/unexpected.fifo"
set +e
verify_output="$("$REPO/Tools/stage.sh" verify "$FIXTURE/verify-special" 2>&1)"
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

# Any error after backup must restore the complete original tree.  A file where the staged Core/
# directory belongs forces the first mid-copy failure without an artificial production hook.
mkdir -p "$FIXTURE/live-parent/broken" "$FIXTURE/backups"
BROKEN="$FIXTURE/live-parent/broken"
cp "$REPO/manifest.json" "$BROKEN/manifest.json"
printf 'blocks directory creation\n' > "$BROKEN/Core"
printf 'survives rollback\n' > "$BROKEN/extra.txt"
snapshot() {
	local root="$1" output="$2"
	(
		cd "$root"
		find . -type f -print0 | LC_ALL=C sort -z | xargs -0 sha256sum
	) > "$output"
}
snapshot "$BROKEN" "$FIXTURE/before.sha256"
set +e
failure="$(TAF_LIVE_MOD="$BROKEN" TAF_DEPLOY_BACKUP_ROOT="$FIXTURE/backups" \
	"$REPO/Tools/stage.sh" deploy --apply 2>&1)"
status=$?
set -e
[ "$status" -ne 0 ]
case "$failure" in *"live folder restored"*) ;; *) printf '%s\n' "$failure" >&2; exit 1 ;; esac
snapshot "$BROKEN" "$FIXTURE/after.sha256"
cmp -s "$FIXTURE/before.sha256" "$FIXTURE/after.sha256"
[ "$(find "$FIXTURE/backups" -mindepth 1 -maxdepth 1 -type d | wc -l)" -eq 1 ]

echo "STAGE TARGET SAFETY CLEAN"
