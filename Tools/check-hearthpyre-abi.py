#!/usr/bin/env python3
"""Verify the exact Hearthpyre 2.2.3 ABI and the bridge's read-only boundary."""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
import re
import sys


ROOT = Path(__file__).resolve().parent.parent
PIN = ROOT / "DevTests" / "Compatibility" / "Hearthpyre223Abi.json"
STUB = ROOT / "DevTests" / "Compatibility" / "Hearthpyre223AbiStub.cs"
BRIDGE = ROOT / "Integrations" / "Hearthpyre223"
DEFAULT_SOURCE = Path(
    "/mnt/f/SteamLibrary/steamapps/workshop/content/333640/1683847053"
)
BANNED_BRIDGE_MARKERS = (
    "AddLiminal",
    "NewHome(",
    "RemoveHome(",
    "NewSettlement",
    "PartyLeader",
    "GetZone(",
    "RequireZone",
    "Notitia",
    "Catalog",
)
BANNED_BRIDGE_PATTERNS = (
    r"\bZoneManager\b(?!\s*\.\s*ActiveZone\b)",
    r"\b(?:Sector|sector|Home|home)\s*\.\s*(?:Add|Remove|Flush)\s*\(",
    r"RealmSystem\s*\.\s*Homes\s*\[[^\]]+\]\s*=",
    r"RealmSystem\s*\.\s*Homes\s*\.\s*(?:Add|Remove|Clear)\s*\(",
    r"(?:Sector|sector)\s*\.\s*Homes\s*\.\s*(?:Add|Remove|Clear)\s*\(",
)
SURFACES = {
    "CS/RealmSystem.cs": (
        r"public static Dictionary<Guid, Settlement> Settlements\s*=",
        r"public static Dictionary<string, Settlement> SettlementsByCellID\s*=",
        r"public static Dictionary<Guid, Sector> Sectors\s*=",
        r"public static Dictionary<string, Sector> SectorsByZoneID\s*=",
        r"public static Dictionary<Guid, Home> Homes\s*=",
    ),
    "CS/Realm/Settlement.cs": (
        r"public Guid ID \{ get; private set; \}",
        r"public Dictionary<string, Sector> SectorsByZoneID \{ get; \}",
    ),
    "CS/Realm/Sector.cs": (
        r"public Guid ID \{ get; private set; \}",
        r"public Settlement Settlement \{ get; \}",
        r"public string ZoneID => Lattice\.ZoneID;",
        r"public List<Home> Homes \{ get; \}",
    ),
    "CS/Realm/Home.cs": (
        r"public class Home : ICogentArea, IEnumerable<Location2D>",
        r"public Guid ID \{ get; private set; \}",
        r"public Sector Sector \{ get; set; \}",
        r"public int Count => Locations\.Count;",
        r"public Location2D Origin;",
        r"public IEnumerator<Location2D> GetEnumerator\(\) => Locations\.GetEnumerator\(\);",
    ),
}


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def fail(message: str) -> None:
    raise SystemExit("Hearthpyre ABI proof failed: " + message)


def prove_core_has_no_foreign_type_references() -> None:
    for folder in ("Api", "Architecture", "Chronicle", "Core", "Debug", "Experience",
                   "Founding", "Growth", "Polity", "Quests", "Raids", "Simulation",
                   "Trade", "World"):
        for path in (ROOT / folder).rglob("*.cs"):
            source = path.read_text(encoding="utf-8-sig")
            if re.search(r"^\s*using\s+Hearthpyre(?:\.|\s*;)", source, re.MULTILINE):
                fail(str(path.relative_to(ROOT)) + " imports a foreign type")
            if re.search(r"\bHearthpyre\s*\.\s*[A-Za-z_]", source):
                fail(str(path.relative_to(ROOT)) + " names a foreign type")


def prove_bridge_boundary() -> None:
    sources = sorted(BRIDGE.glob("*.cs"))
    if not sources:
        fail("bridge source is missing")
    body = "\n".join(path.read_text(encoding="utf-8-sig") for path in sources)
    for marker in BANNED_BRIDGE_MARKERS:
        if marker in body:
            fail("bridge contains forbidden lifecycle/mutation marker: " + marker)
    for pattern in BANNED_BRIDGE_PATTERNS:
        if re.search(pattern, body):
            fail("bridge contains forbidden Hearthpyre mutation: " + pattern)
    required = (
        "RealmSystem.Settlements",
        "RealmSystem.SettlementsByCellID",
        "RealmSystem.Sectors",
        "RealmSystem.SectorsByZoneID",
        "RealmSystem.Homes.TryGetValue",
        "Sector.Homes",
        "Home.Count",
        "Home.Origin",
        "foreach (Location2D location in Home)",
        "[KingdomForeignFootprintProvider]",
        "ReferenceEquals(The.ZoneManager.ActiveZone, ActiveZone)",
        'ProviderVersion => "2.2.3"',
    )
    for proof in required:
        if proof not in body:
            fail("bridge no longer proves required typed surface: " + proof)


def prove_source(source_root: Path, pin: dict) -> None:
    if not source_root.is_dir():
        fail("pinned source directory is missing: " + str(source_root))
    manifest = json.loads((source_root / "manifest.json").read_text(encoding="utf-8"))
    if manifest.get("ID") != pin["id"] or manifest.get("Version") != pin["version"]:
        fail("source manifest is not exact Hearthpyre 2.2.3")
    for relative, expected in pin["files"].items():
        path = source_root / relative
        if not path.is_file() or sha256(path) != expected:
            fail(relative + " differs from the reviewed 2.2.3 source")
    for relative, patterns in SURFACES.items():
        body = (source_root / relative).read_text(encoding="utf-8-sig")
        for pattern in patterns:
            if re.search(pattern, body) is None:
                fail(relative + " is missing exact ABI surface: " + pattern)


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--source", type=Path, default=DEFAULT_SOURCE)
    parser.add_argument("--fixture-only", action="store_true")
    args = parser.parse_args()
    pin = json.loads(PIN.read_text(encoding="utf-8"))
    if sha256(STUB) != pin["stubSha256"]:
        fail("tracked compile stub changed without a reviewed ABI pin update")
    prove_core_has_no_foreign_type_references()
    prove_bridge_boundary()
    if not args.fixture_only:
        prove_source(args.source, pin)
        print("exact installed Hearthpyre 2.2.3 source hashes and ABI: clean")
    else:
        print("tracked Hearthpyre 2.2.3 ABI fixture: clean (installed source not checked)")
    print("core foreign-type boundary and bridge read-only boundary: clean")
    return 0


if __name__ == "__main__":
    sys.exit(main())
