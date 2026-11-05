"""Source of the Surge map layouts: writes surge/Maps/Layouts/<id>.json, and with --preview draws each map.

Coordinates are world units in the game's custom map scene (MapEditorBase). The playable box is about
x -500..500, y -222..381; the bottom lava starts at y -222. Element names are the map editor's prefab names.
Each map also gets a camera box fitted to its geometry, with the margins of the base-game arenas.
Run: py surge/Maps/Tools/layouts.py --shapes <folder from shapes.py> [--preview] [--only id,id]
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

# Vanilla endless survival averages seven weapons; 31 of its 34 arenas use six enemy points.
WEAPON_SPAWN_COUNTS = {"Weapon Spawner A": 6, "Rare Weapon Spawner": 1}
ENEMY_SPAWN_COUNT = 6


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


# Objects that keep their size when a map is scaled: spawns, jump pads (fixed force) and portals
FIXED_SIZE = ("Player Spawn Point", "Weapon Spawner A", "Rare Weapon Spawner", "Jump Pad",
              "PortalPoint", "PortalPoint Variant", "PortalLine", "PortalLine Variant")
# Each portal and its linked exit
PORTALS = {"PortalPoint": "PortalPoint Variant", "PortalLine": "PortalLine Variant"}
# A line portal is a 75-unit gate along its x axis
GATE_HALF = 37.5
SCALE_PIVOT = (0.0, -150.0)


def scaled(e, f):
    """The element moved toward SCALE_PIVOT by the factor f, and resized unless its size is fixed."""
    px, py = SCALE_PIVOT
    m = dict(e)
    m["x"], m["y"] = round(px + (e["x"] - px) * f, 2), round(py + (e["y"] - py) * f, 2)
    if e["o"] in ("Player Spawn Point", "Weapon Spawner A", "Rare Weapon Spawner"):
        m["y"] = round(m["y"] + 12 * (1 - f), 2)
    if e["o"] not in FIXED_SIZE:
        m["sx"], m["sy"] = round(e["sx"] * f, 3), round(e["sy"] * f, 3)
    if "with" in e:
        m["with"] = [scaled(p, f) for p in e["with"]]
    return m


def surge_map(id, name, background, lava, elements, players, enemies, weapons, rare=(), beginner=False, scale=1.0,
              zero_gravity=False, parts=()):
    """Coordinates are written at full size; scale shrinks the whole arena toward SCALE_PIVOT."""
    assert len(players) == 4, f"{id}: needs 4 player spawns"
    assert len(enemies) == ENEMY_SPAWN_COUNT, f"{id}: needs {ENEMY_SPAWN_COUNT} enemy spawns"
    px, py = SCALE_PIVOT
    enemies = [(px + (x - px) * scale, py + (y - py) * scale) for x, y in enemies]
    items = list(elements)
    items += [el("Player Spawn Point", x, y) for x, y in players]
    items += [el("Weapon Spawner A", x, y) for x, y in weapons]
    items += [el("Rare Weapon Spawner", x, y) for x, y in rare]
    parts = [dict(g) for g in parts]
    if scale != 1.0:
        items = [scaled(e, scale) for e in items]
        for g in parts:
            g["x"], g["y"] = round(px + (g["x"] - px) * scale, 2), round(py + (g["y"] - py) * scale, 2)
            g["s"] = round(g["s"] * scale, 3)
    MAPS.append({
        "id": id, "name": name, "background": background, "lava": lava, "beginner": beginner,
        "zeroGravity": zero_gravity, "elements": items, "parts": parts,
        "enemies": [{"x": round(x, 2), "y": round(y, 2)} for x, y in enemies],
    })


def bar(x1, y1, x2, y2, o="Loooong Boi", length=202.0, thick=None):
    """A Loooong Boi (or another long shape of the given unscaled length) from one point to the other.
    Its thickness scales with its length unless thick sets it."""
    dx, dy = x2 - x1, y2 - y1
    return el(o, (x1 + x2) / 2, (y1 + y2) / 2, math.degrees(math.atan2(dy, dx)), math.hypot(dx, dy) / length, thick)


def quarter(cx, cy, quadrant, s=1.0, o="Quarter"):
    """A Quarter arc around (cx, cy) filling the quadrant "ul", "ur", "lr" or "ll"."""
    r = {"ul": 0.0, "ll": 90.0, "lr": 180.0, "ur": -90.0}[quadrant]
    c, sn = math.cos(math.radians(r)), math.sin(math.radians(r))
    ox, oy = 12.2 * s, -21.3 * s
    return el(o, cx - (ox * c - oy * sn), cy - (ox * sn + oy * c), r, s)


def gate(x1, y1, x2, y2, r):
    """A pair of linked line portals, the vanilla arenas' visible gates. Both share one rotation, so whatever
    enters one leaves the other with its velocity, at the same point along the gate."""
    return el("PortalLine", x1, y1, r, with_=[el("PortalLine Variant", x2, y2, r)])


def gate_on(x1, y1, x2, y2, along, scale, clearance=4.0):
    """A gate standing across the top of the beam from (x1, y1) to (x2, y2), `along` units in from the first end.
    The gate keeps its size when the map is scaled, so its offset is worked out at the final scale."""
    length = math.hypot(x2 - x1, y2 - y1)
    ux, uy = (x2 - x1) / length, (y2 - y1) / length
    nx, ny = (-uy, ux) if ux > 0 else (uy, -ux)
    half_thick = 11.6 * length / 202.0 * scale
    offset = (GATE_HALF + half_thick + clearance) / scale
    px, py = x1 + ux * along, y1 + uy * along
    return px + nx * offset, py + ny * offset, math.degrees(math.atan2(ny, nx))


def hover(x, y, r=0.0, s=1.0):
    """A floating platform: solid, but it drifts when pushed or stood on."""
    return el("FloatingPlatformWide Variant", x, y, r, s)


def honeycomb(cx, cy, s=1.0):
    """A breakable ice shelf centered on (cx, cy); about 95 x 34 at scale 1."""
    return el("HoneyComb", cx - 39.5 * s, cy + 12.9 * s, 0, s)


# The base game's networked level gadgets, which the host spawns beside the editor elements (SurgeParts.cs).
# Each is placed by its prefab root, at the prefab's own scale unless s says otherwise.
GADGET_SCALE = {"Box A": 1, "Box B": 1, "Box Wide": 1, "Ball": 1, "Big Spinner": 1, "Spinner": 1.5, "Swing": 1,
                "SuspendedPrism": 2, "DescendingCage": 1.5, "Moving Platform A": 1, "Moving Platform B": 1,
                "HingePlatform": 2}
# Loose bodies, which only have to start clear; like the base game's, they stay lost until the level reloads
LOOSE = ("Box A", "Box B", "Box Wide", "Ball")
# Gadgets that hang under gravity, so they stay upright and need a gravity map
HANGING = ("Swing", "SuspendedPrism", "DescendingCage", "HingePlatform")
# Colliders that stay put while the rest of the gadget moves: anchors and hooks
ANCHORS = {"Swing": {0}, "SuspendedPrism": {2, 3, 4, 5, 6, 7}, "DescendingCage": {2, 3, 4, 5, 6, 7}}
# A touched rail platform rises this far along its root's up direction, at its prefab scale
RAIL_TRAVEL = {"Moving Platform A": 198.0, "Moving Platform B": 108.0}
# A descending cage hangs from 30 to 150 below its hooks at its prefab scale 1.5: about 128 of travel
CAGE_TRAVEL = 128.0


def gadget(o, x, y, r=0.0, s=None, **extra):
    """A gadget; extra may set motor (degrees per second, Big Spinner) or phase (1 starts a cage low)."""
    g = {"o": o, "x": round(x, 2), "y": round(y, 2), "r": round(r, 2), "s": round(GADGET_SCALE[o] if s is None else s, 3)}
    g.update(extra)
    return g


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


# Shelves of breakable ice over one solid floor; floor pads fire up through the gaps once the ice is gone
surge_map(
    "thin_ice", "Thin Ice", FROSTY, LAVA_BOTTOM, scale=0.6,
    elements=[
        el("Loooong Boi", 0, -110, 0, 1.4), el("Ballish", -330, -40, 0, 0.9), el("Ballish", 330, -40, 0, 0.9),
        honeycomb(-200, 0, 1.6), honeycomb(200, 0, 1.6), honeycomb(0, 100, 1.6),
        honeycomb(-230, 200, 1.2), honeycomb(230, 200, 1.2),
        el("Ballish", -110, 275, 0, 0.45), el("Ballish", 110, 275, 0, 0.45),
        el("Jump Pad", -100, -94), el("Jump Pad", 100, -94),
    ],
    players=[(-40, 147), (40, 147), (-45, -74), (45, -74)],
    enemies=[(-360, 100), (360, 100), (-250, -135), (250, -135), (-360, 265), (360, 265)],
    weapons=[(-55.0, -80.0), (55.0, -80.0), (-200.0, 38.0), (200.0, 38.0), (-240, 234), (240, 234)],
    rare=[(0, 147)],
    # Crates ride the shelves down when the ice breaks; a swing hangs between the top footholds
    parts=[
        gadget("Box A", -255, 40), gadget("Box A", 255, 40), gadget("Box B", -190, 236), gadget("Box B", 190, 236),
        gadget("Swing", 0, 305, s=0.6),
    ],
)

# Desert floor with deadly cacti that burn enemies as well as spiders
surge_map(
    "cactus", "Cactus Garden", DESERT, LAVA_BOTTOM, scale=0.52,
    elements=[
        el("Pancake", -170, -120, 0, 2), el("Pancake", 170, -120, 0, 2),
        el("DeathSpike", -200, -87, 0, 0.6), el("DeathSpike", 200, -87, 0, 0.6),
        el("Platform", -120, 40, 0, 1.4), el("Platform", 120, 40, 0, 1.4),
        el("Pancake", 0, 160, 0, 1.3), el("Ballish", -330, 120, 0, 0.9), el("Ballish", 330, 120, 0, 0.9),
    ],
    players=[(-100, -73), (100, -73), (-120, 76), (120, 76)],
    enemies=[(-365, 0), (365, 0), (-250, 235), (250, 235), (0, -199), (0, 267)],
    weapons=[(-330.0, 160.0), (330.0, 160.0), (-120.0, 76.0), (120.0, 76.0), (-100.0, -85.0), (100.0, -85.0)],
    rare=[(0, 192)],
    # Tumbleweeds roll off the outer islands, and a windmill turns over the central gap
    parts=[
        gadget("Ball", -262, -78), gadget("Ball", 262, -78), gadget("Big Spinner", 0, 20, motor=30),
    ],
)

# A volcano with a magma pool in its crater and ledges above the rim
surge_map(
    "caldera", "Caldera", MOUNTAIN, LAVA_BOTTOM, scale=0.58,
    elements=[
        bar(-360, -150, -100, 40), bar(360, -150, 100, 40),
        el("Ballish", -100, 45, 0, 0.8), el("Ballish", 100, 45, 0, 0.8),
        bar(-100, 40, -75, -75), bar(100, 40, 75, -75),
        el("DeathZoneLongBoi", 0, -85, 0, 0.4),
        el("Platform", -200, 150, 0, 1.2), el("Platform", 200, 150, 0, 1.2), el("Pancake", 0, 200),
    ],
    players=[(-250, -26), (-150, 45), (150, 45), (250, -26)],
    enemies=[(-370, -40), (370, -40), (-300, 265), (300, 265), (135, 235), (-17, 293)],
    weapons=[(-200, 178), (200, 178), (-305, -75), (305, -75), (-165, 30), (165, 30)],
    rare=[(0, 253)],
    # A lift rises out of the crater while something stands on it, and crates sit beside the rare weapon
    parts=[
        gadget("Moving Platform B", 0, -52), gadget("Box A", -41.2, 227.3), gadget("Box A", 41.2, 227.3),
    ],
)

# A bowl with jump pads at the bottom that throw spiders up into the fight
surge_map(
    "trampoline", "Trampoline", SPACE, LAVA_SIDES_BOTTOM, scale=0.55,
    elements=[
        el("Pancake", 0, -130, 0, 1.6), bar(-90, -125, -300, 120), bar(90, -125, 300, 120),
        el("Jump Pad", -50, -114), el("Jump Pad", 50, -114),
        el("Platform", -130, 170, 0, 1.2), el("Platform", 130, 170, 0, 1.2), el("Ballish", 0, 250),
    ],
    players=[(-130, -30), (-200, 52), (130, -30), (200, 52)],
    enemies=[(-315, -50), (315, -50), (-315, 250), (315, 250), (20, 361), (0, -20)],
    weapons=[(-130, 198), (130, 198), (-212, 68), (212, 68), (-90, -82), (90, -82)],
    rare=[(0, 293)],
    # A crate sits between the pads; the launch path above the bowl stays open
    parts=[
        gadget("Box B", 0, -94),
    ],
)

# Floating rocks over the lava: vents blow fallen spiders back up, and jump pads on the shelves above them launch higher
surge_map(
    "geysers", "Geysers", DESOLATION, LAVA_BOTTOM, scale=0.58,
    elements=[
        el("Pancake", -290, -40, 0, 1.2), el("Pancake", 0, -110, 0, 1.4), el("Pancake", 290, -40, 0, 1.2),
        el("Pancake", -150, 110), el("Pancake", 150, 110), el("Pancake", 0, 240, 0, 0.9),
        el("AeroTube", -150, -205, 0, 1.5), el("AeroTube", 150, -205, 0, 1.5),
        el("Jump Pad", -175, 121), el("Jump Pad", 175, 121),
    ],
    players=[(-285, -4), (-40, -71), (40, -71), (285, -4)],
    enemies=[(-370, 100), (370, 100), (-300, 250), (300, 250), (80, 326), (-80, 326)],
    weapons=[(-115.0, 137.0), (115.0, 137.0), (-285, -4), (285, -4), (-55, -78), (55, -78)],
    rare=[(0, 266)],
    # The vents juggle a ball each; a cage under the top rock sinks to the middle and rises again
    parts=[
        gadget("Ball", -150, 10), gadget("Ball", 150, 10), gadget("DescendingCage", 0, 198),
        gadget("Box A", -335, -12), gadget("Box A", 335, -12),
    ],
)

# Two toothed jaws, with lava above and below. The upper jaw breaks at its corners, its outer teeth and the
# lower middle tooth burn, and gates in the open sides wrap one side to the other
surge_map(
    "jaws", "Jaws", CAVE, LAVA_TOP_BOTTOM, scale=0.6,
    elements=[
        el("Loooong Boi", 0, -125, 0, 1.6), bar(-160, -125, -320, -35), bar(160, -125, 320, -35),
        el("Loooong Boi", 0, 285, 0, 240 / 202, 1.6),
        bar(-200, 262.5, -320, 195, thick=0.908), bar(200, 262.5, 320, 195, thick=0.908),
        el("Spike", -150, -92, 0, 0.9), el("DeathSpike", 0, -92, 0, 0.9), el("Spike", 150, -92, 0, 0.9),
        el("Spike", -75, 253, 180, 0.9), el("Spike", 75, 253, 180, 0.9),
        el("DeathSpike", -210, 247, 180, 0.8), el("DeathSpike", 210, 247, 180, 0.8),
        el("Ballish", -230, 90), el("Ballish", 230, 90),
        gate(-315, 80, 315, 80, 90),
    ],
    players=[(-80, -82), (80, -82), (-240, -48), (240, -48)],
    enemies=[(-150, 370), (150, 370), (-85, 30), (85, 30), (-300, 305), (300, 305)],
    weapons=[(-110.0, -93.0), (110.0, -93.0), (-230.0, 135.0), (230.0, 135.0), (-100, 317), (100, 317)],
    rare=[(0, 317)],
    # A turning tongue fills the middle of the mouth, with crates loose on the lower jaw
    parts=[
        gadget("Big Spinner", 0, 90, s=0.9, motor=-25), gadget("Box A", -35, -96), gadget("Box A", 35, -96),
    ],
)

# Four broken beams frame an open crossing; gates join each lower tip to the opposite upper tip, so sliding down
# a lower arm drops a spider onto the opposite upper arm
_crossbeams = 0.58
_lower_left = gate_on(-230, -110, -75, 5, 45, _crossbeams)
_upper_right = gate_on(230, 230, 75, 115, 45, _crossbeams)
surge_map(
    "crossbeams", "Crossbeams", MULTIVERSE, LAVA_BOTTOM, scale=_crossbeams,
    elements=[
        bar(-75, 5, -230, -110), bar(75, 5, 230, -110),
        bar(-75, 115, -230, 230), bar(75, 115, 230, 230),
        el("Pancake", 0, -120, 0, 1.3),
        el("Platform", -330, 150, 0, 1.2), el("Platform", 330, 150, 0, 1.2), el("Platform", 0, 250),
        gate(_lower_left[0], _lower_left[1], _upper_right[0], _upper_right[1], _lower_left[2]),
        gate(-_lower_left[0], _lower_left[1], -_upper_right[0], _upper_right[1], -_lower_left[2]),
    ],
    players=[(-141, -21), (141, -21), (-40, -83), (40, -83)],
    enemies=[(-375, 60), (375, 60), (-186, 85), (186, 85), (-143, 372), (143, 372)],
    weapons=[(-330.0, 178.0), (330.0, 178.0), (-56, -83), (56, -83), (-115, 169), (115, 169)],
    rare=[(0, 275)],
    # Balls roll down the upper arms; the central crossing stays open
    parts=[
        gadget("Ball", -212, 250), gadget("Ball", 212, 250),
    ],
)

# Broken pipe runs leave a second exit from every interior pocket; a vent blows up through the central gap,
# and jump pads fire up the clear shafts on each side
surge_map(
    "pipeworks", "Pipeworks", DESOLATION, LAVA_BOTTOM, scale=0.6,
    elements=(
        pipe([(-340, 180), (-340, -90), (-100, -90)])
        + pipe([(100, -90), (340, -90), (340, 180)])
        + pipe([(-200, -20), (-200, 60), (-60, 60), (-60, 180)])
        + pipe([(120, -20), (120, 30), (260, 30)])
        + pipe([(-120, 240), (180, 240), (180, 150)])
        + pipe([(-340, 110), (-250, 110)]) + pipe([(340, 120), (260, 120)])
        + [el("AeroTube", 0, -195, 0, 1.5), el("Jump Pad", -232, -84), el("Jump Pad", 230, 36)]
    ),
    players=[(-300, -68), (-175, -73), (140, -63), (260, -63)],
    enemies=[(-175, 150), (265, 205), (-120, -172), (30, -120), (-250, 280), (320, 345)],
    weapons=[(-125, 80), (200, 70), (-145, -65), (200.0, -65.0), (-290.0, 130.0), (290.0, 140.0)],
    rare=[(30, 260)],
    # A cage under the top pipe sinks into the central gap and rises again; loose crates fill the pockets
    parts=[
        gadget("DescendingCage", 0, 200), gadget("Ball", -175, 82), gadget("Box B", 150, 51),
    ],
)

# A satellite in zero gravity, with long solar wings, a dish underneath and debris that drifts when pushed
surge_map(
    "satellite", "Satellite", SPACE, LAVA_ALL, scale=0.6, zero_gravity=True,
    elements=[
        el("Big Block", 0, 60, 0, 1.4), el("Loooong Boi", -185, 60, 0, 1.2), el("Loooong Boi", 185, 60, 0, 1.2),
        el("Spike", 0, 121, 0, 0.8), quarter(0, -20, "ll", 1.2), quarter(0, -20, "lr", 1.2),
        el("Ballish", -300, -100, 0, 1.1), el("Ballish", 310, -80, 0, 0.9),
        el("Ballish", -200, 220, 0, 0.8), el("Ballish", 230, 230),
        hover(-120, -140, 12), hover(120, -150, -12),
    ],
    players=[(-120, 95), (-250, 95), (120, 95), (250, 95)],
    enemies=[(-340, 150), (340, 150), (0, 358), (-170, -30), (170, -30), (0, -165)],
    weapons=[(-300.0, -54.0), (310.0, -40.0), (-185.0, 95.0), (185.0, 95.0), (-200.0, 266.0), (230.0, 275.0)],
    rare=[(10, -40)],
    # A turning antenna above the body, and loose debris drifting around it
    parts=[
        gadget("Big Spinner", 0, 230, s=0.85, motor=20), gadget("Box Wide", -90, 175, r=20), gadget("Box A", 95, 170),
        gadget("Box B", -335, 30), gadget("Box A", 335, 40), gadget("Ball", -40, -110),
    ],
)

# A wrecked station hull in zero gravity: broken plates around a core, turbines still turning inside and
# cargo floating loose through the gaps
surge_map(
    "derelict", "Derelict", SPACE, LAVA_ALL, scale=0.62, zero_gravity=True,
    elements=[
        el("Loooong Boi", -160, 250, 0, 0.9), el("Loooong Boi", 185, 225, -15, 0.65),
        el("Loooong Boi", -160, -100, 0, 0.9), el("Loooong Boi", 160, -100, 0, 0.9),
        el("Loooong Boi", -300, 75, 90, 0.9), el("Loooong Boi", 300, 45, 75, 0.6),
        el("Big Block", 0, 75, 0, 0.9),
        el("Platform", -272, 75, 0, 0.6), el("Platform", 272, 45, 0, 0.6),
        hover(0, 205, 0, 0.8),
    ],
    players=[(-215, -85), (-134, -80), (120, -85), (215, -80)],
    enemies=[(-195, 336), (185, 141), (-95, 20), (5, -142), (-324, -94), (326, -78)],
    weapons=[(-165, -75), (165, -75), (-160, 275), (185, 250), (-272, 95), (272, 65)],
    rare=[(0, 118)],
    # A powered turbine and a smaller displaced free rotor, with cargo adrift and an open floor breach
    parts=[
        gadget("Big Spinner", -150, 120, s=0.7, motor=35), gadget("Big Spinner", 155, 55, r=25, s=0.55),
        gadget("Box A", -80, 200), gadget("Box B", 80, 195), gadget("Box Wide", -235, 150, r=30),
        gadget("Box A", 245, 125), gadget("Ball", -40, 300), gadget("Ball", 60, -30),
    ],
)


# A crescent shelters one side of an arena while its open mouth invites long swings; vents under the open side
# lift fallen spiders back to the low shelves
surge_map(
    "crescent", "Crescent", SPACE, LAVA_BOTTOM, scale=0.58,
    elements=[
        quarter(-80, 65, "ul", 3.3), quarter(-80, 65, "ll", 3.3),
        el("Platform", -110, 55, 0, 1.3), el("Platform", -50, -95, 0, 1.4),
        el("Platform", 140, -45, -8, 1.6), el("Pentagon", 255, 120, 12, 1.2),
        el("Platform", 20, 235, 0, 1.4),
        el("AeroTube", 60, -205, 0, 1.5), el("AeroTube", 250, -205, 0, 1.5),
    ],
    players=[(-125, 88), (-95, 88), (150, -20), (180, -24)],
    enemies=[(-260, 290), (-50, 321), (305, 250), (305, 5), (220, -135), (10, 65)],
    weapons=[(-50.0, -60.0), (20.0, 270.0), (-110.0, 90.0), (150, -20), (-110.0, 250.0), (-230.0, 175.0)],
    rare=[(255, 200)],
    # A swing hangs under the top shelf, and a spinner turns in the open side
    parts=[
        gadget("Swing", 20, 210, s=0.6), gadget("Big Spinner", 119, 56, s=0.6, motor=40),
    ],
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


def _crosses(a, b, c, d):
    def side(p, q, r):
        return (q[0] - p[0]) * (r[1] - p[1]) - (q[1] - p[1]) * (r[0] - p[0])
    return (side(c, d, a) > 0) != (side(c, d, b) > 0) and (side(a, b, c) > 0) != (side(a, b, d) > 0)


def _overlap(p, q):
    """Whether two outlines touch or one contains the other."""
    if (max(x for x, _ in p) < min(x for x, _ in q) or max(x for x, _ in q) < min(x for x, _ in p)
            or max(y for _, y in p) < min(y for _, y in q) or max(y for _, y in q) < min(y for _, y in p)):
        return False
    if _inside(*p[0], q) or _inside(*q[0], p):
        return True
    return any(_crosses(a, b, c, d) for a, b in zip(p, p[1:] + p[:1]) for c, d in zip(q, q[1:] + q[:1]))


def _poses(g):
    """The motions a gadget's moving colliders go through, as (angle, local pivot, local offset, world offset).
    The first is its rest pose."""
    o, s = g["o"], g["s"]
    if o in ("Big Spinner", "Spinner"):
        return [(a, (0, 0), (0, 0), (0, 0)) for a in range(0, 360, 15)]
    if o == "Swing":
        return [(a, (0.3, -0.9), (0, 0), (0, 0)) for a in (0, -60, -40, -20, 20, 40, 60)]
    if o == "HingePlatform":
        return [(a, (0, 23), (0, 0), (0, 0)) for a in (0, -70, -50, -30, -10, 10, 30, 50, 70)]
    if o == "SuspendedPrism":
        # Its chains are longer than the gap, so it sags below its hooks and sways
        return [(0, (0, 0), (0, -5.8), (wx, 0)) for wx in (0, -15, -7.5, 7.5, 15)]
    if o == "DescendingCage":
        travel = CAGE_TRAVEL * s / GADGET_SCALE[o] + 10
        return [(0, (0, 0), (0, 0), (0, -travel * t / 8)) for t in range(9)]
    if o in RAIL_TRAVEL:
        return [(0, (0, 0), (0, RAIL_TRAVEL[o] * t / 10), (0, 0)) for t in range(11)]
    return [(0, (0, 0), (0, 0), (0, 0))]


def gadget_shapes(g, shapes, pose=None):
    """World outlines of a gadget's solid colliders as (moving, outline), with its moving ones in the pose."""
    angle, (vx, vy), (lx, ly), (wx, wy) = pose or _poses(g)[0]
    anchors = ANCHORS.get(g["o"], set())
    c, sn = math.cos(math.radians(g["r"])), math.sin(math.radians(g["r"]))
    ca, sa = math.cos(math.radians(angle)), math.sin(math.radians(angle))
    out, seen = [], set()
    for i, shape in enumerate(shapes[g["o"]]["shapes"]):
        key = tuple(map(tuple, shape["outline"]))
        if shape["trigger"] or key in seen:
            continue
        seen.add(key)
        moving = i not in anchors
        pts = []
        for px, py in shape["outline"]:
            if moving:
                qx, qy = px - vx, py - vy
                px, py = vx + qx * ca - qy * sa + lx, vy + qx * sa + qy * ca + ly
            px, py = px * g["s"], py * g["s"]
            x, y = g["x"] + px * c - py * sn, g["y"] + px * sn + py * c
            pts.append((x + wx, y + wy) if moving else (x, y))
        out.append((moving, pts))
    return out


def sweep(g, shapes):
    """Every outline a gadget's colliders pass through."""
    out = [pts for pose in _poses(g) for moving, pts in gadget_shapes(g, shapes, pose) if moving]
    return out + [pts for moving, pts in gadget_shapes(g, shapes) if not moving]


def motion(g, shapes):
    """A cue for a gadget's motion: ("circle", x, y, radius) for spinners, swings and hinges, else ("path", points)."""
    poses = _poses(g)
    if g["o"] in ("Big Spinner", "Spinner", "Swing", "HingePlatform"):
        pv = poses[0][1]
        c, sn = math.cos(math.radians(g["r"])), math.sin(math.radians(g["r"]))
        cx, cy = g["x"] + (pv[0] * c - pv[1] * sn) * g["s"], g["y"] + (pv[0] * sn + pv[1] * c) * g["s"]
        reach = max(math.hypot(x - cx, y - cy) for moving, pts in gadget_shapes(g, shapes) if moving for x, y in pts)
        return "circle", cx, cy, reach
    centers = []
    for pose in poses:
        pts = [p for moving, outline in gadget_shapes(g, shapes, pose) if moving for p in outline]
        centers.append((sum(x for x, _ in pts) / len(pts), sum(y for _, y in pts) / len(pts)))
    return "path", sorted(centers, key=lambda p: (p[1], p[0]))


HOVER = "FloatingPlatformWide Variant"
SPAWNS = ("Player Spawn Point", "Weapon Spawner A", "Rare Weapon Spawner")

# The base-game arenas' median camera box reaches about this far past their geometry (sides, below, above)
CAMERA_SIDE, CAMERA_BELOW, CAMERA_ABOVE = 25, 30, 65
# The custom map scene's own camera box, which the fitted one never exceeds
SCENE_CAMERA = (-465, 465, -185, 345)
# The widest player spawn spread among the base-game arenas is 511
MAX_PLAYER_SPREAD = 500


def camera(m, shapes):
    """The camera box for the map: its solids and vents plus the base-game margins, inside the scene's box."""
    pts = [p for o, pts in solids(m, shapes) for p in pts]
    pts += [(e["x"], e["y"]) for e in m["elements"] if e["o"] == "AeroTube"]
    xs, ys = [x for x, _ in pts], [y for _, y in pts]
    x0, x1, y0, y1 = SCENE_CAMERA
    return {"xmin": round(max(x0, min(xs) - CAMERA_SIDE), 1), "xmax": round(min(x1, max(xs) + CAMERA_SIDE), 1),
            "ymin": round(max(y0, min(ys) - CAMERA_BELOW), 1), "ymax": round(min(y1, max(ys) + CAMERA_ABOVE), 1)}


def check_gadgets(m, safe):
    """Pads need footing; portals come in pairs, stay off beginner maps and drop their exits onto solid ground.
    Gates are paired with an exit of the same rotation, so velocity carries straight through."""
    problems = 0
    spawns = [(e["x"], e["y"]) for e in m["elements"] if e["o"] in SPAWNS]
    enemies = [(p["x"], p["y"]) for p in m["enemies"]]
    ends = []
    for e in m["elements"]:
        if e["o"] == "Jump Pad":
            gap = _ground(e["x"], e["y"] - 1, safe, 8)
            if gap is None:
                print(f'{m["id"]}: Jump Pad at ({e["x"]}, {e["y"]}) has no footing'); problems += 1
            if any(math.hypot(x - e["x"], y - e["y"]) < 30 for x, y in spawns):
                print(f'{m["id"]}: Jump Pad at ({e["x"]}, {e["y"]}) is under a spawn'); problems += 1
        if e["o"] in PORTALS:
            pair = [p for p in e.get("with", []) if p["o"] == PORTALS[e["o"]]]
            if len(pair) != 1:
                print(f'{m["id"]}: portal at ({e["x"]}, {e["y"]}) needs exactly one linked exit'); problems += 1
            elif e["o"] == "PortalLine" and abs(pair[0]["r"] - e["r"]) > 0.01:
                print(f'{m["id"]}: gate at ({e["x"]}, {e["y"]}) turns what passes through it, unlike its exit'); problems += 1
            ends += [e] + pair
        elif e["o"] in PORTALS.values():
            print(f'{m["id"]}: portal exit at ({e["x"]}, {e["y"]}) is not linked'); problems += 1
    if ends and m["beginner"]:
        print(f'{m["id"]}: beginner maps get no portals'); problems += 1
    for e in ends:
        x, y = e["x"], e["y"]
        line = e["o"].startswith("PortalLine")
        # A point portal is checked at its center; a gate along its whole length, which may stand on a beam
        points = gate_points(e) if line else [(x, y)]
        if any(not (-440 < px < 440 and -150 < py < 330) for px, py in points):
            print(f'{m["id"]}: portal at ({x}, {y}) is near the lava'); problems += 1
        if any(_inside(px, py, pts) or _distance(px, py, pts) < (3 if line else 20) for px, py in points for _, pts in safe):
            print(f'{m["id"]}: portal at ({x}, {y}) is jammed against a solid'); problems += 1
        near = 45 if line else 70
        if any(math.hypot(sx - px, sy - py) < near for sx, sy in spawns for px, py in points):
            print(f'{m["id"]}: portal at ({x}, {y}) is too close to a player or weapon spawn'); problems += 1
        if any(math.hypot(sx - px, sy - py) < near - 10 for sx, sy in enemies for px, py in points):
            print(f'{m["id"]}: portal at ({x}, {y}) is too close to an enemy spawn'); problems += 1
        # Velocity carries through, so a spider falling into one end lands below the other
        lx, ly = min(points, key=lambda p: p[1])
        gap = _ground(lx, ly, safe, 80)
        under = [o for o, pts in safe if gap is not None and _inside(lx, ly - gap, pts)]
        if gap is None or any(o.startswith("Death") or o == HOVER for o in under):
            print(f'{m["id"]}: portal at ({x}, {y}) has no solid footing below'); problems += 1
    return problems


def check_parts(m, shapes, safe, box):
    """Gadgets must move clear of the solids, each other, spawns, portals and pads, and stay in the camera box.
    Loose boxes and balls only have to start clear, above footing on a gravity map."""
    problems = 0
    spawns = [(e["x"], e["y"]) for e in m["elements"] if e["o"] in SPAWNS]
    enemies = [(p["x"], p["y"]) for p in m["enemies"]]
    gates = [q for e in m["elements"] if e["o"] in PORTALS for p in [e] + e.get("with", [])
             for q in (gate_points(p) if p["o"].startswith("PortalLine") else [(p["x"], p["y"])])]
    pads = [(e["x"], e["y"]) for e in m["elements"] if e["o"] in ("Jump Pad", "AeroTube")]
    keep_clear = ([(p, 22, "a player or weapon spawn") for p in spawns] + [(p, 30, "an enemy spawn") for p in enemies]
                  + [(p, 12, "a portal") for p in gates] + [(p, 15, "a pad or vent") for p in pads])
    swept = []
    for g in m.get("parts", []):
        label = f'{m["id"]}: {g["o"]} at ({g["x"]}, {g["y"]})'
        if g["o"] not in GADGET_SCALE or g["o"] not in shapes:
            print(f"{label} is not a known gadget"); problems += 1
            continue
        if g["o"] in HANGING and (m["zeroGravity"] or abs(g["r"]) > 0.01):
            print(f"{label} hangs, so it needs gravity and no rotation"); problems += 1
        if "motor" in g and g["o"] != "Big Spinner":
            print(f"{label} has no motor to drive"); problems += 1
        polys = sweep(g, shapes)
        if g["o"] in LOOSE:
            if any(_overlap(p, q) for p in polys for _, q in safe):
                print(f"{label} starts inside a solid"); problems += 1
            low = min(y for p in polys for _, y in p)
            vent = any(e["o"] == "AeroTube" and abs(e["x"] - g["x"]) < 25 * e["sx"] and e["y"] < g["y"] for e in m["elements"])
            if not m["zeroGravity"] and not vent and _ground(g["x"], low - 1, safe, 60) is None:
                print(f"{label} has no footing to land on"); problems += 1
        else:
            hit = next((o for p in polys for o, q in safe if _overlap(p, q)), None)
            if hit:
                print(f"{label} runs into a {hit}"); problems += 1
            if any(not (box["xmin"] <= x <= box["xmax"] and box["ymin"] <= y <= box["ymax"]) for p in polys for x, y in p):
                print(f"{label} moves out of the camera box"); problems += 1
        for (x, y), need, what in keep_clear:
            if any(_inside(x, y, p) or _distance(x, y, p) < need for p in polys):
                print(f"{label} comes within {need} of {what} at ({x:.0f}, {y:.0f})"); problems += 1
        for other, others in swept:
            if any(_overlap(p, q) for p in polys for q in others):
                print(f'{label} collides with the {other["o"]} at ({other["x"]}, {other["y"]})'); problems += 1
        swept.append((g, polys))
    return problems


def gate_points(e, count=9):
    """Points along a gate, end to end."""
    c, sn = math.cos(math.radians(e["r"])), math.sin(math.radians(e["r"]))
    half = GATE_HALF * e["sx"]
    return [(e["x"] + c * half * t, e["y"] + sn * half * t) for t in (2 * i / (count - 1) - 1 for i in range(count))]


def check_spawn_distribution(m):
    problems = 0
    weapons = [e for e in m["elements"] if e["o"] in WEAPON_SPAWN_COUNTS]
    regular = [e for e in weapons if e["o"] == "Weapon Spawner A"]
    players = [e for e in m["elements"] if e["o"] == "Player Spawn Point"]
    for kind, count in WEAPON_SPAWN_COUNTS.items():
        actual = sum(e["o"] == kind for e in weapons)
        if actual != count:
            print(f'{m["id"]}: needs {count} {kind} points, has {actual}'); problems += 1
    if len(m["enemies"]) != ENEMY_SPAWN_COUNT:
        print(f'{m["id"]}: needs {ENEMY_SPAWN_COUNT} enemy spawn points, has {len(m["enemies"])}'); problems += 1
    for label, points, clearance in (("weapon", weapons, 60), ("enemy", m["enemies"], 90)):
        for i, a in enumerate(points):
            for b in points[i + 1:]:
                if math.hypot(a["x"] - b["x"], a["y"] - b["y"]) < clearance:
                    print(f'{m["id"]}: {label} spawn points must be at least {clearance} apart'); problems += 1
    if players and regular and min(e["y"] for e in regular) > min(p["y"] for p in players) + 30:
        print(f'{m["id"]}: no weapon supply near the lowest player route'); problems += 1
    for p in m["enemies"]:
        if any(math.hypot(p["x"] - e["x"], p["y"] - e["y"]) < 60 for e in players):
            print(f'{m["id"]}: enemy spawn is within 60 of a player spawn'); problems += 1
    return problems


def check(maps, shapes):
    """Spawns must stand clear of solids: players and weapons on ground below, enemies in open air."""
    problems = 0
    for m in maps:
        problems += check_spawn_distribution(m)
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
            if HOVER in under:
                print(f'{m["id"]}: {kind} at ({x}, {y}) stands on a floating platform that drifts'); problems += 1
        problems += check_gadgets(m, safe)
        players = [e["x"] for e in m["elements"] if e["o"] == "Player Spawn Point"]
        if max(players) - min(players) > MAX_PLAYER_SPREAD:
            print(f'{m["id"]}: player spawns are {max(players) - min(players):.0f} apart (at most {MAX_PLAYER_SPREAD})'); problems += 1
        box = camera(m, shapes)
        problems += check_parts(m, shapes, safe, box)
        for p in m["enemies"]:
            # Enemies appear where the camera can show them, as on the base-game arenas
            if not (box["xmin"] < p["x"] < box["xmax"] and box["ymin"] < p["y"] < box["ymax"]):
                print(f'{m["id"]}: enemy spawn ({p["x"]}, {p["y"]}) is outside the camera box {box}'); problems += 1
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
        box = m.get("camera") or camera(m, shapes)
        ax.add_patch(plt.Rectangle((box["xmin"], box["ymin"]), box["xmax"] - box["xmin"], box["ymax"] - box["ymin"],
                                   fill=False, ec="#88f", ls=":"))
        ax.axhspan(-300, -222, color="#ffcc88")
        if m["lava"] in (LAVA_TOP_BOTTOM, LAVA_ALL):
            ax.axhspan(381, 460, color="#ffcc88")
        if m["lava"] in (LAVA_SIDES_BOTTOM, LAVA_ALL):
            ax.axvspan(-560, -500, color="#ffcc88"); ax.axvspan(500, 560, color="#ffcc88")
        for o, pts in solids(m, shapes):
            col = "#e03030" if o.startswith("Death") else "#48c" if o == HOVER else "#4a7"
            ax.add_patch(Polygon(pts, closed=True, fc=col, ec="none", alpha=0.9))
        for e in m["elements"]:
            if e["o"] == "Player Spawn Point":
                ax.plot(e["x"], e["y"], "o", color="dodgerblue", ms=8)
            elif "Weapon Spawner" in e["o"]:
                ax.plot(e["x"], e["y"], "D", color="gold" if "Rare" in e["o"] else "#2a2", ms=6)
            elif e["o"] in ("Jump Pad", "AeroTube"):
                ax.plot(e["x"], e["y"], "^", color="#f80", ms=10)
            elif e["o"] in PORTALS:
                for p in [e] + e.get("with", []):
                    if p["o"].startswith("PortalLine"):
                        ends = gate_points(p, 2)
                        ax.plot([q[0] for q in ends], [q[1] for q in ends], color="#e6b800", lw=3)
                    else:
                        ax.add_patch(plt.Circle((p["x"], p["y"]), 10, fill=False, ec="#e6b800", lw=2))
                        ax.add_patch(plt.Circle((p["x"], p["y"]), 15, fill=False, ec="#e6b800", lw=0.8, ls=":"))
                for p in e.get("with", []):
                    ax.plot([e["x"], p["x"]], [e["y"], p["y"]], ":", color="#e6b800", lw=1)
        for g in m.get("parts", []):
            for pts in sweep(g, shapes):
                ax.add_patch(Polygon(pts, closed=True, fill=False, ec="#b07cff", lw=0.4, alpha=0.5))
            for _, pts in gadget_shapes(g, shapes):
                ax.add_patch(Polygon(pts, closed=True, fc="#8a4fff", ec="none", alpha=0.9))
        for p in m["enemies"]:
            ax.plot(p["x"], p["y"], "x", color="red", ms=9, mew=2)
        ax.set_xlim(-520, 520); ax.set_ylim(-260, 400); ax.set_aspect("equal")
        ax.set_title(f'{m["name"]} ({m["id"]})  bg={m["background"]} lava={m["lava"]}')
        fig.tight_layout()
        fig.savefig(os.path.join(out_dir, m["id"] + ".png"), dpi=60)
        plt.close(fig)


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--shapes", required=True, help="folder with shapes.json (from shapes.py)")
    parser.add_argument("--preview", action="store_true", help="also draws <id>.png next to shapes.json")
    parser.add_argument("--only", help="comma-separated map ids")
    args = parser.parse_args()
    requested = set(args.only.split(",")) if args.only else {m["id"] for m in MAPS}
    unknown = requested - {m["id"] for m in MAPS}
    if unknown:
        parser.error(f'unknown maps: {", ".join(sorted(unknown))}')
    chosen = [m for m in MAPS if m["id"] in requested]
    if not args.shapes:
        parser.error("--shapes is required: the camera boxes are fitted to the collider outlines")
    shapes = json.load(open(os.path.join(args.shapes, "shapes.json"), encoding="utf-8"))
    problems = check(chosen, shapes)
    print(f"{problems} placement problems")
    if problems:
        sys.exit(1)
    for m in chosen:
        m["camera"] = camera(m, shapes)
    if args.preview:
        preview(chosen, args.shapes, shapes)
    write(chosen)
    print(f"{len(chosen)} maps written to {LAYOUTS}")
