#!/usr/bin/env python3
"""Determinism and concrete-output tests for the exact-lot materialiser."""

from __future__ import annotations

import importlib.util
import sys
import unittest
import xml.etree.ElementTree as ET
from pathlib import Path


GENERATOR_PATH = Path(__file__).resolve().parents[1] / "generate-lot-realizations.py"
SPEC = importlib.util.spec_from_file_location("taf_generate_lot_realizations", GENERATOR_PATH)
if SPEC is None or SPEC.loader is None:
    raise RuntimeError(f"cannot load {GENERATOR_PATH}")
GENERATOR = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = GENERATOR
SPEC.loader.exec_module(GENERATOR)


class LotRealizationGeneratorTests(unittest.TestCase):
    def test_checked_in_output_is_current_concrete_and_exact(self) -> None:
        repository = GENERATOR_PATH.parents[1]
        result = GENERATOR.materialize(repository)
        expected = result.text
        map_count = result.map_count
        plan_count = result.plan_count
        tier_count = result.tier_count
        target = repository / "Architecture" / GENERATOR.OUTPUT_NAME
        self.assertEqual(target.read_text(encoding="utf-8"), expected)
        self.assertEqual((map_count, plan_count, tier_count), (337, 242, 277))
        self.assertEqual(expected.count("<!-- realization source="), 337)

        root = ET.fromstring(expected)
        maps = {item.get("Key"): item for item in root.findall("map")}
        self.assertEqual(len(maps), map_count)
        self.assertEqual(len(root.findall("plan")), plan_count)
        self.assertEqual(
            GENERATOR.IDENTITY_SELECTOR_ATTRIBUTES,
            ("Cultures", "Species", "Genotypes", "Bodies"),
        )
        variants = root.findall("./plan/binding/tier/variant")
        self.assertTrue(any(item.get("Cultures") == "Hindren" for item in variants))
        self.assertTrue(any(item.get("Species") == "hindren" for item in variants))
        self.assertTrue(any(item.get("Bodies") == "robot" for item in variants))
        for binding in root.findall("./plan/binding"):
            size = binding.get("Size")
            self.assertIn(size, GENERATOR.LOT_DIMENSIONS)
            dimensions = GENERATOR.LOT_DIMENSIONS[size]
            for tier in binding.findall("tier"):
                keys = [tier.get("Map")]
                keys.extend(
                    variant.get("Map")
                    for variant in tier.findall("variant")
                    if variant.get("Map")
                )
                for key in keys:
                    architecture_map = maps[key]
                    self.assertEqual(
                        (int(architecture_map.get("Width")), int(architecture_map.get("Height"))),
                        dimensions,
                    )

    def test_every_generated_road_entrance_has_an_exact_unclaimed_egress(self) -> None:
        repository = GENERATOR_PATH.parents[1]
        result = GENERATOR.materialize(repository)
        root = ET.fromstring(result.text)
        maps = {item.get("Key"): item for item in root.findall("map")}
        for binding in root.findall("./plan/binding"):
            if binding.get("Facing") != "road":
                continue
            for tier in binding.findall("tier"):
                keys = {tier.get("Map")}
                keys.update(
                    variant.get("Map")
                    for variant in tier.findall("variant")
                    if variant.get("Map")
                )
                for key in keys:
                    architecture_map = maps[key]
                    canvas = [list(row.get("Cells", "")) for row in architecture_map.findall("row")]
                    entrances = GENERATOR._public_entrances(architecture_map)
                    self.assertTrue(entrances, key)
                    for x, y in entrances:
                        route = GENERATOR._unclaimed_route(canvas, (x, y))
                        self.assertIsNotNone(route, f"{key} entrance {x},{y}")
                        self.assertTrue(
                            all(canvas[ry][rx] == "." for rx, ry in route), key
                        )

    def test_yard_census_accounts_every_added_cell_without_density_heuristics(self) -> None:
        result = GENERATOR.materialize(GENERATOR_PATH.parents[1])
        totals = tuple(
            sum(getattr(record, field) for record in result.records)
            for field in (
                "added_cells",
                "yard_cells",
                "path_cells",
                "boundary_cells",
                "route_cells",
                "intentional_open_cells",
                "inaccessible_open_cells",
            )
        )
        self.assertEqual(totals, (45056, 33753, 8984, 194, 1524, 601, 0))
        self.assertEqual(sum(record.hosted_hold for record in result.records), 3)
        for record in result.records:
            facts = record.yard_cells + record.path_cells + record.boundary_cells
            if record.hosted_hold:
                self.assertIn("separate authored redesign", record.open_reason)
                self.assertEqual(facts, 0)
            else:
                self.assertGreater(facts, 0, record.generated_key)
            if record.intentional_open_cells:
                self.assertTrue(record.open_reason, record.generated_key)
                if not record.hosted_hold:
                    self.assertIn(
                        record.context.build_key,
                        GENERATOR.INTENTIONAL_OPEN_REASONS,
                        record.generated_key,
                    )
            self.assertEqual(
                record.added_cells,
                facts
                + record.route_cells
                + record.intentional_open_cells
                + record.inaccessible_open_cells,
                record.generated_key,
            )

    def test_roofed_and_open_small_plans_gain_readable_m_l_xl_yards(self) -> None:
        result = GENERATOR.materialize(GENERATOR_PATH.parents[1])
        by_source = {}
        for record in result.records:
            by_source.setdefault(record.source_key, {})[record.context.target_size] = record
        for source_key in ("housing-hut-s0", "production-plot-s0"):
            records = by_source[source_key]
            self.assertEqual(set(records), {"M", "L", "XL"})
            for size in ("M", "L", "XL"):
                record = records[size]
                self.assertGreater(record.yard_cells, 0, record.generated_key)
                self.assertGreater(record.path_cells, 0, record.generated_key)
                self.assertGreater(record.added_cells, record.route_cells, record.generated_key)
        self.assertLess(
            by_source["housing-hut-s0"]["M"].yard_cells,
            by_source["housing-hut-s0"]["XL"].yard_cells,
        )

    def test_larger_creed_lots_add_scaled_semantic_work_bays(self) -> None:
        repository = GENERATOR_PATH.parents[1]
        result = GENERATOR.materialize(repository)
        generated = {
            item.get("Key"): item for item in ET.fromstring(result.text).findall("map")
        }
        sources = {}
        palettes = {}
        for _path, root in GENERATOR._source_roots(repository):
            sources.update({item.get("Key"): item for item in root.findall("map")})
            palettes.update({item.get("Key"): item for item in root.findall("palette")})
        objects_root = ET.parse(repository / "RuntimeData" / "ObjectBlueprints.xml").getroot()
        objects = {item.get("Name"): item for item in objects_root.iter("object")}
        allowed_parts = {"Render", "Description", "Physics", "Metal"}
        inert_references = set(GENERATOR.CREED_EXPANSION_INERT_OBJECTS.values())
        expected_blueprints = {
            "r_KingdomCreedPracticeBasket", "r_KingdomCreedPracticeTable",
            "r_KingdomCreedPracticeColdHearth", "r_KingdomCreedPracticeShelf",
            "r_KingdomCreedPracticeStone", "r_KingdomCreedPracticeDryBasin",
            "r_KingdomCreedPracticeBench", "r_KingdomCreedPracticePallet",
            "r_KingdomCreedSpindleWheel", "r_KingdomCreedDryContact",
            "r_KingdomCreedHornPost", "r_KingdomCreedScrapAltar",
            "r_KingdomCreedWeaponRack", "r_KingdomCreedColdBrazier",
            "r_KingdomCreedVineTrellis", "r_KingdomCreedLivingTrunk",
        }
        expected = {"M": 2, "L": 5, "XL": 10}
        records = [
            record for record in result.records
            if record.context.plan_key.startswith("creed-")
        ]
        self.assertEqual(90, len(records))
        self.assertEqual(510, sum(record.fixture_cells for record in records))
        resolved_cells = 0
        resolved_blueprints = set()
        for record in records:
            self.assertEqual(expected[record.context.target_size], record.fixture_cells,
                             record.generated_key)
            architecture_map = generated[record.generated_key]
            source = sources[record.source_key]
            glyphs = GENERATOR._glyphs(architecture_map)
            placed = 0
            for y, row in enumerate(architecture_map.findall("row")):
                for x, char in enumerate(row.get("Cells", "")):
                    inside = (
                        record.offset_x <= x < record.offset_x + int(source.get("Width"))
                        and record.offset_y <= y < record.offset_y + int(source.get("Height"))
                    )
                    glyph = glyphs.get(char)
                    if (
                        not inside and glyph is not None and glyph.get("Object")
                        and glyph.get("Claim") == "yard"
                    ):
                        self.assertIsNone(glyph.get("Anchors"), record.generated_key)
                        self.assertIsNone(glyph.get("Stateful"), record.generated_key)
                        object_reference = glyph.get("Object")
                        self.assertIn(object_reference, inert_references,
                                      record.generated_key)
                        slots = {
                            "$" + slot.get("Key"): slot
                            for slot in palettes[record.context.palette_key].findall("slot")
                        }
                        slot = slots[object_reference]
                        blueprint_name = slot.get("Blueprint")
                        blueprint = objects[blueprint_name]
                        self.assertEqual("Furniture", blueprint.get("Inherits"),
                                         blueprint_name)
                        self.assertTrue(all(child.tag == "part" for child in blueprint),
                                        blueprint_name)
                        parts = {part.get("Name") for part in blueprint.findall("part")}
                        self.assertTrue({"Render", "Description", "Physics"} <= parts,
                                        blueprint_name)
                        self.assertTrue(parts <= allowed_parts, blueprint_name)
                        physics = next(part for part in blueprint.findall("part")
                                       if part.get("Name") == "Physics")
                        self.assertEqual("false", physics.get("Takeable"), blueprint_name)
                        self.assertEqual("false", physics.get("Solid"), blueprint_name)
                        resolved_blueprints.add(blueprint_name)
                        resolved_cells += 1
                        placed += 1
            self.assertEqual(record.fixture_cells, placed, record.generated_key)
        self.assertEqual(510, resolved_cells)
        self.assertEqual(expected_blueprints, resolved_blueprints)

    def test_l_and_xl_surfaces_cannot_be_near_monoculture_fills(self) -> None:
        result = GENERATOR.materialize(GENERATOR_PATH.parents[1])
        checked = 0
        for record in result.records:
            if record.hosted_hold or record.context.target_size not in {"L", "XL"}:
                continue
            surfaces = record.yard_cells + record.path_cells
            self.assertGreater(record.yard_cells, 0, record.generated_key)
            self.assertGreater(record.path_cells, 0, record.generated_key)
            self.assertGreaterEqual(
                min(record.yard_cells, record.path_cells) * 14,
                surfaces,
                record.generated_key,
            )
            checked += 1
        self.assertEqual(260, checked)

    def test_upgrade_family_identity_comes_from_exact_catalogue_graph(self) -> None:
        buildings = GENERATOR._buildings(GENERATOR_PATH.parents[1])
        families = GENERATOR._upgrade_families(buildings)
        self.assertEqual(families["field"], "field>fieldrows")
        self.assertEqual(families["fieldrows"], "field>fieldrows")
        self.assertEqual(
            families["rampart"],
            "palisade>rampart;rubblewall>rampart",
        )
        self.assertEqual(families["palisade"], families["rampart"])
        self.assertEqual(families["rubblewall"], families["rampart"])
        self.assertEqual(families["bench"], "bench")

    def test_natural_structural_slots_cannot_become_walkable_yard_ground(self) -> None:
        source = ET.fromstring(
            '<map Key="cellar"><glyph Char="P" Ground="$floor" Structure="$rock" '
            'Claim="building" Pass="walk" Cover="natural" /></map>'
        )
        palette = ET.fromstring(
            '<palette Key="fungal">'
            '<slot Key="floor" Blueprint="FungalTrailBrick" Role="damp-floor" '
            'Material="mud" MinTech="hands" Natural="yes" />'
            '<slot Key="rock" Blueprint="r_KingdomStructureLimestone" Role="natural-rock" '
            'Material="stone" MinTech="hands" Natural="yes" />'
            '</palette>'
        )
        self.assertEqual("$floor", GENERATOR._ground_reference(source, palette, False))
        self.assertEqual("$floor", GENERATOR._ground_reference(source, palette, True))

    def test_vanilla_dirt_floor_and_path_share_one_native_visual_treatment(self) -> None:
        self.assertEqual(
            GENERATOR._visual_blueprint_key("DirtFloor"),
            GENERATOR._visual_blueprint_key("DirtPath"),
        )
        self.assertNotEqual(
            GENERATOR._visual_blueprint_key("DirtFloor"),
            GENERATOR._visual_blueprint_key("r_KingdomGroundTroddenPath"),
        )

    def test_distinct_lawful_ground_and_path_stay_visibly_distinct(self) -> None:
        repository = GENERATOR_PATH.parents[1]
        result = GENERATOR.materialize(repository)
        generated = {
            item.get("Key"): item
            for item in ET.fromstring(result.text).findall("map")
        }
        maps = {}
        palettes = {}
        for _path, root in GENERATOR._source_roots(repository):
            maps.update({item.get("Key"): item for item in root.findall("map")})
            palettes.update(
                {item.get("Key"): item for item in root.findall("palette")}
            )

        hut = maps["housing-hut-s0"]
        hut_palette = palettes["housing-timber-hands"]
        self.assertEqual("$ground", GENERATOR._ground_reference(hut, hut_palette, False))
        self.assertEqual("$lotpath", GENERATOR._ground_reference(hut, hut_palette, True))
        hut_glyphs = GENERATOR._glyphs(generated["housing-hut-s0-lot-xl-heart"])
        self.assertEqual("$ground", hut_glyphs["y"].get("Ground"))
        self.assertEqual("$lotpath", hut_glyphs["p"].get("Ground"))
        hut_slots = {
            "$" + item.get("Key", ""): item for item in hut_palette.findall("slot")
        }
        self.assertNotEqual(
            hut_slots[hut_glyphs["y"].get("Ground")].get("Blueprint"),
            hut_slots[hut_glyphs["p"].get("Ground")].get("Blueprint"),
        )
        self.assertNotEqual(
            GENERATOR._visual_blueprint_key(
                hut_slots[hut_glyphs["y"].get("Ground")].get("Blueprint")
            ),
            GENERATOR._visual_blueprint_key(
                hut_slots[hut_glyphs["p"].get("Ground")].get("Blueprint")
            ),
        )

        checked = 0
        for record in result.records:
            if record.hosted_hold:
                continue
            source = maps[record.source_key]
            palette = palettes[record.context.palette_key]
            slots = {
                "$" + item.get("Key", ""): item for item in palette.findall("slot")
            }
            yard_reference = GENERATOR._ground_reference(source, palette, False)
            path_reference = GENERATOR._ground_reference(source, palette, True)
            self.assertNotEqual(yard_reference, path_reference, record.generated_key)
            self.assertNotEqual(
                slots[yard_reference].get("Blueprint"),
                slots[path_reference].get("Blueprint"),
                record.generated_key,
            )
            self.assertNotEqual(
                GENERATOR._visual_blueprint_key(
                    slots[yard_reference].get("Blueprint")
                ),
                GENERATOR._visual_blueprint_key(
                    slots[path_reference].get("Blueprint")
                ),
                record.generated_key,
            )
            generated_references = {
                item.get("Ground") for item in generated[record.generated_key].findall("glyph")
            }
            self.assertIn(yard_reference, generated_references, record.generated_key)
            self.assertIn(path_reference, generated_references, record.generated_key)
            checked += 1
        self.assertEqual(334, checked)

    def test_only_food_grammar_uses_repeated_full_width_bands(self) -> None:
        signatures = {}
        for category, policy in GENERATOR.CATEGORY_POLICIES.items():
            context = GENERATOR.RealizationContext(
                f"sample-{category}",
                f"binding-{category}",
                f"work-{category}",
                f"work-{category}",
                category,
                "S",
                "XL",
                "heart",
                f"palette-{category}",
                category == "food",
                frozenset(),
            )
            paths = frozenset(
                (x, y)
                for y in range(14)
                for x in range(20)
                if GENERATOR._yard_kind(policy, context, "N", x, y, 20, 14)
                == "path"
            )
            signatures[category] = paths
            full_rows = sum(
                all((x, y) in paths for x in range(20)) for y in range(14)
            )
            if category == "food":
                self.assertGreater(full_rows, 1)
            else:
                self.assertEqual(0, full_rows, category)
        self.assertEqual(len(signatures), len(set(signatures.values())))

    def test_unrelated_upgrade_families_have_distinct_overlay_fingerprints(self) -> None:
        result = GENERATOR.materialize(GENERATOR_PATH.parents[1])
        fingerprints = {}
        for record in result.records:
            if record.hosted_hold:
                continue
            key = (record.context.target_size, record.overlay_fingerprint)
            previous = fingerprints.get(key)
            if previous is not None:
                self.assertEqual(
                    previous.context.upgrade_family,
                    record.context.upgrade_family,
                    f"{previous.generated_key} and {record.generated_key}",
                )
            fingerprints[key] = record
        self.assertEqual(239, len(fingerprints))

    def test_generated_successors_preserve_every_shared_exterior_kind(self) -> None:
        repository = GENERATOR_PATH.parents[1]
        result = GENERATOR.materialize(repository)
        generated_root = ET.fromstring(result.text)
        generated_maps = {
            item.get("Key"): item for item in generated_root.findall("map")
        }
        records = {item.generated_key: item for item in result.records}
        source_maps = {}
        palettes = {}
        for _path, root in GENERATOR._source_roots(repository):
            source_maps.update(
                {item.get("Key"): item for item in root.findall("map")}
            )
            palettes.update(
                {item.get("Key"): item for item in root.findall("palette")}
            )

        def kind_mask(record):
            architecture_map = generated_maps[record.generated_key]
            source = source_maps[record.source_key]
            palette = palettes[record.context.palette_key]
            yard_reference = GENERATOR._ground_reference(source, palette, False)
            path_reference = GENERATOR._ground_reference(source, palette, True)
            boundary_char = GENERATOR._boundary_char(source)
            glyphs = GENERATOR._glyphs(architecture_map)
            rows = []
            for row in architecture_map.findall("row"):
                kinds = []
                for char in row.get("Cells", ""):
                    glyph = glyphs.get(char)
                    if char == ".":
                        kind = "route/open"
                    elif boundary_char and char == boundary_char:
                        kind = "boundary"
                    elif (
                        glyph is not None
                        and glyph.get("Ground") == path_reference
                        and not glyph.get("Structure")
                        and not glyph.get("Object")
                        and not glyph.get("Anchors")
                    ):
                        kind = "path"
                    elif (
                        glyph is not None
                        and glyph.get("Ground") == yard_reference
                        and not glyph.get("Structure")
                        and not glyph.get("Object")
                        and not glyph.get("Anchors")
                    ):
                        kind = "yard"
                    else:
                        kind = "source"
                    kinds.append(kind)
                rows.append(kinds)
            return rows

        buildings = GENERATOR._buildings(repository)
        edges = {
            (source, target.strip())
            for source, building in buildings.items()
            for target in building.get("UpgradesTo", "").split(",")
            if target.strip()
        }
        checked = 0
        for plan in generated_root.findall("plan"):
            for binding in plan.findall("binding"):
                tiers = {
                    item.get("BuildKey"): item for item in binding.findall("tier")
                }
                for predecessor_key, successor_key in edges:
                    if predecessor_key not in tiers or successor_key not in tiers:
                        continue
                    predecessor = tiers[predecessor_key]
                    successor = tiers[successor_key]
                    predecessor_maps = {predecessor.get("Map")}
                    predecessor_maps.update(
                        item.get("Map")
                        for item in predecessor.findall("variant")
                        if item.get("Map")
                    )
                    successor_maps = {successor.get("Map")}
                    successor_maps.update(
                        item.get("Map")
                        for item in successor.findall("variant")
                        if item.get("Map")
                    )
                    for predecessor_map in predecessor_maps:
                        for successor_map in successor_maps:
                            before = records[predecessor_map]
                            after = records[successor_map]
                            before_kinds = kind_mask(before)
                            after_kinds = kind_mask(after)
                            before_source = source_maps[before.source_key]
                            after_source = source_maps[after.source_key]
                            for y in range(len(before_kinds)):
                                for x in range(len(before_kinds[0])):
                                    inside_before = (
                                        before.offset_x <= x < before.offset_x + int(before_source.get("Width"))
                                        and before.offset_y <= y < before.offset_y + int(before_source.get("Height"))
                                    )
                                    inside_after = (
                                        after.offset_x <= x < after.offset_x + int(after_source.get("Width"))
                                        and after.offset_y <= y < after.offset_y + int(after_source.get("Height"))
                                    )
                                    if inside_before or inside_after:
                                        continue
                                    self.assertEqual(
                                        before_kinds[y][x],
                                        after_kinds[y][x],
                                        f"{plan.get('Key')} {predecessor_map}->{successor_map} at {x},{y}",
                                    )
                            checked += 1
        self.assertEqual(177, checked)

    def test_every_source_coordinate_is_preserved_at_its_recorded_offset(self) -> None:
        repository = GENERATOR_PATH.parents[1]
        result = GENERATOR.materialize(repository)
        generated_root = ET.fromstring(result.text)
        generated = {item.get("Key"): item for item in generated_root.findall("map")}
        sources = {}
        for _path, root in GENERATOR._source_roots(repository):
            sources.update({item.get("Key"): item for item in root.findall("map")})
        for record in result.records:
            source = sources[record.source_key]
            target = generated[record.generated_key]
            source_rows = [row.get("Cells", "") for row in source.findall("row")]
            target_rows = [row.get("Cells", "") for row in target.findall("row")]
            for y, row in enumerate(source_rows):
                for x, char in enumerate(row):
                    self.assertEqual(
                        char,
                        target_rows[record.offset_y + y][record.offset_x + x],
                        f"{record.generated_key} source {x},{y}",
                    )

    def test_every_generated_binding_preserves_the_complete_source_tier_set(self) -> None:
        repository = GENERATOR_PATH.parents[1]
        generated_root = ET.fromstring(GENERATOR.materialize(repository).text)
        generated_plans = {
            item.get("Key"): item for item in generated_root.findall("plan")
        }
        checked = 0
        for _path, root in GENERATOR._source_roots(repository):
            for source_plan in root.findall("plan"):
                source_plan_key = source_plan.get("Key", "")
                for source_binding in source_plan.findall("binding"):
                    source_tiers = source_binding.findall("tier")
                    build_keys = {item.get("BuildKey", "") for item in source_tiers}
                    if not source_tiers or build_keys & GENERATOR.HEART_BUILD_KEYS:
                        continue
                    source_size = source_binding.get("Size", "")
                    source_set = {
                        (item.get("Key"), item.get("BuildKey"), item.get("Level"))
                        for item in source_tiers
                    }
                    for target_size in GENERATOR.LOT_ORDER[
                        GENERATOR.LOT_ORDER.index(source_size) + 1 :
                    ]:
                        plan_key = (
                            f"lot-{target_size.lower()}-{source_plan_key}-"
                            f"{source_binding.get('Key', '')}"
                        )
                        generated_plan = generated_plans[plan_key]
                        binding = generated_plan.find("binding")
                        self.assertIsNotNone(binding, plan_key)
                        generated_set = {
                            (item.get("Key"), item.get("BuildKey"), item.get("Level"))
                            for item in binding.findall("tier")
                        }
                        self.assertEqual(source_set, generated_set, plan_key)
                        checked += 1
        self.assertEqual(242, checked)


if __name__ == "__main__":
    unittest.main()
