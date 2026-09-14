"""Room counterexamples and read-only authoring service contracts."""

import hashlib
import importlib.util
import json
import re
import sys
import threading
import unittest
import urllib.error
import urllib.request
import xml.etree.ElementTree as ET
from pathlib import Path
from types import SimpleNamespace

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(ROOT / "Tools"))
from layout_rooms import analyse
from layout_studio_data import Studio, load_checker
from layout_studio_view import svg

CHECKER = load_checker(ROOT)
GLYPHS = '''
<glyph Char="#" Structure="$wall" Claim="building" Pass="blocked" Cover="soft" />
<glyph Char="d" Structure="$door" Claim="building" Pass="walk" Cover="soft" Anchors="entrance:public" />
<glyph Char="i" Ground="$floor" Claim="building" Pass="walk" Cover="soft" />
<glyph Char="b" Object="$bed" Claim="building" Pass="walk" Cover="soft" Anchors="fixture:sleep" Stateful="yes" />
<glyph Char="s" Object="$store" Claim="building" Pass="walk" Cover="soft" Anchors="fixture:storage" Stateful="yes" />
<glyph Char="x" Object="$crate" Claim="building" Pass="blocked" Cover="soft" />
<glyph Char="a" Object="$crate" Claim="building" Pass="adjacent" Cover="soft" Anchors="work:bench" Stateful="yes" />
<glyph Char="@" Object="$building" Claim="building" Pass="walk" Cover="soft" Anchors="main,function:dwelling" Stateful="yes" />
<glyph Char="p" Ground="$floor" Claim="yard" Pass="walk" Cover="open" />
<glyph Char="e" Ground="$floor" Claim="building" Pass="walk" Cover="soft" Anchors="door:label" />
'''


def map_xml(rows, glyphs=GLYPHS):
    return f'<map Key="test-home" Width="{len(rows[0])}" Height="{len(rows)}" DefaultCover="soft">' + glyphs + ''.join(
        f'<row Cells="{row}" />' for row in rows) + '</map>'


def read(rows, shapes=None, pose="north"):
    issues = []
    amap = CHECKER._parse_map(ET.fromstring(map_xml(rows)), ROOT / "draft.xml", ROOT, 0, issues)
    palette = SimpleNamespace(slots={name: SimpleNamespace(blueprint=name, role=role)
        for name, role in [('wall', 'wall'), ('door', 'door'), ('floor', 'floor'),
                           ('bed', 'sleep'), ('store', 'storage'), ('crate', 'work')]})
    defaults = {name: CHECKER.BlueprintShape(name in {'wall', 'crate'}, name == 'door')
                for name in ['wall', 'door', 'floor', 'bed', 'store', 'crate', 'root']}
    defaults.update(shapes or {})
    result = analyse(amap, palette, SimpleNamespace(blueprint="root"), defaults, CHECKER, pose)
    result.update(width=amap.width, height=amap.height, pose=pose)
    return result


class RoomTopologyTests(unittest.TestCase):
    home = ['#######', '#biiis#', '#ii@ii#', '#iiiii#', '###d###']

    def test_closed_room_has_interior_not_wall_or_fixture_area(self):
        r = read(self.home)
        self.assertEqual((1, 1, 1), (r['rooms'], r['beds'], r['enclosed_beds']))
        room = r['spaces'][0]
        self.assertEqual((15, 12, 3), (room['area'], room['free_floor'], room['furnished_cells']))
        self.assertEqual([], r['inaccessible_fixtures'])

    def test_partitions_create_two_rooms_and_removing_door_merges_them(self):
        rows = ['#########', '#bii#iib#', '#iiidiii#', '#iii#iii#', '##d######']
        split = read(rows)
        self.assertEqual((2, 2), (split['rooms'], split['enclosed_beds']))
        rows[2] = '#iiiiiii#'
        merged = read(rows)
        self.assertEqual((1, 2), (merged['rooms'], merged['enclosed_beds']))

    def test_broken_outer_wall_and_labelled_opening_expose_bed(self):
        for replacement in ['i', 'e', 'x']:
            with self.subTest(replacement=replacement):
                rows = list(self.home)
                rows[2] = replacement + rows[2][1:]
                r = read(rows)
                self.assertEqual((0, 0), (r['rooms'], r['enclosed_beds']))
                self.assertEqual([[1, 1]], r['exposed_beds'])

    def test_unknown_physical_walls_do_not_prove_enclosure(self):
        r = read(self.home, {'wall': CHECKER.BlueprintShape(None, None)})
        self.assertEqual(0, r['rooms'])
        self.assertIn('wall', r['unknown_blueprints'])

    def test_blocked_door_approach_leaves_fixture_unusable(self):
        rows = ['#######', '#biiis#', '#ii@ii#', '#xxxxx#', '###d###']
        r = read(rows)
        self.assertEqual(1, r['rooms'])
        self.assertIn([1, 1], [item['cell'] for item in r['inaccessible_fixtures']])

    def test_adjacent_workstation_uses_its_free_neighbour(self):
        rows = list(self.home)
        rows[1] = '#biias#'
        r = read(rows)
        self.assertEqual([], r['inaccessible_fixtures'])
        self.assertTrue(next(c for c in r['cells'] if c['char'] == 'a')['accessible'])

    def test_walkable_furniture_blocks_hallway_without_creating_rooms(self):
        for furniture in ['b', 's']:
            rows = ['#######', '#biiis#', '#iiiii#', '#' + furniture * 5 + '#', '###d###']
            with self.subTest(furniture=furniture):
                r = read(rows)
                self.assertEqual(1, r['rooms'])
                self.assertEqual(0, r['spaces'][0]['publicly_reachable_cells'])
                self.assertIn([1, 1], [item['cell'] for item in r['inaccessible_fixtures']])
                rows[3] = rows[3][:3] + 'i' + rows[3][4:]
                repaired = read(rows)
                self.assertEqual([], repaired['inaccessible_fixtures'])

    def test_furniture_cannot_supply_its_own_access_or_diagonal_access(self):
        r = read(['#######', '#bsiii#', '#siiii#', '#iiiii#', '###d###'])
        self.assertIn([1, 1], [item['cell'] for item in r['inaccessible_fixtures']])
        self.assertFalse(next(c for c in r['cells'] if c['char'] == 'b')['reached'])

    def test_furniture_on_a_real_door_blocks_that_entrance(self):
        glyphs = GLYPHS.replace('Char="d" Structure="$door"', 'Char="d" Object="$store" Structure="$door"')
        issues = []
        amap = CHECKER._parse_map(ET.fromstring(map_xml(self.home, glyphs)), ROOT / 'draft.xml', ROOT, 0, issues)
        palette = SimpleNamespace(slots={name: SimpleNamespace(blueprint=name, role=name)
            for name in ['wall', 'door', 'floor', 'bed', 'store']})
        shapes = {name: CHECKER.BlueprintShape(name == 'wall', name == 'door')
            for name in ['wall', 'door', 'floor', 'bed', 'store', 'root']}
        r = analyse(amap, palette, SimpleNamespace(blueprint='root'), shapes, CHECKER)
        self.assertEqual(1, r['rooms'])
        self.assertEqual(0, r['spaces'][0]['publicly_reachable_cells'])

    def test_bigger_yard_does_not_increase_room_space(self):
        small = read(self.home)
        padded = read(['p' * 11] * 2 + ['pp' + row + 'pp' for row in self.home] + ['p' * 11] * 2)
        a = next(s for s in small['spaces'] if s['kind'] == 'room')
        b = next(s for s in padded['spaces'] if s['kind'] == 'room')
        self.assertEqual((a['area'], a['free_floor']), (b['area'], b['free_floor']))

    def test_extra_furniture_reduces_free_space_and_removing_bed_removes_capacity(self):
        more = list(self.home)
        more[3] = '#bssii#'
        r = read(more)
        self.assertEqual((2, 9, 4.5), (r['beds'], r['spaces'][0]['free_floor'], r['spaces'][0]['free_floor_per_bed']))
        no_bed = read([row.replace('b', 'i') for row in self.home])
        self.assertEqual((1, 0), (no_bed['rooms'], no_bed['beds']))

    def test_rotation_preserves_geometry_and_source_coordinates(self):
        for pose in CHECKER.POSES:
            r = read(self.home, pose=pose)
            self.assertEqual((1, 1), (r['rooms'], r['enclosed_beds']))
            drawing = ET.fromstring(svg(r))
            self.assertEqual(35, len(drawing.findall('{http://www.w3.org/2000/svg}g')))
            self.assertIn('data-x="1" data-y="1"', svg(r))


class StudioContractTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.studio = Studio(ROOT)
        cls.case = next(c['id'] for c in cls.studio.cases if c['map'] == 'housing-tentrow-s1')

    def test_actual_open_tentrow_is_not_an_enclosed_room(self):
        r = self.studio.review(self.case)
        self.assertEqual((0, 3, 0), (r['rooms'], r['beds'], r['enclosed_beds']))
        self.assertFalse(r['base_supplied'])
        self.assertTrue(r['unknown_blueprints'])

    def test_source_map_export_preserves_attributes_and_rows(self):
        before = self.studio.source_xml(self.case)
        review = self.studio.review(self.case)
        after = ET.fromstring(review['xml'])
        source = ET.fromstring(before)
        self.assertEqual(ET.tostring(source), ET.tostring(after))
        self.assertEqual(hashlib.sha256(before.encode()).hexdigest(), review['source_sha256'])
        self.assertEqual(before, self.studio.source_xml(self.case))

    def test_larger_room_cannot_silently_fit_the_old_small_binding(self):
        xml = (ROOT / 'Tools/layout-examples/enclosed-canvas-room.xml').read_text()
        r = self.studio.review(self.case, xml)
        self.assertEqual(('S', 'M', False), (r['selected_lot'], r['minimum_lot'], r['fits_selected_lot']))
        self.assertTrue(any('draft.envelope:' in issue for issue in r['issues']))

    def test_room_under_construction_is_reviewable_but_keeps_topology_failures(self):
        xml = map_xml(['#####', '#iii#', '#iii#', '#####'])
        review = self.studio.review(self.case, xml)
        self.assertTrue(review['issues'])
        issues = []
        CHECKER._parse_map(ET.fromstring(xml), ROOT / 'draft.xml', ROOT, 0, issues)
        self.assertTrue(issues, 'the ordinary checker must still reject the incomplete map')

    def test_invalid_drafts_and_invalid_case_are_refused(self):
        bad = ['<!DOCTYPE map><map/>', '<map>', '<objects/>', 'x' * 65537,
               map_xml(['iii', 'i']), map_xml(['q']), map_xml(['i' * 21]),
               map_xml(['i'], GLYPHS + '<glyph Char="i" Ground="DirtPath" Claim="building" Pass="walk" Cover="soft"/>')]
        for xml in bad:
            with self.subTest(xml=xml[:50]), self.assertRaises(ValueError):
                self.studio.review(self.case, xml)
        for index in [-1, True, '1', len(self.studio.cases)]:
            with self.assertRaises(ValueError):
                self.studio.review(index)

    def test_editor_service_requires_session_and_never_writes_source(self):
        spec = importlib.util.spec_from_file_location('taf_studio_server', ROOT / 'Tools/layout-studio.py')
        module = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(module)
        server = module.make_server(self.studio)
        thread = threading.Thread(target=server.serve_forever, daemon=True)
        thread.start()
        base = f'http://127.0.0.1:{server.server_port}'
        before = self.studio.source_xml(self.case)
        source_path = ROOT / self.studio.case(self.case)['source']
        disk_before = hashlib.sha256(source_path.read_bytes()).hexdigest()
        try:
            page = urllib.request.urlopen(base + '/', timeout=5).read().decode()
            token = re.search(r'[\'"]X-Layout-Session[\'"]:\s*[\'"]([^\'"]+)', page).group(1)
            body = json.dumps({'case': self.case, 'xml': before, 'pose': 'east'}).encode()
            request = urllib.request.Request(base + '/api/review', body, {'X-Layout-Session': token})
            result = json.load(urllib.request.urlopen(request, timeout=5))
            self.assertEqual((3, 0), (result['beds'], result['rooms']))
            ET.fromstring(result['svg'])
            with self.assertRaises(urllib.error.HTTPError) as refused:
                urllib.request.urlopen(urllib.request.Request(base + '/api/review', body), timeout=5)
            self.assertEqual(403, refused.exception.code)
            self.assertEqual(before, self.studio.source_xml(self.case))
            self.assertEqual(disk_before, hashlib.sha256(source_path.read_bytes()).hexdigest())
        finally:
            server.shutdown()
            server.server_close()
            thread.join(5)


if __name__ == '__main__':
    unittest.main()
