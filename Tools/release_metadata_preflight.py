#!/usr/bin/env python3
"""Read-only, fast private/public Alpha metadata checks using the release validators."""
import json
from pathlib import Path
import sys

if __package__:
    from . import workshop_metadata as metadata
else:
    import workshop_metadata as metadata


def check(root: Path) -> str:
    manifest = metadata.load_manifest(root / "manifest.json", require_preview=True)
    workshop = root / "workshop.json"
    # Visibility chooses the lane only; the existing validator then checks the entire file,
    # including canonical fields and duplicate keys. Unknown lanes must never skip validation.
    data = json.loads(workshop.read_text(encoding="utf-8-sig"))
    if not isinstance(data, dict):
        raise metadata.ValidationError("Workshop metadata must be an object")
    visibility = data.get("Visibility")
    if visibility not in ("0", "2"):
        raise metadata.ValidationError("preflight requires private (0) or public Alpha (2) visibility")
    mode = "test" if visibility == "0" else "alpha"
    metadata.validate_workshop(workshop, manifest, mode)
    preview = root / manifest["PreviewImage"]
    metadata.validate_preview(preview)
    if mode == "alpha":
        metadata.validate_alpha_candidate(manifest, preview, workshop,
            root / "docs/ALPHA_CANDIDATE.json", root / "README.md", root / "CHANGELOG.md")
    return mode


def main() -> int:
    try:
        mode = check(Path(__file__).resolve().parent.parent)
    except (OSError, ValueError, metadata.ValidationError) as error:
        print("RELEASE METADATA PREFLIGHT REFUSED: " + str(error), file=sys.stderr)
        return 1
    print("RELEASE METADATA PREFLIGHT CLEAN: " + mode + " (not package or delivery acceptance)")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
