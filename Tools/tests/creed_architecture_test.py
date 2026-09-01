"""Golden and contract guards for authored creed S-lot architecture."""

from __future__ import annotations

import importlib.util
import sys
import unittest
import xml.etree.ElementTree as ET
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
SOURCE = ROOT / "Architecture" / "KingdomArchitectures-Creeds.xml"
CHECKER_SOURCE = ROOT / "Tools" / "check-architecture.py"
CREED_LOCATION = "Architecture/KingdomArchitectures-Creeds.xml"

# These are design goldens, not generated fixtures. Pinning all 30 plans and 31 maps prevents a later
# dimension repair from quietly becoming a copied floor/wall column with no creed-specific use.
EXPECTED_ROWS = {
    "creed-joppa-seedhouse-s0": ("######", "#ssts#", "#i@ii#", "##+###"),
    "creed-kyakukya-hearth-s0": ("######", "#j0xh#", "#i@ii#", "##+###"),
    "creed-ezra-wheelshade-s0": ("xiiiix", "iw@rri", "xpirix", "ii++ii"),
    "creed-snapjaw-trailden-s0": ("ixefxi", "ep@ppe", "xibiix", "ii++ii"),
    "creed-cragmensch-garden-s0": ("0isiis", "iiiiii", "si@iis", "ii++ii"),
    "creed-robot-chargebay-s0": ("i####i", "i0@cci", "iririi", "ii++ii"),
    "creed-robot-servicebay-s1": ("wwwwww", "wc@ccw", "wrisiL", "wwddww"),
    "creed-baetyl-frame-s0": ("g0iiag", "ioiiai", "gi@iig", "ii++ii"),
    "creed-dromad-shade-s0": ("exiixe", "ri@iri", "xiimix", "ii++ii"),
    "creed-entropy-blind-s0": ("#b####", "#w@ib#", "#biif#", "##+###"),
    "creed-goatfolk-moot-s0": ("i0hihi", "hiiiih", "ih@ihi", "ii++ii"),
    "creed-svardym-nursery-s0": ("0biibb", "if@ifi", "ibbbbi", "ii++ii"),
    "creed-naphtaali-altar-s0": ("######", "#nnv##", "#a@ii#", "##+###"),
    "creed-troll-bridgecourt-s0": ("iibbii", "bitibi", "bb@bbi", "ii++ii"),
    "creed-issachari-porch-s0": (".siis.", "siriis", "si@fis", "ii++ii"),
    "creed-strangers-screen-s0": ("######", "#b##b#", "#0@ii#", "##+###"),
    "creed-hindren-mooncourt-s0": ("0ieiim", "imiimi", "ii@iii", "ii+iii"),
    "creed-mopango-kitchen-s0": ("######", "#0##h#", "#s@ii#", "##++##"),
    "creed-girsh-chapel-s0": ("#0##d#", "diivid", "di@iid", "##+###"),
    "creed-templar-arsenal-s0": ("######", "#0rir#", "#II@I#", "###+##"),
    "creed-gyre-ashcourt-s0": ("0ooiib", "iiibie", "bi@ioo", "i+iiii"),
    "creed-mamon-cistern-s0": ("######", "#ibii#", "#w@wi#", "##+###"),
    "creed-seekers-cell-s0": ("##0b##", "#b@ib#", "#siii#", "##+###"),
    "creed-wardens-lodge-s0": ("######", "#b@iw#", "#fiiw.", "##++##"),
    "creed-water-gaugehouse-s0": ("######", "#1##g#", "#0@ri#", "##+###"),
    "creed-merchants-weighing-s0": ("######", "#0ips#", "#i@ii#", "#+##+#"),
    "creed-farmers-commons-s0": ("biibib", "it@tti", "biiibb", "ii++ii"),
    "creed-resheph-hospice-s0": ("######", "#piip#", "#0@ib#", "i+ii+i"),
    "creed-daughters-repair-s0": ("###+##", "#0rrb#", "#i@bi#", "##+###"),
    "creed-yd-bower-s0": ("VvvvvV", "viitiv", "Vi@iiV", "i+ii+i"),
    "creed-chavvah-school-s0": ("BBBBBB", "BtiitB", "B0@idB", "BB++BB"),
}


def _load_checker():
    spec = importlib.util.spec_from_file_location("creed_architecture_checker", CHECKER_SOURCE)
    if spec is None or spec.loader is None:
        raise RuntimeError(f"cannot load {CHECKER_SOURCE}")
    module = importlib.util.module_from_spec(spec)
    sys.modules[spec.name] = module
    spec.loader.exec_module(module)
    return module


class CreedArchitectureTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls) -> None:
        cls.root = ET.parse(SOURCE).getroot()
        cls.maps = {item.get("Key", ""): item for item in cls.root.findall("map")}
        cls.checker_result = _load_checker().run_check(ROOT)

    def test_every_creed_has_its_exact_authored_s_lot(self) -> None:
        self.assertEqual(set(self.maps), set(EXPECTED_ROWS))
        self.assertEqual(len(self.maps), 31)
        for key, expected in EXPECTED_ROWS.items():
            architecture_map = self.maps[key]
            rows = tuple(row.get("Cells", "") for row in architecture_map.findall("row"))
            self.assertEqual(architecture_map.get("Width"), "6", key)
            self.assertEqual(architecture_map.get("Height"), "4", key)
            self.assertEqual(rows, expected, key)
            self.assertTrue(all(any(row[x] != "." for row in rows) for x in range(6)), key)
        self.assertEqual(len({rows for rows in EXPECTED_ROWS.values()}), 31)

    def test_public_ingress_is_on_a_non_corner_lot_edge(self) -> None:
        for key, architecture_map in self.maps.items():
            glyphs = {glyph.get("Char", ""): glyph for glyph in architecture_map.findall("glyph")}
            rows = tuple(row.get("Cells", "") for row in architecture_map.findall("row"))
            entrances = []
            for y, row in enumerate(rows):
                for x, char in enumerate(row):
                    glyph = glyphs.get(char)
                    anchors = (
                        () if glyph is None else glyph.get("Anchors", "").split(",")
                    )
                    if "entrance:public" in anchors:
                        entrances.append((x, y, glyph))
            self.assertTrue(entrances, key)
            for x, y, glyph in entrances:
                self.assertTrue(x in {0, 5} or y in {0, 3}, (key, x, y))
                self.assertFalse(x in {0, 5} and y in {0, 3}, (key, x, y))
                self.assertEqual(glyph.get("Pass"), "walk", (key, x, y))
                self.assertIn(glyph.get("Claim"), {"building", "yard"}, (key, x, y))

    def test_bindings_remain_exact_authored_road_tiers(self) -> None:
        plans = self.root.findall("plan")
        self.assertEqual(len(plans), 30)
        mapped = []
        for plan in plans:
            bindings = plan.findall("binding")
            self.assertEqual(len(bindings), 1, plan.get("Key"))
            binding = bindings[0]
            self.assertEqual(binding.get("Size"), "S", plan.get("Key"))
            self.assertEqual(binding.get("Facing"), "road", plan.get("Key"))
            tiers = binding.findall("tier")
            if plan.get("Key") == "creed-robots":
                expected_tiers = [
                    ("0", None, "creed-robot-chargebay-s0"),
                    ("1", "renovate", "creed-robot-servicebay-s1"),
                ]
            else:
                expected_tiers = [("0", None, tiers[0].get("Map"))]
            self.assertEqual(len(tiers), len(expected_tiers), plan.get("Key"))
            for tier, expected in zip(tiers, expected_tiers, strict=True):
                level, transition, architecture_map = expected
                self.assertEqual(tier.get("Level"), level, plan.get("Key"))
                self.assertEqual(tier.get("Transition"), transition, plan.get("Key"))
                self.assertEqual(tier.get("Map"), architecture_map, plan.get("Key"))
                variants = tier.findall("variant")
                variant_keys = [
                    (item.get("Key"), item.get("Priority")) for item in variants
                ]
                self.assertEqual(variant_keys, [("fallback", "0")])
                mapped.append(tier.get("Map"))
        self.assertEqual(set(mapped), set(EXPECTED_ROWS))
        self.assertEqual(len(mapped), len(set(mapped)))

    def test_full_checker_reports_no_creed_contract_fault(self) -> None:
        creed_issues = [
            issue
            for issue in self.checker_result.issues
            if issue.location.startswith(CREED_LOCATION)
        ]
        self.assertEqual(creed_issues, [])


if __name__ == "__main__":
    unittest.main()
