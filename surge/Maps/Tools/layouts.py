"""Source of the Surge map layouts: writes surge/Maps/Layouts/<id>.json, and with --preview draws each map.

Coordinates are world units in the game's custom map scene (MapEditorBase). The playable box is about
x -500..500, y -222..381; the bottom lava starts at y -222. Element names are the map editor's prefab names.
Run: py surge/Maps/Tools/layouts.py [--shapes <folder from shapes.py> [--preview]] [--only id,id]
"""

import argparse
import json
import math
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
LAYOUTS = os.path.join(os.path.dirname(HERE), "Layouts")

# Backgrounds and lava sets of the custom map scene
SPACE, CAVE, DESERT, DESOLATION, FROSTY, MOUNTAIN, MULTIVERSE = range(7)
LAVA_BOTTOM, LAVA_TOP_BOTTOM, LAVA_SIDES_BOTTOM, LAVA_ALL = range(4)

MAPS = []


def el(o, x, y, r=0.0, sx=1.0, sy=None, with_=None):
    e = {"o": o, "x": round(x, 2), "y": round(y, 2), "r": round(r, 2), "sx": round(sx, 3), "sy": round(sx if sy is None else sy, 3)}
    if with_:
        e["with"] = with_
    return e


def mirror(elements):
    """The elements plus their mirror image across x = 0 (rotation flipped)."""
    out = list(elements)
    for e in elements:
        if abs(e["x"]) < 0.01:
            continue
        m = dict(e)
        m["x"] = -e["x"]
        m["r"] = -e["r"]
        out.append(m)
    return out


def mirror_points(points):
    out = list(points)
    out += [(-x, y) for x, y in points if abs(x) > 0.01]
    return out


def surge_map(id, name, background, lava, elements, players, enemies, weapons, rare=(), beginner=False):
    assert len(players) == 4, f"{id}: needs 4 player spawns"
    assert len(enemies) >= 5, f"{id}: needs at least 5 enemy spawns"
    items = list(elements)
    items += [el("Player Spawn Point", x, y) for x, y in players]
    items += [el("Weapon Spawner A", x, y) for x, y in weapons]
    items += [el("Rare Weapon Spawner", x, y) for x, y in rare]
    MAPS.append({
        "id": id, "name": name, "background": background, "lava": lava, "beginner": beginner,
        "elements": items, "enemies": [{"x": round(x, 2), "y": round(y, 2)} for x, y in enemies],
    })


def bar(x1, y1, x2, y2, o="Loooong Boi", length=202.0):
    """A Loooong Boi (or another long shape of the given unscaled length) from one point to the other."""
    dx, dy = x2 - x1, y2 - y1
    return el(o, (x1 + x2) / 2, (y1 + y2) / 2, math.degrees(math.atan2(dy, dx)), math.hypot(dx, dy) / length)


def quarter(cx, cy, quadrant, s=1.0, o="Quarter"):
    """A Quarter arc around (cx, cy) filling the quadrant "ul", "ur", "lr" or "ll"."""
    r = {"ul": 0.0, "ll": 90.0, "lr": 180.0, "ur": -90.0}[quadrant]
    c, sn = math.cos(math.radians(r)), math.sin(math.radians(r))
    ox, oy = 12.2 * s, -21.3 * s
    return el(o, cx - (ox * c - oy * sn), cy - (ox * sn + oy * c), r, s)


def honeycomb(cx, cy, s=1.0):
    """A breakable ice shelf centered on (cx, cy); about 95 x 34 at scale 1."""
    return el("HoneyComb", cx - 39.5 * s, cy + 12.9 * s, 0, s)


# ---------------------------------------------------------------- maps

def pipe(points, s=0.6, radius=23.0):
    """A thin pipe through the points with a Quarter elbow at each right-angle corner."""
    out = []
    ends = [points[0]]
    for i in range(1, len(points) - 1):
        (ax, ay), (cx, cy), (bx, by) = points[i - 1], points[i], points[i + 1]
        d1 = ((cx > ax) - (cx < ax), (cy > ay) - (cy < ay))
        d2 = ((bx > cx) - (bx < cx), (by > cy) - (by < cy))
        ends += [(cx - d1[0] * radius, cy - d1[1] * radius), (cx + d2[0] * radius, cy + d2[1] * radius)]
        quadrant = ("u" if d1[1] - d2[1] > 0 else "l") + ("r" if d1[0] - d2[0] > 0 else "l")
        out.append(quarter(cx + (d2[0] - d1[0]) * radius, cy + (d2[1] - d1[1]) * radius, quadrant, radius / 41.6))
    ends.append(points[-1])
    for (x1, y1), (x2, y2) in zip(ends[::2], ends[1::2]):
        length = math.hypot(x2 - x1, y2 - y1)
        out.append(el("Loooong Boi", (x1 + x2) / 2, (y1 + y2) / 2,
                      math.degrees(math.atan2(y2 - y1, x2 - x1)), length / 202.0, s))
    return out


def rotate(elements, cx, cy, deg):
    """The elements turned by deg degrees around (cx, cy)."""
    c, sn = math.cos(math.radians(deg)), math.sin(math.radians(deg))
    out = []
    for e in elements:
        m = dict(e)
        dx, dy = e["x"] - cx, e["y"] - cy
        m["x"], m["y"], m["r"] = round(cx + dx * c - dy * sn, 2), round(cy + dx * sn + dy * c, 2), round(e["r"] + deg, 2)
        out.append(m)
    return out


# Ruined arches with a broken deck on top; the arches give cover underneath
_arches = []
for ax in (-224, -75, 75, 224):
    _arches += [quarter(ax, -10, "ul", 1.8), quarter(ax, -10, "ur", 1.8)]
_pillars = []
for px, depth in ((-299, 4), (-150, 3), (0, 4), (150, 2), (299, 3)):
    _pillars += [el("Big Block", px, -31 - 41 * k, 0, 0.65) for k in range(depth)]
surge_map(
    "aqueduct", "Aqueduct", DESERT, LAVA_BOTTOM, beginner=True,
    elements=_arches + _pillars + [
        el("Loooong Boi", -245, 96), el("Loooong Boi", -95, 96, 0, 0.8),
        el("Loooong Boi", 120, 96, 0, 0.8), el("Loooong Boi", 262, 96, 0, 0.9),
        el("Platform", -150, 205, 0, 1.2), el("Platform", 150, 205, 0, 1.2), el("Pancake", 0, 265, 0, 0.9),
    ],
    players=[(-200, 128), (-60, 128), (90, 128), (230, 128)],
    enemies=[(-420, 60), (420, 60), (-300, 280), (300, 280), (0, 340), (-75, -140), (75, -140)],
    weapons=[(-150, 233), (150, 233), (262, 122)],
    rare=[(0, 291)],
)

# A W of long ramps; the valleys hold the players, the peaks split the map
surge_map(
    "zigzag", "Zigzag", SPACE, LAVA_BOTTOM, beginner=True,
    elements=[
        bar(-380, 130, -190, -40), bar(-190, -40, 0, 110), bar(0, 110, 190, -40), bar(190, -40, 380, 130),
        el("Platform", -190, 150, 0, 1.3), el("Platform", 190, 150, 0, 1.3),
        el("Ballish", -190, -40, 0, 0.55), el("Ballish", 0, 110, 0, 0.55), el("Ballish", 190, -40, 0, 0.55),
        el("Pancake", 0, 250, 0, 1.1), el("Ballish", -330, 260, 0, 0.8), el("Ballish", 330, 260, 0, 0.8),
    ],
    players=[(-235, 40), (-145, 32), (145, 32), (235, 40)],
    enemies=[(-420, -60), (420, -60), (-190, -150), (190, -150), (0, -60), (-330, 340), (330, 340), (0, 330)],
    weapons=[(-190, 0), (190, 0), (0, 280)],
    rare=[(0, 140)],
)

# One long staircase climbing from the bottom left to the top right
surge_map(
    "stairwell", "Stairwell", CAVE, LAVA_BOTTOM,
    elements=[el("Platform", -300 + 100 * k, -130 + 60 * k, 0, 1.3) for k in range(7)] + [
        el("Pancake", -260, 190, 0, 1.2), el("Pancake", 270, -110, 0, 1.2),
        el("Ballish", -330, 40, 0, 0.8), el("Ballish", 340, 90, 0, 0.8),
    ],
    players=[(-200, -35), (-100, 25), (0, 85), (100, 145)],
    enemies=[(-420, 180), (420, 220), (-150, 300), (0, 330), (150, -60), (-60, -170), (300, 340), (-410, -80)],
    weapons=[(-300, -101), (300, 259), (-260, 220)],
    rare=[(270, -80)],
)

# Shelves of breakable ice over one solid floor; every shot thins them out
surge_map(
    "thin_ice", "Thin Ice", FROSTY, LAVA_BOTTOM,
    elements=[
        el("Loooong Boi", 0, -110, 0, 1.4), el("Ballish", -330, -40, 0, 0.9), el("Ballish", 330, -40, 0, 0.9),
        honeycomb(-200, 0, 1.6), honeycomb(200, 0, 1.6), honeycomb(0, 100, 1.6),
        honeycomb(-230, 200, 1.2), honeycomb(230, 200, 1.2),
    ],
    players=[(-40, 147), (40, 147), (-80, -74), (80, -74)],
    enemies=[(-420, 100), (420, 100), (0, 300), (-250, -160), (250, -160), (-380, 300), (380, 300)],
    weapons=[(0, -80), (-230, 234)],
    rare=[(230, 234)],
)

# Desert floor with deadly cacti that burn enemies as well as spiders
surge_map(
    "cactus", "Cactus Garden", DESERT, LAVA_BOTTOM,
    elements=[
        el("Pancake", -170, -120, 0, 2), el("Pancake", 170, -120, 0, 2),
        el("DeathSpike", -200, -74, 0, 1.2), el("DeathSpike", 200, -74, 0, 1.2),
        el("Platform", -120, 40, 0, 1.4), el("Platform", 120, 40, 0, 1.4),
        el("Pancake", 0, 160, 0, 1.3), el("Ballish", -330, 120, 0, 0.9), el("Ballish", 330, 120, 0, 0.9),
    ],
    players=[(-100, -73), (100, -73), (-120, 76), (120, 76)],
    enemies=[(-420, 0), (420, 0), (-250, 280), (250, 280), (0, 320), (0, -170), (0, 60)],
    weapons=[(-330, 160), (330, 160)],
    rare=[(0, 192)],
)

# A volcano with a magma pool in its crater and ledges above the rim
surge_map(
    "caldera", "Caldera", MOUNTAIN, LAVA_BOTTOM,
    elements=[
        bar(-360, -150, -100, 40), bar(360, -150, 100, 40),
        el("Ballish", -100, 45, 0, 0.8), el("Ballish", 100, 45, 0, 0.8),
        bar(-100, 40, -75, -75), bar(100, 40, 75, -75),
        el("DeathZoneLongBoi", 0, -85, 0, 0.4),
        el("Platform", -200, 150, 0, 1.2), el("Platform", 200, 150, 0, 1.2), el("Pancake", 0, 200),
    ],
    players=[(-250, -26), (-150, 45), (150, 45), (250, -26)],
    enemies=[(-430, -50), (430, -50), (-300, 280), (300, 280), (0, 330), (0, 60), (-180, -120), (180, -120)],
    weapons=[(-200, 178), (200, 178)],
    rare=[(0, 227)],
)

# Broken slabs with staggered edges and open routes between the rubble
surge_map(
    "fallen_blocks", "Fallen Blocks", MULTIVERSE, LAVA_BOTTOM,
    elements=[
        el("Big Block", -40, -110, -12, 1.6, 0.8), el("Platform", 75, -95, 8, 1.5),
        el("Pentagon", -260, 5, 18, 1.1), el("Big Block", 250, 35, -22, 1.7, 0.9),
        el("Platform", -110, 180, -28, 1.7), el("Pentagon", 135, 205, 12, 1.1),
        el("Platform", -335, 190, 10, 0.9),
    ],
    players=[(-55, -65), (-20, -70), (60, -60), (90, -55)],
    enemies=[(-430, 100), (430, 120), (0, 80), (-300, 300), (330, 300), (0, 330), (-200, -150), (200, -140)],
    weapons=[(-260, 80), (250, 80)],
    rare=[(135, 280)],
)

# A broken truss: two abutments, an upper span, and a clear central crossing
surge_map(
    "truss_bridge", "Truss Bridge", DESOLATION, LAVA_BOTTOM,
    elements=[
        el("Loooong Boi", -230, -50, 0, 1.1), el("Loooong Boi", 230, -50, 0, 1.1),
        bar(-310, -50, -220, 120), bar(310, -50, 220, 120),
        el("Platform", -220, 140, 0, 1.4), el("Platform", 220, 140, 0, 1.4),
        el("Loooong Boi", 0, 190, 0, 1.1), el("Platform", 0, -95, 0, 1.3),
    ],
    players=[(-220, -18), (-160, -18), (160, -18), (220, -18)],
    enemies=[(-430, 120), (430, 120), (-150, -150), (150, -150), (0, 330), (-300, 280), (300, 280), (0, 50)],
    weapons=[(-220, 173), (220, 173), (0, 223)],
    rare=[(0, -60)],
)

# A bowl with jump pads at the bottom that throw spiders up into the fight
surge_map(
    "trampoline", "Trampoline", SPACE, LAVA_SIDES_BOTTOM,
    elements=[
        el("Pancake", 0, -130, 0, 1.6), bar(-90, -125, -300, 120), bar(90, -125, 300, 120),
        el("Jump Pad", -50, -114, 0, 1.5), el("Jump Pad", 50, -114, 0, 1.5),
        el("Platform", -130, 170, 0, 1.2), el("Platform", 130, 170, 0, 1.2), el("Ballish", 0, 250),
    ],
    players=[(-130, -30), (-200, 52), (130, -30), (200, 52)],
    enemies=[(-420, -50), (420, -50), (-400, 250), (400, 250), (0, 340), (0, 40), (-250, -150), (250, -150)],
    weapons=[(-130, 198), (130, 198)],
    rare=[(0, 293)],
)

# Floating rocks over the lava, with geysers that blow fallen spiders back up
surge_map(
    "geysers", "Geysers", DESOLATION, LAVA_BOTTOM,
    elements=[
        el("Pancake", -290, -40, 0, 1.2), el("Pancake", 0, -110, 0, 1.4), el("Pancake", 290, -40, 0, 1.2),
        el("Pancake", -150, 110), el("Pancake", 150, 110), el("Pancake", 0, 240, 0, 0.9),
        el("AeroTube", -150, -205, 0, 1.5), el("AeroTube", 150, -205, 0, 1.5),
    ],
    players=[(-290, -4), (-40, -71), (40, -71), (290, -4)],
    enemies=[(-430, 100), (430, 100), (-300, 250), (300, 250), (0, 340), (0, 40), (-420, -120), (420, -120)],
    weapons=[(-150, 137), (150, 137)],
    rare=[(0, 266)],
)

# Temple ruins: a long floor with standing, broken and fallen columns
surge_map(
    "colonnade", "Colonnade", MOUNTAIN, LAVA_BOTTOM, beginner=True,
    elements=[
        el("Loooong Boi", -222, -120, 0, 1.1), el("Loooong Boi", 0, -120, 0, 1.1), el("Loooong Boi", 222, -120, 0, 1.1),
        el("Loooong Boi", -260, -32, 90, 0.75), el("Loooong Boi", -90, -32, 90, 0.75),
        el("Loooong Boi", 90, -62, 90, 0.45), el("Loooong Boi", 260, -32, 90, 0.75),
        el("Loooong Boi", -175, 55, 0, 0.9), el("Triangle", -175, 104, 0, 1.6),
        el("Pancake", 260, 51, 0, 0.6), bar(130, -103, 245, -5),
        el("Pancake", 0, 190, 0, 1.1), el("Platform", 300, 200, 0, 1.1),
    ],
    players=[(-320, -87), (-175, -87), (0, -87), (320, -87)],
    enemies=[(-430, 40), (430, 40), (-300, 250), (150, 300), (0, 330), (0, -20), (420, 250)],
    weapons=[(-100, 80), (0, 219)],
    rare=[(260, 73)],
)

# Two jaws full of teeth, with lava above and below
surge_map(
    "jaws", "Jaws", CAVE, LAVA_TOP_BOTTOM,
    elements=[
        el("Loooong Boi", 0, -110, 0, 1.6), bar(-160, -110, -320, -20), bar(160, -110, 320, -20),
        el("Loooong Boi", 0, 260, 0, 1.6), bar(-160, 260, -320, 170), bar(160, 260, 320, 170),
        el("Spike", -150, -77, 0, 0.9), el("Spike", 0, -77, 0, 0.9), el("Spike", 150, -77, 0, 0.9),
        el("Spike", -75, 228, 180, 0.9), el("Spike", 75, 228, 180, 0.9),
        el("Spike", -210, 222, 180, 0.8), el("Spike", 210, 222, 180, 0.8),
        el("Pentagon", 0, 90, 0, 0.8), el("Ballish", -250, 90), el("Ballish", 250, 90),
    ],
    players=[(-75, -72), (75, -72), (-240, -33), (240, -33)],
    enemies=[(-420, 90), (420, 90), (-80, 30), (80, 30), (-300, 300), (300, 300), (0, 330), (-420, -120)],
    weapons=[(-110, -78), (110, -78)],
    rare=[(0, 140)],
)

# Four broken beams frame an open crossing instead of sealing off four wedges
surge_map(
    "crossbeams", "Crossbeams", MULTIVERSE, LAVA_BOTTOM,
    elements=[
        bar(-75, 5, -230, -110), bar(75, 5, 230, -110),
        bar(-75, 115, -230, 230), bar(75, 115, 230, 230),
        el("Pancake", 0, -120, 0, 1.3),
        el("Platform", -330, 150, 0, 1.2), el("Platform", 330, 150, 0, 1.2), el("Platform", 0, 250),
    ],
    players=[(-150, -20), (150, -20), (-40, -83), (40, -83)],
    enemies=[(-430, 60), (430, 60), (0, 30), (0, 150), (-200, 330), (200, 330), (-330, -150), (330, -150)],
    weapons=[(-330, 178), (330, 178)],
    rare=[(0, 275)],
)

# Broken ladder spines with alternating ledges; no closed cells between rungs
_ladders = []
for lx, lean in ((-200, -12), (200, 12)):
    ladder = [el("Loooong Boi", lx, y, 90, 0.65) for y in (-70, 140)]
    ladder += [el("Platform", lx + offset, y, 0, 1.3)
               for offset, y in ((-35, -120), (35, -35), (-35, 65), (35, 165))]
    _ladders += rotate(ladder, lx, 40, lean)
surge_map(
    "ladders", "Ladders", FROSTY, LAVA_TOP_BOTTOM,
    elements=_ladders + [el("Ballish", 0, -80, 0, 1.4), el("Pancake", 0, 80, 0, 1.2), el("Ballish", 0, 220, 0, 0.9)],
    players=[(-183, -15), (255, 15), (-30, 116), (30, 116)],
    enemies=[(-420, 60), (420, 60), (-420, 250), (420, 250), (0, 310), (0, -170), (-100, 300), (100, 300)],
    weapons=[(-140, 180), (208, 200)],
    rare=[(0, -26)],
)

# Broken pipe runs leave a second exit from every interior pocket
surge_map(
    "pipeworks", "Pipeworks", DESOLATION, LAVA_BOTTOM,
    elements=(
        pipe([(-340, 180), (-340, -90), (-100, -90)])
        + pipe([(100, -90), (340, -90), (340, 180)])
        + pipe([(-200, -20), (-200, 60), (-60, 60), (-60, 180)])
        + pipe([(120, -20), (120, 30), (260, 30)])
        + pipe([(-120, 240), (180, 240), (180, 150)])
        + pipe([(-340, 110), (-250, 110)]) + pipe([(340, 120), (260, 120)])
    ),
    players=[(-270, -63), (-140, -63), (140, -63), (260, -63)],
    enemies=[(-430, 40), (430, 40), (-200, -170), (200, -170), (0, 320), (-250, 280), (300, 300), (0, 140)],
    weapons=[(-130, 80), (190, 50)],
    rare=[(30, 260)],
)

# A satellite with long solar wings, a dish underneath and drifting debris
surge_map(
    "satellite", "Satellite", SPACE, LAVA_BOTTOM, beginner=True,
    elements=[
        el("Big Block", 0, 60, 0, 1.4), el("Loooong Boi", -185, 60, 0, 1.2), el("Loooong Boi", 185, 60, 0, 1.2),
        el("Spike", 0, 121, 0, 0.8), quarter(0, -20, "ll", 1.2), quarter(0, -20, "lr", 1.2),
        el("Ballish", -300, -100, 0, 1.1), el("Ballish", 310, -80, 0, 0.9),
        el("Ballish", -200, 220, 0, 0.8), el("Ballish", 230, 230),
        el("Platform", -120, -140, 15), el("Platform", 120, -150, -15),
    ],
    players=[(-120, 95), (-250, 95), (120, 95), (250, 95)],
    enemies=[(-430, 150), (430, 150), (0, 300), (-120, -50), (120, -50), (-350, 280), (350, 280), (0, -170)],
    weapons=[(-300, -54), (310, -40)],
    rare=[(10, -40)],
)


# A crescent shelters one side of an arena while its open mouth invites long swings
surge_map(
    "crescent", "Crescent", SPACE, LAVA_BOTTOM,
    elements=[
        quarter(-80, 65, "ul", 3.3), quarter(-80, 65, "ll", 3.3),
        el("Platform", -110, 55, 0, 1.3), el("Platform", -50, -95, 0, 1.4),
        el("Platform", 140, -45, -8, 1.6), el("Pentagon", 255, 120, 12, 1.2),
        el("Platform", 20, 235, 0, 1.4),
    ],
    players=[(-125, 88), (-95, 88), (120, -10), (155, -15)],
    enemies=[(-380, 65), (-330, 290), (0, 330), (400, 250), (410, 0), (220, -160), (0, 65)],
    weapons=[(-50, -60), (20, 270)],
    rare=[(255, 200)],
)

# A diagonal ravine: sheltered lower shelves and exposed high crossing points
surge_map(
    "faultline", "Faultline", CAVE, LAVA_BOTTOM,
    elements=[
        el("Pentagon", -240, -90, -15, 1.9), el("Pentagon", 225, -35, 20, 1.8),
        el("Platform", -120, 100, -12, 1.7), el("Platform", 100, 175, 12, 1.7),
        el("Platform", -310, 210, 0, 1.2), el("Platform", 315, 260, 0, 1.2),
        el("Platform", -35, -125, 0, 1.1),
    ],
    players=[(-260, 8), (-220, 22), (205, 72), (245, 55)],
    enemies=[(-430, 70), (430, 120), (-170, 300), (0, 320), (180, 315), (0, 0), (380, -150)],
    weapons=[(-120, 140), (100, 215)],
    rare=[(-35, -90)],
)

# A fragile direct crossing with durable islands and a permanent overhead detour
surge_map(
    "causeway", "Causeway", FROSTY, LAVA_BOTTOM,
    elements=[
        el("Pentagon", -270, -90, -8, 1.5), el("Pentagon", 270, -90, 8, 1.5),
        honeycomb(-125, -30, 1.4), honeycomb(15, -30, 1.4), honeycomb(155, -30, 1.1),
        el("Platform", -225, 95, 0, 1.3), el("Platform", 220, 95, 0, 1.3),
        el("Loooong Boi", 0, 195, 0, 1.0),
    ],
    players=[(-290, -5), (-250, -5), (250, -5), (290, -5)],
    enemies=[(-430, 80), (430, 80), (-300, 300), (300, 300), (0, 325), (0, 75), (0, -155)],
    weapons=[(-225, 130), (220, 130)],
    rare=[(15, 12)],
)

# Offset launch islands: pads feed an open upper lane, with a lower recovery ledge
surge_map(
    "slingshot", "Slingshot", DESERT, LAVA_BOTTOM,
    elements=[
        el("Platform", -285, -100, 0, 2.0), el("Platform", 275, -20, 0, 2.0),
        el("Jump Pad", -285, -78, 0, 1.5), el("Jump Pad", 275, 2, 0, 1.5),
        el("Platform", -100, 100, -10, 1.4), el("Platform", 100, 190, 10, 1.4),
        el("Pancake", 0, -115, 0, 1.2), el("Ballish", -290, 245, 0, 0.8),
    ],
    players=[(-325, -62), (-245, -62), (235, 18), (315, 18)],
    enemies=[(-430, 70), (430, 140), (-160, 300), (250, 315), (0, 325), (0, 20), (180, -150)],
    weapons=[(-100, 135), (100, 225)],
    rare=[(0, -80)],
)


def write(maps):
    os.makedirs(LAYOUTS, exist_ok=True)
    keep = {m["id"] + ".json" for m in MAPS}
    for name in os.listdir(LAYOUTS):
        if name.endswith(".json") and name not in keep:
            os.remove(os.path.join(LAYOUTS, name))
    for m in maps:
        with open(os.path.join(LAYOUTS, m["id"] + ".json"), "w", encoding="utf-8", newline="\n") as f:
            json.dump(m, f, indent=1)
            f.write("\n")


def solids(m, shapes):
    """World outlines of every solid collider in the map."""
    out = []
    for e in m["elements"]:
        for part in [e] + e.get("with", []):
            s = shapes.get(part["o"])
            if not s or part["o"] in ("Player Spawn Point", "Weapon Spawner A", "Rare Weapon Spawner"):
                continue
            rad = math.radians(part["r"])
            c, sn = math.cos(rad), math.sin(rad)
            for shape in s["shapes"]:
                if shape["trigger"] and not part["o"].startswith("DeathZone"):
                    continue
                pts = [(part["x"] + px * part["sx"] * c - py * part["sy"] * sn,
                        part["y"] + px * part["sx"] * sn + py * part["sy"] * c) for px, py in shape["outline"]]
                out.append((part["o"], pts))
    return out


def _inside(x, y, pts):
    hit = False
    for (ax, ay), (bx, by) in zip(pts, pts[1:] + pts[:1]):
        if (ay > y) != (by > y) and x < ax + (y - ay) * (bx - ax) / (by - ay):
            hit = not hit
    return hit


def _distance(x, y, pts):
    best = 1e9
    for (ax, ay), (bx, by) in zip(pts, pts[1:] + pts[:1]):
        dx, dy = bx - ax, by - ay
        t = max(0.0, min(1.0, ((x - ax) * dx + (y - ay) * dy) / (dx * dx + dy * dy or 1)))
        best = min(best, math.hypot(x - ax - t * dx, y - ay - t * dy))
    return best


def _ground(x, y, polys, reach):
    """Height above the first solid below (x, y), or None within reach."""
    for step in range(0, int(reach) + 1, 2):
        if any(_inside(x, y - step, pts) for _, pts in polys):
            return step
    return None


def check(maps, shapes):
    """Spawns must stand clear of solids: players and weapons on ground below, enemies in open air."""
    problems = 0
    for m in maps:
        unknown = {p["o"] for e in m["elements"] for p in [e] + e.get("with", []) if p["o"] not in shapes}
        if unknown:
            print(f'{m["id"]}: missing collider data for {", ".join(sorted(unknown))}'); problems += 1
            continue
        polys = solids(m, shapes)
        safe = [(o, p) for o, p in polys]
        if not safe:
            print(f'{m["id"]}: no solid ground'); problems += 1
            continue
        for e in m["elements"]:
            kind = e["o"]
            if kind not in ("Player Spawn Point", "Weapon Spawner A", "Rare Weapon Spawner"):
                continue
            x, y = e["x"], e["y"]
            if not (-470 < x < 470 and -190 < y < 350):
                print(f'{m["id"]}: {kind} at ({x}, {y}) is near the lava'); problems += 1
            if any(_inside(x, y, pts) for _, pts in safe) or min(_distance(x, y, pts) for _, pts in safe) < 8:
                print(f'{m["id"]}: {kind} at ({x}, {y}) is inside a solid'); problems += 1
                continue
            gap = _ground(x, y, safe, 60)
            if gap is None or gap > (45 if kind == "Player Spawn Point" else 35):
                print(f'{m["id"]}: {kind} at ({x}, {y}) has no ground close below (gap {gap})'); problems += 1
            under = [o for o, pts in safe if _inside(x, y - (gap or 0), pts)]
            if any(o.startswith("Death") for o in under):
                print(f'{m["id"]}: {kind} at ({x}, {y}) stands on a hazard'); problems += 1
        for p in m["enemies"]:
            d = min(_distance(p["x"], p["y"], pts) for _, pts in safe)
            if d < 45 or any(_inside(p["x"], p["y"], pts) for _, pts in safe):
                print(f'{m["id"]}: enemy spawn ({p["x"]}, {p["y"]}) is {d:.0f} from a solid'); problems += 1
            if not (-470 < p["x"] < 470 and -190 < p["y"] < 350):
                print(f'{m["id"]}: enemy spawn ({p["x"]}, {p["y"]}) is near the lava'); problems += 1
        for o, pts in polys:
            if any(not (-490 < x < 490 and -215 < y < 375) for x, y in pts):
                print(f'{m["id"]}: {o} reaches the lava'); problems += 1
                break
    return problems


def preview(maps, out_dir, shapes):
    """Draws each map with the prefab collider outlines from the Unity export (workspace tools only)."""
    import matplotlib
    matplotlib.use("Agg")
    import matplotlib.pyplot as plt
    from matplotlib.patches import Polygon
    for m in maps:
        fig, ax = plt.subplots(figsize=(10, 6.2))
        ax.add_patch(plt.Rectangle((-500, -222), 1000, 603, fill=False, ec="#ccc", ls="--"))
        ax.axhspan(-300, -222, color="#ffcc88")
        if m["lava"] in (LAVA_TOP_BOTTOM, LAVA_ALL):
            ax.axhspan(381, 460, color="#ffcc88")
        if m["lava"] in (LAVA_SIDES_BOTTOM, LAVA_ALL):
            ax.axvspan(-560, -500, color="#ffcc88"); ax.axvspan(500, 560, color="#ffcc88")
        for o, pts in solids(m, shapes):
            col = "#e03030" if o.startswith("Death") else "#4a7"
            ax.add_patch(Polygon(pts, closed=True, fc=col, ec="none", alpha=0.9))
        for e in m["elements"]:
            if e["o"] == "Player Spawn Point":
                ax.plot(e["x"], e["y"], "o", color="dodgerblue", ms=8)
            elif "Weapon Spawner" in e["o"]:
                ax.plot(e["x"], e["y"], "D", color="gold" if "Rare" in e["o"] else "#2a2", ms=6)
            elif e["o"] in ("Jump Pad", "AeroTube"):
                ax.plot(e["x"], e["y"], "^", color="#f80", ms=10)
        for p in m["enemies"]:
            ax.plot(p["x"], p["y"], "x", color="red", ms=9, mew=2)
        ax.set_xlim(-520, 520); ax.set_ylim(-260, 400); ax.set_aspect("equal")
        ax.set_title(f'{m["name"]} ({m["id"]})  bg={m["background"]} lava={m["lava"]}')
        fig.tight_layout()
        fig.savefig(os.path.join(out_dir, m["id"] + ".png"), dpi=60)
        plt.close(fig)


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--shapes", help="folder with shapes.json (from shapes.py); checks the spawns")
    parser.add_argument("--preview", action="store_true", help="also draws <id>.png next to shapes.json")
    parser.add_argument("--only", help="comma-separated map ids")
    args = parser.parse_args()
    requested = set(args.only.split(",")) if args.only else {m["id"] for m in MAPS}
    unknown = requested - {m["id"] for m in MAPS}
    if unknown:
        parser.error(f'unknown maps: {", ".join(sorted(unknown))}')
    chosen = [m for m in MAPS if m["id"] in requested]
    if args.preview and not args.shapes:
        parser.error("--preview requires --shapes")
    if args.shapes:
        shapes = json.load(open(os.path.join(args.shapes, "shapes.json"), encoding="utf-8"))
        problems = check(chosen, shapes)
        print(f"{problems} placement problems")
        if problems:
            sys.exit(1)
        if args.preview:
            preview(chosen, args.shapes, shapes)
    write(chosen)
    print(f"{len(chosen)} maps written to {LAYOUTS}")
