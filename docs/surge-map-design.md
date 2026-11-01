# Surge map design

The base game's black silhouettes, thin illuminated edges, subdued backgrounds and bright interactive objects are the reference. Its arenas usually have a strong overall shape with generous gaps for webs, thrown weapons and flying enemies. Symmetry is common, but it is not a requirement: the additions should offer useful movement choices rather than resemble detailed models of real structures.

The selection contains 14 arenas. Each earns its place through a recognizable silhouette or movement mechanic, rather than another arrangement of detached platforms.

| Arena | Distinct role |
|---|---|
| Aqueduct | Repeated arches support a broken upper deck, with cover underneath. |
| Zigzag | A continuous W of ramps creates alternating valleys and peaks. |
| Thin Ice | Breakable shelves disappear above a permanent recovery floor. |
| Cactus Garden | Exposed islands with lethal cacti make low footing hazardous. |
| Caldera | Outer slopes surround a central lava crater. |
| Trampoline | A deep bowl funnels players toward jump pads at its base. |
| Geysers | Air vents beneath floating shelves provide recovery from below. |
| Colonnade | A broad floor and uneven ruins offer grounded fighting and cover. |
| Jaws | Opposing toothed surfaces frame a horizontal arena with lava above and below. |
| Crossbeams | Four separated diagonal arms leave an open central crossing. |
| Ladders | Broken climbing spines and alternating ledges allow vertical travel and side exits. |
| Pipeworks | Interrupted pipes form pockets with multiple exits. |
| Satellite | Long wings divide upper and lower routes around a central body and dish. |
| Crescent | A large curved wall shelters one side while detached islands expose the other. |

Six overlapping layouts were removed from the previous 20-map selection:

| Removed | Reason |
|---|---|
| Fallen Blocks, Faultline, Slingshot | These and Crescent repeated the scattered-island arrangement. Crescent remains as the strongest silhouette; Trampoline already supplies jump-pad play. |
| Stairwell | Another sequence of detached footholds; Ladders gives climbing a clearer structure. |
| Truss Bridge | Its split bridge and upper crossing overlap the stronger architectural identity of Aqueduct. |
| Causeway | Its breakable crossing overlaps Thin Ice, which makes destruction the defining feature across several shelves. |

Base-game arenas remain in Surge's pool. These maps do not replace their geometry or change their behavior; normal Wave Survival and Versus keep their original pools.

![Geometry previews of the 14 retained Surge maps](images/surge-map-roster.png)

All 14 cards use consistent geometry previews: native collider silhouettes, subdued biome colors, orange hazards, cyan ice and pad/vent markers. These are diagrams, not gameplay captures; they omit scenery, weapons and players. Regenerating them from the layouts keeps the card's shape accurate after an edit.

## Authoring and validation

`surge/Maps/Tools/layouts.py` is the source for the embedded JSON. Extract current collider outlines with `shapes.py`, then run:

```powershell
python surge/Maps/Tools/shapes.py <scratch> --scenedump <workspace>/SceneDump
python surge/Maps/Tools/layouts.py --shapes <scratch>
python surge/Maps/Tools/thumbnails.py <scratch>
```

The checker rejects unknown collider data, unsupported spawn positions, solid overlaps, hazard footing, and insufficient enemy clearance. A failure returns a nonzero exit code before writing layouts. `--only id,id` validates and writes only those maps. `--preview` also draws collider diagrams and requires matplotlib.

The thumbnail renderer requires Pillow (available in the workspace's SceneDump environment), validates all maps before writing, and produces 320 × 180 PNGs. It changes only card artwork; the playable maps still use the game's actual prefab materials and backgrounds.

These are static placement checks. They do not establish reachability, pad trajectories, destruction behavior, camera framing or online synchronization; those require gameplay observation. Changes to an arena also require a matching thumbnail, since the card should show its actual shape.
