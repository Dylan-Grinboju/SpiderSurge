"""Extracts the collider outlines of the map editor's elements from the Unity export into <out>/shapes.json.

layouts.py --shapes <out> uses them to check spawn placement and draw previews.
Needs the workspace's SceneDump tools and Spiderheck_unityproject_steam export.
Run: py surge/Maps/Tools/shapes.py <out folder> [--scenedump <SceneDump folder>]
"""

import argparse
import json
import math
import os
import sys

WORKSPACE = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "..", "..", ".."))
NAMES = [
    "Platform", "Big Block", "Ballish", "Loooong Boi", "Triangle", "Pancake", "Quarter", "Spike", "Pentagon", "HoneyComb",
    "HoneycombSector", "DeathPlatform", "DeathBigBlock", "DeathBallish", "DeathLongBoi", "DeathTriangle", "DeathPancake",
    "DeathQuarter", "DeathSpike", "DeathPentagon", "Player Spawn Point", "Weapon Spawner A", "Rare Weapon Spawner",
    "Jump Pad", "AeroTube", "DeathZoneBlock", "DeathZoneBall", "DeathZoneLongBoi", "DeathZoneNarrowBlock",
    "DeathZoneTriangle", "DeathZoneSpike",
]


def extract(uy, lv):
    ctx = lv.Context()
    out = {}
    for name in NAMES:
        path = os.path.join(uy.ASSETS, "GameObject", name + ".prefab")
        if not os.path.exists(path):
            print("missing", name)
            continue
        f = uy.UnityFile(path)
        root = f.roots()[0]
        # Outlines are in the prefab's own space; the map file supplies position, rotation and scale
        t = f.transform_of[root]
        t.data["m_LocalPosition"] = {"x": 0, "y": 0, "z": 0}
        t.data["m_LocalRotation"] = {"x": 0, "y": 0, "z": 0, "w": 1}
        t.data["m_LocalScale"] = {"x": 1, "y": 1, "z": 1}
        f._world = {}
        shapes = []
        for d in f.docs.values():
            if d.cls not in uy.COLLIDERS_2D or str(d.data.get("m_Enabled")) == "0":
                continue
            trigger = str(d.data.get("m_IsTrigger")) == "1"
            for label, x0, x1, y0, y1, outline in lv.collider_shapes(f, d):
                if outline is None and label.startswith("Circle"):
                    cx, cy, r = (x0 + x1) / 2, (y0 + y1) / 2, (x1 - x0) / 2
                    outline = [(cx + r * math.cos(a / 12 * math.pi * 2), cy + r * math.sin(a / 12 * math.pi * 2)) for a in range(12)]
                elif outline is None:
                    outline = [(x0, y0), (x1, y0), (x1, y1), (x0, y1)]
                shapes.append({"trigger": trigger, "label": label, "outline": [[round(a, 2), round(b, 2)] for a, b in outline]})
        out[name] = {"shapes": shapes}
    return out


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("out", help="folder for shapes.json")
    parser.add_argument("--scenedump", default=os.path.join(WORKSPACE, "SceneDump"))
    args = parser.parse_args()
    sys.path.insert(0, args.scenedump)
    import levels as lv
    import unity_yaml as uy
    os.makedirs(args.out, exist_ok=True)
    with open(os.path.join(args.out, "shapes.json"), "w", encoding="utf-8") as f:
        json.dump(extract(uy, lv), f, indent=1)
    print(f"{len(NAMES)} elements written to {args.out}")
