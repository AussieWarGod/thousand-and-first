#!/usr/bin/env python3
"""Completed-heart and real-frame oracle; also require lifecycle, source, seal and log checks."""
import argparse
import json
from pathlib import Path
import re
from guest_save_check import field, one, require
from personas import persona_matrix


def sight(detail):
    for name, value in (("whole-zone-drawn", "true"), ("cells", "2000"),
                        ("gameplay-sight-restored", "true"), ("synthetic-visibility", "false")):
        require(field(detail, name) == value, "sight invariant failed: " + name)
    counts = {}
    for name in ("frames", "naturally-hidden-cells"):
        raw = field(detail, name)
        require(re.fullmatch(r"[1-9][0-9]{0,4}", raw), "invalid sight count: " + name)
        counts[name] = int(raw)
    require(3 <= counts["frames"] <= 240 and counts["naturally-hidden-cells"] <= 2000,
            "real frame count or occlusion is outside bounds")
    return counts["frames"]


def geometry(detail):
    for name, value in (("completed", "true"), ("canvas", "7"), ("roof", "open"), ("enclosed", "false"),
                        ("synthetic-completion", "false"), ("world-repair", "false")):
        require(field(detail, name) == value, "heart invariant failed: " + name)
    values = {name: field(detail, name) for name in ("heart", "canvas-cells", "layout", "realized-sha256")}
    require(re.fullmatch(r"[0-9a-f]{64}", values["realized-sha256"]), "invalid physical digest")
    require(values["layout"] in ("6x4", "4x6"), "heart layout dimensions differ")
    cells = values["canvas-cells"].split("|")
    require(len(cells) == len(set(cells)) == 7, "canvas cells missing or repeated")
    for cell in cells:
        require(re.fullmatch(r"(?:0|[1-9][0-9]?),\d{1,2}", cell), "invalid canvas coordinate")
        x, y = map(int, cell.split(","))
        require(x < 80 and y < 25, "canvas outside local zone")
    return values


def judge(source, loaded):
    for rows in (source, loaded):
        require(persona_matrix.terminal_row(rows) == "SCRIPT-COMPLETE", "session did not complete")
        require(all(outcome == "OK" for _, outcome, _ in rows), "session contains a refusal")
        terminal, _ = one(rows, "SCRIPT-COMPLETE")
        require(terminal == len(rows) - 1, "observations continue after completion")
    opened, opening = one(source, "heart-site-open")
    advance, _ = one(source, "advance")
    grown, _ = one(source, "lifecycle-grown")
    inspected, physical = one(source, "heart-sight-inspect")
    yielded, _ = one(source, "yield-frames-complete")
    checked, drawn = one(source, "heart-sight-check")
    saved, _ = one(source, "lifecycle-save")
    require(opened < advance < grown < inspected < yielded < checked < saved, "source phases out of order")
    require(field(opening, "synthetic-completion") == "false", "opening forced construction")
    expected = geometry(physical)
    source_frames = sight(drawn)
    require(field(drawn, "witness") == "recorded", "source did not save physical witness")
    digest = field(drawn, "receipt-sha256")
    require(re.fullmatch(r"[0-9a-f]{64}", digest), "invalid saved physical witness digest")
    pre, before = one(loaded, "heart-load-preactivation")
    post, after = one(loaded, "heart-load-verified")
    life, _ = one(loaded, "lifecycle-loaded")
    paid, _ = one(loaded, "lifecycle-next")
    resume, resumed = one(loaded, "heart-load-resume")
    frames, _ = one(loaded, "yield-frames-complete")
    rendered, final = one(loaded, "heart-load-rendered")
    terminal, complete = one(loaded, "SCRIPT-COMPLETE")
    require(pre < post < life < paid < resume < frames < rendered < terminal, "cold-load phases out of order")
    require(field(before, "before-AfterGameLoaded") == "true", "missing preactivation proof")
    for detail in (before, after, final):
        require(geometry(detail) == expected and field(detail, "receipt-sha256") == digest
                and field(detail, "same-physical-heart") == "true", "physical heart changed across cold load")
    for name, value in (("vanilla-Continue", "true"), ("session-backup", "false"),
                        ("saved-script-considered", "true"), ("requested-frames", "3")):
        require(field(resumed, name) == value, "invalid render continuation: " + name)
    loaded_frames = sight(final)
    require(field(final, "new-game-script-replayed") == "false"
            and field(complete, "real-loaded-frames") == "true", "loaded loop replayed or did not render")
    return {"verdict": "PASS", "heartId": expected["heart"], "canvasCells": expected["canvas-cells"],
            "sourceFrames": source_frames, "loadedFrames": loaded_frames, "snapshotSha256": digest,
            "enclosed": False, "scope": "journal only; require lifecycle, source binding, strict logs and owned stops"}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("source", type=Path)
    parser.add_argument("loaded", type=Path)
    parser.add_argument("--results", type=Path, required=True)
    args = parser.parse_args()
    try:
        result = judge(*(persona_matrix.read_journal(p.read_text(encoding="utf-8-sig"))
                         for p in (args.source, args.loaded)))
    except (OSError, ValueError) as error:
        result = {"verdict": "FAIL", "reason": str(error)}
    args.results.write_text(json.dumps(result, indent=2) + "\n")
    print(json.dumps(result))
    return 0 if result["verdict"] == "PASS" else 1


if __name__ == "__main__":
    raise SystemExit(main())
