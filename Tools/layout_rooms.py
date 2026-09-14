"""Physical room and activity-space readings for the architecture workbench.

These are design-time facts, not a beauty score or a simulation of a resident. Door
parts separate rooms while remaining potentially traversable; native use must still
prove opening, locks, body access and terrain outside the draft.
"""

from collections import Counter, deque


def neighbours(point, width, height):
    x, y = point
    return [(a, b) for a, b in ((x - 1, y), (x + 1, y), (x, y - 1), (x, y + 1))
            if 0 <= a < width and 0 <= b < height]


def flood(starts, allowed, width, height):
    found = set(starts) & allowed
    queue = deque(sorted(found))
    while queue:
        for point in neighbours(queue.popleft(), width, height):
            if point in allowed and point not in found:
                found.add(point)
                queue.append(point)
    return found


def analyse(amap, palette, building, shapes, checker, pose="north", poses=None):
    if pose not in checker.POSES:
        raise ValueError("unsupported pose")
    width, height = amap.width, amap.height
    points = {(x, y) for y in range(height) for x in range(width)}
    walls, doors, blocked, unknown = set(), set(), set(), set()
    objects, beds, entrances, interior = set(), set(), set(), set()
    roles, facts = {}, {}
    sleeps = {(x, y) for x, y, _ in checker._sleep_provider_cells(amap, palette)}
    for point in sorted(points):
        x, y = point
        glyph = amap.glyph_at(x, y)
        facts[point] = {"x": x, "y": y, "char": amap.rows[y][x], "layers": [], "roles": []}
        if glyph is None:
            continue
        fact = facts[point]
        fact.update(claim=glyph.claim, cover=glyph.cover or amap.default_cover)
        roles[point] = list(glyph.anchors)
        if glyph.object:
            objects.add(point)
        if point in sleeps:
            beds.add(point)
        if glyph.claim == "building" and fact["cover"] != "open":
            interior.add(point)
        if any(anchor.startswith("entrance:public") for anchor in glyph.anchors):
            entrances.add(point)
        if glyph.pass_mode != "walk":
            blocked.add(point)
        for layer, reference in glyph.layers():
            blueprint = checker._resolve_layer_pose(reference, layer, glyph, palette,
                                                    building, poses or {}, pose)
            shape = shapes.get(blueprint)
            fact["layers"].append(f"{layer}: {blueprint or reference}")
            if reference.startswith("$") and reference != "$building":
                slot = palette.slots.get(reference[1:])
                if slot:
                    roles[point].append(slot.role)
            if shape is None or shape.solid is None or shape.door is None:
                unknown.add(blueprint or reference)
                if layer != "Ground":
                    fact["unproved"] = True
                continue
            if shape.solid and not shape.door:
                blocked.add(point)
            if shape.door:
                doors.add(point)
                if layer == "Object":
                    objects.discard(point)
            elif layer == "Structure" and shape.solid:
                walls.add(point)
        fact["roles"] = roles[point]

    # Reserve every authored object footprint, including natively walkable furniture.
    # Furniture obstructs circulation but never supplies a structural room boundary.
    blocked |= objects
    boundary = walls | doors
    remaining = points - boundary
    spaces = []
    room_cells = set()
    exposed = set()
    while remaining:
        seed = min(remaining, key=lambda point: (point[1], point[0]))
        region = flood([seed], remaining, width, height)
        remaining -= region
        outside = any(x in (0, width - 1) or y in (0, height - 1) for x, y in region)
        uses_interior = bool(region & interior)
        all_interior = region <= interior
        if outside:
            exposed |= region & interior
            kind = "exterior"
        elif all_interior:
            kind = "room"
            room_cells |= region
        elif uses_interior:
            kind = "mixed open/interior"
            exposed |= region & interior
        else:
            kind = "courtyard"
        free = region - objects - blocked
        space_beds = region & beds
        space_roles = Counter(role for p in region for role in roles.get(p, []))
        spaces.append({"id": len(spaces) + 1, "kind": kind,
                       "cells": [list(p) for p in sorted(region)], "area": len(region),
                       "free_floor": len(free), "furnished_cells": len(region & objects),
                       "beds": len(space_beds), "roles": dict(sorted(space_roles.items())),
                       "free_floor_per_bed": round(len(free) / len(space_beds), 2) if space_beds else None})
    walk = points - blocked
    reached = flood(entrances, walk, width, height)
    inaccessible = []
    for point, point_roles in sorted(roles.items()):
        glyph = amap.glyph_at(*point)
        accessible = point in reached
        if point in objects or glyph.pass_mode == "adjacent":
            accessible = any(p in reached for p in neighbours(point, width, height))
        facts[point]["accessible"] = accessible
        if not accessible and (point in objects or glyph.anchors):
            inaccessible.append({"cell": list(point), "roles": point_roles})
    exposed_beds = beds - room_cells
    for point, fact in facts.items():
        fact.update(wall=point in walls, door=point in doors, blocked=point in blocked,
                    reached=point in reached, exposed=point in exposed,
                    bed=point in beds, fixture=point in objects)
    for space in spaces:
        space["publicly_reachable_cells"] = len({tuple(p) for p in space["cells"]} & reached)
    return {"rooms": sum(space["kind"] == "room" for space in spaces),
            "spaces": spaces, "beds": len(beds), "enclosed_beds": len(beds & room_cells),
            "exposed_beds": [list(p) for p in sorted(exposed_beds)],
            "exposed_interior": [list(p) for p in sorted(exposed)],
            "doors": len(doors), "unknown_blueprints": sorted(unknown),
            "blocked_doorways": [list(p) for p in sorted(doors & objects)],
            "inaccessible_fixtures": inaccessible, "cells": list(facts.values()),
            "assumptions": ["Physical blueprint shape, not a live zone survey.",
                            "All object footprints are occupied; fixtures need reachable adjacent clear floor.",
                            "Doors assumed openable for circulation; locks and body access need native tests.",
                            "Unclaimed draft cells assumed clear; no external terrain or road-width proof.",
                            "Room size and furniture readings do not award gameplay quality or privacy."]}
