#!/usr/bin/env python3
"""Render tracked Qud reference names for one licensed local installation."""

from __future__ import annotations

import argparse
import re
from pathlib import Path, PureWindowsPath

from workshop_metadata import GAME_CORE_BUILD, GAME_MARKETING_VERSION


class ReferenceError(ValueError):
    pass


def _symbols(template: str, mode: str) -> str:
    match = re.search(r"(?m)^-define:(.*)$", template)
    if match is None:
        raise ReferenceError("reference template has no -define line")
    existing = [value for value in match.group(1).split(";") if value]
    mods = [value for value in existing if value.startswith("MOD_")]
    marketing = GAME_MARKETING_VERSION.split(".")
    core = GAME_CORE_BUILD.split(".")
    if len(marketing) < 2 or len(core) < 3:
        raise ReferenceError("release target versions cannot form Qud compiler symbols")
    required = [
        "VERSION_" + "_".join(marketing[:2]),
        "BUILD_" + "_".join(core[:3]),
    ]
    return ";".join(required + (mods if mode == "compatibility" else []))


def render(template: str, managed: Path, managed_windows: str, mode: str) -> str:
    if mode not in {"baseline", "compatibility"}:
        raise ReferenceError("reference mode must be baseline or compatibility")
    if not managed.is_dir():
        raise ReferenceError(f"Qud managed directory not found: {managed}")
    root_windows = PureWindowsPath(managed_windows)
    symbols = _symbols(template, mode)
    output: list[str] = []
    references = 0
    assembly = False
    for raw in template.splitlines():
        line = raw.lstrip("\ufeff")
        if line.startswith("-define:"):
            output.append("-define:" + symbols)
            continue
        match = re.fullmatch(r'-r:"(.*)"', line)
        if match is None:
            output.append(line)
            continue
        name = PureWindowsPath(match.group(1)).name
        local = managed / name
        if not local.is_file():
            raise ReferenceError(f"Qud managed reference not found: {local}")
        references += 1
        assembly = assembly or name.casefold() == "assembly-csharp.dll"
        output.append(f'-r:"{root_windows / name}"')
    if not references:
        raise ReferenceError("reference template contains no managed assemblies")
    if not assembly:
        raise ReferenceError("reference template omits Assembly-CSharp.dll")
    return "\n".join(output) + "\n"


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--template", required=True, type=Path)
    parser.add_argument("--managed", required=True, type=Path)
    parser.add_argument("--managed-windows", required=True)
    parser.add_argument("--mode", required=True, choices=("baseline", "compatibility"))
    parser.add_argument("--output", required=True, type=Path)
    args = parser.parse_args()
    try:
        template = args.template.read_text(encoding="utf-8-sig")
        payload = render(template, args.managed, args.managed_windows, args.mode)
        args.output.write_text(payload, encoding="utf-8", newline="\n")
    except (OSError, UnicodeError, ReferenceError) as error:
        parser.error(str(error))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
