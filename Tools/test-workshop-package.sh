#!/usr/bin/env bash
# Destructive only inside one mktemp-owned /tmp fixture tree.

set -euo pipefail

SOURCE_REPO="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd -P)"
FIXTURE_ROOT="$(mktemp -d /tmp/taf-workshop-package.XXXXXX)"
BASE="$FIXTURE_ROOT/base"

cleanup() {
	local status=$?
	trap - EXIT
	case "$FIXTURE_ROOT" in
		/tmp/taf-workshop-package.*) find -P "$FIXTURE_ROOT" -depth -delete ;;
		*) echo "refusing unexpected Workshop fixture cleanup path: $FIXTURE_ROOT" >&2; status=1 ;;
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
	[ "$status" -ne 0 ] || {
		echo "$label unexpectedly succeeded" >&2; exit 1; }
	case "$output" in
		*"$needle"*) ;;
		*) echo "$label failed for the wrong reason:" >&2; printf '%s\n' "$output" >&2; exit 1 ;;
	esac
}

clone_case() {
	local name="$1" target
	target="$FIXTURE_ROOT/$name"
	git clone -q -- "$BASE" "$target"
	git -C "$target" config user.name "TAF package harness"
	git -C "$target" config user.email "fixture@example.invalid"
	printf '%s\n' "$target"
}

write_workshop() {
	local repo="$1" visibility="$2"
	python3 - "$repo" "$visibility" <<'PY'
import importlib.util
import sys
from pathlib import Path

root = Path(sys.argv[1])
visibility = sys.argv[2]
spec = importlib.util.spec_from_file_location("workshop_metadata", root / "Tools/workshop_metadata.py")
module = importlib.util.module_from_spec(spec)
assert spec.loader is not None
spec.loader.exec_module(module)
manifest = module.load_manifest(root / "manifest.json", require_preview=True)
data = module.canonical_workshop_data(manifest, 123456789, visibility)
(root / "workshop.json").write_bytes(module.canonical_workshop_bytes(data))
PY
}

write_evidence() {
	local repo="$1" candidate="$2"
	mkdir -p "$repo/docs"
	python3 - "$repo" "$candidate" <<'PY'
import hashlib
import importlib.util
import json
import sys
from pathlib import Path

root = Path(sys.argv[1])
candidate = sys.argv[2]
spec = importlib.util.spec_from_file_location(
    "workshop_metadata_evidence", root / "Tools/workshop_metadata.py"
)
module = importlib.util.module_from_spec(spec)
assert spec.loader is not None
spec.loader.exec_module(module)
private_receipt = root / "docs/PRIVATE_PACKAGE_RECEIPT.sha256"
artifact_root = root / "docs/release-evidence"
artifact_root.mkdir(parents=True, exist_ok=True)
def retain(filename: str, payload: bytes) -> tuple[str, str]:
    path = artifact_root / filename
    path.write_bytes(payload)
    return (
        "docs/release-evidence/" + filename,
        hashlib.sha256(payload).hexdigest(),
    )

def binding(pass_id: str, filename: str, payload=None) -> dict:
    if payload is None:
        payload = (pass_id + "\n").encode("utf-8")
    artifact, digest = retain(filename, payload)
    return {
        "passId": pass_id,
        "artifactRef": artifact,
        "artifactSha256": digest,
    }

pass_ids = list(module.testing_pass_ids(root / "TESTING.md"))
protocol_ref, protocol_digest = retain(
    "numbered-protocols.txt",
    "".join(pass_id + " passed\n" for pass_id in pass_ids).encode("utf-8"),
)
preview_sha = hashlib.sha256((root / "preview.png").read_bytes()).hexdigest()
preview_review_ref, preview_review_digest = retain(
    "final-native-preview-review.txt",
    (
        "Final native preview reviewed against the candidate package.\n"
        "Preview SHA-256: " + preview_sha + "\n"
    ).encode("utf-8"),
)

evidence = {
    "schemaVersion": 4,
    "releaseVersion": "0.2.0",
    "candidateCommit": candidate,
    "gameMarketingVersion": "1.0.5",
    "gameCoreBuild": "2.0.211.51",
    "gameAssemblySha256": "a" * 64,
    "workshopId": 123456789,
    "previewSha256": preview_sha,
    "privatePackageReceiptSha256": hashlib.sha256(private_receipt.read_bytes()).hexdigest(),
    "verification": {
        "nativeCompileLoad": binding(
            "native-compile-load",
            "native-compile-load.txt",
            (
                "native-compile-load\n"
                + "Assembly-CSharp SHA-256: " + "a" * 64 + "\n"
            ).encode("utf-8"),
        ),
        "architectureGallery": binding(
            "architecture-gallery", "architecture-gallery.txt"),
        "controllerAndColor": binding(
            "controller-color-accessibility", "controller-color.txt"),
        "denseCityPerformance": binding(
            "dense-city-performance", "dense-city-performance.csv"),
        "oneSurveyReceipt": binding(
            "one-survey-receipt", "one-survey-receipt.txt"),
        "compatibilityMatrix": binding(
            "compatibility-matrix", "compatibility-matrix.csv"),
        "previewReview": {
            "passId": "final-native-preview-review",
            "artifactRef": preview_review_ref,
            "artifactSha256": preview_review_digest,
            "source": "native-game-screenshot",
            "generativeAssistance": False,
            "previewSha256": preview_sha,
            "capturedBy": "Release Screenshot Capturer",
            "captureUtc": "2026-08-24T00:00:00Z",
            "sourceSave": "Dedicated clean release gallery save",
            "editSummary": "Cropped to a square and resized without generated content.",
            "reviewedBy": "Release Preview Reviewer",
            "completedUtc": "2026-08-24T00:00:00Z",
        },
        "numberedProtocols": {
            "artifactRef": protocol_ref,
            "artifactSha256": protocol_digest,
            "passIds": pass_ids,
            "waivers": [],
        },
    },
    "privateSubscription": {
        "source": "steam-subscription",
        "inventory": "clean",
        "receipt": "clean",
        "loader": "passed",
        "newGame": "passed",
        "saveReload": "passed",
        "oldSave": "passed",
        "representativeFeatures": "passed",
        "playerLog": "clean",
        "localDuplicatesRemoved": True,
        "uploadHiddenFiles": True,
        "testedBy": "Harness Tester",
        "completedUtc": "2026-08-24T00:00:00Z",
    },
}
(root / "docs/RELEASE_EVIDENCE.json").write_text(
    json.dumps(evidence, indent=2) + "\n", encoding="utf-8"
)
PY
}

assert_public_workshop_golden() {
	local path="$1/workshop.json"
	# Golden republished 2026-08-30: canonical_description now wraps every manifest in the
	# reviewed ALPHA/BETA pre-release frame (header, repo links, save-format note), so the
	# fixture's canonical bytes grew from the prior 656-byte golden 99cd43c1....
	[ "$(sha256sum "$path" | cut -d' ' -f1)" = \
		"73477ab64eb01af9e3ccdd5d8adab1582b441adc6e3e8d25081d7c25a9d873ae" ] || {
		echo "Qud workshop.json golden hash changed" >&2; exit 1; }
	python3 - "$path" <<'PY'
import sys
from pathlib import Path

payload = Path(sys.argv[1]).read_bytes()
assert len(payload) == 1199
assert not payload.startswith(b"\xef\xbb\xbf")
assert payload.startswith(b'{\r\n  "WorkshopId": 123456789,\r\n')
assert payload.endswith(b'  "ImagePath": "preview.png"\r\n}')
assert b"\n" not in payload.replace(b"\r\n", b"")
assert not payload.endswith((b"\n", b"\r"))
ordered = [b'"WorkshopId"', b'"Title"', b'"Description"', b'"Tags"', b'"Visibility"', b'"ImagePath"']
positions = [payload.index(field) for field in ordered]
assert positions == sorted(positions)
PY
}

commit_all() {
	local repo="$1" message="$2"
	git -C "$repo" add --all
	git -C "$repo" commit -q -m "$message"
}

freeze_private_candidate() {
	local repo="$1" candidate package
	write_workshop "$repo" 0
	commit_all "$repo" "frozen private package source"
	package="$FIXTURE_ROOT/private-proof-$(basename -- "$repo")"
	"$repo/Tools/workshop-package.sh" --test "$package" >/dev/null
	mkdir -p -- "$repo/docs"
	cp -- "$package.sha256" "$repo/docs/PRIVATE_PACKAGE_RECEIPT.sha256"
	commit_all "$repo" "bind private package receipt"
	candidate="$(git -C "$repo" rev-parse HEAD)"
	printf '%s\n' "$candidate"
}

select_dormant_file() {
	local repo="$1"
	python3 - "$repo/Tools/stage.sh" <<'PY'
import sys
from pathlib import Path

path = Path(sys.argv[1])
text = path.read_text(encoding="utf-8")
old = "ROOT_META=(README.md LICENSE NOTICE CHANGELOG.md manifest.json modconfig.json preview.png workshop.json)"
new = "ROOT_META=(README.md LICENSE NOTICE CHANGELOG.md manifest.json modconfig.json preview.png workshop.json Dormant.dat)"
if text.count(old) != 1:
    raise SystemExit("stage-rule drift fixture could not find ROOT_META")
path.write_text(text.replace(old, new), encoding="utf-8")
PY
}

write_structure_review() {
	local repo="$1" inventory_sha
	inventory_sha="$(
		PYTHONDONTWRITEBYTECODE=1 python3 "$repo/Tools/check-structure.py" \
			--repo-root "$repo" --json \
			| python3 -c 'import json, sys; print(json.load(sys.stdin)["inventorySha256"])'
	)"
	mkdir -p -- "$repo/docs"
	python3 - "$repo/docs/STRUCTURE_REVIEW.json" "$inventory_sha" <<'PY'
import json
import sys
from pathlib import Path

path = Path(sys.argv[1])
inventory_sha = sys.argv[2]
payload = {
    "schemaVersion": 1,
    "inventorySha256": inventory_sha,
    "exceptions": [],
    "reviewedBy": "TAF package harness",
    "completedUtc": "2026-08-27T00:00:00Z",
    "oneResponsibility": {
        "status": "passed",
        "notes": "Fixture review: Core/Test.cs is one one-line runtime declaration.",
    },
    "protocolsAtBoundaries": {
        "status": "passed",
        "notes": "Fixture review: Core/Test.cs has no engine, serialization, API, or extension boundary.",
    },
}
path.write_text(json.dumps(payload, indent=2) + "\n", encoding="utf-8")
PY
}

mkdir -p "$BASE/Tools" "$BASE/Core" "$BASE/Art"
cp -- "$SOURCE_REPO/Tools/stage.sh" "$BASE/Tools/stage.sh"
cp -- "$SOURCE_REPO/Tools/workshop-package.sh" "$BASE/Tools/workshop-package.sh"
cp -- "$SOURCE_REPO/Tools/workshop_metadata.py" "$BASE/Tools/workshop_metadata.py"
cp -- "$SOURCE_REPO/Tools/check-structure.py" "$BASE/Tools/check-structure.py"
cp -- "$SOURCE_REPO/Art/check_wiring.py" "$BASE/Art/check_wiring.py"
cp -- "$SOURCE_REPO/Art/runtime-assets.json" "$BASE/Art/runtime-assets.json"
cp -- "$SOURCE_REPO/TESTING.md" "$BASE/TESTING.md"
chmod +x "$BASE/Tools/stage.sh" "$BASE/Tools/workshop-package.sh" "$BASE/Tools/workshop_metadata.py"
printf '%s\n' '// fixture runtime' > "$BASE/Core/Test.cs"
printf '%s\n' '<objects />' > "$BASE/ObjectBlueprints.xml"
printf '%s\n' '# Fixture' '' '**Status: 0.2.0 public playtest release.**' > "$BASE/README.md"
printf '%s\n' 'fixture license' > "$BASE/LICENSE"
printf '%s\n' '# Changes' '' '## [0.2.0] — 2026-08-24' > "$BASE/CHANGELOG.md"
printf '%s\n' 'Ignored.cs' > "$BASE/.gitignore"
printf '%s\n' '*.json text eol=lf' 'workshop.json -text' '*.sh text eol=lf' '*.py text eol=lf' > "$BASE/.gitattributes"
python3 - "$BASE" <<'PY'
import json
import struct
import sys
import zlib
from pathlib import Path

root = Path(sys.argv[1])
manifest = {
    "id": "r_ThousandAndFirst",
    "title": "The Thousand and First",
    "description": (
        "Found a faction through a water rite, plant and govern settlements, build districts, "
        "grow food, manage water, trade between cities, answer threats, and optionally leave a "
        "legacy across worlds."
    ),
    "version": "0.2.0",
    "author": "AussieWarGod",
    "tags": "Beta,Faction,Settlement,Script",
    "PreviewImage": "preview.png",
}
(root / "manifest.json").write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")

def chunk(kind: bytes, body: bytes) -> bytes:
    return struct.pack(">I", len(body)) + kind + body + struct.pack(">I", zlib.crc32(kind + body) & 0xFFFFFFFF)

raw = b"".join(b"\x00" + b"\x18\x24\x30" * 512 for _ in range(512))
png = (
    b"\x89PNG\r\n\x1a\n"
    + chunk(b"IHDR", struct.pack(">IIBBBBB", 512, 512, 8, 2, 0, 0, 0))
    + chunk(b"IDAT", zlib.compress(raw, 9))
    + chunk(b"IEND", b"")
)
(root / "preview.png").write_bytes(png)
PY

write_structure_review "$BASE"

git -C "$BASE" init -q
git -C "$BASE" config user.name "TAF package harness"
git -C "$BASE" config user.email "fixture@example.invalid"
commit_all "$BASE" "fixture base"

# Exercise the structural PNG parser independently of package staging.
cp "$BASE/preview.png" "$FIXTURE_ROOT/bad-crc.png"
python3 - "$FIXTURE_ROOT/bad-crc.png" <<'PY'
import sys
from pathlib import Path
path = Path(sys.argv[1])
payload = bytearray(path.read_bytes())
payload[32] ^= 1  # Last byte of IHDR CRC.
path.write_bytes(payload)
PY
expect_fail "bad PNG checksum" "bad PNG chunk checksum" \
	python3 "$BASE/Tools/workshop_metadata.py" preview "$FIXTURE_ROOT/bad-crc.png"
cp "$BASE/preview.png" "$FIXTURE_ROOT/trailing.png"
printf 'trailing' >> "$FIXTURE_ROOT/trailing.png"
expect_fail "trailing PNG bytes" "one complete IEND" \
	python3 "$BASE/Tools/workshop_metadata.py" preview "$FIXTURE_ROOT/trailing.png"
cp "$BASE/preview.png" "$FIXTURE_ROOT/wrong-size.png"
python3 - "$FIXTURE_ROOT/wrong-size.png" <<'PY'
import struct
import sys
import zlib
from pathlib import Path
path = Path(sys.argv[1])
payload = bytearray(path.read_bytes())
payload[16:20] = struct.pack(">I", 511)
payload[29:33] = struct.pack(">I", zlib.crc32(payload[12:29]) & 0xFFFFFFFF)
path.write_bytes(payload)
PY
expect_fail "wrong PNG dimensions" "must be 512x512" \
	python3 "$BASE/Tools/workshop_metadata.py" preview "$FIXTURE_ROOT/wrong-size.png"
cp "$BASE/preview.png" "$FIXTURE_ROOT/truncated.png"
truncate -s -5 "$FIXTURE_ROOT/truncated.png"
expect_fail "truncated PNG" "truncated PNG chunk" \
	python3 "$BASE/Tools/workshop_metadata.py" preview "$FIXTURE_ROOT/truncated.png"
cp "$BASE/preview.png" "$FIXTURE_ROOT/interlaced.png"
python3 - "$FIXTURE_ROOT/interlaced.png" <<'PY'
import struct
import sys
import zlib
from pathlib import Path
path = Path(sys.argv[1])
payload = bytearray(path.read_bytes())
payload[28] = 1
payload[29:33] = struct.pack(">I", zlib.crc32(payload[12:29]) & 0xFFFFFFFF)
path.write_bytes(payload)
PY
expect_fail "interlaced PNG" "non-interlaced" \
	python3 "$BASE/Tools/workshop_metadata.py" preview "$FIXTURE_ROOT/interlaced.png"
cp "$BASE/preview.png" "$FIXTURE_ROOT/unknown-critical.png"
python3 - "$FIXTURE_ROOT/unknown-critical.png" <<'PY'
import struct
import sys
import zlib
from pathlib import Path
path = Path(sys.argv[1])
payload = path.read_bytes()
kind = b"ABCD"
chunk = struct.pack(">I", 0) + kind + struct.pack(">I", zlib.crc32(kind) & 0xFFFFFFFF)
path.write_bytes(payload[:33] + chunk + payload[33:])
PY
expect_fail "unknown critical PNG chunk" "unknown critical PNG chunk" \
	python3 "$BASE/Tools/workshop_metadata.py" preview "$FIXTURE_ROOT/unknown-critical.png"
cp "$BASE/preview.png" "$FIXTURE_ROOT/late-palette.png"
python3 - "$FIXTURE_ROOT/late-palette.png" <<'PY'
import struct
import sys
import zlib
from pathlib import Path

path = Path(sys.argv[1])
payload = path.read_bytes()
offset = 8
while payload[offset + 4:offset + 8] != b"IEND":
    offset += 12 + struct.unpack(">I", payload[offset:offset + 4])[0]
kind = b"PLTE"
body = b"\x00\x00\x00"
chunk = struct.pack(">I", len(body)) + kind + body + struct.pack(
    ">I", zlib.crc32(kind + body) & 0xFFFFFFFF
)
path.write_bytes(payload[:offset] + chunk + payload[offset:])
PY
expect_fail "late PNG palette" "palette must precede image data" \
	python3 "$BASE/Tools/workshop_metadata.py" preview "$FIXTURE_ROOT/late-palette.png"
cp "$BASE/preview.png" "$FIXTURE_ROOT/limit.png"
truncate -s 1000000 "$FIXTURE_ROOT/limit.png"
expect_fail "decimal PNG byte limit" "under 1,000,000 bytes" \
	python3 "$BASE/Tools/workshop_metadata.py" preview "$FIXTURE_ROOT/limit.png"
python3 - "$FIXTURE_ROOT/overinflate.png" <<'PY'
import struct
import sys
import zlib
from pathlib import Path

def chunk(kind: bytes, body: bytes) -> bytes:
    return struct.pack(">I", len(body)) + kind + body + struct.pack(">I", zlib.crc32(kind + body) & 0xFFFFFFFF)

raw = (b"\x00" + b"\x00\x00\x00" * 512) * 512 + b"excess decoded bytes"
payload = (
    b"\x89PNG\r\n\x1a\n"
    + chunk(b"IHDR", struct.pack(">IIBBBBB", 512, 512, 8, 2, 0, 0, 0))
    + chunk(b"IDAT", zlib.compress(raw, 9))
    + chunk(b"IEND", b"")
)
Path(sys.argv[1]).write_bytes(payload)
PY
expect_fail "overinflated PNG" "exceeds its declared size" \
	python3 "$BASE/Tools/workshop_metadata.py" preview "$FIXTURE_ROOT/overinflate.png"

# Json.NET escapes exactly these Unicode separators differently from Python; fail closed rather
# than silently producing metadata bytes Qud will rewrite. Surrogates are invalid UTF-8 on both.
for codepoint in 0085 2028 2029 D800; do
	manifest_probe="$FIXTURE_ROOT/manifest-$codepoint.json"
	python3 - "$BASE/manifest.json" "$manifest_probe" "$codepoint" <<'PY'
import json
import sys
from pathlib import Path
source, target, codepoint = sys.argv[1:]
data = json.loads(Path(source).read_text(encoding="utf-8"))
data["description"] = data["description"].replace("plant", "plant" + chr(int(codepoint, 16)), 1)
Path(target).write_text(json.dumps(data, ensure_ascii=True), encoding="ascii")
PY
	if [ "$codepoint" = D800 ]; then expected="surrogate"; else expected="U+$codepoint"; fi
	expect_fail "unsafe JSON codepoint $codepoint" "$expected" \
		python3 "$BASE/Tools/workshop_metadata.py" copy "$manifest_probe"
done

positive="$(clone_case positive-private)"
private_dest="$FIXTURE_ROOT/private-package"
"$positive/Tools/workshop-package.sh" --test "$private_dest" >/dev/null
(
	cd "$private_dest"
	sha256sum -c "$private_dest.sha256" >/dev/null
)
"$positive/Tools/stage.sh" verify "$private_dest" >/dev/null

canonicalize="$(clone_case canonicalize-json)"
printf '%s' '{"WorkshopId":123456789}' > "$canonicalize/workshop.json"
python3 "$canonicalize/Tools/workshop_metadata.py" canonicalize test \
	"$canonicalize/manifest.json" "$canonicalize/workshop.json"
python3 "$canonicalize/Tools/workshop_metadata.py" workshop test \
	"$canonicalize/manifest.json" "$canonicalize/workshop.json"
python3 "$canonicalize/Tools/workshop_metadata.py" canonicalize release \
	"$canonicalize/manifest.json" "$canonicalize/workshop.json"
assert_public_workshop_golden "$canonicalize"
printf '%s' '{"WorkshopId":123456789}' > "$FIXTURE_ROOT/canonicalize-target.json"
ln -s "$FIXTURE_ROOT/canonicalize-target.json" "$FIXTURE_ROOT/canonicalize-link.json"
expect_fail "linked canonicalization target" "regular non-link file" \
	python3 "$canonicalize/Tools/workshop_metadata.py" canonicalize test \
	"$canonicalize/manifest.json" "$FIXTURE_ROOT/canonicalize-link.json"
[ "$(<"$FIXTURE_ROOT/canonicalize-target.json")" = '{"WorkshopId":123456789}' ]

inside="$(clone_case inside-repo)"
expect_fail "inside-repository destination" "inside repository" \
	"$inside/Tools/workshop-package.sh" --test "$inside/package"

preexisting="$(clone_case preexisting)"
mkdir "$FIXTURE_ROOT/preexisting-package"
expect_fail "preexisting destination" "already exists" \
	"$preexisting/Tools/workshop-package.sh" --test "$FIXTURE_ROOT/preexisting-package"

receipt_link="$(clone_case receipt-link)"
printf '%s\n' sentinel > "$FIXTURE_ROOT/receipt-target"
ln -s "$FIXTURE_ROOT/receipt-target" "$FIXTURE_ROOT/receipt-link-package.sha256"
expect_fail "linked receipt" "already exists" \
	"$receipt_link/Tools/workshop-package.sh" --test "$FIXTURE_ROOT/receipt-link-package"
[ "$(<"$FIXTURE_ROOT/receipt-target")" = sentinel ]

linked_parent="$(clone_case linked-parent)"
mkdir "$FIXTURE_ROOT/real-parent"
ln -s "$FIXTURE_ROOT/real-parent" "$FIXTURE_ROOT/linked-parent-path"
expect_fail "linked destination parent" "linked destination parent" \
	"$linked_parent/Tools/workshop-package.sh" --test "$FIXTURE_ROOT/linked-parent-path/package"

# A private direct parent does not help when an ancestor permits another principal to rename that
# parent. Reject the exact non-sticky shared ancestor before mktemp can expose any pathname; the
# hostile wrapper would damage the sentinel and leave a marker if reached.
unsafe_parent_case="$(clone_case unsafe-shared-parent)"
unsafe_ancestor="$FIXTURE_ROOT/unsafe-shared-ancestor"
unsafe_parent="$unsafe_ancestor/private-parent"
mkdir -p "$unsafe_parent"
chmod 0777 "$unsafe_ancestor"
chmod 0700 "$unsafe_parent"
printf '%s\n' 'shared-parent sentinel survives' > "$FIXTURE_ROOT/shared-parent-sentinel"
UNSAFE_PARENT_BIN="$FIXTURE_ROOT/unsafe-parent-bin"
mkdir "$UNSAFE_PARENT_BIN"
printf '%s\n' \
	'#!/usr/bin/env bash' \
	'set -euo pipefail' \
	': > "$TAF_UNSAFE_PARENT_MARKER"' \
	'printf "%s\n" "hostile pre-open" > "$TAF_UNSAFE_PARENT_SENTINEL"' \
	'exec "$TAF_REAL_MKTEMP" "$@"' > "$UNSAFE_PARENT_BIN/mktemp"
chmod +x "$UNSAFE_PARENT_BIN/mktemp"
unsafe_dest="$unsafe_parent/package"
expect_fail "non-sticky shared parent" \
	"unsafe destination ancestor for scratch names: $unsafe_ancestor" \
	env PATH="$UNSAFE_PARENT_BIN:$PATH" TAF_REAL_MKTEMP="$(command -v mktemp)" \
		TAF_UNSAFE_PARENT_MARKER="$FIXTURE_ROOT/unsafe-parent-marker" \
		TAF_UNSAFE_PARENT_SENTINEL="$FIXTURE_ROOT/shared-parent-sentinel" \
		"$unsafe_parent_case/Tools/workshop-package.sh" --test "$unsafe_dest"
[ ! -e "$FIXTURE_ROOT/unsafe-parent-marker" ] \
	&& [ ! -L "$FIXTURE_ROOT/unsafe-parent-marker" ]
[ "$(<"$FIXTURE_ROOT/shared-parent-sentinel")" = "shared-parent sentinel survives" ]
[ ! -e "$unsafe_dest" ] && [ ! -L "$unsafe_dest" ]
[ ! -e "$unsafe_dest.sha256" ] && [ ! -L "$unsafe_dest.sha256" ]
[ -z "$(find -P "$unsafe_parent" -maxdepth 1 \
	-name '.package.scratch.*' -print -quit)" ]

# Sticky shared directories retain the ownership protection expected from /tmp and remain valid
# package parents.
sticky_parent_case="$(clone_case sticky-shared-parent)"
sticky_parent="$FIXTURE_ROOT/sticky-shared-parent-dir"
mkdir "$sticky_parent"
chmod 1777 "$sticky_parent"
sticky_dest="$sticky_parent/package"
"$sticky_parent_case/Tools/workshop-package.sh" --test "$sticky_dest" >/dev/null
[ -d "$sticky_dest" ] && [ -f "$sticky_dest.sha256" ]
[ -z "$(find -P "$sticky_parent" -maxdepth 1 \
	-name '.package.scratch.*' -print -quit)" ]

# In a user namespace, host-root-owned /tmp appears as foreign uid 65534 while the process is uid
# 0. Its sticky bit alone must not authorize scratch naming; the gate explicitly requires owner
# uid current-or-root. Skip only where user namespaces are disabled.
if unshare -Ur true >/dev/null 2>&1; then
	foreign_private_case="$(clone_case foreign-owned-private-ancestor)"
	foreign_private_dest="/usr/.taf-workshop-foreign-owner-$$"
	printf '%s\n' 'foreign-private sentinel survives' \
		> "$FIXTURE_ROOT/foreign-private-sentinel"
	set +e
	foreign_private_output="$(unshare -Ur env \
		PATH="$UNSAFE_PARENT_BIN:$PATH" TAF_REAL_MKTEMP="$(command -v mktemp)" \
		TAF_UNSAFE_PARENT_MARKER="$FIXTURE_ROOT/foreign-private-marker" \
		TAF_UNSAFE_PARENT_SENTINEL="$FIXTURE_ROOT/foreign-private-sentinel" \
		"$foreign_private_case/Tools/workshop-package.sh" --test \
		"$foreign_private_dest" 2>&1)"
	foreign_private_status=$?
	set -e
	[ "$foreign_private_status" -ne 0 ] || {
		echo "foreign-owned private ancestor unexpectedly succeeded" >&2; exit 1; }
	case "$foreign_private_output" in
		*"unsafe destination ancestor for scratch names: /usr (owner uid "*) ;;
		*) printf '%s\n' "$foreign_private_output" >&2; exit 1 ;;
	esac
	[ ! -e "$FIXTURE_ROOT/foreign-private-marker" ] \
		&& [ ! -L "$FIXTURE_ROOT/foreign-private-marker" ]
	[ "$(<"$FIXTURE_ROOT/foreign-private-sentinel")" = \
		"foreign-private sentinel survives" ]
	[ ! -e "$foreign_private_dest" ] && [ ! -L "$foreign_private_dest" ]
	[ ! -e "$foreign_private_dest.sha256" ] && [ ! -L "$foreign_private_dest.sha256" ]
	[ -z "$(find -P /usr -maxdepth 1 \
		-name "..taf-workshop-foreign-owner-$$.scratch.*" -print -quit)" ]

	foreign_owner_case="$(clone_case foreign-owned-sticky-ancestor)"
	foreign_dest="$FIXTURE_ROOT/foreign-owner-package"
	set +e
	foreign_output="$(unshare -Ur \
		"$foreign_owner_case/Tools/workshop-package.sh" --test "$foreign_dest" 2>&1)"
	foreign_status=$?
	set -e
	[ "$foreign_status" -ne 0 ] || {
		echo "foreign-owned sticky ancestor unexpectedly succeeded" >&2; exit 1; }
	case "$foreign_output" in
		*"unsafe destination ancestor for scratch names: /tmp"*) ;;
		*) printf '%s\n' "$foreign_output" >&2; exit 1 ;;
	esac
	[ ! -e "$foreign_dest" ] && [ ! -L "$foreign_dest" ]
	[ ! -e "$foreign_dest.sha256" ] && [ ! -L "$foreign_dest.sha256" ]
	[ -z "$(find -P "$FIXTURE_ROOT" -maxdepth 1 \
		-name '.foreign-owner-package.scratch.*' -print -quit)" ]
else
	echo "FOREIGN STICKY-OWNER FIXTURE SKIPPED: user namespace unavailable" >&2
fi

# A bind-mounted spelling can be physically inside the repository without any lexical overlap.
# Where unprivileged mount namespaces are available, prove the dev:inode check catches it and the
# just-created empty alias directory is removed from the repository.
mkdir "$FIXTURE_ROOT/mount-probe-source" "$FIXTURE_ROOT/mount-probe-target"
if unshare -Urnm bash -c 'mount --bind "$1" "$2" && umount "$2"' \
		_ "$FIXTURE_ROOT/mount-probe-source" "$FIXTURE_ROOT/mount-probe-target" \
		>/dev/null 2>&1; then
	aliased_build="$(clone_case aliased-build)"
	mkdir "$FIXTURE_ROOT/aliased-build-parent"
	set +e
	alias_output="$(unshare -Urnm bash -c '
		mount --bind "$1" "$2"
		"$1/Tools/workshop-package.sh" --test "$2/alias-package"
	' _ "$aliased_build" "$FIXTURE_ROOT/aliased-build-parent" 2>&1)"
	alias_status=$?
	set -e
	[ "$alias_status" -ne 0 ]
	case "$alias_output" in
		*"destination parent aliases repository"*) ;;
		*"unsafe destination ancestor for scratch names: /tmp"*)
			echo "BIND-ALIAS PACKAGE FIXTURE SKIPPED: /tmp is foreign-owned in user namespace" >&2 ;;
		*) printf '%s\n' "$alias_output" >&2; exit 1 ;;
	esac
	[ ! -e "$aliased_build/alias-package" ] && [ ! -L "$aliased_build/alias-package" ]
	[ -z "$(find -P "$aliased_build" -maxdepth 1 -name '.alias-package.scratch.*' -print -quit)" ]
	[ -z "$(git -C "$aliased_build" status --porcelain=v1 --untracked-files=all)" ]
else
	echo "BIND-ALIAS PACKAGE FIXTURE SKIPPED: unprivileged mount namespace unavailable" >&2
fi

# A PATH wrapper can replace a scratch pathname after its already-open writer starts. The next
# phase must reject the link before reopening it, and tree cleanup must never follow it to the
# external sentinel.
scratch_linked="$(clone_case scratch-file-symlink-race)"
SCRATCH_LINK_BIN="$FIXTURE_ROOT/scratch-link-bin"
mkdir "$SCRATCH_LINK_BIN"
printf '%s\n' 'external sentinel survives' > "$FIXTURE_ROOT/scratch-link-sentinel"
printf '%s\n' \
	'#!/usr/bin/env bash' \
	'set -euo pipefail' \
	'if [ ! -e "$TAF_SCRATCH_LINK_MARKER" ]; then' \
	'  shopt -s nullglob' \
	'  for path in "$TAF_SCRATCH_PARENT"/.scratch-link-package.scratch.*/list.*; do' \
	'    "$TAF_REAL_MV" -- "$path" "$path.owned"' \
	'    ln -s -- "$TAF_SCRATCH_SENTINEL" "$path"' \
	'    : > "$TAF_SCRATCH_LINK_MARKER"' \
	'    break' \
	'  done' \
	'fi' \
	'exec "$TAF_REAL_GIT" "$@"' > "$SCRATCH_LINK_BIN/git"
chmod +x "$SCRATCH_LINK_BIN/git"
expect_fail "linked scratch file race" "private scratch contains a link" \
	env PATH="$SCRATCH_LINK_BIN:$PATH" TAF_REAL_GIT="$(command -v git)" \
		TAF_REAL_MV="$(command -v mv)" TAF_SCRATCH_PARENT="$FIXTURE_ROOT" \
		TAF_SCRATCH_LINK_MARKER="$FIXTURE_ROOT/scratch-link-marker" \
		TAF_SCRATCH_SENTINEL="$FIXTURE_ROOT/scratch-link-sentinel" \
		"$scratch_linked/Tools/workshop-package.sh" --test \
		"$FIXTURE_ROOT/scratch-link-package"
[ "$(<"$FIXTURE_ROOT/scratch-link-sentinel")" = "external sentinel survives" ]
[ ! -e "$FIXTURE_ROOT/scratch-link-package" ] \
	&& [ ! -L "$FIXTURE_ROOT/scratch-link-package" ]
[ ! -e "$FIXTURE_ROOT/scratch-link-package.sha256" ] \
	&& [ ! -L "$FIXTURE_ROOT/scratch-link-package.sha256" ]
[ -z "$(find -P "$FIXTURE_ROOT" -maxdepth 1 \
	-name '.scratch-link-package.scratch.*' -print -quit)" ]

# A regular-file replacement is less visible than a symlink. Its new inode must still fail the
# registered scratch-file identity proof before that pathname is reused.
scratch_replaced="$(clone_case scratch-file-replacement-race)"
SCRATCH_REPLACE_BIN="$FIXTURE_ROOT/scratch-replace-bin"
mkdir "$SCRATCH_REPLACE_BIN"
printf '%s\n' \
	'#!/usr/bin/env bash' \
	'set -euo pipefail' \
	'if [ ! -e "$TAF_SCRATCH_REPLACE_MARKER" ]; then' \
	'  shopt -s nullglob' \
	'  for path in "$TAF_SCRATCH_PARENT"/.scratch-replace-package.scratch.*/list.*; do' \
	'    "$TAF_REAL_MV" -- "$path" "$path.owned"' \
	'    printf "%s\n" "attacker replacement" > "$path"' \
	'    : > "$TAF_SCRATCH_REPLACE_MARKER"' \
	'    break' \
	'  done' \
	'fi' \
	'exec "$TAF_REAL_GIT" "$@"' > "$SCRATCH_REPLACE_BIN/git"
chmod +x "$SCRATCH_REPLACE_BIN/git"
expect_fail "replaced scratch file race" "private scratch file identity changed" \
	env PATH="$SCRATCH_REPLACE_BIN:$PATH" TAF_REAL_GIT="$(command -v git)" \
		TAF_REAL_MV="$(command -v mv)" TAF_SCRATCH_PARENT="$FIXTURE_ROOT" \
		TAF_SCRATCH_REPLACE_MARKER="$FIXTURE_ROOT/scratch-replace-marker" \
		"$scratch_replaced/Tools/workshop-package.sh" --test \
		"$FIXTURE_ROOT/scratch-replace-package"
[ ! -e "$FIXTURE_ROOT/scratch-replace-package" ] \
	&& [ ! -L "$FIXTURE_ROOT/scratch-replace-package" ]
[ ! -e "$FIXTURE_ROOT/scratch-replace-package.sha256" ] \
	&& [ ! -L "$FIXTURE_ROOT/scratch-replace-package.sha256" ]
[ -z "$(find -P "$FIXTURE_ROOT" -maxdepth 1 \
	-name '.scratch-replace-package.scratch.*' -print -quit)" ]

# Rollback identities are armed before both no-clobber renames. TERM immediately after either
# successful rename must remove only this invocation's artifacts and its private scratch tree.
SIGNAL_BIN="$FIXTURE_ROOT/signal-bin"
mkdir "$SIGNAL_BIN"
printf '%s\n' \
	'#!/usr/bin/env bash' \
	'set -euo pipefail' \
	'count=0' \
	'[ ! -f "$TAF_SIGNAL_COUNT" ] || count="$(<"$TAF_SIGNAL_COUNT")"' \
	'count=$((count + 1))' \
	'printf "%s\n" "$count" > "$TAF_SIGNAL_COUNT"' \
	'"$TAF_REAL_MV" "$@"' \
	'if [ "$count" -eq "$TAF_SIGNAL_AFTER" ]; then' \
	'  kill -TERM "$PPID"' \
	'fi' > "$SIGNAL_BIN/mv"
chmod +x "$SIGNAL_BIN/mv"
for signal_after in 1 2; do
	signal_case="$(clone_case "publication-signal-$signal_after")"
	signal_dest="$FIXTURE_ROOT/signal-$signal_after-package"
	set +e
	env PATH="$SIGNAL_BIN:$PATH" TAF_REAL_MV="$(command -v mv)" \
		TAF_SIGNAL_COUNT="$FIXTURE_ROOT/signal-$signal_after-count" \
		TAF_SIGNAL_AFTER="$signal_after" \
		"$signal_case/Tools/workshop-package.sh" --test "$signal_dest" \
		>/dev/null 2>&1
	signal_status=$?
	set -e
	[ "$signal_status" -ne 0 ] || {
		echo "publication signal $signal_after unexpectedly succeeded" >&2; exit 1; }
	[ ! -e "$signal_dest" ] && [ ! -L "$signal_dest" ]
	[ ! -e "$signal_dest.sha256" ] && [ ! -L "$signal_dest.sha256" ]
	[ -z "$(find -P "$FIXTURE_ROOT" -maxdepth 1 \
		-name ".signal-$signal_after-package.scratch.*" -print -quit)" ]
done

# If a receipt appears between the two no-clobber moves, cleanup removes only this invocation's
# published package. The racing receipt and its bytes must survive.
racing="$(clone_case receipt-publication-race)"
RACE_BIN="$FIXTURE_ROOT/race-bin"
mkdir "$RACE_BIN"
printf '%s\n' \
	'#!/usr/bin/env bash' \
	'set -euo pipefail' \
	'count=0' \
	'[ ! -f "$TAF_RACE_COUNT" ] || count="$(<"$TAF_RACE_COUNT")"' \
	'count=$((count + 1))' \
	'printf "%s\n" "$count" > "$TAF_RACE_COUNT"' \
	'if [ "$count" -eq 2 ]; then' \
	'  printf "%s\n" "racer receipt survives" > "$TAF_RACE_RECEIPT"' \
	'fi' \
	'exec "$TAF_REAL_MV" "$@"' > "$RACE_BIN/mv"
chmod +x "$RACE_BIN/mv"
race_dest="$FIXTURE_ROOT/race-package"
expect_fail "receipt publication race" "package receipt appeared during publication" \
	env PATH="$RACE_BIN:$PATH" TAF_REAL_MV="$(command -v mv)" \
		TAF_RACE_COUNT="$FIXTURE_ROOT/race-mv-count" \
		TAF_RACE_RECEIPT="$race_dest.sha256" \
		"$racing/Tools/workshop-package.sh" --test "$race_dest"
[ ! -e "$race_dest" ] && [ ! -L "$race_dest" ]
[ "$(<"$race_dest.sha256")" = "racer receipt survives" ]
[ -z "$(find -P "$FIXTURE_ROOT" -maxdepth 1 -name '.race-package.scratch.*' -print -quit)" ]

# The second move runs after the package directory is public. Moving that owned directory aside
# and replacing its path must fail the post-receipt device/inode proof. Cleanup must preserve the
# racing replacement because it does not own that inode.
destination_swap="$(clone_case destination-post-publication-swap)"
DEST_SWAP_BIN="$FIXTURE_ROOT/destination-swap-bin"
mkdir "$DEST_SWAP_BIN"
printf '%s\n' \
	'#!/usr/bin/env bash' \
	'set -euo pipefail' \
	'count=0' \
	'[ ! -f "$TAF_SWAP_COUNT" ] || count="$(<"$TAF_SWAP_COUNT")"' \
	'count=$((count + 1))' \
	'printf "%s\n" "$count" > "$TAF_SWAP_COUNT"' \
	'if [ "$count" -eq 2 ]; then' \
	'  "$TAF_REAL_MV" -T -- "$TAF_SWAP_DEST" "$TAF_SWAP_MOVED"' \
	'  mkdir -- "$TAF_SWAP_DEST"' \
	'  printf "%s\n" "racer replacement survives" > "$TAF_SWAP_DEST/racer-sentinel"' \
	'fi' \
	'exec "$TAF_REAL_MV" "$@"' > "$DEST_SWAP_BIN/mv"
chmod +x "$DEST_SWAP_BIN/mv"
swap_dest="$FIXTURE_ROOT/destination-swap-package"
swap_moved="$FIXTURE_ROOT/destination-swap-owned-package"
expect_fail "destination changed after receipt publication" \
	"published package identity changed after receipt publication" \
	env PATH="$DEST_SWAP_BIN:$PATH" TAF_REAL_MV="$(command -v mv)" \
		TAF_SWAP_COUNT="$FIXTURE_ROOT/destination-swap-mv-count" \
		TAF_SWAP_DEST="$swap_dest" TAF_SWAP_MOVED="$swap_moved" \
		"$destination_swap/Tools/workshop-package.sh" --test "$swap_dest"
[ -d "$swap_moved" ] && [ ! -L "$swap_moved" ]
[ "$(<"$swap_dest/racer-sentinel")" = "racer replacement survives" ]
[ ! -e "$swap_dest.sha256" ] && [ ! -L "$swap_dest.sha256" ]
[ -z "$(find -P "$FIXTURE_ROOT" -maxdepth 1 \
	-name '.destination-swap-package.scratch.*' -print -quit)" ]

# A same-inode edit during the second move bypasses destination identity checks. Exact Git blob
# and mode validation after receipt publication must catch it, then owned cleanup removes both
# published artifacts.
content_mutation="$(clone_case content-post-publication-mutation)"
CONTENT_MUTATION_BIN="$FIXTURE_ROOT/content-mutation-bin"
mkdir "$CONTENT_MUTATION_BIN"
printf '%s\n' \
	'#!/usr/bin/env bash' \
	'set -euo pipefail' \
	'count=0' \
	'[ ! -f "$TAF_MUTATION_COUNT" ] || count="$(<"$TAF_MUTATION_COUNT")"' \
	'count=$((count + 1))' \
	'printf "%s\n" "$count" > "$TAF_MUTATION_COUNT"' \
	'if [ "$count" -eq 2 ]; then' \
	'  printf "%s\n" "// publication racer" >> "$TAF_MUTATION_DEST/Core/Test.cs"' \
	'fi' \
	'exec "$TAF_REAL_MV" "$@"' > "$CONTENT_MUTATION_BIN/mv"
chmod +x "$CONTENT_MUTATION_BIN/mv"
mutation_dest="$FIXTURE_ROOT/content-mutation-package"
expect_fail "runtime bytes changed after receipt publication" \
	"published package bytes changed after receipt publication: Core/Test.cs" \
	env PATH="$CONTENT_MUTATION_BIN:$PATH" TAF_REAL_MV="$(command -v mv)" \
		TAF_MUTATION_COUNT="$FIXTURE_ROOT/content-mutation-mv-count" \
		TAF_MUTATION_DEST="$mutation_dest" \
		"$content_mutation/Tools/workshop-package.sh" --test "$mutation_dest"
[ ! -e "$mutation_dest" ] && [ ! -L "$mutation_dest" ]
[ ! -e "$mutation_dest.sha256" ] && [ ! -L "$mutation_dest.sha256" ]
[ -z "$(find -P "$FIXTURE_ROOT" -maxdepth 1 \
	-name '.content-mutation-package.scratch.*' -print -quit)" ]

ignored="$(clone_case ignored-source)"
printf '%s\n' '// ignored but stage-selected' > "$ignored/Ignored.cs"
[ -z "$(git -C "$ignored" status --porcelain=v1 --untracked-files=all)" ]
expect_fail "ignored staged source" "worktree runtime inventory differs from HEAD" \
	"$ignored/Tools/workshop-package.sh" --test "$FIXTURE_ROOT/ignored-package"

runtime_raster="$(clone_case runtime-raster)"
mkdir "$runtime_raster/Textures"
cp "$runtime_raster/preview.png" "$runtime_raster/Textures/forbidden.PnG"
commit_all "$runtime_raster" "add case-variant runtime raster"
expect_fail "case-insensitive runtime raster" \
	"bundled runtime art is absent from provenance manifest: Textures/forbidden.PnG" \
	"$runtime_raster/Tools/workshop-package.sh" --test "$FIXTURE_ROOT/runtime-raster-package"

allowlisted_raster="$(clone_case allowlisted-runtime-raster)"
mkdir -p "$allowlisted_raster/Textures" "$allowlisted_raster/Art/Sources"
cp "$allowlisted_raster/preview.png" "$allowlisted_raster/Textures/fixture.png"
cp "$allowlisted_raster/preview.png" "$allowlisted_raster/Art/Sources/fixture.png"
python3 - "$allowlisted_raster" <<'PY'
import hashlib
import json
import sys
from pathlib import Path

root = Path(sys.argv[1])
runtime = root / "Textures/fixture.png"
manifest = {
    "schema": 1,
    "assets": [{
        "tile": "ThousandAndFirst/fixture.png",
        "path": "Textures/fixture.png",
        "sha256": hashlib.sha256(runtime.read_bytes()).hexdigest(),
        "creator": "TAF package harness",
        "created": "2026-08-26",
        "license": "test fixture only",
        "source": "Art/Sources/fixture.png",
        "method": "copied deterministic fixture bytes",
        "fallback": "tiles/sw_wall.bmp",
        "review": "approved by the package harness",
    }],
}
(root / "Art/runtime-assets.json").write_text(
    json.dumps(manifest, indent=2) + "\n", encoding="utf-8"
)
PY
commit_all "$allowlisted_raster" "add allowlisted runtime raster"
allowlisted_dest="$FIXTURE_ROOT/allowlisted-runtime-raster-package"
"$allowlisted_raster/Tools/workshop-package.sh" --test "$allowlisted_dest" >/dev/null
[ -f "$allowlisted_dest/Textures/fixture.png" ]
"$allowlisted_raster/Tools/stage.sh" verify "$allowlisted_dest" >/dev/null

oversized="$(clone_case oversized-description)"
python3 - "$oversized/manifest.json" <<'PY'
import json
import sys
from pathlib import Path

path = Path(sys.argv[1])
data = json.loads(path.read_text(encoding="utf-8"))
data["description"] = (
    "Found settlements and optionally preserve a legacy across worlds. " + "x" * 8000
)
path.write_text(json.dumps(data, indent=2) + "\n", encoding="utf-8")
PY
commit_all "$oversized" "oversized canonical description"
expect_fail "oversized canonical text without workshop.json" \
	"Workshop Description must be nonempty and under 8000 UTF-8 bytes" \
	"$oversized/Tools/workshop-package.sh" --test "$FIXTURE_ROOT/oversized-package"

malformed_manifest="$(clone_case malformed-manifest)"
printf '%s\n' '{"id":' > "$malformed_manifest/manifest.json"
commit_all "$malformed_manifest" "malformed manifest"
expect_fail "malformed manifest from HEAD" "cannot read manifest.json" \
	"$malformed_manifest/Tools/workshop-package.sh" --test "$FIXTURE_ROOT/malformed-package"

corrupt="$(clone_case corrupt-preview)"
printf '%s\n' 'not a png' > "$corrupt/preview.png"
commit_all "$corrupt" "corrupt preview"
expect_fail "corrupt preview" "not a PNG" \
	"$corrupt/Tools/workshop-package.sh" --test "$FIXTURE_ROOT/corrupt-package"

linked_preview="$(clone_case linked-preview)"
unlink -- "$linked_preview/preview.png"
ln -s "$BASE/preview.png" "$linked_preview/preview.png"
commit_all "$linked_preview" "link preview"
expect_fail "linked preview" "not a regular non-link file: preview.png" \
	"$linked_preview/Tools/workshop-package.sh" --test "$FIXTURE_ROOT/linked-preview-package"

bad_private="$(clone_case bad-private-metadata)"
write_workshop "$bad_private" 2
commit_all "$bad_private" "wrong private visibility"
expect_fail "wrong private metadata" "Visibility must exactly match" \
	"$bad_private/Tools/workshop-package.sh" --test "$FIXTURE_ROOT/bad-private-package"

malformed_workshop="$(clone_case malformed-workshop-json)"
printf '%s\n' '{"WorkshopId":' > "$malformed_workshop/workshop.json"
commit_all "$malformed_workshop" "malformed workshop metadata"
expect_fail "malformed workshop metadata" "cannot read workshop.json" \
	"$malformed_workshop/Tools/workshop-package.sh" --test \
	"$FIXTURE_ROOT/malformed-workshop-package"

lf_json="$(clone_case lf-workshop-json)"
write_workshop "$lf_json" 0
python3 - "$lf_json/workshop.json" <<'PY'
import sys
from pathlib import Path
path = Path(sys.argv[1])
path.write_bytes(path.read_bytes().replace(b"\r\n", b"\n"))
PY
commit_all "$lf_json" "normalize workshop json"
expect_fail "LF workshop metadata" "canonical Windows serializer output" \
	"$lf_json/Tools/workshop-package.sh" --test "$FIXTURE_ROOT/lf-json-package"

bom_json="$(clone_case bom-workshop-json)"
write_workshop "$bom_json" 0
python3 - "$bom_json/workshop.json" <<'PY'
import sys
from pathlib import Path
path = Path(sys.argv[1])
path.write_bytes(b"\xef\xbb\xbf" + path.read_bytes())
PY
commit_all "$bom_json" "add workshop json bom"
expect_fail "BOM workshop metadata" "canonical Windows serializer output" \
	"$bom_json/Tools/workshop-package.sh" --test "$FIXTURE_ROOT/bom-json-package"

trailing_json="$(clone_case trailing-workshop-json)"
write_workshop "$trailing_json" 0
python3 - "$trailing_json/workshop.json" <<'PY'
import sys
from pathlib import Path
path = Path(sys.argv[1])
path.write_bytes(path.read_bytes() + b"\r\n")
PY
commit_all "$trailing_json" "append workshop json newline"
expect_fail "trailing workshop metadata" "canonical Windows serializer output" \
	"$trailing_json/Tools/workshop-package.sh" --test "$FIXTURE_ROOT/trailing-json-package"

pending_evidence="$(clone_case pending-release-evidence)"
pending_candidate="$(freeze_private_candidate "$pending_evidence")"
write_workshop "$pending_evidence" 2
write_evidence "$pending_evidence" "$pending_candidate"
printf '%s\n' '# Fixture' 'Status: not once run in the live game.' > "$pending_evidence/README.md"
printf '%s\n' '# Changes' '## [Unreleased] — 0.2.0 in progress' > "$pending_evidence/CHANGELOG.md"
commit_all "$pending_evidence" "leave release evidence pending"
expect_fail "pending release evidence" "version-bound release status" \
	"$pending_evidence/Tools/workshop-package.sh" --release "$FIXTURE_ROOT/pending-evidence-package"

missing_evidence="$(clone_case missing-release-evidence)"
freeze_private_candidate "$missing_evidence" >/dev/null
write_workshop "$missing_evidence" 2
commit_all "$missing_evidence" "public metadata without evidence"
expect_fail "missing structured release evidence" "RELEASE_EVIDENCE.json" \
	"$missing_evidence/Tools/workshop-package.sh" --release "$FIXTURE_ROOT/missing-evidence-package"

# Ignored proof files are clean to `git status`, but they are not release evidence. The package
# must enumerate refs from the committed record and require every referenced byte as a HEAD blob.
ignored_evidence_artifacts="$(clone_case ignored-release-evidence-artifacts)"
ignored_evidence_candidate="$(freeze_private_candidate "$ignored_evidence_artifacts")"
write_workshop "$ignored_evidence_artifacts" 2
printf '%s\n' 'docs/release-evidence/' >> "$ignored_evidence_artifacts/.gitignore"
write_evidence "$ignored_evidence_artifacts" "$ignored_evidence_candidate"
commit_all "$ignored_evidence_artifacts" "public metadata with ignored evidence artifacts"
git -C "$ignored_evidence_artifacts" tag -a v0.2.0 -m "fixture release"
[ -z "$(git -C "$ignored_evidence_artifacts" ls-tree -r --name-only HEAD -- \
	'docs/release-evidence')" ]
[ -z "$(git -C "$ignored_evidence_artifacts" status --porcelain=v1 --untracked-files=all)" ]
expect_fail "ignored release evidence artifacts" \
	"release evidence artifact is absent from HEAD" \
	"$ignored_evidence_artifacts/Tools/workshop-package.sh" --release \
	"$FIXTURE_ROOT/ignored-evidence-artifacts-package"

# The semantic ledger is equally load-bearing. A retained-but-ignored worktree copy cannot sign a
# release after its index entry is removed.
ignored_structure="$(clone_case ignored-structure-review)"
ignored_structure_candidate="$(freeze_private_candidate "$ignored_structure")"
write_workshop "$ignored_structure" 2
write_evidence "$ignored_structure" "$ignored_structure_candidate"
printf '%s\n' 'docs/STRUCTURE_REVIEW.json' >> "$ignored_structure/.gitignore"
git -C "$ignored_structure" rm -q --cached docs/STRUCTURE_REVIEW.json
commit_all "$ignored_structure" "public metadata with ignored structural review"
git -C "$ignored_structure" tag -a v0.2.0 -m "fixture release"
[ -z "$(git -C "$ignored_structure" ls-tree -r --name-only HEAD -- \
	'docs/STRUCTURE_REVIEW.json')" ]
[ -z "$(git -C "$ignored_structure" status --porcelain=v1 --untracked-files=all)" ]
expect_fail "ignored structural review" \
	"release package requires exact-inventory semantic review in HEAD" \
	"$ignored_structure/Tools/workshop-package.sh" --release \
	"$FIXTURE_ROOT/ignored-structure-package"

# Numbered protocols belong to the exact subscribed candidate, not a later shortened definition.
testing_drift="$(clone_case candidate-testing-drift)"
testing_drift_candidate="$(freeze_private_candidate "$testing_drift")"
python3 - "$testing_drift/TESTING.md" <<'PY'
import re
import sys
from pathlib import Path

path = Path(sys.argv[1])
lines = path.read_text(encoding="utf-8-sig").splitlines()
for index in range(len(lines) - 1, -1, -1):
    fields = lines[index].lstrip().split("|", 2)
    if len(fields) >= 3 and re.fullmatch(r"[0-9]+[a-z0-9]*(?:\.[0-9]+)?", fields[1].strip()):
        del lines[index]
        break
else:
    raise SystemExit("candidate TESTING drift fixture found no numbered row")
path.write_text("\n".join(lines) + "\n", encoding="utf-8")
PY
write_workshop "$testing_drift" 2
write_evidence "$testing_drift" "$testing_drift_candidate"
commit_all "$testing_drift" "shorten protocols after private subscription"
git -C "$testing_drift" tag -a v0.2.0 -m "fixture release"
expect_fail "candidate TESTING drift" \
	"release TESTING.md differs from subscribed private candidate" \
	"$testing_drift/Tools/workshop-package.sh" --release \
	"$FIXTURE_ROOT/testing-drift-package"

wrong_evidence="$(clone_case wrong-release-evidence)"
wrong_candidate="$(freeze_private_candidate "$wrong_evidence")"
write_workshop "$wrong_evidence" 2
write_evidence "$wrong_evidence" "$wrong_candidate"
python3 - "$wrong_evidence/docs/RELEASE_EVIDENCE.json" <<'PY'
import json
import sys
from pathlib import Path
path = Path(sys.argv[1])
data = json.loads(path.read_text(encoding="utf-8"))
data["releaseVersion"] = "9.9.9"
path.write_text(json.dumps(data, indent=2) + "\n", encoding="utf-8")
PY
commit_all "$wrong_evidence" "wrong-version release evidence"
expect_fail "wrong-version structured release evidence" "version must match manifest" \
	"$wrong_evidence/Tools/workshop-package.sh" --release "$FIXTURE_ROOT/wrong-evidence-package"

numeric_evidence="$(clone_case numeric-release-evidence)"
numeric_candidate="$(freeze_private_candidate "$numeric_evidence")"
write_workshop "$numeric_evidence" 2
write_evidence "$numeric_evidence" "$numeric_candidate"
python3 - "$numeric_evidence/docs/RELEASE_EVIDENCE.json" <<'PY'
import json
import sys
from pathlib import Path

path = Path(sys.argv[1])
data = json.loads(path.read_text(encoding="utf-8"))
data["schemaVersion"] = 4.0
path.write_text(json.dumps(data, indent=2) + "\n", encoding="utf-8")
PY
commit_all "$numeric_evidence" "numeric evidence types"
expect_fail "floating-point release evidence" "schemaVersion must be 4" \
	"$numeric_evidence/Tools/workshop-package.sh" --release \
	"$FIXTURE_ROOT/numeric-evidence-package"

assembly_mismatch="$(clone_case mismatched-game-assembly-receipt)"
assembly_mismatch_candidate="$(freeze_private_candidate "$assembly_mismatch")"
write_workshop "$assembly_mismatch" 2
write_evidence "$assembly_mismatch" "$assembly_mismatch_candidate"
python3 - "$assembly_mismatch/docs/RELEASE_EVIDENCE.json" <<'PY'
import json
import sys
from pathlib import Path

path = Path(sys.argv[1])
data = json.loads(path.read_text(encoding="utf-8"))
data["gameAssemblySha256"] = "b" * 64
path.write_text(json.dumps(data, indent=2) + "\n", encoding="utf-8")
PY
commit_all "$assembly_mismatch" "mismatch game assembly receipt"
expect_fail "mismatched game assembly receipt" \
	"gameAssemblySha256 must match the unique Assembly-CSharp SHA-256 receipt in verification.nativeCompileLoad" \
	"$assembly_mismatch/Tools/workshop-package.sh" --release \
	"$FIXTURE_ROOT/assembly-mismatch-package"

numeric_id_evidence="$(clone_case numeric-workshop-id-evidence)"
numeric_id_candidate="$(freeze_private_candidate "$numeric_id_evidence")"
write_workshop "$numeric_id_evidence" 2
write_evidence "$numeric_id_evidence" "$numeric_id_candidate"
python3 - "$numeric_id_evidence/docs/RELEASE_EVIDENCE.json" <<'PY'
import json
import sys
from pathlib import Path

path = Path(sys.argv[1])
data = json.loads(path.read_text(encoding="utf-8"))
data["workshopId"] = 123456789.0
path.write_text(json.dumps(data, indent=2) + "\n", encoding="utf-8")
PY
commit_all "$numeric_id_evidence" "floating-point evidence Workshop ID"
expect_fail "floating-point evidence Workshop ID" "workshopId must match workshop.json" \
	"$numeric_id_evidence/Tools/workshop-package.sh" --release \
	"$FIXTURE_ROOT/numeric-id-evidence-package"

boolean_evidence="$(clone_case numeric-boolean-evidence)"
boolean_candidate="$(freeze_private_candidate "$boolean_evidence")"
write_workshop "$boolean_evidence" 2
write_evidence "$boolean_evidence" "$boolean_candidate"
python3 - "$boolean_evidence/docs/RELEASE_EVIDENCE.json" <<'PY'
import json
import sys
from pathlib import Path

path = Path(sys.argv[1])
data = json.loads(path.read_text(encoding="utf-8"))
data["privateSubscription"]["localDuplicatesRemoved"] = 1
data["privateSubscription"]["uploadHiddenFiles"] = 1
path.write_text(json.dumps(data, indent=2) + "\n", encoding="utf-8")
PY
commit_all "$boolean_evidence" "numeric evidence booleans"
expect_fail "numeric release evidence booleans" \
	"privateSubscription.localDuplicatesRemoved must be True" \
	"$boolean_evidence/Tools/workshop-package.sh" --release \
	"$FIXTURE_ROOT/boolean-evidence-package"

missing_verification="$(clone_case missing-verification-lane)"
missing_verification_candidate="$(freeze_private_candidate "$missing_verification")"
write_workshop "$missing_verification" 2
write_evidence "$missing_verification" "$missing_verification_candidate"
python3 - "$missing_verification/docs/RELEASE_EVIDENCE.json" <<'PY'
import json
import sys
from pathlib import Path

path = Path(sys.argv[1])
data = json.loads(path.read_text(encoding="utf-8"))
del data["verification"]["oneSurveyReceipt"]
path.write_text(json.dumps(data, indent=2) + "\n", encoding="utf-8")
PY
commit_all "$missing_verification" "missing verification lane"
expect_fail "missing verification lane" \
	"verification fields must exactly match schema version 4" \
	"$missing_verification/Tools/workshop-package.sh" --release \
	"$FIXTURE_ROOT/missing-verification-package"

bad_artifact="$(clone_case invalid-verification-artifact)"
bad_artifact_candidate="$(freeze_private_candidate "$bad_artifact")"
write_workshop "$bad_artifact" 2
write_evidence "$bad_artifact" "$bad_artifact_candidate"
python3 - "$bad_artifact/docs/RELEASE_EVIDENCE.json" <<'PY'
import json
import sys
from pathlib import Path

path = Path(sys.argv[1])
data = json.loads(path.read_text(encoding="utf-8"))
data["verification"]["architectureGallery"]["artifactSha256"] = "0" * 64
path.write_text(json.dumps(data, indent=2) + "\n", encoding="utf-8")
PY
commit_all "$bad_artifact" "invalid verification artifact hash"
expect_fail "invalid verification artifact hash" \
	"verification.architectureGallery.artifactSha256 must be a nonzero lowercase SHA-256" \
	"$bad_artifact/Tools/workshop-package.sh" --release \
	"$FIXTURE_ROOT/bad-artifact-package"

drifted_artifact="$(clone_case drifted-verification-artifact)"
drifted_artifact_candidate="$(freeze_private_candidate "$drifted_artifact")"
write_workshop "$drifted_artifact" 2
write_evidence "$drifted_artifact" "$drifted_artifact_candidate"
printf '%s\n' 'changed after the recorded pass' > \
	"$drifted_artifact/docs/release-evidence/architecture-gallery.txt"
commit_all "$drifted_artifact" "drift retained verification artifact"
expect_fail "drifted verification artifact" \
	"verification.architectureGallery.artifactSha256 must match retained artifact" \
	"$drifted_artifact/Tools/workshop-package.sh" --release \
	"$FIXTURE_ROOT/drifted-artifact-package"

bad_pass_id="$(clone_case invalid-verification-pass-id)"
bad_pass_id_candidate="$(freeze_private_candidate "$bad_pass_id")"
write_workshop "$bad_pass_id" 2
write_evidence "$bad_pass_id" "$bad_pass_id_candidate"
python3 - "$bad_pass_id/docs/RELEASE_EVIDENCE.json" <<'PY'
import json
import sys
from pathlib import Path

path = Path(sys.argv[1])
data = json.loads(path.read_text(encoding="utf-8"))
data["verification"]["denseCityPerformance"]["passId"] = "performance"
path.write_text(json.dumps(data, indent=2) + "\n", encoding="utf-8")
PY
commit_all "$bad_pass_id" "invalid verification pass id"
expect_fail "invalid verification pass id" \
	"verification.denseCityPerformance.passId must be 'dense-city-performance'" \
	"$bad_pass_id/Tools/workshop-package.sh" --release \
	"$FIXTURE_ROOT/bad-pass-id-package"

protocol_range="$(clone_case ranged-protocol-evidence)"
protocol_range_candidate="$(freeze_private_candidate "$protocol_range")"
write_workshop "$protocol_range" 2
write_evidence "$protocol_range" "$protocol_range_candidate"
python3 - "$protocol_range/docs/RELEASE_EVIDENCE.json" <<'PY'
import json
import sys
from pathlib import Path

path = Path(sys.argv[1])
data = json.loads(path.read_text(encoding="utf-8"))
data["verification"]["numberedProtocols"]["passIds"] = ["55f3-55f8"]
path.write_text(json.dumps(data, indent=2) + "\n", encoding="utf-8")
PY
commit_all "$protocol_range" "ranged protocol evidence"
expect_fail "ranged protocol evidence" \
	"passIds must contain exact individual TESTING.md IDs" \
	"$protocol_range/Tools/workshop-package.sh" --release \
	"$FIXTURE_ROOT/protocol-range-package"

unknown_protocol="$(clone_case unknown-protocol-evidence)"
unknown_protocol_candidate="$(freeze_private_candidate "$unknown_protocol")"
write_workshop "$unknown_protocol" 2
write_evidence "$unknown_protocol" "$unknown_protocol_candidate"
python3 - "$unknown_protocol/docs/RELEASE_EVIDENCE.json" <<'PY'
import json
import sys
from pathlib import Path

path = Path(sys.argv[1])
data = json.loads(path.read_text(encoding="utf-8"))
data["verification"]["numberedProtocols"]["passIds"] = ["999z9"]
path.write_text(json.dumps(data, indent=2) + "\n", encoding="utf-8")
PY
commit_all "$unknown_protocol" "unknown protocol evidence"
expect_fail "unknown protocol evidence" \
	"passIds are absent from TESTING.md: 999z9" \
	"$unknown_protocol/Tools/workshop-package.sh" --release \
	"$FIXTURE_ROOT/unknown-protocol-package"

missing_protocol="$(clone_case missing-protocol-evidence)"
missing_protocol_candidate="$(freeze_private_candidate "$missing_protocol")"
write_workshop "$missing_protocol" 2
write_evidence "$missing_protocol" "$missing_protocol_candidate"
python3 - "$missing_protocol/docs/RELEASE_EVIDENCE.json" <<'PY'
import json
import sys
from pathlib import Path

path = Path(sys.argv[1])
data = json.loads(path.read_text(encoding="utf-8"))
data["verification"]["numberedProtocols"]["passIds"].pop()
path.write_text(json.dumps(data, indent=2) + "\n", encoding="utf-8")
PY
expect_fail "unwaived protocol omission" \
	"missing TESTING.md IDs without a human-reviewed waiver" \
	python3 "$missing_protocol/Tools/workshop_metadata.py" evidence \
	"$missing_protocol/manifest.json" "$missing_protocol/preview.png" \
	"$missing_protocol/workshop.json" "$missing_protocol/docs/RELEASE_EVIDENCE.json" \
	"$missing_protocol/README.md" "$missing_protocol/CHANGELOG.md"

duplicate_protocol="$(clone_case duplicate-protocol-evidence)"
duplicate_protocol_candidate="$(freeze_private_candidate "$duplicate_protocol")"
write_workshop "$duplicate_protocol" 2
write_evidence "$duplicate_protocol" "$duplicate_protocol_candidate"
python3 - "$duplicate_protocol/docs/RELEASE_EVIDENCE.json" <<'PY'
import json
import sys
from pathlib import Path

path = Path(sys.argv[1])
data = json.loads(path.read_text(encoding="utf-8"))
passes = data["verification"]["numberedProtocols"]["passIds"]
passes.append(passes[0])
path.write_text(json.dumps(data, indent=2) + "\n", encoding="utf-8")
PY
expect_fail "duplicate protocol evidence" "passIds must not contain duplicates" \
	python3 "$duplicate_protocol/Tools/workshop_metadata.py" evidence \
	"$duplicate_protocol/manifest.json" "$duplicate_protocol/preview.png" \
	"$duplicate_protocol/workshop.json" "$duplicate_protocol/docs/RELEASE_EVIDENCE.json" \
	"$duplicate_protocol/README.md" "$duplicate_protocol/CHANGELOG.md"

waived_protocol="$(clone_case bounded-protocol-waiver)"
waived_protocol_candidate="$(freeze_private_candidate "$waived_protocol")"
write_workshop "$waived_protocol" 2
write_evidence "$waived_protocol" "$waived_protocol_candidate"
python3 - "$waived_protocol/docs/RELEASE_EVIDENCE.json" <<'PY'
import json
import sys
from pathlib import Path

path = Path(sys.argv[1])
data = json.loads(path.read_text(encoding="utf-8"))
protocols = data["verification"]["numberedProtocols"]
pass_id = protocols["passIds"].pop()
protocols["waivers"] = [{
    "passId": pass_id,
    "reason": "Native platform access was unavailable during the bounded release window.",
    "reviewedBy": "Release Evidence Reviewer",
    "completedUtc": "2026-08-24T00:00:00Z",
}]
path.write_text(json.dumps(data, indent=2) + "\n", encoding="utf-8")
PY
python3 "$waived_protocol/Tools/workshop_metadata.py" evidence \
	"$waived_protocol/manifest.json" "$waived_protocol/preview.png" \
	"$waived_protocol/workshop.json" "$waived_protocol/docs/RELEASE_EVIDENCE.json" \
	"$waived_protocol/README.md" "$waived_protocol/CHANGELOG.md" >/dev/null
python3 - "$waived_protocol/docs/RELEASE_EVIDENCE.json" <<'PY'
import json
import sys
from pathlib import Path

path = Path(sys.argv[1])
data = json.loads(path.read_text(encoding="utf-8"))
data["verification"]["numberedProtocols"]["waivers"][0]["reason"] = \
    "TODO: decide whether this pass is needed."
path.write_text(json.dumps(data, indent=2) + "\n", encoding="utf-8")
PY
expect_fail "placeholder protocol waiver" "bounded human-reviewed reason" \
	python3 "$waived_protocol/Tools/workshop_metadata.py" evidence \
	"$waived_protocol/manifest.json" "$waived_protocol/preview.png" \
	"$waived_protocol/workshop.json" "$waived_protocol/docs/RELEASE_EVIDENCE.json" \
	"$waived_protocol/README.md" "$waived_protocol/CHANGELOG.md"

placeholder_human="$(clone_case placeholder-human-evidence)"
placeholder_human_candidate="$(freeze_private_candidate "$placeholder_human")"
write_workshop "$placeholder_human" 2
write_evidence "$placeholder_human" "$placeholder_human_candidate"
python3 - "$placeholder_human/docs/RELEASE_EVIDENCE.json" <<'PY'
import json
import sys
from pathlib import Path

path = Path(sys.argv[1])
data = json.loads(path.read_text(encoding="utf-8"))
data["privateSubscription"]["testedBy"] = "HUMAN_TESTER_NAME_OR_ALIAS"
path.write_text(json.dumps(data, indent=2) + "\n", encoding="utf-8")
PY
expect_fail "placeholder human tester" "testedBy must name the human tester" \
	python3 "$placeholder_human/Tools/workshop_metadata.py" evidence \
	"$placeholder_human/manifest.json" "$placeholder_human/preview.png" \
	"$placeholder_human/workshop.json" "$placeholder_human/docs/RELEASE_EVIDENCE.json" \
	"$placeholder_human/README.md" "$placeholder_human/CHANGELOG.md"
write_evidence "$placeholder_human" "$placeholder_human_candidate"
python3 - "$placeholder_human/docs/RELEASE_EVIDENCE.json" <<'PY'
import json
import sys
from pathlib import Path

path = Path(sys.argv[1])
data = json.loads(path.read_text(encoding="utf-8"))
data["verification"]["previewReview"]["reviewedBy"] = "Example Reviewer"
path.write_text(json.dumps(data, indent=2) + "\n", encoding="utf-8")
PY
expect_fail "placeholder preview reviewer" "reviewedBy must name the human reviewer" \
	python3 "$placeholder_human/Tools/workshop_metadata.py" evidence \
	"$placeholder_human/manifest.json" "$placeholder_human/preview.png" \
	"$placeholder_human/workshop.json" "$placeholder_human/docs/RELEASE_EVIDENCE.json" \
	"$placeholder_human/README.md" "$placeholder_human/CHANGELOG.md"

interim_preview="$(clone_case interim-preview-evidence)"
cp -- "$SOURCE_REPO/preview.png" "$interim_preview/preview.png"
interim_preview_candidate="$(freeze_private_candidate "$interim_preview")"
write_workshop "$interim_preview" 2
write_evidence "$interim_preview" "$interim_preview_candidate"
expect_fail "known interim preview" "refuses the known interim preview" \
	python3 "$interim_preview/Tools/workshop_metadata.py" evidence \
	"$interim_preview/manifest.json" "$interim_preview/preview.png" \
	"$interim_preview/workshop.json" "$interim_preview/docs/RELEASE_EVIDENCE.json" \
	"$interim_preview/README.md" "$interim_preview/CHANGELOG.md"

missing_structure="$(clone_case missing-structure-review)"
missing_structure_candidate="$(freeze_private_candidate "$missing_structure")"
write_workshop "$missing_structure" 2
write_evidence "$missing_structure" "$missing_structure_candidate"
rm -- "$missing_structure/docs/STRUCTURE_REVIEW.json"
commit_all "$missing_structure" "remove structural review"
git -C "$missing_structure" tag -a v0.2.0 -m "fixture release"
expect_fail "missing structural review" \
	"release package requires exact-inventory semantic review in HEAD" \
	"$missing_structure/Tools/workshop-package.sh" --release \
	"$FIXTURE_ROOT/missing-structure-package"

stale_structure="$(clone_case stale-structure-review)"
stale_structure_candidate="$(freeze_private_candidate "$stale_structure")"
write_workshop "$stale_structure" 2
write_evidence "$stale_structure" "$stale_structure_candidate"
python3 - "$stale_structure/docs/STRUCTURE_REVIEW.json" <<'PY'
import json
import sys
from pathlib import Path

path = Path(sys.argv[1])
data = json.loads(path.read_text(encoding="utf-8"))
data["inventorySha256"] = "0" * 64
path.write_text(json.dumps(data, indent=2) + "\n", encoding="utf-8")
PY
commit_all "$stale_structure" "stale structural review"
git -C "$stale_structure" tag -a v0.2.0 -m "fixture release"
expect_fail "stale structural review" \
	"semantic review does not bind the current staged C# inventory" \
	"$stale_structure/Tools/workshop-package.sh" --release \
	"$FIXTURE_ROOT/stale-structure-package"

branch_only="$(clone_case branch-only-tag)"
branch_candidate="$(freeze_private_candidate "$branch_only")"
write_workshop "$branch_only" 2
write_evidence "$branch_only" "$branch_candidate"
commit_all "$branch_only" "public metadata"
git -C "$branch_only" branch v0.2.0
expect_fail "branch named like release tag" "requires annotated tag" \
	"$branch_only/Tools/workshop-package.sh" --release "$FIXTURE_ROOT/branch-package"

lightweight="$(clone_case lightweight-tag)"
lightweight_candidate="$(freeze_private_candidate "$lightweight")"
write_workshop "$lightweight" 2
write_evidence "$lightweight" "$lightweight_candidate"
commit_all "$lightweight" "public metadata"
git -C "$lightweight" tag v0.2.0
expect_fail "lightweight release tag" "must be annotated" \
	"$lightweight/Tools/workshop-package.sh" --release "$FIXTURE_ROOT/lightweight-package"

release="$(clone_case positive-release)"
release_candidate="$(freeze_private_candidate "$release")"
write_workshop "$release" 2
write_evidence "$release" "$release_candidate"
assert_public_workshop_golden "$release"
commit_all "$release" "public metadata"
git -C "$release" tag -a v0.2.0 -m "fixture release"
release_dest="$FIXTURE_ROOT/release-package"
"$release/Tools/workshop-package.sh" --release "$release_dest" >/dev/null
(
	cd "$release_dest"
	sha256sum -c "$release_dest.sha256" >/dev/null
)
cmp -s "$release/workshop.json" "$release_dest/workshop.json"

mutated="$(clone_case mutated-after-private)"
mutated_candidate="$(freeze_private_candidate "$mutated")"
write_workshop "$mutated" 2
write_evidence "$mutated" "$mutated_candidate"
printf '%s\n' '// changed after subscribed private evidence' >> "$mutated/Core/Test.cs"
write_structure_review "$mutated"
commit_all "$mutated" "change runtime after private test"
expect_fail "runtime changed after private subscription" \
	"release runtime differs from subscribed private candidate: Core/Test.cs" \
	"$mutated/Tools/workshop-package.sh" --release "$FIXTURE_ROOT/mutated-package"

# Candidate provenance comes from the exact tested receipt, not release checkout's newer
# staging rules. A file that existed but was excluded from the private package cannot appear
# merely because stage.sh later starts selecting it.
stage_drift="$(clone_case candidate-stage-rule-drift)"
printf '%s\n' 'historically dormant data' > "$stage_drift/Dormant.dat"
commit_all "$stage_drift" "add dormant candidate data"
stage_drift_candidate="$(freeze_private_candidate "$stage_drift")"
select_dormant_file "$stage_drift"
write_workshop "$stage_drift" 2
write_evidence "$stage_drift" "$stage_drift_candidate"
commit_all "$stage_drift" "change staging rules after private test"
expect_fail "candidate staging-rule drift" \
	"release stage inventory differs from subscribed private package receipt" \
	"$stage_drift/Tools/workshop-package.sh" --release "$FIXTURE_ROOT/stage-drift-package"

mode_drift="$(clone_case candidate-mode-drift)"
mode_drift_candidate="$(freeze_private_candidate "$mode_drift")"
write_workshop "$mode_drift" 2
write_evidence "$mode_drift" "$mode_drift_candidate"
chmod +x -- "$mode_drift/Core/Test.cs"
commit_all "$mode_drift" "change runtime mode after private test"
expect_fail "candidate runtime mode drift" \
	"release runtime mode differs from subscribed private candidate: Core/Test.cs" \
	"$mode_drift/Tools/workshop-package.sh" --release "$FIXTURE_ROOT/mode-drift-package"

forged_receipt="$(clone_case forged-private-receipt)"
freeze_private_candidate "$forged_receipt" >/dev/null
python3 - "$forged_receipt/docs/PRIVATE_PACKAGE_RECEIPT.sha256" <<'PY'
import re
import sys
from pathlib import Path

path = Path(sys.argv[1])
text = path.read_text(encoding="utf-8")
path.write_text(re.sub(r"^[0-9a-f]{64}", "0" * 64, text, count=1), encoding="utf-8")
PY
commit_all "$forged_receipt" "bind forged private receipt"
forged_candidate="$(git -C "$forged_receipt" rev-parse HEAD)"
write_workshop "$forged_receipt" 2
write_evidence "$forged_receipt" "$forged_candidate"
commit_all "$forged_receipt" "public metadata with forged receipt evidence"
expect_fail "forged private receipt" \
	"private package receipt differs from candidate commit" \
	"$forged_receipt/Tools/workshop-package.sh" --release "$FIXTURE_ROOT/forged-receipt-package"

# A syntactically valid receipt with real candidate hashes is still forged if changed after the
# candidate commit.  Recompute evidence from that forged HEAD copy and prove candidate binding,
# not internal receipt consistency, blocks release.
valid_forgery="$(clone_case internally-valid-forged-private-receipt)"
printf '%s\n' 'historically dormant data' > "$valid_forgery/Dormant.dat"
commit_all "$valid_forgery" "add dormant candidate data"
valid_forgery_candidate="$(freeze_private_candidate "$valid_forgery")"
select_dormant_file "$valid_forgery"
python3 - "$valid_forgery/docs/PRIVATE_PACKAGE_RECEIPT.sha256" \
	"$valid_forgery/Dormant.dat" <<'PY'
import hashlib
import sys
from pathlib import Path

receipt_path, dormant_path = map(Path, sys.argv[1:])
lines = receipt_path.read_text(encoding="utf-8").splitlines()
lines.append(
    hashlib.sha256(dormant_path.read_bytes()).hexdigest() + "  ./Dormant.dat"
)
lines.sort(key=lambda line: line.split("  ./", 1)[1].encode("utf-8"))
receipt_path.write_text("\n".join(lines) + "\n", encoding="utf-8")
PY
write_workshop "$valid_forgery" 2
write_evidence "$valid_forgery" "$valid_forgery_candidate"
commit_all "$valid_forgery" "forge internally valid receipt after private test"
expect_fail "internally valid forged private receipt" \
	"release HEAD private package receipt differs from candidate commit" \
	"$valid_forgery/Tools/workshop-package.sh" --release \
	"$FIXTURE_ROOT/internally-valid-forged-receipt-package"

echo "WORKSHOP PACKAGE HARNESS CLEAN"
