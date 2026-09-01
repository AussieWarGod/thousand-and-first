#!/usr/bin/env python3
"""Determinism and concrete-output tests for the exact-lot materialiser."""

from __future__ import annotations

import importlib.util
import sys
import unittest
import xml.etree.ElementTree as ET
from pathlib import Path


GENERATOR_PATH = Path(__file__).resolve().parents[1] / "generate-lot-realizations.py"
SPEC = importlib.util.spec_from_file_location(
    "taf_generate_lot_realizations", GENERATOR_PATH
)
if SPEC is None or SPEC.loader is None:
    raise RuntimeError(f"cannot load {GENERATOR_PATH}")
GENERATOR = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = GENERATOR
SPEC.loader.exec_module(GENERATOR)


class LotRealizationGeneratorTests(unittest.TestCase):
    def test_layer_transforms_remove_or_replace_matching_orientation(self) -> None:
        glyph = ET.Element(
            "glyph",
            {
                "Char": "x",
                "Ground": "$ground",
                "GroundOrientation": "north",
                "Structure": "$wall",
                "StructureOrientation": "west",
                "Object": "$fixture",
                "ObjectOrientation": "east",
                "Anchors": "fixture:test",
                "Stateful": "yes",
                "Claim": "building",
                "Pass": "adjacent",
                "Cover": "walled",
            },
        )
        structural = GENERATOR._glyph_attributes_without_custody(
            glyph, preserve_structure=True
        )
        self.assertNotIn("Object", structural)
        self.assertNotIn("ObjectOrientation", structural)
        self.assertEqual(structural["StructureOrientation"], "west")
        background = GENERATOR._glyph_attributes_without_custody(
            glyph, preserve_structure=False
        )
        self.assertNotIn("Structure", background)
        self.assertNotIn("StructureOrientation", background)
        self.assertEqual(background["GroundOrientation"], "north")

        for layer in ("Ground", "Structure", "Object"):
            attributes = dict(glyph.attrib)
            GENERATOR._remove_layer(attributes, layer)
            self.assertNotIn(layer, attributes)
            self.assertNotIn(layer + "Orientation", attributes)

        attributes = dict(glyph.attrib)
        GENERATOR._replace_layer(attributes, "Ground", "$new-ground")
        self.assertEqual(attributes["Ground"], "$new-ground")
        self.assertNotIn("GroundOrientation", attributes)
        unchanged = dict(glyph.attrib)
        GENERATOR._replace_layer(unchanged, "Ground", "$ground")
        self.assertEqual(unchanged["GroundOrientation"], "north")

    def test_generated_output_has_no_orphan_layer_orientation(self) -> None:
        root = ET.fromstring(GENERATOR.materialize(GENERATOR_PATH.parents[1]).text)
        for glyph in root.findall("./map/glyph"):
            for layer in ("Ground", "Structure", "Object"):
                if glyph.get(layer + "Orientation") is not None:
                    self.assertIsNotNone(
                        glyph.get(layer),
                        f"{glyph.get('Char')} has orphan {layer}Orientation",
                    )

    def test_normalized_source_catalogues_use_only_canonical_envelopes(self) -> None:
        repository = GENERATOR_PATH.parents[1]
        canonical = set(GENERATOR.LOT_DIMENSIONS.values())
        checked = 0
        for name in (
            "KingdomArchitectures-HousingWater.xml",
            "KingdomArchitectures-Production.xml",
            "KingdomArchitectures-CivicFaith.xml",
        ):
            root = ET.parse(repository / "Architecture" / name).getroot()
            for architecture_map in root.findall("map"):
                dimensions = (
                    int(architecture_map.get("Width", "0")),
                    int(architecture_map.get("Height", "0")),
                )
                self.assertIn(dimensions, canonical, architecture_map.get("Key"))
                checked += 1
        self.assertGreater(checked, 70)

    def test_checked_in_output_is_current_concrete_and_exact(self) -> None:
        repository = GENERATOR_PATH.parents[1]
        result = GENERATOR.materialize(repository)
        expected = result.text
        map_count = result.map_count
        plan_count = result.plan_count
        tier_count = result.tier_count
        target = repository / "Architecture" / GENERATOR.OUTPUT_NAME
        self.assertEqual(target.read_text(encoding="utf-8"), expected)
        self.assertEqual((map_count, plan_count, tier_count), (146, 107, 122))
        self.assertEqual(expected.count("<!-- realization source="), map_count)

        root = ET.fromstring(expected)
        maps = {item.get("Key"): item for item in root.findall("map")}
        self.assertEqual(len(maps), map_count)
        self.assertEqual(len(root.findall("plan")), plan_count)
        self.assertEqual(
            GENERATOR.IDENTITY_SELECTOR_ATTRIBUTES,
            ("Cultures", "Species", "Genotypes", "Bodies"),
        )
        variants = root.findall("./plan/binding/tier/variant")
        self.assertTrue(variants)
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
                        (
                            int(architecture_map.get("Width")),
                            int(architecture_map.get("Height")),
                        ),
                        dimensions,
                    )

    def test_all_generated_maps_have_exact_runtime_lanes_and_campus_thresholds(
        self,
    ) -> None:
        repository = GENERATOR_PATH.parents[1]
        result = GENERATOR.materialize(repository)
        root = ET.fromstring(result.text)
        maps = {item.get("Key"): item for item in root.findall("map")}
        checked_maps = set()
        for binding in root.findall("./plan/binding"):
            for tier in binding.findall("tier"):
                keys = {tier.get("Map")}
                keys.update(
                    variant.get("Map")
                    for variant in tier.findall("variant")
                    if variant.get("Map")
                )
                for key in keys:
                    checked_maps.add(key)
                    architecture_map = maps[key]
                    width = int(architecture_map.get("Width", "0"))
                    height = int(architecture_map.get("Height", "0"))
                    canvas = [
                        list(row.get("Cells", ""))
                        for row in architecture_map.findall("row")
                    ]
                    entrances = GENERATOR._public_entrances(architecture_map)
                    self.assertTrue(entrances, key)
                    glyphs = GENERATOR._glyphs(architecture_map)
                    visible_thresholds = []
                    threshold_sides = []
                    door_sides = []
                    for x, y in entrances:
                        authored_lane = GENERATOR._runtime_authored_lane(
                            canvas, (x, y)
                        )
                        self.assertIsNotNone(
                            authored_lane, f"{key} entrance {x},{y}"
                        )
                        route, lane = authored_lane
                        egress = GENERATOR._unclaimed_route(canvas, (x, y))
                        edge_x, edge_y = egress[-1] if egress else (x, y)
                        exit_side = (
                            "N"
                            if edge_y == 0
                            else "E"
                            if edge_x == width - 1
                            else "S"
                            if edge_y == height - 1
                            else "W"
                        )
                        self.assertTrue(route, f"{key} has no reserved margin")
                        self.assertEqual(
                            abs(route[-1][0] - lane[0])
                            + abs(route[-1][1] - lane[1]),
                            1,
                        )
                        glyph = glyphs[canvas[y][x]]
                        if (
                            glyph.get("Ground") == "$lotpath"
                            and (x in {0, width - 1} or y in {0, height - 1})
                        ):
                            visible_thresholds.append((x, y))
                            threshold_sides.append(exit_side)
                            self.assertEqual(len(route), GENERATOR.ROAD_MARGIN)
                            self.assertTrue(
                                any(
                                    0 <= x + dx < width
                                    and 0 <= y + dy < height
                                    and (
                                        neighbor := glyphs.get(
                                            canvas[y + dy][x + dx]
                                        )
                                    )
                                    is not None
                                    and neighbor.get("Ground") == "$lotpath"
                                    and neighbor.get("Claim") == "yard"
                                    and neighbor.get("Pass") == "walk"
                                    for dx, dy in (
                                        (0, -1),
                                        (1, 0),
                                        (0, 1),
                                        (-1, 0),
                                    )
                                ),
                                f"{key} threshold has no visible cardinal path",
                            )
                        else:
                            door_sides.append(exit_side)
                    if key.startswith("creed-"):
                        self.assertEqual(
                            len(visible_thresholds),
                            1,
                            f"{key} needs one visible claimed path threshold",
                        )
                        self.assertIn(
                            threshold_sides[0],
                            door_sides,
                            f"{key} threshold left its authored door facade",
                        )
                    else:
                        self.assertEqual(0, len(visible_thresholds), key)
        self.assertEqual(len(checked_maps), 146)

    def test_site_census_accounts_every_cell_after_renovation(
        self,
    ) -> None:
        result = GENERATOR.materialize(GENERATOR_PATH.parents[1])
        totals = tuple(
            sum(getattr(record, field) for record in result.records)
            for field in (
                "site_cells",
                "transformed_cells",
                "structure_cells",
                "feature_cells",
                "yard_cells",
                "path_cells",
                "boundary_cells",
                "route_cells",
                "intentional_open_cells",
                "inaccessible_open_cells",
            )
        )
        (
            site,
            transformed,
            structure,
            features,
            yard,
            path,
            boundary,
            route,
            intentional,
            inaccessible,
        ) = totals
        self.assertGreater(site, 0)
        self.assertGreater(transformed, 0)
        self.assertGreater(structure, features)
        self.assertGreater(features, boundary)
        self.assertGreater(yard, path)
        self.assertGreater(path, route)
        self.assertGreater(route, intentional)
        self.assertEqual(0, inaccessible)
        self.assertEqual(sum(record.hosted_hold for record in result.records), 0)
        for record in result.records:
            facts = record.yard_cells + record.path_cells + record.boundary_cells
            self.assertGreater(record.transformed_cells, 0, record.generated_key)
            self.assertGreater(
                record.structure_cells + record.feature_cells,
                1,
                record.generated_key,
            )
            self.assertGreater(facts, 0, record.generated_key)
            if record.context.plan_key in GENERATOR.CREED_CAMPUS_PROGRAMMES:
                self.assertLessEqual(
                    record.site_cells,
                    record.fixture_cells
                    * GENERATOR.MAX_NEW_SITE_CELLS_PER_SEMANTIC_FIXTURE,
                    record.generated_key,
                )
            else:
                self.assertIn(
                    record.context.plan_key,
                    GENERATOR.SITE_EXPANSION_PROGRAMMES,
                    record.generated_key,
                )
                self.assertEqual(0, record.fixture_cells, record.generated_key)
                self.assertGreaterEqual(record.programme_regions, 2, record.generated_key)
            if record.intentional_open_cells:
                self.assertTrue(record.open_reason, record.generated_key)
                self.assertIn(
                    record.context.build_key,
                    GENERATOR.INTENTIONAL_OPEN_REASONS,
                    record.generated_key,
                )
            self.assertEqual(
                record.site_cells,
                record.transformed_cells
                + facts
                + record.route_cells
                + record.intentional_open_cells
                + record.inaccessible_open_cells,
                record.generated_key,
            )

    def test_only_explicitly_programmed_families_gain_larger_bindings(
        self,
    ) -> None:
        result = GENERATOR.materialize(GENERATOR_PATH.parents[1])
        by_source = {}
        for record in result.records:
            by_source.setdefault(record.source_key, {})[record.context.target_size] = (
                record
            )
        self.assertEqual(
            {
                "canvas-shelter",
                "timber-hut",
                "mud-hut",
                "ruin-block-hut",
                "deepend-underbench",
                "deepend-reliquary",
                "deepend-factorhouse",
            },
            set(GENERATOR.SITE_EXPANSION_PROGRAMMES),
        )
        self.assertEqual(set(by_source["housing-hut-s0"]), {"M", "L", "XL"})
        self.assertEqual(set(by_source["deepend-underbench-m0"]), {"L", "XL"})
        self.assertEqual(set(by_source["deepend-reliquary-l0"]), {"XL"})
        self.assertEqual(set(by_source["deepend-factorhouse-m0"]), {"L", "XL"})
        self.assertNotIn("production-plot-s0", by_source)
        records = by_source["creed-joppa-seedhouse-s0"]
        self.assertEqual(set(records), {"M", "L", "XL"})
        expected_fixtures = {"M": 2, "L": 5, "XL": 10}
        for size, fixture_count in expected_fixtures.items():
            record = records[size]
            self.assertEqual(fixture_count, record.fixture_cells)
            self.assertEqual(record.envelope_width, 6)
            self.assertEqual(record.envelope_height, 4)
            self.assertGreater(record.yard_cells, 0)
            self.assertGreater(record.path_cells, 0)

    def test_every_shipped_creed_has_one_reviewed_campus_programme(self) -> None:
        source = ET.parse(
            GENERATOR_PATH.parents[1]
            / "Architecture"
            / "KingdomArchitectures-Creeds.xml"
        ).getroot()
        plan_keys = {plan.get("Key") for plan in source.findall("plan")}
        programmes = GENERATOR.CREED_CAMPUS_PROGRAMMES
        self.assertEqual(plan_keys, set(programmes))
        self.assertEqual(30, len(programmes))
        self.assertEqual(30, len({item.key for item in programmes.values()}))
        self.assertEqual(
            {"paired-bays", "court-edge", "outer-bays", "alternating-bays"},
            {item.fixture_pattern for item in programmes.values()},
        )
        for plan_key, programme in programmes.items():
            self.assertIn(programme.axis_bias, {-1, 0, 1}, plan_key)
            self.assertIn(programme.court_bias, {-1, 0, 1}, plan_key)
            self.assertIn(programme.bay_growth, {0, 1}, plan_key)
            self.assertIn(programme.handedness, {-1, 1}, plan_key)
            self.assertIn(programme.rhythm, {0, 1, 2}, plan_key)

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
        objects_root = ET.parse(
            repository / "RuntimeData" / "ObjectBlueprints.xml"
        ).getroot()
        objects = {item.get("Name"): item for item in objects_root.iter("object")}
        allowed_parts = {"Render", "Description", "Physics", "Metal"}
        inert_references = set(GENERATOR.CREED_EXPANSION_INERT_OBJECTS.values())
        expected_blueprints = {
            "r_KingdomCreedPracticeSeedHamper",
            "r_KingdomCreedPracticeSpiceJar",
            "r_KingdomCreedPracticeMeatCache",
            "r_KingdomCreedPracticeLabelledBin",
            "r_KingdomCreedPracticeTable",
            "r_KingdomCreedPracticeColdHearth",
            "r_KingdomCreedPracticeShelf",
            "r_KingdomCreedPracticeStone",
            "r_KingdomCreedPracticeDryBasin",
            "r_KingdomCreedPracticeBench",
            "r_KingdomCreedPracticePallet",
            "r_KingdomCreedSpindleWheel",
            "r_KingdomCreedDryContact",
            "r_KingdomCreedGoatfolkChallengePennon",
            "r_KingdomCreedScrapAltar",
            "r_KingdomCreedPracticeArmsRack",
            "r_KingdomCreedColdBrazier",
            "r_KingdomCreedVineTrellis",
        }
        expected = {"M": 2, "L": 5, "XL": 10}
        records = [
            record
            for record in result.records
            if record.context.plan_key.startswith("creed-")
        ]
        self.assertEqual(93, len(records))
        self.assertEqual(527, sum(record.fixture_cells for record in records))
        resolved_cells = 0
        resolved_blueprints = set()
        for record in records:
            self.assertEqual(
                expected[record.context.target_size],
                record.fixture_cells,
                record.generated_key,
            )
            architecture_map = generated[record.generated_key]
            glyphs = GENERATOR._glyphs(architecture_map)
            placed = 0
            positions = []
            for y, row in enumerate(architecture_map.findall("row")):
                for x, char in enumerate(row.get("Cells", "")):
                    glyph = glyphs.get(char)
                    if (
                        glyph is not None
                        and glyph.get("Object")
                        and glyph.get("Claim") == "yard"
                        and glyph.get("Object") in inert_references
                        and not glyph.get("Anchors")
                        and glyph.get("Stateful") != "yes"
                    ):
                        self.assertIsNone(glyph.get("Anchors"), record.generated_key)
                        self.assertIsNone(glyph.get("Stateful"), record.generated_key)
                        object_reference = glyph.get("Object")
                        self.assertIn(
                            object_reference, inert_references, record.generated_key
                        )
                        slots = {
                            "$" + slot.get("Key"): slot
                            for slot in palettes[record.context.palette_key].findall(
                                "slot"
                            )
                        }
                        slot = slots[object_reference]
                        blueprint_name = slot.get("Blueprint")
                        blueprint = objects[blueprint_name]
                        self.assertEqual(
                            "Furniture", blueprint.get("Inherits"), blueprint_name
                        )
                        self.assertTrue(
                            all(child.tag == "part" for child in blueprint),
                            blueprint_name,
                        )
                        parts = {part.get("Name") for part in blueprint.findall("part")}
                        self.assertTrue(
                            {"Render", "Description", "Physics"} <= parts,
                            blueprint_name,
                        )
                        self.assertTrue(parts <= allowed_parts, blueprint_name)
                        physics = next(
                            part
                            for part in blueprint.findall("part")
                            if part.get("Name") == "Physics"
                        )
                        self.assertEqual(
                            "false", physics.get("Takeable"), blueprint_name
                        )
                        self.assertEqual("false", physics.get("Solid"), blueprint_name)
                        resolved_blueprints.add(blueprint_name)
                        resolved_cells += 1
                        placed += 1
                        positions.append((x, y))
            self.assertEqual(record.fixture_cells, placed, record.generated_key)
            self.assertEqual(expected[record.context.target_size] // 2, record.station_pairs)
            side = GENERATOR._dominant_frontage_side(sources[record.source_key])
            width = int(architecture_map.get("Width"))
            height = int(architecture_map.get("Height"))
            oriented = [
                GENERATOR._oriented(x, y, width, height, side)[:2]
                for x, y in positions
            ]
            span = width if side in {"N", "S"} else height
            depth_span = height if side in {"N", "S"} else width
            regions = {
                (
                    0 if cross * 2 < span else 1,
                    min(2, depth * 3 // depth_span),
                )
                for cross, depth in oriented
            }
            required_regions = {"M": 2, "L": 4, "XL": 5}[
                record.context.target_size
            ]
            required_depths = {"M": 1, "L": 2, "XL": 3}[
                record.context.target_size
            ]
            self.assertGreaterEqual(
                len(regions), required_regions, record.generated_key
            )
            self.assertEqual(
                {0, 1}, {region[0] for region in regions}, record.generated_key
            )
            self.assertGreaterEqual(
                len({region[1] for region in regions}),
                required_depths,
                record.generated_key,
            )
            self.assertEqual(len(regions), record.programme_regions)
        self.assertEqual(527, resolved_cells)
        self.assertEqual(expected_blueprints, resolved_blueprints)
        self.assertEqual(394, sum(record.programme_regions for record in records))
        self.assertEqual(248, sum(record.station_pairs for record in records))

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
        self.assertEqual(99, checked)

    def test_upgrade_family_identity_comes_from_exact_catalogue_graph(self) -> None:
        buildings = GENERATOR._buildings(GENERATOR_PATH.parents[1])
        families = GENERATOR._upgrade_families(buildings)
        self.assertEqual(
            families["field"], "field>fieldrows;fieldrows>grange"
        )
        self.assertEqual(families["grange"], families["field"])
        self.assertEqual(families["fieldrows"], families["field"])
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
            "</palette>"
        )
        self.assertEqual("$floor", GENERATOR._ground_reference(source, palette, False))
        self.assertEqual("$floor", GENERATOR._ground_reference(source, palette, True))

    def test_vanilla_dirt_floor_and_path_share_one_native_visual_treatment(
        self,
    ) -> None:
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
            item.get("Key"): item for item in ET.fromstring(result.text).findall("map")
        }
        maps = {}
        palettes = {}
        for _path, root in GENERATOR._source_roots(repository):
            maps.update({item.get("Key"): item for item in root.findall("map")})
            palettes.update({item.get("Key"): item for item in root.findall("palette")})

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
                GENERATOR._visual_blueprint_key(slots[yard_reference].get("Blueprint")),
                GENERATOR._visual_blueprint_key(slots[path_reference].get("Blueprint")),
                record.generated_key,
            )
            generated_references = {
                item.get("Ground")
                for item in generated[record.generated_key].findall("glyph")
            }
            self.assertIn(yard_reference, generated_references, record.generated_key)
            self.assertIn(path_reference, generated_references, record.generated_key)
            checked += 1
        self.assertEqual(146, checked)

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
                for y in range(18)
                for x in range(20)
                if GENERATOR._yard_kind(policy, context, "N", x, y, 20, 18) == "path"
            )
            signatures[category] = paths
            full_rows = sum(all((x, y) in paths for x in range(20)) for y in range(18))
            if category == "food":
                self.assertGreater(full_rows, 1)
            else:
                self.assertEqual(0, full_rows, category)
        self.assertEqual(len(signatures), len(set(signatures.values())))

    def test_unrelated_upgrade_families_have_distinct_overlay_fingerprints(
        self,
    ) -> None:
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
        self.assertEqual(115, len(fingerprints))

    def test_reviewed_geometry_ignores_incidental_receipt_identity(self) -> None:
        result = GENERATOR.materialize(GENERATOR_PATH.parents[1])
        checked = 0
        for record in result.records:
            context = record.context
            noisy = GENERATOR.RealizationContext(
                context.plan_key,
                context.binding_key + "-receipt-noise",
                context.build_key,
                context.upgrade_family + "-receipt-noise",
                context.category,
                context.source_size,
                context.target_size,
                context.facing,
                context.palette_key,
                context.open_design,
                context.reserved_route_cells,
            )
            policy = GENERATOR._policy_for(context)
            self.assertIsNotNone(policy, context.plan_key)
            width, height = GENERATOR.LOT_DIMENSIONS[context.target_size]
            original_mask = tuple(
                GENERATOR._yard_kind(policy, context, "S", x, y, width, height)
                for y in range(height)
                for x in range(width)
            )
            noisy_mask = tuple(
                GENERATOR._yard_kind(policy, noisy, "S", x, y, width, height)
                for y in range(height)
                for x in range(width)
            )
            self.assertEqual(original_mask, noisy_mask, record.generated_key)
            for cell in ((0, 0), (width // 2, height // 2), (width - 1, height - 1)):
                self.assertEqual(
                    GENERATOR._site_choice_key(
                        context, "S", cell, width, height, "test"
                    ),
                    GENERATOR._site_choice_key(
                        noisy, "S", cell, width, height, "test"
                    ),
                    record.generated_key,
                )
            checked += 1
        self.assertEqual(146, checked)

    def test_generated_bindings_do_not_invent_upgrade_transitions(
        self,
    ) -> None:
        repository = GENERATOR_PATH.parents[1]
        result = GENERATOR.materialize(repository)
        generated_root = ET.fromstring(result.text)
        transition_tiers = []
        for plan in generated_root.findall("plan"):
            for binding in plan.findall("binding"):
                for tier in binding.findall("tier"):
                    transition = tier.get("Transition")
                    if transition is None:
                        continue
                    self.assertEqual("renovate", transition, plan.get("Key"))
                    self.assertEqual("1", tier.get("Level"), plan.get("Key"))
                    self.assertIn(
                        tier.get("BuildKey"),
                        {
                            "tentrow",
                            "hutyard",
                            "mudhutcourt",
                            "blockyard",
                            "robotservicebay",
                        },
                        plan.get("Key"),
                    )
                    transition_tiers.append(tier)
        self.assertEqual(15, len(transition_tiers))

    def test_every_generated_map_projects_features_once_inside_its_reviewed_envelope(self) -> None:
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
            source_width = int(source.get("Width"))
            source_height = int(source.get("Height"))
            if record.context.footprint_width:
                source_footprint = GENERATOR._authored_map_footprint(source)
                self.assertEqual(
                    (record.envelope_width, record.envelope_height),
                    (
                        record.context.footprint_width,
                        record.context.footprint_height,
                    ),
                )
                offset_x = record.footprint_x - source_footprint.x
                offset_y = record.footprint_y - source_footprint.y
                source_glyphs = GENERATOR._glyphs(source)
                protected_counts = {}
                for source_y, row in enumerate(source_rows):
                    for source_x, char in enumerate(row):
                        if char == ".":
                            continue
                        glyph = source_glyphs[char]
                        self.assertEqual(
                            char,
                            target_rows[offset_y + source_y][offset_x + source_x],
                            f"{record.generated_key} changed exact authored site "
                            f"{source_x},{source_y}",
                        )
                        if not (
                            glyph.get("Object")
                            or glyph.get("Anchors")
                            or glyph.get("Stateful") == "yes"
                        ):
                            continue
                        protected_counts[char] = protected_counts.get(char, 0) + 1
                for char, count in protected_counts.items():
                    self.assertEqual(
                        count,
                        sum(target_row.count(char) for target_row in target_rows),
                        f"{record.generated_key} cloned protected glyph {char!r}",
                    )
                continue
            if record.context.plan_key in (
                set(GENERATOR.CREED_CAMPUS_PROGRAMMES)
                | set(GENERATOR.DEEPEND_COMPOSED_SITE_PROGRAMMES)
            ):
                self.assertEqual(record.envelope_width, source_width)
                self.assertEqual(record.envelope_height, source_height)
            else:
                self.assertGreaterEqual(record.envelope_width, source_width)
                self.assertGreaterEqual(record.envelope_height, source_height)
                self.assertTrue(
                    record.envelope_width > source_width
                    or record.envelope_height > source_height,
                    record.generated_key,
                )
            source_nonempty = sum(char != "." for row in source_rows for char in row)
            if record.context.plan_key in GENERATOR.CREED_CAMPUS_PROGRAMMES:
                self.assertGreaterEqual(record.transformed_cells, source_nonempty)
            source_glyphs = GENERATOR._glyphs(source)
            feature_counts = {}
            for y, row in enumerate(source_rows):
                for x, char in enumerate(row):
                    glyph = source_glyphs.get(char)
                    if glyph is None or not (
                        glyph.get("Object") or glyph.get("Anchors")
                    ):
                        continue
                    target_x = record.envelope_x + GENERATOR._project_coordinate(
                        x, source_width, record.envelope_width
                    )
                    target_y = record.envelope_y + GENERATOR._project_coordinate(
                        y, source_height, record.envelope_height
                    )
                    self.assertEqual(char, target_rows[target_y][target_x])
                    feature_counts[char] = feature_counts.get(char, 0) + 1
            for char, count in feature_counts.items():
                self.assertEqual(
                    count,
                    sum(target_row.count(char) for target_row in target_rows),
                    f"{record.generated_key} cloned protected glyph {char!r}",
                )

    def test_explicit_footprints_keep_one_exact_shelter_and_use_added_lot_as_yard(
        self,
    ) -> None:
        repository = GENERATOR_PATH.parents[1]
        result = GENERATOR.materialize(repository)
        generated = {
            item.get("Key"): item
            for item in ET.fromstring(result.text).findall("map")
        }
        sources = {}
        for _path, root in GENERATOR._source_roots(repository):
            sources.update({item.get("Key"): item for item in root.findall("map")})

        explicit = 0
        implicit = 0
        for record in result.records:
            target = generated[record.generated_key]
            target_width = int(target.get("Width", "0"))
            target_height = int(target.get("Height", "0"))
            if not record.context.footprint_width:
                self.assertIsNone(target.get("Footprint"), record.generated_key)
                self.assertEqual(
                    (
                        record.footprint_x,
                        record.footprint_y,
                        record.footprint_width,
                        record.footprint_height,
                    ),
                    (0, 0, target_width, target_height),
                    record.generated_key,
                )
                implicit += 1
                continue

            explicit += 1
            self.assertEqual("housing", record.context.category)
            source = sources[record.source_key]
            source_footprint = GENERATOR._authored_map_footprint(source)
            target_footprint = GENERATOR._authored_map_footprint(target)
            self.assertEqual(
                (target_footprint.width, target_footprint.height),
                (
                    record.context.footprint_width,
                    record.context.footprint_height,
                ),
                record.generated_key,
            )
            self.assertEqual(
                target.get("Footprint"),
                f"{record.footprint_x},{record.footprint_y},"
                f"{record.footprint_width}x{record.footprint_height}",
            )
            self.assertEqual(
                target_footprint,
                GENERATOR.Envelope(
                    record.footprint_x,
                    record.footprint_y,
                    record.footprint_width,
                    record.footprint_height,
                ),
            )
            footprint_cells = {
                (x, y)
                for y in range(
                    target_footprint.y,
                    target_footprint.y + target_footprint.height,
                )
                for x in range(
                    target_footprint.x,
                    target_footprint.x + target_footprint.width,
                )
            }
            source_rows = [row.get("Cells", "") for row in source.findall("row")]
            target_rows = [row.get("Cells", "") for row in target.findall("row")]
            source_glyphs = GENERATOR._glyphs(source)
            target_glyphs = GENERATOR._glyphs(target)
            offset_x = target_footprint.x - source_footprint.x
            offset_y = target_footprint.y - source_footprint.y

            protected_source = set()
            for source_y, row in enumerate(source_rows):
                for source_x, char in enumerate(row):
                    if char == ".":
                        continue
                    self.assertEqual(
                        char,
                        target_rows[offset_y + source_y][offset_x + source_x],
                        f"{record.generated_key} changed source cell "
                        f"{source_x},{source_y}",
                    )
                    glyph = source_glyphs[char]
                    if (
                        glyph.get("Object")
                        or glyph.get("Anchors")
                        or glyph.get("Stateful") == "yes"
                    ):
                        protected_source.add(
                            (offset_x + source_x, offset_y + source_y)
                        )
            protected_target = {
                (x, y)
                for y, row in enumerate(target_rows)
                for x, char in enumerate(row)
                if char != "."
                and (
                    target_glyphs[char].get("Object")
                    or target_glyphs[char].get("Anchors")
                    or target_glyphs[char].get("Stateful") == "yes"
                )
            }
            self.assertEqual(
                protected_source, protected_target, record.generated_key
            )
            self.assertEqual(
                {
                    (offset_x + x, offset_y + y)
                    for x, y in GENERATOR._public_entrances(source)
                },
                set(GENERATOR._public_entrances(target)),
                record.generated_key,
            )

            source_building_count = sum(
                char != "." and source_glyphs[char].get("Claim") == "building"
                for row in source_rows
                for char in row
            )
            building_cells = {
                (x, y)
                for y, row in enumerate(target_rows)
                for x, char in enumerate(row)
                if char != "." and target_glyphs[char].get("Claim") == "building"
            }
            main_cells = {
                (x, y)
                for y, row in enumerate(target_rows)
                for x, char in enumerate(row)
                if char != "."
                and "main" in target_glyphs[char].get("Anchors", "").split(",")
            }
            self.assertEqual(
                source_building_count, len(building_cells), record.generated_key
            )
            self.assertTrue(main_cells, record.generated_key)
            self.assertTrue(
                building_cells | main_cells <= footprint_cells,
                record.generated_key,
            )

            remaining = set(building_cells)
            components = 0
            while remaining:
                components += 1
                seed = min(remaining, key=lambda cell: (cell[1], cell[0]))
                remaining.remove(seed)
                queue = [seed]
                while queue:
                    x, y = queue.pop()
                    for neighbor in (
                        (x, y - 1),
                        (x + 1, y),
                        (x, y + 1),
                        (x - 1, y),
                    ):
                        if neighbor in remaining:
                            remaining.remove(neighbor)
                            queue.append(neighbor)
            self.assertEqual(1, components, record.generated_key)
            self.assertEqual(1, record.composition_bays, record.generated_key)
            self.assertEqual(0, record.blind_wall_blocks, record.generated_key)
            self.assertGreater(record.yard_cells, 0, record.generated_key)
            self.assertGreater(record.path_cells, 0, record.generated_key)
            self.assertGreater(record.route_cells, 0, record.generated_key)
            for entrance in GENERATOR._public_entrances(target):
                self.assertIsNotNone(
                    GENERATOR._runtime_authored_lane(
                        [list(row) for row in target_rows], entrance
                    ),
                    f"{record.generated_key} entrance {entrance} has no runtime lane",
                )

        self.assertEqual(48, explicit)
        self.assertEqual(98, implicit)

    def test_exact_footprint_housing_uses_compact_family_courts_not_estate_grids(
        self,
    ) -> None:
        result = GENERATOR.materialize(GENERATOR_PATH.parents[1])
        records = [
            record for record in result.records if record.context.footprint_width
        ]
        limits = {"M": (5, 10), "L": (9, 22), "XL": (25, 35)}
        expected_regions = {"M": 2, "L": 3, "XL": 3}
        for record in records:
            lower, upper = limits[record.context.target_size]
            self.assertGreaterEqual(record.path_cells, lower, record.generated_key)
            self.assertLessEqual(record.path_cells, upper, record.generated_key)
            self.assertGreaterEqual(
                record.programme_regions,
                expected_regions[record.context.target_size],
                record.generated_key,
            )
            self.assertEqual(0, record.fixture_cells, record.generated_key)
            self.assertEqual(0, record.station_pairs, record.generated_key)

        xl_fingerprints = {}
        for record in records:
            if record.context.target_size == "XL":
                xl_fingerprints.setdefault(record.context.plan_key, set()).add(
                    record.overlay_fingerprint
                )
        self.assertEqual(
            set(xl_fingerprints),
            {"canvas-shelter", "timber-hut", "mud-hut", "ruin-block-hut"},
        )
        lineages = sorted(xl_fingerprints)
        for index, first in enumerate(lineages):
            for second in lineages[index + 1 :]:
                self.assertFalse(
                    xl_fingerprints[first] & xl_fingerprints[second],
                    f"{first} and {second} share an XL court silhouette",
                )

    def test_deepend_larger_lots_are_room_campuses_not_scaled_wall_masses(
        self,
    ) -> None:
        repository = GENERATOR_PATH.parents[1]
        result = GENERATOR.materialize(repository)
        generated = {
            item.get("Key"): item
            for item in ET.fromstring(result.text).findall("map")
        }
        sources = {}
        palettes = {}
        for _path, root in GENERATOR._source_roots(repository):
            sources.update({item.get("Key"): item for item in root.findall("map")})
            palettes.update(
                {item.get("Key"): item for item in root.findall("palette")}
            )
        records = [
            record
            for record in result.records
            if record.context.plan_key in GENERATOR.DEEPEND_COMPOSED_SITE_PROGRAMMES
        ]
        self.assertEqual(5, len(records))
        expected_bays = {"L": 3, "XL": 5}
        for record in records:
            source = sources[record.source_key]
            target = generated[record.generated_key]
            source_glyphs = GENERATOR._glyphs(source)
            target_glyphs = GENERATOR._glyphs(target)
            source_rows = [row.get("Cells", "") for row in source.findall("row")]
            target_rows = [row.get("Cells", "") for row in target.findall("row")]
            source_width = int(source.get("Width"))
            source_height = int(source.get("Height"))
            self.assertEqual(source_width, record.envelope_width)
            self.assertEqual(source_height, record.envelope_height)
            self.assertEqual(
                expected_bays[record.context.target_size],
                record.composition_bays,
                record.generated_key,
            )
            self.assertGreaterEqual(
                record.composition_thresholds,
                record.composition_bays,
                record.generated_key,
            )
            self.assertEqual(0, record.blind_wall_blocks, record.generated_key)
            for source_y, row in enumerate(source_rows):
                for source_x, char in enumerate(row):
                    if char == ".":
                        continue
                    self.assertEqual(
                        char,
                        target_rows[record.envelope_y + source_y][
                            record.envelope_x + source_x
                        ],
                        f"{record.generated_key} changed exact paid core {source_x},{source_y}",
                    )

            palette_references = {
                "$" + slot.get("Key", "")
                for slot in palettes[record.context.palette_key].findall("slot")
                if slot.get("Key")
            }
            for char, glyph in target_glyphs.items():
                if char in source_glyphs:
                    continue
                self.assertIsNone(glyph.get("Object"), record.generated_key)
                self.assertIsNone(glyph.get("Anchors"), record.generated_key)
                self.assertNotEqual("yes", glyph.get("Stateful"), record.generated_key)
                for attribute in ("Ground", "Structure"):
                    reference = glyph.get(attribute)
                    if reference:
                        self.assertIn(
                            reference,
                            palette_references,
                            f"{record.generated_key} invented {attribute}={reference}",
                        )

            yard_reference = GENERATOR._ground_reference(
                source,
                palettes[record.context.palette_key],
                want_path=False,
            )
            path_reference = GENERATOR._ground_reference(
                source,
                palettes[record.context.palette_key],
                want_path=True,
            )
            floor_reference = source_glyphs[
                min(
                    char
                    for char, glyph in source_glyphs.items()
                    if glyph.get("Claim") == "building"
                    and glyph.get("Pass") == "walk"
                    and not glyph.get("Structure")
                    and not glyph.get("Object")
                    and not glyph.get("Anchors")
                )
            ].get("Ground", "")
            service_reference = (
                yard_reference
                if yard_reference != floor_reference
                else path_reference
            )
            service_pads = {
                (x, y)
                for y, row in enumerate(target_rows)
                for x, char in enumerate(row)
                if char not in source_glyphs
                and char in target_glyphs
                and (glyph := target_glyphs[char]).get("Ground")
                == service_reference
                and glyph.get("Claim") == "building"
                and glyph.get("Pass") == "walk"
                and not glyph.get("Structure")
                and not glyph.get("Object")
                and not glyph.get("Anchors")
            }
            self.assertEqual(
                record.composition_bays - 1,
                len(service_pads),
                f"{record.generated_key} needs one readable service pad per annex",
            )

            building_cells = {
                (x, y)
                for y, row in enumerate(target_rows)
                for x, char in enumerate(row)
                if target_glyphs.get(char) is not None
                and target_glyphs[char].get("Claim") == "building"
            }
            components = []
            remaining = set(building_cells)
            while remaining:
                seed = min(remaining, key=lambda cell: (cell[1], cell[0]))
                component = {seed}
                remaining.remove(seed)
                queue = [seed]
                while queue:
                    x, y = queue.pop()
                    for neighbor in (
                        (x, y - 1),
                        (x + 1, y),
                        (x, y + 1),
                        (x - 1, y),
                    ):
                        if neighbor in remaining:
                            remaining.remove(neighbor)
                            component.add(neighbor)
                            queue.append(neighbor)
                components.append(component)
            self.assertEqual(record.composition_bays, len(components))
            source_fabric = sum(
                source_glyphs[char].get("Claim") == "building"
                for row in source_rows
                for char in row
                if char != "."
            )
            self.assertEqual(
                source_fabric,
                max(map(len, components)),
                f"{record.generated_key} inflated its paid core",
            )
            for component in components:
                walk = {
                    cell
                    for cell in component
                    if target_glyphs[target_rows[cell[1]][cell[0]]].get("Pass")
                    == "walk"
                }
                structure = {
                    cell
                    for cell in component
                    if target_glyphs[target_rows[cell[1]][cell[0]]].get("Structure")
                }
                self.assertGreaterEqual(len(walk), 2, record.generated_key)
                self.assertGreaterEqual(len(structure), 4, record.generated_key)
                self.assertTrue(
                    any(
                        (
                            0 <= cell[0] + dx < int(target.get("Width"))
                            and 0 <= cell[1] + dy < int(target.get("Height"))
                            and (cell[0] + dx, cell[1] + dy) not in component
                            and (
                                target_rows[cell[1] + dy][cell[0] + dx] == "."
                                or (
                                    outside := target_glyphs[
                                        target_rows[cell[1] + dy][cell[0] + dx]
                                    ]
                                ).get("Claim")
                                == "yard"
                                and outside.get("Pass") == "walk"
                            )
                        )
                        for cell in walk
                        for dx, dy in ((0, -1), (1, 0), (0, 1), (-1, 0))
                    ),
                    f"{record.generated_key} has a roofed bay without circulation",
                )
            canvas = [list(row) for row in target_rows]
            self.assertEqual(
                0,
                GENERATOR._solid_wall_block_count(canvas, target_glyphs),
                record.generated_key,
            )
            reachable = GENERATOR._strict_claimed_walk(
                canvas,
                target_glyphs,
                GENERATOR._public_entrances(target),
            )
            for y, row in enumerate(target_rows):
                for x, char in enumerate(row):
                    glyph = target_glyphs.get(char)
                    if glyph is None or glyph.get("Pass") != "adjacent":
                        continue
                    self.assertTrue(
                        any(
                            (x + dx, y + dy) in reachable
                            for dx, dy in ((0, -1), (1, 0), (0, 1), (-1, 0))
                        ),
                        f"{record.generated_key} strands adjacent-use cell {x},{y}",
                    )

    def test_every_generated_binding_preserves_the_complete_source_tier_set(
        self,
    ) -> None:
        repository = GENERATOR_PATH.parents[1]
        generated_root = ET.fromstring(GENERATOR.materialize(repository).text)
        generated_plans = {
            item.get("Key"): item for item in generated_root.findall("plan")
        }
        checked = 0
        authored = 0
        for _path, root in GENERATOR._source_roots(repository):
            for source_plan in root.findall("plan"):
                source_plan_key = source_plan.get("Key", "")
                authored_bindings = source_plan.findall("binding")
                for source_binding in source_plan.findall("binding"):
                    source_tiers = source_binding.findall("tier")
                    build_keys = {item.get("BuildKey", "") for item in source_tiers}
                    if not source_tiers or build_keys & GENERATOR.HEART_BUILD_KEYS:
                        continue
                    if GENERATOR._expansion_program(source_plan_key) is None:
                        continue
                    source_size = source_binding.get("Size", "")
                    source_set = {
                        (
                            item.get("Key"),
                            item.get("BuildKey"),
                            item.get("Level"),
                            item.get("Transition"),
                        )
                        for item in source_tiers
                    }
                    for target_size in GENERATOR.LOT_ORDER[
                        GENERATOR.LOT_ORDER.index(source_size) + 1 :
                    ]:
                        authored_targets = [
                            candidate
                            for candidate in authored_bindings
                            if candidate is not source_binding
                            and candidate.get("Size") == target_size
                            and candidate.get("Type") == source_binding.get("Type")
                            and candidate.get("Facing") == source_binding.get("Facing")
                            and build_keys
                            <= {
                                tier.get("BuildKey", "")
                                for tier in candidate.findall("tier")
                            }
                        ]
                        if authored_targets:
                            self.assertEqual(1, len(authored_targets))
                            authored += 1
                            continue
                        plan_key = (
                            f"lot-{target_size.lower()}-{source_plan_key}-"
                            f"{source_binding.get('Key', '')}"
                        )
                        generated_plan = generated_plans[plan_key]
                        binding = generated_plan.find("binding")
                        self.assertIsNotNone(binding, plan_key)
                        generated_set = {
                            (
                                item.get("Key"),
                                item.get("BuildKey"),
                                item.get("Level"),
                                item.get("Transition"),
                            )
                            for item in binding.findall("tier")
                        }
                        self.assertEqual(source_set, generated_set, plan_key)
                        checked += 1
        self.assertEqual(107, checked)
        self.assertEqual(0, authored)

    def test_shipped_agrarian_line_uses_real_cross_size_renovation(self) -> None:
        repository = GENERATOR_PATH.parents[1]
        architecture = ET.parse(
            repository / "Architecture" / "KingdomArchitectures-Production.xml"
        ).getroot()
        buildings = ET.parse(
            repository / "RuntimeData" / "KingdomBuildings.xml"
        ).getroot()
        fieldrows = next(
            item for item in buildings.findall("building")
            if item.get("Key") == "fieldrows"
        )
        self.assertEqual("grange", fieldrows.get("UpgradesTo"))
        plan = next(
            item for item in architecture.findall("plan")
            if item.get("Key") == "production-field"
        )
        bindings = {item.get("Size"): item for item in plan.findall("binding")}
        self.assertEqual({"M", "L", "XL"}, set(bindings))
        medium = bindings["M"]
        large = bindings["L"]
        predecessor = next(
            item for item in medium.findall("tier")
            if item.get("BuildKey") == "fieldrows"
        )
        successor = next(
            item for item in large.findall("tier")
            if item.get("BuildKey") == "grange"
        )
        self.assertEqual("1", predecessor.get("Level"))
        self.assertEqual("2", successor.get("Level"))
        self.assertEqual("renovate-expand", successor.get("Transition"))
        self.assertFalse(
            any(item.get("BuildKey") == "grange" for item in medium.findall("tier"))
        )
        generated = ET.fromstring(GENERATOR.materialize(repository).text)
        generated_plan_keys = {item.get("Key") for item in generated.findall("plan")}
        self.assertNotIn(
            "lot-l-production-field-food-m-field", generated_plan_keys
        )
        self.assertNotIn(
            "lot-xl-production-field-food-m-field", generated_plan_keys
        )


if __name__ == "__main__":
    unittest.main()
