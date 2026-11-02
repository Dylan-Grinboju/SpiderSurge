"""Render consistent map-card silhouettes from the game's exported collider outlines.

Run after shapes.py: python thumbnails.py <folder containing shapes.json>
Requires Pillow. These are geometry previews, not gameplay screenshots.
"""

import argparse
import json
import math
from pathlib import Path

from PIL import Image, ImageDraw, ImageFilter

import layouts


PALETTES = [
    ((7, 7, 17), (24, 13, 23)),      # Space
    ((23, 20, 9), (43, 37, 22)),     # Cave
    ((27, 10, 13), (52, 18, 21)),    # Desert
    ((24, 14, 26), (43, 26, 41)),    # Desolation
    ((10, 25, 29), (19, 43, 46)),    # Frosty
    ((24, 12, 33), (49, 26, 61)),    # Mountain
    ((16, 6, 29), (32, 13, 48)),     # Multiverse
]
SIZE = (1280, 720)


def point(x, y):
    return ((x + 480) * SIZE[0] / 960, (318 - y) * SIZE[1] / 540)


def render(arena, shapes):
    image = Image.new("RGB", SIZE)
    draw = ImageDraw.Draw(image)
    top, bottom = PALETTES[arena["background"]]
    for y in range(SIZE[1]):
        t = y / (SIZE[1] - 1)
        lava = max(0, (t - 0.90) / 0.10) ** 2
        color = tuple(round(a * (1 - t) + b * t + c * lava)
                      for a, b, c in zip(top, bottom, (24, 10, 0)))
        draw.line((0, y, SIZE[0], y), fill=color)

    glow = Image.new("RGBA", SIZE)
    lights = ImageDraw.Draw(glow)
    polygons = layouts.solids(arena, shapes)
    for name, points in polygons:
        if name.startswith("Death") or name in ("HoneyComb", "HoneycombSector", layouts.HOVER):
            color = (255, 120, 22, 140) if name.startswith("Death") else (55, 185, 210, 90) if name != layouts.HOVER else (40, 110, 255, 120)
            lights.line([point(*p) for p in points + points[:1]], fill=color, width=8)
    for x, y in portals(arena):
        cx, cy = point(x, y)
        lights.ellipse((cx - 22, cy - 22, cx + 22, cy + 22), outline=(255, 223, 0, 170), width=10)
    image = Image.alpha_composite(image.convert("RGBA"), glow.filter(ImageFilter.GaussianBlur(8)))
    draw = ImageDraw.Draw(image)
    for name, points in polygons:
        outline = [point(*p) for p in points]
        hazard = name.startswith("Death")
        ice = name in ("HoneyComb", "HoneycombSector")
        fill = (5, 6, 10, 255)
        edge = (185, 179, 187, 255)
        if hazard:
            fill, edge = (55, 17, 10, 255), (255, 164, 41, 255)
        elif ice:
            fill, edge = (13, 49, 59, 255), (75, 173, 195, 255)
        elif name == layouts.HOVER:
            edge = (90, 150, 255, 255)
        draw.polygon(outline, fill=fill)
        draw.line(outline + outline[:1], fill=edge, width=2, joint="curve")

    # Interactive objects have trigger-only colliders and need their own visible cue.
    for e in arena["elements"]:
        if e["o"] not in ("Jump Pad", "AeroTube"):
            continue
        angle = math.radians(e["r"])
        c, s = math.cos(angle), math.sin(angle)

        def local(x, y):
            x, y = x * e["sx"], y * e["sy"]
            return point(e["x"] + x * c - y * s, e["y"] + x * s + y * c)

        if e["o"] == "Jump Pad":
            draw.line([local(-10, 0), local(10, 0)], fill=(119, 235, 255), width=5)
            draw.line([local(-4, 5), local(0, 10), local(4, 5)], fill=(119, 235, 255), width=3)
        else:
            draw.line([local(-10, 0), local(10, 0)], fill=(92, 187, 213), width=4)
            for y in (10, 25, 40):
                draw.line([local(-7, y), local(0, y + 7), local(7, y)], fill=(53, 107, 128), width=2)

    for x, y in portals(arena):
        cx, cy = point(x, y)
        draw.ellipse((cx - 14, cy - 14, cx + 14, cy + 14), outline=(255, 223, 0), width=4)

    return image.convert("RGB").resize((320, 180), Image.Resampling.LANCZOS)


def portals(arena):
    """Both ends of every portal pair."""
    return [(p["x"], p["y"]) for e in arena["elements"] if e["o"] == "PortalPoint"
            for p in [e] + e.get("with", [])]


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("shapes", type=Path)
    parser.add_argument("--output", type=Path, default=Path(__file__).resolve().parent.parent / "Thumbnails")
    args = parser.parse_args()
    shapes = json.loads((args.shapes / "shapes.json").read_text(encoding="utf-8"))
    if layouts.check(layouts.MAPS, shapes):
        raise SystemExit("Fix placement errors before rendering thumbnails")
    args.output.mkdir(parents=True, exist_ok=True)
    for arena in layouts.MAPS:
        render(arena, shapes).save(args.output / ("map_" + arena["id"] + ".png"))
    print(f"Rendered {len(layouts.MAPS)} geometry thumbnails to {args.output}")
