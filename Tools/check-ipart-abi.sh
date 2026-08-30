#!/usr/bin/env bash
# Guard the positional IPart wire inherited from the last deployed release.
#
# Qud's IComponent.Write/Read reflects every public instance field in metadata
# order.  A field appended to an already-shipped IPart therefore is not an
# additive schema change: an old save has no value for the new positional read.
# This gate compares source declarations with the deployed 0.1.0 baseline and
# permits layout drift only for the small set of parts whose reviewed custom
# serializer owns a backwards-compatible wire.

set -euo pipefail

REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
BASE_REF="${TAF_IPART_ABI_BASE_REF:-1dca10b}"

cd "$REPO"
git cat-file -e "${BASE_REF}^{commit}" 2>/dev/null || {
	echo "IPART ABI BASE MISSING: $BASE_REF" >&2
	exit 2
}

# Output records:
#   C|Type                 class exists
#   F|Type|Type Field      serialized public instance field, declaration order
#   W|Type                 direct custom Write override exists
#   R|Type                 direct custom Read override exists
#   N|Type                 direct WriteNamedFields call exists
#   D|Type                 direct ReadNamedFields call exists
extract_parts() {
	local known="${1:-}"
	awk -v known="$known" '
	BEGIN { depth=0; inpart=0; pending=0; skip=0; cname="" }
	{
		raw=$0
		line=$0
		if (!inpart && match(line,
			/public (sealed )?(partial )?class[[:space:]]+([A-Za-z0-9_]+)[[:space:]]*:[[:space:]]*IPart/, m)) {
			cname=m[3]
			pending=1
			print "C|" cname
		} else if (!inpart && match(line,
			/public (sealed )?partial class[[:space:]]+([A-Za-z0-9_]+)/, m) &&
			index(known, "|" m[2] "|") != 0) {
			# A partial continuation owns fields and custom serializers of the same IPart.
			cname=m[2]
			pending=1
		}
		opens=gsub(/\{/, "{", line)
		closes=gsub(/\}/, "}", line)
		if (pending && opens>0) {
			inpart=1
			cdepth=depth+1
			pending=0
		}
		if (inpart && depth==cdepth) {
			if (raw ~ /^[[:space:]]*public[[:space:]]+override[[:space:]]+void[[:space:]]+Write[[:space:]]*\(/)
				print "W|" cname
			if (raw ~ /^[[:space:]]*public[[:space:]]+override[[:space:]]+void[[:space:]]+Read[[:space:]]*\(/)
				print "R|" cname
			if (raw ~ /\[NonSerialized\]/) {
				skip=1
			} else if (raw ~ /^[[:space:]]*public[[:space:]]+/ &&
				raw ~ /;[[:space:]]*$/ &&
				raw !~ /^[[:space:]]*public[[:space:]]+(const|static)[[:space:]]/ &&
				raw !~ /=>/ && raw !~ /\(/) {
				if (!skip) {
					s=raw
					sub(/^[[:space:]]*public[[:space:]]+/, "", s)
					sub(/^readonly[[:space:]]+/, "", s)
					sub(/[[:space:]]*=.*$/, "", s)
					sub(/;[[:space:]]*$/, "", s)
					gsub(/[[:space:]]+/, " ", s)
					print "F|" cname "|" s
				}
				skip=0
			} else if (raw !~ /^[[:space:]]*\[/ &&
				raw !~ /^[[:space:]]*$/ &&
				raw !~ /^[[:space:]]*\/\//) {
				skip=0
			}
		}
		if (inpart && raw ~ /Writer\.WriteNamedFields\(this,[[:space:]]*typeof\(/)
			print "N|" cname
		if (inpart && raw ~ /Reader\.ReadNamedFields\(this,[[:space:]]*typeof\(/)
			print "D|" cname
		depth += opens-closes
		if (inpart && depth<cdepth) {
			inpart=0
			cname=""
			skip=0
		}
	}'
}

scan_baseline() {
	git grep -l -E 'class .*: IPart' "$BASE_REF" -- '*.cs' |
		sed 's/^[^:]*://' | LC_ALL=C sort |
		while IFS= read -r file; do
			git show "${BASE_REF}:${file}" | extract_parts
		done
}

# Tracked files only, via git: the deployed baseline is a commit, so comparing it against
# anything untracked would judge classes no release ships.  git grep exits 1 on a lawful
# zero-match scan and >1 only on a real fault, so the two are told apart explicitly - a scan
# tool failure must never read as "every shipped class was removed".
scan_worktree() {
	local known status=0
	known="$(git grep -h -o -E \
		'public (sealed )?(partial )?class[[:space:]]+[A-Za-z0-9_]+[[:space:]]*:[[:space:]]*IPart' \
		-- '*.cs' ':(exclude)DevTests' |
		sed -E 's/.*class[[:space:]]+([A-Za-z0-9_]+).*/\1/' |
		LC_ALL=C sort -u | paste -sd'|' -)" || status=$?
	if [ "$status" -gt 1 ]; then
		echo "IPART ABI SCAN FAILED: git grep exited $status" >&2
		exit 2
	fi
	[ -n "$known" ] || return 0
	known="|$known|"
	git ls-files -- '*.cs' ':(exclude)DevTests' |
		LC_ALL=C sort |
		while IFS= read -r file; do
			extract_parts "$known" < "$file"
		done
}

declare -A old_class=() old_fields=() old_write=() old_read=() old_named_write=() old_named_read=()
declare -A new_class=() new_fields=() new_write=() new_read=() new_named_write=() new_named_read=()

while IFS='|' read -r kind class value; do
	case "$kind" in
		C) old_class["$class"]=1 ;;
		F) old_fields["$class"]+="${old_fields[$class]:+$'\n'}$value" ;;
		W) old_write["$class"]=1 ;;
		R) old_read["$class"]=1 ;;
		N) old_named_write["$class"]=1 ;;
		D) old_named_read["$class"]=1 ;;
	esac
done < <(scan_baseline)

while IFS='|' read -r kind class value; do
	case "$kind" in
		C) new_class["$class"]=1 ;;
		F) new_fields["$class"]+="${new_fields[$class]:+$'\n'}$value" ;;
		W) new_write["$class"]=1 ;;
		R) new_read["$class"]=1 ;;
		N) new_named_write["$class"]=1 ;;
		D) new_named_read["$class"]=1 ;;
	esac
done < <(scan_worktree)

# These parts have literal old-wire fixtures/source contracts in DevTests and reviewed custom
# compatibility readers. r_KingdomEnrolled is the named-wire case: both deployed and current
# source must directly retain WriteNamedFields/ReadNamedFields. Its frozen V1 source fixture proves
# the five deployed names; Qud's named reader consumes unknown names and leaves missing names at
# their initializers. Adding a name here is a review event, not an escape hatch.
custom_compat() {
	case "$1" in
		r_KingdomWear|r_KingdomNotice|r_KingdomLabJob|r_KingdomLabRemovalJob|r_KingdomLabEffectLedger|r_KingdomLabRecord|r_KingdomEnrolled)
			return 0 ;;
		*) return 1 ;;
	esac
}

deployed_named_compat() {
	[ "$1" = r_KingdomEnrolled ]
}

failed=0
checked=0
custom=0
while IFS= read -r class; do
	checked=$((checked + 1))
	if [[ -z "${new_class[$class]:-}" ]]; then
		echo "IPART ABI REMOVED: $class" >&2
		failed=1
		continue
	fi
	if custom_compat "$class"; then
		custom=$((custom + 1))
		if [[ -z "${new_write[$class]:-}" || -z "${new_read[$class]:-}" ]]; then
			echo "IPART ABI CUSTOM CONTRACT LOST: $class needs direct Write and Read overrides" >&2
			failed=1
		fi
		if deployed_named_compat "$class" &&
			[[ -z "${old_write[$class]:-}" || -z "${old_read[$class]:-}"
				|| -z "${old_named_write[$class]:-}" || -z "${old_named_read[$class]:-}"
				|| -z "${new_named_write[$class]:-}" || -z "${new_named_read[$class]:-}" ]]; then
			echo "IPART ABI DEPLOYED NAMED CONTRACT LOST: $class" >&2
			failed=1
		fi
		continue
	fi
	before="${old_fields[$class]:-}"
	after="${new_fields[$class]:-}"
	if [[ "$before" != "$after" ]]; then
		echo "IPART ABI POSITIONAL LAYOUT CHANGED: $class" >&2
		diff -u --label "${class}@${BASE_REF}" --label "${class}@worktree" \
			<(printf '%s\n' "$before") <(printf '%s\n' "$after") >&2 || true
		failed=1
	fi
done < <(printf '%s\n' "${!old_class[@]}" | LC_ALL=C sort)

if (( failed != 0 )); then
	echo "IPART ABI FAILED ($checked shipped classes checked)" >&2
	exit 1
fi

echo "IPART ABI CLEAN ($checked shipped classes; $custom custom compatibility contracts)"
