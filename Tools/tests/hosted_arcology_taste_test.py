"""Pure source audit for the hosted arcology's 27 authored programmes."""

from __future__ import annotations

import re
import unittest
from collections import deque
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
PROGRAMME = ROOT / "World" / "KingdomHostedArcologyProgrammeBuilder.cs"
BUILDER = ROOT / "World" / "KingdomHostedArcologyBuilder.cs"
TOPOLOGY = ROOT / "Growth" / "KingdomHostedArcologyTopology.cs"
ARCHETYPES = (
    "Cellular", "Nave", "Comb", "Courts", "Terraces",
    "Workbays", "Aisles", "Branches", "Lightwell",
)


def method_body(source: str, name: str) -> str:
    start = source.index(f"private static bool {name}(")
    brace = source.index("{", start)
    depth = 0
    for index in range(brace, len(source)):
        if source[index] == "{":
            depth += 1
        elif source[index] == "}":
            depth -= 1
            if depth == 0:
                return source[brace + 1:index]
    raise AssertionError(f"unterminated method {name}")


def cells_for(source: str, name: str) -> frozenset[tuple[int, int]]:
    body = method_body(source, name)
    cells: list[tuple[int, int]] = []
    for x1, x2, y in re.findall(
        r"\bH\(z,at,root,(\d+),(\d+),(\d+),[ab],", body
    ):
        cells.extend((x, int(y)) for x in range(int(x1), int(x2) + 1))
    for x, y1, y2 in re.findall(
        r"\bV\(z,at,root,(\d+),(\d+),(\d+),[ab],", body
    ):
        cells.extend((int(x), y) for y in range(int(y1), int(y2) + 1))
    for x1, x2, y1, y2 in re.findall(
        r"\bCorner\(z,at,root,(\d+),(\d+),(\d+),(\d+),[ab],[ab],", body
    ):
        left, right, top, bottom = map(int, (x1, x2, y1, y2))
        cells.extend((x, bottom) for x in range(left, right + 1))
        cells.extend((right, y) for y in range(top, bottom))
    for x1, x2, y1, y2 in re.findall(
        r"\bBay\(z,at,root,(\d+),(\d+),(\d+),(\d+),[ab],[ab],", body
    ):
        left, right, top, bottom = map(int, (x1, x2, y1, y2))
        cap = top if top < 10 else bottom
        cells.extend((x, cap) for x in range(left, right + 1))
        start = top + 1 if cap == top else top
        end = bottom - 1 if cap == bottom else bottom
        for x in (left, right):
            cells.extend((x, y) for y in range(start, end + 1))
    if len(cells) != len(set(cells)):
        raise AssertionError(f"{name} stacks authored fabric on one cell")
    return frozenset(cells)


def parse_decor(source: str) -> dict[str, tuple[str, str, str]]:
    rows = re.findall(
        r"case KingdomArcologyProgramme\.(\w+): return Set\("
        r'"([^"]+)","([^"]+)","([^"]+)"\);',
        source,
    )
    return {name: (a, b, c) for name, a, b, c in rows}


def parse_enum(source: str) -> dict[str, int]:
    block = source[source.index("public enum KingdomArcologyProgramme"):]
    block = block[:block.index("}")]
    return {name: int(value) for name, value in re.findall(r"\b(\w+)\s*=\s*(\d+)", block)}


def parse_fixture_block(source: str, name: str) -> list[tuple[int, int, str, str]]:
    start = source.index(f"KingdomArcologyFixtureSpec[] {name}")
    end = source.index("};", start)
    return [
        (int(x), int(y), blueprint, role)
        for x, y, blueprint, role in re.findall(
            r'F\((\d+),(\d+),"([^"]+)","([^"]+)"\)', source[start:end]
        )
    ]


def shell_for(x: int, y: int) -> set[tuple[int, int]]:
    walls: set[tuple[int, int]] = set()
    for px in range(80):
        if y == 0 or px not in (39, 40):
            walls.add((px, 0))
        if y == 2 or px not in (39, 40):
            walls.add((px, 24))
    for py in range(1, 24):
        if x == 0 or py not in (11, 12):
            walls.add((0, py))
        if x == 2 or py not in (11, 12):
            walls.add((79, py))
    return walls


def reachable(blocked: set[tuple[int, int]], start: tuple[int, int]) -> set[tuple[int, int]]:
    seen = {start}
    pending = deque([start])
    while pending:
        x, y = pending.popleft()
        for point in ((x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1)):
            if (0 <= point[0] < 80 and 0 <= point[1] < 25
                    and point not in blocked and point not in seen):
                seen.add(point)
                pending.append(point)
    return seen


class HostedArcologyTasteTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.source = PROGRAMME.read_text(encoding="utf-8")
        cls.builder = BUILDER.read_text(encoding="utf-8")
        cls.topology = TOPOLOGY.read_text(encoding="utf-8")
        cls.layouts = [cells_for(cls.source, name) for name in ARCHETYPES]
        cls.decor = parse_decor(cls.source)
        cls.values = parse_enum(cls.topology)
        cls.ward = parse_fixture_block(cls.source, "Ward")
        cls.terrace = parse_fixture_block(cls.source, "Terrace")

    def test_nine_archetypes_are_sparse_distinct_and_keep_the_route_cross(self) -> None:
        self.assertEqual(9, len(set(self.layouts)))
        for name, cells in zip(ARCHETYPES, self.layouts):
            self.assertGreaterEqual(len(cells), 40, name)
            self.assertLessEqual(len(cells), 140, name)
            self.assertFalse(
                any(34 <= x <= 45 or 9 <= y <= 15 for x, y in cells), name
            )

    def test_all_twenty_seven_programmes_have_distinct_auditable_signatures(self) -> None:
        self.assertEqual(27, len(self.values))
        self.assertEqual(set(self.values), set(self.decor))
        self.assertEqual(27, len(set(self.decor.values())))
        material = {
            0: ("FoamcreteFloor", "LowConcrete", "HalfStone"),
            1: ("GreyMarbleFloor", "HalfStone", "LowConcrete"),
            2: ("SmallHexFloor", "LowMetalScreen", "RustedMetalWall"),
        }
        signatures = {
            (self.layouts[(value - 1) % 9], material[(value - 1) // 9], self.decor[name])
            for name, value in self.values.items()
        }
        self.assertEqual(27, len(signatures))
        vocabulary = {blueprint for row in self.decor.values() for blueprint in row}
        self.assertGreaterEqual(len(vocabulary), 20)

    def test_every_zone_connects_thresholds_stairs_anchor_and_exit(self) -> None:
        programme_order = re.findall(
            r"KingdomArcologyProgramme\.(\w+),?", self.topology[
                self.topology.index("Programmes ="):self.topology.index("public static bool InBounds")
            ]
        )
        self.assertEqual(27, len(programme_order))
        for index, name in enumerate(programme_order):
            z, rem = divmod(index, 9)
            y, x = divmod(rem, 3)
            blocked = shell_for(x, y) | set(self.layouts[(self.values[name] - 1) % 9])
            paid = self.terrace if name == "HydroponicTerrace" else self.ward if name == "LodgingWard" else []
            blocked.update((px, py) for px, py, _, _ in paid)
            points = {(40, 3)}
            if y > 0: points.update(((39, 0), (40, 0)))
            if y < 2: points.update(((39, 24), (40, 24)))
            if x > 0: points.update(((0, 11), (0, 12)))
            if x < 2: points.update(((79, 11), (79, 12)))
            if z > 0: points.add(((36 if z == 1 else 44), 12))
            if z < 2: points.add(((36 if z == 0 else 44), 12))
            if (x, y, z) == (1, 1, 1): points.add((40, 22))
            self.assertTrue(points.isdisjoint(blocked), (name, points & blocked))
            self.assertTrue(points.issubset(reachable(blocked, (40, 3))), name)

    def test_paid_fixture_authority_matches_programme_and_physical_counts(self) -> None:
        self.assertEqual(14, len(self.ward))
        self.assertEqual(18, len(self.terrace))
        self.assertEqual(8, sum(bp == "r_KingdomFixtureBedMetal" for _, _, bp, _ in self.ward))
        self.assertEqual(1, sum(bp == "r_KingdomArcologyWardAmenity" for _, _, bp, _ in self.ward))
        self.assertEqual(14, sum(bp == "r_KingdomArcologyGrowbed" for _, _, bp, _ in self.terrace))
        for label, rows in (("ward", self.ward), ("terrace", self.terrace)):
            self.assertEqual(len(rows), len({(x, y) for x, y, _, _ in rows}), label)
            self.assertEqual(len(rows), len({role for _, _, _, role in rows}), label)
        occupied_cues = {
            (11, 5), (68, 5), (11, 19), (68, 19),
            (10, 10), (30, 10), (49, 14), (69, 14), (40, 7), (40, 17),
            (40, 3), (36, 12), (44, 12), (40, 22),
        }
        for programme, rows in (
            ("HydroponicTerrace", self.terrace), ("LodgingWard", self.ward)
        ):
            fixtures = {(x, y) for x, y, _, _ in rows}
            fabric = self.layouts[(self.values[programme] - 1) % 9]
            self.assertTrue(fixtures.isdisjoint(fabric), programme)
            self.assertTrue(fixtures.isdisjoint(occupied_cues), programme)
        self.assertIn("TryPaidFixtures", self.source)
        self.assertIn("ProgrammeAt(\n\t\t\t\tAnchor.ZoneX", (ROOT / "Growth" / "KingdomHostedArcology.Visual.cs").read_text(encoding="utf-8"))

    def test_real_lights_and_stable_identity_are_not_optional_decor(self) -> None:
        lights = re.findall(r'Add\(z,at,root,\d+,\d+,"Techlight1","light:[^"]+"\)', self.source)
        self.assertEqual(6, len(lights))
        self.assertIn("candidate.IDIfAssigned == id", self.builder)
        self.assertIn("count != 0 || !cell.IsPassable()", self.builder)
        self.assertIn("KingdomHostedArcology.Quarantine", self.builder)
        self.assertIn('"FoamcreteFloor"', self.source)
        self.assertIn('"GreyMarbleFloor"', self.source)
        self.assertIn('"SmallHexFloor"', self.source)


if __name__ == "__main__":
    unittest.main()
