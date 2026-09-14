"""SVG design drawings, with no extracted or bundled game art."""

from html import escape


def svg(reading):
    width, height, pose = reading["width"], reading["height"], reading["pose"]
    across, down = (height, width) if pose in ("east", "west") else (width, height)
    size = 36
    items = [f'<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 {across * size} {down * size}" '
             'role="img" aria-label="Layout plan; coordinates use the unrotated source">']
    for cell in reading["cells"]:
        x, y = cell["x"], cell["y"]
        if pose == "east":
            x, y = height - 1 - y, x
        elif pose == "south":
            x, y = width - 1 - x, height - 1 - y
        elif pose == "west":
            x, y = y, width - 1 - x
        color = "#152b32" if cell.get("claim") == "building" else "#20392f"
        if cell["char"] == ".":
            color = "#151c21"
        if cell["exposed"]:
            color = "#773e3a"
        if cell["wall"]:
            color = "#a1acaa"
        elif cell["door"]:
            color = "#d7ad64"
        label = [f'{cell["x"]},{cell["y"]}: {cell["char"]}', *cell["layers"], *cell["roles"]]
        if cell.get("unproved"):
            label.append("Physical shape unproved")
        outline = "#f27b68" if cell["fixture"] and not cell.get("accessible") else "#38505a"
        items.append(f'<g data-x="{cell["x"]}" data-y="{cell["y"]}"><title>{escape(chr(10).join(label))}</title>')
        items.append(f'<rect x="{x * size}" y="{y * size}" width="{size}" height="{size}" '
                     f'fill="{color}" stroke="{outline}" stroke-width="1.5"/>')
        text_color = "#10212a" if cell["wall"] or cell["door"] else "#f4ead5"
        items.append(f'<text x="{x * size + size / 2}" y="{y * size + 24}" text-anchor="middle" '
                     f'fill="{text_color}" font-family="monospace" font-size="18" pointer-events="none">'
                     f'{escape(cell["char"])}</text></g>')
    return "".join(items) + "</svg>"
