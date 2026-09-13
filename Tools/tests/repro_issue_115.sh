#!/usr/bin/env bash
# Reproduction for issue #115: cleanup_private_entry's post-removal `locate`
# matches by (st_dev, st_ino) only, so an UNRELATED directory that reuses the
# freed inode is reported as the retained exact identity -> exit 5.
set -euo pipefail
HELPER="$(cd "$(dirname "$0")/../.." && pwd)/Tools/atomic_tree_publish.py"
PARENT="$(mktemp -d)"
trap 'rm -rf "$PARENT"' EXIT
exec 9<"$PARENT"
flock -x 9
PARENT_ID="$(python3 -c 'import os,sys;s=os.stat(sys.argv[1]);print(f"{s.st_dev}:{s.st_ino}")' "$PARENT")"
h() { python3 "$HELPER" "$@" --parent "$PARENT" --parent-id "$PARENT_ID" --lock-fd 9; }

mkdir "$PARENT/victim"
VICTIM_ID="$(python3 -c 'import os,sys;s=os.stat(sys.argv[1]);print(f"{s.st_dev}:{s.st_ino}")' "$PARENT/victim")"
VICTIM_INO="${VICTIM_ID#*:}"
echo "tracked entry victim id=$VICTIM_ID"

h inspect --name victim
h remove --kind directory --name victim --expected-id "$VICTIM_ID"
echo "removal done; post-inspect: $(h inspect --name victim)"

# An unrelated concurrent producer (cf. DevTests/KingdomSealStoreTests.cs:153)
# creates directories under the same parent and reuses the freed inode.
FOREIGN=""
for _ in $(seq 1 2000); do
	n="taf-seal-store-$(python3 -c 'import uuid;print(uuid.uuid4().hex)')"
	mkdir "$PARENT/$n"
	ino="$(stat -c %i "$PARENT/$n")"
	if [ "$ino" = "$VICTIM_INO" ]; then FOREIGN="$n"; break; fi
	rmdir "$PARENT/$n"
done
[ -n "$FOREIGN" ] || { echo "INODE NOT REUSED - repro inconclusive" >&2; exit 1; }
echo "foreign entry $FOREIGN reused inode $VICTIM_INO"

LOC="$(h locate --kind directory --expected-id "$VICTIM_ID")"
if [ -n "$LOC" ]; then
	printf 'REPRODUCED: cleanup retained exact identities under %s:\n%s\n' "$PARENT" "$LOC"
	exit 0
fi
echo "NOT REPRODUCED: locate returned nothing" >&2
exit 1
